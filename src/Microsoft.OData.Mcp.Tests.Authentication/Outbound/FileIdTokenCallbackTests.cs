// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Tests.Authentication.Outbound
{

    /// <summary>
    /// Exercises <see cref="FileIdTokenCallback"/> against real files and the real environment block, proving
    /// which source wins and what an operator is told when neither is configured.
    /// </summary>
    /// <remarks>
    /// Every test that touches <see cref="ODataMcpAuthConstants.IdTokenEnvironmentVariable"/> restores the
    /// previous value in a <c>finally</c>, and every temporary file is deleted the same way, so these tests
    /// leave the process exactly as they found it.
    /// </remarks>
    [TestClass]
    public class FileIdTokenCallbackTests
    {

        #region Public Methods

        /// <summary>
        /// With no file configured the environment variable is read, and trimmed the same way.
        /// </summary>
        [TestMethod]
        public async Task ReadAsync_EnvironmentSource_ReturnsTrimmedToken()
        {
            var previous = Environment.GetEnvironmentVariable(ODataMcpAuthConstants.IdTokenEnvironmentVariable);

            Environment.SetEnvironmentVariable(ODataMcpAuthConstants.IdTokenEnvironmentVariable, "\nheader.payload.signature\n");

            try
            {
                var token = await new FileIdTokenCallback(null).ReadAsync(Context(), CancellationToken.None);

                token.Should().Be("header.payload.signature");
            }
            finally
            {
                Environment.SetEnvironmentVariable(ODataMcpAuthConstants.IdTokenEnvironmentVariable, previous);
            }
        }

        /// <summary>
        /// A configured file is preferred over the environment variable, so a daemon whose sidecar rotates a
        /// file is never quietly overridden by a stale export.
        /// </summary>
        [TestMethod]
        public async Task ReadAsync_FileAndEnvironmentBothSet_PrefersFile()
        {
            var path = WriteToken("from-the-file");
            var previous = Environment.GetEnvironmentVariable(ODataMcpAuthConstants.IdTokenEnvironmentVariable);

            Environment.SetEnvironmentVariable(ODataMcpAuthConstants.IdTokenEnvironmentVariable, "from-the-environment");

            try
            {
                var token = await new FileIdTokenCallback(path).ReadAsync(Context(), CancellationToken.None);

                token.Should().Be("from-the-file");
            }
            finally
            {
                Environment.SetEnvironmentVariable(ODataMcpAuthConstants.IdTokenEnvironmentVariable, previous);
                File.Delete(path);
            }
        }

        /// <summary>
        /// A configured file that does not exist names the flag and the environment variable, so an operator
        /// knows both ways out.
        /// </summary>
        [TestMethod]
        public async Task ReadAsync_FileMissing_ThrowsNamingBothSources()
        {
            var callback = new FileIdTokenCallback(Path.Combine(Path.GetTempPath(), $"odata-mcp-absent-{Guid.NewGuid():N}.jwt"));

            Func<Task> act = () => callback.ReadAsync(Context(), CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage($"*--idp-id-token-file*{ODataMcpAuthConstants.IdTokenEnvironmentVariable}*");
        }

        /// <summary>
        /// The file source reads the token and trims the trailing newline a shell redirection or a projected
        /// volume leaves behind.
        /// </summary>
        [TestMethod]
        public async Task ReadAsync_FileSource_ReturnsTrimmedToken()
        {
            var path = WriteToken("  header.payload.signature\r\n");

            try
            {
                var token = await new FileIdTokenCallback(path).ReadAsync(Context(), CancellationToken.None);

                token.Should().Be("header.payload.signature");
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// With neither source configured the failure names both, rather than letting an empty assertion reach
        /// the identity provider.
        /// </summary>
        [TestMethod]
        public async Task ReadAsync_NoSourceConfigured_Throws()
        {
            var previous = Environment.GetEnvironmentVariable(ODataMcpAuthConstants.IdTokenEnvironmentVariable);

            Environment.SetEnvironmentVariable(ODataMcpAuthConstants.IdTokenEnvironmentVariable, null);

            try
            {
                Func<Task> act = () => new FileIdTokenCallback(null).ReadAsync(Context(), CancellationToken.None);

                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage($"*--idp-id-token-file*{ODataMcpAuthConstants.IdTokenEnvironmentVariable}*");
            }
            finally
            {
                Environment.SetEnvironmentVariable(ODataMcpAuthConstants.IdTokenEnvironmentVariable, previous);
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds the SDK context the callback ignores.
        /// </summary>
        /// <returns>
        /// A context naming a resource and an authorization server.
        /// </returns>
        internal static SdkAuth.IdentityAssertionGrantContext Context()
        {
            return new SdkAuth.IdentityAssertionGrantContext
            {
                AuthorizationServerUrl = new Uri("https://login.example/oauth/v2.0"),
                ResourceUrl = new Uri("https://api.example/odata/")
            };
        }

        /// <summary>
        /// Writes a token to a throwaway file.
        /// </summary>
        /// <param name="contents">The exact file contents, including any surrounding whitespace under test.</param>
        /// <returns>
        /// The path the caller must delete.
        /// </returns>
        internal static string WriteToken(string contents)
        {
            var path = Path.Combine(Path.GetTempPath(), $"odata-mcp-id-token-{Guid.NewGuid():N}.jwt");

            File.WriteAllText(path, contents);

            return path;
        }

        #endregion

    }

}
