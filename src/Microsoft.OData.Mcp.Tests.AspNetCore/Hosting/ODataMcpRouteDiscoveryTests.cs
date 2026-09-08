// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Edm;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Hosting
{

    /// <summary>
    /// Discovery tests that duck-type prefix and <see cref="IEdmModel"/> without compiling the host against OData types.
    /// </summary>
    [TestClass]
    public class ODataMcpRouteDiscoveryTests
    {

        #region Public Methods

        /// <summary>
        /// Catch-all templates from Restier and OData 7 yield prefix and route name.
        /// </summary>
        [TestMethod]
        public void TryParseODataCatchAll_RestierAndOData7_Templates()
        {
            ODataMcpRouteDiscovery.TryParseODataCatchAll("odata/{**ODataEndpointPath_odata}", out var restierPrefix, out var restierName)
                .Should().BeTrue();
            restierPrefix.Should().Be("odata");
            restierName.Should().Be("odata");

            ODataMcpRouteDiscovery.TryParseODataCatchAll("api/v1/{*ODataEndpointPath_ApiV1}", out var nestedPrefix, out var nestedName)
                .Should().BeTrue();
            nestedPrefix.Should().Be("api/v1");
            nestedName.Should().Be("ApiV1");

            ODataMcpRouteDiscovery.TryParseODataCatchAll("{**ODataEndpointPath_root}", out var rootPrefix, out var rootName)
                .Should().BeTrue();
            rootPrefix.Should().BeEmpty();
            rootName.Should().Be("root");
        }

        /// <summary>
        /// Non-OData templates are ignored.
        /// </summary>
        [TestMethod]
        public void TryParseODataCatchAll_NonOData_ReturnsFalse()
        {
            ODataMcpRouteDiscovery.TryParseODataCatchAll("odata/Customers", out _, out _).Should().BeFalse();
            ODataMcpRouteDiscovery.TryParseODataCatchAll(null, out _, out _).Should().BeFalse();
            ODataMcpRouteDiscovery.TryParseODataCatchAll("   ", out _, out _).Should().BeFalse();
            ODataMcpRouteDiscovery.TryParseODataCatchAll("{**ODataEndpointPath_}", out _, out _).Should().BeFalse();
        }

        /// <summary>
        /// Duck-typed Prefix and Model metadata is accepted.
        /// </summary>
        [TestMethod]
        public void TryReadPrefixAndModel_FakeMetadata_Succeeds()
        {
            var model = TestModels.GetMinimalModel();
            var metadata = new FakeODataRoutingMetadata
            {
                Model = model,
                Prefix = "odata"
            };

            ODataMcpRouteDiscovery.TryReadPrefixAndModel(metadata, out var prefix, out var read)
                .Should().BeTrue();
            prefix.Should().Be("odata");
            read.Should().BeSameAs(model);
        }

        /// <summary>
        /// Objects without Prefix and Model are ignored.
        /// </summary>
        [TestMethod]
        public void TryReadPrefixAndModel_WrongShape_Fails()
        {
            ODataMcpRouteDiscovery.TryReadPrefixAndModel("not-metadata", out _, out _).Should().BeFalse();
            ODataMcpRouteDiscovery.TryReadPrefixAndModel(new MissingModelMetadata { Prefix = "odata" }, out _, out _).Should().BeFalse();
        }

        /// <summary>
        /// Endpoint metadata discovery finds a fake OData 8 routing metadata object.
        /// </summary>
        [TestMethod]
        public void Discover_FromEndpointMetadata_FindsPrefix()
        {
            var model = TestModels.GetSimpleModel();
            var endpoint = CreateEndpoint("odata/{**odataPath}", new FakeODataRoutingMetadata
            {
                Model = model,
                Prefix = "odata"
            });
            var services = new ServiceCollection();
            services.AddSingleton<EndpointDataSource>(new StaticEndpointDataSource(endpoint));
            using var provider = services.BuildServiceProvider();

            var bindings = ODataMcpRouteDiscovery.Discover(provider, new ODataMcpHostOptions());

            bindings.Should().ContainSingle();
            bindings[0].Prefix.Should().Be("odata");
            bindings[0].Model.Should().BeSameAs(model);
        }

        /// <summary>
        /// IncludePrefixes keeps one of two metadata prefixes.
        /// </summary>
        [TestMethod]
        public void Discover_IncludePrefixes_Filters()
        {
            var odata = CreateEndpoint("odata/{**x}", new FakeODataRoutingMetadata
            {
                Model = TestModels.GetSimpleModel(),
                Prefix = "odata"
            });
            var hidden = CreateEndpoint("internal/{**x}", new FakeODataRoutingMetadata
            {
                Model = TestModels.GetMinimalModel(),
                Prefix = "internal"
            });
            var services = new ServiceCollection();
            services.AddSingleton<EndpointDataSource>(new StaticEndpointDataSource(odata, hidden));
            using var provider = services.BuildServiceProvider();
            var options = new ODataMcpHostOptions();
            options.IncludePrefixes.Add("/odata/");

            var bindings = ODataMcpRouteDiscovery.Discover(provider, options);

            bindings.Select(binding => binding.Prefix).Should().Equal("odata");
        }

        /// <summary>
        /// ExcludeRoutes drops a prefix even when it was discovered.
        /// </summary>
        [TestMethod]
        public void Discover_ExcludeRoutes_DropsPrefix()
        {
            var odata = CreateEndpoint("odata/{**x}", new FakeODataRoutingMetadata
            {
                Model = TestModels.GetSimpleModel(),
                Prefix = "odata"
            });
            var hidden = CreateEndpoint("internal/{**x}", new FakeODataRoutingMetadata
            {
                Model = TestModels.GetMinimalModel(),
                Prefix = "internal"
            });
            var services = new ServiceCollection();
            services.AddSingleton<EndpointDataSource>(new StaticEndpointDataSource(odata, hidden));
            using var provider = services.BuildServiceProvider();
            var options = new ODataMcpHostOptions();
            options.ExcludeRoutes.Add("internal");

            var bindings = ODataMcpRouteDiscovery.Discover(provider, options);

            bindings.Select(binding => binding.Prefix).Should().Equal("odata");
        }

        /// <summary>
        /// Explicit routes are used when no endpoints exist.
        /// </summary>
        [TestMethod]
        public void Discover_ExplicitRoute_WithoutEndpoints()
        {
            var services = new ServiceCollection();
            using var provider = services.BuildServiceProvider();
            var options = new ODataMcpHostOptions();
            options.AddRoute("manual", TestModels.GetMinimalModel());

            var bindings = ODataMcpRouteDiscovery.Discover(provider, options);

            bindings.Should().ContainSingle();
            bindings[0].Prefix.Should().Be("manual");
        }

        /// <summary>
        /// Catch-all endpoints without a per-route container do not invent a model.
        /// </summary>
        [TestMethod]
        public void Discover_CatchAllWithoutContainer_DoesNotAddRoute()
        {
            var endpoint = CreateEndpoint("odata/{**ODataEndpointPath_odata}");
            var services = new ServiceCollection();
            services.AddSingleton<EndpointDataSource>(new StaticEndpointDataSource(endpoint));
            using var provider = services.BuildServiceProvider();

            var bindings = ODataMcpRouteDiscovery.Discover(provider, new ODataMcpHostOptions());

            bindings.Should().BeEmpty();
        }

        /// <summary>
        /// RouteComponents on OData 8 options are picked up when endpoints are empty.
        /// </summary>
        [TestMethod]
        public void Discover_RouteComponentsFallback_FindsODataEightOptions()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddControllers().AddOData(options =>
            {
                options.AddRouteComponents("odata", TestModels.GetSimpleModel());
                options.AddRouteComponents("reporting", TestModels.GetMinimalModel());
            });
            using var provider = services.BuildServiceProvider();

            var bindings = ODataMcpRouteDiscovery.Discover(provider, new ODataMcpHostOptions());

            bindings.Select(binding => binding.Prefix).Should().BeEquivalentTo("odata", "reporting");
        }

        /// <summary>
        /// Tuple Item1 is read as the model from a RouteComponents value.
        /// </summary>
        [TestMethod]
        public void TryReadModel_TupleItem1_ReturnsModel()
        {
            var model = TestModels.GetMinimalModel();
            var tuple = (model, "unused");

            ODataMcpRouteDiscovery.TryReadModel(tuple).Should().BeSameAs(model);
            ODataMcpRouteDiscovery.TryReadModel(model).Should().BeSameAs(model);
            ODataMcpRouteDiscovery.TryReadModel(null).Should().BeNull();
            ODataMcpRouteDiscovery.TryReadModel("nope").Should().BeNull();
        }

        /// <summary>
        /// Discovery rejects null arguments.
        /// </summary>
        [TestMethod]
        public void Discover_NullArguments_Throw()
        {
            var services = new ServiceCollection().BuildServiceProvider();
            var options = new ODataMcpHostOptions();

            var nullServices = () => ODataMcpRouteDiscovery.Discover(null!, options);
            var nullOptions = () => ODataMcpRouteDiscovery.Discover(services, null!);

            nullServices.Should().Throw<ArgumentNullException>();
            nullOptions.Should().Throw<ArgumentNullException>();
        }

        /// <summary>
        /// NormalizePrefix trims slashes.
        /// </summary>
        [TestMethod]
        public void NormalizePrefix_TrimsSlashes()
        {
            ODataMcpRouteDiscovery.NormalizePrefix("/odata/").Should().Be("odata");
            ODataMcpRouteDiscovery.NormalizePrefix(null).Should().BeEmpty();
            ODataMcpRouteDiscovery.NormalizePrefix("").Should().BeEmpty();
        }

        /// <summary>
        /// Per-route lookup with an empty route name throws.
        /// </summary>
        [TestMethod]
        public void TryGetModelFromPerRouteContainer_BlankRoute_Throws()
        {
            using var provider = new ServiceCollection().BuildServiceProvider();
            var act = () => ODataMcpRouteDiscovery.TryGetModelFromPerRouteContainer(provider, " ");

            act.Should().Throw<ArgumentException>();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds a route endpoint with optional metadata.
        /// </summary>
        /// <param name="pattern">The route pattern.</param>
        /// <param name="metadata">Optional endpoint metadata.</param>
        /// <returns>
        /// The endpoint.
        /// </returns>
        internal static Endpoint CreateEndpoint(string pattern, object? metadata = null)
        {
            var builder = new RouteEndpointBuilder(
                _ => Task.CompletedTask,
                RoutePatternFactory.Parse(pattern),
                order: 0);
            if (metadata is not null)
            {
                builder.Metadata.Add(metadata);
            }

            return builder.Build();
        }

        #endregion

    }

}
