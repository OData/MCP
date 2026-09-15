// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.AspNetCore.Execution;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Core.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Execution
{

    /// <summary>
    /// In-process execute walks the captured ASP.NET Core pipeline, not a loopback HTTP client.
    /// </summary>
    [TestClass]
    public class InProcessODataExecutorPipelineTests
    {

        #region Public Methods

        /// <summary>
        /// Execute copies the JSON body onto the inner request.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_CopiesJsonBody()
        {
            string? body = null;
            var executor = CreateExecutor(async context =>
            {
                using var reader = new StreamReader(context.Request.Body);
                body = await reader.ReadToEndAsync();
                context.Response.StatusCode = StatusCodes.Status204NoContent;
            });

            await executor.ExecuteAsync(
                new ODataExecuteRequest
                {
                    JsonBody = """{"CustomerId":1}""",
                    Method = HttpMethod.Post,
                    RelativePath = "Customers"
                },
                CancellationToken.None);

            body.Should().Be("""{"CustomerId":1}""");
        }

        /// <summary>
        /// Execute copies the authenticated user onto the inner context.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_CopiesUser()
        {
            string? subject = null;
            var accessor = new McpHttpContextAccessor
            {
                HttpContext = new DefaultHttpContext()
            };
            accessor.HttpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim("sub", "42")], "test"));
            var executor = CreateExecutor(
                context =>
                {
                    subject = context.User.FindFirst("sub")?.Value;
                    context.Response.StatusCode = StatusCodes.Status204NoContent;

                    return Task.CompletedTask;
                },
                accessor);

            await executor.ExecuteAsync(
                new ODataExecuteRequest { RelativePath = "Customers" },
                CancellationToken.None);

            subject.Should().Be("42");
        }

        /// <summary>
        /// Public origin and telemetry headers from the MCP request are on the inner call.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_CopiesPublicOriginAndTelemetryHeaders()
        {
            string? scheme = null;
            HostString host = default;
            PathString pathBase = default;
            string? trace = null;
            string? correlation = null;
            string? authorization = null;
            var outer = new DefaultHttpContext();
            outer.Request.Scheme = "https";
            outer.Request.Host = new HostString("api.example.com", 443);
            outer.Request.PathBase = "/myapp";
            outer.Request.Headers["traceparent"] = "00-trace-01";
            outer.Request.Headers["x-correlation-id"] = "abc";
            outer.Request.Headers.Authorization = "Bearer tok";
            var accessor = new McpHttpContextAccessor { HttpContext = outer };
            var executor = CreateExecutor(
                context =>
                {
                    scheme = context.Request.Scheme;
                    host = context.Request.Host;
                    pathBase = context.Request.PathBase;
                    trace = context.Request.Headers["traceparent"].ToString();
                    correlation = context.Request.Headers["x-correlation-id"].ToString();
                    authorization = context.Request.Headers.Authorization.ToString();
                    context.Response.StatusCode = StatusCodes.Status204NoContent;

                    return Task.CompletedTask;
                },
                accessor);

            await executor.ExecuteAsync(
                new ODataExecuteRequest { RelativePath = "Customers" },
                CancellationToken.None);

            scheme.Should().Be("https");
            host.ToString().Should().Be("api.example.com:443");
            pathBase.Value.Should().Be("/myapp");
            trace.Should().Be("00-trace-01");
            correlation.Should().Be("abc");
            authorization.Should().Be("Bearer tok");
        }

        /// <summary>
        /// Hop-by-hop, body, and HTTP conditional headers from the MCP request are not replayed.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_SkipsHopByHopAndConditionals()
        {
            string? acceptEncoding = null;
            string? ifMatch = null;
            long? contentLength = null;
            string? accept = null;
            var outer = new DefaultHttpContext();
            outer.Request.Headers.AcceptEncoding = "gzip";
            outer.Request.Headers.IfMatch = "\"etag\"";
            outer.Request.ContentLength = 999;
            var accessor = new McpHttpContextAccessor { HttpContext = outer };
            var executor = CreateExecutor(
                context =>
                {
                    acceptEncoding = context.Request.Headers.AcceptEncoding.ToString();
                    ifMatch = context.Request.Headers.IfMatch.ToString();
                    contentLength = context.Request.ContentLength;
                    accept = context.Request.Headers.Accept.ToString();
                    context.Response.StatusCode = StatusCodes.Status204NoContent;

                    return Task.CompletedTask;
                },
                accessor);

            await executor.ExecuteAsync(
                new ODataExecuteRequest { RelativePath = "Customers" },
                CancellationToken.None);

            acceptEncoding.Should().BeEmpty();
            ifMatch.Should().BeEmpty();
            contentLength.Should().BeNull();
            accept.Should().Be("application/json");
        }

        /// <summary>
        /// Each execute gets its own DI scope.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_CreatesNewScope()
        {
            var services = new ServiceCollection();
            services.AddScoped<Marker>();
            await using var provider = services.BuildServiceProvider();
            var seen = new List<Marker>();
            var holder = new ODataMcpPipeline
            {
                IsEnabled = true,
                Pipeline = context =>
                {
                    seen.Add(context.RequestServices.GetRequiredService<Marker>());
                    context.Response.StatusCode = StatusCodes.Status204NoContent;

                    return Task.CompletedTask;
                }
            };
            var executor = new InProcessODataExecutor(
                holder,
                new McpHttpContextAccessor(),
                provider.GetRequiredService<IServiceScopeFactory>(),
                "odata");

            await executor.ExecuteAsync(new ODataExecuteRequest { RelativePath = "Customers" }, CancellationToken.None);
            await executor.ExecuteAsync(new ODataExecuteRequest { RelativePath = "Customers" }, CancellationToken.None);

            seen.Should().HaveCount(2);
            seen[0].Should().NotBeSameAs(seen[1]);
        }

        /// <summary>
        /// The inner body is the tool payload as written; it is not parsed or re-serialized.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_InnerBody_IsToolJsonWithoutRoundTrip()
        {
            const string payload = """{ "ok": true, "keep": "spacing" }""";
            var executor = CreateExecutor(context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.ContentType = "application/json";

                return context.Response.WriteAsync(payload);
            });

            var result = await executor.ExecuteAsync(
                new ODataExecuteRequest { RelativePath = "Customers" },
                CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            ReadUtf8(result.BodyStream).Should().Be(payload);
        }

        /// <summary>
        /// Execute builds the OData path and query under the route prefix.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_InvokesPipelinePathAndQuery()
        {
            string? path = null;
            string? query = null;
            var executor = CreateExecutor(context =>
            {
                path = context.Request.Path.Value;
                query = context.Request.QueryString.Value;
                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.ContentType = "application/json";

                return context.Response.WriteAsync("""{"ok":true}""");
            });

            var result = await executor.ExecuteAsync(
                new ODataExecuteRequest
                {
                    QueryOptions = { ["top"] = "1" },
                    RelativePath = "Customers"
                },
                CancellationToken.None);

            path.Should().Be("/odata/Customers");
            query.Should().Be("?$top=1");
            result.StatusCode.Should().Be(StatusCodes.Status200OK);
            result.IsSuccess.Should().BeTrue();
            ReadUtf8(result.BodyStream).Should().Be("""{"ok":true}""");
            result.MediaType.Should().Be("application/json");
        }

        /// <summary>
        /// Missing pipeline capture means <c>UseODataMcp</c> was never called.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_MissingPipeline_Throws()
        {
            var executor = CreateExecutor(pipeline: null, enabled: false);

            var act = async () => await executor.ExecuteAsync(
                new ODataExecuteRequest { RelativePath = "Customers" },
                CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*UseODataMcp*");
        }

        /// <summary>
        /// Non-2xx is an error result; the inner body is included as text without a JSON round-trip.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_NonSuccess_IncludesBodyWithoutJsonRoundTrip()
        {
            const string payload = """{ "error": { "code": "Bad" } }""";
            var executor = CreateExecutor(context =>
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;

                return context.Response.WriteAsync(payload);
            });

            var result = await executor.ExecuteAsync(
                new ODataExecuteRequest { RelativePath = "Customers" },
                CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
            result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            ReadUtf8(result.BodyStream).Should().Be(payload);
        }

        /// <summary>
        /// During the pipeline, the accessor returns the inner context; after execute it is the outer
        /// request and <see cref="McpHttpContextAccessor.Inner"/> is <see langword="null"/>.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_StartEnd_SwapsAccessor()
        {
            HttpContext? seenByAccessor = null;
            var outer = new DefaultHttpContext();
            var accessor = new McpHttpContextAccessor { HttpContext = outer };
            var executor = CreateExecutor(
                context =>
                {
                    seenByAccessor = accessor.HttpContext;
                    accessor.HttpContext.Should().BeSameAs(context);
                    accessor.Active.Should().BeSameAs(context);
                    accessor.Inner.Should().BeSameAs(context);
                    accessor.Outer.Should().BeSameAs(outer);
                    context.Response.StatusCode = StatusCodes.Status204NoContent;

                    return Task.CompletedTask;
                },
                accessor);

            await executor.ExecuteAsync(
                new ODataExecuteRequest { RelativePath = "Customers" },
                CancellationToken.None);

            seenByAccessor.Should().NotBeNull();
            seenByAccessor.Should().NotBeSameAs(outer);
            accessor.HttpContext.Should().BeSameAs(outer);
            accessor.Active.Should().BeSameAs(outer);
            accessor.Outer.Should().BeSameAs(outer);
            accessor.Inner.Should().BeNull();
        }

        /// <summary>
        /// Constructor rejects null dependencies.
        /// </summary>
        [TestMethod]
        public void Executor_NullDependencies_Throw()
        {
            var pipeline = new ODataMcpPipeline();
            var accessor = new McpHttpContextAccessor();
            var scopes = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

            var nullPipeline = () => new InProcessODataExecutor(null!, accessor, scopes, "odata");
            var nullAccessor = () => new InProcessODataExecutor(pipeline, null!, scopes, "odata");
            var nullScopes = () => new InProcessODataExecutor(pipeline, accessor, null!, "odata");
            var nullPrefix = () => new InProcessODataExecutor(pipeline, accessor, scopes, null!);

            nullPipeline.Should().Throw<ArgumentNullException>();
            nullAccessor.Should().Throw<ArgumentNullException>();
            nullScopes.Should().Throw<ArgumentNullException>();
            nullPrefix.Should().Throw<ArgumentNullException>();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds an executor over an in-memory pipeline.
        /// </summary>
        /// <param name="pipeline">The pipeline to invoke, or <c>null</c> to leave it uncaptured.</param>
        /// <param name="accessor">Optional HTTP context accessor.</param>
        /// <param name="enabled">Whether <c>UseODataMcp</c> has turned the host on.</param>
        /// <returns>
        /// The executor.
        /// </returns>
        internal static InProcessODataExecutor CreateExecutor(RequestDelegate? pipeline, McpHttpContextAccessor? accessor = null, bool enabled = true)
        {
            var holder = new ODataMcpPipeline
            {
                IsEnabled = enabled,
                Pipeline = pipeline
            };

            return new InProcessODataExecutor(
                holder,
                accessor ?? new McpHttpContextAccessor(),
                new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
                "odata");
        }

        /// <summary>
        /// Reads an executor body stream as UTF-8.
        /// </summary>
        /// <param name="stream">The body stream.</param>
        /// <returns>
        /// The text.
        /// </returns>
        internal static string ReadUtf8(Stream? stream)
        {
            stream.Should().NotBeNull();
            if (stream!.CanSeek)
            {
                stream.Position = 0;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);

            return reader.ReadToEnd();
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// Scoped marker used to prove a new DI scope per execute.
        /// </summary>
        internal sealed class Marker
        {
        }

        #endregion

    }

}
