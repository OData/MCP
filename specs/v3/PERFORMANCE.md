# Performance

**Status:** Plan. Not implemented.  
**Revised:** 2026-10-04

Implement the tasks below in order. Each task names the files, the change, and the test that proves it. Behavior of tool names, schema text, describe text, cursors, and resource cards stays the same. This document is the whole assignment.

Run every `dotnet` command from `D:\GitHub\MCP\src` with `--configuration Debug -f net11.0`.

Code written for these tasks uses block namespaces, Allman braces, and `#region` in this order: Fields, Properties, Constructors, Public Methods, Private Methods. Public members come before internal members. Alphabetical within a visibility group. `is null` / `is not null`. `ArgumentNullException.ThrowIfNull` and `ArgumentException.ThrowIfNullOrWhiteSpace` at entry points. XML docs on public APIs, with `<param>` content on the same line and `<remarks>` last. Tests are MSTest and FluentAssertions. No mocks.

`Microsoft.OData.Mcp.Core` is visible to `Microsoft.OData.Mcp.Tests.Core`. Assert internal members from that test project.

## Leave these alone

- `ODataToolInvocationResult.ToStructuredContent` clones the parsed JSON because the `JsonDocument` is disposed before the MCP result is serialized. That clone stays.
- `MaxResources` (default 50), `MaxCompletionValues` (default 50), and `ToolsPageSize` (default 100).
- OData `$top` / `EnableQuery` page size. Those hits are query paging, not `tools/list`.
- The Tools host's `MetadataXml`. `ToolsMcpHost.CreateAsync` stores the CSDL document the remote service returned. Return that string unchanged.
- Resource and resource-template list paging. This work is memory inside the catalog that already exists.
- `IncludeEntitySets` is order only. `ExcludeEntitySets` still removes sets from tools and resources. `IncludeCreate`, `IncludeUpdate`, and `IncludeDelete` still omit those verbs.

## What is already cached

`ODataMcpCatalog`'s constructor builds `Resources`, `ResourceTemplates`, and `Tools` once. `UseODataMcp` calls `ODataMcpSessionFactory.Rebuild` once at startup and keeps one session per route. `IsDynamicModel` false (the default) fills `_shapes` in that constructor. List handlers return those stored lists. `tools/list` already slices with `ODataMcpToolPages.Page` and projects only the page through `ODataMcpHandlers.ToTool`.

The cost that grows with request rate is inside `ToTool`. The cost that grows with the model is the schemas and shapes built in the constructor and then rebuilt on describe.

---

## Task 1 — Parse each tool schema once

`ODataMcpToolPages.Page` calls `ODataMcpHandlers.ToTool` for every tool on the page. `ToTool` calls `ParseElement`, which runs `JsonDocument.Parse` and `Clone` on `InputSchema` and, when present, `OutputSchema`. A full page repeats that for every schema, including the large create and update documents.

Identical schema text already exists. Every `list_*` tool is assigned the same literal. Every `delete_*` tool is assigned the same literal. `odata_list_entity_sets` and `odata_list_operations` share the empty-object schema. Parsing each unique string once, and handing that `JsonElement` to every later `ToTool`, removes the parse from the request path. Steady memory grows by one parsed document per distinct schema. Per-request memory drops by that amount on every `tools/list`.

Keep the `InputSchema` and `OutputSchema` strings. Tests and the public descriptor read them as text.

### Files

- `src/Microsoft.OData.Mcp.Core/Catalog/ODataToolDescriptor.cs`
- `src/Microsoft.OData.Mcp.Core/Catalog/ODataMcpHandlers.cs`
- `src/Microsoft.OData.Mcp.Core/Catalog/ODataMcpCatalog.cs`
- `src/Microsoft.OData.Mcp.Tests.Core/Catalog/CatalogPerformanceTests.cs` (new)

### Steps

1. On `ODataToolDescriptor`, after the public properties, add two internal properties: `ParsedInputSchema` and `ParsedOutputSchema`, both `JsonElement`. The default value is `JsonValueKind.Undefined`.

2. Add `ODataMcpHandlers.BindSchemas(IReadOnlyList<ODataToolDescriptor> tools)`. Walk the list once. Keep a `Dictionary<string, JsonElement>` with `StringComparer.Ordinal`. For each descriptor, resolve `InputSchema` through that dictionary, calling the existing `ParseElement` only on a miss, and assign `ParsedInputSchema`. Do the same for `OutputSchema` when it is not null or whitespace. Return the number of dictionary entries.

3. Add `internal int ParsedSchemaCount` on `ODataMcpCatalog`. At the end of the constructor, after `Tools = BuildTools(includedSets)`, set `ParsedSchemaCount = ODataMcpHandlers.BindSchemas(Tools)`.

4. Change `ToTool` so `InputSchema` uses `descriptor.ParsedInputSchema` when its `ValueKind` is not `Undefined`, and calls `ParseElement` only for a descriptor that never went through `BindSchemas` (unit tests construct those directly). Apply the same rule to `OutputSchema`, still returning null when the string is null or whitespace.

5. Copying a `JsonElement` struct shares the cloned document. Do not call `Clone` again inside `ToTool`.

### Tests

In `CatalogPerformanceTests`:

- Build a catalog from a short CSDL with three entity sets of three types and `IncludeCreate`, `IncludeUpdate`, and `IncludeDelete` left at their defaults. `ParsedSchemaCount` is less than `Tools.Count`.
- Every `list_*` descriptor's `ParsedInputSchema.GetRawText()` is equal. Every `delete_*` descriptor's `ParsedInputSchema.GetRawText()` is equal.
- `ODataMcpHandlers.ToTool` on `odata_query` still returns an input schema whose raw text contains `filter` and does not contain `$filter`. That is the existing assertion in `ODataMcpCatalogMoreTests.ToTool_CopiesHintsAndSchema`; leave that test in place.
- `ODataMcpToolPages.Page` on that catalog, null cursor, returns `ToolsPageSize` tools when the catalog is larger than the page, and each tool's `InputSchema.GetRawText()` is non-empty. Use `CatalogComplexityTests`'s wide model if the helper is reachable; otherwise a catalog of 25 sets from the same shape as `ODataMcpToolPageTests` is enough.

---

## Task 2 — Build the property schema once per type

`BuildNamedFamily` calls `GetShape`, then throws that shape away and constructs a second `EdmTypeShape` whenever the cached shape's entity set is a different set of the same type. `BuildCreateSchema` and `BuildUpdateSchema` each call `BuildPropertySchema`, which walks every exposed property and builds a dictionary. Two sets of one type therefore build the property map four times, and serialize create and update twice each. The create schema, the update schema, and the named key schema depend on the entity type. They do not read the entity set name.

`BuildListSuffix` also reads only `ExposedProperties`. The cached shape is enough for the whole family.

### Files

- `src/Microsoft.OData.Mcp.Core/Catalog/ODataMcpCatalog.cs`
- `src/Microsoft.OData.Mcp.Tests.Core/Catalog/CatalogPerformanceTests.cs`

### Steps

1. Delete the `ReferenceEquals` branch in `BuildNamedFamily`. Use the shape from `GetShape(type)` for `BuildListSuffix`, `BuildCreateSchema`, and `BuildUpdateSchema`.

2. Change `BuildTools` to own three dictionaries, keyed by `entityType.FullName` with `StringComparer.Ordinal`: create schema text, update schema text, named key schema text. Pass them into `BuildNamedFamily`.

3. On a hit, assign those strings onto the new descriptors. On a miss, call `BuildPropertySchema` once. Pass that dictionary into both serializers. Store the results.

4. Change `BuildCreateSchema` and `BuildUpdateSchema` to take the property dictionary they serialize, so neither method calls `BuildPropertySchema` itself. `BuildUpdateSchema` still wraps that map with `key` and still marks only `key` as required. `BuildCreateSchema` still adds `required` only when `RequiredOnCreate` is non-empty. The serialized JSON stays byte-for-byte the same, because `SchemaSerializerOptions` is unchanged and the dictionary insertion order is unchanged.

5. `BuildNamedKeySchema` stays a pure function of the key property names. Cache its return value in the third dictionary. `get_*` for a second set of the same type reuses it.

6. Skip the create entry when `IncludeCreate` is false, the update entry when `IncludeUpdate` is false. `get_*` is always built, so the key schema is always cached.

### Tests

Add a CSDL fixture with one entity type and two entity sets of that type. Parse it with `CsdlParser.ParseFromString` and build an `ODataMcpCatalog`.

- The two `create_*` descriptors' `InputSchema` strings are `ReferenceEquals`.
- The two `update_*` descriptors' `InputSchema` strings are `ReferenceEquals`.
- The two `get_*` descriptors' `InputSchema` strings are `ReferenceEquals`.
- With `IncludeCreate` false, neither create tool exists, and the update schemas are still shared.
- An existing Northwind or documented-CSDL schema test (`NamedSchemaTests`) still parses `required`, property names, and the absence of `body`. Do not change those expected strings.

---

## Task 3 — Keep one shape representation

Each `EdmTypeShape` constructor stores three copies of the same declaration:

- `Text`, the grammar used by `format=text`.
- `JsonBody`, the dictionary under the type-name key.
- `Json`, `JsonSerializer.Serialize` of a one-entry dictionary whose value is `JsonBody`.

`BuildTypeCard` assigns `shape.Json` to `ODataResourceDescriptor.ReadContents`. That assignment shares the string. The duplicate that stays resident is `JsonBody`.

`JsonBody` is a public property. Nothing in the test suite reads it. `EdmModelShape.RenderCompleteJson` embeds it. `EdmModelShape.RenderSummaryJson` reads only the `navs` entry.

`Text` stays eager. `format=text` is the default describe result, and type cards are built at startup.

### Files

- `src/Microsoft.OData.Mcp.Core/Catalog/EdmTypeShape.cs`
- `src/Microsoft.OData.Mcp.Core/Catalog/EdmModelShape.cs`
- `src/Microsoft.OData.Mcp.Tests.Core/Catalog/TypeShapeBaselineTests.cs` (existing baselines must still match)

### Steps

1. Stop storing `JsonBody`. The constructor still calls `BuildJsonBody` once, serializes `Json` from it, and lets the dictionary become eligible for collection. `Text = RenderText()` stays where it is.

2. Keep the public `JsonBody` getter. Implement it as `BuildJsonBody()` so a caller still compiles. Say in the remarks that each read builds a new dictionary.

3. In `RenderCompleteJson`, call `shape.BuildJsonBody()` once per shape and store that dictionary into `types`. Do not also read the public getter.

4. In `RenderSummaryJson`, stop reading `shape.JsonBody`. Build the `navs` map from `shape.EntityType.NavigationProperties` with `EdmTypeShape.MapShapeType`, which is the same mapping `BuildJsonBody` uses. Omit `navs` when the type has no navigation properties. `type`, `key`, and `FirstDocumentation` stay as they are.

5. Do not drop `Json`. Resource cards and `odata_describe_type` with `format=json` return that string.

### Tests

- `TypeShapeBaselineTests` for Northwind Customer and TripPin Person, text and JSON, still match `Current/`. If a baseline fails, the renderer changed and the renderer is wrong.
- `DescribeType_AgreesWithResourceReadTypeCard` still passes: the JSON describe of a set equals that set's resource `ReadContents`.
- A summary JSON describe of a type that has a navigation still contains that navigation name. Use the existing shop or Northwind describe-model test that already asserts navs.

---

## Task 4 — Stop holding a pretty-printed `$metadata` document

`EdmModelAdapter.ToCsdlXml` writes indented XML. `ODataMcpSessionFactory.Rebuild` stores that string on `ODataMcpSession.MetadataXml` for the life of the process. `ODataMcpHandlers.ReadResourceAsync` is the only reader, and only when the URI ends with `/$metadata`.

The OData pipeline already serves `GET $metadata` from the live `IEdmModel`. The MCP resource is a second copy.

### Files

- `src/Microsoft.OData.Mcp.AspNetCore/Adaptation/EdmModelAdapter.cs`
- `src/Microsoft.OData.Mcp.Core/Catalog/ODataMcpSession.cs`
- `src/Microsoft.OData.Mcp.AspNetCore/Hosting/ODataMcpSessionFactory.cs`

### Steps

1. In `ToCsdlXml`, set `XmlWriterSettings.Indent` to false. Keep `OmitXmlDeclaration` false. Keep the null return when `CsdlWriter.TryWriteCsdl` fails.

2. Add a public constructor on `ODataMcpSession`:

   `ODataMcpSession(ODataMcpCatalog catalog, ODataToolRuntime runtime, Func<string?> readMetadata)`

   Store a `Lazy<string?>` that invokes `readMetadata`. `ArgumentNullException.ThrowIfNull` on all three arguments. The existing string constructor stays and wraps the string in `Lazy<string?>(() => metadataXml)` so the property is still one code path. `MetadataXml` returns `_metadataXml.Value`.

3. In `Rebuild`, pass a delegate instead of the XML string:

   `() => EdmModelAdapter.ToCsdlXml(binding.Model)`

   Capture the model in the closure. The endpoint data source keeps that `IEdmModel` alive. The first `resources/read` of `$metadata` writes the document. Later reads return the same string.

4. Do not change `ToolsMcpHost.CreateAsync`. It passes the fetched XML into the string constructor.

### Tests

- `JsonRpcProtocolHostTests.ResourcesRead_Metadata_ReturnsCsdlXml` still sees `application/xml`, `EntityContainer`, and `Customers`, and still rejects an empty text payload.
- `RestierProtocolToolTests.ResourcesRead_Metadata_ReturnsCsdlXml` still sees `Customers` when the resource text is non-empty.
- Add a Core or AspNetCore test that `ToCsdlXml` on a model with an entity container returns a document that contains `EntityContainer` and does not contain a newline followed by two spaces.
- A session built with the `Func<string?>` constructor does not invoke the delegate until `MetadataXml` is read, then returns the same string on the second read. A test counter on the delegate proves the deferral.

---

## Task 5 — Reuse the type shape when the set is different

`GetShape` caches one `EdmTypeShape` per entity type. `BuildShape` pairs it with the first included set of that type. These three sites construct a full second shape when the requested set is not that set:

- `ODataMcpCatalog.BuildTypeCard`
- `ODataToolRuntime.DescribeType`
- `EdmModelShape.ResolveShape`

The second constructor re-filters properties, re-scans every action and function in the model, and re-renders text and JSON. The set changes only the header (`set: Name`), the set's documentation lines, and the `set` / set-description entries in the JSON body. Properties, keys, navigations, bound operations, and required-on-create do not change.

Task 2 already removed the same rebuild from `BuildNamedFamily`. Leave that method alone in this task.

### Files

- `src/Microsoft.OData.Mcp.Core/Catalog/EdmTypeShape.cs`
- `src/Microsoft.OData.Mcp.Core/Catalog/ODataMcpCatalog.cs`
- `src/Microsoft.OData.Mcp.Core/Catalog/ODataToolRuntime.cs`
- `src/Microsoft.OData.Mcp.Core/Catalog/EdmModelShape.cs`
- `src/Microsoft.OData.Mcp.Tests.Core/Catalog/CatalogPerformanceTests.cs`

### Steps

1. Add an internal constructor `EdmTypeShape(EdmTypeShape source, EdmEntitySet entitySet)`. Copy `_model`, `EntityType`, `ExposedProperties`, `RequiredOnCreate`, `BoundActions`, and `BoundFunctions` by reference. Set `EntitySet` to the argument. Then run the same `Text` and `Json` initialization the public constructor runs. Do not re-filter properties and do not re-scan `model.Actions` or `model.Functions`.

2. Add `internal EdmTypeShape WithSet(EdmEntitySet entitySet)`. `ArgumentNullException.ThrowIfNull`. When `ReferenceEquals(EntitySet, entitySet)`, return `this`. Otherwise return `new EdmTypeShape(this, entitySet)`.

3. On `ODataMcpCatalog`, add `internal EdmTypeShape ShapeFor(EdmEntitySet set)`. Resolve the type with `ResolveEntityType`. When the type is null, throw `ArgumentException` (the three callers already have a null-type path and must not call `ShapeFor` in that case). When `IsDynamicModel` is true, return `GetShape(type).WithSet(set)` and do not store it. Otherwise cache in a new `ConcurrentDictionary<string, EdmTypeShape>` keyed by `set.Name` with `StringComparer.Ordinal`. The first call for a set stores `GetShape(type).WithSet(set)`. Later calls return that instance.

4. Replace the three rebuild sites:

   - `BuildTypeCard`: when `entityType` is not null, `return ShapeFor(set).Json`. Keep the unresolved-type card that names only the set and its declared type.
   - `DescribeType`: when `set` is not null, use `ShapeFor(set)` instead of `GetShape` plus `new EdmTypeShape`. When `set` is null (the caller named a type), keep `GetShape(type)`.
   - `ResolveShape`: return `catalog.ShapeFor(set)` after the null-type check, and delete the `new EdmTypeShape` line.

5. `WithSet` output for the first set is the cached instance, so its `Text` and `Json` stay the strings already shared with the resource card. For another set, the new `Text` still starts with the header from `RenderHeaderLine` (`Type  (set: OtherSet, key: ...)`), and the new `Json` still contains `"set":"OtherSet"`. Property lines, navigation lines, and the operations block match the first set's shape.

### Tests

Using the two-set, one-type fixture from Task 2:

- `ShapeFor` on the first set and `ShapeFor` on that same set return the same instance.
- `ShapeFor` on the second set returns a different instance. Its `Text` contains `set: ` plus the second set's name. Its `Json` contains the second set's name. Its `ExposedProperties` list is `ReferenceEquals` to the first shape's list.
- `odata_describe_type` for the second set's name returns that header. `format=json` contains the second set's name and the same property names as the first set.
- Two calls to `ShapeFor` for the second set return the same instance when `IsDynamicModel` is false, and different instances when it is true.

---

## Task 6 — Keep the included-set list, and index tool names

`ResolveIncludedSets` allocates a new array on every call. The constructor calls it once. After that, `GetShape` (dynamic models only), `DescribeType`, `DescribeModel`, `ListEntitySets`, `ValidateEntityBody`, and the named-tool dispatch call it again. The static model does not change, so the constructor's list is the list.

`InvokeNamedAsync` walks `Tools` with `FirstOrDefault` on every named call. The OData call dominates that cost. The index is still one dictionary built with the catalog, and lookup becomes a hit.

### Files

- `src/Microsoft.OData.Mcp.Core/Catalog/ODataMcpCatalog.cs`
- `src/Microsoft.OData.Mcp.Core/Catalog/ODataToolRuntime.cs`
- `src/Microsoft.OData.Mcp.Tests.Core/Catalog/CatalogPerformanceTests.cs`

### Steps

1. Add `internal IReadOnlyList<EdmEntitySet> IncludedSets`. In the constructor, keep the existing local from `ResolveIncludedSets` and assign the field. When `IsDynamicModel` is true, `ResolveIncludedSets` keeps computing a fresh list, because that flag means the model may change. When it is false, `ResolveIncludedSets` returns `IncludedSets`.

2. Add `internal EdmEntitySet? FindIncludedSet(string name)`. `ArgumentException.ThrowIfNullOrWhiteSpace`. For a static model, look up a dictionary built once in the constructor: `Dictionary<string, EdmEntitySet>` with `StringComparer.OrdinalIgnoreCase`, first set winning if two names differ only by case. For a dynamic model, scan the fresh `ResolveIncludedSets` result the way `FirstOrDefault` does today.

3. Replace these `ResolveIncludedSets().FirstOrDefault(name equals ...)` calls with `FindIncludedSet`:

   - `ODataToolRuntime.DescribeType`
   - `ODataToolRuntime.ValidateEntityBody`

   Leave `ListEntitySets` and describe-model on `ResolveIncludedSets`. They need the full list, which is now the stored list for a static model.

4. Add `internal ODataToolDescriptor? FindTool(string name)` on the catalog. Build a `Dictionary<string, ODataToolDescriptor>` with `StringComparer.Ordinal` at the end of the constructor, after `Tools` is assigned. Insert only when the key is absent, so the first descriptor wins. That matches `FirstOrDefault`. `InvokeNamedAsync` uses `FindTool` and keeps the current branches for list, get, create, update, delete, and the unknown-tool error.

5. `CompleteEntitySetNames` stays a walk of the container. It includes sets that `ExcludeEntitySets` removed, and it applies `MaxCompletionValues`. Do not point it at `IncludedSets`.

### Tests

- On a static catalog, `ResolveIncludedSets()` called twice returns the same instance.
- On a catalog with `IsDynamicModel` true, the two calls return equal names and are not the same instance.
- `FindIncludedSet` finds a set by a different casing of its name, and returns null for a name in `ExcludeEntitySets`.
- `FindTool("odata_query")` returns that descriptor. `FindTool` of a name that is not in `Tools` returns null.
- A named `list_*` call still reaches the executor. Use an existing named-tool test rather than a new host.

---

## Done when

From `D:\GitHub\MCP\src`:

```powershell
dotnet test --project Microsoft.OData.Mcp.Tests.Core -c Debug -f net11.0 -- --filter "FullyQualifiedName~CatalogPerformanceTests|FullyQualifiedName~ODataMcpToolPageTests|FullyQualifiedName~NamedSchemaTests|FullyQualifiedName~TypeShapeBaselineTests|FullyQualifiedName~ToTool_CopiesHintsAndSchema"
dotnet test --project Microsoft.OData.Mcp.Tests.AspNetCore -c Debug -f net11.0 -- --filter "FullyQualifiedName~ResourcesRead_Metadata_ReturnsCsdlXml|FullyQualifiedName~DescribeType_AgreesWithResourceReadTypeCard|FullyQualifiedName~ToolsListPagingHostTests"
dotnet test --project Microsoft.OData.Mcp.Tests.AspNetCore.Restier -c Debug -f net11.0 -- --filter "FullyQualifiedName~ResourcesRead_Metadata_ReturnsCsdlXml"
```

All three exit 0. Describe baselines in `Baselines/TypeShapes/Current/` are unchanged. No package pack.
