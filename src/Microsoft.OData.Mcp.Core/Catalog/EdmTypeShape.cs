// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.OData.Mcp.Core.Models;
using static Microsoft.OData.Mcp.Core.Constants.ODataMcpCatalogConstants;

namespace Microsoft.OData.Mcp.Core.Catalog
{

    /// <summary>
    /// The shape of one entity type as the calling AI reads it: the declaration-grammar text, the compact JSON,
    /// the exposed properties, the required-on-create names, and the operations bound to the type.
    /// </summary>
    /// <remarks>
    /// Instances are computed once per type and cached for the catalog lifetime when the model is static
    /// (<see cref="ODataMcpCatalogOptions.IsDynamicModel"/> is <c>false</c>). Everything here comes from
    /// declared EDM members; CLR entity types are never walked. The grammar is specified in
    /// <c>specs/v3/TYPE-SHAPES.md</c>.
    /// </remarks>
    /// <example>
    /// <code>
    /// Customer  (set: Customers, key: CustomerID)
    ///   CustomerID: string // key
    ///   CompanyName?: string
    ///   Orders -> Order[]
    ///   operations
    ///     Discount(percentage: int) // writes
    /// </code>
    /// </example>
    public sealed class EdmTypeShape
    {

        #region Fields

        internal const int InlineMaxLength = 16;

        internal readonly EdmModel _model;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the actions bound to this type or one of its base types, in declaration order.
        /// </summary>
        public IReadOnlyList<EdmAction> BoundActions { get; }

        /// <summary>
        /// Gets the functions bound to this type or one of its base types, in declaration order.
        /// </summary>
        public IReadOnlyList<EdmFunction> BoundFunctions { get; }

        /// <summary>
        /// Gets the entity set the shape was resolved through, or <c>null</c> when the type has no included set.
        /// </summary>
        public EdmEntitySet? EntitySet { get; }

        /// <summary>
        /// Gets the entity type.
        /// </summary>
        public EdmEntityType EntityType { get; }

        /// <summary>
        /// Gets the declared structural properties that appear in shapes and schemas (binary and stream excluded).
        /// </summary>
        public IReadOnlyList<EdmProperty> ExposedProperties { get; }

        /// <summary>
        /// Gets the fully qualified type name, which is also the cache key.
        /// </summary>
        public string FullName => EntityType.FullName;

        /// <summary>
        /// Gets the compact JSON representation used by <c>format=json</c> and <c>resources/read</c> type cards.
        /// </summary>
        public string Json { get; }

        /// <summary>
        /// Gets the compact JSON body (everything under the type-name key), for embedding in model-level shapes.
        /// </summary>
        public IReadOnlyDictionary<string, object?> JsonBody { get; }

        /// <summary>
        /// Gets the names of exposed properties that a POST must include.
        /// </summary>
        /// <remarks>
        /// A property is required on create when it is non-nullable, has no <c>DefaultValue</c>, and is not
        /// <see cref="EdmProperty.Computed"/>. Store generation is never inferred from the property type.
        /// </remarks>
        public IReadOnlyList<string> RequiredOnCreate { get; }

        /// <summary>
        /// Gets the declaration-grammar text used by <c>format=text</c>, the default describe representation.
        /// </summary>
        public string Text { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="EdmTypeShape"/> class.
        /// </summary>
        /// <param name="model">The Core EDM the type belongs to.</param>
        /// <param name="entityType">The entity type to shape.</param>
        /// <param name="entitySet">The entity set the type is reached through, or <c>null</c>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> or <paramref name="entityType"/> is null.</exception>
        public EdmTypeShape(EdmModel model, EdmEntityType entityType, EdmEntitySet? entitySet)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(entityType);

            _model = model;
            EntityType = entityType;
            EntitySet = entitySet;
            ExposedProperties = [.. entityType.Properties.Where(ODataMcpCatalog.IsExposedProperty)];
            RequiredOnCreate = [.. ExposedProperties.Where(IsRequiredOnCreate).Select(property => property.Name)];

            var bindingTypes = ResolveBindingTypeNames(model, entityType);
            BoundActions = [.. model.Actions.Where(action => action.IsBound && BindsTo(action.BindingParameterType, bindingTypes))];
            BoundFunctions = [.. model.Functions.Where(function => function.IsBound && BindsTo(function.BindingParameterType, bindingTypes))];

            Text = RenderText();
            JsonBody = BuildJsonBody();
            Json = JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal) { [HeaderName()] = JsonBody }, ODataMcpCatalog.SchemaSerializerOptions);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Determines whether a binding parameter type is a collection.
        /// </summary>
        /// <param name="bindingParameterType">The operation's binding parameter type, or <c>null</c>.</param>
        /// <returns>
        /// <c>true</c> when the binding parameter is <c>Collection(...)</c>; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsCollectionBound(string? bindingParameterType)
        {
            return !string.IsNullOrWhiteSpace(bindingParameterType)
                && bindingParameterType.StartsWith("Collection(", StringComparison.Ordinal);
        }

        /// <summary>
        /// Determines whether a property must be present on POST.
        /// </summary>
        /// <param name="property">The declared property.</param>
        /// <returns>
        /// <c>true</c> when the property is non-nullable, has no <c>DefaultValue</c>, and is not computed.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="property"/> is null.</exception>
        public static bool IsRequiredOnCreate(EdmProperty property)
        {
            ArgumentNullException.ThrowIfNull(property);

            return !property.Nullable
                && string.IsNullOrWhiteSpace(property.DefaultValue)
                && !property.Computed;
        }

        /// <summary>
        /// Maps an EDM type name to the shape grammar's type token.
        /// </summary>
        /// <param name="model">The Core EDM, used to resolve enumeration members.</param>
        /// <param name="edmType">The EDM type name, for example <c>Edm.String</c>, <c>NS.Color</c>, or <c>Collection(NS.Order)</c>.</param>
        /// <param name="maxLength">The declared <c>MaxLength</c>, inlined as <c>string(n)</c> only when at most 16.</param>
        /// <returns>
        /// The token: <c>string</c>, <c>string(n)</c>, <c>bool</c>, <c>int</c>, <c>int64</c>, <c>number</c>, <c>date</c>,
        /// <c>datetime</c>, <c>time</c>, <c>duration</c>, <c>guid</c>, <c>enum(A|B)</c>, a short type name, or any of those with <c>[]</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is null.</exception>
        public static string MapShapeType(EdmModel model, string? edmType, int? maxLength = null)
        {
            ArgumentNullException.ThrowIfNull(model);

            if (string.IsNullOrWhiteSpace(edmType))
            {
                return "string";
            }

            if (edmType.StartsWith("Collection(", StringComparison.Ordinal) && edmType.EndsWith(')'))
            {
                return $"{MapShapeType(model, edmType["Collection(".Length..^1], maxLength)}[]";
            }

            var enumType = model.GetEnumType(edmType);
            if (enumType is not null)
            {
                return $"enum({string.Join("|", enumType.Members.Select(member => member.Name))})";
            }

            return edmType switch
            {
                "Edm.String" when maxLength is > 0 and <= InlineMaxLength => $"string({maxLength})",
                "Edm.String" => "string",
                "Edm.Boolean" => "bool",
                "Edm.Byte" or "Edm.SByte" or "Edm.Int16" or "Edm.Int32" => "int",
                "Edm.Int64" => "int64",
                "Edm.Decimal" or "Edm.Double" or "Edm.Single" => "number",
                "Edm.Date" => "date",
                "Edm.DateTimeOffset" => "datetime",
                "Edm.TimeOfDay" => "time",
                "Edm.Duration" => "duration",
                "Edm.Guid" => "guid",
                _ when edmType.StartsWith("Edm.", StringComparison.Ordinal) => edmType["Edm.".Length..],
                _ => ShortName(edmType)
            };
        }

        /// <summary>
        /// Renders an operation signature in the shape grammar, omitting the binding parameter.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="name">The operation name, or <c>null</c> to render only the parenthesized part (compact JSON).</param>
        /// <param name="parameters">The declared parameters, including the binding parameter when bound.</param>
        /// <param name="returnType">The declared return type, or <c>null</c> for none.</param>
        /// <param name="isBound">Whether the first parameter is the binding parameter.</param>
        /// <param name="bindingParameterType">The binding parameter type, used for the <c>// collection</c> marker.</param>
        /// <param name="writes">Whether the operation mutates (an EDM action).</param>
        /// <param name="description">CSDL documentation, or <c>null</c>.</param>
        /// <returns>
        /// For example <c>ShareTrip(userName: string, tripId: int) // writes</c> or <c>(count: int) -> Person[] // collection</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> or <paramref name="parameters"/> is null.</exception>
        public static string RenderOperation(EdmModel model, string? name, IReadOnlyList<EdmParameter> parameters, string? returnType, bool isBound, string? bindingParameterType, bool writes, string? description)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(parameters);

            var arguments = parameters
                .Skip(isBound ? 1 : 0)
                .Select(parameter => $"{parameter.Name}{(parameter.Nullable ? "?" : string.Empty)}: {MapShapeType(model, parameter.Type, parameter.MaxLength)}");
            var builder = new StringBuilder();
            builder.Append(name).Append('(').Append(string.Join(", ", arguments)).Append(')');
            if (!string.IsNullOrWhiteSpace(returnType))
            {
                builder.Append(" -> ").Append(MapShapeType(model, returnType));
            }

            var markers = new List<string>();
            if (isBound && IsCollectionBound(bindingParameterType))
            {
                markers.Add("collection");
            }

            if (writes)
            {
                markers.Add("writes");
            }

            AppendComment(builder, markers, DocumentationFor(description, null, name));

            return builder.ToString();
        }

        /// <summary>
        /// Renders one structural property line in the shape grammar, without indentation.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="property">The property.</param>
        /// <param name="isKey">Whether the property is part of the entity key.</param>
        /// <returns>
        /// For example <c>CustomerID: string // key</c> or <c>Access?: enum(Read|Write) // flags, comma-separated</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> or <paramref name="property"/> is null.</exception>
        public static string RenderProperty(EdmModel model, EdmProperty property, bool isKey)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(property);

            var builder = new StringBuilder(property.Name);
            if (!IsRequiredOnCreate(property))
            {
                builder.Append('?');
            }

            builder.Append(": ").Append(MapShapeType(model, property.Type, property.MaxLength));

            var markers = new List<string>();
            if (isKey)
            {
                markers.Add(property.Computed ? "key, store-generated" : "key");
            }

            if (EnumTypeOf(model, property)?.IsFlags == true)
            {
                markers.Add("flags, comma-separated");
            }

            AppendComment(builder, markers, Informative(property.Description, property.Name) ? property.Description!.Trim() : null);

            return builder.ToString();
        }

        /// <summary>
        /// Renders one navigation line in the shape grammar, without indentation.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="navigation">The navigation property.</param>
        /// <returns>
        /// <c>Orders -> Order[]</c> for collections, <c>BestFriend? -> Person</c> for optional single targets.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> or <paramref name="navigation"/> is null.</exception>
        public static string RenderNavigation(EdmModel model, EdmNavigationProperty navigation)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(navigation);

            var builder = new StringBuilder(navigation.Name);
            if (!navigation.IsCollection && navigation.Nullable)
            {
                builder.Append('?');
            }

            builder.Append(" -> ").Append(MapShapeType(model, navigation.Type));
            AppendComment(builder, [], DocumentationFor(navigation.Description, navigation.LongDescription, navigation.Name));

            return builder.ToString();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Adds an informative documentation string to a list unless an equal string is already present.
        /// </summary>
        /// <param name="target">The list.</param>
        /// <param name="text">The candidate text.</param>
        /// <param name="memberName">The member the text documents.</param>
        internal static void AddDistinct(List<string> target, string? text, string memberName)
        {
            if (!Informative(text, memberName))
            {
                return;
            }

            var trimmed = text!.Trim();
            if (!target.Contains(trimmed, StringComparer.Ordinal))
            {
                target.Add(trimmed);
            }
        }

        /// <summary>
        /// Appends a <c>// marker, marker; docs</c> comment when there is anything to say.
        /// </summary>
        /// <param name="builder">The line under construction.</param>
        /// <param name="markers">Structural markers such as <c>key</c> or <c>writes</c>.</param>
        /// <param name="documentation">Documentation text, or <c>null</c>.</param>
        internal static void AppendComment(StringBuilder builder, IReadOnlyList<string> markers, string? documentation)
        {
            var parts = new List<string>();
            if (markers.Count > 0)
            {
                parts.Add(string.Join(", ", markers));
            }

            if (!string.IsNullOrWhiteSpace(documentation))
            {
                parts.Add(documentation);
            }

            if (parts.Count > 0)
            {
                builder.Append(" // ").Append(string.Join("; ", parts));
            }
        }

        /// <summary>
        /// Determines whether a binding parameter type names one of the given types, directly or as a collection.
        /// </summary>
        /// <param name="bindingParameterType">The operation's binding parameter type.</param>
        /// <param name="typeNames">The full names of the type and its base types.</param>
        /// <returns>
        /// <c>true</c> when the binding parameter targets one of the names.
        /// </returns>
        internal static bool BindsTo(string? bindingParameterType, IReadOnlyCollection<string> typeNames)
        {
            if (string.IsNullOrWhiteSpace(bindingParameterType))
            {
                return false;
            }

            var target = IsCollectionBound(bindingParameterType)
                ? bindingParameterType["Collection(".Length..^1]
                : bindingParameterType;

            return typeNames.Contains(target, StringComparer.Ordinal);
        }

        /// <summary>
        /// Builds the compact JSON body: set, key, docs, enum literal, props, docs, navs, ops.
        /// </summary>
        /// <returns>
        /// The body dictionary, in emission order.
        /// </returns>
        internal Dictionary<string, object?> BuildJsonBody()
        {
            var body = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (EntitySet is not null)
            {
                body[Set] = EntitySet.Name;
            }

            body[Key] = EntityType.Key;

            var description = Informative(EntityType.Description, EntityType.Name) ? EntityType.Description!.Trim() : null;
            var longDescription = Informative(EntityType.LongDescription, EntityType.Name) ? EntityType.LongDescription!.Trim() : null;
            var setDescription = EntitySet is not null && Informative(EntitySet.Description, EntitySet.Name) ? EntitySet.Description!.Trim() : null;
            EdmDocumentation.Add(body, Description, description);
            EdmDocumentation.Add(body, LongDescription, longDescription, description);
            EdmDocumentation.Add(body, SetDescription, setDescription, description);

            var firstEnum = FirstEnumType();
            if (firstEnum is not null)
            {
                body[EnumLiteral] = RenderEnumLiteral(firstEnum);
            }

            var docs = new Dictionary<string, string>(StringComparer.Ordinal);
            body[Props] = BuildPropsJson(_model, ExposedProperties, docs);

            var navs = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var navigation in EntityType.NavigationProperties)
            {
                navs[navigation.Name] = MapShapeType(_model, navigation.Type);
                EdmDocumentation.Add(docs, navigation.Name, DocumentationFor(navigation.Description, navigation.LongDescription, navigation.Name));
            }

            if (docs.Count > 0)
            {
                body[Docs] = docs;
            }

            if (navs.Count > 0)
            {
                body[Navs] = navs;
            }

            var ops = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var function in BoundFunctions)
            {
                ops[function.Name] = RenderOperation(_model, null, function.Parameters, function.ReturnType, function.IsBound, function.BindingParameterType, writes: false, function.Description);
            }

            foreach (var action in BoundActions)
            {
                ops[action.Name] = RenderOperation(_model, null, action.Parameters, action.ReturnType, action.IsBound, action.BindingParameterType, writes: true, action.Description);
            }

            if (ops.Count > 0)
            {
                body[Ops] = ops;
            }

            return body;
        }

        /// <summary>
        /// Builds the <c>props</c> map (<c>"Name": "type!"</c>) and collects property docs.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="properties">The exposed properties.</param>
        /// <param name="docs">Receives informative property documentation.</param>
        /// <returns>
        /// The props map in declaration order.
        /// </returns>
        internal static Dictionary<string, string> BuildPropsJson(EdmModel model, IEnumerable<EdmProperty> properties, Dictionary<string, string> docs)
        {
            var props = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in properties)
            {
                var token = MapShapeType(model, property.Type, property.MaxLength);
                if (EnumTypeOf(model, property)?.IsFlags == true)
                {
                    token = token.Replace("enum(", "flags(", StringComparison.Ordinal);
                }

                props[property.Name] = IsRequiredOnCreate(property) ? $"{token}!" : token;
                EdmDocumentation.Add(docs, property.Name, DocumentationFor(property.Description, property.LongDescription, property.Name));
            }

            return props;
        }

        /// <summary>
        /// Counts the namespaces that declare entity, complex, or enumeration types.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <returns>
        /// The distinct type namespace count. Container-only namespaces do not count.
        /// </returns>
        internal static int CountTypeNamespaces(EdmModel model)
        {
            return model.EntityTypes.Select(type => type.Namespace)
                .Concat(model.ComplexTypes.Select(type => type.Namespace))
                .Concat(model.EnumTypes.Select(type => type.Namespace))
                .Distinct(StringComparer.Ordinal)
                .Count();
        }

        /// <summary>
        /// Returns the documentation worth printing: the description when it carries information, plus the long
        /// description when it adds something the description did not say.
        /// </summary>
        /// <param name="description">The CSDL description.</param>
        /// <param name="longDescription">The CSDL long description.</param>
        /// <param name="memberName">The member name, which is never documentation.</param>
        /// <returns>
        /// The combined text, or <c>null</c> when nothing informative is declared.
        /// </returns>
        internal static string? DocumentationFor(string? description, string? longDescription, string? memberName)
        {
            var summary = Informative(description, memberName) ? description!.Trim() : null;
            var detail = Informative(longDescription, memberName) && !string.Equals(longDescription!.Trim(), summary, StringComparison.Ordinal)
                ? longDescription.Trim()
                : null;

            return (summary, detail) switch
            {
                (null, null) => null,
                (_, null) => summary,
                (null, _) => detail,
                _ => $"{summary} {detail}"
            };
        }

        /// <summary>
        /// Resolves the enumeration type behind a property, if any.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="property">The property.</param>
        /// <returns>
        /// The enumeration type, or <c>null</c>.
        /// </returns>
        internal static EdmEnumType? EnumTypeOf(EdmModel model, EdmProperty property)
        {
            return model.GetEnumType(property.IsCollection ? property.ElementType : property.Type);
        }

        /// <summary>
        /// Finds the first enumeration type used by an exposed property, for the literal hint.
        /// </summary>
        /// <returns>
        /// The enumeration type, or <c>null</c> when the type has no enum properties.
        /// </returns>
        internal EdmEnumType? FirstEnumType()
        {
            return FirstEnumType(_model, ExposedProperties);
        }

        /// <summary>
        /// Finds the first enumeration type used by any of the given properties.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="properties">The properties to scan.</param>
        /// <returns>
        /// The enumeration type, or <c>null</c>.
        /// </returns>
        internal static EdmEnumType? FirstEnumType(EdmModel model, IEnumerable<EdmProperty> properties)
        {
            foreach (var property in properties)
            {
                var enumType = EnumTypeOf(model, property);
                if (enumType is not null)
                {
                    return enumType;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the header name: the short name, or the full name when the model declares types in more than one namespace.
        /// </summary>
        /// <returns>
        /// The display name.
        /// </returns>
        internal string HeaderName()
        {
            return HeaderName(_model, EntityType.Namespace, EntityType.Name);
        }

        /// <summary>
        /// Gets the display name for a type: short unless the model declares types in more than one namespace.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="namespace">The type namespace.</param>
        /// <param name="name">The type name.</param>
        /// <returns>
        /// The display name.
        /// </returns>
        internal static string HeaderName(EdmModel model, string @namespace, string name)
        {
            return CountTypeNamespaces(model) > 1 ? $"{@namespace}.{name}" : name;
        }

        /// <summary>
        /// Determines whether a documentation string says something beyond the member name.
        /// </summary>
        /// <param name="text">The documentation string.</param>
        /// <param name="memberName">The member name.</param>
        /// <returns>
        /// <c>true</c> when the text is non-whitespace and not the member name.
        /// </returns>
        internal static bool Informative(string? text, string? memberName)
        {
            return !string.IsNullOrWhiteSpace(text)
                && !string.Equals(text.Trim(), memberName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Renders the OData filter literal example for an enumeration, for example <c>NS.Color'Red'</c>.
        /// </summary>
        /// <param name="enumType">The enumeration type.</param>
        /// <returns>
        /// The literal.
        /// </returns>
        internal static string RenderEnumLiteral(EdmEnumType enumType)
        {
            return $"{enumType.FullName}'{enumType.Members[0].Name}'";
        }

        /// <summary>
        /// Renders the header documentation lines: type description, type long description, set description, set long description, deduplicated.
        /// </summary>
        /// <returns>
        /// Zero or more <c>  // text</c> lines.
        /// </returns>
        internal IReadOnlyList<string> RenderHeaderDocLines()
        {
            var headerDocs = new List<string>();
            AddDistinct(headerDocs, EntityType.Description, EntityType.Name);
            AddDistinct(headerDocs, EntityType.LongDescription, EntityType.Name);
            if (EntitySet is not null)
            {
                AddDistinct(headerDocs, EntitySet.Description, EntitySet.Name);
                AddDistinct(headerDocs, EntitySet.LongDescription, EntitySet.Name);
            }

            return [.. headerDocs.Select(doc => $"  // {doc}")];
        }

        /// <summary>
        /// Renders the header line, for example <c>Customer  (set: Customers, key: CustomerID)</c>.
        /// </summary>
        /// <returns>
        /// The header line.
        /// </returns>
        internal string RenderHeaderLine()
        {
            var headerParts = new List<string>();
            if (EntitySet is not null)
            {
                headerParts.Add($"set: {EntitySet.Name}");
            }

            if (EntityType.Key.Count > 0)
            {
                headerParts.Add($"key: {string.Join(", ", EntityType.Key)}");
            }

            return headerParts.Count > 0 ? $"{HeaderName()}  ({string.Join(", ", headerParts)})" : HeaderName();
        }

        /// <summary>
        /// Renders the indented navigation lines.
        /// </summary>
        /// <returns>
        /// One <c>  Name -> Type</c> line per navigation property.
        /// </returns>
        internal IReadOnlyList<string> RenderNavigationLines()
        {
            return [.. EntityType.NavigationProperties.Select(navigation => $"  {RenderNavigation(_model, navigation)}")];
        }

        /// <summary>
        /// Renders the indented bound-operation block, or nothing when the type has no bound operations.
        /// </summary>
        /// <returns>
        /// <c>  operations</c> followed by one <c>    Signature</c> line per operation, or an empty list.
        /// </returns>
        internal IReadOnlyList<string> RenderOperationLines()
        {
            if (BoundFunctions.Count == 0 && BoundActions.Count == 0)
            {
                return [];
            }

            var lines = new List<string> { "  operations" };
            lines.AddRange(BoundFunctions.Select(function => $"    {RenderOperation(_model, function.Name, function.Parameters, function.ReturnType, function.IsBound, function.BindingParameterType, writes: false, function.Description)}"));
            lines.AddRange(BoundActions.Select(action => $"    {RenderOperation(_model, action.Name, action.Parameters, action.ReturnType, action.IsBound, action.BindingParameterType, writes: true, action.Description)}"));

            return lines;
        }

        /// <summary>
        /// Renders the indented property lines, including the enum literal hint and long-description lines.
        /// </summary>
        /// <returns>
        /// The lines.
        /// </returns>
        internal IReadOnlyList<string> RenderPropertyLines()
        {
            return RenderPropertyLines(_model, ExposedProperties, EntityType.Key);
        }

        /// <summary>
        /// Renders indented property lines for any structured type.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="properties">The exposed properties.</param>
        /// <param name="key">The key property names, or empty.</param>
        /// <returns>
        /// The enum literal hint (when any property is an enum), then one line per property with an optional long-description line.
        /// </returns>
        internal static IReadOnlyList<string> RenderPropertyLines(EdmModel model, IReadOnlyList<EdmProperty> properties, IReadOnlyList<string> key)
        {
            var lines = new List<string>();
            var firstEnum = FirstEnumType(model, properties);
            if (firstEnum is not null)
            {
                lines.Add($"  // enum literal: {RenderEnumLiteral(firstEnum)}");
            }

            foreach (var property in properties)
            {
                lines.Add($"  {RenderProperty(model, property, key.Contains(property.Name, StringComparer.Ordinal))}");

                var summary = Informative(property.Description, property.Name) ? property.Description!.Trim() : null;
                if (Informative(property.LongDescription, property.Name) && !string.Equals(property.LongDescription!.Trim(), summary, StringComparison.Ordinal))
                {
                    lines.Add($"    // {property.LongDescription.Trim()}");
                }
            }

            return lines;
        }

        /// <summary>
        /// Renders the declaration-grammar text.
        /// </summary>
        /// <returns>
        /// The text, LF-separated, without a trailing newline.
        /// </returns>
        internal string RenderText()
        {
            var lines = new List<string> { RenderHeaderLine() };
            lines.AddRange(RenderHeaderDocLines());
            lines.AddRange(RenderPropertyLines());
            lines.AddRange(RenderNavigationLines());
            lines.AddRange(RenderOperationLines());

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Collects the full names of a type and every base type declared in the model.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="entityType">The starting type.</param>
        /// <returns>
        /// The type's full name followed by its base chain.
        /// </returns>
        internal static IReadOnlyCollection<string> ResolveBindingTypeNames(EdmModel model, EdmEntityType entityType)
        {
            var names = new List<string> { entityType.FullName };
            var current = entityType;
            while (current.HasBaseType && !names.Contains(current.BaseType!, StringComparer.Ordinal))
            {
                names.Add(current.BaseType!);
                var next = model.GetEntityType(current.BaseType!);
                if (next is null)
                {
                    break;
                }

                current = next;
            }

            return names;
        }

        /// <summary>
        /// Returns the part of a qualified name after the last dot.
        /// </summary>
        /// <param name="qualifiedName">The qualified name.</param>
        /// <returns>
        /// The short name.
        /// </returns>
        internal static string ShortName(string qualifiedName)
        {
            var dot = qualifiedName.LastIndexOf('.');

            return dot >= 0 ? qualifiedName[(dot + 1)..] : qualifiedName;
        }

        #endregion

    }

}
