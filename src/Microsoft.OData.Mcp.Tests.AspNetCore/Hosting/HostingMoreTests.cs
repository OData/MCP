// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.AspNetCore.Execution;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Execution;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Hosting
{

    /// <summary>
    /// Additional host and in-process executor tests.
    /// </summary>
    [TestClass]
    public class HostingMoreTests
    {

        #region Public Methods

        /// <summary>
        /// Catalog option copies set the route name from the prefix.
        /// </summary>
        [TestMethod]
        public void CopyCatalogOptions_SetsRouteName()
        {
            var copy = ODataMcpSessionFactory.CopyCatalogOptions(
                new ODataMcpCatalogOptions
                {
                    MaxNamedTools = 20,
                    IncludeCreate = false
                },
                "odata");

            copy.RouteName.Should().Be("odata");
            copy.MaxNamedTools.Should().Be(20);
            copy.IncludeCreate.Should().BeFalse();
        }

        /// <summary>
        /// Catalog option copies carry the shape options: enum wire format, preface, and dynamic model.
        /// </summary>
        [TestMethod]
        public void CopyCatalogOptions_CopiesShapeOptions()
        {
            var copy = ODataMcpSessionFactory.CopyCatalogOptions(
                new ODataMcpCatalogOptions
                {
                    EnumJsonFormat = ODataEnumJsonFormat.Integer,
                    InstructionsPreface = "Contoso.",
                    IsDynamicModel = true
                },
                "odata");

            copy.EnumJsonFormat.Should().Be(ODataEnumJsonFormat.Integer);
            copy.InstructionsPreface.Should().Be("Contoso.");
            copy.IsDynamicModel.Should().BeTrue();
        }

        /// <summary>
        /// In-process URIs prefix $ on query keys once.
        /// </summary>
        [TestMethod]
        public void InProcessExecutor_BuildRelativeUri_PrefixesQuery()
        {
            var executor = CreateUriExecutor("odata");
            var uri = executor.BuildRelativeUri(new ODataExecuteRequest
            {
                RelativePath = "Customers",
                QueryOptions =
                {
                    ["filter"] = "CustomerId eq 1",
                    ["top"] = "1"
                }
            });

            uri.Should().StartWith("odata/Customers?");
            uri.Should().Contain("$filter=");
            uri.Should().Contain("$top=");
            uri.Should().NotContain("$$");
        }

        /// <summary>
        /// An empty route prefix does not add a segment.
        /// </summary>
        [TestMethod]
        public void InProcessExecutor_EmptyPrefix_OmitsPrefix()
        {
            var executor = CreateUriExecutor("");
            executor.BuildRelativeUri(new ODataExecuteRequest
            {
                RelativePath = "Customers"
            }).Should().Be("Customers");
        }

        /// <summary>
        /// Query keys that already have $ are not doubled.
        /// </summary>
        [TestMethod]
        public void InProcessExecutor_BuildRelativeUri_DoesNotDoubleDollar()
        {
            var executor = CreateUriExecutor("odata");
            var uri = executor.BuildRelativeUri(new ODataExecuteRequest
            {
                QueryOptions = { ["$top"] = "1" },
                RelativePath = "Customers"
            });

            uri.Should().Contain("$top=1");
            uri.Should().NotContain("$$");
        }

        /// <summary>
        /// Session factory resolve returns the only session.
        /// </summary>
        [TestMethod]
        public void SessionFactory_SingleSession_ResolvesWithoutHttp()
        {
            var factory = CreateFactory("odata");

            factory.Resolve(null).Should().BeSameAs(factory.Sessions.Values.Single());
        }

        /// <summary>
        /// Multiple sessions match the MCP path prefix.
        /// </summary>
        [TestMethod]
        public void SessionFactory_MultipleSessions_MatchesPath()
        {
            var factory = CreateFactory("odata", "api");
            var http = new DefaultHttpContext();
            http.Request.Path = "/api/mcp";

            factory.Resolve(http).Should().BeSameAs(factory.Sessions["api"]);
        }

        /// <summary>
        /// Unknown paths throw.
        /// </summary>
        [TestMethod]
        public void SessionFactory_UnknownPath_Throws()
        {
            var factory = CreateFactory("odata", "api");
            var http = new DefaultHttpContext();
            http.Request.Path = "/other/mcp";

            var act = () => factory.Resolve(http);
            act.Should().Throw<InvalidOperationException>();
        }

        /// <summary>
        /// ASP.NET JSON option instances are configured.
        /// </summary>
        [TestMethod]
        public void AspNetCoreJsonConstants_AreConfigured()
        {
            Microsoft.OData.Mcp.AspNetCore.Constants.AspNetCoreJsonConstants.ApiResponse.Should().NotBeNull();
            Microsoft.OData.Mcp.AspNetCore.Constants.AspNetCoreJsonConstants.ErrorResponse.PropertyNamingPolicy.Should().NotBeNull();
            Microsoft.OData.Mcp.AspNetCore.Constants.AspNetCoreJsonConstants.HealthCheck.WriteIndented.Should().BeFalse();
            Microsoft.OData.Mcp.AspNetCore.Constants.AspNetCoreJsonConstants.McpResponse.Should().BeSameAs(Microsoft.OData.Mcp.AspNetCore.Constants.AspNetCoreJsonConstants.ApiResponse);
        }

        /// <summary>
        /// Empty prefixes copy as route name odata.
        /// </summary>
        [TestMethod]
        public void CopyCatalogOptions_EmptyPrefix_UsesOdata()
        {
            var copy = ODataMcpSessionFactory.CopyCatalogOptions(new ODataMcpCatalogOptions(), "  ");

            copy.RouteName.Should().Be("odata");
        }

        /// <summary>
        /// CopyCatalogOptions rejects a null source.
        /// </summary>
        [TestMethod]
        public void CopyCatalogOptions_Null_Throws()
        {
            var act = () => ODataMcpSessionFactory.CopyCatalogOptions(null!, "odata");

            act.Should().Throw<ArgumentNullException>();
        }

        /// <summary>
        /// An empty-prefix session matches <c>/mcp</c> when multiple routes exist.
        /// </summary>
        [TestMethod]
        public void SessionFactory_EmptyPrefix_ResolvesMcp()
        {
            var factory = CreateFactory("", "odata");
            var http = new DefaultHttpContext();
            http.Request.Path = "/mcp";

            factory.Resolve(http).Should().BeSameAs(factory.Sessions[""]);
        }

        /// <summary>
        /// <c>UseODataMcp</c> rejects a null application builder.
        /// </summary>
        [TestMethod]
        public void UseODataMcp_NullApp_Throws()
        {
            var act = () => ODataMcp_AspNetCore_ApplicationBuilderExtensions.UseODataMcp(null!);

            act.Should().Throw<ArgumentNullException>();
        }

        /// <summary>
        /// Host conventions attach rate limiting and authorization metadata.
        /// </summary>
        [TestMethod]
        public void ApplyHostConventions_AttachesPolicies()
        {
            var builder = new RecordingConventionBuilder();
            ODataMcp_AspNetCore_ApplicationBuilderExtensions.ApplyHostConventions(builder, new ODataMcpHostOptions
            {
                RateLimitingPolicyName = "mcp",
                RequireAuthorization = true
            });

            builder.Conventions.Count.Should().Be(2);
        }

        /// <summary>
        /// Session factory constructor rejects null dependencies.
        /// </summary>
        [TestMethod]
        public void SessionFactory_NullDependencies_Throw()
        {
            var services = new ServiceCollection().BuildServiceProvider();
            var options = Options.Create(new ODataMcpHostOptions());
            var pipeline = new ODataMcpPipeline();
            var accessor = new McpHttpContextAccessor();

            var nullServices = () => new ODataMcpSessionFactory(null!, options, pipeline, accessor);
            var nullOptions = () => new ODataMcpSessionFactory(services, null!, pipeline, accessor);
            var nullPipeline = () => new ODataMcpSessionFactory(services, options, null!, accessor);
            var nullAccessor = () => new ODataMcpSessionFactory(services, options, pipeline, null!);

            nullServices.Should().Throw<ArgumentNullException>();
            nullOptions.Should().Throw<ArgumentNullException>();
            nullPipeline.Should().Throw<ArgumentNullException>();
            nullAccessor.Should().Throw<ArgumentNullException>();
        }

        /// <summary>
        /// Host options AddRoute rejects null arguments.
        /// </summary>
        [TestMethod]
        public void HostOptions_AddRoute_Null_Throws()
        {
            var options = new ODataMcpHostOptions();
            var model = TestModels.GetMinimalModel();

            var nullPrefix = () => options.AddRoute(null!, model);
            var nullModel = () => options.AddRoute("odata", null!);

            nullPrefix.Should().Throw<ArgumentNullException>();
            nullModel.Should().Throw<ArgumentNullException>();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds a session factory over explicit models for the given prefixes.
        /// </summary>
        /// <param name="prefixes">OData route prefixes.</param>
        /// <returns>
        /// The factory.
        /// </returns>
        internal static ODataMcpSessionFactory CreateFactory(params string[] prefixes)
        {
            var host = new ODataMcpHostOptions();
            foreach (var prefix in prefixes)
            {
                host.AddRoute(prefix, TestModels.GetSimpleModel());
            }

            var services = new ServiceCollection();
            services.AddSingleton(Options.Create(host));
            var provider = services.BuildServiceProvider();

            return new ODataMcpSessionFactory(
                provider,
                Options.Create(host),
                new ODataMcpPipeline(),
                new McpHttpContextAccessor());
        }

        /// <summary>
        /// Builds an executor used only for URI composition.
        /// </summary>
        /// <param name="prefix">The OData route prefix.</param>
        /// <returns>
        /// The executor.
        /// </returns>
        internal static InProcessODataExecutor CreateUriExecutor(string prefix)
        {
            return new InProcessODataExecutor(
                new ODataMcpPipeline(),
                new McpHttpContextAccessor(),
                new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
                prefix);
        }

        #endregion

    }

}
