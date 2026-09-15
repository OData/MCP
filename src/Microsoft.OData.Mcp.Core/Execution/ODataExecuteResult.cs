// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;

namespace Microsoft.OData.Mcp.Core.Execution
{

    /// <summary>
    /// The HTTP result of an OData call.
    /// </summary>
    public sealed class ODataExecuteResult
    {

        #region Properties

        /// <summary>
        /// Gets or sets the response body.
        /// </summary>
        public string Body { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the response body stream when the executor kept the OData output as written.
        /// </summary>
        /// <remarks>
        /// Prefer this over <see cref="Body"/> for in-process success. Do not dispose it; the executor owns it.
        /// </remarks>
        public Stream? BodyStream { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the status code is success.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Gets or sets the response media type.
        /// </summary>
        public string? MediaType { get; set; }

        /// <summary>
        /// Gets or sets the <c>Retry-After</c> header, when the service sent one.
        /// </summary>
        public string? RetryAfter { get; set; }

        /// <summary>
        /// Gets or sets the HTTP status code.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Gets or sets raw <c>WWW-Authenticate</c> header values, when the service sent any.
        /// </summary>
        /// <remarks>
        /// Values are unparsed challenge strings. OAuth interpretation lives outside Core.
        /// </remarks>
        public IReadOnlyList<string> WwwAuthenticate { get; set; } = [];

        #endregion

        #region Public Methods

        /// <summary>
        /// Copies status, body, media type, and <c>Retry-After</c> from an HTTP response.
        /// </summary>
        /// <param name="response">The HTTP response.</param>
        /// <param name="body">The already-read body.</param>
        /// <returns>
        /// The execute result.
        /// </returns>
        public static ODataExecuteResult FromHttp(HttpResponseMessage response, string body)
        {
            ArgumentNullException.ThrowIfNull(response);

            return new ODataExecuteResult
            {
                Body = body ?? string.Empty,
                IsSuccess = response.IsSuccessStatusCode,
                MediaType = response.Content.Headers.ContentType?.MediaType,
                RetryAfter = ReadRetryAfter(response),
                StatusCode = (int)response.StatusCode,
                WwwAuthenticate = ReadWwwAuthenticate(response)
            };
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Reads <c>Retry-After</c> from the response, if present.
        /// </summary>
        /// <param name="response">The HTTP response.</param>
        /// <returns>
        /// The header value, or <c>null</c>.
        /// </returns>
        internal static string? ReadRetryAfter(HttpResponseMessage response)
        {
            ArgumentNullException.ThrowIfNull(response);

            if (response.Headers.RetryAfter is not null)
            {
                return response.Headers.RetryAfter.ToString();
            }

            if (response.Headers.TryGetValues("Retry-After", out var values))
            {
                return values.FirstOrDefault();
            }

            return null;
        }

        /// <summary>
        /// Reads raw, deduplicated <c>WWW-Authenticate</c> header values from the response.
        /// </summary>
        /// <param name="response">The HTTP response.</param>
        /// <returns>
        /// The distinct header values, in the order received, or an empty list when the header is absent.
        /// </returns>
        internal static IReadOnlyList<string> ReadWwwAuthenticate(HttpResponseMessage response)
        {
            ArgumentNullException.ThrowIfNull(response);

            if (response.Headers.TryGetValues("WWW-Authenticate", out var values))
            {
                return values.Distinct(StringComparer.Ordinal).ToList();
            }

            return [];
        }

        #endregion

    }

}

