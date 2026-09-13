// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using Microsoft.OData.Mcp.Benchmarks.Infrastructure;

namespace Microsoft.OData.Mcp.Benchmarks.Reports
{

    /// <summary>
    /// One measured rendering: what it is, where it is written, and its content.
    /// </summary>
    public sealed class Artifact
    {

        #region Properties

        /// <summary>
        /// The measured text.
        /// </summary>
        public string Content { get; }

        /// <summary>
        /// The file name under <c>Formats/</c>.
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// The row label, for example <c>CSDL JSON</c>.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// The token count of <see cref="Content"/>.
        /// </summary>
        public int Tokens { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="Artifact"/> class and counts its tokens once.
        /// </summary>
        /// <param name="label">The row label.</param>
        /// <param name="fileName">The file name under <c>Formats/</c>.</param>
        /// <param name="content">The measured text.</param>
        public Artifact(string label, string fileName, string content)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(label);
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
            ArgumentNullException.ThrowIfNull(content);

            Label = label;
            FileName = fileName;
            Content = content;
            Tokens = Tokenizer.Count(content);
        }

        #endregion

    }

}
