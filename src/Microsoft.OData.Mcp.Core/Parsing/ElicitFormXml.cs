// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.OData.Mcp.Core.Vocabularies;

namespace Microsoft.OData.Mcp.Core.Parsing
{

    /// <summary>
    /// Closed AOT reader for the compact <c>Form</c> / <c>Field</c> / <c>Option</c> island
    /// nested under <c>Microsoft.OData.Mcp.V1.ElicitForm</c>.
    /// </summary>
    /// <remarks>
    /// Same grammar as McpEssentials <c>FormXml</c>. Core does not reference McpEssentials.
    /// Readers match <see cref="XmlReader.LocalName"/> only. DTD processing is prohibited.
    /// </remarks>
    public static class ElicitFormXml
    {

        #region Fields

        internal const string DefaultAttribute = "Default";
        internal const string DescriptionAttribute = "Description";
        internal const string FalseLiteral = "false";
        internal const string FieldElement = "Field";
        internal const string FormatAttribute = "Format";
        internal const string MaxAttribute = "Max";
        internal const string MaxLengthAttribute = "MaxLength";
        internal const string MessageAttribute = "Message";
        internal const string MinAttribute = "Min";
        internal const string MinLengthAttribute = "MinLength";
        internal const string NameAttribute = "Name";
        internal const string OptionElement = "Option";
        internal const string RequiredAttribute = "Required";
        internal const string SelectedAttribute = "Selected";
        internal const string TitleAttribute = "Title";
        internal const string TrueLiteral = "true";
        internal const string TypeAttribute = "Type";
        internal const string TypeBoolean = "boolean";
        internal const string TypeEnum = "enum";
        internal const string TypeInteger = "integer";
        internal const string TypeMulti = "multi";
        internal const string TypeNumber = "number";
        internal const string TypeString = "string";
        internal const string ValueAttribute = "Value";
        internal const string XmlNamespaceUri = "http://www.w3.org/XML/1998/namespace";
        internal const string XmlnsNamespaceUri = "http://www.w3.org/2000/xmlns/";

        #endregion

        #region Public Methods

        /// <summary>
        /// Reads a compact form island. Namespace prefixes are ignored.
        /// </summary>
        /// <param name="xml">The island text.</param>
        /// <returns>The catalog snapshot.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="xml"/> is empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the island is not a valid form.</exception>
        /// <exception cref="XmlException">Thrown when the text is not well-formed XML or contains a DTD.</exception>
        public static EdmElicitForm Parse(string xml)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(xml);

            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreProcessingInstructions = false,
                IgnoreWhitespace = true
            };

            using var reader = XmlReader.Create(new StringReader(xml), settings);
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == McpVocabulary.FormElementName)
                {
                    return ReadForm(reader);
                }
            }

            throw new InvalidOperationException("Form XML must have a Form root element.");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Maps a compact <c>Type</c> spelling onto <see cref="EdmElicitFormFieldType"/>.
        /// </summary>
        /// <param name="type">The attribute value.</param>
        /// <returns>The field type.</returns>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="type"/> is unknown.</exception>
        internal static EdmElicitFormFieldType ParseFieldType(string type)
        {
            return type switch
            {
                TypeString => EdmElicitFormFieldType.String,
                TypeInteger => EdmElicitFormFieldType.Integer,
                TypeNumber => EdmElicitFormFieldType.Number,
                TypeBoolean => EdmElicitFormFieldType.Boolean,
                TypeEnum => EdmElicitFormFieldType.Enum,
                TypeMulti => EdmElicitFormFieldType.Multi,
                _ => throw new InvalidOperationException($"Unknown form field Type '{type}'.")
            };
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Gets whether the current attribute is xmlns or xml: and should be skipped.
        /// </summary>
        /// <param name="reader">The reader, positioned on an attribute.</param>
        /// <returns><see langword="true"/> when the attribute is a namespace declaration.</returns>
        private static bool IsIgnoredNamespace(XmlReader reader)
        {
            return reader.NamespaceURI is XmlnsNamespaceUri or XmlNamespaceUri;
        }

        /// <summary>
        /// Reads a boolean attribute that accepts <c>true</c>, <c>True</c>, and <c>1</c>.
        /// </summary>
        /// <param name="value">The attribute value.</param>
        /// <param name="attributeName">The attribute local name, for errors.</param>
        /// <returns><see langword="true"/> when the value is a true token.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the value is not a boolean token.</exception>
        private static bool ParseBoolean(string value, string attributeName)
        {
            if (string.Equals(value, TrueLiteral, StringComparison.OrdinalIgnoreCase) || value == "1")
            {
                return true;
            }

            if (string.Equals(value, FalseLiteral, StringComparison.OrdinalIgnoreCase) || value == "0")
            {
                return false;
            }

            throw new InvalidOperationException($"Form attribute {attributeName} value '{value}' is not a boolean.");
        }

        /// <summary>
        /// Reads a double using invariant XML conversion.
        /// </summary>
        /// <param name="value">The attribute value.</param>
        /// <param name="attributeName">The attribute local name, for errors.</param>
        /// <returns>The number.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the value is not a number.</exception>
        private static double ParseDouble(string value, string attributeName)
        {
            try
            {
                var number = XmlConvert.ToDouble(value);
                if (double.IsInfinity(number) || double.IsNaN(number))
                {
                    throw new InvalidOperationException($"Form attribute {attributeName} value '{value}' is not a number.");
                }

                return number;
            }
            catch (Exception ex) when (ex is FormatException or OverflowException)
            {
                throw new InvalidOperationException($"Form attribute {attributeName} value '{value}' is not a number.", ex);
            }
        }

        /// <summary>
        /// Reads an integer using invariant XML conversion.
        /// </summary>
        /// <param name="value">The attribute value.</param>
        /// <param name="attributeName">The attribute local name, for errors.</param>
        /// <returns>The integer.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the value is not an integer.</exception>
        private static int ParseInt(string value, string attributeName)
        {
            try
            {
                return XmlConvert.ToInt32(value);
            }
            catch (Exception ex) when (ex is FormatException or OverflowException)
            {
                throw new InvalidOperationException($"Form attribute {attributeName} value '{value}' is not an integer.", ex);
            }
        }

        /// <summary>
        /// Reads one <c>Field</c> element, including nested <c>Option</c> children.
        /// </summary>
        /// <param name="reader">Positioned on the <c>Field</c> start tag.</param>
        /// <returns>The field snapshot.</returns>
        private static EdmElicitFormField ReadField(XmlReader reader)
        {
            string? name = null;
            string? typeText = null;
            var required = false;
            string? format = null;
            string? title = null;
            string? description = null;
            int? minLength = null;
            int? maxLength = null;
            double? minimum = null;
            double? maximum = null;
            int? minItems = null;
            int? maxItems = null;
            string? defaultText = null;

            if (reader.MoveToFirstAttribute())
            {
                do
                {
                    if (IsIgnoredNamespace(reader))
                    {
                        continue;
                    }

                    switch (reader.LocalName)
                    {
                        case NameAttribute:
                            name = reader.Value;
                            break;
                        case TypeAttribute:
                            typeText = reader.Value;
                            break;
                        case RequiredAttribute:
                            required = ParseBoolean(reader.Value, RequiredAttribute);
                            break;
                        case FormatAttribute:
                            format = reader.Value;
                            break;
                        case TitleAttribute:
                            title = reader.Value;
                            break;
                        case DescriptionAttribute:
                            description = reader.Value;
                            break;
                        case MinLengthAttribute:
                            minLength = ParseInt(reader.Value, MinLengthAttribute);
                            break;
                        case MaxLengthAttribute:
                            maxLength = ParseInt(reader.Value, MaxLengthAttribute);
                            break;
                        case MinAttribute:
                            minimum = ParseDouble(reader.Value, MinAttribute);
                            break;
                        case MaxAttribute:
                            maximum = ParseDouble(reader.Value, MaxAttribute);
                            break;
                        case DefaultAttribute:
                            defaultText = reader.Value;
                            break;
                        default:
                            throw new InvalidOperationException($"Unknown form Field attribute '{reader.LocalName}'.");
                    }
                }
                while (reader.MoveToNextAttribute());

                reader.MoveToElement();
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Form Field Name is required.");
            }

            if (string.IsNullOrWhiteSpace(typeText))
            {
                throw new InvalidOperationException($"Form Field '{name}' is missing Type.");
            }

            var type = ParseFieldType(typeText);
            if (type is EdmElicitFormFieldType.Enum or EdmElicitFormFieldType.Multi && defaultText is not null)
            {
                throw new InvalidOperationException($"Field '{name}' of Type '{typeText}' must use Selected on Option, not Default.");
            }

            if (type is EdmElicitFormFieldType.Multi)
            {
                try
                {
                    minItems = minimum is { } min ? checked((int)min) : null;
                    maxItems = maximum is { } max ? checked((int)max) : null;
                }
                catch (OverflowException ex)
                {
                    throw new InvalidOperationException($"Field '{name}' Min/Max is out of range for an item count.", ex);
                }

                minimum = null;
                maximum = null;
            }

            var options = new List<EdmElicitFormOption>();
            ReadToEnd(reader, $"Form Field '{name}'", child =>
            {
                if (child.LocalName != OptionElement)
                {
                    throw new InvalidOperationException($"Form Field '{name}' has unexpected child '{child.LocalName}'.");
                }

                options.Add(ReadOption(child));
            });

            bool? defaultBool = null;
            double? defaultNumber = null;
            string? defaultString = null;
            IReadOnlyList<string>? defaultStrings = null;
            switch (type)
            {
                case EdmElicitFormFieldType.Boolean when defaultText is not null:
                    defaultBool = ParseBoolean(defaultText, DefaultAttribute);
                    break;
                case EdmElicitFormFieldType.Integer or EdmElicitFormFieldType.Number when defaultText is not null:
                    defaultNumber = ParseDouble(defaultText, DefaultAttribute);
                    break;
                case EdmElicitFormFieldType.String when defaultText is not null:
                    defaultString = defaultText;
                    break;
                case EdmElicitFormFieldType.Enum:
                    defaultString = SelectedValue(options);
                    break;
                case EdmElicitFormFieldType.Multi:
                    defaultStrings = SelectedValues(options);
                    break;
                default:
                    break;
            }

            return new EdmElicitFormField
            {
                Name = name,
                Type = type,
                Required = required,
                Format = format,
                Title = title,
                Description = description,
                MinLength = minLength,
                MaxLength = maxLength,
                Minimum = minimum,
                Maximum = maximum,
                MinItems = minItems,
                MaxItems = maxItems,
                DefaultBool = defaultBool,
                DefaultNumber = defaultNumber,
                DefaultString = defaultString,
                DefaultStrings = defaultStrings,
                Options = options
            };
        }

        /// <summary>
        /// Reads the <c>Form</c> element the reader is positioned on.
        /// </summary>
        /// <param name="reader">Positioned on the <c>Form</c> start tag.</param>
        /// <returns>The form snapshot.</returns>
        private static EdmElicitForm ReadForm(XmlReader reader)
        {
            string? message = null;
            string? name = null;

            if (reader.MoveToFirstAttribute())
            {
                do
                {
                    if (IsIgnoredNamespace(reader))
                    {
                        continue;
                    }

                    switch (reader.LocalName)
                    {
                        case MessageAttribute:
                            message = reader.Value;
                            break;
                        case NameAttribute:
                            name = reader.Value;
                            break;
                        default:
                            throw new InvalidOperationException($"Unknown form Form attribute '{reader.LocalName}'.");
                    }
                }
                while (reader.MoveToNextAttribute());

                reader.MoveToElement();
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new InvalidOperationException("Form elicitation message cannot be empty.");
            }

            var fields = new List<EdmElicitFormField>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            ReadToEnd(reader, "Form", child =>
            {
                if (child.LocalName != FieldElement)
                {
                    throw new InvalidOperationException($"Form has unexpected child '{child.LocalName}'.");
                }

                var field = ReadField(child);
                if (!seen.Add(field.Name))
                {
                    throw new InvalidOperationException($"Form already has a field named '{field.Name}'.");
                }

                fields.Add(field);
            });

            return new EdmElicitForm
            {
                Message = message,
                Name = string.IsNullOrWhiteSpace(name) ? null : name,
                Fields = fields
            };
        }

        /// <summary>
        /// Reads one <c>Option</c> element.
        /// </summary>
        /// <param name="reader">Positioned on the <c>Option</c> start tag.</param>
        /// <returns>The option snapshot.</returns>
        private static EdmElicitFormOption ReadOption(XmlReader reader)
        {
            string? value = null;
            string? title = null;
            var selected = false;

            if (reader.MoveToFirstAttribute())
            {
                do
                {
                    if (IsIgnoredNamespace(reader))
                    {
                        continue;
                    }

                    switch (reader.LocalName)
                    {
                        case ValueAttribute:
                            value = reader.Value;
                            break;
                        case TitleAttribute:
                            title = reader.Value;
                            break;
                        case SelectedAttribute:
                            selected = ParseBoolean(reader.Value, SelectedAttribute);
                            break;
                        default:
                            throw new InvalidOperationException($"Unknown form Option attribute '{reader.LocalName}'.");
                    }
                }
                while (reader.MoveToNextAttribute());

                reader.MoveToElement();
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException("Form Option Value is required.");
            }

            ReadToEnd(reader, $"Form Option '{value}'", onElement: null);

            return new EdmElicitFormOption
            {
                Value = value,
                Title = title,
                Selected = selected
            };
        }

        /// <summary>
        /// Consumes the element the reader is positioned on.
        /// </summary>
        /// <param name="reader">Positioned on the start tag.</param>
        /// <param name="owner">The element being read, for errors.</param>
        /// <param name="onElement">
        /// Called for each child element. <see langword="null"/> when the owner forbids children.
        /// </param>
        private static void ReadToEnd(XmlReader reader, string owner, Action<XmlReader>? onElement)
        {
            var empty = reader.IsEmptyElement;
            if (!empty)
            {
                var depth = reader.Depth;
                reader.Read();
                while (reader.Depth > depth)
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (onElement is null)
                        {
                            throw new InvalidOperationException($"{owner} has unexpected child '{reader.LocalName}'.");
                        }

                        onElement(reader);
                        continue;
                    }

                    ThrowIfMixedContent(reader, owner);
                    throw new InvalidOperationException($"{owner} has unexpected node '{reader.NodeType}'.");
                }
            }

            reader.Read();
        }

        /// <summary>
        /// Gets the single selected option value, or <see langword="null"/> when none is selected.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>The selected value.</returns>
        /// <exception cref="InvalidOperationException">Thrown when more than one option is selected.</exception>
        private static string? SelectedValue(List<EdmElicitFormOption> options)
        {
            string? selected = null;
            foreach (var option in options)
            {
                if (!option.Selected)
                {
                    continue;
                }

                if (selected is not null)
                {
                    throw new InvalidOperationException("An enum field can have at most one Selected option.");
                }

                selected = option.Value;
            }

            return selected;
        }

        /// <summary>
        /// Gets every selected option value, or <see langword="null"/> when none is selected.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>The selected values.</returns>
        private static IReadOnlyList<string>? SelectedValues(List<EdmElicitFormOption> options)
        {
            List<string>? selected = null;
            foreach (var option in options)
            {
                if (!option.Selected)
                {
                    continue;
                }

                selected ??= [];
                selected.Add(option.Value);
            }

            return selected;
        }

        /// <summary>
        /// Throws when the current node is mixed content.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="owner">The element being read, for the error message.</param>
        /// <exception cref="InvalidOperationException">Thrown when mixed content is present.</exception>
        private static void ThrowIfMixedContent(XmlReader reader, string owner)
        {
            if (reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.SignificantWhitespace)
            {
                throw new InvalidOperationException($"{owner} has mixed content.");
            }
        }

        #endregion

    }

}
