// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OData.Mcp.Core.Constants;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.OData.Mcp.Core.Parsing;

namespace Microsoft.OData.Mcp.Core.Diagnostics
{

    /// <summary>
    /// Answers four questions about an OData service with three requests: is <c>$metadata</c> reachable, is the
    /// data reachable, is either of them secured, and does a real row look like the model says it should.
    /// </summary>
    /// <remarks>
    /// The probe uses whatever <see cref="HttpClient"/> it is given. Pass a plain client to learn what an anonymous
    /// caller sees; pass the authenticated OData client to learn what the catalog will see. It never throws for a
    /// service that misbehaves; every failure lands in a <see cref="ProbeStep"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// var probe = new ServiceProbe(httpClient, new CsdlParser());
    /// var result = await probe.ProbeAsync(new Uri("https://services.odata.org/V4/Northwind/Northwind.svc/"), ct);
    /// Console.WriteLine(result.Verdict); // Ready
    /// </code>
    /// </example>
    public sealed class ServiceProbe
    {

        #region Fields

        /// <summary>
        /// The most characters of an unexpected body that a step detail quotes.
        /// </summary>
        public const int SnippetLength = 80;

        private readonly HttpClient _client;
        private readonly CsdlParser _parser;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceProbe"/> class.
        /// </summary>
        /// <param name="client">The client every request goes through.</param>
        /// <param name="parser">The CSDL parser.</param>
        public ServiceProbe(HttpClient client, CsdlParser parser)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(parser);

            _client = client;
            _parser = parser;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Picks the entity set to sample: the first one the container declares whose type the model resolves.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns>
        /// The set and its type, or <c>null</c> when the model declares no usable set.
        /// </returns>
        public static (EdmEntitySet Set, EdmEntityType Type)? ChooseEntitySet(EdmModel model)
        {
            ArgumentNullException.ThrowIfNull(model);

            foreach (var set in model.AllEntitySets)
            {
                var type = model.GetEntityType(set.EntityTypeName, set.EntityTypeNamespace);
                if (type is not null)
                {
                    return (set, type);
                }
            }

            return null;
        }

        /// <summary>
        /// Checks a data response body against the model: is it JSON, is it an OData collection, and do the first
        /// row's property names belong to the type.
        /// </summary>
        /// <param name="type">The entity type behind the sampled set.</param>
        /// <param name="json">The response body.</param>
        /// <returns>
        /// <see cref="ProbeOutcome.Passed"/> when the row's properties are declared on the type (an empty set also
        /// passes, with a note); <see cref="ProbeOutcome.Unreadable"/> when the body is not JSON, has no
        /// <c>value</c> array, or shares no property names with the type.
        /// </returns>
        public static ProbeStep InterpretRow(EdmEntityType type, string json)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(json);

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json);
            }
            catch (JsonException exception)
            {
                return new ProbeStep(ProbeOutcome.Unreadable, $"body is not JSON ({exception.Message.Split('.')[0]}): {Snippet(json)}");
            }

            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
                {
                    return new ProbeStep(ProbeOutcome.Unreadable, $"JSON has no value array, so it is not an OData collection: {Snippet(json)}");
                }

                if (value.GetArrayLength() == 0)
                {
                    return new ProbeStep(ProbeOutcome.Passed, $"{type.Name} set is empty; row shape not checked");
                }

                var row = value[0];
                if (row.ValueKind != JsonValueKind.Object)
                {
                    return new ProbeStep(ProbeOutcome.Unreadable, $"first value entry is a {row.ValueKind}, not an object");
                }

                var declared = new HashSet<string>(type.Properties.Select(property => property.Name).Concat(type.NavigationProperties.Select(navigation => navigation.Name)), StringComparer.Ordinal);
                var names = row.EnumerateObject().Select(property => property.Name).Where(name => !name.StartsWith('@') && !name.Contains("@odata.", StringComparison.Ordinal)).ToList();
                var unknown = names.Where(name => !declared.Contains(name)).ToList();
                if (names.Count == 0)
                {
                    return new ProbeStep(ProbeOutcome.Unreadable, $"first {type.Name} row carries no properties, only annotations");
                }

                if (unknown.Count == names.Count)
                {
                    return new ProbeStep(ProbeOutcome.Unreadable, $"none of the {names.Count} properties on the first row are declared on {type.FullName}: {string.Join(", ", unknown.Take(5))}");
                }

                if (unknown.Count > 0)
                {
                    return new ProbeStep(ProbeOutcome.Passed, $"1 {type.Name} row; {names.Count - unknown.Count} of {names.Count} properties match the model, unknown: {string.Join(", ", unknown.Take(5))}");
                }

                return new ProbeStep(ProbeOutcome.Passed, $"1 {type.Name} row; all {names.Count} properties match the model");
            }
        }

        /// <summary>
        /// Runs all three steps.
        /// </summary>
        /// <param name="serviceRoot">The service root; a trailing slash is added when missing.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The result. Never throws for a misbehaving service.
        /// </returns>
        public async Task<ServiceProbeResult> ProbeAsync(Uri serviceRoot, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(serviceRoot);

            var root = WithTrailingSlash(serviceRoot);
            var (metadata, model) = await ProbeMetadataAsync(root, cancellationToken).ConfigureAwait(false);
            if (model is null)
            {
                return new ServiceProbeResult(root, metadata, ProbeStep.NotRun, ProbeStep.NotRun, null, null, null);
            }

            var data = await ProbeDataAsync(root, model, cancellationToken).ConfigureAwait(false);

            return new ServiceProbeResult(root, metadata, data.Data, data.Results, model, data.EntitySet, data.EntityType);
        }

        /// <summary>
        /// Runs steps 2 and 3 against a model that is already parsed, for hosts that fetched <c>$metadata</c> themselves.
        /// </summary>
        /// <param name="serviceRoot">The service root.</param>
        /// <param name="model">The parsed model.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// A result whose metadata step is recorded as passed.
        /// </returns>
        public async Task<ServiceProbeResult> ProbeWithModelAsync(Uri serviceRoot, EdmModel model, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(serviceRoot);
            ArgumentNullException.ThrowIfNull(model);

            var root = WithTrailingSlash(serviceRoot);
            var metadata = new ProbeStep(ProbeOutcome.Passed, DescribeModel(model), (int)HttpStatusCode.OK);
            var data = await ProbeDataAsync(root, model, cancellationToken).ConfigureAwait(false);

            return new ServiceProbeResult(root, metadata, data.Data, data.Results, model, data.EntitySet, data.EntityType);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Summarizes a parsed model for the metadata step detail.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns>
        /// For example <c>26 entity sets, 26 entity types, 0 operations</c>.
        /// </returns>
        internal static string DescribeModel(EdmModel model)
        {
            var operations = model.Actions.Count + model.Functions.Count;

            return $"{model.AllEntitySets.Count()} entity sets, {model.EntityTypes.Count} entity types, {operations} operations";
        }

        /// <summary>
        /// Maps a non-success response onto a step, capturing <c>WWW-Authenticate</c> on 401 and 403.
        /// </summary>
        /// <param name="what">What was requested, for the detail line.</param>
        /// <param name="response">The response.</param>
        /// <returns>
        /// The step.
        /// </returns>
        internal static ProbeStep FromFailure(string what, HttpResponseMessage response)
        {
            var status = (int)response.StatusCode;
            var challenges = response.Headers.WwwAuthenticate.Select(value => value.ToString()).ToList();
            var outcome = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => ProbeOutcome.Unauthorized,
                HttpStatusCode.Forbidden => ProbeOutcome.Forbidden,
                HttpStatusCode.NotFound => ProbeOutcome.NotFound,
                _ => ProbeOutcome.Failed
            };
            var challenge = challenges.Count > 0 ? $"; WWW-Authenticate: {string.Join(" | ", challenges)}" : string.Empty;

            return new ProbeStep(outcome, $"{what} -> {status} {response.ReasonPhrase}{challenge}", status, challenges);
        }

        /// <summary>
        /// Steps 2 and 3: fetch one row from the chosen set and interpret it.
        /// </summary>
        /// <param name="root">The service root with a trailing slash.</param>
        /// <param name="model">The parsed model.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The data and results steps plus what was sampled.
        /// </returns>
        internal async Task<(ProbeStep Data, ProbeStep Results, string? EntitySet, string? EntityType)> ProbeDataAsync(Uri root, EdmModel model, CancellationToken cancellationToken)
        {
            var chosen = ChooseEntitySet(model);
            if (chosen is null)
            {
                return (new ProbeStep(ProbeOutcome.Skipped, "the model declares no entity set to sample"), ProbeStep.NotRun, null, null);
            }

            var (set, type) = chosen.Value;
            var what = $"{set.Name}?$top=1";
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(root, $"{set.Name}?$top=1"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ODataMcpCatalogConstants.ApplicationJson));

            HttpResponseMessage response;
            try
            {
                response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                return (new ProbeStep(ProbeOutcome.Failed, $"{what} -> {exception.Message}"), ProbeStep.NotRun, set.Name, type.FullName);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    return (FromFailure(what, response), ProbeStep.NotRun, set.Name, type.FullName);
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var mediaType = response.Content.Headers.ContentType?.MediaType ?? "no content type";
                var data = new ProbeStep(ProbeOutcome.Passed, $"{what} -> {(int)response.StatusCode} {mediaType}", (int)response.StatusCode);

                return (data, InterpretRow(type, body), set.Name, type.FullName);
            }
        }

        /// <summary>
        /// Step 1: fetch and parse <c>$metadata</c>.
        /// </summary>
        /// <param name="root">The service root with a trailing slash.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The step and the parsed model when it passed.
        /// </returns>
        internal async Task<(ProbeStep Step, EdmModel? Model)> ProbeMetadataAsync(Uri root, CancellationToken cancellationToken)
        {
            const string what = "$metadata";
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(root, ODataMcpCatalogConstants.Metadata));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ODataMcpCatalogConstants.ApplicationXml));

            HttpResponseMessage response;
            try
            {
                response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                return (new ProbeStep(ProbeOutcome.Failed, $"{what} -> {exception.Message}"), null);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    return (FromFailure(what, response), null);
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var mediaType = response.Content.Headers.ContentType?.MediaType ?? "no content type";
                if (!body.Contains("Edmx", StringComparison.Ordinal) || !body.TrimStart().StartsWith('<'))
                {
                    return (new ProbeStep(ProbeOutcome.Unreadable, $"{what} -> 200 {mediaType}, but the body is not CSDL: {Snippet(body)}", (int)response.StatusCode), null);
                }

                try
                {
                    var model = _parser.ParseFromString(body);

                    return (new ProbeStep(ProbeOutcome.Passed, $"{what} -> 200; {DescribeModel(model)}", (int)response.StatusCode), model);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    return (new ProbeStep(ProbeOutcome.Unreadable, $"{what} -> 200, but the CSDL did not parse: {exception.Message}", (int)response.StatusCode), null);
                }
            }
        }

        /// <summary>
        /// Trims a body to one quotable line.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <returns>
        /// The first <see cref="SnippetLength"/> characters with whitespace collapsed.
        /// </returns>
        internal static string Snippet(string body)
        {
            var collapsed = string.Join(' ', body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

            return collapsed.Length <= SnippetLength ? collapsed : collapsed[..SnippetLength] + "…";
        }

        /// <summary>
        /// Ensures the root ends in <c>/</c> so relative segments append instead of replacing the last segment.
        /// </summary>
        /// <param name="serviceRoot">The root.</param>
        /// <returns>
        /// The root with exactly one trailing slash.
        /// </returns>
        internal static Uri WithTrailingSlash(Uri serviceRoot)
        {
            var text = serviceRoot.ToString();

            return text.EndsWith('/') ? serviceRoot : new Uri(text + "/", UriKind.Absolute);
        }

        #endregion

    }

}
