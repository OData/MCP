// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Hosting
{

    /// <summary>
    /// <c>AddODataMcp</c> registers the pipeline holder disabled; no host is required.
    /// </summary>
    [TestClass]
    public class ODataMcpPipelineRegistrationTests
    {

        #region Public Methods

        /// <summary>
        /// <c>AddODataMcp</c> registers a disabled pipeline holder and the startup filter.
        /// </summary>
        [TestMethod]
        public void AddODataMcp_RegistersDisabledPipelineAndFilter()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddODataMcp();

            using var provider = services.BuildServiceProvider();
            var pipeline = provider.GetRequiredService<ODataMcpPipeline>();

            pipeline.IsEnabled.Should().BeFalse();
            pipeline.Pipeline.Should().BeNull();
            provider.GetServices<IStartupFilter>().Should().Contain(filter => filter is ODataMcpPipelineStartupFilter);
        }

        /// <summary>
        /// The filter constructor rejects a null pipeline holder.
        /// </summary>
        [TestMethod]
        public void PipelineStartupFilter_NullPipeline_Throws()
        {
            var act = () => new ODataMcpPipelineStartupFilter(null!);

            act.Should().Throw<ArgumentNullException>();
        }

        /// <summary>
        /// <c>Configure</c> rejects a null continuation.
        /// </summary>
        [TestMethod]
        public void PipelineStartupFilter_NullNext_Throws()
        {
            var filter = new ODataMcpPipelineStartupFilter(new ODataMcpPipeline { IsEnabled = true });

            var act = () => filter.Configure(null!);

            act.Should().Throw<ArgumentNullException>();
        }

        #endregion

    }

}
