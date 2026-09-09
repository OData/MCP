// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using SharpToken;

namespace Microsoft.OData.Mcp.Benchmarks.Infrastructure
{

    /// <summary>
    /// The one tokenizer every report uses, so numbers across reports are comparable.
    /// </summary>
    public static class Tokenizer
    {

        #region Fields

        /// <summary>
        /// The encoding name. <c>cl100k_base</c> is the encoding the optimization spec standardizes on.
        /// </summary>
        public const string EncodingName = "cl100k_base";

        private static readonly GptEncoding _encoding = GptEncoding.GetEncoding(EncodingName);

        #endregion

        #region Public Methods

        /// <summary>
        /// Counts the tokens in <paramref name="text"/>.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>
        /// The token count; zero for an empty string.
        /// </returns>
        public static int Count(string text)
        {
            ArgumentNullException.ThrowIfNull(text);

            return text.Length == 0 ? 0 : _encoding.CountTokens(text);
        }

        #endregion

    }

}
