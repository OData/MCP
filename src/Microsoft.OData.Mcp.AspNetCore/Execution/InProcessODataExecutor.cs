// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.OData.Mcp.Core.Execution;

namespace Microsoft.OData.Mcp.AspNetCore.Execution
{

    /// <summary>
    /// Executes OData requests against the same ASP.NET Core app that hosts MCP.
    /// </summary>
    public sealed class InProcessODataExecutor : IODataExecutor
    {

        #region Fields

        internal const string HttpClientName = "ODataInProcess";

        internal readonly IHttpClientFactory _httpClientFactory;
        internal readonly IHttpContextAccessor _httpContextAccessor;
        internal readonly string _routePrefix;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="InProcessODataExecutor"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="httpContextAccessor">The current HTTP context.</param>
        /// <param name="routePrefix">The OData route prefix, for example <c>odata</c>.</param>
        public InProcessODataExecutor(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor, string routePrefix)
        {
            ArgumentNullException.ThrowIfNull(httpClientFactory);
            ArgumentNullException.ThrowIfNull(httpContextAccessor);
            ArgumentNullException.ThrowIfNull(routePrefix);

            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
            _routePrefix = routePrefix.Trim('/');
        }

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public async Task<ODataExecuteResult> ExecuteAsync(ODataExecuteRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.RelativePath);

            var http = _httpContextAccessor.HttpContext;
            var client = _httpClientFactory.CreateClient(HttpClientName);
            if (http is not null && client.BaseAddress is null)
            {
                client.BaseAddress = new Uri($"{http.Request.Scheme}://{http.Request.Host}{http.Request.PathBase}/");
            }

            var relative = BuildRelativeUri(request);
            using var message = new HttpRequestMessage(request.Method, relative);
            if (http is not null && http.Request.Headers.TryGetValue("Authorization", out var authorization))
            {
                message.Headers.TryAddWithoutValidation("Authorization", authorization.ToString());
            }

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
        /// Builds a relative URI under the OData prefix with $-prefixed query options.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>
        /// The relative URI.
        /// </returns>
        internal string BuildRelativeUri(ODataExecuteRequest request)
        {
            var path = string.IsNullOrWhiteSpace(_routePrefix)
                ? request.RelativePath
                : $"{_routePrefix}/{request.RelativePath}";

            if (request.QueryOptions.Count == 0)
            {
                return path;
            }

            var pairs = request.QueryOptions.Select(pair =>
            {
                var key = pair.Key.StartsWith('$') ? pair.Key : $"${pair.Key}";

                return $"{key}={Uri.EscapeDataString(pair.Value)}";
            });

            return $"{path}?{string.Join("&", pairs)}";
        }

        /// <summary>
        /// Creates a primary handler that talks to <see cref="IServer"/> when it is a TestServer.
        /// </summary>
        /// <param name="server">The ASP.NET Core server.</param>
        /// <returns>
        /// A handler, or <c>null</c> to use the default sockets handler.
        /// </returns>
        internal static HttpMessageHandler? TryCreateServerHandler(IServer? server)
        {
            if (server is null)
            {
                return null;
            }

            var method = server.GetType().GetMethod("CreateHandler", Type.EmptyTypes);
            if (method is null || !typeof(HttpMessageHandler).IsAssignableFrom(method.ReturnType))
            {
                return null;
            }

            return method.Invoke(server, null) as HttpMessageHandler;
        }

        #endregion

    }

}
