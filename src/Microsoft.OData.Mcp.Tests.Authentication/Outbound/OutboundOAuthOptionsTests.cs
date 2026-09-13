// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using FluentAssertions;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Authentication.Outbound
{

    /// <summary>
    /// Locks the default values, <see cref="OutboundOAuthOptions.Validate"/> rules, and
    /// <see cref="OutboundOAuthOptions.FromEnvironment(OutboundOAuthOptions)"/> environment binding of
    /// <see cref="OutboundOAuthOptions"/>.
    /// </summary>
    /// <remarks>
    /// Every test that sets an environment variable restores it in a <c>finally</c> block; this class is not
    /// safe to run in parallel with itself, which is why the assembly-wide <c>Parallelize(Workers = 1)</c>
    /// attribute in <c>Directory.Build.props</c> applies.
    /// </remarks>
    [TestClass]
    public class OutboundOAuthOptionsTests
    {

        #region Public Methods

        /// <summary>
        /// The default <see cref="OutboundOAuthOptions.AuthTimeout"/> is 300 seconds.
        /// </summary>
        [TestMethod]
        public void AuthTimeout_Default_Is300Seconds()
        {
            new OutboundOAuthOptions().AuthTimeout.Should().Be(TimeSpan.FromSeconds(300));
        }

        /// <summary>
        /// <see cref="OutboundOAuthOptions.FromEnvironment(OutboundOAuthOptions)"/> leaves an explicit
        /// <see cref="OutboundOAuthOptions.ClientSecret"/> untouched even when the environment variable is set.
        /// </summary>
        [TestMethod]
        public void FromEnvironment_ClientSecretAlreadySet_DoesNotOverwrite()
        {
            var previous = Environment.GetEnvironmentVariable(ODataMcpAuthConstants.ClientSecretEnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(ODataMcpAuthConstants.ClientSecretEnvironmentVariable, "from-environment");
                var options = new OutboundOAuthOptions
                {
                    ClientSecret = "explicit-secret"
                };

                OutboundOAuthOptions.FromEnvironment(options);

                options.ClientSecret.Should().Be("explicit-secret");
            }
            finally
            {
                Environment.SetEnvironmentVariable(ODataMcpAuthConstants.ClientSecretEnvironmentVariable, previous);
            }
        }

        /// <summary>
        /// <see cref="OutboundOAuthOptions.FromEnvironment(OutboundOAuthOptions)"/> binds
        /// <see cref="OutboundOAuthOptions.ClientSecret"/> from <c>ODATA_MCP_CLIENT_SECRET</c> when unset.
        /// </summary>
        [TestMethod]
        public void FromEnvironment_ClientSecretUnset_BindsFromEnvironmentVariable()
        {
            var previous = Environment.GetEnvironmentVariable(ODataMcpAuthConstants.ClientSecretEnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(ODataMcpAuthConstants.ClientSecretEnvironmentVariable, "from-environment");
                var options = new OutboundOAuthOptions();

                OutboundOAuthOptions.FromEnvironment(options);

                options.ClientSecret.Should().Be("from-environment");
            }
            finally
            {
                Environment.SetEnvironmentVariable(ODataMcpAuthConstants.ClientSecretEnvironmentVariable, previous);
            }
        }

        /// <summary>
        /// <see cref="OutboundOAuthOptions.FromEnvironment(OutboundOAuthOptions)"/> sets
        /// <see cref="OutboundOAuthOptions.HasEnvironmentIdToken"/> when <c>ODATA_MCP_ID_TOKEN</c> is present and
        /// <see cref="OutboundOAuthOptions.IdpIdTokenFile"/> is unset.
        /// </summary>
        [TestMethod]
        public void FromEnvironment_IdTokenVariableSet_SetsHasEnvironmentIdToken()
        {
            var previous = Environment.GetEnvironmentVariable(ODataMcpAuthConstants.IdTokenEnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(ODataMcpAuthConstants.IdTokenEnvironmentVariable, "an-id-token");
                var options = new OutboundOAuthOptions();

                OutboundOAuthOptions.FromEnvironment(options);

                options.HasEnvironmentIdToken.Should().BeTrue();
            }
            finally
            {
                Environment.SetEnvironmentVariable(ODataMcpAuthConstants.IdTokenEnvironmentVariable, previous);
            }
        }

        /// <summary>
        /// <see cref="OutboundOAuthOptions.FromEnvironment(OutboundOAuthOptions)"/> leaves
        /// <see cref="OutboundOAuthOptions.HasEnvironmentIdToken"/> <see langword="false"/> when
        /// <c>ODATA_MCP_ID_TOKEN</c> is not set.
        /// </summary>
        [TestMethod]
        public void FromEnvironment_IdTokenVariableUnset_LeavesHasEnvironmentIdTokenFalse()
        {
            var previous = Environment.GetEnvironmentVariable(ODataMcpAuthConstants.IdTokenEnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(ODataMcpAuthConstants.IdTokenEnvironmentVariable, null);
                var options = new OutboundOAuthOptions();

                OutboundOAuthOptions.FromEnvironment(options);

                options.HasEnvironmentIdToken.Should().BeFalse();
            }
            finally
            {
                Environment.SetEnvironmentVariable(ODataMcpAuthConstants.IdTokenEnvironmentVariable, previous);
            }
        }

        /// <summary>
        /// <see cref="OutboundOAuthOptions.FromEnvironment(OutboundOAuthOptions)"/> returns the same instance it
        /// was given.
        /// </summary>
        [TestMethod]
        public void FromEnvironment_ReturnsSameInstance()
        {
            var options = new OutboundOAuthOptions();

            var result = OutboundOAuthOptions.FromEnvironment(options);

            result.Should().BeSameAs(options);
        }

        /// <summary>
        /// <see cref="OutboundOAuthOptions.HasExplicitCredentials"/> is <see langword="true"/> when
        /// <see cref="OutboundOAuthOptions.ApiKey"/> is set.
        /// </summary>
        [TestMethod]
        public void HasExplicitCredentials_ApiKeySet_IsTrue()
        {
            new OutboundOAuthOptions { ApiKey = "key" }.HasExplicitCredentials.Should().BeTrue();
        }

        /// <summary>
        /// <see cref="OutboundOAuthOptions.HasExplicitCredentials"/> is <see langword="true"/> when
        /// <see cref="OutboundOAuthOptions.AuthToken"/> is set.
        /// </summary>
        [TestMethod]
        public void HasExplicitCredentials_AuthTokenSet_IsTrue()
        {
            new OutboundOAuthOptions { AuthToken = "token" }.HasExplicitCredentials.Should().BeTrue();
        }

        /// <summary>
        /// <see cref="OutboundOAuthOptions.HasExplicitCredentials"/> is <see langword="true"/> when
        /// <see cref="OutboundOAuthOptions.BasicUser"/> is set.
        /// </summary>
        [TestMethod]
        public void HasExplicitCredentials_BasicUserSet_IsTrue()
        {
            new OutboundOAuthOptions { BasicUser = "user" }.HasExplicitCredentials.Should().BeTrue();
        }

        /// <summary>
        /// <see cref="OutboundOAuthOptions.HasExplicitCredentials"/> is <see langword="false"/> on a default
        /// instance.
        /// </summary>
        [TestMethod]
        public void HasExplicitCredentials_Default_IsFalse()
        {
            new OutboundOAuthOptions().HasExplicitCredentials.Should().BeFalse();
        }

        /// <summary>
        /// <see cref="OutboundOAuthOptions.ApiKey"/> without <see cref="OutboundOAuthOptions.ApiKeyHeader"/>
        /// throws, naming <c>--api-key-header</c>.
        /// </summary>
        [TestMethod]
        public void Validate_ApiKeyWithoutHeader_ThrowsArgumentException()
        {
            var options = new OutboundOAuthOptions { ApiKey = "key" };

            var act = options.Validate;

            act.Should().Throw<ArgumentException>().WithMessage("*--api-key-header*");
        }

        /// <summary>
        /// A negative <see cref="OutboundOAuthOptions.AuthTimeout"/> throws, naming <c>--auth-timeout</c>.
        /// </summary>
        [TestMethod]
        public void Validate_AuthTimeoutIsNegative_ThrowsArgumentException()
        {
            var options = new OutboundOAuthOptions { AuthTimeout = TimeSpan.FromSeconds(-1) };

            var act = options.Validate;

            act.Should().Throw<ArgumentException>().WithMessage("*--auth-timeout*");
        }

        /// <summary>
        /// <see cref="OutboundOAuthOptions.AuthTimeout"/> of zero throws, naming <c>--auth-timeout</c>.
        /// </summary>
        [TestMethod]
        public void Validate_AuthTimeoutIsZero_ThrowsArgumentException()
        {
            var options = new OutboundOAuthOptions { AuthTimeout = TimeSpan.Zero };

            var act = options.Validate;

            act.Should().Throw<ArgumentException>().WithMessage("*--auth-timeout*");
        }

        /// <summary>
        /// <see cref="OutboundOAuthOptions.BasicUser"/> without <see cref="OutboundOAuthOptions.BasicPassword"/>
        /// throws, naming <c>--basic-password</c>.
        /// </summary>
        [TestMethod]
        public void Validate_BasicUserWithoutPassword_ThrowsArgumentException()
        {
            var options = new OutboundOAuthOptions { BasicUser = "user" };

            var act = options.Validate;

            act.Should().Throw<ArgumentException>().WithMessage("*--basic-password*");
        }

        /// <summary>
        /// A default <see cref="OutboundOAuthOptions"/> instance validates without throwing.
        /// </summary>
        [TestMethod]
        public void Validate_DefaultInstance_DoesNotThrow()
        {
            var act = () => new OutboundOAuthOptions().Validate();

            act.Should().NotThrow();
        }

        /// <summary>
        /// An undefined <see cref="OutboundOAuthOptions.Grant"/> value throws, naming <c>--grant</c>.
        /// </summary>
        [TestMethod]
        public void Validate_GrantIsUndefinedValue_ThrowsArgumentException()
        {
            var options = new OutboundOAuthOptions { Grant = (OutboundGrantKind)999 };

            var act = options.Validate;

            act.Should().Throw<ArgumentException>().WithMessage("*--grant*");
        }

        /// <summary>
        /// Identity assertion satisfied by <see cref="OutboundOAuthOptions.IdpTokenEndpoint"/> and
        /// <see cref="OutboundOAuthOptions.HasEnvironmentIdToken"/> validates without throwing.
        /// </summary>
        [TestMethod]
        public void Validate_IdentityAssertionSatisfiedByIdpTokenEndpointAndEnvironmentIdToken_DoesNotThrow()
        {
            var options = new OutboundOAuthOptions
            {
                Grant = OutboundGrantKind.IdentityAssertion,
                IdpTokenEndpoint = new Uri("https://idp.example.com/token"),
                HasEnvironmentIdToken = true,
                IdpClientId = "idp-client",
                ClientId = "client"
            };

            var act = options.Validate;

            act.Should().NotThrow();
        }

        /// <summary>
        /// Identity assertion satisfied by <see cref="OutboundOAuthOptions.IdpUrl"/>,
        /// <see cref="OutboundOAuthOptions.IdpIdTokenFile"/>, <see cref="OutboundOAuthOptions.IdpClientId"/> and
        /// <see cref="OutboundOAuthOptions.ClientId"/> validates without throwing.
        /// </summary>
        [TestMethod]
        public void Validate_IdentityAssertionSatisfiedByIdpUrlAndIdTokenFile_DoesNotThrow()
        {
            var options = new OutboundOAuthOptions
            {
                Grant = OutboundGrantKind.IdentityAssertion,
                IdpUrl = new Uri("https://idp.example.com"),
                IdpIdTokenFile = "/path/to/id-token",
                IdpClientId = "idp-client",
                ClientId = "client"
            };

            var act = options.Validate;

            act.Should().NotThrow();
        }

        /// <summary>
        /// Identity assertion without <see cref="OutboundOAuthOptions.ClientId"/> throws, naming
        /// <c>--idp-client-id</c>.
        /// </summary>
        [TestMethod]
        public void Validate_IdentityAssertionWithoutClientId_ThrowsArgumentException()
        {
            var options = new OutboundOAuthOptions
            {
                Grant = OutboundGrantKind.IdentityAssertion,
                IdpUrl = new Uri("https://idp.example.com"),
                IdpIdTokenFile = "/path/to/id-token",
                IdpClientId = "idp-client"
            };

            var act = options.Validate;

            act.Should().Throw<ArgumentException>().WithMessage("*--idp-client-id*");
        }

        /// <summary>
        /// Identity assertion without an id-token source throws, naming <c>--idp-id-token-file</c>.
        /// </summary>
        [TestMethod]
        public void Validate_IdentityAssertionWithoutIdTokenSource_ThrowsArgumentException()
        {
            var options = new OutboundOAuthOptions
            {
                Grant = OutboundGrantKind.IdentityAssertion,
                IdpUrl = new Uri("https://idp.example.com"),
                IdpClientId = "idp-client",
                ClientId = "client"
            };

            var act = options.Validate;

            act.Should().Throw<ArgumentException>().WithMessage("*--idp-id-token-file*");
        }

        /// <summary>
        /// Identity assertion without <see cref="OutboundOAuthOptions.IdpClientId"/> throws, naming
        /// <c>--idp-client-id</c>.
        /// </summary>
        [TestMethod]
        public void Validate_IdentityAssertionWithoutIdpClientId_ThrowsArgumentException()
        {
            var options = new OutboundOAuthOptions
            {
                Grant = OutboundGrantKind.IdentityAssertion,
                IdpUrl = new Uri("https://idp.example.com"),
                IdpIdTokenFile = "/path/to/id-token",
                ClientId = "client"
            };

            var act = options.Validate;

            act.Should().Throw<ArgumentException>().WithMessage("*--idp-client-id*");
        }

        /// <summary>
        /// Identity assertion without <see cref="OutboundOAuthOptions.IdpUrl"/> or
        /// <see cref="OutboundOAuthOptions.IdpTokenEndpoint"/> throws, naming <c>--idp-url</c>.
        /// </summary>
        [TestMethod]
        public void Validate_IdentityAssertionWithoutIdpEndpoint_ThrowsArgumentException()
        {
            var options = new OutboundOAuthOptions
            {
                Grant = OutboundGrantKind.IdentityAssertion,
                IdpIdTokenFile = "/path/to/id-token",
                IdpClientId = "idp-client",
                ClientId = "client"
            };

            var act = options.Validate;

            act.Should().Throw<ArgumentException>().WithMessage("*--idp-url*");
        }

        #endregion

    }

}
