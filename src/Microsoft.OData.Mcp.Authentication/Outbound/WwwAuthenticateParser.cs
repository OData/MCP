// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// Parses <c>WWW-Authenticate</c> response header values into <see cref="OAuthChallenge"/> instances using
    /// the RFC 9110 section 11.6.1 challenge grammar and the RFC 6750 / RFC 9728 OAuth auth-params.
    /// </summary>
    /// <remarks>
    /// The grammar implemented is <c>challenge = auth-scheme [ 1*SP ( token68 / #auth-param ) ]</c> with
    /// <c>auth-param = token BWS "=" BWS ( token / quoted-string )</c>. A hand written tokenizer is used rather
    /// than a regular expression so that a comma inside a quoted-string never splits a challenge, and so that
    /// the scheme of a following challenge (a token that is not followed by <c>=</c>) is recognized correctly.
    /// A <c>token68</c> credential is consumed so that it cannot masquerade as an extra challenge, but it is
    /// not surfaced: OAuth challenges always use auth-params.
    /// </remarks>
    public static class WwwAuthenticateParser
    {

        #region Public Methods

        /// <summary>
        /// Parses a single <c>WWW-Authenticate</c> header value and returns its first challenge.
        /// </summary>
        /// <param name="headerValue">The raw header value, for example <c>Bearer realm="", scope="read"</c>.</param>
        /// <returns>The first <see cref="OAuthChallenge"/> in <paramref name="headerValue"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="headerValue"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="headerValue"/> is empty or white space.</exception>
        /// <exception cref="FormatException"><paramref name="headerValue"/> does not contain a well formed challenge.</exception>
        /// <example>
        /// <code>
        /// var challenge = WwwAuthenticateParser.Parse("Bearer realm=\"\", client_id=\"00000003-0000-0000-c000-000000000000\"");
        ///
        /// Console.WriteLine(challenge.Scheme);           // Bearer
        /// Console.WriteLine(challenge.Realm);            // (empty string)
        /// Console.WriteLine(challenge.ResourceClientId); // 00000003-0000-0000-c000-000000000000
        /// </code>
        /// </example>
        /// <remarks>
        /// Use <see cref="ParseAll(string)"/> when a header value may advertise more than one scheme; this
        /// method deliberately keeps only the first challenge.
        /// </remarks>
        public static OAuthChallenge Parse(string headerValue)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(headerValue);

            var challenges = ReadChallenges(headerValue);
            if (challenges.Count == 0)
            {
                throw new FormatException("The WWW-Authenticate value did not contain an authentication challenge.");
            }

            return challenges[0];
        }

        /// <summary>
        /// Parses a sequence of <c>WWW-Authenticate</c> header values, in order, into a flat challenge list.
        /// </summary>
        /// <param name="values">The header values, typically the ones surfaced on an OData execute result.</param>
        /// <returns>Every challenge advertised across <paramref name="values"/>, in the order encountered.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="values"/> is <see langword="null"/>.</exception>
        /// <exception cref="FormatException">One of the values is not a well formed challenge.</exception>
        /// <remarks>
        /// A response may repeat the header instead of comma joining it, and each value may itself carry more
        /// than one challenge. Entries that are <see langword="null"/>, empty, or white space carry no
        /// challenge and are skipped, so a response with no usable challenge yields an empty list rather than
        /// an exception; callers apply the "401 with no WWW-Authenticate" rule themselves.
        /// </remarks>
        public static IReadOnlyList<OAuthChallenge> ParseAll(IEnumerable<string> values)
        {
            ArgumentNullException.ThrowIfNull(values);

            var challenges = new List<OAuthChallenge>();
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                challenges.AddRange(ReadChallenges(value));
            }

            return challenges;
        }

        /// <summary>
        /// Parses a single <c>WWW-Authenticate</c> header value that may advertise several challenges.
        /// </summary>
        /// <param name="headerValue">The raw header value, for example <c>Bearer realm="a", Basic realm="b"</c>.</param>
        /// <returns>Every challenge in <paramref name="headerValue"/>, in the order encountered.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="headerValue"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="headerValue"/> is empty or white space.</exception>
        /// <exception cref="FormatException"><paramref name="headerValue"/> is not a well formed challenge list.</exception>
        public static IReadOnlyList<OAuthChallenge> ParseAll(string headerValue)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(headerValue);

            return ReadChallenges(headerValue);
        }

        /// <summary>
        /// Selects the first <c>Bearer</c> challenge from a parsed challenge list.
        /// </summary>
        /// <param name="challenges">The challenges to search, in the order the server advertised them.</param>
        /// <returns>The first challenge whose scheme is <c>Bearer</c>, or <see langword="null"/> when none is.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="challenges"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// Authentication schemes are case-insensitive, so <c>bearer</c> and <c>Bearer</c> both match.
        /// </remarks>
        public static OAuthChallenge? SelectBearer(IReadOnlyList<OAuthChallenge> challenges)
        {
            ArgumentNullException.ThrowIfNull(challenges);

            return challenges.FirstOrDefault(challenge => ODataMcpAuthConstants.BearerScheme.Equals(challenge.Scheme, StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Projects a scheme and its auth-params onto an <see cref="OAuthChallenge"/>, promoting the OAuth
        /// parameters this client understands to typed properties.
        /// </summary>
        /// <param name="scheme">The authentication scheme exactly as the server spelled it.</param>
        /// <param name="parameters">The auth-params of the challenge, keyed case-insensitively.</param>
        /// <returns>The immutable challenge.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="scheme"/> is <see langword="null"/>, empty, or white space.</exception>
        /// <remarks>
        /// The challenge <c>client_id</c> lands on <see cref="OAuthChallenge.ResourceClientId"/> and nowhere
        /// else, because it identifies the resource application rather than this client.
        /// </remarks>
        internal static OAuthChallenge CreateChallenge(string scheme, Dictionary<string, string> parameters)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
            ArgumentNullException.ThrowIfNull(parameters);

            return new OAuthChallenge
            {
                AuthorizationUri = ToAbsoluteUri(GetParameter(parameters, ODataMcpAuthConstants.AuthorizationUriParameter)),
                Error = GetParameter(parameters, ODataMcpAuthConstants.ErrorParameter),
                ErrorDescription = GetParameter(parameters, ODataMcpAuthConstants.ErrorDescriptionParameter),
                Parameters = new ReadOnlyDictionary<string, string>(parameters),
                Realm = GetParameter(parameters, ODataMcpAuthConstants.RealmParameter),
                ResourceClientId = GetParameter(parameters, ODataMcpAuthConstants.ClientIdParameter),
                ResourceMetadata = ToAbsoluteUri(GetParameter(parameters, ODataMcpAuthConstants.ResourceMetadataParameter)),
                Scheme = scheme,
                Scope = GetParameter(parameters, ODataMcpAuthConstants.ScopeParameter)
            };
        }

        /// <summary>
        /// Reads a parameter that may be absent.
        /// </summary>
        /// <param name="parameters">The auth-params of a challenge.</param>
        /// <param name="name">The parameter name to look up.</param>
        /// <returns>The parameter value, or <see langword="null"/> when the parameter was not advertised.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="name"/> is <see langword="null"/>, empty, or white space.</exception>
        internal static string? GetParameter(IReadOnlyDictionary<string, string> parameters, string name)
        {
            ArgumentNullException.ThrowIfNull(parameters);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            return parameters.TryGetValue(name, out var value) ? value : null;
        }

        /// <summary>
        /// Determines whether a character is a <c>token68</c> character, excluding the trailing <c>=</c> padding.
        /// </summary>
        /// <param name="character">The character to test.</param>
        /// <returns><see langword="true"/> when the character may appear in a <c>token68</c>.</returns>
        internal static bool IsToken68Character(char character)
        {
            return character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-' or '.' or '_' or '~' or '+' or '/';
        }

        /// <summary>
        /// Determines whether a character is an RFC 9110 <c>tchar</c>.
        /// </summary>
        /// <param name="character">The character to test.</param>
        /// <returns><see langword="true"/> when the character may appear in a token.</returns>
        internal static bool IsTokenCharacter(char character)
        {
            return character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9')
                or '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~';
        }

        /// <summary>
        /// Tokenizes one header value into the challenges it advertises.
        /// </summary>
        /// <param name="headerValue">The raw header value.</param>
        /// <returns>Every challenge in <paramref name="headerValue"/>, in the order encountered.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="headerValue"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="headerValue"/> is empty or white space.</exception>
        /// <exception cref="FormatException"><paramref name="headerValue"/> is not a well formed challenge list.</exception>
        /// <remarks>
        /// A token that is not followed by <c>=</c> ends the current challenge and starts the next one, which is
        /// how RFC 9110 disambiguates <c>Bearer realm="a", Basic realm="b"</c> from a two-parameter challenge.
        /// Only the item directly after a scheme may be a <c>token68</c>; every later item must be an auth-param
        /// or the scheme of the next challenge, and items are comma separated. A repeated auth-param name keeps
        /// the last value, matching how HTTP clients generally treat a duplicated parameter.
        /// </remarks>
        internal static IReadOnlyList<OAuthChallenge> ReadChallenges(string headerValue)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(headerValue);

            var challenges = new List<OAuthChallenge>();
            var index = 0;
            string? scheme = null;

            while (true)
            {
                if (scheme is null)
                {
                    SkipWhitespaceAndCommas(headerValue, ref index);
                    if (index >= headerValue.Length)
                    {
                        break;
                    }

                    scheme = ReadToken(headerValue, ref index);
                    if (scheme.Length == 0)
                    {
                        throw new FormatException($"Expected an authentication scheme at position {index} of the WWW-Authenticate value.");
                    }
                }

                var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var allowCredentials = true;
                var expectSeparator = false;
                string? nextScheme = null;

                while (true)
                {
                    SkipWhitespace(headerValue, ref index);
                    if (index >= headerValue.Length)
                    {
                        break;
                    }

                    if (headerValue[index] == ',')
                    {
                        index++;
                        allowCredentials = false;
                        expectSeparator = false;
                        continue;
                    }

                    if (expectSeparator)
                    {
                        throw new FormatException($"Expected ',' at position {index} of the WWW-Authenticate value.");
                    }

                    if (allowCredentials && TryReadToken68(headerValue, ref index))
                    {
                        allowCredentials = false;
                        expectSeparator = true;
                        continue;
                    }

                    var name = ReadToken(headerValue, ref index);
                    if (name.Length == 0)
                    {
                        throw new FormatException($"Expected an auth-param name at position {index} of the WWW-Authenticate value.");
                    }

                    SkipWhitespace(headerValue, ref index);
                    if (index >= headerValue.Length || headerValue[index] != '=')
                    {
                        if (allowCredentials)
                        {
                            throw new FormatException($"Expected '=' after '{name}' at position {index} of the WWW-Authenticate value.");
                        }

                        nextScheme = name;
                        break;
                    }

                    index++;
                    SkipWhitespace(headerValue, ref index);
                    if (index >= headerValue.Length)
                    {
                        throw new FormatException($"The auth-param '{name}' has no value.");
                    }

                    string value;
                    if (headerValue[index] == '"')
                    {
                        value = ReadQuotedString(headerValue, ref index);
                    }
                    else
                    {
                        value = ReadToken(headerValue, ref index);
                        if (value.Length == 0)
                        {
                            throw new FormatException($"The auth-param '{name}' has no value.");
                        }
                    }

                    parameters[name] = value;
                    allowCredentials = false;
                    expectSeparator = true;
                }

                challenges.Add(CreateChallenge(scheme, parameters));
                scheme = nextScheme;
            }

            return challenges;
        }

        /// <summary>
        /// Reads a quoted-string, resolving <c>\</c> escapes, and leaves the cursor after the closing quote.
        /// </summary>
        /// <param name="value">The header value being tokenized.</param>
        /// <param name="index">The cursor, which must point at the opening quote.</param>
        /// <returns>The unescaped content of the quoted-string, which may be empty.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
        /// <exception cref="FormatException">The cursor is not on a quote, or the quoted-string is never closed.</exception>
        internal static string ReadQuotedString(string value, ref int index)
        {
            ArgumentNullException.ThrowIfNull(value);

            if (index >= value.Length || value[index] != '"')
            {
                throw new FormatException($"Expected a quoted-string at position {index} of the WWW-Authenticate value.");
            }

            index++;
            var builder = new StringBuilder();
            while (index < value.Length)
            {
                var current = value[index];
                if (current == '\\')
                {
                    index++;
                    if (index >= value.Length)
                    {
                        throw new FormatException("The WWW-Authenticate value ends with an unterminated escape sequence.");
                    }

                    builder.Append(value[index]);
                    index++;
                    continue;
                }

                index++;
                if (current == '"')
                {
                    return builder.ToString();
                }

                builder.Append(current);
            }

            throw new FormatException("The WWW-Authenticate value contains an unterminated quoted-string.");
        }

        /// <summary>
        /// Reads an RFC 9110 token and leaves the cursor on the first character that is not a <c>tchar</c>.
        /// </summary>
        /// <param name="value">The header value being tokenized.</param>
        /// <param name="index">The cursor to advance.</param>
        /// <returns>The token, which is empty when the cursor is not on a <c>tchar</c>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
        internal static string ReadToken(string value, ref int index)
        {
            ArgumentNullException.ThrowIfNull(value);

            var start = index;
            while (index < value.Length && IsTokenCharacter(value[index]))
            {
                index++;
            }

            return value.Substring(start, index - start);
        }

        /// <summary>
        /// Advances the cursor past optional white space.
        /// </summary>
        /// <param name="value">The header value being tokenized.</param>
        /// <param name="index">The cursor to advance.</param>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
        internal static void SkipWhitespace(string value, ref int index)
        {
            ArgumentNullException.ThrowIfNull(value);

            while (index < value.Length && char.IsWhiteSpace(value[index]))
            {
                index++;
            }
        }

        /// <summary>
        /// Advances the cursor past optional white space and the empty list elements RFC 9110 tolerates.
        /// </summary>
        /// <param name="value">The header value being tokenized.</param>
        /// <param name="index">The cursor to advance.</param>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
        internal static void SkipWhitespaceAndCommas(string value, ref int index)
        {
            ArgumentNullException.ThrowIfNull(value);

            while (index < value.Length && (char.IsWhiteSpace(value[index]) || value[index] == ','))
            {
                index++;
            }
        }

        /// <summary>
        /// Converts a raw parameter value to an absolute URI.
        /// </summary>
        /// <param name="value">The raw parameter value, which may be absent.</param>
        /// <returns>The absolute URI, or <see langword="null"/> when the value is absent or not absolute.</returns>
        /// <remarks>
        /// A relative or unparseable value is not an error: it stays available through
        /// <see cref="OAuthChallenge.Parameters"/> so a caller can still log or resolve it.
        /// </remarks>
        internal static Uri? ToAbsoluteUri(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
        }

        /// <summary>
        /// Consumes a <c>token68</c> credential when, and only when, the item at the cursor really is one.
        /// </summary>
        /// <param name="value">The header value being tokenized.</param>
        /// <param name="index">The cursor, advanced past the credential only when this method returns <see langword="true"/>.</param>
        /// <returns><see langword="true"/> when a <c>token68</c> was consumed.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// A <c>token68</c> runs to the end of the header value or to the next comma, so an item such as
        /// <c>realm=api</c> is rejected here (the <c>=</c> is followed by more characters) and parsed as an
        /// auth-param instead.
        /// </remarks>
        internal static bool TryReadToken68(string value, ref int index)
        {
            ArgumentNullException.ThrowIfNull(value);

            var scan = index;
            while (scan < value.Length && IsToken68Character(value[scan]))
            {
                scan++;
            }

            if (scan == index)
            {
                return false;
            }

            while (scan < value.Length && value[scan] == '=')
            {
                scan++;
            }

            var probe = scan;
            while (probe < value.Length && char.IsWhiteSpace(value[probe]))
            {
                probe++;
            }

            if (probe < value.Length && value[probe] != ',')
            {
                return false;
            }

            index = scan;

            return true;
        }

        #endregion

    }

}
