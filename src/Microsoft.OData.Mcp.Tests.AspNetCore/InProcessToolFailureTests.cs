// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OData.Mcp.AspNetCore.Execution;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Execution;
using Microsoft.OData.Mcp.Tests.AspNetCore.Execution;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore
{

    /// <summary>
    /// In-process MCP tool failure modes: unknown tools, missing arguments, auth, and bad payloads.
    /// </summary>
    [TestClass]
    public class InProcessToolFailureTests : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        /// <summary>
        /// Builds a convention OData host with MCP.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services
                    .AddControllers()
                    .AddApplicationPart(typeof(CustomersController).Assembly)
                    .AddOData(options =>
                    {
                        options.EnableQueryFeatures();
                        options.AddRouteComponents("odata", TestModels.GetSimpleModel());
                    });

                services.AddODataMcp();
            });

            AddMinimalMvc();
            TestHostBuilder.ConfigureWebHost(web =>
            {
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapControllers();
                    });
                    app.UseODataMcp();
                });
            });
            TestSetup();
        }

        /// <summary>
        /// Tears down the host.
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
            TestTearDown();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Unknown tools return an error result.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_UnknownTool_IsError()
        {
            var result = await Runtime().InvokeAsync("not_a_tool", null, CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("not_a_tool");
        }

        /// <summary>
        /// Query without an entity set is an error.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_QueryMissingEntitySet_IsError()
        {
            var result = await Runtime().InvokeAsync("odata_query", new Dictionary<string, JsonElement>(), CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("entitySet");
        }

        /// <summary>
        /// Get without a key is an error.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_GetMissingKey_IsError()
        {
            var result = await Runtime().InvokeAsync(
                "odata_get",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Customers")
                },
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// Navigate without a navigation property is an error.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_NavigateMissingNavigation_IsError()
        {
            var result = await Runtime().InvokeAsync(
                "odata_navigate",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Customers"),
                    ["key"] = JsonSerializer.SerializeToElement("1")
                },
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("navigation");
        }

        /// <summary>
        /// Describe-type of an unknown name is an error.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_DescribeUnknownType_IsError()
        {
            var result = await Runtime().InvokeAsync(
                "odata_describe_type",
                new Dictionary<string, JsonElement>
                {
                    ["name"] = JsonSerializer.SerializeToElement("Ghost")
                },
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("Ghost");
        }

        /// <summary>
        /// Querying a set with no controller returns a failed OData status.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_UnknownEntitySet_IsError()
        {
            var result = await Runtime().InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("DoesNotExist")
                },
                CancellationToken.None);

            result.IsError.Should().BeTrue();
        }

        /// <summary>
        /// Get of a missing key returns not found.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_GetMissingCustomer_IsError()
        {
            var result = await Runtime().InvokeAsync(
                "odata_get",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Customers"),
                    ["key"] = JsonSerializer.SerializeToElement("999")
                },
                CancellationToken.None);

            result.IsError.Should().BeTrue();
        }

        /// <summary>
        /// Create without an Authorization header is unauthorized.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_CreateWithoutAuthorization_IsError()
        {
            var result = await Runtime().InvokeAsync(
                "odata_create",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Customers"),
                    ["body"] = JsonSerializer.SerializeToElement("""{"CustomerId":2,"CompanyName":"Fabrikam"}""")
                },
                CancellationToken.None);

            result.IsError.Should().BeTrue();
        }

        /// <summary>
        /// Create with Authorization and a company name succeeds.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_CreateWithAuthorization_Succeeds()
        {
            var result = await ExecutorWithAuthorization().ExecuteAsync(
                new ODataExecuteRequest
                {
                    JsonBody = """{"CustomerId":2,"CompanyName":"Fabrikam"}""",
                    Method = HttpMethod.Post,
                    RelativePath = "Customers"
                },
                CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.StatusCode.Should().BeOneOf(200, 201);
        }

        /// <summary>
        /// Create with Authorization but no company name is a bad request.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_CreateEmptyCompanyName_IsBadRequest()
        {
            var result = await ExecutorWithAuthorization().ExecuteAsync(
                new ODataExecuteRequest
                {
                    JsonBody = """{"CustomerId":2,"CompanyName":""}""",
                    Method = HttpMethod.Post,
                    RelativePath = "Customers"
                },
                CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
            result.StatusCode.Should().Be(400);
        }

        /// <summary>
        /// Malformed JSON bodies are rejected by the in-app pipeline.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_MalformedJson_IsNotSuccess()
        {
            var result = await ExecutorWithAuthorization().ExecuteAsync(
                new ODataExecuteRequest
                {
                    JsonBody = """{"CompanyName":""",
                    Method = HttpMethod.Post,
                    RelativePath = "Customers"
                },
                CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
        }

        /// <summary>
        /// Patch without Authorization is unauthorized.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_UpdateWithoutAuthorization_IsError()
        {
            var result = await Runtime().InvokeAsync(
                "odata_update",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Customers"),
                    ["key"] = JsonSerializer.SerializeToElement("1"),
                    ["body"] = JsonSerializer.SerializeToElement("""{"CompanyName":"Updated"}""")
                },
                CancellationToken.None);

            result.IsError.Should().BeTrue();
        }

        /// <summary>
        /// Patch with Authorization updates the seeded customer.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_UpdateWithAuthorization_Succeeds()
        {
            var result = await ExecutorWithAuthorization().ExecuteAsync(
                new ODataExecuteRequest
                {
                    JsonBody = """{"CompanyName":"Updated"}""",
                    Method = HttpMethod.Patch,
                    RelativePath = "Customers(1)"
                },
                CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
        }

        /// <summary>
        /// Delete of a missing key is not found.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_DeleteMissing_IsError()
        {
            var result = await Runtime().InvokeAsync(
                "odata_delete",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Customers"),
                    ["key"] = JsonSerializer.SerializeToElement("999")
                },
                CancellationToken.None);

            result.IsError.Should().BeTrue();
        }

        /// <summary>
        /// Navigate to a missing property fails.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_NavigateMissingProperty_IsError()
        {
            var result = await Runtime().InvokeAsync(
                "odata_navigate",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Customers"),
                    ["key"] = JsonSerializer.SerializeToElement("1"),
                    ["navigation"] = JsonSerializer.SerializeToElement("NotANav")
                },
                CancellationToken.None);

            result.IsError.Should().BeTrue();
        }

        /// <summary>
        /// Get of the seeded customer succeeds.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_GetCustomer_ReturnsContoso()
        {
            var result = await Runtime().InvokeAsync(
                "odata_get",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Customers"),
                    ["key"] = JsonSerializer.SerializeToElement("1")
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
        }

        /// <summary>
        /// The MCP endpoint is mapped under the OData prefix.
        /// </summary>
        [TestMethod]
        public async Task McpEndpoint_IsMapped()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync("/odata/mcp");

            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
        }

        /// <summary>
        /// In-process execute rejects a null request.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_NullRequest_Throws()
        {
            var act = async () => await Executor().ExecuteAsync(null!, CancellationToken.None);

            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        /// <summary>
        /// In-process execute rejects an empty path.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_EmptyPath_Throws()
        {
            var act = async () => await Executor().ExecuteAsync(new ODataExecuteRequest
            {
                Method = HttpMethod.Get,
                RelativePath = " "
            }, CancellationToken.None);

            await act.Should().ThrowAsync<ArgumentException>();
        }

        /// <summary>
        /// Authorization is forwarded from HTTP context onto the in-process request.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_ForwardsAuthorizationHeader()
        {
            var result = await ExecutorWithAuthorization().ExecuteAsync(
                new ODataExecuteRequest
                {
                    Method = HttpMethod.Get,
                    RelativePath = "Customers"
                },
                CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Body.Should().Contain("Contoso");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds an in-process executor that talks to the TestServer without a base address.
        /// </summary>
        /// <param name="authorization">Optional Authorization header on the current HTTP context.</param>
        /// <returns>
        /// The executor.
        /// </returns>
        internal InProcessODataExecutor Executor(string? authorization = null)
        {
            var handler = InProcessODataExecutor.TryCreateServerHandler(TestServer);
            handler.Should().NotBeNull();
            var accessor = new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext()
            };
            accessor.HttpContext.Request.Scheme = "http";
            accessor.HttpContext.Request.Host = new HostString("localhost");
            if (!string.IsNullOrWhiteSpace(authorization))
            {
                accessor.HttpContext.Request.Headers.Authorization = authorization;
            }

            return new InProcessODataExecutor(new TestServerHandlerFactory(handler!), accessor, "odata");
        }

        /// <summary>
        /// Builds an executor that forwards a bearer token.
        /// </summary>
        /// <returns>
        /// The executor.
        /// </returns>
        internal InProcessODataExecutor ExecutorWithAuthorization()
        {
            return Executor("Bearer test-token");
        }

        /// <summary>
        /// Gets the in-process runtime from the test host.
        /// </summary>
        /// <returns>
        /// The runtime.
        /// </returns>
        internal ODataToolRuntime Runtime()
        {
            return TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Runtime;
        }

        #endregion

    }

}
