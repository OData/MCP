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
            foreach (var complexType in ResolveComplexTypes(catalog._model, shapes.Select(shape => shape.EntityType)))
            {
                complexTypes[EdmTypeShape.HeaderName(catalog._model, complexType.Namespace, complexType.Name)] = RenderComplexTypeJson(catalog._model, complexType);
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
            blocks.AddRange(ResolveComplexTypes(catalog._model, shapes.Select(shape => shape.EntityType)).Select(complexType => RenderComplexTypeText(catalog._model, complexType)));

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
                    var target = MermaidName(EdmTypeShape.ShortName(navigation.TargetType));
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
        /// Renders a complex type as a compact JSON body with <c>props</c>, <c>docs</c>, and <c>navs</c>.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="complexType">The complex type.</param>
        /// <returns>
        /// The body dictionary.
        /// </returns>
        internal static Dictionary<string, object?> RenderComplexTypeJson(EdmModel model, EdmComplexType complexType)
        {
            var body = new Dictionary<string, object?>(StringComparer.Ordinal);
            EdmDocumentation.Add(body, Description, EdmTypeShape.Informative(complexType.Description, complexType.Name) ? complexType.Description : null);

            var exposed = complexType.Properties.Where(ODataMcpCatalog.IsExposedProperty).ToList();
            var literals = EdmTypeShape.RenderEnumFilterLiterals(model, exposed);
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
        /// <returns>
        /// The text block.
        /// </returns>
        internal static string RenderComplexTypeText(EdmModel model, EdmComplexType complexType)
        {
            var lines = new List<string> { $"{EdmTypeShape.HeaderName(model, complexType.Namespace, complexType.Name)}  (complex)" };
            if (EdmTypeShape.Informative(complexType.Description, complexType.Name))
            {
                lines.Add($"  // {complexType.Description!.Trim()}");
            }

            lines.AddRange(EdmTypeShape.RenderPropertyLines(model, [.. complexType.Properties.Where(ODataMcpCatalog.IsExposedProperty)], []));
            lines.AddRange(complexType.NavigationProperties.Select(navigation => $"  {EdmTypeShape.RenderNavigation(model, navigation)}"));

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Collects the complex types used, directly or transitively, by the given entity types, in first-use order.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="entityTypes">The in-scope entity types.</param>
        /// <returns>
        /// The complex types declared in the model that those entities reference.
        /// </returns>
        internal static IReadOnlyList<EdmComplexType> ResolveComplexTypes(EdmModel model, IEnumerable<EdmEntityType> entityTypes)
        {
            var found = new List<EdmComplexType>();
            var pending = new Queue<IEnumerable<EdmProperty>>(entityTypes.Select(type => type.Properties.Where(ODataMcpCatalog.IsExposedProperty)));
            while (pending.Count > 0)
            {
                foreach (var property in pending.Dequeue())
                {
                    var complexType = model.GetComplexType(property.IsCollection ? property.ElementType : property.Type);
                    if (complexType is null || found.Contains(complexType))
                    {
                        continue;
                    }

                    found.Add(complexType);
                    pending.Enqueue(complexType.Properties.Where(ODataMcpCatalog.IsExposedProperty));
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

        #endregion

    }

}
