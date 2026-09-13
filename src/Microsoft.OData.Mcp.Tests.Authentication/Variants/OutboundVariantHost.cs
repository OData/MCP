// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Authentication.Variants
{

    /// <summary>
    /// The shared shape of a fixture variant: the same secured OData 8 host and the same linked tool surface as
    /// the full suite, driven by a handful of representative operations so a variant can assert on what the
    /// authorization server saw rather than re-running four hundred tests per grant.
    /// </summary>
    /// <example>
    /// <code>
    /// [TestClass]
    /// public class MyVariantTests : OutboundVariantHost
    /// {
    ///     internal override LocalAuthorizationServerOptions CreateAuthorizationServerOptions()
    ///     {
    ///         return new LocalAuthorizationServerOptions { AccessTokenLifetime = TimeSpan.FromSeconds(20) };
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <remarks>
    /// The operations chosen are the ones that cover every HTTP verb the outbound path attaches a token to:
    /// a catalog read that needs none, two GETs, a POST, and a DELETE. A variant that changes the grant, the
    /// token lifetime, or the discovery shape has to survive all five or its credential never reached the wire.
    /// </remarks>
    public abstract class OutboundVariantHost : ConventionRichHost
    {

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
            await RunRepresentativeToolsAsync();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Runs the representative tool operations and asserts each one reached the secured service.
        /// </summary>
        /// <returns>
        /// A task that completes once every operation has run.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown when the created row carries no readable key.</exception>
        internal async Task RunRepresentativeToolsAsync()
        {
            var listed = await InvokeAsync("odata_list_entity_sets");
            listed.IsError.Should().BeFalse(listed.Text);
            listed.StructuredContent.Should().Contain("Customers");

            var queried = await InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers"));
            queried.IsError.Should().BeFalse(queried.Text);
            queried.StructuredContent.Should().Contain("Contoso");

            var got = await InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "1"));
            got.IsError.Should().BeFalse(got.Text);

            var created = await InvokeAsync("odata_create", ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"VariantCo"}"""));
            created.IsError.Should().BeFalse(created.Text);

            var key = ReadCustomerId(created.StructuredContent);
            var deleted = await InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", key.ToString()));
            deleted.IsError.Should().BeFalse(deleted.Text);
        }

        /// <summary>
        /// Reads the customer key out of an OData JSON payload.
        /// </summary>
        /// <param name="json">The payload the create tool returned.</param>
        /// <returns>
        /// The key.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown when the payload carries no readable key.</exception>
        internal static int ReadCustomerId(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("The create tool returned no structured content to read a key from.");
            }

            using var document = JsonDocument.Parse(json);
            foreach (var name in new[] { "CustomerId", "customerId" })
            {
                if (document.RootElement.TryGetProperty(name, out var value) && value.TryGetInt32(out var id))
                {
                    return id;
                }
            }

            throw new InvalidOperationException(json);
        }

        #endregion

    }

}
