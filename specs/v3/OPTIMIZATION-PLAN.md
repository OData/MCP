# Optimization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Do **not** implement from [EXECUTION-PLAN.md](./EXECUTION-PLAN.md) — that work is done.

**Goal:** Replace EDMX as the thing the calling AI reads, and make the first `tools/call` succeed, without changing the tool set except adding `odata_describe_model`.

**Architecture:** Evolve the existing Core catalog and runtime. Compact type shapes ([TYPE-SHAPES.md](./TYPE-SHAPES.md)). Instructions, schemas, EdmType validation, enums, cache ([OPTIMIZATION.md](./OPTIMIZATION.md)). Two hosts keep sharing one catalog. Capture today’s payloads as Breakdance `Before/` first so later `Current/` diffs are real.

**Tech Stack:** .NET 8/9/10, C# 14 on net10, ModelContextProtocol 2.x, MSTest, Breakdance, FluentAssertions, live Northwind + TripPin. Optional `Ben.TypeDictionary` for CLR `Type` keys.

**Spec:** [OPTIMIZATION.md](./OPTIMIZATION.md) and [TYPE-SHAPES.md](./TYPE-SHAPES.md). This plan argues from those. Update [TOOL-TEST-MANIFEST.md](./TOOL-TEST-MANIFEST.md) in the same task that changes a catalog contract.

## Global Constraints

- Sequential `dotnet` with `-c Debug`. Never omit Configuration.
- Never mock `HttpClient`, OData, MCP, or metadata.
- Core does not reference `Microsoft.OData.Edm`, `Microsoft.OData.Core`, ASP.NET, or `Microsoft.OData.Mcp.Authentication`.
- One C# type per file. No nested types. Normal namespaces. Newline before `{`. `#region` Fields, Properties, Constructors, Public Methods, Private Methods. Public then internal. Alphabetical within a group. No `private` except fields.
- `is null` / `is not null`. `ArgumentNullException.ThrowIfNull`. `ArgumentException.ThrowIfNullOrWhiteSpace`. `.IsNullOrWhiteSpace`.
- Catalogs emit only declared EDM members. Do not walk CLR entity types.
- Do not register `odata_call_function`, `odata_call_action`, or `list_{set}_{nav}`.
- Do not ship `ServerInstructions` that name `odata_describe_model` before that tool is registered (Task 5 before Task 8).
- Do not infer store-generated keys from Int32/Guid. Only `EdmProperty.Computed`.
- Do not rewrite advertised schemas mid-session for enum wire observation.
- Query args still have no `$`.
- XML docs on public APIs. `<param>` on the same line. `<remarks>` last.
- When adding properties to `ODataMcpCatalogOptions`, also update `ODataMcpSessionFactory.CopyCatalogOptions`.

---

## Target file map

```
src/Microsoft.OData.Mcp.Core/
  Models/EdmEnumType.cs, EdmEnumMember.cs          # NEW
  Models/EdmProperty.cs                            # add Computed
  Models/EdmModel.cs                               # EnumTypes
  Models/EdmDocumentation.cs                       # Add(dict, key, value, skipIfEqualTo)
  Parsing/CsdlParser.cs                            # EnumType, Core.Computed
  Catalog/ODataMcpCatalog.cs                       # schemas, shapes, cache, 11th generic
  Catalog/ODataMcpCatalogOptions.cs                # Preface, IsDynamicModel, EnumJsonFormat
  Catalog/ODataMcpInstructions.cs                  # NEW — Default + Compose
  Catalog/ODataToolDescriptor.cs                   # OutputSchema
  Catalog/ODataToolRuntime.cs                      # describe text/json, describe_model, call, validate
  Catalog/EdmTypeShape.cs                          # NEW — cached shape
  Constants/ODataMcpCatalogConstants.cs            # OdataDescribeModel, Format, Detail, Computed, …

src/Microsoft.OData.Mcp.AspNetCore/
  Adaptation/EdmModelAdapter.cs                    # enums + Computed
  Hosting/ODataMcpSessionFactory.cs                # CopyCatalogOptions new fields; ServerInstructions
  Extensions/ODataMcp_AspNetCore_ServiceCollectionExtensions.cs

src/Microsoft.OData.Mcp.Tools/Hosting/ToolsMcpHost.cs   # ServerInstructions via pre-Build holder

src/Microsoft.OData.Mcp.Tests.Core/
  Baselines/TypeShapes/Before/, Current/
  Catalog/TypeShapeBaselineTests.cs                # NEW
  …existing catalog/parser tests updated per task
```

Live helpers already exist: `LiveMetadata.LoadNorthwindModelAsync()`, `LiveMetadata.LoadTripPinModelAsync()`, `LiveToolRuntime`.

---

### Task 1: Freeze today’s payloads (no product change)

**Files:**
- Create: `src/Microsoft.OData.Mcp.Tests.Core/Catalog/TypeShapeBaselineTests.cs`
- Create: `src/Microsoft.OData.Mcp.Tests.Core/Baselines/TypeShapes/Before/` and `Current/` (generator writes them)
- Test: that class

**Interfaces:**
- Consumes: existing `ODataMcpCatalog`, `ODataToolRuntime`, `LiveMetadata`
- Produces: write-once `Before/` files and asserted `Current/` files

- [x] **Step 1: Write tests that fail because baseline files are missing**

One class, no nested types. Deterministic JSON (catalog `SchemaSerializerOptions`). Capture at least:

- `northwind.describe_type.customer.json` — `odata_describe_type` `name=Customers`
- `northwind.create_customer.inputschema.json` — named `create_*` InputSchema for Customer
- `northwind.list_entity_sets.json`
- `northwind.list_operations.json`
- `northwind.tools.list.json` — tool names + each generic InputSchema, ordered
- `trippin.describe_type.person.json`
- `trippin.list_operations.json`

```csharp
[TestMethod]
public async Task DescribeType_Northwind_Customer_MatchesCurrentBaseline()
{
    var catalog = new ODataMcpCatalog(await LiveMetadata.LoadNorthwindModelAsync(), new ODataMcpCatalogOptions());
    var runtime = new ODataToolRuntime(catalog, new CapturingODataExecutor());
    var result = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Customers"), CancellationToken.None);
    result.IsError.Should().BeFalse();
    var baseline = File.ReadAllText(Path.Combine(ProjectPath, "Baselines/TypeShapes/Current/northwind.describe_type.customer.json"));
    result.StructuredContent.Should().NotBeNull();
    result.StructuredContent!.GetRawText().Should().Be(baseline);
}
```

Use `CapturingODataExecutor` / existing test doubles only if they are **not** mocks of HttpClient/OData — catalog-only tools do not need HTTP. `projectPath = "..//..//..//"` like Breakdance.

- [x] **Step 2: Run** `dotnet test src/Microsoft.OData.Mcp.Tests.Core/Microsoft.OData.Mcp.Tests.Core.csproj -c Debug --filter TypeShapeBaselineTests`  
  Expected: FAIL (files missing)

- [x] **Step 3: Add `[BreakdanceManifestGenerator]` `WriteTypeShapeBaselines(string projectPath)`** that writes each payload to `Current/` always, and to `Before/` only if the file does not exist.

- [x] **Step 4: Run the generator the way this repo already runs Breakdance manifest generators. Re-run the tests.** Expected: PASS.

- [x] **Step 5: Commit** `test: freeze type-shape payloads before optimization`

Do not change product code in this task.

---

### Task 2: Enum types + `Computed` in Core EDM

**Files:**
- Create: `src/Microsoft.OData.Mcp.Core/Models/EdmEnumType.cs`
- Create: `src/Microsoft.OData.Mcp.Core/Models/EdmEnumMember.cs`
- Modify: `EdmModel.cs` — `List<EdmEnumType> EnumTypes`
- Modify: `EdmProperty.cs` — `bool Computed { get; set; }`
- Modify: `CsdlParser.cs` — parse `EnumType` / members / `IsFlags` / `UnderlyingType`; set `Computed` from `Org.OData.Core.V1.Computed` and `Core.ComputedDefaultValue` (boolean true)
- Modify: `EdmModelAdapter.cs` — same from `IEdmEnumType` / vocabulary
- Modify: `ODataMcpSessionFactory.CopyCatalogOptions` only if you add options here (do not yet)
- Test: `CsdlParserMoreTests.cs`, `EdmModelAdapterTests.cs`, `EdmModelSurfaceTests.cs`

**Interfaces:**
- Produces: `EdmEnumType { Name, Namespace, UnderlyingType, IsFlags, Members, Description, LongDescription, FullName }`; `EdmEnumMember { Name, Value }`; `EdmProperty.Computed`

- [x] **Step 1: Tests** — fixture CSDL with `EnumType Color` members `Red=0,Green=1`, `IsFlags="false"`. Property typed `NS.Color`. Property with `Core.Computed` true. Assert `model.EnumTypes` has Color with two members; Computed property is true; property without annotation is false. Adapter test: `IEdmModel` with enum + Computed term. Zero-member EnumType → parse throws.

- [x] **Step 2: Run — FAIL**

- [x] **Step 3: Implement parser + adapter + types. One class per file. Do not guess Computed from Int32/Guid.**

- [x] **Step 4: PASS** `dotnet test …Tests.Core -c Debug --filter Enum` and adapter tests.

- [x] **Step 5: Commit** `feat: parse EDM enum types and Core.Computed`

---

### Task 3: Catalog options, instructions helper, type cache (no surface change yet)

**Files:**
- Modify: `ODataMcpCatalogOptions.cs` — `InstructionsPreface`, `IsDynamicModel`, `EnumJsonFormat` (enum in its own file `ODataEnumJsonFormat.cs`)
- Create: `src/Microsoft.OData.Mcp.Core/Catalog/ODataMcpInstructions.cs` — `Default` may be empty or a placeholder **without** naming `odata_describe_model` until Task 8. Prefer: add `Compose` now; set `Default` to the full OPTIMIZATION.md §1 string in Task 8.
- Create: `src/Microsoft.OData.Mcp.Core/Catalog/EdmTypeShape.cs`
- Modify: `ODataMcpCatalog.cs` — `ConcurrentDictionary<string, EdmTypeShape>` filled eager in ctor when `!IsDynamicModel`
- Modify: `ODataMcpSessionFactory.CopyCatalogOptions` — copy the three new properties
- PackageReference `Ben.TypeDictionary` on Core if you add a CLR `Type` map; otherwise skip until needed. EDM cache stays `ConcurrentDictionary<string, EdmTypeShape>`.
- Test: options copy; cache has an entry for `NorthwindModel.Customer` (or whatever `FullName` is) after catalog construct; `IsDynamicModel=true` → cache empty/skipped

- [x] **Step 1: Tests**
- [x] **Step 2: FAIL**
- [x] **Step 3: Implement**
- [x] **Step 4: PASS** `dotnet test … -c Debug`
- [x] **Step 5: Commit** `feat: catalog type-shape cache and options`

---

### Task 4: Compact `odata_describe_type` + omit-null docs + bound ops + enums

**Files:**
- Modify: `ODataMcpCatalogConstants.cs` — `Format`, `Text`, `Json` (and any shape keys). Alphabetical.
- Modify: `ODataMcpCatalogConstantsTests.cs`
- Modify: `ODataMcpCatalog.BuildGenericTools` — describe `InputSchema` adds optional `format` enum `text`|`json`
- Modify: `ODataToolRuntime.DescribeType` — default `text` per [TYPE-SHAPES.md](./TYPE-SHAPES.md) §1.1; `format=json` §1.2; bound operations; enums; omit null docs; drop `entityTypeDescription`
- Modify: `BuildTypeCard` — compact JSON, same bound ops/enums, omit-null
- Modify: existing describe tests (`OdataDescribeTypeHostTests`, live Northwind/TripPin describe tests) so they assert the **new** shape, not `"nullable":false` blobs
- Modify: [TOOL-TEST-MANIFEST.md](./TOOL-TEST-MANIFEST.md) describe section
- Regenerator: Task 1 generator now writes text + json describe files; tests lock `Current/`

**Grammar:** implement [TYPE-SHAPES.md](./TYPE-SHAPES.md) §§1–5. `MaxLength` inline only when ≤ 16. Required-on-create uses `!Computed`. Bound ops on the type; `// writes` for actions; `// collection` when `BindingParameterType` starts with `Collection(`. Unbound ops **not** on this tool.

- [x] **Step 1: Tests** — Northwind Customer text contains `CompanyName: string` and `Orders ->`; does not contain `"description":null` or `entityTypeDescription`. `format=json` has `"props"`. `format=garbage` is error. TripPin Person lists bound operations, not `isBound`. Enum fixture property renders `enum(Red|Green)`.
- [x] **Step 2: FAIL**
- [x] **Step 3: Implement. One representation per call. Do not dual-emit text + JSON.**
- [x] **Step 4: PASS** `dotnet test src/Microsoft.OData.Mcp.Tests.Core -c Debug` and host describe tests in AspNetCore/Restier/Authentication **sequentially**.
- [x] **Step 5: Run baseline generator. Update `Current/` (never `Before/`). Commit** `feat: compact odata_describe_type`

---

### Task 5: `odata_describe_model`

**Files:**
- Modify: constants — `OdataDescribeModel = "odata_describe_model"`, `Detail`, `Summary`, `Complete`, `Mermaid`, `Sets`
- Modify: `BuildGenericTools` — 11th generic **after** `odata_describe_type`, before `odata_query` (keep documented order; update `Catalog_GenericTools_HaveExpectedNames` to insert it there)
- Modify: `ODataToolRuntime.InvokeAsync` switch + `DescribeModel`
- [TYPE-SHAPES.md](./TYPE-SHAPES.md) §6.2
- TOOL-TEST-MANIFEST: add the tool
- Baseline files: `northwind.describe_model.summary.txt`

Order of generics after this task:

```
odata_list_entity_sets
odata_describe_type
odata_describe_model
odata_query
odata_get
odata_create
odata_update
odata_delete
odata_navigate
odata_list_operations
odata_call
```

- [x] **Step 1: Tests** — no args → summary text, Northwind has `Customers:` and `-> Orders`. `detail=complete` includes `CompanyName:`. `format=mermaid` contains `erDiagram` and no `{ string`. Oversize: set `MaxResponseBytes` tiny, expect error mentioning `sets` or `summary`, not CSDL. Unknown `detail`/`format` → error. `sets: ["Customers"]` complete does not dump Orders properties.
- [x] **Step 2: FAIL**
- [x] **Step 3: Implement**
- [x] **Step 4: PASS**
- [x] **Step 5: Generator + `Current/`. Commit** `feat: odata_describe_model`

---

### Task 6: `odata_list_operations` unbound only

**Files:**
- Modify: `ODataToolRuntime.ListOperations` — skip `IsBound`; compact signatures; parameter types; `// writes` on actions; omit null docs; no `isBound`/`kind`
- Modify: live TripPin list-operations tests (today they expect GetNearestAirport **and** bound names in one list). Bound names must **not** appear. They appear on `odata_describe_type` for that type (Task 4).
- TOOL-TEST-MANIFEST list-operations section

- [x] **Step 1: Tests** — TripPin list_operations contains unbound `ResetDataSource` / `GetNearestAirport` as applicable; does not contain `ShareTrip` if ShareTrip is bound (verify on EDM). Describe Person still lists ShareTrip.
- [x] **Step 2: FAIL** (if current tests still expect mixed list, **change those tests in this step** — they are wrong under the new spec)
- [x] **Step 3: Implement**
- [x] **Step 4: PASS**
- [x] **Step 5: Generator + commit** `feat: list unbound operations only`

---

### Task 7: Named create/update schemas + omit name-as-description

**Files:**
- Modify: `ODataMcpCatalog.BuildPropertySchema` / `BuildNamedFamily`
- Create schema: `"required"` = required-on-create (`!Nullable && no DefaultValue && !Computed`)
- Update schema: property map + `key`; `"required": ["key"]` only
- No `"description": "CompanyName"`
- Enum `enum` array per `EnumJsonFormat` (default Auto = string names)
- `maxLength` only when ≤ 16
- `OutputSchema` on list_entity_sets / list_operations only (OPTIMIZATION.md §3 table)
- Tests: Northwind `create_customer` InputSchema contains `"required"` including `CustomerID` and `CompanyName`; does not contain `"description":"CompanyName"`. Update tool schema contains `"key"` in required and not CompanyName.

- [x] **Step 1: Tests**
- [x] **Step 2: FAIL**
- [x] **Step 3: Implement**
- [x] **Step 4: PASS**
- [x] **Step 5: Generator + commit** `feat: typed create and update schemas with required`

---

### Task 8: `odata_call` `parameters` + EdmType validation

**Files:**
- Modify: `BuildGenericTools` `odata_call` InputSchema
- Modify: `CallOperationAsync` — read `parameters` object; no `body`; no extra top-level args; collection-bound: `entitySet` no `key`; validate then HTTP
- Modify: `CreateAsync` / named create path — validate required-on-create and types before HTTP
- Modify: `OdataCallHostTests` and live TripPin call tests
- TOOL-TEST-MANIFEST §15

Validation table is [OPTIMIZATION.md](./OPTIMIZATION.md) §4. Int64/Decimal accept number or string. Int32 number only.

- [x] **Step 1: Tests** — `odata_call` `{ name: ResetDataSource, parameters: {} }` succeeds or hits real OData (TripPin). `{ name: GetNearestAirport, lat: 1 }` extra top-level → missing parameters / error, not URL scrape. `parameters: "{}"` string → `parameters must be a JSON object`. Bound without key → error contains `Pass entitySet and key`. Unknown param → `Unknown parameter`. Create Customer omitting CompanyName → error before HTTP (`CapturingODataExecutor` records **zero** sends).
- [x] **Step 2: FAIL**
- [x] **Step 3: Implement**
- [x] **Step 4: PASS** sequential Core + host call tests
- [x] **Step 5: Generator + commit** `feat: odata_call parameters object and EdmType validation`

---

### Task 9: ServerInstructions + tool description copy

**Files:**
- Modify: `ODataMcpInstructions.Default` — exact OPTIMIZATION.md §1 block (now legal: `odata_describe_model` exists)
- Modify: `ToolsMcpHost.CreateAsync` — holder **before** `Build()`; `IConfigureOptions<McpServerOptions>` or equivalent sets `ServerInstructions = ODataMcpInstructions.Compose(options.InstructionsPreface)`
- Modify: AspNetCore `AddODataMcp` — same `ServerInstructions` from catalog options (preface known without metadata)
- Modify: every `Description =` in `BuildGenericTools` / `BuildNamedFamily` to OPTIMIZATION.md §2
- Tests: Tools and AspNetCore initialize / `IOptions<McpServerOptions>.Value.ServerInstructions` contains `Do not read $metadata` and `parameters`. Preface `Contoso.` appears before Default. Empty preface does not insert extra blank noise.

- [x] **Step 1: Tests**
- [x] **Step 2: FAIL**
- [x] **Step 3: Implement. Do not assign ServerInstructions after `Build()`.**
- [x] **Step 4: PASS**
- [x] **Step 5: Generator + commit** `feat: MCP server instructions and first-call tool descriptions`

---

### Task 10: Manifest + full test pass

**Files:**
- Modify: [TOOL-TEST-MANIFEST.md](./TOOL-TEST-MANIFEST.md) registered-tools table to the **new** contracts (format, describe_model, call parameters, list_operations unbound)
- Run: sequential `dotnet test` on Tests.Core, Tests.Tools, Tests.AspNetCore, Tests.AspNetCore.Restier, Tests.Authentication, Tests.Authentication.Restier, Tests.Integration — all `-c Debug`
- Baseline generator one last time; `Before/` unchanged; `Current/` matches HEAD

- [x] **Step 1: Update TOOL-TEST-MANIFEST tables**
- [x] **Step 2: Full sequential test pass**
- [x] **Step 3: Commit** `docs: tool test manifest matches optimized catalog`

Tokenizer report (`OPTIMIZATION-REPORT.md`) is **after** this, optional, not a gate.

---

## Do not

- Implement from EXECUTION-PLAN.md
- Point the AI at `$metadata` for exploration
- Dual-emit text and JSON in one tool result
- Fuzzy-match parameter names
- Mid-session schema rewrite for enums
- Infer Computed from key type
- Add `list_{set}_{nav}`
- Python or Perl
- Mock HttpClient / OData / MCP
