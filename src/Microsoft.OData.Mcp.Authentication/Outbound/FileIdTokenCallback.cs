// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// Supplies the enterprise OpenID Connect id token the identity assertion grant exchanges, reading it from
    /// the file <c>--idp-id-token-file</c> names or from the
    /// <see cref="ODataMcpAuthConstants.IdTokenEnvironmentVariable"/> environment variable.
    /// </summary>
    /// <example>
    /// <code>
    /// var callback = new FileIdTokenCallback(options.IdpIdTokenFile);
    ///
    /// var providerOptions = new IdentityAssertionGrantProviderOptions
    /// {
    ///     ClientId = options.ClientId,
    ///     IdpClientId = options.IdpClientId,
    ///     IdTokenCallback = callback.ReadAsync
    /// };
    /// </code>
    /// </example>
    /// <remarks>
    /// Per <c>specs/v3/AUTHENTICATION.md</c> "Grants → Identity Assertion Grant" the id token is never passed on
    /// argv: a process list is world-readable on every platform this tool ships to, and an id token is a bearer
    /// credential for the end user's enterprise identity. The file is re-read on every call rather than cached,
    /// so a daemon whose sidecar rotates the token picks the new one up without a restart, and per
    /// <c>AUTH-12</c> nothing here ever logs, elicits, or echoes the token itself — only the path it came from.
    /// </remarks>
    public sealed class FileIdTokenCallback
    {

        #region Properties

        /// <summary>
        /// Gets the path of the file the id token is read from, or <see langword="null"/> when only the
        /// environment variable is consulted.
        /// </summary>
        public string? Path { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="FileIdTokenCallback"/> class.
        /// </summary>
        /// <param name="path">The path of the file holding a UTF-8 id token, or <see langword="null"/> to read <see cref="ODataMcpAuthConstants.IdTokenEnvironmentVariable"/> instead.</param>
        /// <remarks>
        /// A <see langword="null"/> path is not an error: the environment variable is the other documented
        /// source, and which of the two an operator configured is only knowable when the token is actually
        /// needed.
        /// </remarks>
        public FileIdTokenCallback(string? path)
        {
            Path = path;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Reads the id token, preferring the configured file over the environment variable.
        /// </summary>
        /// <param name="context">The resource and authorization server URLs the SDK discovered, unused by this source.</param>
        /// <param name="cancellationToken">The token that cancels the file read.</param>
        /// <returns>
        /// The trimmed id token.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the configured file does not exist, when it is empty, or when neither a file nor
        /// <see cref="ODataMcpAuthConstants.IdTokenEnvironmentVariable"/> supplies a token.
        /// </exception>
        /// <example>
        /// <code>
        /// SdkAuth.IdentityAssertionGrantIdTokenCallback callback = new FileIdTokenCallback(path).ReadAsync;
        /// </code>
        /// </example>
        /// <remarks>
        /// The token is trimmed because both sources routinely pick up a trailing newline — a shell
        /// redirection, a Kubernetes projected volume — and a JWT with a newline on the end is rejected by
        /// every token endpoint with an error that says nothing about whitespace. Failures name the source,
        /// never the value.
        /// </remarks>
        public async Task<string> ReadAsync(SdkAuth.IdentityAssertionGrantContext context, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(Path))
            {
                if (!File.Exists(Path))
                {
                    throw new InvalidOperationException($"The id token file '{Path}' does not exist; fix --idp-id-token-file or set {ODataMcpAuthConstants.IdTokenEnvironmentVariable}.");
                }

                var fromFile = (await File.ReadAllTextAsync(Path, Encoding.UTF8, cancellationToken).ConfigureAwait(false)).Trim();
                if (string.IsNullOrWhiteSpace(fromFile))
                {
                    throw new InvalidOperationException($"The id token file '{Path}' is empty.");
                }

                return fromFile;
            }

            var fromEnvironment = Environment.GetEnvironmentVariable(ODataMcpAuthConstants.IdTokenEnvironmentVariable)?.Trim();
            if (string.IsNullOrWhiteSpace(fromEnvironment))
            {
                throw new InvalidOperationException($"No id token is available; pass --idp-id-token-file or set {ODataMcpAuthConstants.IdTokenEnvironmentVariable}.");
            }

            return fromEnvironment;
        }

        #endregion

    }

}
