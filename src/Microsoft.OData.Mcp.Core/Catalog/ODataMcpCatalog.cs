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

        internal static readonly JsonSerializerOptions SchemaSerializerOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        internal readonly EdmModel _model;
        internal readonly ODataMcpCatalogOptions _options;
        internal readonly ConcurrentDictionary<string, EdmTypeShape> _shapes = new(StringComparer.Ordinal);

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

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataMcpCatalog"/> class.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="options">Catalog options.</param>
        public ODataMcpCatalog(EdmModel model, ODataMcpCatalogOptions options)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentException.ThrowIfNullOrWhiteSpace(options.RouteName);

            _model = model;
            _options = options;

            var includedSets = ResolveIncludedSets();
            if (!options.IsDynamicModel)
            {
                FillShapeCache(includedSets);
            }

            Resources = BuildResources(includedSets);
            ResourceTemplates = BuildTemplates();
            Tools = BuildTools(includedSets);
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
        /// Converts a name to snake_case.
        /// </summary>
        /// <param name="value">The source name.</param>
        /// <returns>
        /// The snake_case name.
        /// </returns>
        internal static string ToSnakeCase(string value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            var chars = new List<char>(value.Length + 4);
            for (var index = 0; index < value.Length; index++)
            {
                var current = value[index];
                if (char.IsUpper(current) && index > 0 && value[index - 1] is not '_' and not '-')
                {
                    chars.Add('_');
                }

                chars.Add(char.ToLowerInvariant(current == '-' ? '_' : current));
            }

            return new string([.. chars]);
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

            var type = property.Type ?? string.Empty;
            return !type.Contains(EdmBinary, StringComparison.OrdinalIgnoreCase)
                && !type.Contains(EdmStream, StringComparison.OrdinalIgnoreCase);
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
        /// Builds generic and capped named tools.
        /// </summary>
        /// <param name="sets">The included entity sets.</param>
        /// <returns>
        /// Tool descriptors.
        /// </returns>
        internal IReadOnlyList<ODataToolDescriptor> BuildTools(IReadOnlyList<EdmEntitySet> sets)
        {
            var tools = new List<ODataToolDescriptor>(BuildGenericTools());
            var remaining = Math.Max(0, _options.MaxNamedTools - tools.Count);
            if (remaining == 0)
            {
                return tools;
            }

            var ordered = OrderSetsForNamedTools(sets);
            foreach (var set in ordered)
            {
                var family = BuildNamedFamily(set);
                if (family.Count == 0 || family.Count > remaining)
                {
                    break;
                }

                tools.AddRange(family);
                remaining -= family.Count;
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
                    Description = "Lists entity sets declared in the OData model, including CSDL documentation when the metadata provides Documentation or Core.Description annotations.",
                    InputSchema = """{"type":"object","properties":{},"additionalProperties":false}""",
                    Name = OdataListEntitySets,
                    OutputSchema = """{"type":"object","properties":{"entitySets":{"type":"array","items":{"type":"object","properties":{"name":{"type":"string"},"entityType":{"type":"string"},"keys":{"type":"array","items":{"type":"string"}},"description":{"type":"string"}},"required":["name","entityType","keys"]}}},"required":["entitySets"]}""",
                    ReadOnlyHint = true,
                    IdempotentHint = true,
                    Title = "List entity sets"
                },
                new ODataToolDescriptor
                {
                    Description = "Declared properties, keys, navigations, bound operations, and enums for a type or set. format=text (default) or json. Bound operations are here; unbound are on odata_list_operations. No ? = required on create; Name?: = optional. PATCH may omit any field; JSON null is invalid for required fields.",
                    InputSchema = """{"type":"object","properties":{"name":{"type":"string","description":"Entity set or type name declared in the model."},"format":{"type":"string","enum":["text","json"]}},"required":["name"]}""",
                    Name = OdataDescribeType,
                    ReadOnlyHint = true,
                    IdempotentHint = true,
                    Title = "Describe type"
                },
                new ODataToolDescriptor
                {
                    Description = "summary (default): sets, keys, navigations, then unbound operations. complete: every type with properties, enums, and bound operations, then unbound operations. Prefer complete instead of calling odata_describe_type once per type. Pass sets to scope a large model. format=text (default), json, or mermaid. Do not read $metadata to explore.",
                    InputSchema = """{"type":"object","properties":{"detail":{"type":"string","enum":["summary","complete"]},"format":{"type":"string","enum":["text","json","mermaid"]},"sets":{"type":"array","items":{"type":"string"},"description":"Entity set names to include. Omit for every included set."}}}""",
                    Name = OdataDescribeModel,
                    ReadOnlyHint = true,
                    IdempotentHint = true,
                    Title = "Describe model"
                },
                new ODataToolDescriptor
                {
                    Description = "Queries an entity set. Parameter names do not include $; the executor adds $filter, $select, $orderby, $expand, $top, $skip, and $count.",
                    InputSchema = querySchema,
                    Name = OdataQuery,
                    ReadOnlyHint = true,
                    IdempotentHint = true,
                    Title = "Query entity set"
                },
                new ODataToolDescriptor
                {
                    Description = "Gets an entity by key.",
                    InputSchema = """{"type":"object","properties":{"entitySet":{"type":"string"},"key":{"type":"string"}},"required":["entitySet","key"]}""",
                    Name = OdataGet,
                    ReadOnlyHint = true,
                    IdempotentHint = true,
                    Title = "Get entity"
                },
                new ODataToolDescriptor
                {
                    Description = "Creates an entity from a JSON body.",
                    InputSchema = """{"type":"object","properties":{"entitySet":{"type":"string"},"body":{"type":"string"}},"required":["entitySet","body"]}""",
                    Name = OdataCreate,
                    Title = "Create entity"
                },
                new ODataToolDescriptor
                {
                    Description = "Updates an entity with PATCH.",
                    IdempotentHint = true,
                    InputSchema = """{"type":"object","properties":{"entitySet":{"type":"string"},"key":{"type":"string"},"body":{"type":"string"}},"required":["entitySet","key","body"]}""",
                    Name = OdataUpdate,
                    Title = "Update entity"
                },
                new ODataToolDescriptor
                {
                    Description = "Deletes an entity by key.",
                    DestructiveHint = true,
                    IdempotentHint = true,
                    InputSchema = """{"type":"object","properties":{"entitySet":{"type":"string"},"key":{"type":"string"}},"required":["entitySet","key"]}""",
                    Name = OdataDelete,
                    Title = "Delete entity"
                },
                new ODataToolDescriptor
                {
                    Description = "Follows a navigation property from a key.",
                    InputSchema = """{"type":"object","properties":{"entitySet":{"type":"string"},"key":{"type":"string"},"navigation":{"type":"string"}},"required":["entitySet","key","navigation"]}""",
                    Name = OdataNavigate,
                    ReadOnlyHint = true,
                    IdempotentHint = true,
                    Title = "Navigate"
                },
                new ODataToolDescriptor
                {
                    Description = "Unbound operations on the service. Bound operations are on odata_describe_type.",
                    InputSchema = """{"type":"object","properties":{},"additionalProperties":false}""",
                    Name = OdataListOperations,
                    OutputSchema = """{"type":"object","properties":{"operations":{"type":"object","additionalProperties":{"type":"string"}}}}""",
                    ReadOnlyHint = true,
                    IdempotentHint = true,
                    Title = "List operations"
                },
                new ODataToolDescriptor
                {
                    Description = "Call a declared operation by name as listed on odata_describe_type (bound) or odata_list_operations (unbound). Put every argument in parameters using those names; do not stringify; do not add extra top-level fields; do not wrap under the operation name. Instance-bound: also entitySet and key. Collection-bound (// collection on the listing): entitySet only. Unbound: neither. The server sends GET or POST as declared — do not pass a method. // writes means it mutates. If arguments are wrong the tool errors with the signature; do not guess a different payload shape.",
                    InputSchema = """{"type":"object","properties":{"name":{"type":"string"},"parameters":{"type":"object","description":"Operation arguments keyed by the declared parameter names."},"entitySet":{"type":"string"},"key":{"type":"string"}},"required":["name"],"additionalProperties":false}""",
                    Name = OdataCall,
                    Title = "Call operation"
                }
            ];
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

            var setSnake = ToSnakeCase(set.Name);
            var singular = ToSnakeCase(type.Name);
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
                    Description = ComposeToolDescription(DescribeSet(set, type), documented, "Query parameter names do not include $."),
                    EntitySetName = set.Name,
                    InputSchema = """{"type":"object","properties":{"filter":{"type":"string"},"select":{"type":"string"},"orderby":{"type":"string"},"expand":{"type":"string"},"top":{"type":"number"},"skip":{"type":"number"},"count":{"type":"boolean"}}}""",
                    Name = $"{ListPrefix}{setSnake}",
                    ReadOnlyHint = true,
                    IdempotentHint = true,
                    Title = ResolveTitle($"List {set.Name}", set.Description, type.Description)
                },
                new()
                {
                    Description = ComposeToolDescription($"Gets a {type.Name} by key.", documented),
                    EntitySetName = set.Name,
                    InputSchema = """{"type":"object","properties":{"key":{"type":"string"}},"required":["key"]}""",
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
                    : $"{documented} {operation}";

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
