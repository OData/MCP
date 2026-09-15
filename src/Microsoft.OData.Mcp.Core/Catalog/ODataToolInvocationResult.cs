// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Microsoft.OData.Mcp.Core.Catalog
{

    /// <summary>
    /// The result of invoking a catalog tool.
    /// </summary>
    public sealed class ODataToolInvocationResult
    {

        #region Fields

        /// <summary>
        /// Catalog-produced JSON, or a UTF-8 reading of <see cref="StructuredStream"/> after first access.
        /// </summary>
        internal string? _structuredContent;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether the tool failed.
        /// </summary>
        public bool IsError { get; set; }

        /// <summary>
        /// Gets or sets structured JSON. Catalog tools set this as text. OData success keeps
        /// <see cref="StructuredStream"/> and materializes this string on first read.
        /// </summary>
        public string? StructuredContent
        {
            get
            {
                if (_structuredContent is not null)
                {
                    return _structuredContent;
                }

                if (StructuredStream is null)
                {
                    return null;
                }

                _structuredContent = ReadUtf8(StructuredStream);

                return _structuredContent;
            }
            set => _structuredContent = value;
        }

        /// <summary>
        /// Gets or sets the OData JSON stream as written, when the executor kept it.
        /// </summary>
        public Stream? StructuredStream { get; set; }

        /// <summary>
        /// Gets or sets the text content returned to the client.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns structured JSON for <c>CallToolResult.StructuredContent</c>. Prefers the OData stream
        /// (UTF-8 parse, no string copy). Falls back to <see cref="StructuredContent"/>.
        /// </summary>
        /// <returns>
        /// The JSON element, or <see langword="null"/> when there is no structured payload.
        /// </returns>
        public JsonElement? ToStructuredContent()
        {
            if (StructuredStream is not null)
            {
                if (StructuredStream.CanSeek && StructuredStream.Length == 0)
                {
                    return null;
                }

                return ParseUtf8(StructuredStream);
            }

            if (string.IsNullOrWhiteSpace(StructuredContent))
            {
                return null;
            }

            return ParseUtf8(StructuredContent);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Parses UTF-8 JSON from a stream without an intermediate string. Uses the existing
        /// <see cref="MemoryStream"/> buffer when possible.
        /// </summary>
        /// <param name="stream">The JSON stream.</param>
        /// <returns>
        /// A detached <see cref="JsonElement"/>.
        /// </returns>
        internal static JsonElement ParseUtf8(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            if (stream is MemoryStream memory && memory.TryGetBuffer(out var segment))
            {
                var start = segment.Offset + (int)memory.Position;
                var length = (int)(memory.Length - memory.Position);
                using var buffered = JsonDocument.Parse(new ReadOnlyMemory<byte>(segment.Array!, start, length));

                return buffered.RootElement.Clone();
            }

            using var document = JsonDocument.Parse(stream);

            return document.RootElement.Clone();
        }

        /// <summary>
        /// Parses UTF-8 JSON from a string. Used for catalog-generated JSON, not OData payloads.
        /// </summary>
        /// <param name="json">The JSON text.</param>
        /// <returns>
        /// A detached <see cref="JsonElement"/>.
        /// </returns>
        internal static JsonElement ParseUtf8(string json)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);

            using var document = JsonDocument.Parse(json);

            return document.RootElement.Clone();
        }

        /// <summary>
        /// Reads a JSON stream as UTF-8 text. Used when a caller asks for
        /// <see cref="StructuredContent"/> after the executor kept <see cref="StructuredStream"/>.
        /// </summary>
        /// <param name="stream">The JSON stream.</param>
        /// <returns>
        /// The UTF-8 payload.
        /// </returns>
        internal static string ReadUtf8(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            if (stream is MemoryStream memory && memory.TryGetBuffer(out var segment))
            {
                var start = segment.Offset + (int)memory.Position;
                var length = (int)(memory.Length - memory.Position);

                return Encoding.UTF8.GetString(segment.Array!, start, length);
            }

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);

            return reader.ReadToEnd();
        }

        #endregion

    }

}
