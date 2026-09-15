// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Hosting
{

    /// <summary>
    /// <see cref="McpHttpContextAccessor"/> swaps Active/Outer/Inner without Microsoft's setter.
    /// </summary>
    [TestClass]
    public class McpHttpContextAccessorTests
    {

        #region Public Methods

        /// <summary>
        /// <c>AddODataMcp</c> last-wins: <see cref="IHttpContextAccessor"/> is <see cref="McpHttpContextAccessor"/>.
        /// </summary>
        [TestMethod]
        public void AddODataMcp_IHttpContextAccessor_IsMcpHttpContextAccessor()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddODataMcp();

            using var provider = services.BuildServiceProvider();
            var accessor = provider.GetRequiredService<IHttpContextAccessor>();
            var typed = provider.GetRequiredService<McpHttpContextAccessor>();

            accessor.Should().BeSameAs(typed);
            accessor.Should().BeOfType<McpHttpContextAccessor>();
        }

        /// <summary>
        /// After <c>End</c>, <c>HttpContext</c> and <see cref="McpHttpContextAccessor.Active"/> are the outer
        /// request and <see cref="McpHttpContextAccessor.Inner"/> is <see langword="null"/>.
        /// </summary>
        [TestMethod]
        public void End_RestoresActiveToOuter_ClearsInner()
        {
            var accessor = new McpHttpContextAccessor();
            var outer = new DefaultHttpContext();
            var inner = new DefaultHttpContext();
            accessor.HttpContext = outer;
            accessor.Start(inner);

            accessor.End();

            accessor.HttpContext.Should().BeSameAs(outer);
            accessor.Active.Should().BeSameAs(outer);
            accessor.Outer.Should().BeSameAs(outer);
            accessor.Inner.Should().BeNull();
        }

        /// <summary>
        /// Host <c>Dispose</c> (<c>HttpContext = null</c>) clears Outer, Active, and Inner on the shared holder.
        /// </summary>
        [TestMethod]
        public void HttpContext_SetNull_ClearsOuterActiveAndInner()
        {
            var accessor = new McpHttpContextAccessor();
            var outer = new DefaultHttpContext();
            var inner = new DefaultHttpContext();
            accessor.HttpContext = outer;
            accessor.Start(inner);

            accessor.HttpContext = null;

            accessor.HttpContext.Should().BeNull();
            accessor.Active.Should().BeNull();
            accessor.Outer.Should().BeNull();
            accessor.Inner.Should().BeNull();
        }

        /// <summary>
        /// Host <c>Initialize</c> (<c>HttpContext = request</c>) loads Outer and Active.
        /// </summary>
        [TestMethod]
        public void HttpContext_Set_LoadsOuterAndActive()
        {
            var accessor = new McpHttpContextAccessor();
            var outer = new DefaultHttpContext();

            accessor.HttpContext = outer;

            accessor.HttpContext.Should().BeSameAs(outer);
            accessor.Active.Should().BeSameAs(outer);
            accessor.Outer.Should().BeSameAs(outer);
            accessor.Inner.Should().BeNull();
        }

        /// <summary>
        /// Inner/outer survive <c>await</c> on the same execution context.
        /// </summary>
        [TestMethod]
        public async Task HttpContext_FlowsAcrossAwait()
        {
            var accessor = new McpHttpContextAccessor();
            var outer = new DefaultHttpContext();
            var inner = new DefaultHttpContext();
            accessor.HttpContext = outer;
            accessor.Start(inner);

            await Task.Yield();

            accessor.HttpContext.Should().BeSameAs(inner);
            accessor.Active.Should().BeSameAs(inner);
            accessor.Outer.Should().BeSameAs(outer);
            accessor.Inner.Should().BeSameAs(inner);

            accessor.End();
            await Task.Yield();

            accessor.HttpContext.Should().BeSameAs(outer);
            accessor.Active.Should().BeSameAs(outer);
            accessor.Inner.Should().BeNull();
        }

        /// <summary>
        /// <c>Start</c> rejects a null inner context.
        /// </summary>
        [TestMethod]
        public void Start_NullInner_Throws()
        {
            var accessor = new McpHttpContextAccessor();

            var act = () => accessor.Start(null!);

            act.Should().Throw<ArgumentNullException>();
        }

        /// <summary>
        /// <c>Start</c> makes <c>HttpContext</c> the inner request without dropping the outer one.
        /// </summary>
        [TestMethod]
        public void Start_SwapsActiveToInner_KeepsOuter()
        {
            var accessor = new McpHttpContextAccessor();
            var outer = new DefaultHttpContext();
            var inner = new DefaultHttpContext();
            accessor.HttpContext = outer;

            accessor.Start(inner);

            accessor.HttpContext.Should().BeSameAs(inner);
            accessor.Active.Should().BeSameAs(inner);
            accessor.Outer.Should().BeSameAs(outer);
            accessor.Inner.Should().BeSameAs(inner);
        }

        #endregion

    }

}
