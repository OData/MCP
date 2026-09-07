// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.OData.Mcp.Core.Execution
{

    /// <summary>
    /// Executes OData requests against a remote service using a named HttpClient.
    /// </summary>
    public sealed class RemoteODataExecutor : IODataExecutor
    {

        #region Fields

        internal const string HttpClientName = "OData";

        internal readonly IHttpClientFactory _httpClientFactory;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="RemoteODataExecutor"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        public RemoteODataExecutor(IHttpClientFactory httpClientFactory)
        {
            ArgumentNullException.ThrowIfNull(httpClientFactory);

            _httpClientFactory = httpClientFactory;
        }

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public async Task<ODataExecuteResult> ExecuteAsync(ODataExecuteRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.RelativePath);

            var client = _httpClientFactory.CreateClient(HttpClientName);
            var uri = BuildRelativeUri(request);
            using var message = new HttpRequestMessage(request.Method, uri);

            if (!string.IsNullOrWhiteSpace(request.JsonBody))
            {
                message.Content = new StringContent(request.JsonBody, Encoding.UTF8, "application/json");
            }

            using var response = await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return ODataExecuteResult.FromHttp(response, body);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds a relative URI with $-prefixed query options.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>
        /// The relative URI.
        /// </returns>
        internal static string BuildRelativeUri(ODataExecuteRequest request)
        {
            if (request.QueryOptions.Count == 0)
            {
                return request.RelativePath;
            }

            var pairs = request.QueryOptions.Select(pair =>
            {
                var key = pair.Key.StartsWith('$') ? pair.Key : $"${pair.Key}";

                return $"{key}={Uri.EscapeDataString(pair.Value)}";
            });

            return $"{request.RelativePath}?{string.Join("&", pairs)}";
        }

        #endregion

    }

}
