// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.OData.Mcp.Core.Constants;
using Microsoft.OData.Mcp.Core.Models;
using static Microsoft.OData.Mcp.Core.Constants.ODataMcpCatalogConstants;

namespace Microsoft.OData.Mcp.Core.Catalog
{

    /// <summary>
    /// Builds MCP resources and tools from a Core EDM. Only declared model members
    /// are serialized into catalogs, schemas, and completions.
    /// </summary>
    public sealed class ODataMcpCatalog
    {

        #region Fields

        /// <summary>
        /// JSON Schema for every <c>key</c> argument: how to write a single or composite key literal.
        /// </summary>
        internal const string KeySchema = """{"type":"string","description":"Key as text: ALFKI or 10248; strings are quoted for you. Composite: OrderID=10248,ProductID=11."}""";

        internal static readonly JsonSerializerOptions SchemaSerializerOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        internal readonly EdmModel _model;
        internal readonly ODataMcpCatalogOptions _options;
        internal readonly ConcurrentDictionary<string, EdmTypeShape> _shapes = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, ODataToolDescriptor> _toolsByName;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the resource descriptors.
        /// </summary>
        public IReadOnlyList<ODataResourceDescriptor> Resources { get; }

        /// <summary>
        /// Gets the resource templates.
        /// </summary>
        public IReadOnlyList<ODataResourceTemplateDescriptor> ResourceTemplates { get; }

        /// <summary>
        /// Gets the cached type shapes keyed by EDM full name. Empty when <see cref="ODataMcpCatalogOptions.IsDynamicModel"/> is <c>true</c>.
        /// </summary>
        public IReadOnlyDictionary<string, EdmTypeShape> Shapes => _shapes;

        /// <summary>
        /// Gets the tool descriptors (generic first, then named).
        /// </summary>
        public IReadOnlyList<ODataToolDescriptor> Tools { get; }

        /// <summary>
        /// Gets the identity stamped into <c>tools/list</c> cursors for this catalog instance.
        /// </summary>
        internal string ToolListStamp { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataMcpCatalog"/> class.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="options">Catalog options.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <see cref="ODataMcpCatalogOptions.RouteName"/> is null, empty, or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="ODataMcpCatalogOptions.ToolsPageSize"/> is not greater than zero.</exception>
        public ODataMcpCatalog(EdmModel model, ODataMcpCatalogOptions options)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentException.ThrowIfNullOrWhiteSpace(options.RouteName);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.ToolsPageSize, nameof(options.ToolsPageSize));

            _model = model;
            _options = options;
            ToolListStamp = Guid.NewGuid().ToString("N");

            var includedSets = ResolveIncludedSets();
            if (!options.IsDynamicModel)
            {
                FillShapeCache(includedSets);
            }

            Resources = BuildResources(includedSets);
            ResourceTemplates = BuildTemplates();
            Tools = BuildTools(includedSets);
            _toolsByName = new Dictionary<string, ODataToolDescriptor>(Tools.Count, StringComparer.Ordinal);
            foreach (var tool in Tools)
            {
                _toolsByName[tool.Name] = tool;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Completes entity set names for resource templates.
        /// </summary>
        /// <param name="prefix">The prefix typed so far. Empty means all declared sets.</param>
        /// <returns>
        /// Matching declared entity set names from the EDM. Never invents names.
        /// </returns>
        public IReadOnlyList<string> CompleteEntitySetNames(string prefix)
        {
            ArgumentNullException.ThrowIfNull(prefix);

            var names = (_model.EntityContainer?.EntitySets.Select(set => set.Name) ?? [])
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);
            var matches = prefix.Length == 0
                ? names
                : names.Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            var cap = _options.MaxCompletionValues > 0 ? _options.MaxCompletionValues : 50;

            return [.. matches.Take(cap)];
        }

        /// <summary>
        /// Gets the shape for an entity type, from the cache when the model is static.
        /// </summary>
        /// <param name="entityType">The entity type.</param>
        /// <returns>
        /// The cached shape, or a freshly built one when <see cref="ODataMcpCatalogOptions.IsDynamicModel"/> is <c>true</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entityType"/> is null.</exception>
        public EdmTypeShape GetShape(EdmEntityType entityType)
        {
            ArgumentNullException.ThrowIfNull(entityType);

            if (_options.IsDynamicModel)
            {
                return BuildShape(entityType, ResolveIncludedSets());
            }

            return _shapes.GetOrAdd(entityType.FullName, _ => BuildShape(entityType, ResolveIncludedSets()));
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds the shape for a type, pairing it with the first included set declared on that type.
        /// </summary>
        /// <param name="entityType">The entity type.</param>
        /// <param name="includedSets">The included entity sets.</param>
        /// <returns>
        /// The shape.
        /// </returns>
        internal EdmTypeShape BuildShape(EdmEntityType entityType, IReadOnlyList<EdmEntitySet> includedSets)
        {
            var set = includedSets.FirstOrDefault(candidate => ReferenceEquals(ResolveEntityType(candidate), entityType));

            return new EdmTypeShape(_model, entityType, set);
        }

        /// <summary>
        /// Computes a shape for every declared entity type and stores it in the cache.
        /// </summary>
        /// <param name="includedSets">The included entity sets.</param>
        internal void FillShapeCache(IReadOnlyList<EdmEntitySet> includedSets)
        {
            foreach (var entityType in _model.EntityTypes)
            {
                _shapes[entityType.FullName] = BuildShape(entityType, includedSets);
            }
        }

        /// <summary>
        /// Builds the named <c>create_*</c> input schema: declared properties with <c>required</c> = required-on-create.
        /// </summary>
        /// <param name="shape">The type shape.</param>
        /// <returns>
        /// JSON Schema text. <c>required</c> is omitted when nothing is required on create.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="shape"/> is null.</exception>
        internal string BuildCreateSchema(EdmTypeShape shape)
        {
            ArgumentNullException.ThrowIfNull(shape);

            var schema = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [ODataMcpCatalogConstants.Type] = JsonObject,
                [Properties] = BuildPropertySchema(shape)
            };

            if (shape.RequiredOnCreate.Count > 0)
            {
                schema[Required] = shape.RequiredOnCreate;
            }

            return JsonSerializer.Serialize(schema, SchemaSerializerOptions);
        }

        /// <summary>
        /// Builds the JSON Schema for an enumeration-typed value according to <see cref="ODataMcpCatalogOptions.EnumJsonFormat"/>.
        /// </summary>
        /// <param name="enumType">The enumeration type.</param>
        /// <returns>
        /// <c>{ "type": "string", "enum": [names] }</c> for <see cref="ODataEnumJsonFormat.String"/> and <see cref="ODataEnumJsonFormat.Auto"/>;
        /// <c>{ "type": "number", "enum": [values], "description": "Name=value, ..." }</c> for <see cref="ODataEnumJsonFormat.Integer"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="enumType"/> is null.</exception>
        internal Dictionary<string, object?> BuildEnumSchema(EdmEnumType enumType)
        {
            ArgumentNullException.ThrowIfNull(enumType);

            if (_options.EnumJsonFormat == ODataEnumJsonFormat.Integer)
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [ODataMcpCatalogConstants.Type] = JsonNumber,
                    [EnumKeyword] =enumType.Members.Select(member => (object)member.Value).ToList(),
                    [Description] = string.Join(", ", enumType.Members.Select(member => $"{member.Name}={member.Value}"))
                };
            }

            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [ODataMcpCatalogConstants.Type] = JsonString,
                [EnumKeyword] =enumType.Members.Select(member => (object?)member.Name).ToList()
            };
        }

        /// <summary>
        /// Builds the <c>key</c> schema for a named <c>get_*</c> tool, naming the type's actual key properties.
        /// </summary>
        /// <param name="entityType">The entity type the tool binds to.</param>
        /// <returns>
        /// A string schema whose description is <c>{Key} as text; strings are quoted for you.</c> for a single key,
        /// or <c>Name=value pairs: K1=...,K2=...; strings are quoted for you.</c> for a composite key. Falls back to
        /// <see cref="KeySchema"/> when the type declares no key.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entityType"/> is null.</exception>
        /// <remarks>
        /// Named tools repeat once per entity set, so this stays shorter than the generic <see cref="KeySchema"/>
        /// while saying more: the calling model sees the key names it must use.
        /// </remarks>
        internal static string BuildNamedKeySchema(EdmEntityType entityType)
        {
            ArgumentNullException.ThrowIfNull(entityType);

            return entityType.Key.Count == 0
                ? KeySchema
                : JsonSerializer.Serialize(BuildNamedKeySchemaObject(entityType), SchemaSerializerOptions);
        }

        /// <summary>
        /// Builds the <c>key</c> schema object for a named tool.
        /// </summary>
        /// <param name="entityType">The entity type the tool binds to, or <c>null</c>.</param>
        /// <returns>
        /// A string schema whose description names the key properties when they are declared.
        /// </returns>
        internal static Dictionary<string, object?> BuildNamedKeySchemaObject(EdmEntityType? entityType)
        {
            if (entityType is null || entityType.Key.Count == 0)
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [ODataMcpCatalogConstants.Type] = JsonString,
                    [Description] = "Key as text: ALFKI or 10248; strings are quoted for you. Composite: OrderID=10248,ProductID=11."
                };
            }

            var description = entityType.Key.Count == 1
                ? $"{entityType.Key[0]} as text; strings are quoted for you."
                : $"Name=value pairs: {string.Join(",", entityType.Key.Select(name => $"{name}=..."))}; strings are quoted for you.";

            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [ODataMcpCatalogConstants.Type] = JsonString,
                [Description] = description
            };
        }

        /// <summary>
        /// Builds the JSON Schema for one declared property: type (with <c>null</c> when nullable), enum members,
        /// <c>maxLength</c> when at most 16, and the CSDL description when there is one.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <returns>
        /// The property schema.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="property"/> is null.</exception>
        /// <remarks>
        /// Collections become <c>array</c> with <c>items</c>; complex types become <c>object</c>. The member name is never
        /// used as a description.
        /// </remarks>
        internal Dictionary<string, object?> BuildPropertyJsonSchema(EdmProperty property)
        {
            ArgumentNullException.ThrowIfNull(property);

            var elementType = property.IsCollection ? property.ElementType : property.Type;
            var enumType = _model.GetEnumType(elementType);
            var element = enumType is not null
                ? BuildEnumSchema(enumType)
                : new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [ODataMcpCatalogConstants.Type] = _model.GetComplexType(elementType) is not null ? JsonObject : MapJsonType(elementType)
                };

            var schema = property.IsCollection
                ? new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [ODataMcpCatalogConstants.Type] = JsonArray,
                    [Items] = element
                }
                : element;

            if (property.Nullable)
            {
                schema[ODataMcpCatalogConstants.Type] = new[] { (string)schema[ODataMcpCatalogConstants.Type]!, JsonNull };
                if (!property.IsCollection && schema.TryGetValue(EnumKeyword, out var members) && members is List<object?> names)
                {
                    names.Add(null);
                }
            }

            if (!property.IsCollection && property.MaxLength is > 0 and <= EdmTypeShape.InlineMaxLength && property.Type == EdmString)
            {
                schema[MaxLength] = property.MaxLength;
            }

            var documentation = EdmTypeShape.DocumentationFor(property.Description, property.LongDescription, property.Name);
            if (documentation is not null && !schema.ContainsKey(Description))
            {
                schema[Description] = documentation;
            }

            return schema;
        }

        /// <summary>
        /// Builds the JSON Schema property map for a type's exposed properties.
        /// </summary>
        /// <param name="shape">The type shape.</param>
        /// <returns>
        /// Property name to property schema, in declaration order.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="shape"/> is null.</exception>
        internal Dictionary<string, object?> BuildPropertySchema(EdmTypeShape shape)
        {
            ArgumentNullException.ThrowIfNull(shape);

            var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var property in shape.ExposedProperties)
            {
                properties[property.Name] = BuildPropertyJsonSchema(property);
            }

            return properties;
        }

        /// <summary>
        /// Builds the named <c>update_*</c> input schema: <c>key</c> plus the same property map, with only <c>key</c> required.
        /// </summary>
        /// <param name="shape">The type shape.</param>
        /// <returns>
        /// JSON Schema text.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="shape"/> is null.</exception>
        internal string BuildUpdateSchema(EdmTypeShape shape)
        {
            ArgumentNullException.ThrowIfNull(shape);

            var properties = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [Key] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [ODataMcpCatalogConstants.Type] = JsonString
                }
            };

            foreach (var pair in BuildPropertySchema(shape))
            {
                properties[pair.Key] = pair.Value;
            }

            var schema = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [ODataMcpCatalogConstants.Type] = JsonObject,
                [Properties] = properties,
                [Required] = new[] { Key }
            };

            return JsonSerializer.Serialize(schema, SchemaSerializerOptions);
        }

        /// <summary>
        /// Converts an EDM short name into a catalog tool-name segment.
        /// </summary>
        /// <param name="value">The declared set, type, or operation name.</param>
        /// <returns>
        /// <paramref name="value"/> folded to lowercase. Underscores and hyphens already in the name are kept;
        /// none are inserted. <c>OrderDetails</c> and <c>Order_Details</c> stay distinct.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null or whitespace.</exception>
        /// <remarks>
        /// Named tools are <c>{prefix}{segment}</c>. The model already learned the EDM identifier from
        /// <c>odata_describe_type</c>; inserting separators would invent a dialect and collapse names that
        /// differ only by PascalCase versus underscores.
        /// </remarks>
        internal static string ToToolName(string value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            return value.ToLowerInvariant();
        }

        /// <summary>
        /// Finds a catalog tool by exact name.
        /// </summary>
        /// <param name="name">The tool name.</param>
        /// <param name="tool">The descriptor when found.</param>
        /// <returns>
        /// <c>true</c> when the catalog advertises <paramref name="name"/>.
        /// </returns>
        internal bool TryGetTool(string name, out ODataToolDescriptor? tool)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            return _toolsByName.TryGetValue(name, out tool);
        }

        /// <summary>
        /// Returns whether a structural property should appear in generated schemas.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <returns>
        /// <c>true</c> when the property is declared and not binary/stream.
        /// </returns>
        internal static bool IsExposedProperty(EdmProperty property)
        {
            ArgumentNullException.ThrowIfNull(property);

            return IsExposedType(property.Type);
        }

        /// <summary>
        /// Returns whether an EDM type should appear in generated schemas.
        /// </summary>
        /// <param name="type">The EDM type name.</param>
        /// <returns>
        /// <c>true</c> when the type is not binary or stream.
        /// </returns>
        internal static bool IsExposedType(string? type)
        {
            var value = type ?? string.Empty;

            return !value.Contains(EdmBinary, StringComparison.OrdinalIgnoreCase)
                && !value.Contains(EdmStream, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Maps an EDM type name to a JSON Schema type.
        /// </summary>
        /// <param name="edmType">The EDM type.</param>
        /// <returns>
        /// A JSON Schema type string.
        /// </returns>
        internal static string MapJsonType(string edmType)
        {
            if (string.IsNullOrWhiteSpace(edmType))
            {
                return JsonString;
            }

            if (edmType.Contains(EdmInt16, StringComparison.OrdinalIgnoreCase)
                || edmType.Contains(EdmInt32, StringComparison.OrdinalIgnoreCase)
                || edmType.Contains(EdmInt64, StringComparison.OrdinalIgnoreCase)
                || edmType.Contains(EdmByte, StringComparison.OrdinalIgnoreCase)
                || edmType.Contains(EdmSByte, StringComparison.OrdinalIgnoreCase)
                || edmType.Contains(EdmDecimal, StringComparison.OrdinalIgnoreCase)
                || edmType.Contains(EdmDouble, StringComparison.OrdinalIgnoreCase)
                || edmType.Contains(EdmSingle, StringComparison.OrdinalIgnoreCase))
            {
                return JsonNumber;
            }

            if (edmType.Contains(EdmBoolean, StringComparison.OrdinalIgnoreCase))
            {
                return JsonBoolean;
            }

            return JsonString;
        }

        /// <summary>
        /// Builds resource descriptors from declared entity sets.
        /// </summary>
        /// <param name="sets">The included entity sets.</param>
        /// <returns>
        /// Resource descriptors.
        /// </returns>
        internal IReadOnlyList<ODataResourceDescriptor> BuildResources(IReadOnlyList<EdmEntitySet> sets)
        {
            var resources = new List<ODataResourceDescriptor>
            {
                new()
                {
                    Description = "OData CSDL metadata document.",
                    MimeType = ApplicationXml,
                    Name = Metadata,
                    Title = "Metadata",
                    Uri = $"{UriSchemePrefix}{_options.RouteName}/{Metadata}"
                }
            };

            var cap = _options.MaxResources > 0 ? _options.MaxResources : 50;
            foreach (var set in OrderSetsForNamedTools(sets))
            {
                if (resources.Count >= cap)
                {
                    break;
                }

                var type = ResolveEntityType(set);
                resources.Add(new ODataResourceDescriptor
                {
                    Description = DescribeSet(set, type),
                    MimeType = ApplicationJson,
                    Name = set.Name,
                    ReadContents = BuildTypeCard(set, type),
                    Title = ResolveTitle(set.Name, set.Description, type?.Description),
                    Uri = $"{UriSchemePrefix}{_options.RouteName}/{set.Name}"
                });
            }

            return resources;
        }

        /// <summary>
        /// Builds resource templates for OData paths.
        /// </summary>
        /// <returns>
        /// Resource templates.
        /// </returns>
        internal IReadOnlyList<ODataResourceTemplateDescriptor> BuildTemplates()
        {
            return
            [
                new ODataResourceTemplateDescriptor
                {
                    Description = "An entity set in the OData model.",
                    Name = EntitySet,
                    Title = "Entity set",
                    UriTemplate = $"{UriSchemePrefix}{_options.RouteName}/{{{EntitySet}}}"
                },
                new ODataResourceTemplateDescriptor
                {
                    Description = "An entity addressed by key.",
                    Name = EntityByKey,
                    Title = "Entity by key",
                    UriTemplate = $"{UriSchemePrefix}{_options.RouteName}/{{{EntitySet}}}({{{Key}}})"
                }
            ];
        }

        /// <summary>
        /// Builds generic tools, then unbound named operations, then a complete named family and bound operations for every included entity set.
        /// </summary>
        /// <param name="sets">The included entity sets.</param>
        /// <returns>
        /// Tool descriptors. Generic tools come first, then unbound <c>function_*</c> / <c>action_*</c>,
        /// then per-set CRUD and bound operations in include-list order and then alphabetical.
        /// </returns>
        internal IReadOnlyList<ODataToolDescriptor> BuildTools(IReadOnlyList<EdmEntitySet> sets)
        {
            var tools = new List<ODataToolDescriptor>(BuildGenericTools());
            var usedNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tool in tools)
            {
                usedNames.Add(tool.Name);
            }

            var operations = IndexCatalogOperations();
            AddNamedOperations(tools, usedNames, operations.UnboundFunctions, set: null, type: null, setsByType: null);
            AddNamedOperations(tools, usedNames, operations.UnboundActions, set: null, type: null, setsByType: null);

            var orderedSets = OrderSetsForNamedTools(sets);
            var setsByType = CountIncludedSetsByType(orderedSets);
            foreach (var set in orderedSets)
            {
                foreach (var tool in BuildNamedFamily(set))
                {
                    if (usedNames.Add(tool.Name))
                    {
                        tools.Add(tool);
                    }
                }

                AddBoundOperationTools(tools, usedNames, set, setsByType, operations);
            }

            return tools;
        }

        /// <summary>
        /// Builds the compact JSON type card for an entity set, the same shape <c>odata_describe_type</c> returns for <c>format=json</c>.
        /// </summary>
        /// <param name="set">The entity set.</param>
        /// <param name="entityType">The entity type, if resolved.</param>
        /// <returns>
        /// JSON text. When the set's type is not declared, a card that names only the set and its declared type.
        /// </returns>
        internal string BuildTypeCard(EdmEntitySet set, EdmEntityType? entityType)
        {
            if (entityType is null)
            {
                var card = new Dictionary<string, object?>
                {
                    [ShortTypeName(set.EntityType)] = new Dictionary<string, object?>
                    {
                        [Set] = set.Name,
                        [EntityType] = set.EntityType
                    }
                };

                return JsonSerializer.Serialize(card, SchemaSerializerOptions);
            }

            var shape = GetShape(entityType);
            if (!ReferenceEquals(shape.EntitySet, set))
            {
                shape = new EdmTypeShape(_model, entityType, set);
            }

            return shape.Json;
        }

        /// <summary>
        /// Returns the part of a qualified type name after the last dot.
        /// </summary>
        /// <param name="qualifiedName">The qualified name.</param>
        /// <returns>
        /// The short name.
        /// </returns>
        internal static string ShortTypeName(string qualifiedName)
        {
            return EdmTypeShape.ShortName(qualifiedName);
        }

        /// <summary>
        /// Builds the generic OData tools.
        /// </summary>
        /// <returns>
        /// Generic tool descriptors.
        /// </returns>
        internal List<ODataToolDescriptor> BuildGenericTools()
        {
            var querySchema = """
                {"type":"object","properties":{"entitySet":{"type":"string"},"filter":{"type":"string"},"select":{"type":"string"},"orderby":{"type":"string"},"expand":{"type":"string"},"top":{"type":"number"},"skip":{"type":"number"},"count":{"type":"boolean"}},"required":["entitySet"]}
                """;

            return
            [
                new ODataToolDescriptor
                {
                    Description = "Names, types, and keys of declared entity sets.",
                    InputSchema = """{"type":"object","properties":{},"additionalProperties":false}""",
                    Name = ODataListEntitySets,
                    OutputSchema = """{"type":"object","properties":{"entitySets":{"type":"array","items":{"type":"object","properties":{"name":{"type":"string"},"entityType":{"type":"string"},"keys":{"type":"array","items":{"type":"string"}},"description":{"type":"string"}},"required":["name","entityType","keys"]}}},"required":["entitySets"]}""",
                    ReadOnlyHint = true,
                    IdempotentHint = true,
                    Title = "List entity sets"
                },
                new ODataToolDescriptor
                {
                    Description = "Declared properties, keys, navigations, bound operations, and enums for a set or an entity, complex, or enum type. format=text (default) or json. No ? = required on create; Name?: = optional; -> = navigation; [] = collection. // key, store-generated = omit on create; plain // key = send it unless the service generates it.",
                    InputSchema = """{"type":"object","properties":{"name":{"type":"string","description":"Entity set or type name declared in the model."},"format":{"type":"string","enum":["text","json"]}},"required":["name"]}""",
                    Name = ODataDescribeType,
                    ReadOnlyHint = true,
                    IdempotentHint = true,
                    Title = "Describe type"
                },
                new ODataToolDescriptor
                {
                    Description = "summary (default): sets, keys, navigations, then unbound operations. complete: every type with properties, enums, and bound operations, then unbound operations. Prefer complete instead of calling odata_describe_type once per type. Pass sets to scope a large model. format=text (default), json, or mermaid. Do not read $metadata to explore.",
                    InputSchema = """{"type":"object","properties":{"detail":{"type":"string","enum":["summary","complete"]},"format":{"type":"string","enum":["text","json","mermaid"]},"sets":{"type":"array","items":{"type":"string"},"description":"Entity set names to include. Omit for every included set."}}}""",
                    Name = ODataDescribeModel,
                    ReadOnlyHint = true,
                    IdempotentHint = true,
                    Title = "Describe model"
                },
                new ODataToolDescriptor
                {
                    Description = "Queries an entity set. Parameter names do not include $; the executor adds $filter, $select, $orderby, $expand, $top, $skip, and $count.",
                    InputSchema = querySchema,
                    Name = ODataQuery,
                    ReadOnlyHint = true,
                    IdempotentHint = true,
                    Title = "Query entity set"
                },
                new ODataToolDescriptor
                {
                    Description = "Gets an entity by key. Optional select and expand shape the result.",
                    InputSchema = $$$"""{"type":"object","properties":{"entitySet":{"type":"string"},"key":{{{KeySchema}}},"select":{"type":"string"},"expand":{"type":"string"}},"required":["entitySet","key"]}""",
                    Name = ODataGet,
                    ReadOnlyHint = true,
                    IdempotentHint = true,
                    Title = "Get entity"
                },
                new ODataToolDescriptor
                {
                    Description = "JSON object in body. Include every property the type lists as required on create (odata_describe_type). Client-assigned keys are required; omit store-generated keys.",
                    InputSchema = """{"type":"object","properties":{"entitySet":{"type":"string"},"body":{"type":"object","description":"The entity as a JSON object, not a string."}},"required":["entitySet","body"]}""",
                    Name = ODataCreate,
                    Title = "Create entity"
                },
                new ODataToolDescriptor
                {
                    Description = "PATCH object in body. Send only fields to change. Omitted fields keep their values. JSON null clears an optional property; never send it for a required one.",
                    IdempotentHint = true,
                    InputSchema = $$$"""{"type":"object","properties":{"entitySet":{"type":"string"},"key":{{{KeySchema}}},"body":{"type":"object","description":"Changed properties as a JSON object, not a string."}},"required":["entitySet","key","body"]}""",
                    Name = ODataUpdate,
                    Title = "Update entity"
                },
                new ODataToolDescriptor
                {
                    Description = "Deletes an entity by key.",
                    DestructiveHint = true,
                    IdempotentHint = true,
                    InputSchema = $$$"""{"type":"object","properties":{"entitySet":{"type":"string"},"key":{{{KeySchema}}}},"required":["entitySet","key"]}""",
                    Name = ODataDelete,
                    Title = "Delete entity"
                },
                new ODataToolDescriptor
                {
                    Description = "Follows a navigation property from a key. Accepts the same query options as odata_query.",
                    InputSchema = $$$"""{"type":"object","properties":{"entitySet":{"type":"string"},"key":{{{KeySchema}}},"navigation":{"type":"string"},"filter":{"type":"string"},"select":{"type":"string"},"orderby":{"type":"string"},"expand":{"type":"string"},"top":{"type":"number"},"skip":{"type":"number"},"count":{"type":"boolean"}},"required":["entitySet","key","navigation"]}""",
                    Name = ODataNavigate,
                    ReadOnlyHint = true,
                    IdempotentHint = true,
                    Title = "Navigate"
                },
                new ODataToolDescriptor
                {
                    Description = "Unbound operations on the service.",
                    InputSchema = """{"type":"object","properties":{},"additionalProperties":false}""",
                    Name = ODataListOperations,
                    OutputSchema = """{"type":"object","properties":{"operations":{"type":"object","additionalProperties":{"type":"string"}}}}""",
                    ReadOnlyHint = true,
                    IdempotentHint = true,
                    Title = "List operations"
                },
                new ODataToolDescriptor
                {
                    Description = "Call a declared operation by name as listed on odata_describe_type (bound) or odata_list_operations (unbound). Put every argument in parameters using the declared parameter names; do not stringify; do not add extra top-level fields; do not wrap under the operation name. Instance-bound (the default for bound): also entitySet and key. Collection-bound (// collection on the listing): entitySet only. Unbound: neither. The server sends GET or POST as declared — do not pass a method. // writes means it mutates. If arguments are wrong the tool errors with the signature; do not guess a different payload shape.",
                    InputSchema = $$$"""{"type":"object","properties":{"name":{"type":"string"},"parameters":{"type":"object","description":"Operation arguments keyed by the declared parameter names. Omit when the operation takes none."},"entitySet":{"type":"string"},"key":{{{KeySchema}}}},"required":["name"],"additionalProperties":false}""",
                    Name = ODataCall,
                    Title = "Call operation"
                }
            ];
        }

        /// <summary>
        /// Builds the trailing reminder on a named <c>list_*</c> tool: no <c>$</c> on parameter names, plus the
        /// <c>$filter</c> literal pattern for every enumeration the type exposes.
        /// </summary>
        /// <param name="shape">The shape of the listed type.</param>
        /// <returns>
        /// <c>Query parameter names do not include $.</c> alone when the type has no enum properties; otherwise that
        /// sentence followed by <c>Filter enums as NS.Enum'{value}', ...</c>.
        /// </returns>
        /// <remarks>
        /// A model steered toward named tools never calls <c>odata_describe_type</c>, so the enum filter hint that
        /// heads the type shape has to travel with the list tool as well.
        /// </remarks>
        internal string BuildListSuffix(EdmTypeShape shape)
        {
            ArgumentNullException.ThrowIfNull(shape);

            const string noDollar = "Query parameter names do not include $.";
            var literals = EdmTypeShape.RenderEnumFilterLiterals(_model, shape.ExposedProperties);
            if (literals.Count == 0)
            {
                return noDollar;
            }

            return $"{noDollar} Filter enums as {string.Join(", ", literals)}.";
        }

        /// <summary>
        /// Builds the named CRUD family for one entity set.
        /// </summary>
        /// <param name="set">The entity set.</param>
        /// <returns>
        /// Named tools. Empty when the type cannot be resolved.
        /// </returns>
        internal List<ODataToolDescriptor> BuildNamedFamily(EdmEntitySet set)
        {
            var type = ResolveEntityType(set);
            if (type is null)
            {
                return [];
            }

            var setName = ToToolName(set.Name);
            var singular = ToToolName(type.Name);
            var shape = GetShape(type);
            if (!ReferenceEquals(shape.EntitySet, set))
            {
                shape = new EdmTypeShape(_model, type, set);
            }

            var documented = EdmDocumentation.First(set.Description, set.LongDescription, type.Description, type.LongDescription);
            var family = new List<ODataToolDescriptor>
            {
                new()
                {
                    Description = ComposeToolDescription(DescribeSet(set, type), documented, BuildListSuffix(shape)),
                    EntitySetName = set.Name,
                    InputSchema = """{"type":"object","properties":{"filter":{"type":"string"},"select":{"type":"string"},"orderby":{"type":"string"},"expand":{"type":"string"},"top":{"type":"number"},"skip":{"type":"number"},"count":{"type":"boolean"}}}""",
                    Name = $"{ListPrefix}{setName}",
                    ReadOnlyHint = true,
                    IdempotentHint = true,
                    Title = ResolveTitle($"List {set.Name}", set.Description, type.Description)
                },
                new()
                {
                    Description = ComposeToolDescription($"Gets a {type.Name} by key.", documented),
                    EntitySetName = set.Name,
                    InputSchema = $$$"""{"type":"object","properties":{"key":{{{BuildNamedKeySchema(type)}}},"select":{"type":"string"},"expand":{"type":"string"}},"required":["key"]}""",
                    Name = $"{GetPrefix}{singular}",
                    ReadOnlyHint = true,
                    IdempotentHint = true,
                    Title = ResolveTitle($"Get {type.Name}", type.Description, set.Description)
                }
            };

            if (_options.IncludeCreate)
            {
                family.Add(new ODataToolDescriptor
                {
                    Description = ComposeToolDescription($"Creates a {type.Name}.", documented),
                    EntitySetName = set.Name,
                    InputSchema = BuildCreateSchema(shape),
                    Name = $"{CreatePrefix}{singular}",
                    Title = ResolveTitle($"Create {type.Name}", type.Description, set.Description)
                });
            }

            if (_options.IncludeUpdate)
            {
                family.Add(new ODataToolDescriptor
                {
                    Description = ComposeToolDescription($"PATCH a {type.Name}. Send only fields to change; omit to keep. Do not send JSON null for required properties.", documented),
                    EntitySetName = set.Name,
                    IdempotentHint = true,
                    InputSchema = BuildUpdateSchema(shape),
                    Name = $"{UpdatePrefix}{singular}",
                    Title = ResolveTitle($"Update {type.Name}", type.Description, set.Description)
                });
            }

            if (_options.IncludeDelete)
            {
                family.Add(new ODataToolDescriptor
                {
                    Description = ComposeToolDescription($"Deletes a {type.Name}.", documented),
                    DestructiveHint = true,
                    EntitySetName = set.Name,
                    IdempotentHint = true,
                    InputSchema = """{"type":"object","properties":{"key":{"type":"string"}},"required":["key"]}""",
                    Name = $"{DeletePrefix}{singular}",
                    Title = ResolveTitle($"Delete {type.Name}", type.Description, set.Description)
                });
            }

            return family;
        }

        /// <summary>
        /// Adds instance-bound then collection-bound named operation tools for one entity set.
        /// </summary>
        /// <param name="tools">The catalog tools being built.</param>
        /// <param name="usedNames">Tool names already advertised.</param>
        /// <param name="set">The entity set.</param>
        /// <param name="setsByType">How many included sets share each entity type full name.</param>
        /// <param name="operations">First-overload operations grouped by binding.</param>
        internal void AddBoundOperationTools(
            List<ODataToolDescriptor> tools,
            HashSet<string> usedNames,
            EdmEntitySet set,
            IReadOnlyDictionary<string, int> setsByType,
            CatalogOperationIndex operations)
        {
            ArgumentNullException.ThrowIfNull(tools);
            ArgumentNullException.ThrowIfNull(usedNames);
            ArgumentNullException.ThrowIfNull(set);
            ArgumentNullException.ThrowIfNull(setsByType);
            ArgumentNullException.ThrowIfNull(operations);

            if (operations.InstanceFunctionsByType.Count == 0
                && operations.InstanceActionsByType.Count == 0
                && operations.CollectionFunctionsByType.Count == 0
                && operations.CollectionActionsByType.Count == 0)
            {
                return;
            }

            var type = ResolveEntityType(set);
            if (type is null)
            {
                return;
            }

            var bindingTypes = EdmTypeShape.ResolveBindingTypeNames(_model, type);
            AddNamedOperations(tools, usedNames, CollectBoundOperations(operations.InstanceFunctionsByType, bindingTypes), set, type, setsByType);
            AddNamedOperations(tools, usedNames, CollectBoundOperations(operations.InstanceActionsByType, bindingTypes), set, type, setsByType);
            AddNamedOperations(tools, usedNames, CollectBoundOperations(operations.CollectionFunctionsByType, bindingTypes), set, type, setsByType);
            AddNamedOperations(tools, usedNames, CollectBoundOperations(operations.CollectionActionsByType, bindingTypes), set, type, setsByType);
        }

        /// <summary>
        /// Names and appends operation tools, sorted alphabetically.
        /// </summary>
        /// <param name="tools">The catalog tools being built.</param>
        /// <param name="usedNames">Tool names already advertised.</param>
        /// <param name="operations">Operations to emit.</param>
        /// <param name="set">The baked entity set for bound tools, or <c>null</c> when unbound.</param>
        /// <param name="type">The binding entity type for instance-bound tools.</param>
        /// <param name="setsByType">Included-set counts by entity type full name, or <c>null</c> when unbound.</param>
        internal void AddNamedOperations(
            List<ODataToolDescriptor> tools,
            HashSet<string> usedNames,
            List<CatalogOperation> operations,
            EdmEntitySet? set,
            EdmEntityType? type,
            IReadOnlyDictionary<string, int>? setsByType)
        {
            ArgumentNullException.ThrowIfNull(tools);
            ArgumentNullException.ThrowIfNull(usedNames);
            ArgumentNullException.ThrowIfNull(operations);

            if (operations.Count == 0)
            {
                return;
            }

            var batch = new List<ODataToolDescriptor>(operations.Count);
            foreach (var operation in operations)
            {
                var name = set is null
                    ? $"{(operation.IsAction ? ActionPrefix : FunctionPrefix)}{ToToolName(operation.Name)}"
                    : BoundOperationToolName(operation, set, setsByType!, usedNames);
                if (name is null || !usedNames.Add(name))
                {
                    continue;
                }

                batch.Add(BuildOperationTool(name, operation, set, type));
            }

            batch.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));
            tools.AddRange(batch);
        }

        /// <summary>
        /// Chooses a bound operation tool name: type stem when that type has one included set, otherwise the set stem.
        /// Falls back to the set stem when the preferred name is already advertised.
        /// </summary>
        /// <param name="operation">The operation.</param>
        /// <param name="set">The entity set.</param>
        /// <param name="setsByType">Included-set counts by entity type full name.</param>
        /// <param name="taken">Names already claimed in this catalog.</param>
        /// <returns>
        /// The tool name, or <c>null</c> when both stems are taken.
        /// </returns>
        internal static string? BoundOperationToolName(
            CatalogOperation operation,
            EdmEntitySet set,
            IReadOnlyDictionary<string, int> setsByType,
            HashSet<string> taken)
        {
            ArgumentNullException.ThrowIfNull(set);
            ArgumentNullException.ThrowIfNull(setsByType);
            ArgumentNullException.ThrowIfNull(taken);

            var prefix = operation.IsAction ? ActionPrefix : FunctionPrefix;
            var operationStem = ToToolName(operation.Name);
            var setName = $"{prefix}{ToToolName(set.Name)}_{operationStem}";
            if (operation.CollectionBound || string.IsNullOrWhiteSpace(operation.BindingType))
            {
                return taken.Contains(setName) ? null : setName;
            }

            var preferred = setsByType.GetValueOrDefault(operation.BindingType) > 1
                ? setName
                : $"{prefix}{ToToolName(EdmTypeShape.ShortName(operation.BindingType))}_{operationStem}";
            if (!taken.Contains(preferred))
            {
                return preferred;
            }

            return taken.Contains(setName) ? null : setName;
        }

        /// <summary>
        /// Builds the flattened JSON Schema for a named operation tool.
        /// </summary>
        /// <param name="parameters">Declared parameters including the binding parameter.</param>
        /// <param name="isBound">Whether the operation is bound.</param>
        /// <param name="collectionBound">Whether the binding parameter is a collection.</param>
        /// <param name="entityType">The binding entity type when instance-bound.</param>
        /// <returns>
        /// JSON Schema text with <c>additionalProperties: false</c>.
        /// </returns>
        internal string BuildOperationInputSchema(IReadOnlyList<EdmParameter> parameters, bool isBound, bool collectionBound, EdmEntityType? entityType)
        {
            ArgumentNullException.ThrowIfNull(parameters);

            var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
            var required = new List<string>();
            if (isBound && !collectionBound)
            {
                properties[Key] = BuildNamedKeySchemaObject(entityType);
                required.Add(Key);
            }

            for (var index = isBound ? 1 : 0; index < parameters.Count; index++)
            {
                var parameter = parameters[index];
                if (!IsExposedType(parameter.Type))
                {
                    continue;
                }

                properties[parameter.Name] = BuildParameterJsonSchema(parameter);
                if (!parameter.Nullable)
                {
                    required.Add(parameter.Name);
                }
            }

            var schema = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [ODataMcpCatalogConstants.Type] = JsonObject,
                [Properties] = properties,
                [AdditionalProperties] = false
            };
            if (required.Count > 0)
            {
                schema[Required] = required;
            }

            return JsonSerializer.Serialize(schema, SchemaSerializerOptions);
        }

        /// <summary>
        /// Builds <c>outputSchema</c> for a named operation return type.
        /// </summary>
        /// <param name="returnType">The EDM return type, or <c>null</c> when the operation returns nothing.</param>
        /// <returns>
        /// JSON Schema text, or <c>null</c> when there is no return type.
        /// </returns>
        internal string? BuildOperationOutputSchema(string? returnType)
        {
            if (string.IsNullOrWhiteSpace(returnType))
            {
                return null;
            }

            var collection = EdmTypeShape.IsCollectionBound(returnType);
            var element = EdmTypeShape.UnwrapCollectionType(returnType)!;
            var structured = BuildStructuredObjectSchema(element);
            Dictionary<string, object?> schema;
            if (structured is not null)
            {
                schema = collection
                    ? new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        [ODataMcpCatalogConstants.Type] = JsonArray,
                        [Items] = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            [ODataMcpCatalogConstants.Type] = JsonObject,
                            [Properties] = structured
                        }
                    }
                    : new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        [ODataMcpCatalogConstants.Type] = JsonObject,
                        [Properties] = structured
                    };
            }
            else
            {
                var item = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [ODataMcpCatalogConstants.Type] = MapJsonType(element)
                };
                var value = collection
                    ? new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        [ODataMcpCatalogConstants.Type] = JsonArray,
                        [Items] = item
                    }
                    : item;
                schema = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [ODataMcpCatalogConstants.Type] = JsonObject,
                    [Properties] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["value"] = value
                    }
                };
            }

            return JsonSerializer.Serialize(schema, SchemaSerializerOptions);
        }

        /// <summary>
        /// Builds one named action or function tool.
        /// </summary>
        /// <param name="name">The catalog tool name.</param>
        /// <param name="operation">The first-overload operation.</param>
        /// <param name="set">The baked entity set for bound tools.</param>
        /// <param name="type">The binding entity type for instance-bound tools.</param>
        /// <returns>
        /// The tool descriptor.
        /// </returns>
        internal ODataToolDescriptor BuildOperationTool(string name, CatalogOperation operation, EdmEntitySet? set, EdmEntityType? type)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            var verb = operation.IsAction ? "POST" : "GET";
            string sentence;
            if (!operation.IsBound)
            {
                sentence = $"{verb} {operation.Name}.";
            }
            else if (operation.CollectionBound)
            {
                sentence = $"{verb} {set!.Name}/{operation.Name}.";
            }
            else
            {
                sentence = $"{verb} {set!.Name}({{key}})/{operation.Name}.";
            }

            var documented = EdmDocumentation.First(operation.Description, operation.LongDescription);
            var fallbackTitle = operation.IsBound && type is not null ? $"{operation.Name} a {type.Name}" : operation.Name;

            return new ODataToolDescriptor
            {
                BindingParameterType = operation.BindingParameterType,
                Description = ComposeToolDescription(sentence, documented),
                DestructiveHint = operation.IsAction,
                EntitySetName = set?.Name,
                IdempotentHint = !operation.IsAction,
                InputSchema = BuildOperationInputSchema(operation.Parameters, operation.IsBound, operation.CollectionBound, type),
                Name = name,
                OpenWorldHint = true,
                OperationKind = operation.IsAction ? ODataOperationKind.Action : ODataOperationKind.Function,
                OperationName = operation.Name,
                OutputSchema = BuildOperationOutputSchema(operation.ReturnType),
                ReadOnlyHint = !operation.IsAction,
                Title = ResolveTitle(fallbackTitle, operation.Description)
            };
        }

        /// <summary>
        /// Builds JSON Schema for one non-binding operation parameter.
        /// </summary>
        /// <param name="parameter">The parameter.</param>
        /// <returns>
        /// The parameter schema. Complex and collection types name the EDM type for <c>odata_describe_type</c>.
        /// </returns>
        internal Dictionary<string, object?> BuildParameterJsonSchema(EdmParameter parameter)
        {
            ArgumentNullException.ThrowIfNull(parameter);

            var property = new EdmProperty
            {
                Description = parameter.Description,
                LongDescription = parameter.LongDescription,
                MaxLength = parameter.MaxLength,
                Name = parameter.Name,
                Nullable = parameter.Nullable,
                Type = parameter.Type
            };
            var schema = BuildPropertyJsonSchema(property);
            var element = EdmTypeShape.UnwrapCollectionType(parameter.Type);
            if (element is not null && (_model.GetComplexType(element) is not null || _model.GetEntityType(element) is not null))
            {
                schema[Description] = $"{element}. Use odata_describe_type.";
            }

            return schema;
        }

        /// <summary>
        /// Builds the object property map for a structured return type.
        /// </summary>
        /// <param name="edmType">The non-collection EDM type name.</param>
        /// <returns>
        /// Property schemas, or <c>null</c> when the type is primitive or enum.
        /// </returns>
        internal Dictionary<string, object?>? BuildStructuredObjectSchema(string edmType)
        {
            var entity = _model.GetEntityType(edmType);
            if (entity is not null)
            {
                return BuildPropertySchema(new EdmTypeShape(_model, entity, null));
            }

            var complex = _model.GetComplexType(edmType);
            if (complex is null)
            {
                return null;
            }

            var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var property in complex.Properties)
            {
                if (!IsExposedProperty(property))
                {
                    continue;
                }

                properties[property.Name] = BuildPropertyJsonSchema(property);
            }

            return properties;
        }

        /// <summary>
        /// Collects first-overload bound operations for a type chain.
        /// </summary>
        /// <param name="byType">Operations keyed by binding type full name.</param>
        /// <param name="bindingTypes">The set's type and its base types.</param>
        /// <returns>
        /// Matching operations in type-chain then declaration order.
        /// </returns>
        internal static List<CatalogOperation> CollectBoundOperations(
            Dictionary<string, List<CatalogOperation>> byType,
            IReadOnlyCollection<string> bindingTypes)
        {
            ArgumentNullException.ThrowIfNull(byType);
            ArgumentNullException.ThrowIfNull(bindingTypes);

            var operations = new List<CatalogOperation>();
            foreach (var bindingType in bindingTypes)
            {
                if (byType.TryGetValue(bindingType, out var matches))
                {
                    operations.AddRange(matches);
                }
            }

            return operations;
        }

        /// <summary>
        /// Counts included entity sets per entity type full name.
        /// </summary>
        /// <param name="sets">The included sets in catalog order.</param>
        /// <returns>
        /// Full name to count.
        /// </returns>
        internal Dictionary<string, int> CountIncludedSetsByType(IReadOnlyList<EdmEntitySet> sets)
        {
            ArgumentNullException.ThrowIfNull(sets);

            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var set in sets)
            {
                var type = ResolveEntityType(set);
                if (type is null)
                {
                    continue;
                }

                counts[type.FullName] = counts.GetValueOrDefault(type.FullName) + 1;
            }

            return counts;
        }

        /// <summary>
        /// Indexes first-overload functions and actions for named-tool emission.
        /// </summary>
        /// <returns>
        /// Unbound lists and bound maps keyed by binding type.
        /// </returns>
        internal CatalogOperationIndex IndexCatalogOperations()
        {
            var index = new CatalogOperationIndex();
            if (_options.IncludeFunctions)
            {
                foreach (var function in _model.Functions)
                {
                    index.Add(new CatalogOperation(function));
                }
            }

            if (_options.IncludeActions)
            {
                foreach (var action in _model.Actions)
                {
                    index.Add(new CatalogOperation(action));
                }
            }

            return index;
        }

        /// <summary>
        /// Builds a human description for an entity set from declared metadata.
        /// </summary>
        /// <param name="set">The entity set.</param>
        /// <param name="type">The entity type.</param>
        /// <returns>
        /// Description text.
        /// </returns>
        internal static string DescribeSet(EdmEntitySet set, EdmEntityType? type)
        {
            var documented = EdmDocumentation.First(set.Description, set.LongDescription, type?.Description, type?.LongDescription);
            string structural;
            if (type is null)
            {
                structural = $"{set.Name} entity set of type {set.EntityType}.";
            }
            else
            {
                var keys = type.Key.Count == 0 ? "no key" : string.Join(", ", type.Key);
                structural = $"{set.Name} entity set of type {type.FullName}; key {keys}.";
            }

            return documented is null ? structural : $"{documented} {structural}";
        }

        /// <summary>
        /// Combines CSDL documentation with an operational sentence and optional suffix.
        /// </summary>
        /// <param name="operation">The CRUD or query sentence.</param>
        /// <param name="documented">CSDL or vocabulary documentation, if any.</param>
        /// <param name="suffix">An optional trailing reminder.</param>
        /// <returns>
        /// Description text that prefers metadata documentation.
        /// </returns>
        internal static string ComposeToolDescription(string operation, string? documented, string? suffix = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(operation);

            var text = string.IsNullOrWhiteSpace(documented)
                ? operation
                : documented.Contains(operation, StringComparison.Ordinal)
                    ? documented
                    : $"{operation} {documented}";

            if (string.IsNullOrWhiteSpace(suffix))
            {
                return text.Trim();
            }

            return $"{text.Trim()} {suffix}".Trim();
        }

        /// <summary>
        /// Uses a short CSDL summary as a title when it fits a UI label.
        /// </summary>
        /// <param name="fallback">The operational title.</param>
        /// <param name="candidates">Documentation candidates, in preference order.</param>
        /// <returns>
        /// A short documented title, or <paramref name="fallback"/>.
        /// </returns>
        internal static string ResolveTitle(string fallback, params string?[] candidates)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fallback);
            ArgumentNullException.ThrowIfNull(candidates);

            var documented = EdmDocumentation.First(candidates);
            if (documented is not null && documented.Length <= 80 && documented.IndexOf('\n') < 0)
            {
                return documented;
            }

            return fallback;
        }

        /// <summary>
        /// Orders entity sets: include list first, then remaining alphabetical.
        /// </summary>
        /// <param name="sets">The candidate sets.</param>
        /// <returns>
        /// Ordered sets.
        /// </returns>
        internal IReadOnlyList<EdmEntitySet> OrderSetsForNamedTools(IReadOnlyList<EdmEntitySet> sets)
        {
            var include = _options.IncludeEntitySets
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => sets.FirstOrDefault(set => set.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                .Where(set => set is not null)
                .Cast<EdmEntitySet>()
                .ToList();

            var rest = sets
                .Where(set => include.All(item => !item.Name.Equals(set.Name, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(set => set.Name, StringComparer.Ordinal);

            return [.. include, .. rest];
        }

        /// <summary>
        /// Resolves entity sets that are not excluded.
        /// </summary>
        /// <returns>
        /// Declared entity sets minus exclusions.
        /// </returns>
        internal IReadOnlyList<EdmEntitySet> ResolveIncludedSets()
        {
            var sets = _model.EntityContainer?.EntitySets ?? [];
            if (_options.ExcludeEntitySets.Count == 0)
            {
                return [.. sets];
            }

            return [.. sets.Where(set => !_options.ExcludeEntitySets.Contains(set.Name, StringComparer.OrdinalIgnoreCase))];
        }

        /// <summary>
        /// Resolves the declared entity type for a set.
        /// </summary>
        /// <param name="set">The entity set.</param>
        /// <returns>
        /// The entity type, or <c>null</c> if it is not in the model.
        /// </returns>
        internal EdmEntityType? ResolveEntityType(EdmEntitySet set)
        {
            var byFullName = _model.GetEntityType(set.EntityType);
            if (byFullName is not null)
            {
                return byFullName;
            }

            var shortName = set.EntityType.Contains('.', StringComparison.Ordinal)
                ? set.EntityType[(set.EntityType.LastIndexOf('.') + 1)..]
                : set.EntityType;

            return _model.EntityTypes.FirstOrDefault(type => type.Name.Equals(shortName, StringComparison.Ordinal));
        }

        #endregion

    }

}
