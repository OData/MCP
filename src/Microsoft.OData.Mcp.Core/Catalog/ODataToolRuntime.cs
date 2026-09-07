// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OData.Mcp.Core.Constants;
using Microsoft.OData.Mcp.Core.Execution;
using Microsoft.OData.Mcp.Core.Models;
using static Microsoft.OData.Mcp.Core.Constants.ODataMcpCatalogConstants;

namespace Microsoft.OData.Mcp.Core.Catalog
{

    /// <summary>
    /// Executes catalog tools against an <see cref="IODataExecutor"/>.
    /// </summary>
    public sealed class ODataToolRuntime
    {

        #region Fields

        internal static readonly HashSet<string> QueryOptionNames =
        [
            Filter,
            Select,
            OrderBy,
            Expand,
            Top,
            Skip,
            Count
        ];

        internal readonly ODataMcpCatalog _catalog;
        internal readonly IODataExecutor _executor;
        internal readonly int _maxResponseBytes;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataToolRuntime"/> class.
        /// </summary>
        /// <param name="catalog">The catalog that advertised the tools.</param>
        /// <param name="executor">The OData executor.</param>
        public ODataToolRuntime(ODataMcpCatalog catalog, IODataExecutor executor)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(executor);

            _catalog = catalog;
            _executor = executor;
            _maxResponseBytes = catalog._options.MaxResponseBytes > 0 ? catalog._options.MaxResponseBytes : 1_048_576;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Invokes a catalog tool.
        /// </summary>
        /// <param name="name">The tool name.</param>
        /// <param name="arguments">Tool arguments.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The invocation result.
        /// </returns>
        public async Task<ODataToolInvocationResult> InvokeAsync(string name, IEnumerable<KeyValuePair<string, JsonElement>>? arguments, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            IReadOnlyDictionary<string, JsonElement> args = arguments is null
                ? new Dictionary<string, JsonElement>()
                : arguments as IReadOnlyDictionary<string, JsonElement> ?? new Dictionary<string, JsonElement>(arguments as IDictionary<string, JsonElement> ?? arguments.ToDictionary(pair => pair.Key, pair => pair.Value));

            try
            {
                var task = name switch
                {
                    OdataListEntitySets => Task.FromResult(ListEntitySets()),
                    OdataDescribeType => Task.FromResult(DescribeType(args)),
                    OdataQuery => QueryAsync(args, cancellationToken),
                    OdataGet => GetAsync(args, cancellationToken),
                    OdataCreate => CreateAsync(args, cancellationToken),
                    OdataUpdate => UpdateAsync(args, cancellationToken),
                    OdataDelete => DeleteAsync(args, cancellationToken),
                    OdataNavigate => NavigateAsync(args, cancellationToken),
                    OdataListOperations => Task.FromResult(ListOperations()),
                    OdataCall => CallOperationAsync(args, cancellationToken),
                    _ => InvokeNamedAsync(name, args, cancellationToken)
                };

                return await task.ConfigureAwait(false);
            }
            catch (ArgumentException ex)
            {
                return Error(ex.Message);
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds a JSON result, applying the response size guard.
        /// </summary>
        /// <param name="json">The JSON payload.</param>
        /// <param name="fallbackText">Short text when the JSON is omitted.</param>
        /// <returns>
        /// The invocation result.
        /// </returns>
        internal ODataToolInvocationResult Complete(string json, string fallbackText)
        {
            if (json.Length > _maxResponseBytes)
            {
                return new ODataToolInvocationResult
                {
                    IsError = true,
                    Text = "The OData response exceeded the size limit. Add select and top to reduce the payload."
                };
            }

            return new ODataToolInvocationResult
            {
                StructuredContent = json,
                Text = fallbackText
            };
        }

        /// <summary>
        /// Rejects oversized filter, expand, and select text. Does not add or rewrite query options.
        /// </summary>
        /// <param name="options">Query options without $.</param>
        /// <returns>
        /// The same dictionary.
        /// </returns>
        internal Dictionary<string, string> ApplyQueryGuards(Dictionary<string, string> options)
        {
            ArgumentNullException.ThrowIfNull(options);

            GuardQueryLength(options, Expand, _catalog._options.MaxExpandLength > 0 ? _catalog._options.MaxExpandLength : 512);
            GuardQueryLength(options, Filter, _catalog._options.MaxFilterLength > 0 ? _catalog._options.MaxFilterLength : 2_048);
            GuardQueryLength(options, Select, _catalog._options.MaxSelectLength > 0 ? _catalog._options.MaxSelectLength : 1_024);

            return options;
        }

        /// <summary>
        /// Calls a declared function or action.
        /// </summary>
        /// <param name="arguments">Tool arguments.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The invocation result.
        /// </returns>
        internal async Task<ODataToolInvocationResult> CallOperationAsync(IReadOnlyDictionary<string, JsonElement> arguments, CancellationToken cancellationToken)
        {
            var name = ReadRequired(arguments, Name);
            var action = _catalog._model.Actions.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            var function = action is null
                ? _catalog._model.Functions.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                : null;
            if (action is null && function is null)
            {
                return Error($"Operation '{name}' is not declared in the model.");
            }

            var declaredName = action?.Name ?? function!.Name;
            var isBound = action?.IsBound == true || function?.IsBound == true;
            var path = declaredName;
            if (isBound)
            {
                var entitySet = ReadRequired(arguments, EntitySet);
                var key = ReadRequired(arguments, Key);
                path = $"{entitySet}({FormatKey(key)})/{declaredName}";
            }

            if (function is not null)
            {
                var pairs = function.Parameters
                    .Where(parameter => !isBound || !parameter.Name.Equals(function.Parameters.FirstOrDefault()?.Name, StringComparison.Ordinal))
                    .Select(parameter =>
                    {
                        if (!arguments.TryGetValue(parameter.Name, out var value) || value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
                        {
                            return null;
                        }

                        var text = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();
                        var formatted = parameter.Type.Contains("String", StringComparison.OrdinalIgnoreCase) ? FormatKey(text) : text;

                        return $"{parameter.Name}={formatted}";
                    })
                    .Where(pair => pair is not null)
                    .ToList();
                if (pairs.Count > 0)
                {
                    path = $"{path}({string.Join(",", pairs)})";
                }

                return await ExecuteAsync(HttpMethod.Get, path, null, null, cancellationToken).ConfigureAwait(false);
            }

            var body = ReadBody(arguments);
            GuardRequestBody(body);

            return await ExecuteAsync(HttpMethod.Post, path, body, null, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates an entity from a JSON body.
        /// </summary>
        /// <param name="arguments">Tool arguments.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The invocation result.
        /// </returns>
        internal async Task<ODataToolInvocationResult> CreateAsync(IReadOnlyDictionary<string, JsonElement> arguments, CancellationToken cancellationToken)
        {
            var entitySet = ReadRequired(arguments, EntitySet);
            var body = ReadBody(arguments);
            GuardRequestBody(body);

            return await ExecuteAsync(HttpMethod.Post, entitySet, body, null, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes an entity by key.
        /// </summary>
        /// <param name="arguments">Tool arguments.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The invocation result.
        /// </returns>
        internal async Task<ODataToolInvocationResult> DeleteAsync(IReadOnlyDictionary<string, JsonElement> arguments, CancellationToken cancellationToken)
        {
            var entitySet = ReadRequired(arguments, EntitySet);
            var key = ReadRequired(arguments, Key);

            return await ExecuteAsync(HttpMethod.Delete, $"{entitySet}({FormatKey(key)})", null, null, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Describes a declared type or entity set, including CSDL documentation.
        /// </summary>
        /// <param name="arguments">Tool arguments.</param>
        /// <returns>
        /// The invocation result.
        /// </returns>
        internal ODataToolInvocationResult DescribeType(IReadOnlyDictionary<string, JsonElement> arguments)
        {
            var name = ReadRequired(arguments, Name);
            var set = _catalog.ResolveIncludedSets().FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            var type = set is not null
                ? _catalog.ResolveEntityType(set)
                : _catalog._model.EntityTypes.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || item.FullName.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (type is null)
            {
                return Error($"Type or entity set '{name}' is not declared in the model.");
            }

            var payload = new Dictionary<string, object?>
            {
                [Description] = EdmDocumentation.First(set?.Description, set?.LongDescription, type.Description, type.LongDescription),
                [EntitySet] = set?.Name,
                [EntityTypeDescription] = EdmDocumentation.First(type.Description, type.LongDescription),
                [Keys] = type.Key,
                [LongDescription] = EdmDocumentation.First(set?.LongDescription, type.LongDescription),
                [Name] = type.Name,
                [Namespace] = type.Namespace,
                [Navigations] = type.NavigationProperties.Select(navigation => new Dictionary<string, object?>
                {
                    [Description] = EdmDocumentation.First(navigation.Description, navigation.LongDescription),
                    [Name] = navigation.Name,
                    [ODataMcpCatalogConstants.Type] = navigation.Type
                }).ToList(),
                [Properties] = type.Properties.Where(ODataMcpCatalog.IsExposedProperty).Select(property => new Dictionary<string, object?>
                {
                    [Description] = EdmDocumentation.First(property.Description, property.LongDescription),
                    [Name] = property.Name,
                    [ODataMcpCatalogConstants.Nullable] = property.Nullable,
                    [ODataMcpCatalogConstants.Type] = property.Type
                }).ToList()
            };

            var json = JsonSerializer.Serialize(payload, ODataMcpCatalog.SchemaSerializerOptions);

            return Complete(json, type.Description ?? type.Name);
        }

        /// <summary>
        /// Executes an OData request and maps the HTTP result.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="relativePath">The path relative to the service root.</param>
        /// <param name="jsonBody">The JSON body, if any.</param>
        /// <param name="queryOptions">Query options without $.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The invocation result.
        /// </returns>
        internal async Task<ODataToolInvocationResult> ExecuteAsync(HttpMethod method, string relativePath, string? jsonBody, Dictionary<string, string>? queryOptions, CancellationToken cancellationToken)
        {
            var result = await _executor.ExecuteAsync(
                new ODataExecuteRequest
                {
                    JsonBody = jsonBody,
                    Method = method,
                    QueryOptions = queryOptions ?? [],
                    RelativePath = relativePath
                },
                cancellationToken).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                return new ODataToolInvocationResult
                {
                    IsError = true,
                    Text = FormatFailure(result)
                };
            }

            if (string.IsNullOrWhiteSpace(result.Body))
            {
                return new ODataToolInvocationResult
                {
                    Text = $"OData {method} {relativePath} succeeded with status {result.StatusCode}."
                };
            }

            return Complete(result.Body, $"OData {method} {relativePath} returned {result.StatusCode}.");
        }

        /// <summary>
        /// Builds a tool error that always includes the HTTP status and <c>Retry-After</c> when present.
        /// </summary>
        /// <param name="result">The failed OData HTTP result.</param>
        /// <returns>
        /// The error text.
        /// </returns>
        internal static string FormatFailure(ODataExecuteResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            var text = $"OData request failed with status {result.StatusCode}.";
            if (!string.IsNullOrWhiteSpace(result.RetryAfter))
            {
                text = $"{text} Retry-After: {result.RetryAfter}.";
            }

            if (!string.IsNullOrWhiteSpace(result.Body))
            {
                text = $"{text} {result.Body}";
            }

            return text;
        }

        /// <summary>
        /// Formats an entity key for an OData path.
        /// </summary>
        /// <param name="key">The key value.</param>
        /// <returns>
        /// The parenthetical key.
        /// </returns>
        /// <remarks>
        /// Numeric, GUID, and Boolean values are unquoted. String values are quoted with
        /// doubled apostrophes. Named and composite keys such as
        /// <c>OrderID=10248,ProductID=11</c> format each value independently so the wire
        /// path is <c>Order_Details(OrderID=10248,ProductID=11)</c>, not a single quoted string.
        /// </remarks>
        internal static string FormatKey(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (TryFormatNamedKey(key, out var named))
            {
                return named;
            }

            return FormatSimpleKey(key);
        }

        /// <summary>
        /// Formats a single (non-composite) key value.
        /// </summary>
        /// <param name="key">The key value.</param>
        /// <returns>
        /// The formatted value.
        /// </returns>
        internal static string FormatSimpleKey(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (long.TryParse(key, out _) || Guid.TryParse(key, out _) || bool.TryParse(key, out _))
            {
                return key;
            }

            if (key.StartsWith('\'') && key.EndsWith('\''))
            {
                return key;
            }

            return $"'{key.Replace("'", "''", StringComparison.Ordinal)}'";
        }

        /// <summary>
        /// Gets an entity by key.
        /// </summary>
        /// <param name="arguments">Tool arguments.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The invocation result.
        /// </returns>
        internal async Task<ODataToolInvocationResult> GetAsync(IReadOnlyDictionary<string, JsonElement> arguments, CancellationToken cancellationToken)
        {
            var entitySet = ReadRequired(arguments, EntitySet);
            var key = ReadRequired(arguments, Key);

            return await ExecuteAsync(HttpMethod.Get, $"{entitySet}({FormatKey(key)})", null, ApplyQueryGuards(ReadQueryOptions(arguments)), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Rejects query option text that exceeds the configured length.
        /// </summary>
        /// <param name="options">Query options.</param>
        /// <param name="name">The option name.</param>
        /// <param name="maxLength">The maximum length.</param>
        internal static void GuardQueryLength(Dictionary<string, string> options, string name, int maxLength)
        {
            if (options.TryGetValue(name, out var value) && value.Length > maxLength)
            {
                throw new ArgumentException($"{name} exceeds the maximum length of {maxLength} characters.", nameof(options));
            }
        }

        /// <summary>
        /// Rejects request bodies that exceed <see cref="ODataMcpCatalogOptions.MaxRequestBodyBytes"/>.
        /// </summary>
        /// <param name="body">The JSON body.</param>
        internal void GuardRequestBody(string? body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            var max = _catalog._options.MaxRequestBodyBytes > 0 ? _catalog._options.MaxRequestBodyBytes : 262_144;
            if (body.Length > max)
            {
                throw new ArgumentException($"The request body exceeds the maximum size of {max} bytes.");
            }
        }

        /// <summary>
        /// Invokes a named CRUD tool.
        /// </summary>
        /// <param name="name">The tool name.</param>
        /// <param name="arguments">Tool arguments.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The invocation result.
        /// </returns>
        internal Task<ODataToolInvocationResult> InvokeNamedAsync(string name, IReadOnlyDictionary<string, JsonElement> arguments, CancellationToken cancellationToken)
        {
            var descriptor = _catalog.Tools.FirstOrDefault(tool => tool.Name.Equals(name, StringComparison.Ordinal));
            if (descriptor is null || string.IsNullOrWhiteSpace(descriptor.EntitySetName))
            {
                return Task.FromResult(Error($"Unknown tool '{name}'."));
            }

            var named = new Dictionary<string, JsonElement>(arguments, StringComparer.OrdinalIgnoreCase)
            {
                [EntitySet] = JsonSerializer.SerializeToElement(descriptor.EntitySetName)
            };

            if (name.StartsWith(ListPrefix, StringComparison.Ordinal))
            {
                return QueryAsync(named, cancellationToken);
            }

            if (name.StartsWith(GetPrefix, StringComparison.Ordinal))
            {
                return GetAsync(named, cancellationToken);
            }

            if (name.StartsWith(CreatePrefix, StringComparison.Ordinal))
            {
                if (!named.ContainsKey(Body))
                {
                    named[Body] = JsonSerializer.SerializeToElement(JsonSerializer.Serialize(arguments.Where(pair => !QueryOptionNames.Contains(pair.Key) && pair.Key is not Key and not EntitySet).ToDictionary(pair => pair.Key, pair => pair.Value), ODataMcpCatalog.SchemaSerializerOptions));
                }

                return CreateAsync(named, cancellationToken);
            }

            if (name.StartsWith(UpdatePrefix, StringComparison.Ordinal))
            {
                return UpdateAsync(named, cancellationToken);
            }

            if (name.StartsWith(DeletePrefix, StringComparison.Ordinal))
            {
                return DeleteAsync(named, cancellationToken);
            }

            return Task.FromResult(Error($"Unknown named tool '{name}'."));
        }

        /// <summary>
        /// Returns whether <paramref name="name"/> is an OData identifier.
        /// </summary>
        /// <param name="name">The candidate name.</param>
        /// <returns>
        /// <c>true</c> when the name is a simple identifier.
        /// </returns>
        internal static bool IsODataIdentifier(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || (!char.IsLetter(name[0]) && name[0] != '_'))
            {
                return false;
            }

            for (var index = 1; index < name.Length; index++)
            {
                if (!char.IsLetterOrDigit(name[index]) && name[index] != '_')
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Lists declared entity sets, including CSDL documentation.
        /// </summary>
        /// <returns>
        /// The invocation result.
        /// </returns>
        internal ODataToolInvocationResult ListEntitySets()
        {
            var sets = _catalog.ResolveIncludedSets().Select(set =>
            {
                var type = _catalog.ResolveEntityType(set);

                return new Dictionary<string, object?>
                {
                    [Description] = EdmDocumentation.First(set.Description, set.LongDescription, type?.Description, type?.LongDescription),
                    [EntityType] = set.EntityType,
                    [Keys] = type?.Key ?? [],
                    [Name] = set.Name
                };
            }).ToList();

            var json = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                [EntitySets] = sets
            }, ODataMcpCatalog.SchemaSerializerOptions);

            return Complete(json, $"Declared entity sets: {sets.Count}.");
        }

        /// <summary>
        /// Lists declared functions and actions.
        /// </summary>
        /// <returns>
        /// The invocation result.
        /// </returns>
        internal ODataToolInvocationResult ListOperations()
        {
            var operations = _catalog._model.Functions.Select(item => new Dictionary<string, object?>
            {
                [Description] = EdmDocumentation.First(item.Description, item.LongDescription),
                [IsBound] = item.IsBound,
                [Kind] = Function,
                [Name] = item.Name,
                [Parameters] = item.Parameters.Select(parameter => parameter.Name).ToList(),
                [ReturnType] = item.ReturnType
            }).Concat(_catalog._model.Actions.Select(item => new Dictionary<string, object?>
            {
                [Description] = EdmDocumentation.First(item.Description, item.LongDescription),
                [IsBound] = item.IsBound,
                [Kind] = ODataMcpCatalogConstants.Action,
                [Name] = item.Name,
                [Parameters] = item.Parameters.Select(parameter => parameter.Name).ToList(),
                [ReturnType] = item.ReturnType
            })).ToList();

            var json = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                [Operations] = operations
            }, ODataMcpCatalog.SchemaSerializerOptions);

            return Complete(json, $"Declared operations: {operations.Count}.");
        }

        /// <summary>
        /// Follows a navigation property from a key.
        /// </summary>
        /// <param name="arguments">Tool arguments.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The invocation result.
        /// </returns>
        internal async Task<ODataToolInvocationResult> NavigateAsync(IReadOnlyDictionary<string, JsonElement> arguments, CancellationToken cancellationToken)
        {
            var entitySet = ReadRequired(arguments, EntitySet);
            var key = ReadRequired(arguments, Key);
            var navigation = ReadRequired(arguments, Navigation);

            return await ExecuteAsync(HttpMethod.Get, $"{entitySet}({FormatKey(key)})/{navigation}", null, ApplyQueryGuards(ReadQueryOptions(arguments)), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Queries an entity set.
        /// </summary>
        /// <param name="arguments">Tool arguments.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The invocation result.
        /// </returns>
        internal async Task<ODataToolInvocationResult> QueryAsync(IReadOnlyDictionary<string, JsonElement> arguments, CancellationToken cancellationToken)
        {
            var entitySet = ReadRequired(arguments, EntitySet);

            return await ExecuteAsync(HttpMethod.Get, entitySet, null, ApplyQueryGuards(ReadQueryOptions(arguments)), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Reads a JSON body from a <c>body</c> argument or the remaining properties.
        /// </summary>
        /// <param name="arguments">Tool arguments.</param>
        /// <returns>
        /// The JSON body.
        /// </returns>
        internal static string ReadBody(IReadOnlyDictionary<string, JsonElement> arguments)
        {
            if (arguments.TryGetValue(Body, out var body))
            {
                return body.ValueKind == JsonValueKind.String ? body.GetString() ?? "{}" : body.GetRawText();
            }

            var properties = arguments
                .Where(pair => pair.Key is not EntitySet and not Key)
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            return JsonSerializer.Serialize(properties, ODataMcpCatalog.SchemaSerializerOptions);
        }

        /// <summary>
        /// Reads query options without a $ prefix.
        /// </summary>
        /// <param name="arguments">Tool arguments.</param>
        /// <returns>
        /// Query options for the executor.
        /// </returns>
        internal static Dictionary<string, string> ReadQueryOptions(IReadOnlyDictionary<string, JsonElement> arguments)
        {
            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var name in QueryOptionNames)
            {
                if (!arguments.TryGetValue(name, out var value) || value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
                {
                    continue;
                }

                options[name] = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();
            }

            return options;
        }

        /// <summary>
        /// Reads a required string argument.
        /// </summary>
        /// <param name="arguments">Tool arguments.</param>
        /// <param name="name">The argument name.</param>
        /// <returns>
        /// The argument value.
        /// </returns>
        internal static string ReadRequired(IReadOnlyDictionary<string, JsonElement> arguments, string name)
        {
            if (!arguments.TryGetValue(name, out var value) || value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                throw new ArgumentException($"Missing required argument '{name}'.", nameof(arguments));
            }

            var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
            ArgumentException.ThrowIfNullOrWhiteSpace(text, name);

            return text;
        }

        /// <summary>
        /// Splits a key on commas that are not inside single-quoted values.
        /// </summary>
        /// <param name="key">The key text.</param>
        /// <returns>
        /// The segments.
        /// </returns>
        internal static List<string> SplitUnquotedCommas(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            var parts = new List<string>();
            var start = 0;
            var inQuote = false;
            for (var index = 0; index < key.Length; index++)
            {
                var character = key[index];
                if (character == '\'')
                {
                    if (inQuote && index + 1 < key.Length && key[index + 1] == '\'')
                    {
                        index++;
                        continue;
                    }

                    inQuote = !inQuote;
                    continue;
                }

                if (character == ',' && !inQuote)
                {
                    parts.Add(key[start..index]);
                    start = index + 1;
                }
            }

            parts.Add(key[start..]);

            return parts;
        }

        /// <summary>
        /// Formats named or composite keys such as <c>OrderID=10248,ProductID=11</c>.
        /// </summary>
        /// <param name="key">The key text.</param>
        /// <param name="formatted">The formatted key when this method returns <c>true</c>.</param>
        /// <returns>
        /// <c>true</c> when every segment is <c>Name=value</c>.
        /// </returns>
        internal static bool TryFormatNamedKey(string key, out string formatted)
        {
            formatted = string.Empty;
            if (string.IsNullOrWhiteSpace(key) || !key.Contains('=', StringComparison.Ordinal))
            {
                return false;
            }

            var parts = SplitUnquotedCommas(key);
            if (parts.Count == 0)
            {
                return false;
            }

            var pairs = new List<string>(parts.Count);
            foreach (var part in parts)
            {
                var equals = part.IndexOf('=');
                if (equals <= 0)
                {
                    return false;
                }

                var name = part[..equals].Trim();
                var value = part[(equals + 1)..].Trim();
                if (!IsODataIdentifier(name) || string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }

                pairs.Add($"{name}={FormatSimpleKey(value)}");
            }

            formatted = string.Join(",", pairs);

            return true;
        }

        /// <summary>
        /// Updates an entity with PATCH.
        /// </summary>
        /// <param name="arguments">Tool arguments.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The invocation result.
        /// </returns>
        internal async Task<ODataToolInvocationResult> UpdateAsync(IReadOnlyDictionary<string, JsonElement> arguments, CancellationToken cancellationToken)
        {
            var entitySet = ReadRequired(arguments, EntitySet);
            var key = ReadRequired(arguments, Key);
            var body = ReadBody(arguments);
            GuardRequestBody(body);

            return await ExecuteAsync(HttpMethod.Patch, $"{entitySet}({FormatKey(key)})", body, null, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Builds an error result.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <returns>
        /// The invocation result.
        /// </returns>
        internal static ODataToolInvocationResult Error(string message)
        {
            return new ODataToolInvocationResult
            {
                IsError = true,
                Text = message
            };
        }

        #endregion

    }

}
