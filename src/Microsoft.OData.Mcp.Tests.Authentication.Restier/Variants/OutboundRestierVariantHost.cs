// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Tests.AspNetCore.Restier;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Authentication.Restier.Variants
{

    /// <summary>
    /// The shared shape of a Restier fixture variant: the same secured OData 7 host and the same linked tool
    /// surface as the full suite, driven by a handful of representative operations so a variant can assert on
    /// what the authorization server saw rather than re-running the whole suite per grant.
    /// </summary>
    /// <example>
    /// <code>
    /// [TestClass]
    /// public class MyRestierVariantTests : OutboundRestierVariantHost
    /// {
    ///     public MyRestierVariantTests()
    ///         : base("MyRestierVariant")
    ///     {
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <remarks>
    /// The operations chosen cover every HTTP verb the outbound path attaches a token to: a catalog read that
    /// needs none, two GETs, a POST, and a DELETE. A variant that changes the grant or the token lifetime has to
    /// survive all five or its credential never reached the wire.
    /// </remarks>
    public abstract class OutboundRestierVariantHost : RestierToolTestBase
    {

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="OutboundRestierVariantHost"/> class.
        /// </summary>
        /// <param name="databasePrefix">The prefix used to build the in-memory database name.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="databasePrefix"/> is <see langword="null"/>, empty, or whitespace.</exception>
        protected OutboundRestierVariantHost(string databasePrefix)
            : base(databasePrefix)
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Every representative operation succeeds under this variant's outbound settings.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task RepresentativeTools_AllSucceed()
        {
            await RunRepresentativeToolsAsync(20);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Runs the representative tool operations and asserts each one reached the secured service.
        /// </summary>
        /// <param name="key">The key the created row is written under and then deleted by.</param>
        /// <returns>
        /// A task that completes once every operation has run.
        /// </returns>
        /// <remarks>
        /// Restier's entity keys are supplied by the caller rather than generated, so each variant passes a key
        /// no other test in its own database has used.
        /// </remarks>
        internal async Task RunRepresentativeToolsAsync(int key)
        {
            var listed = await Runtime().InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);
            listed.IsError.Should().BeFalse(listed.Text);
            listed.StructuredContent.Should().Contain("Customers");

            var queried = await Runtime().InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers"), CancellationToken.None);
            queried.IsError.Should().BeFalse(queried.Text);
            queried.StructuredContent.Should().Contain("Contoso");

            var got = await Runtime().InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "1"), CancellationToken.None);
            got.IsError.Should().BeFalse(got.Text);

            var created = await Runtime().InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", $$"""{"Id":{{key}},"CompanyName":"VariantCo"}"""),
                CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);

            var deleted = await Runtime().InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", key.ToString()), CancellationToken.None);
            deleted.IsError.Should().BeFalse(deleted.Text);
        }

        #endregion

    }

}
