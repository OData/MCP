// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Microsoft.OData.Mcp.Core.Models;

namespace Microsoft.OData.Mcp.Core.Catalog
{

    /// <summary>
    /// Validates tool payloads against the declared EDM before any OData HTTP is sent, and formats values for the wire.
    /// </summary>
    /// <remarks>
    /// This is how the first <c>tools/call</c> becomes the right one: a wrong JSON kind, an unknown property, a missing
    /// required-on-create value, an enum member that does not exist, or a string over <c>MaxLength</c> is reported with
    /// the declared shape instead of a round trip to the service. Every rule here is OPTIMIZATION.md §4 and §5.
    /// </remarks>
    public static class EdmPayloadValidator
    {

        #region Public Methods

        /// <summary>
        /// Describes the JSON the EDM type expects, for error messages.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="edmType">The EDM type name.</param>
        /// <returns>
        /// Text such as <c>a JSON number</c> or <c>one of Red, Green</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is null.</exception>
        public static string DescribeExpected(EdmModel model, string edmType)
        {
            ArgumentNullException.ThrowIfNull(model);

            if (edmType.StartsWith("Collection(", StringComparison.Ordinal))
            {
                return "a JSON array";
            }

            var enumType = model.GetEnumType(edmType);
            if (enumType is not null)
            {
                return $"one of {string.Join(", ", enumType.Members.Select(member => member.Name))}";
            }

            if (model.GetComplexType(edmType) is not null)
            {
                return "a JSON object";
            }

            return edmType switch
            {
                "Edm.Boolean" => "a JSON boolean",
                "Edm.Byte" or "Edm.SByte" or "Edm.Int16" or "Edm.Int32" => "a JSON integer",
                "Edm.Int64" or "Edm.Decimal" => "a JSON number or a numeric string",
                "Edm.Double" or "Edm.Single" => "a JSON number",
                "Edm.Guid" => "a GUID string",
                _ when edmType.StartsWith("Edm.Geography", StringComparison.Ordinal) || edmType.StartsWith("Edm.Geometry", StringComparison.Ordinal) => "a GeoJSON object",
                _ when edmType.StartsWith("Edm.", StringComparison.Ordinal) => "a JSON string",
                _ => "a JSON object"
            };
        }

        /// <summary>
        /// Formats a primitive or enumeration argument as an OData URL literal for a function call.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="edmType">The parameter's EDM type.</param>
        /// <param name="value">The validated JSON value.</param>
        /// <returns>
        /// The literal (for example <c>'open'</c>, <c>33</c>, <c>NS.Color'Red'</c>, <c>duration'P1D'</c>), or <c>null</c> when the
        /// value must travel as a parameter alias (complex types, collections, entities).
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is null.</exception>
        public static string? FormatUrlLiteral(EdmModel model, string edmType, JsonElement value)
        {
            ArgumentNullException.ThrowIfNull(model);

            if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return "null";
            }

            if (edmType.StartsWith("Collection(", StringComparison.Ordinal) || model.GetComplexType(edmType) is not null || model.GetEntityType(edmType) is not null)
            {
                return null;
            }

            var enumType = model.GetEnumType(edmType);
            if (enumType is not null)
            {
                return $"{enumType.FullName}'{NormalizeEnumText(enumType, value)}'";
            }

            var text = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();

            return edmType switch
            {
                "Edm.String" => $"'{text.Replace("'", "''", StringComparison.Ordinal)}'",
                "Edm.Duration" => $"duration'{text}'",
                "Edm.Binary" => $"binary'{text}'",
                _ => text
            };
        }

        /// <summary>
        /// Converts a validated argument to the value written into an action's JSON body.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="edmType">The parameter's EDM type.</param>
        /// <param name="value">The validated JSON value.</param>
        /// <returns>
        /// The member name for enumerations given as numbers or OData literals; otherwise the element unchanged.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is null.</exception>
        public static object? NormalizeBodyValue(EdmModel model, string edmType, JsonElement value)
        {
            ArgumentNullException.ThrowIfNull(model);

            if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return null;
            }

            var enumType = model.GetEnumType(edmType);

            return enumType is null ? value : NormalizeEnumText(enumType, value);
        }

        /// <summary>
        /// Validates a create or update body against the declared type.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="options">Catalog options (required-on-create enforcement, enum wire format).</param>
        /// <param name="shape">The target type's shape.</param>
        /// <param name="body">The parsed JSON object.</param>
        /// <param name="isCreate"><c>true</c> for POST, which also checks required-on-create; <c>false</c> for PATCH.</param>
        /// <returns>
        /// <c>null</c> when the body is valid; otherwise the message the caller returns as a tool error.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/>, <paramref name="options"/>, or <paramref name="shape"/> is null.</exception>
        /// <remarks>
        /// Checks, in order: required-on-create (POST only, unless <see cref="ODataMcpCatalogOptions.EnforceRequiredOnCreate"/>
        /// is off), unknown properties on closed types, JSON kind per EDM type, <c>null</c> for non-nullable properties, enum
        /// membership, and <c>MaxLength</c>. Keys containing <c>@</c> (annotations, <c>Nav@odata.bind</c>) and navigation
        /// property names (deep insert) are passed through.
        /// </remarks>
        public static string? ValidateEntityBody(EdmModel model, ODataMcpCatalogOptions options, EdmTypeShape shape, JsonElement body, bool isCreate)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(shape);

            if (body.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var type = shape.EntityType;
            if (isCreate && options.EnforceRequiredOnCreate)
            {
                var missing = shape.RequiredOnCreate
                    .Where(name => !body.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                    .ToList();
                if (missing.Count > 0)
                {
                    return $"Missing required properties on {type.Name}: {string.Join(", ", missing)}. Required on create: {string.Join(", ", shape.RequiredOnCreate)}.";
                }
            }

            return ValidateStructuredValue(model, options, type.Name, type.Properties, type.NavigationProperties, type.OpenType, body);
        }

        /// <summary>
        /// Validates the <c>parameters</c> object of an <c>odata_call</c> against the operation's declared parameters.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="options">Catalog options (enum wire format).</param>
        /// <param name="parameters">The declared parameters, including the binding parameter when bound.</param>
        /// <param name="isBound">Whether the first declared parameter is the binding parameter.</param>
        /// <param name="arguments">The caller's <c>parameters</c> object, or <c>null</c> when omitted.</param>
        /// <param name="signature">The rendered signature, appended to errors.</param>
        /// <param name="values">Receives the accepted arguments keyed by declared parameter name, in declaration order.</param>
        /// <returns>
        /// <c>null</c> when every argument is declared, required ones are present, and kinds match; otherwise the error message.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/>, <paramref name="options"/>, or <paramref name="parameters"/> is null.</exception>
        /// <remarks>
        /// Names match by exact spelling; there is no fuzzy matching. Passing the binding parameter is an unknown-parameter error.
        /// </remarks>
        public static string? ValidateOperationArguments(EdmModel model, ODataMcpCatalogOptions options, IReadOnlyList<EdmParameter> parameters, bool isBound, JsonElement? arguments, string signature, out List<KeyValuePair<EdmParameter, JsonElement>> values)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(parameters);

            values = [];
            var declared = parameters.Skip(isBound ? 1 : 0).ToList();
            var declaredNames = declared.Count == 0 ? "none" : string.Join(", ", declared.Select(parameter => parameter.Name));

            if (arguments is { ValueKind: JsonValueKind.Object } supplied)
            {
                foreach (var argument in supplied.EnumerateObject())
                {
                    if (declared.All(parameter => !parameter.Name.Equals(argument.Name, StringComparison.Ordinal)))
                    {
                        return $"Unknown parameter '{argument.Name}'. Declared: {declaredNames}.";
                    }
                }
            }

            foreach (var parameter in declared)
            {
                var value = default(JsonElement);
                var provided = arguments is { ValueKind: JsonValueKind.Object } source
                    && source.TryGetProperty(parameter.Name, out value)
                    && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);
                if (!provided)
                {
                    if (!parameter.Nullable)
                    {
                        return $"Missing parameter '{parameter.Name}'. Signature: {signature}";
                    }

                    continue;
                }

                var error = ValidateValue(model, options, parameter.Type, parameter.Nullable, parameter.MaxLength, value, $"Parameter '{parameter.Name}'");
                if (error is not null)
                {
                    return $"{error} Signature: {signature}";
                }

                values.Add(new KeyValuePair<EdmParameter, JsonElement>(parameter, value));
            }

            return null;
        }

        /// <summary>
        /// Validates one JSON value against an EDM type.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="options">Catalog options (enum wire format).</param>
        /// <param name="edmType">The EDM type name, possibly <c>Collection(...)</c>.</param>
        /// <param name="nullable">Whether JSON <c>null</c> is acceptable.</param>
        /// <param name="maxLength">The declared <c>MaxLength</c> for strings, or <c>null</c>.</param>
        /// <param name="value">The JSON value.</param>
        /// <param name="label">How to name the value in errors, for example <c>Parameter 'lat'</c> or <c>Property 'Age' on Person</c>.</param>
        /// <returns>
        /// <c>null</c> when the value fits; otherwise a sentence ending in a period.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> or <paramref name="options"/> is null.</exception>
        public static string? ValidateValue(EdmModel model, ODataMcpCatalogOptions options, string edmType, bool nullable, int? maxLength, JsonElement value, string label)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(options);

            if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return nullable ? null : $"{label} cannot be null.";
            }

            if (edmType.StartsWith("Collection(", StringComparison.Ordinal) && edmType.EndsWith(')'))
            {
                if (value.ValueKind != JsonValueKind.Array)
                {
                    return $"{label} must be a JSON array.";
                }

                var elementType = edmType["Collection(".Length..^1];
                var index = 0;
                foreach (var item in value.EnumerateArray())
                {
                    var error = ValidateValue(model, options, elementType, nullable: true, maxLength, item, $"{label}[{index}]");
                    if (error is not null)
                    {
                        return error;
                    }

                    index++;
                }

                return null;
            }

            var enumType = model.GetEnumType(edmType);
            if (enumType is not null)
            {
                return ValidateEnum(enumType, options.EnumJsonFormat, value, label);
            }

            var complexType = model.GetComplexType(edmType);
            if (complexType is not null)
            {
                return value.ValueKind == JsonValueKind.Object
                    ? ValidateStructuredValue(model, options, complexType.Name, complexType.Properties, complexType.NavigationProperties, complexType.OpenType, value)
                    : $"{label} must be a JSON object.";
            }

            if (model.GetEntityType(edmType) is not null)
            {
                return value.ValueKind is JsonValueKind.Object or JsonValueKind.Array ? null : $"{label} must be a JSON object.";
            }

            return edmType switch
            {
                "Edm.Boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False ? null : $"{label} must be a JSON boolean.",
                "Edm.Byte" or "Edm.SByte" or "Edm.Int16" or "Edm.Int32" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _) ? null : $"{label} must be a JSON integer.",
                "Edm.Int64" or "Edm.Decimal" => IsNumberOrNumericString(value) ? null : $"{label} must be a JSON number or a numeric string.",
                "Edm.Double" or "Edm.Single" => value.ValueKind == JsonValueKind.Number || (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out _)) ? null : $"{label} must be a JSON number.",
                "Edm.Guid" => value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out _) ? null : $"{label} must be a GUID string.",
                "Edm.String" => ValidateString(value, maxLength, label),
                _ when edmType.StartsWith("Edm.Geography", StringComparison.Ordinal) || edmType.StartsWith("Edm.Geometry", StringComparison.Ordinal) => value.ValueKind == JsonValueKind.Object ? null : $"{label} must be a GeoJSON object.",
                _ when edmType.StartsWith("Edm.", StringComparison.Ordinal) => value.ValueKind == JsonValueKind.String ? null : $"{label} must be a JSON string.",
                _ => null
            };
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Determines whether a value is a JSON number or a string that parses as a decimal number.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// <c>true</c> for numbers and numeric strings.
        /// </returns>
        internal static bool IsNumberOrNumericString(JsonElement value)
        {
            return value.ValueKind == JsonValueKind.Number
                || (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out _));
        }

        /// <summary>
        /// Converts an enumeration value in any accepted form to canonical member names (comma-separated for flags).
        /// </summary>
        /// <param name="enumType">The enumeration type.</param>
        /// <param name="value">A member name, an OData literal, or a number.</param>
        /// <returns>
        /// The member name(s) with declared casing; unknown input is returned as typed.
        /// </returns>
        internal static string NormalizeEnumText(EdmEnumType enumType, JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
            {
                if (!enumType.IsFlags)
                {
                    return enumType.Members.FirstOrDefault(member => member.Value == number)?.Name ?? number.ToString(CultureInfo.InvariantCulture);
                }

                var names = enumType.Members.Where(member => member.Value != 0 && (member.Value & number) == member.Value).Select(member => member.Name).ToList();

                return names.Count > 0 ? string.Join(",", names) : number.ToString(CultureInfo.InvariantCulture);
            }

            var text = StripEnumLiteral(enumType, value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText());
            var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => enumType.Members.FirstOrDefault(member => member.Name.Equals(part, StringComparison.OrdinalIgnoreCase))?.Name
                    ?? (long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric) ? enumType.Members.FirstOrDefault(member => member.Value == numeric)?.Name : null)
                    ?? part);

            return string.Join(",", parts);
        }

        /// <summary>
        /// Removes the <c>NS.Enum'...'</c> wrapper from an OData enum literal, if present.
        /// </summary>
        /// <param name="enumType">The enumeration type.</param>
        /// <param name="text">The raw text.</param>
        /// <returns>
        /// The inner member text, or the text unchanged.
        /// </returns>
        internal static string StripEnumLiteral(EdmEnumType enumType, string text)
        {
            var trimmed = text.Trim();
            foreach (var prefix in new[] { $"{enumType.FullName}'", $"{enumType.Name}'" })
            {
                if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith('\''))
                {
                    return trimmed[prefix.Length..^1];
                }
            }

            return trimmed;
        }

        /// <summary>
        /// Validates an enumeration value according to the catalog's wire format.
        /// </summary>
        /// <param name="enumType">The enumeration type.</param>
        /// <param name="format">The advertised wire format.</param>
        /// <param name="value">The JSON value.</param>
        /// <param name="label">The value's name in errors.</param>
        /// <returns>
        /// <c>null</c> when every member named or numbered exists; otherwise the error.
        /// </returns>
        internal static string? ValidateEnum(EdmEnumType enumType, ODataEnumJsonFormat format, JsonElement value, string label)
        {
            var names = string.Join(", ", enumType.Members.Select(member => member.Name));
            if (value.ValueKind == JsonValueKind.Number)
            {
                if (format == ODataEnumJsonFormat.String)
                {
                    return $"{label} must be one of {names} (a member name, not a number).";
                }

                if (!value.TryGetInt64(out var number))
                {
                    return $"{label} must be one of {names}.";
                }

                return enumType.IsFlags || enumType.Members.Any(member => member.Value == number)
                    ? null
                    : $"{label} must be one of {names}; {number} is not a member value.";
            }

            if (value.ValueKind != JsonValueKind.String)
            {
                return $"{label} must be one of {names}.";
            }

            var text = StripEnumLiteral(enumType, value.GetString() ?? string.Empty);
            var parts = enumType.IsFlags
                ? text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : [text.Trim()];
            if (parts.Length == 0)
            {
                return $"{label} must be one of {names}.";
            }

            foreach (var part in parts)
            {
                var known = enumType.Members.Any(member => member.Name.Equals(part, StringComparison.OrdinalIgnoreCase))
                    || (long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric) && enumType.Members.Any(member => member.Value == numeric));
                if (!known)
                {
                    return $"{label} must be one of {names}; '{part}' is not a member.";
                }
            }

            return null;
        }

        /// <summary>
        /// Validates a string value and its <c>MaxLength</c>.
        /// </summary>
        /// <param name="value">The JSON value.</param>
        /// <param name="maxLength">The declared maximum, or <c>null</c>.</param>
        /// <param name="label">The value's name in errors.</param>
        /// <returns>
        /// <c>null</c> when the value is a string within the limit.
        /// </returns>
        internal static string? ValidateString(JsonElement value, int? maxLength, string label)
        {
            if (value.ValueKind != JsonValueKind.String)
            {
                return $"{label} must be a JSON string.";
            }

            if (maxLength is > 0 && (value.GetString()?.Length ?? 0) > maxLength)
            {
                return $"{label} exceeds MaxLength {maxLength}.";
            }

            return null;
        }

        /// <summary>
        /// Validates the members of a JSON object against a structured type's declared properties.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="options">Catalog options.</param>
        /// <param name="typeName">The type's short name for errors.</param>
        /// <param name="properties">The declared structural properties.</param>
        /// <param name="navigations">The declared navigation properties (passed through).</param>
        /// <param name="isOpen">Whether undeclared members are allowed.</param>
        /// <param name="body">The JSON object.</param>
        /// <returns>
        /// <c>null</c> when every member is declared (or the type is open) and every declared member fits its type.
        /// </returns>
        internal static string? ValidateStructuredValue(EdmModel model, ODataMcpCatalogOptions options, string typeName, IReadOnlyList<EdmProperty> properties, IReadOnlyList<EdmNavigationProperty> navigations, bool isOpen, JsonElement body)
        {
            foreach (var member in body.EnumerateObject())
            {
                if (member.Name.Contains('@', StringComparison.Ordinal))
                {
                    continue;
                }

                var property = properties.FirstOrDefault(candidate => candidate.Name.Equals(member.Name, StringComparison.Ordinal));
                if (property is null)
                {
                    if (isOpen || navigations.Any(navigation => navigation.Name.Equals(member.Name, StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    return $"Unknown property '{member.Name}' on {typeName}. Declared: {string.Join(", ", properties.Select(candidate => candidate.Name))}.";
                }

                var error = ValidateValue(model, options, property.Type, property.Nullable, property.MaxLength, member.Value, $"Property '{member.Name}' on {typeName}");
                if (error is not null)
                {
                    return error;
                }
            }

            return null;
        }

        #endregion

    }

}
