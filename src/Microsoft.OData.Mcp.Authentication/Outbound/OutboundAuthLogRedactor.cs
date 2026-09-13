// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// Masks token and credential values out of text before it reaches a log sink, per
    /// <c>specs/v3/AUTHENTICATION.md</c> "Observability -&gt; Redaction" and "Security &amp; Privacy -&gt; Logging
    /// tokens".
    /// </summary>
    /// <remarks>
    /// Every regular expression is source-generated (<see cref="GeneratedRegexAttribute"/>), so redaction costs
    /// no reflection, matching this library's AOT-first posture. <see cref="Redact(string)"/> never throws on
    /// text that carries no sensitive content, because log text is redacted on every write and must never
    /// itself become a source of failure.
    /// </remarks>
    public static partial class OutboundAuthLogRedactor
    {

        #region Fields

        /// <summary>
        /// The six sensitive names <see cref="JsonPropertyRegex"/> and <see cref="FormEncodedRegex"/> mask,
        /// piped for regex alternation. Built from <see cref="ODataMcpAuthConstants"/> where a constant already
        /// exists; <c>client_secret</c>, <c>code_verifier</c>, and <c>id_token</c> have none there.
        /// </summary>
        private const string SensitiveNames = ODataMcpAuthConstants.AccessTokenProperty + "|" + ODataMcpAuthConstants.RefreshTokenProperty
            + "|client_secret|" + ODataMcpAuthConstants.DeviceCodeParameter + "|code_verifier|id_token";

        #endregion

        #region Public Methods

        /// <summary>
        /// Describes a token container for logging without ever emitting
        /// <see cref="SdkAuth.TokenContainer.AccessToken"/> or <see cref="SdkAuth.TokenContainer.RefreshToken"/>.
        /// </summary>
        /// <param name="token">The token container to describe.</param>
        /// <returns>
        /// <c>type={TokenType} expiresIn={ExpiresIn} hasRefresh={true|false} scope={Scope}</c>, reporting
        /// <c>expiresIn</c> and <c>scope</c> as <c>none</c> when absent.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="token"/> is <see langword="null"/>.</exception>
        /// <example>
        /// <code>
        /// var description = OutboundAuthLogRedactor.DescribeToken(token);
        /// logger.LogInformation("Token acquired: {Description}", description);
        /// // Token acquired: type=Bearer expiresIn=3600 hasRefresh=true scope=read
        /// </code>
        /// </example>
        public static string DescribeToken(SdkAuth.TokenContainer token)
        {
            ArgumentNullException.ThrowIfNull(token);

            var expiresIn = token.ExpiresIn?.ToString(CultureInfo.InvariantCulture) ?? "none";
            var hasRefresh = string.IsNullOrWhiteSpace(token.RefreshToken) ? "false" : "true";
            var scope = token.Scope ?? "none";

            return $"type={token.TokenType} expiresIn={expiresIn} hasRefresh={hasRefresh} scope={scope}";
        }

        /// <summary>
        /// Masks sensitive token and credential values in free-form log text.
        /// </summary>
        /// <param name="text">The text to redact.</param>
        /// <returns>
        /// <paramref name="text"/> with every sensitive JSON property value, <c>Authorization</c> header value,
        /// and sensitive form-encoded value replaced by <c>***</c>. Text with no sensitive content, including an
        /// empty string, is returned unchanged.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
        /// <example>
        /// <code>
        /// OutboundAuthLogRedactor.Redact("{\"access_token\":\"abc\"}");
        /// // {"access_token":"***"}
        ///
        /// OutboundAuthLogRedactor.Redact("Authorization: Bearer eyJabc");
        /// // Authorization: Bearer ***
        ///
        /// OutboundAuthLogRedactor.Redact("grant_type=refresh_token&amp;refresh_token=abc");
        /// // grant_type=refresh_token&amp;refresh_token=***
        /// </code>
        /// </example>
        /// <remarks>
        /// Three independent passes run in order, so a single line of text can match more than one: JSON string
        /// properties named <c>access_token</c>, <c>refresh_token</c>, <c>client_secret</c>, <c>device_code</c>,
        /// <c>code_verifier</c>, or <c>id_token</c> (case-insensitive, quoted, arbitrary whitespace around
        /// <c>:</c>); an <c>Authorization</c> header written as <c>Authorization: &lt;scheme&gt; &lt;value&gt;</c>
        /// or <c>Authorization=&lt;scheme&gt; &lt;value&gt;</c> (header name case-insensitive); and
        /// form-encoded <c>name=value</c> pairs for the same six names.
        /// </remarks>
        public static string Redact(string text)
        {
            ArgumentNullException.ThrowIfNull(text);

            var redacted = JsonPropertyRegex().Replace(text, "$1***$2");
            redacted = AuthorizationHeaderRegex().Replace(redacted, "***");
            redacted = FormEncodedRegex().Replace(redacted, "$1=***");

            return redacted;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Matches only the value of an <c>Authorization</c> header written as
        /// <c>Authorization: &lt;scheme&gt; &lt;value&gt;</c> or <c>Authorization=&lt;scheme&gt; &lt;value&gt;</c>.
        /// The header name, separator, and scheme sit in a lookbehind, so the match carries no capturing groups
        /// and the header text ahead of the value survives a replace untouched.
        /// </summary>
        /// <returns>The compiled, source-generated regular expression.</returns>
        [GeneratedRegex("(?<=" + ODataMcpAuthConstants.AuthorizationHeader + @"\s*[:=]\s*[A-Za-z][A-Za-z0-9]*\s+)\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 1000)]
        internal static partial Regex AuthorizationHeaderRegex();

        /// <summary>
        /// Matches a form-encoded <c>name=value</c> pair whose name is <c>client_secret</c>,
        /// <c>refresh_token</c>, <c>device_code</c>, <c>code_verifier</c>, <c>id_token</c>, or
        /// <c>access_token</c>, capturing the name in group 1. The value runs up to the next <c>&amp;</c> or
        /// whitespace.
        /// </summary>
        /// <returns>The compiled, source-generated regular expression.</returns>
        [GeneratedRegex(@"(?<=^|&)(" + SensitiveNames + @")=[^&\s]*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 1000)]
        internal static partial Regex FormEncodedRegex();

        /// <summary>
        /// Matches the quoted string value of a JSON property named <c>access_token</c>, <c>refresh_token</c>,
        /// <c>client_secret</c>, <c>device_code</c>, <c>code_verifier</c>, or <c>id_token</c>, capturing
        /// everything up to and including the opening quote of the value in group 1 and the closing quote in
        /// group 2.
        /// </summary>
        /// <returns>The compiled, source-generated regular expression.</returns>
        /// <remarks>
        /// The value alternates <c>\.</c> (an escape sequence: backslash plus any one character) with
        /// <c>[^"\\]</c> (any character that is neither an unescaped quote nor a backslash), so a JSON-escaped
        /// quote (<c>\"</c>) or backslash (<c>\\</c>) inside the value is consumed as part of it instead of
        /// being mistaken for the closing quote, which would otherwise leave the remainder of the value
        /// unmasked.
        /// </remarks>
        [GeneratedRegex("(\"(?:" + SensitiveNames + @")""\s*:\s*"")(?:\\.|[^""\\])*("")", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 1000)]
        internal static partial Regex JsonPropertyRegex();

        #endregion

    }

}
