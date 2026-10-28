// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using CloudNimble.McpEssentials.Core.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.Extensions.Tasks;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Hosting
{

    /// <summary>
    /// <c>WithTasks</c> stamps TTL so Claude Code receives <c>ttlMs</c> on a task handle.
    /// </summary>
    [TestClass]
    public class ODataMcpServerBuilderTests
    {

        #region Public Methods

        /// <summary>
        /// <see cref="InMemoryMcpTaskStore"/> gets <c>DefaultTimeToLive</c> so serialized
        /// <c>CreateTaskResult</c> includes <c>ttlMs</c>.
        /// </summary>
        [TestMethod]
        public void ApplyDefaultTimeToLive_InMemoryStore_SetsTtl()
        {
            var store = new InMemoryMcpTaskStore();

            ODataMcpServerBuilder.ApplyDefaultTimeToLive(store, TimeSpan.FromMinutes(10));

            store.DefaultTimeToLive.Should().Be(TimeSpan.FromMinutes(10));
        }

        /// <summary>
        /// Plus stores are stamped through <see cref="IMcpTaskStorePlus.DefaultTimeToLive"/>.
        /// </summary>
        [TestMethod]
        public void ApplyDefaultTimeToLive_PlusStore_SetsTtl()
        {
            var store = new InMemoryMcpTaskStorePlus("alice");

            ODataMcpServerBuilder.ApplyDefaultTimeToLive(store, TimeSpan.FromMinutes(10));

            store.DefaultTimeToLive.Should().Be(TimeSpan.FromMinutes(10));
        }

        #endregion

    }

}
