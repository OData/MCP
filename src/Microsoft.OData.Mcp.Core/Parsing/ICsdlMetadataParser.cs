// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.IO;
using Microsoft.OData.Mcp.Core.Models;

namespace Microsoft.OData.Mcp.Core.Parsing
{

    /// <summary>
    /// Interface for parsing OData CSDL (Conceptual Schema Definition Language) XML documents into EDM models.
    /// </summary>
    /// <remarks>
    /// This interface abstracts the parsing of CSDL XML documents that describe the structure of OData services,
    /// including entity types, complex types, entity containers, and their relationships.
    /// It supports OData specification versions 4.0 and later.
    /// </remarks>
    public interface ICsdlMetadataParser
    {

        /// <summary>
        /// Parses a CSDL XML document from a string.
        /// </summary>
        /// <param name="csdlXml">The CSDL XML content as a string.</param>
        /// <returns>The parsed EDM model.</returns>
        /// <exception cref="System.ArgumentException">Thrown when <paramref name="csdlXml"/> is null or whitespace.</exception>
        /// <exception cref="System.Xml.XmlException">Thrown when the XML is malformed.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the CSDL structure is invalid.</exception>
        EdmModel ParseFromString(string csdlXml);

        /// <summary>
        /// Parses a CSDL XML document from a stream.
        /// </summary>
        /// <param name="stream">The stream containing the CSDL XML content.</param>
        /// <returns>The parsed EDM model.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
        /// <exception cref="System.Xml.XmlException">Thrown when the XML is malformed.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the CSDL structure is invalid.</exception>
        EdmModel ParseFromStream(Stream stream);

        /// <summary>
        /// Parses a CSDL XML document from a file.
        /// </summary>
        /// <param name="filePath">The path to the file containing the CSDL XML content.</param>
        /// <returns>The parsed EDM model.</returns>
        /// <exception cref="System.ArgumentException">Thrown when <paramref name="filePath"/> is null or whitespace.</exception>
        /// <exception cref="System.IO.FileNotFoundException">Thrown when the file does not exist.</exception>
        /// <exception cref="System.Xml.XmlException">Thrown when the XML is malformed.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the CSDL structure is invalid.</exception>
        EdmModel ParseFromFile(string filePath);

    }

}
