// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using FluentAssertions;
using Microsoft.OData.Mcp.AspNetCore.Authentication;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Authentication
{

    /// <summary>
    /// <see cref="ProtectedResourceMetadataOptions.Validate"/> is what turns a misconfigured API into a host that
    /// refuses to start rather than one that misdirects every client.
    /// </summary>
    [TestClass]
    public class ProtectedResourceMetadataOptionsTests
    {

        #region Public Methods

        /// <summary>
        /// An empty string is the application root, the same value Endpoint Routing accepts.
        /// </summary>
        [TestMethod]
        public void Validate_EmptyStringPrefix_Passes()
        {
            var options = new ProtectedResourceMetadataOptions();
            options.AuthorizationServers.Add(new Uri("https://login.example.com/contoso/v2.0"));
            options.Prefixes.Add(string.Empty);

            Action act = options.Validate;

            act.Should().NotThrow();
        }

        /// <summary>
        /// A loopback authorization server over plain http is how a test host and a local identity provider are
        /// modelled, so it passes.
        /// </summary>
        [TestMethod]
        public void Validate_LoopbackHttpAuthorizationServer_Passes()
        {
            var options = new ProtectedResourceMetadataOptions();
            options.AuthorizationServers.Add(new Uri("http://127.0.0.1:5000"));

            Action act = options.Validate;

            act.Should().NotThrow();
        }

        /// <summary>
        /// A nested route base is a valid Endpoint Routing prefix.
        /// </summary>
        [TestMethod]
        public void Validate_NestedPrefix_Passes()
        {
            var options = new ProtectedResourceMetadataOptions();
            options.AuthorizationServers.Add(new Uri("https://login.example.com/contoso/v2.0"));
            options.Prefixes.Add("api/v1");

            Action act = options.Validate;

            act.Should().NotThrow();
        }

        /// <summary>
        /// A document with no authorization server tells a client nothing at all.
        /// </summary>
        [TestMethod]
        public void Validate_NoAuthorizationServers_Throws()
        {
            var options = new ProtectedResourceMetadataOptions();

            Action act = options.Validate;

            act.Should().Throw<InvalidOperationException>().WithMessage($"*{nameof(ProtectedResourceMetadataOptions.AuthorizationServers)}*");
        }

        /// <summary>
        /// A relative authorization server is not something a client can probe.
        /// </summary>
        [TestMethod]
        public void Validate_NonAbsoluteAuthorizationServer_Throws()
        {
            var options = new ProtectedResourceMetadataOptions();
            options.AuthorizationServers.Add(new Uri("/oauth/v2.0", UriKind.Relative));

            Action act = options.Validate;

            act.Should().Throw<InvalidOperationException>().WithMessage("*absolute*");
        }

        /// <summary>
        /// A plain http authorization server that is not loopback would carry every credential in the clear.
        /// </summary>
        [TestMethod]
        public void Validate_NonLoopbackHttpAuthorizationServer_Throws()
        {
            var options = new ProtectedResourceMetadataOptions();
            options.AuthorizationServers.Add(new Uri("http://example.com"));

            Action act = options.Validate;

            act.Should().Throw<InvalidOperationException>().WithMessage("*https*");
        }

        /// <summary>
        /// A null explicit prefix is not a route base.
        /// </summary>
        [TestMethod]
        public void Validate_NullPrefix_Throws()
        {
            var options = new ProtectedResourceMetadataOptions();
            options.AuthorizationServers.Add(new Uri("https://login.example.com/contoso/v2.0"));
            options.Prefixes.Add(null!);

            Action act = options.Validate;

            act.Should().Throw<InvalidOperationException>().WithMessage($"*{nameof(ProtectedResourceMetadataOptions.Prefixes)}*");
        }

        /// <summary>
        /// <c>/</c> is the application root.
        /// </summary>
        [TestMethod]
        public void Validate_SlashPrefix_Passes()
        {
            var options = new ProtectedResourceMetadataOptions();
            options.AuthorizationServers.Add(new Uri("https://login.example.com/contoso/v2.0"));
            options.Prefixes.Add("/");

            Action act = options.Validate;

            act.Should().NotThrow();
        }

        /// <summary>
        /// A whitespace-only explicit prefix is a typo, not a root service.
        /// </summary>
        [TestMethod]
        public void Validate_WhitespacePrefix_Throws()
        {
            var options = new ProtectedResourceMetadataOptions();
            options.AuthorizationServers.Add(new Uri("https://login.example.com/contoso/v2.0"));
            options.Prefixes.Add("   ");

            Action act = options.Validate;

            act.Should().Throw<InvalidOperationException>().WithMessage($"*{nameof(ProtectedResourceMetadataOptions.Prefixes)}*");
        }

        #endregion

    }

}
