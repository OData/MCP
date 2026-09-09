// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Core.Constants
{

    /// <summary>
    /// Protocol identifiers for the OData MCP catalog and tool runtime.
    /// </summary>
    /// <remarks>
    /// Tool names, argument names, type-card keys, and EDM primitive names are the catalog
    /// contract. Descriptions and full JSON Schema documents stay inline at the call site.
    /// </remarks>
    public static class ODataMcpCatalogConstants
    {

        #region Fields

        /// <summary>
        /// JSON payloads.
        /// </summary>
        public const string ApplicationJson = "application/json";

        /// <summary>
        /// CSDL metadata.
        /// </summary>
        public const string ApplicationXml = "application/xml";

        /// <summary>
        /// JSON request body argument.
        /// </summary>
        public const string Body = "body";

        /// <summary>
        /// <c>detail</c> value that dumps every in-scope type with properties, enums, and bound operations.
        /// </summary>
        public const string CompleteDetail = "complete";

        /// <summary>
        /// Model-shape key for complex types used by in-scope entity types.
        /// </summary>
        public const string ComplexTypes = "complexTypes";

        /// <summary>
        /// OData <c>$count</c> argument.
        /// </summary>
        public const string Count = "count";

        /// <summary>
        /// Named create tool prefix.
        /// </summary>
        public const string CreatePrefix = "create_";

        /// <summary>
        /// Named delete tool prefix.
        /// </summary>
        public const string DeletePrefix = "delete_";

        /// <summary>
        /// Short documentation property.
        /// </summary>
        public const string Description = "description";

        /// <summary>
        /// Level-of-detail argument on <c>odata_describe_model</c>.
        /// </summary>
        public const string Detail = "detail";

        /// <summary>
        /// Compact-shape key for member documentation.
        /// </summary>
        public const string Docs = "docs";

        /// <summary>
        /// <c>Edm.Binary</c>.
        /// </summary>
        public const string EdmBinary = "Edm.Binary";

        /// <summary>
        /// <c>Edm.Boolean</c>.
        /// </summary>
        public const string EdmBoolean = "Edm.Boolean";

        /// <summary>
        /// <c>Edm.Byte</c>.
        /// </summary>
        public const string EdmByte = "Edm.Byte";

        /// <summary>
        /// <c>Edm.Decimal</c>.
        /// </summary>
        public const string EdmDecimal = "Edm.Decimal";

        /// <summary>
        /// <c>Edm.Double</c>.
        /// </summary>
        public const string EdmDouble = "Edm.Double";

        /// <summary>
        /// <c>Edm.Int16</c>.
        /// </summary>
        public const string EdmInt16 = "Edm.Int16";

        /// <summary>
        /// <c>Edm.Int32</c>.
        /// </summary>
        public const string EdmInt32 = "Edm.Int32";

        /// <summary>
        /// <c>Edm.Int64</c>.
        /// </summary>
        public const string EdmInt64 = "Edm.Int64";

        /// <summary>
        /// <c>Edm.SByte</c>.
        /// </summary>
        public const string EdmSByte = "Edm.SByte";

        /// <summary>
        /// <c>Edm.Single</c>.
        /// </summary>
        public const string EdmSingle = "Edm.Single";

        /// <summary>
        /// <c>Edm.Stream</c>.
        /// </summary>
        public const string EdmStream = "Edm.Stream";

        /// <summary>
        /// EDM primitive <c>Edm.String</c>.
        /// </summary>
        public const string EdmString = "Edm.String";

        /// <summary>
        /// Entity-by-key resource template name.
        /// </summary>
        public const string EntityByKey = "entityByKey";

        /// <summary>
        /// Entity set argument, card property, and template name.
        /// </summary>
        public const string EntitySet = "entitySet";

        /// <summary>
        /// Array of entity sets.
        /// </summary>
        public const string EntitySets = "entitySets";

        /// <summary>
        /// Entity type name.
        /// </summary>
        public const string EntityType = "entityType";

        /// <summary>
        /// Compact-shape key: one <c>$filter</c> literal pattern (<c>NS.Enum'{value}'</c>) per enumeration the shape uses.
        /// </summary>
        public const string EnumFilterLiterals = "enumFilterLiterals";

        /// <summary>
        /// JSON Schema <c>enum</c> keyword.
        /// </summary>
        public const string EnumKeyword = "enum";

        /// <summary>
        /// OData <c>$expand</c> argument.
        /// </summary>
        public const string Expand = "expand";

        /// <summary>
        /// OData <c>$filter</c> argument.
        /// </summary>
        public const string Filter = "filter";

        /// <summary>
        /// Representation argument on describe tools.
        /// </summary>
        public const string Format = "format";

        /// <summary>
        /// Named get tool prefix.
        /// </summary>
        public const string GetPrefix = "get_";

        /// <summary>
        /// JSON Schema <c>items</c> keyword for array element schemas.
        /// </summary>
        public const string Items = "items";

        /// <summary>
        /// JSON Schema type <c>array</c>.
        /// </summary>
        public const string JsonArray = "array";

        /// <summary>
        /// JSON Schema type <c>boolean</c>.
        /// </summary>
        public const string JsonBoolean = "boolean";

        /// <summary>
        /// <c>format</c> value that selects compact JSON.
        /// </summary>
        public const string JsonFormat = "json";

        /// <summary>
        /// JSON Schema type <c>null</c>, added to nullable property types.
        /// </summary>
        public const string JsonNull = "null";

        /// <summary>
        /// JSON Schema number.
        /// </summary>
        public const string JsonNumber = "number";

        /// <summary>
        /// JSON Schema object.
        /// </summary>
        public const string JsonObject = "object";

        /// <summary>
        /// JSON Schema string.
        /// </summary>
        public const string JsonString = "string";

        /// <summary>
        /// Entity key argument.
        /// </summary>
        public const string Key = "key";

        /// <summary>
        /// Key property names.
        /// </summary>
        public const string Keys = "keys";

        /// <summary>
        /// Named list tool prefix.
        /// </summary>
        public const string ListPrefix = "list_";

        /// <summary>
        /// Long documentation property.
        /// </summary>
        public const string LongDescription = "longDescription";

        /// <summary>
        /// JSON Schema <c>maxLength</c> keyword, emitted only when the EDM <c>MaxLength</c> is at most 16.
        /// </summary>
        public const string MaxLength = "maxLength";

        /// <summary>
        /// <c>format</c> value that selects a relationship-only mermaid <c>erDiagram</c> on <c>odata_describe_model</c>.
        /// </summary>
        public const string MermaidFormat = "mermaid";

        /// <summary>
        /// The CSDL metadata document resource name.
        /// </summary>
        public const string Metadata = "$metadata";

        /// <summary>
        /// Type, set, operation, or member name.
        /// </summary>
        public const string Name = "name";

        /// <summary>
        /// Type namespace.
        /// </summary>
        public const string Namespace = "namespace";

        /// <summary>
        /// Navigation property argument.
        /// </summary>
        public const string Navigation = "navigation";

        /// <summary>
        /// Navigation properties collection.
        /// </summary>
        public const string Navigations = "navigations";

        /// <summary>
        /// Compact-shape key for navigation properties.
        /// </summary>
        public const string Navs = "navs";

        /// <summary>
        /// <c>odata_call</c>.
        /// </summary>
        public const string OdataCall = "odata_call";

        /// <summary>
        /// <c>odata_create</c>.
        /// </summary>
        public const string OdataCreate = "odata_create";

        /// <summary>
        /// <c>odata_delete</c>.
        /// </summary>
        public const string OdataDelete = "odata_delete";

        /// <summary>
        /// <c>odata_describe_model</c>.
        /// </summary>
        public const string OdataDescribeModel = "odata_describe_model";

        /// <summary>
        /// <c>odata_describe_type</c>.
        /// </summary>
        public const string OdataDescribeType = "odata_describe_type";

        /// <summary>
        /// <c>odata_get</c>.
        /// </summary>
        public const string OdataGet = "odata_get";

        /// <summary>
        /// <c>odata_list_entity_sets</c>.
        /// </summary>
        public const string OdataListEntitySets = "odata_list_entity_sets";

        /// <summary>
        /// <c>odata_list_operations</c>.
        /// </summary>
        public const string OdataListOperations = "odata_list_operations";

        /// <summary>
        /// <c>odata_navigate</c>.
        /// </summary>
        public const string OdataNavigate = "odata_navigate";

        /// <summary>
        /// <c>odata_query</c>.
        /// </summary>
        public const string OdataQuery = "odata_query";

        /// <summary>
        /// <c>odata_update</c>.
        /// </summary>
        public const string OdataUpdate = "odata_update";

        /// <summary>
        /// Declared functions and actions.
        /// </summary>
        public const string Operations = "operations";

        /// <summary>
        /// Compact-shape key for bound operations.
        /// </summary>
        public const string Ops = "ops";

        /// <summary>
        /// OData <c>$orderby</c> argument.
        /// </summary>
        public const string OrderBy = "orderby";

        /// <summary>
        /// Operation parameters.
        /// </summary>
        public const string Parameters = "parameters";

        /// <summary>
        /// Structural properties or JSON Schema <c>properties</c>.
        /// </summary>
        public const string Properties = "properties";

        /// <summary>
        /// Compact-shape key for structural properties.
        /// </summary>
        public const string Props = "props";

        /// <summary>
        /// JSON Schema <c>required</c> keyword.
        /// </summary>
        public const string Required = "required";

        /// <summary>
        /// OData <c>$select</c> argument.
        /// </summary>
        public const string Select = "select";

        /// <summary>
        /// Compact-shape key for the entity set a type is reached through.
        /// </summary>
        public const string Set = "set";

        /// <summary>
        /// Compact-shape key for entity set documentation when it differs from the type's.
        /// </summary>
        public const string SetDescription = "setDescription";

        /// <summary>
        /// Entity-set scope argument and model-shape key on <c>odata_describe_model</c>.
        /// </summary>
        public const string Sets = "sets";

        /// <summary>
        /// OData <c>$skip</c> argument.
        /// </summary>
        public const string Skip = "skip";

        /// <summary>
        /// <c>detail</c> value that lists sets, keys, and navigations only. The default.
        /// </summary>
        public const string SummaryDetail = "summary";

        /// <summary>
        /// <c>format</c> value that selects the declaration-grammar text. The default.
        /// </summary>
        public const string TextFormat = "text";

        /// <summary>
        /// OData <c>$top</c> argument.
        /// </summary>
        public const string Top = "top";

        /// <summary>
        /// EDM or JSON Schema type.
        /// </summary>
        public const string Type = "type";

        /// <summary>
        /// Model-shape key for entity type bodies in <c>detail=complete</c>.
        /// </summary>
        public const string Types = "types";

        /// <summary>
        /// Named update tool prefix.
        /// </summary>
        public const string UpdatePrefix = "update_";

        /// <summary>
        /// MCP resource scheme prefix, for example <c>odata://route/</c>.
        /// </summary>
        public const string UriSchemePrefix = "odata://";

        #endregion

    }

}
