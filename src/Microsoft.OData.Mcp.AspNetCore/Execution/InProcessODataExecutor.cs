// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Core.Execution;

namespace Microsoft.OData.Mcp.AspNetCore.Execution
{

    /// <summary>
    /// Executes OData requests by invoking the same ASP.NET Core pipeline that hosts MCP.
    /// </summary>
    public sealed class InProcessODataExecutor : IODataExecutor
    {

        #region Fields

        /// <summary>
        /// Request headers that must not be replayed onto the inner OData call.
        /// Hop-by-hop, entity-body, and conditionals that describe the MCP request rather than the OData resource.
        /// </summary>
        internal static readonly HashSet<string> SkippedRequestHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Accept",
            "Accept-Encoding",
            "Connection",
            "Content-Encoding",
            "Content-Length",
            "Content-Type",
            "Expect",
            "Host",
            "If-Match",
            "If-Modified-Since",
            "If-None-Match",
            "If-Range",
            "If-Unmodified-Since",
            "Keep-Alive",
            "Proxy-Authenticate",
            "Proxy-Authorization",
            "Range",
            "TE",
            "Trailer",
            "Transfer-Encoding",
            "Upgrade"
        };

        internal readonly McpHttpContextAccessor _httpContextAccessor;

        internal readonly ODataMcpPipeline _pipeline;

        internal readonly string _routePrefix;

        internal readonly IServiceScopeFactory _scopeFactory;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="InProcessODataExecutor"/> class.
        /// </summary>
        /// <param name="pipeline">The captured application pipeline.</param>
        /// <param name="httpContextAccessor">The wrapping accessor that can Start/End the inner context.</param>
        /// <param name="scopeFactory">Creates a request scope for the inner OData call.</param>
        /// <param name="routePrefix">The OData route prefix, for example <c>odata</c>.</param>
        public InProcessODataExecutor(
            ODataMcpPipeline pipeline,
            McpHttpContextAccessor httpContextAccessor,
            IServiceScopeFactory scopeFactory,
            string routePrefix)
        {
            ArgumentNullException.ThrowIfNull(pipeline);
            ArgumentNullException.ThrowIfNull(httpContextAccessor);
            ArgumentNullException.ThrowIfNull(scopeFactory);
            ArgumentNullException.ThrowIfNull(routePrefix);

            _pipeline = pipeline;
            _httpContextAccessor = httpContextAccessor;
            _scopeFactory = scopeFactory;
            _routePrefix = routePrefix.Trim('/');
        }

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public async Task<ODataExecuteResult> ExecuteAsync(ODataExecuteRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.RelativePath);

            var pipeline = _pipeline.Pipeline;
            if (!_pipeline.IsEnabled || pipeline is null)
            {
                throw new InvalidOperationException("In-process OData execution requires UseODataMcp. AddODataMcp registers services only.");
            }

            var outer = _httpContextAccessor.Outer;
            using var scope = _scopeFactory.CreateScope();
            var inner = CreateInnerContext(request, outer, scope.ServiceProvider, cancellationToken);
            _httpContextAccessor.Start(inner);
            try
            {
                await pipeline(inner);
            }
            finally
            {
                _httpContextAccessor.End();
            }

            if (inner.Response.Body.CanSeek)
            {
                inner.Response.Body.Seek(0, SeekOrigin.Begin);
            }

            return ToExecuteResult(inner);
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
        /// Copies MCP request headers onto the inner call, skipping <see cref="SkippedRequestHeaders"/>.
        /// </summary>
        /// <param name="outer">The MCP context, when present.</param>
        /// <param name="inner">The inner OData context.</param>
        internal static void CopyIncomingHeaders(HttpContext? outer, HttpContext inner)
        {
            ArgumentNullException.ThrowIfNull(inner);

            if (outer is null)
            {
                return;
            }

            foreach (var header in outer.Request.Headers)
            {
                if (SkippedRequestHeaders.Contains(header.Key))
                {
                    continue;
                }

                inner.Request.Headers[header.Key] = header.Value;
            }
        }

        /// <summary>
        /// Builds an inner HTTP context that does not share the MCP response stream.
        /// </summary>
        /// <param name="request">The OData execute request.</param>
        /// <param name="outer">The MCP HTTP context, when present.</param>
        /// <param name="requestServices">The inner request service provider.</param>
        /// <param name="cancellationToken">Cancellation for the inner request.</param>
        /// <returns>
        /// The inner context.
        /// </returns>
        /// <remarks>
        /// Scheme, host, and path base come from the MCP request so <c>@odata.context</c> and
        /// <c>@odata.nextLink</c> match the public URL. Other request headers are copied except
        /// hop-by-hop, entity-body, and HTTP conditionals (see <see cref="SkippedRequestHeaders"/>).
        /// </remarks>
        internal DefaultHttpContext CreateInnerContext(
            ODataExecuteRequest request,
            HttpContext? outer,
            IServiceProvider requestServices,
            CancellationToken cancellationToken)
        {
            var inner = new DefaultHttpContext
            {
                RequestAborted = cancellationToken,
                RequestServices = requestServices,
                User = outer?.User ?? new ClaimsPrincipal()
            };

            var relative = BuildRelativeUri(request);
            var question = relative.IndexOf('?', StringComparison.Ordinal);
            var path = question < 0 ? relative : relative[..question];
            var query = question < 0 ? string.Empty : relative[question..];

            inner.Request.Method = request.Method.Method;
            inner.Request.Scheme = outer?.Request.Scheme is { Length: > 0 } scheme ? scheme : "http";
            inner.Request.Host = outer is not null && outer.Request.Host.HasValue
                ? outer.Request.Host
                : new HostString("localhost");
            inner.Request.PathBase = outer?.Request.PathBase ?? PathString.Empty;
            inner.Request.Path = "/" + path.TrimStart('/');
            inner.Request.QueryString = new QueryString(query);
            CopyIncomingHeaders(outer, inner);
            inner.Request.Headers.Accept = "application/json";

            if (!string.IsNullOrWhiteSpace(request.JsonBody))
            {
                var bytes = Encoding.UTF8.GetBytes(request.JsonBody);
                inner.Request.Body = new MemoryStream(bytes);
                inner.Request.ContentType = "application/json";
                inner.Request.ContentLength = bytes.Length;
            }

            inner.Response.Body = new MemoryStream();

            return inner;
        }

        /// <summary>
        /// Maps an inner HTTP response onto an execute result.
        /// </summary>
        /// <param name="inner">The inner HTTP context.</param>
        /// <returns>
        /// The execute result. <see cref="ODataExecuteResult.BodyStream"/> is the inner response
        /// stream as written.
        /// </returns>
        internal static ODataExecuteResult ToExecuteResult(HttpContext inner)
        {
            ArgumentNullException.ThrowIfNull(inner);

            var contentType = inner.Response.ContentType;
            var mediaType = contentType is null
                ? null
                : contentType.Split(';', 2)[0].Trim();
            var status = inner.Response.StatusCode;

            return new ODataExecuteResult
            {
                BodyStream = inner.Response.Body,
                IsSuccess = status is >= StatusCodes.Status200OK and <= 299,
                MediaType = string.IsNullOrWhiteSpace(mediaType) ? null : mediaType,
                RetryAfter = inner.Response.Headers.RetryAfter.ToString() is { Length: > 0 } retryAfter ? retryAfter : null,
                StatusCode = status,
                WwwAuthenticate = [.. inner.Response.Headers.WWWAuthenticate
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!)
                    .Distinct(StringComparer.Ordinal)]
            };
        }

        #endregion

    }

}
