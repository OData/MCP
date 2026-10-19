// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.OData.Mcp.Core.Models;
using static Microsoft.OData.Mcp.Core.Constants.ODataMcpCatalogConstants;

namespace Microsoft.OData.Mcp.Core.Catalog
{

    /// <summary>
    /// Renders the whole-service map behind <c>odata_describe_model</c>: a summary of sets and navigations, a complete
    /// dump of every in-scope type, a relationship-only mermaid diagram, and the unbound operation list.
    /// </summary>
    /// <remarks>
    /// This is the context-sized stand-in for <c>$metadata</c>. Every shape is composed from the cached
    /// <see cref="EdmTypeShape"/> instances so <c>odata_describe_type</c>, type cards, and the model map never disagree.
    /// </remarks>
    public static class EdmModelShape
    {

        #region Fields

        internal static readonly Regex MermaidSafeName = new("^[A-Za-z_][A-Za-z0-9_-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        #endregion

        #region Public Methods

        /// <summary>
        /// Renders the complete model as compact JSON: every in-scope type body, the complex types they use, and unbound operations.
        /// </summary>
        /// <param name="catalog">The catalog.</param>
        /// <param name="sets">The in-scope entity sets.</param>
        /// <returns>
        /// <c>{ "types": {...}, "complexTypes"?: {...}, "operations"?: {...} }</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> or <paramref name="sets"/> is null.</exception>
        public static string RenderCompleteJson(ODataMcpCatalog catalog, IReadOnlyList<EdmEntitySet> sets)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(sets);

            var shapes = ResolveShapes(catalog, sets);
            var types = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var shape in shapes)
            {
                types[shape.HeaderName()] = shape.JsonBody;
            }

            var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [Types] = types
            };

            var complexTypes = new Dictionary<string, object?>(StringComparer.Ordinal);
            var filterable = FilterableComplexTypes(catalog._model);
            foreach (var complexType in ResolveComplexTypes(catalog._model, ReferencedTypeNames(catalog._model, shapes)))
            {
                complexTypes[EdmTypeShape.HeaderName(catalog._model, complexType.Namespace, complexType.Name)] = RenderComplexTypeJson(catalog._model, complexType, filterable.Contains(complexType));
            }

            if (complexTypes.Count > 0)
            {
                payload[ComplexTypes] = complexTypes;
            }

            AddOperationsJson(catalog._model, payload);

            return JsonSerializer.Serialize(payload, ODataMcpCatalog.SchemaSerializerOptions);
        }

        /// <summary>
        /// Renders the complete model as text: every in-scope type in the declaration grammar, the complex types they use, then unbound operations.
        /// </summary>
        /// <param name="catalog">The catalog.</param>
        /// <param name="sets">The in-scope entity sets.</param>
        /// <returns>
        /// Blank-line separated blocks, LF endings, no trailing newline.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> or <paramref name="sets"/> is null.</exception>
        public static string RenderCompleteText(ODataMcpCatalog catalog, IReadOnlyList<EdmEntitySet> sets)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(sets);

            var shapes = ResolveShapes(catalog, sets);
            var blocks = shapes.Select(shape => shape.Text).ToList();
            var filterable = FilterableComplexTypes(catalog._model);
            blocks.AddRange(ResolveComplexTypes(catalog._model, ReferencedTypeNames(catalog._model, shapes)).Select(complexType => RenderComplexTypeText(catalog._model, complexType, filterable.Contains(complexType))));

            var operations = RenderOperationLines(catalog._model);
            if (operations.Count > 0)
            {
                blocks.Add(string.Join("\n", operations));
            }

            return string.Join("\n\n", blocks);
        }

        /// <summary>
        /// Renders a relationship-only mermaid <c>erDiagram</c> for the in-scope types.
        /// </summary>
        /// <param name="catalog">The catalog.</param>
        /// <param name="sets">The in-scope entity sets.</param>
        /// <returns>
        /// One <c>A ||--o{ B : Nav</c> line per navigation (<c>||--o|</c> for single targets) and a bare entity line for types with no navigations.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> or <paramref name="sets"/> is null.</exception>
        /// <remarks>
        /// No attribute compartments, whatever the requested detail. Names that are not mermaid-safe are quoted.
        /// </remarks>
        public static string RenderMermaid(ODataMcpCatalog catalog, IReadOnlyList<EdmEntitySet> sets)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(sets);

            var lines = new List<string> { "erDiagram" };
            foreach (var shape in ResolveShapes(catalog, sets))
            {
                var source = MermaidName(shape.HeaderName());
                if (shape.EntityType.NavigationProperties.Count == 0)
                {
                    lines.Add($"  {source}");
                    continue;
                }

                foreach (var navigation in shape.EntityType.NavigationProperties)
                {
                    var targetType = catalog._model.GetEntityType(navigation.TargetType);
                    var target = MermaidName(targetType is null
                        ? EdmTypeShape.ShortName(navigation.TargetType)
                        : EdmTypeShape.HeaderName(catalog._model, targetType.Namespace, targetType.Name));
                    var cardinality = navigation.IsCollection ? "||--o{" : "||--o|";
                    lines.Add($"  {source} {cardinality} {target} : {MermaidName(navigation.Name)}");
                }
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Renders the unbound operations as a name-to-signature map.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <returns>
        /// <c>{ "Name": "(args) -> Return // writes" }</c> in declaration order, functions first. Empty when none are declared.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is null.</exception>
        public static Dictionary<string, string> RenderOperationsJson(EdmModel model)
        {
            ArgumentNullException.ThrowIfNull(model);

            var operations = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var function in model.Functions.Where(function => !function.IsBound))
            {
                operations[function.Name] = EdmTypeShape.RenderOperation(model, null, function.Parameters, function.ReturnType, isBound: false, null, writes: false, function.Description);
            }

            foreach (var action in model.Actions.Where(action => !action.IsBound))
            {
                operations[action.Name] = EdmTypeShape.RenderOperation(model, null, action.Parameters, action.ReturnType, isBound: false, null, writes: true, action.Description);
            }

            return operations;
        }

        /// <summary>
        /// Renders the unbound operations block for text shapes.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <returns>
        /// <c>operations</c> followed by one indented signature per unbound operation, or an empty list when none are declared.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is null.</exception>
        public static IReadOnlyList<string> RenderOperationLines(EdmModel model)
        {
            ArgumentNullException.ThrowIfNull(model);

            var lines = new List<string>();
            lines.AddRange(model.Functions.Where(function => !function.IsBound)
                .Select(function => $"  {EdmTypeShape.RenderOperation(model, function.Name, function.Parameters, function.ReturnType, isBound: false, null, writes: false, function.Description)}"));
            lines.AddRange(model.Actions.Where(action => !action.IsBound)
                .Select(action => $"  {EdmTypeShape.RenderOperation(model, action.Name, action.Parameters, action.ReturnType, isBound: false, null, writes: true, action.Description)}"));

            return lines.Count == 0 ? [] : [Operations, .. lines];
        }

        /// <summary>
        /// Renders the model summary as compact JSON: every in-scope set with its type, key, docs, and navigations, then unbound operations.
        /// </summary>
        /// <param name="catalog">The catalog.</param>
        /// <param name="sets">The in-scope entity sets.</param>
        /// <returns>
        /// <c>{ "sets": { "Customers": { "type", "key", "description"?, "navs"? } }, "operations"?: {...} }</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> or <paramref name="sets"/> is null.</exception>
        public static string RenderSummaryJson(ODataMcpCatalog catalog, IReadOnlyList<EdmEntitySet> sets)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(sets);

            var setMap = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var set in sets)
            {
                var shape = ResolveShape(catalog, set);
                if (shape is null)
                {
                    continue;
                }

                var entry = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [Constants.ODataMcpCatalogConstants.Type] = shape.HeaderName(),
                    [Key] = shape.EntityType.Key
                };
                EdmDocumentation.Add(entry, Description, FirstDocumentation(shape));

                var navs = shape.JsonBody.TryGetValue(Navs, out var value) ? value : null;
                if (navs is not null)
                {
                    entry[Navs] = navs;
                }

                setMap[set.Name] = entry;
            }

            var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [Sets] = setMap
            };
            AddOperationsJson(catalog._model, payload);

            return JsonSerializer.Serialize(payload, ODataMcpCatalog.SchemaSerializerOptions);
        }

        /// <summary>
        /// Renders the model summary as text: one header per in-scope set with at most one doc line and its navigations, then unbound operations.
        /// </summary>
        /// <param name="catalog">The catalog.</param>
        /// <param name="sets">The in-scope entity sets.</param>
        /// <returns>
        /// LF-separated lines, no property lists, no trailing newline.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> or <paramref name="sets"/> is null.</exception>
        public static string RenderSummaryText(ODataMcpCatalog catalog, IReadOnlyList<EdmEntitySet> sets)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(sets);

            var lines = new List<string>();
            foreach (var set in sets)
            {
                var shape = ResolveShape(catalog, set);
                if (shape is null)
                {
                    continue;
                }

                lines.Add(shape.RenderHeaderLine());
                var doc = FirstDocumentation(shape);
                if (doc is not null)
                {
                    lines.Add($"  // {doc}");
                }

                lines.AddRange(shape.RenderNavigationLines());
            }

            lines.AddRange(RenderOperationLines(catalog._model));

            return string.Join("\n", lines);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Adds the unbound operation map to a payload when any are declared.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="payload">The payload.</param>
        internal static void AddOperationsJson(EdmModel model, Dictionary<string, object?> payload)
        {
            var operations = RenderOperationsJson(model);
            if (operations.Count > 0)
            {
                payload[Operations] = operations;
            }
        }

        /// <summary>
        /// Adds the <c>returnedBy</c> list to a type body when any operation returns the type.
        /// </summary>
        /// <param name="body">The type body.</param>
        /// <param name="returnedBy">The operation references from <see cref="RenderReturnedBy"/>.</param>
        internal static void AddReturnedByJson(Dictionary<string, object?> body, List<string> returnedBy)
        {
            if (returnedBy.Count > 0)
            {
                body[ReturnedBy] = returnedBy;
            }
        }

        /// <summary>
        /// Collects the complex types an entity property holds, directly or through other complex types.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <returns>
        /// The complex types a <c>$filter</c> on some entity can reach. A complex type missing from this list is only ever
        /// an operation parameter or result, so an enum filter hint on it would never apply.
        /// </returns>
        internal static IReadOnlyList<EdmComplexType> FilterableComplexTypes(EdmModel model)
        {
            return ResolveComplexTypes(model, model.EntityTypes.SelectMany(type => type.Properties.Where(ODataMcpCatalog.IsExposedProperty)).Select(property => property.Type));
        }

        /// <summary>
        /// Finds the entity, complex, and enumeration types a describe name refers to.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="name">A short or namespace-qualified type name, compared case-insensitively.</param>
        /// <returns>
        /// The <see cref="EdmEntityType"/>, <see cref="EdmComplexType"/>, and <see cref="EdmEnumType"/> matches. A full-name
        /// match wins over short-name matches; more than one result means the short name is declared in several namespaces.
        /// </returns>
        /// <remarks>
        /// Every schema in the document contributes types, so a type declared in a second namespace resolves like any other.
        /// </remarks>
        internal static IReadOnlyList<object> FindDeclaredTypes(EdmModel model, string name)
        {
            var matches = model.EntityTypes.Where(type => MatchesTypeName(type.Name, type.FullName, name)).Cast<object>()
                .Concat(model.ComplexTypes.Where(type => MatchesTypeName(type.Name, type.FullName, name)))
                .Concat(model.EnumTypes.Where(type => MatchesTypeName(type.Name, type.FullName, name)))
                .ToList();
            var exact = matches.Where(type => FullNameOf(type).Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();

            return exact.Count > 0 ? exact : matches;
        }

        /// <summary>
        /// Returns the first informative documentation string for a shape: type description, then set description.
        /// </summary>
        /// <param name="shape">The shape.</param>
        /// <returns>
        /// The text, or <c>null</c>.
        /// </returns>
        internal static string? FirstDocumentation(EdmTypeShape shape)
        {
            if (EdmTypeShape.Informative(shape.EntityType.Description, shape.EntityType.Name))
            {
                return shape.EntityType.Description!.Trim();
            }

            if (shape.EntitySet is not null && EdmTypeShape.Informative(shape.EntitySet.Description, shape.EntitySet.Name))
            {
                return shape.EntitySet.Description!.Trim();
            }

            return null;
        }

        /// <summary>
        /// Gets the qualified name of a type returned by <see cref="FindDeclaredTypes"/>.
        /// </summary>
        /// <param name="type">An <see cref="EdmEntityType"/>, <see cref="EdmComplexType"/>, or <see cref="EdmEnumType"/>.</param>
        /// <returns>
        /// The full name.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="type"/> is not a declared EDM type.</exception>
        internal static string FullNameOf(object type)
        {
            return type switch
            {
                EdmEntityType entityType => entityType.FullName,
                EdmComplexType complexType => complexType.FullName,
                EdmEnumType enumType => enumType.FullName,
                _ => throw new ArgumentException($"'{type.GetType().Name}' is not a declared EDM type.", nameof(type))
            };
        }

        /// <summary>
        /// Determines whether a describe name equals a type's short or full name, ignoring case.
        /// </summary>
        /// <param name="shortName">The type's short name.</param>
        /// <param name="fullName">The type's qualified name.</param>
        /// <param name="name">The requested name.</param>
        /// <returns>
        /// <c>true</c> when either name matches.
        /// </returns>
        internal static bool MatchesTypeName(string shortName, string fullName, string name)
        {
            return shortName.Equals(name, StringComparison.OrdinalIgnoreCase) || fullName.Equals(name, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Quotes a name for mermaid when it contains characters outside letters, digits, underscore, and hyphen.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>
        /// The name, quoted if needed.
        /// </returns>
        internal static string MermaidName(string name)
        {
            return MermaidSafeName.IsMatch(name) ? name : $"\"{name.Replace("\"", "'", StringComparison.Ordinal)}\"";
        }

        /// <summary>
        /// Collects the type names the in-scope shapes reference: every exposed property type first, then the parameter and
        /// return types of their bound operations and of the unbound operations.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="shapes">The in-scope shapes.</param>
        /// <returns>
        /// Type names in first-use order, possibly wrapped in <c>Collection(...)</c>.
        /// </returns>
        /// <remarks>
        /// Operation types follow property types so a complex type only an operation uses, such as an action's result,
        /// is appended after the ones the entities themselves carry.
        /// </remarks>
        internal static IEnumerable<string> ReferencedTypeNames(EdmModel model, IReadOnlyList<EdmTypeShape> shapes)
        {
            var propertyTypes = shapes.SelectMany(shape => shape.ExposedProperties).Select(property => property.Type);
            var operations = shapes.SelectMany(shape => shape.BoundFunctions.Select(function => (function.Parameters, function.ReturnType))
                    .Concat(shape.BoundActions.Select(action => (action.Parameters, action.ReturnType))))
                .Concat(model.Functions.Where(function => !function.IsBound).Select(function => (function.Parameters, function.ReturnType)))
                .Concat(model.Actions.Where(action => !action.IsBound).Select(action => (action.Parameters, action.ReturnType)));
            var operationTypes = operations.SelectMany(operation => operation.Parameters.Select(parameter => (string?)parameter.Type).Append(operation.ReturnType));

            return propertyTypes.Concat(operationTypes.OfType<string>());
        }

        /// <summary>
        /// Renders a complex type as a compact JSON body with <c>returnedBy</c>, <c>props</c>, <c>docs</c>, and <c>navs</c>.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="complexType">The complex type.</param>
        /// <param name="filterable">Whether an entity property holds the type, so <c>$filter</c> can reach it; see <see cref="FilterableComplexTypes"/>.</param>
        /// <returns>
        /// The body dictionary. <c>enumFilterLiterals</c> is present only when <paramref name="filterable"/> is <c>true</c>.
        /// </returns>
        internal static Dictionary<string, object?> RenderComplexTypeJson(EdmModel model, EdmComplexType complexType, bool filterable)
        {
            var body = new Dictionary<string, object?>(StringComparer.Ordinal);
            EdmDocumentation.Add(body, Description, EdmTypeShape.Informative(complexType.Description, complexType.Name) ? complexType.Description : null);
            AddReturnedByJson(body, RenderReturnedBy(model, complexType.FullName));

            var exposed = complexType.Properties.Where(ODataMcpCatalog.IsExposedProperty).ToList();
            var literals = filterable ? EdmTypeShape.RenderEnumFilterLiterals(model, exposed) : [];
            if (literals.Count > 0)
            {
                body[EnumFilterLiterals] = literals;
            }

            var docs = new Dictionary<string, string>(StringComparer.Ordinal);
            body[Props] = EdmTypeShape.BuildPropsJson(model, exposed, docs);

            var navs = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var navigation in complexType.NavigationProperties)
            {
                navs[navigation.Name] = EdmTypeShape.MapShapeType(model, navigation.Type);
                EdmDocumentation.Add(docs, navigation.Name, EdmTypeShape.DocumentationFor(navigation.Description, navigation.LongDescription, navigation.Name));
            }

            if (docs.Count > 0)
            {
                body[Docs] = docs;
            }

            if (navs.Count > 0)
            {
                body[Navs] = navs;
            }

            return body;
        }

        /// <summary>
        /// Renders a complex type in the declaration grammar with a <c>(complex)</c> header marker.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="complexType">The complex type.</param>
        /// <param name="filterable">Whether an entity property holds the type, so <c>$filter</c> can reach it; see <see cref="FilterableComplexTypes"/>.</param>
        /// <returns>
        /// The text block: header, description, <c>// returned by</c> line, enum filter hint (only when filterable), then members.
        /// </returns>
        internal static string RenderComplexTypeText(EdmModel model, EdmComplexType complexType, bool filterable)
        {
            var lines = new List<string> { $"{EdmTypeShape.HeaderName(model, complexType.Namespace, complexType.Name)}  (complex)" };
            if (EdmTypeShape.Informative(complexType.Description, complexType.Name))
            {
                lines.Add($"  // {complexType.Description!.Trim()}");
            }

            lines.AddRange(RenderReturnedByLines(model, complexType.FullName));
            lines.AddRange(EdmTypeShape.RenderPropertyLines(model, [.. complexType.Properties.Where(ODataMcpCatalog.IsExposedProperty)], [], filterable));
            lines.AddRange(complexType.NavigationProperties.Select(navigation => $"  {EdmTypeShape.RenderNavigation(model, navigation)}"));

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Renders an enumeration as a compact JSON body with the operations that return it, its filter literal, and member values.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="enumType">The enumeration type.</param>
        /// <returns>
        /// <c>{ "description"?, "longDescription"?, "returnedBy"?: [...], "flags"?: true, "enumFilterLiterals": ["NS.Enum'{value}'"], "members": { "Name": value } }</c>.
        /// </returns>
        /// <remarks>
        /// The filter literal is always present: any property that holds an enumeration value can be filtered by it.
        /// </remarks>
        internal static Dictionary<string, object?> RenderEnumTypeJson(EdmModel model, EdmEnumType enumType)
        {
            var body = new Dictionary<string, object?>(StringComparer.Ordinal);
            var description = EdmTypeShape.Informative(enumType.Description, enumType.Name) ? enumType.Description!.Trim() : null;
            EdmDocumentation.Add(body, Description, description);
            EdmDocumentation.Add(body, LongDescription, EdmTypeShape.Informative(enumType.LongDescription, enumType.Name) ? enumType.LongDescription!.Trim() : null, description);
            AddReturnedByJson(body, RenderReturnedBy(model, enumType.FullName));

            if (enumType.IsFlags)
            {
                body[Flags] = true;
            }

            body[EnumFilterLiterals] = new List<string> { EdmTypeShape.RenderEnumFilterLiteral(enumType) };
            body[Members] = enumType.Members.ToDictionary(member => member.Name, member => member.Value, StringComparer.Ordinal);

            return body;
        }

        /// <summary>
        /// Renders an enumeration in the declaration grammar with an <c>(enum)</c> header marker and one <c>Name = value</c> line per member.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="enumType">The enumeration type.</param>
        /// <returns>
        /// The text block. Flags enumerations are marked <c>(enum, flags)</c>; combine their members comma-separated.
        /// </returns>
        internal static string RenderEnumTypeText(EdmModel model, EdmEnumType enumType)
        {
            var header = EdmTypeShape.HeaderName(model, enumType.Namespace, enumType.Name);
            var lines = new List<string> { enumType.IsFlags ? $"{header}  (enum, flags)" : $"{header}  (enum)" };
            var docs = new List<string>();
            EdmTypeShape.AddDistinct(docs, enumType.Description, enumType.Name);
            EdmTypeShape.AddDistinct(docs, enumType.LongDescription, enumType.Name);
            lines.AddRange(docs.Select(doc => $"  // {doc}"));
            lines.AddRange(RenderReturnedByLines(model, enumType.FullName));
            lines.Add($"  // filter enums as {EdmTypeShape.RenderEnumFilterLiteral(enumType)}");
            lines.AddRange(enumType.Members.Select(member => $"  {member.Name} = {member.Value}"));

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Lists the operations whose return type is the given type or a collection of it, functions first, in declaration order.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="fullName">The returned type's qualified name.</param>
        /// <returns>
        /// <c>Binding.Name</c> for bound operations (<c>Binding[].Name</c> when collection-bound) and <c>Name</c> for unbound
        /// ones, for example <c>IntegrationConnection.Authorize</c>. Empty when no operation returns the type.
        /// </returns>
        /// <remarks>
        /// This tells the calling AI where a result-only type comes from without searching the model. The signature stays
        /// on the binding type's <c>operations</c> block or in <c>odata_list_operations</c>.
        /// </remarks>
        internal static List<string> RenderReturnedBy(EdmModel model, string fullName)
        {
            var operations = model.Functions.Select(function => (function.Name, function.IsBound, function.BindingParameterType, function.ReturnType))
                .Concat(model.Actions.Select(action => (action.Name, action.IsBound, action.BindingParameterType, action.ReturnType)));

            return [.. operations
                .Where(operation => operation.ReturnType is not null
                    && (operation.ReturnType.Equals(fullName, StringComparison.Ordinal) || operation.ReturnType.Equals($"Collection({fullName})", StringComparison.Ordinal)))
                .Select(operation => operation.IsBound && !string.IsNullOrWhiteSpace(operation.BindingParameterType)
                    ? $"{EdmTypeShape.MapShapeType(model, operation.BindingParameterType)}.{operation.Name}"
                    : operation.Name)];
        }

        /// <summary>
        /// Renders the <c>// returned by</c> line for a type, or nothing when no operation returns it.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="fullName">The returned type's qualified name.</param>
        /// <returns>
        /// Zero or one indented comment line.
        /// </returns>
        internal static IReadOnlyList<string> RenderReturnedByLines(EdmModel model, string fullName)
        {
            var returnedBy = RenderReturnedBy(model, fullName);

            return returnedBy.Count > 0 ? [$"  // returned by {string.Join(", ", returnedBy)}"] : [];
        }

        /// <summary>
        /// Collects the complex types the given type names reference, directly or through other complex types, in first-use order.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="typeNames">Referenced type names, possibly wrapped in <c>Collection(...)</c>; names that are not complex types are skipped.</param>
        /// <returns>
        /// The complex types declared in the model that those names reach.
        /// </returns>
        internal static IReadOnlyList<EdmComplexType> ResolveComplexTypes(EdmModel model, IEnumerable<string> typeNames)
        {
            var found = new List<EdmComplexType>();
            var pending = new Queue<IEnumerable<string>>([typeNames]);
            while (pending.Count > 0)
            {
                foreach (var typeName in pending.Dequeue())
                {
                    var complexType = model.GetComplexType(typeName.StartsWith("Collection(", StringComparison.Ordinal) && typeName.EndsWith(')') ? typeName["Collection(".Length..^1] : typeName);
                    if (complexType is null || found.Contains(complexType))
                    {
                        continue;
                    }

                    found.Add(complexType);
                    pending.Enqueue(complexType.Properties.Where(ODataMcpCatalog.IsExposedProperty).Select(property => property.Type));
                }
            }

            return found;
        }

        /// <summary>
        /// Resolves the shape for a set, rebuilding when the cached shape was paired with a different set of the same type.
        /// </summary>
        /// <param name="catalog">The catalog.</param>
        /// <param name="set">The entity set.</param>
        /// <returns>
        /// The shape, or <c>null</c> when the set's type is not declared.
        /// </returns>
        internal static EdmTypeShape? ResolveShape(ODataMcpCatalog catalog, EdmEntitySet set)
        {
            var type = catalog.ResolveEntityType(set);
            if (type is null)
            {
                return null;
            }

            var shape = catalog.GetShape(type);

            return ReferenceEquals(shape.EntitySet, set) ? shape : new EdmTypeShape(catalog._model, type, set);
        }

        /// <summary>
        /// Resolves one shape per distinct entity type behind the given sets, in set order.
        /// </summary>
        /// <param name="catalog">The catalog.</param>
        /// <param name="sets">The in-scope entity sets.</param>
        /// <returns>
        /// The shapes.
        /// </returns>
        internal static IReadOnlyList<EdmTypeShape> ResolveShapes(ODataMcpCatalog catalog, IReadOnlyList<EdmEntitySet> sets)
        {
            var shapes = new List<EdmTypeShape>();
            foreach (var set in sets)
            {
                var shape = ResolveShape(catalog, set);
                if (shape is not null && shapes.All(existing => !existing.FullName.Equals(shape.FullName, StringComparison.Ordinal)))
                {
                    shapes.Add(shape);
                }
            }

            return shapes;
        }

        /// <summary>
        /// Serializes one type body under its header name, the <c>format=json</c> envelope <c>odata_describe_type</c> returns.
        /// </summary>
        /// <param name="headerName">The type's display name.</param>
        /// <param name="body">The compact JSON body.</param>
        /// <returns>
        /// <c>{ "Type": { ... } }</c>.
        /// </returns>
        internal static string SerializeType(string headerName, IReadOnlyDictionary<string, object?> body)
        {
            return JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal) { [headerName] = body }, ODataMcpCatalog.SchemaSerializerOptions);
        }

        #endregion

    }

}
