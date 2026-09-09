# Type Shapes — Analysis (draft, not normative)

**Status:** Historical brainstorm. Not a v3 spec. Do not implement from this file.

**Question asked:** are we exposing too much OData (navigations, `IsBound`, facets), should there be a
shorter format, and should string/number length restrictions be exposed at all?

**Short answer:** treat **deferred tool loading as the default**. Harnesses already optimize the
context window: they search/load tools instead of stuffing the full catalog into every turn. Compact
`describe` still matters (paid when asked). Named-tool schemas matter when a tool is *loaded*, not
as a 150-tool dump. Expose *semantic* constraints (enums, keys, **required-on-create**, literal
syntax) and skip *storage* constraints (length, precision, unicode, SRID) except short codes the
model is likely to get wrong. One type and the whole model are **different jobs**: `odata_describe_type`
zooms in (text by default, compact JSON on request); `odata_describe_model` is the whole-service tool
with a **summary / complete** switch — summary is the map, complete is every type in one payload so
the model is not forced to call `describe_type` N times. Never EDMX. CSDL stays the escape hatch on
`odata://{route}/$metadata`.

---

## 1. What the model receives today

Five surfaces describe types, plus `$metadata` which is the whole-model trap. Only `tools/list` is paid
on search/load; the rest are on demand.

| Surface | When paid | Shape today | Source |
|---|---|---|---|
| `tools/list` | **On search / load**, not every turn, under deferred loading (the default we design for). Eager full-list clients still exist; they pay more. | 10 generic tools today (11 with `odata_describe_model`) + up to 150 named tools. `create_{x}` carries a JSON Schema with every property (`type` + `description`) and **no object-level `required` array**. `list_{set}` repeats the 7-parameter query schema per set; `update_{x}` takes an untyped `body: string`. | `ODataMcpCatalog.BuildNamedFamily`, `BuildPropertySchema` |
| `odata_describe_type` | On demand | JSON: `description`, `entitySet`, `entityTypeDescription`, `keys[]`, `longDescription`, `name`, `namespace`, `navigations[{description,name,type}]`, `properties[{description,name,nullable,type}]`. **No operations.** `name` is required; there is no format argument. | `ODataToolRuntime.DescribeType` |
| Entity-set resource (type card) | On demand (`resources/read`) | Same as above minus `nullable`, plus set-level fields | `ODataMcpCatalog.BuildTypeCard` |
| `odata_list_entity_sets` | On demand | JSON `{ entitySets: [{ name, entityType, keys, description }] }`. Names and keys only — **no navigations**, so it is not a graph. | `ODataToolRuntime.ListEntitySets` |
| `odata://{route}/$metadata` | On demand (`resources/read`) | **Full CSDL XML.** Northwind is tens of KB; Graph/Dynamics is megabytes. This is what a host returns when “the system asks for the whole model” today. | `ODataMcpCatalog.BuildResources` |
| `odata_list_operations` | On demand | Flat list of **all** functions and actions (bound and unbound): `{description, isBound, kind, name, parameters[names], returnType}`. `isBound` is a boolean, not a target. Parameter types are missing. | `ODataToolRuntime.ListOperations` |

Facts worth knowing before redesigning:

- **Length/precision facets are not exposed anywhere today** except inside the raw CSDL resource. `EdmProperty` carries `MaxLength`, `Precision`, `Scale`, `Unicode`, `DefaultValue`, `SRID`, but no surface reads them. So "should we expose them" is a question about *adding*, not removing.
- **Enum members are not exposed anywhere.** A property typed `NS.Color` shows up as the string `"NS.Color"`. The model cannot write `Color eq NS.Color'Red'` without guessing. This is a bigger correctness gap than any facet.
- **Property descriptions fall back to the property name.** `BuildPropertySchema` emits `"description": "CompanyName"` for `CompanyName` when the CSDL has no documentation — pure duplication, paid on every turn for every property of every `create_*` tool.
- **Null keys are always emitted.** `DescribeType` and `BuildTypeCard` build a fixed dictionary (`description`, `entityTypeDescription`, `longDescription` on the type; `description` on every property and navigation) and `SchemaSerializerOptions` does **not** set `WhenWritingNull`. Northwind therefore ships `"description":null` on every member. AspNetCore JSON options already omit nulls; the catalog serializer does not.
- **Descriptions are tripled when they exist.** `describe` returns `description`, `entityTypeDescription`, and `longDescription`, which are often the same string. The parser already copies LongDescription into Description when Summary is absent.
- **Types are fully qualified.** `Edm.String`, `Collection(NorthwindModel.Order)`. The namespace carries no information the model can act on unless the model has several namespaces.

---

## 2. Where the tokens go (estimates, 1 token ≈ 4 chars, Northwind `Customer`: 11 properties, 2 navigations)

### 2.1 `tools/list` — the always-on cost

| Item | Approx. tokens | Notes |
|---|---|---|
| `list_{set}` input schema | ~90 | identical for every set; 150 sets → ~13,500 tokens of repetition |
| `create_{x}` input schema, 11 props | ~250 | `"CompanyName":{"type":"string","description":"CompanyName"}` ≈ 20 tokens × 11 |
| `create_{x}` for a 40-property type | ~850 | wide tables are common in line-of-business APIs |
| One CRUD family (list/get/create/update/delete), 11 props | ~550 | name + title + description + schema + hints |
| **150 named tools at the cap** | **~18,000–30,000** | paid in full only by eager-list clients. Deferred loaders pay when a tool is searched or selected. |

For comparison, one `odata_describe_type` result for `Customer` is ~420 tokens and is paid once, when asked.
A compact declaration of the same type is ~110. A Northwind **summary** (every set, keys, navigations, no
properties) is ~600. A Northwind **complete** dump (every type with properties) is ~1,400–2,000 — one
call, not 13. Northwind **CSDL** is tens of thousands — that is what `$metadata` returns today.

**Conclusion under deferred loading (the default):** do not gut named `create_*` schemas to save a dump the harness no longer sends. Compact *each loaded tool* and compact `describe`. Eager-list clients still benefit from the same compact schemas. The cap is search-result size, not context dump.

### 2.2 `odata_describe_type` today vs. a compact shape

Today (abridged, real key names):

```json
{"description":null,"entitySet":"Customers","entityTypeDescription":null,"keys":["CustomerID"],
 "longDescription":null,"name":"Customer","namespace":"NorthwindModel",
 "navigations":[{"description":null,"name":"Orders","type":"Collection(NorthwindModel.Order)"},
                {"description":null,"name":"CustomerDemographics","type":"Collection(NorthwindModel.CustomerDemographic)"}],
 "properties":[{"description":null,"name":"CustomerID","nullable":false,"type":"Edm.String"},
               {"description":null,"name":"CompanyName","nullable":false,"type":"Edm.String"},
               ... 9 more ...]}
```
≈ 420 tokens. Per property ≈ 28 tokens, of which the information content is "name, string, required".

Compact declaration-style shape (the format the model has seen most of in training — TypeScript/C#
declarations):

```
Customer  (set: Customers, key: CustomerID)
  CustomerID: string(5)      // key
  CompanyName: string
  ContactName?: string
  ContactTitle?: string
  Address?: string
  City?: string
  Region?: string
  PostalCode?: string
  Country?: string
  Phone?: string
  Fax?: string
  Orders -> Order[]
  CustomerDemographics -> CustomerDemographic[]
```
≈ 110 tokens. Same information plus the one facet that matters (`CustomerID` is a 5-char code). **~74% fewer tokens**, and it reads the way the model writes code.

Compact JSON (if a machine-readable form is required — e.g. for resources):

```json
{"Customer":{"set":"Customers","key":["CustomerID"],
 "props":{"CustomerID":"string(5)!","CompanyName":"string!","ContactName":"string","City":"string", ...},
 "navs":{"Orders":"Order[]","CustomerDemographics":"CustomerDemographic[]"}}}
```
≈ 150 tokens. Still ~65% smaller than today; keeps `resources/read` JSON.

---

## 3. Answers to the specific questions

### 3.1 Navigations — keep, but make them one line each

Navigations *are* the graph. They are the only way the model learns that `expand=Orders` and
`odata_navigate(Customers, ALFKI, Orders)` are legal, and they are what makes multi-hop questions
("orders for German customers") answerable without a tour of the metadata. Removing them would push the
model back to guessing names. What should go is the ceremony: an object with a null `description` and a
`Collection(NS.Type)` string collapses to `Orders -> Order[]`. `Partner`, `ContainsTarget`, `OnDelete`,
and referential constraints are not exposed today and should stay that way; none change what the model
can *ask for*.

### 3.2 Operations — bound on the type, unbound on the service

Open decision 8 was a flat list with a `Type.Name(...)` prefix. That is the wrong split.

- **Bound** = what you can do **with the type** (and its set). Lives on `odata_describe_type`.
- **Unbound** = what you can do **with the service**. Lives on `odata_list_operations` (unbound only) and at the **end** of `odata_describe_model`.

If the caller asks for a type, they want two things in one payload: **what it is** (properties, keys, navs, docs) and **what they can do with it** (bound operations). They should not have to scan a service-wide list, guess `isBound`, and match a binding parameter.

Today `odata_list_operations` dumps everything with a boolean and a `kind` of `function` / `action`. `odata_describe_type` lists no operations. `odata_call` already executes both and **already picks GET vs POST from the EDM** — the caller passes `name` (and `entitySet`/`key` when bound). Listing should not re-teach a taxonomy the wire does not need.

**Function vs action is a distinction without a difference for the caller.** OData’s split is GET + URL params + no side effects vs POST + body + maybe side effects. The executor already knows. The model and the end user do not need the words, two sections, or `kind`. Do not emit `functions` / `actions` headers. One `operations` list.

The only fact from that split that is worth a mark: **side effects.** An action can mutate; a function must not. That is MCP `readOnlyHint`, not “this is an Action.” Mark mutating operations `// writes`. Everything else is callable the same way. Do **not** mark GET/POST, composable, or `kind`. Composability (`$filter` with a function) is real OData and almost never what the model is doing; skip it.

**On the type** (`odata_describe_type`, each type in `describe_model` `complete`, type-card resources):

```
Person  (set: People, key: UserName)
  UserName: string            // key, required on create
  FirstName: string
  …
  Friends -> Person[]
  Trips -> Trip[]
  operations
    GetFavoriteAirline() -> Airline
    GetFriendsTrips(userName: string) -> Trip[]
    ShareTrip(userName: string, tripId: int)    // writes
```

No `Person.` prefix — they are already under the type. No `isBound`, no `kind`. Drop the binding parameter from the argument list (it is the instance or the set). Keep parameter **types**.

Collection-bound vs instance-bound still belongs **on the type** (both are “what can I do with this type”). Mark collection-bound so `odata_call` knows it needs the set, not a key:

```
Product  (set: Products, key: ProductID)
  …
  operations
    MostExpensive() -> Product                  // collection
    GetRelated(count?: int) -> Product[]
    Discount(percentage: int)                   // writes
```

`// collection` when `BindingParameterType` is `Collection(...)`. Instance-bound is the default (needs `entitySet` + `key` on `odata_call`). Omit the section when the type has no bound operations (§3.5: do not emit empty keys).

CSDL documentation on the operation follows §3.5: trailing `// summary` when present.

**On the service** (`odata_list_operations` + tail of `odata_describe_model`):

```
operations
  GetNearestAirport(lat: double, lon: double) -> Airport
  ResetDataSource()                             // writes
```

`odata_list_operations` becomes **unbound only**. Bound operations do not appear there. Same compact signatures, parameter types, docs-when-present, `// writes` on mutating. No `isBound`, no `kind`. Empty list → omit the section, not an array of nulls.

`odata_call` is one payload format for every operation — see §3.9. The listing’s parameter names **are** the `parameters` keys. Do not make the caller pick GET vs POST.

**`odata_describe_model`:** unbound signatures at the **end**, after the types (summary and complete). Bound operations sit on each type in `complete` (and are omitted from `summary`, which has no property lists either — zoom is `describe_type`). Mermaid does not render operations.

**Do not add a 12th generic.** Keep `odata_list_operations`; change its contract and description:

> Lists unbound operations on the service. Bound operations are on `odata_describe_type` for that type.

`odata_describe_type` description gains: `Includes bound operations. Call odata_list_operations for service-level (unbound) operations.`

### 3.3 Length / precision / scale — expose almost nothing, and only where the model would fail

Split constraints into two kinds:

- **Semantic constraints** change *what the model writes*: key, **required-on-create** (see §3.6),
  enum members, type family (string/number/bool/date/datetime/guid/duration), collection-ness, and the
  OData literal syntax that goes with the type. These belong in every shape.
- **Storage constraints** change *whether the server accepts it*: `MaxLength`, `Precision`, `Scale`,
  `Unicode`, `SRID`, `DefaultValue`. The model almost never violates a `MaxLength(200)` on a name field,
  and when it does the server's 400 already names the property — that feedback loop costs nothing until
  it is needed. Exposing them on every property on every turn is paying insurance on a risk that is
  nearly zero.

Recommendation: expose `MaxLength` **only when it is small** (proposed threshold ≤ 16), because short
fixed-width codes (`CustomerID` nchar(5), `CountryCode` char(2), `Currency` char(3)) are precisely the
fields a model invents plausible-but-too-long values for. Render it inline as `string(5)` — two tokens.
Never expose `Precision`/`Scale`/`Unicode`/`SRID` in shapes; they stay in the CSDL resource for anyone
who wants them. `DefaultValue` is interesting only to mark a non-nullable property as *optional on
create*; that can be folded into the `?` marker rather than shown.

Related: JSON Schema on `create_*` tools *could* carry `maxLength`. Same rule: only for the short codes,
never as a blanket. It is worth more there than in `describe`, because the create schema is what the
model fills in.

### 3.4 Enums — the missing piece

This is the one *addition* the analysis argues for. `Color: enum(Red|Green|Blue)` (plus the literal form
once at the top of the shape: `enum literal: NS.Color'Red'`) costs a handful of tokens and removes an
entire class of failed `$filter`s. Today enum members are unreachable except by reading the CSDL.

### 3.5 Descriptions — keep them, omit them when absent

Yes: CSDL documentation is useful to the calling model. It is the same information as an XmlDoc
`<summary>` / `<remarks>` or a JSDoc `/** */` — domain language, units, which field to filter on,
deprecated, PII, “this is the legal name, not the display name.” DotNetDocs stamps XmlDoc onto
`IEdmModel` as `Core.Description` / `Core.LongDescription`; the AspNetCore adapter already copies
those through (`METADATA-STRATEGY.md` §3). TripPin and any documented LOB API have them. Northwind
mostly does not, which is why today’s payloads look empty — not because the fields are worthless.

| CSDL / vocabulary | XmlDoc analogue | Core property | When to emit |
|---|---|---|---|
| `<Documentation><Summary>` or `Core.Description` | `<summary>` | `Description` | Always, when non-whitespace **and** not equal to the member name |
| `<Documentation><LongDescription>` or `Core.LongDescription` | `<remarks>` | `LongDescription` | When non-whitespace **and** different from `Description` |
| (none) | — | both null | **Do not emit the key** |

Drop `entityTypeDescription`. It is a third copy of the type’s summary.

**The defect is the dictionary, not the annotations.** `DescribeType` / `BuildTypeCard` / per-property maps always insert every key, then serialize nulls. `BuildPropertySchema` is worse: it *invents* `"description": "CompanyName"` when the CSDL is silent. The catalog serializer has no `WhenWritingNull`; AspNetCore’s JSON options already do.

Pattern: start with the required keys, add extras only when they carry information.

```
var property = new Dictionary<string, object>
{
    [Name] = member.Name,
    [Type] = member.Type
};
EdmDocumentation.Add(property, Description, member.Description, skipIfEqualTo: member.Name);
EdmDocumentation.Add(property, LongDescription, member.LongDescription, skipIfEqualTo: member.Description);
```

`WhenWritingNull` on `SchemaSerializerOptions` is defense in depth. It is not a substitute for not inserting the key — compact JSON, tests, and anything that does not go through that options object still see the empty slots if we put them in.

**Where the strings go** (same omit-if-absent rule everywhere):

| Surface | Summary | Long / remarks |
|---|---|---|
| Text shape (`describe` / `complete`) | Trailing `// …` on the member. Type-level summary as a header comment. | Second comment line, only when it differs from summary. |
| Compact JSON | Sparse `description` on the type; sparse `docs` map for members that have one (`"docs": { "CompanyName": "Legal registered name." }`). Do not change `"CompanyName":"string!"` into an object just because it is documented. | Sparse `longDescription` on the type only, and only when it differs. Property remarks are rare; if present they go in `docs` as a longer string or a sibling `longDocs` map — only if they differ from `docs`. |
| JSON Schema on `create_*` / `update_*` | `"description"` on the property schema **only when CSDL has it**. Never the property name. | Skip. Schema `description` is the summary; remarks bloat every loaded create tool. |
| Named / generic **tool** `description` | Already composed (`ComposeToolDescription`). Keep using `EdmDocumentation.First` (summary, then remarks if no summary). | Do not append remarks onto every `create_customer` in `tools/list`. Remarks belong on `describe` / `complete`. |
| `odata_list_entity_sets` | `description` on a set only when present. | Omit. |

**Do not cap** individual strings. Authors wrote them for people using the API; the model is that person. The `MaxResponseBytes` guard on `complete` still applies: if remarks on 400 types blow the budget, the error tells the caller to pass `sets` or use `summary`, same as §3.8. Do not silently strip documentation to fit.

Structural fallbacks (“`Customers` entity set of type `Customer`; key `CustomerID`.”) stay on **MCP tool descriptions**, which must always be a sentence. They do not belong inside type-card JSON or the declaration shape.

### 3.6 Required values — create vs PATCH (how we denote it)

Non-nullable in CSDL means the **stored entity** always has a value. It does **not** mean every HTTP
payload must list every such property.

| Operation | What the server requires | How we mark it |
|---|---|---|
| **Insert (POST)** | Payload **must include** every property that is non-nullable, has **no** `DefaultValue`, and is **not** store-generated (identity / computed key). Client-assigned keys (`CustomerID`) are required. | Shape: **no** `?`. JSON Schema on `create_*`: property in the object `"required"` array. Compact JSON: trailing `!`. |
| **Update (PATCH)** | OData merge: omitted properties keep their current values. If the model **sends** a non-nullable property, the value must not be JSON `null`. | Shape: same `?` / no-`?` as create (the type is not PATCH-optional). JSON Schema on `update_*`: **empty** `required` except `key`; each non-nullable property is `"type":"string"` (no `null`). Compact JSON: `!` still means “cannot be null”, not “must appear on PATCH”. |
| **Nullable or has default** | Optional on create. | Shape: `Name?: type`. JSON Schema: omit from `required`. Compact JSON: no `!`. |

**Required-on-create** (the `!` / no-`?` / Schema `required` set) =

`!property.Nullable && string.IsNullOrWhiteSpace(property.DefaultValue) && !IsStoreGenerated(property)`

`IsStoreGenerated`: key whose type is integer/guid **and** the set does not expect the client to supply
it (identity). Northwind `CustomerID` is a client-assigned string key → required. TripPin `Person`
`UserName` is the key the client sends → required. An `Id` integer identity → optional on create,
still marked `// key`.

Today `BuildPropertySchema` never emits a `required` array. Describe has `nullable` but no
required-on-create vs PATCH distinction. That is the gap.

**Shape examples**

```
Customer  (set: Customers, key: CustomerID)
  CustomerID: string(5)      // key, required on create
  CompanyName: string        // required on create
  ContactName?: string       // optional
```

Compact JSON: `"CompanyName":"string!"` vs `"ContactName":"string"`.

JSON Schema `create_customer` (when that named tool is loaded):

```json
{"type":"object","properties":{
  "CustomerID":{"type":"string","maxLength":5},
  "CompanyName":{"type":"string"},
  "ContactName":{"type":"string"}
},"required":["CustomerID","CompanyName"]}
```

JSON Schema `update_customer`: `required` is `["key"]` only; `CompanyName` stays `"type":"string"` so a
null PATCH is still invalid.

**The calling model will not know this unless the loaded tool says so.** `?` / `!` are our marks; they are not a protocol the model was trained on (TypeScript `?` is close; OData PATCH-omit is not). Instruction has to ride with whatever the harness actually put in context:

| Channel | When it is in context | What it must say |
|---|---|---|
| JSON Schema `"required"` on `create_*` / `odata_create` | When that create tool is loaded | Industry-standard. Models already treat `required` as “must send”. This is the primary create signal. Do not invent a parallel mark and skip `required`. |
| Create tool **description** (one sentence) | Same load | `Body must include every property not marked optional; keys the client assigns are required; omit store-generated keys.` |
| Update tool **description** (one sentence) | When `update_*` / `odata_update` is loaded | `PATCH: send only fields to change. Omitted fields keep their values. Do not send JSON null for required properties.` |
| `odata_describe_type` **description** | When describe is loaded | `No ? = required on create. Name?: = optional. PATCH may omit any field; null is invalid for required fields. Bound operations on this type are included; service-level (unbound) operations are on odata_list_operations.` |
| Shape legend | Inside the describe **result** | Optional one-liner. Redundant if the tool description already said it. Skip if we are counting tokens; keep if describe is used without reading the tool description (some harnesses strip descriptions). |

Do **not** rely on the compact JSON `!` suffix without the Schema `required` array. `!` is fine as a mirror of `required` in cards; it is not a substitute.

Generic `odata_create` / `odata_update` keep those two sentences even when the body is a string — that is the only instruction if the model never loads a named `create_*`.

### 3.7 `odata_describe_type` — optional `format`, one type only

Yes: add optional `format` with two values. Default is the declaration. Do not overload this tool into a whole-model dump (`name` stays required).

| `format` | Result | Who it is for |
|---|---|---|
| `text` (**default**, omit the argument) | Declaration block in §2.2 | The calling model. This is the fill-in-the-form surface. |
| `json` | Compact JSON in §2.2 (`props` / `navs` maps, `!` on required-on-create) | A client that parses, a test, or a model that is generating code. |

The text (and compact JSON) **includes bound operations** for that type (§3.2). That is “what it is” plus “what I can do with it.” Unbound operations are not on this tool. One `operations` list — not `functions` / `actions`.

Do **not** add `mermaid` here. One type is not a diagram; mermaid of a single entity with attribute compartments is strictly worse than the declaration (more tokens, worse names, no required-on-create / enum / literal).

Do **not** return both representations in one call. MCP `structuredContent` plus a text block is the spec’s dual channel, but many harnesses dump **both** into the model and we pay twice. One format per call, caller picks, default is what the model reads.

Keep type-card `resources/read` as compact JSON (open decision 2). That is a different surface with a different audience; it does not need a `format` argument.

Schema addition (eager generic, cheap):

```json
"format":{"type":"string","enum":["text","json"],"description":"text (default) is the declaration. json is the compact property map."}
```

Unknown `format` → tool error. Omitted → `text`.

### 3.8 Whole model — summary or complete, never EDMX

`resources/read` of `$metadata` is the full CSDL document. That is the right **escape hatch** (humans, codegen, fidelity) and the wrong **exploration** payload. A model that asks for the whole service should not receive XML.

Forcing `odata_describe_type` once per type is the other failure: **one MCP round trip per type**. A 500-set service is 500 calls, 500 payloads, and 500 chances to drop a type. `odata_describe_model` exists so the caller can take the map **or** the whole structural model **in one request**.

Two orthogonal arguments:

| Argument | Values | Default | What it selects |
|---|---|---|---|
| `detail` | `summary` \| `complete` | `summary` | How much of the model |
| `format` | `text` \| `json` \| `mermaid` | `text` | How it is written |
| `sets` | string[] (optional) | all included sets | Scope. Complete of 8 named sets is still one call. |

Do not squash `summary` / `complete` into `format`. `format=json` has to work for both depths.

**`detail=summary` (default)** — the map. Same grammar as describe, stacked, properties omitted except the key. Unbound operations (what you can do with the **service**) at the end; bound operations are not here — they live on the type.

```
NorthwindModel  (13 sets)
Customers: Customer  key CustomerID
  -> Orders[]
  -> CustomerDemographics[]
Orders: Order  key OrderID
  -> Customer
  -> Employee
  -> Order_Details[]
  -> Shipper
…
operations
  GetNearestAirport(lat: double, lon: double) -> Airport
  ResetDataSource()                             // writes
```

Northwind: ~600 tokens. Enough to write `expand=Orders` and `odata_navigate`. First exploratory call on Graph stays cheap because models omit optional args.

**`detail=complete`** — every in-scope type, one payload, same declaration grammar as `odata_describe_type`:

```
NorthwindModel  (13 sets)

Customer  (set: Customers, key: CustomerID)
  CustomerID: string(5)      // key, required on create
  CompanyName: string        // required on create
  ContactName?: string
  …
  Orders -> Order[]
  CustomerDemographics -> CustomerDemographic[]

Order  (set: Orders, key: OrderID)
  …
```

Plus, in the same payload: complex types that appear on those entities, enum members, **bound** operations on each type (§3.2), and **unbound** operations at the end. That is the one-shot “I do not want 500 describe calls.” Northwind: ~1,400–2,000 tokens of text vs tens of thousands of CSDL. A 40-property × 50-set LOB model is still cheaper as one complete dump than 50 round trips.

**Tool description (the instruction channel).** Models will not infer this from the name. The loaded tool has to say it:

> `summary` (default): sets, keys, navigations — the map — then unbound (service) operations. `complete`: every type with properties, enums, and bound operations, then unbound operations. Prefer `complete` instead of calling `odata_describe_type` once per type. Pass `sets` to scope a large model. Do not read `$metadata` to explore.

**Size-guard, fail first.** Honor `MaxResponseBytes` (already on the catalog). If `complete` of the requested scope would exceed it: **error**, with byte count, set count, and “pass `sets` to scope, or use `summary`.” Do not silently truncate. Do not return CSDL. Do not start calling `describe_type` internally. `sets` is how a Graph-sized model still gets a complete dump of the slice it needs — still one request.

**`format` (presentation, independent of detail):**

| `format` | `summary` | `complete` |
|---|---|---|
| `text` (**default**) | Outline above | Stacked declarations above |
| `json` | `{ "sets": { "Customers": { "type":"Customer", "key":[…], "navs": {…} } } }` | Same maps plus `props` / enums / operations |
| `mermaid` | Relationship-only `erDiagram`. No attribute compartments. | **Same graph.** Mermaid cannot carry §3.6; `complete`+`mermaid` does not grow property boxes. If the caller needed properties they wanted `text` or `json`. |

Unknown `detail` / `format` → tool error. Omitted → `summary` + `text`.

**Why default is summary, not complete.** Calling `odata_describe_model` with no args is the usual “what’s here?” Models omit optional arguments. Complete-by-default would ingest Graph on first contact. Complete is one explicit argument, called out in the tool description. Open decision 14 if we would rather default to complete.

**Why not Mermaid as the default.** The calling model reads **source**, not a rendered diagram. `||--o{` tokens buy nothing `-> Orders[]` does not. Names (`Order_Details`) and required-on-create / enums / literals are worse in mermaid. Hosts that **draw** it for a human are the real win — that is why mermaid is a format, not the assistant default.

**Do not overload `odata_describe_type`.** Omitting `name` to mean “the whole model” is an accidental dump. One-type vs whole-service are different questions. That is an 11th eager generic (`odata_describe_model`).

`odata_list_entity_sets` stays the **name index** (JSON list of sets/keys). Do not grow it into a diagram — current callers and tests expect `{ entitySets: [...] }`.

**MCP `initialize.instructions`** (optional extra channel — **not** `PROTOCOL.md`, which the calling model never sees). The MCP initialize result may carry an `instructions` string; some clients put it in the system prompt. We drafted copy in `specs/v3/PROTOCOL.md` §8 / `TOOL-SURFACE.md` §10. The product does **not** set it today (no `Instructions` in the hosts). If we wire it, it can repeat the one-line map/describe/call rule. It is not a substitute for tool descriptions and schemas: many clients truncate or ignore it, and it is not paid when a named tool is loaded later. The channels the model actually gets are the loaded tool’s `description`, its `inputSchema`, and the tool result (including fail-before-HTTP errors).

`$metadata` remains listed in `resources/list` for hosts and for anyone who actually needs CSDL.

### 3.9 `odata_call` — one payload format, no guessing

The last version failed first-time success: the model tried one incorrect shape, ate the 400, and fumbled into another. Two surfaces caused that:

1. **Function arguments were not in the tool schema.** `CallOperationAsync` reads extra top-level keys (`lat`, `lon`, …) that `InputSchema` never declared. Models either omit them or stuff them into `body`.
2. **`body` is a string.** A string has no format. Models guess a JSON object, a stringified object, OData type annotations, a wrapper `{ "ShareTrip": {…} }`, even XML.

`odata_call` today: `{ name, entitySet, key, body }`, required `name` only. Bound always demands `key` (wrong for collection-bound). Actions POST `body`; functions scrape undeclared arguments into the URL. The description even says “function (GET) or action (POST),” which invites the model to pick a verb it cannot pass.

**One call shape, copied from the listing:**

```json
{
  "name": "ShareTrip",
  "parameters": { "userName": "scottketchum", "tripId": 0 },
  "entitySet": "People",
  "key": "scottketchum"
}
```

Unbound:

```json
{
  "name": "GetNearestAirport",
  "parameters": { "lat": 33.8, "lon": -118.3 }
}
```

Collection-bound: `name` + `parameters` + `entitySet`, **no** `key`.

Schema (eager generic):

```json
{
  "type": "object",
  "properties": {
    "name": { "type": "string", "description": "Operation name exactly as listed on the type or by odata_list_operations." },
    "parameters": { "type": "object", "description": "Argument map. Keys are the parameter names from that listing. Omit the binding parameter. Do not stringify. Do not wrap under the operation name." },
    "entitySet": { "type": "string", "description": "Required when the listing is bound. The entity set that holds the binding instance or collection." },
    "key": { "type": "string", "description": "Required when the listing is instance-bound. Omit for collection-bound and unbound." }
  },
  "required": ["name"]
}
```

`parameters` is an **object**, not a string. `additionalProperties` stays open because a generic tool cannot name every operation’s arguments — the listing is the schema for those keys. Delete `body` from this tool. Delete undeclared top-level argument scraping.

**The listing and the call must use the same names.** `ShareTrip(userName: string, tripId: int)` means `parameters.userName` and `parameters.tripId`, same spelling, same types. If describe prints `userName` and the model sends `UserName`, that is a miss — do not fuzzy-match; return the declared names. First-time success is copy-from-listing, not discover-by-400.

**Fail before HTTP.** Look the operation up in the EDM, then:

| Mistake | Error (include the declared signature) |
|---|---|
| Unknown `name` | `Operation 'X' is not declared.` |
| Bound instance, missing `entitySet` or `key` | `ShareTrip is bound to Person. Pass entitySet and key. Signature: ShareTrip(userName: string, tripId: int) // writes` |
| Collection-bound, missing `entitySet` or extra `key` | `MostExpensive is collection-bound. Pass entitySet; omit key.` |
| Unbound with `entitySet`/`key` | `GetNearestAirport is unbound. Omit entitySet and key.` |
| Missing required parameter | `Missing parameter 'tripId'. Signature: ShareTrip(userName: string, tripId: int)` |
| Unknown parameter key (including the binding parameter) | `Unknown parameter 'UserName'. Declared: userName, tripId.` |
| `parameters` passed as a string | `parameters must be a JSON object, not a string.` |

Do not send a bad OData request so the model can “learn” from a 400. The error **is** the listing line they should have copied.

**Tool description — say whatever prevents a wrong first call**, including under-the-hood facts that stop a bad decision. Proposed copy:

> Call a declared operation by `name` as listed on `odata_describe_type` (bound) or `odata_list_operations` (unbound). Put every argument in `parameters` using those names; do not stringify; do not add extra top-level fields; do not wrap under the operation name. Instance-bound: also `entitySet` and `key`. Collection-bound (`// collection` on the listing): `entitySet` only. Unbound: neither. The server sends GET or POST as declared — do not pass a method. `// writes` means it mutates. If arguments are wrong the tool errors with the signature; do not guess a different payload shape.

That is allowed to mention GET vs POST **so they do not invent a `method` argument**. It is not allowed to ask them to choose one.

Runtime still maps `parameters` onto the URL (function) or the POST body (action). That mapping is not the model’s problem. `readOnlyHint` stays off on `odata_call` because some operations write.

**Why this stops guessing**

| Old surface | What the model invented | New surface |
|---|---|---|
| Extra undeclared tool args | Skip them, or put them in `body` | Only `parameters` is valid; extra top-level keys are unused and the description forbids them |
| `body: string` | Stringified JSON, wrappers, `@odata.type`, XML | `parameters` is a JSON object in the schema |
| Description says GET vs POST | Try to pick a verb | Description says the server picks; do not pass `method` |
| Bound always requires `key` | Fail on collection-bound, then retry | Listing says `// collection`; schema + error agree |
| OData 400 as teacher | Second and third attempts | EDM check first; error reprints the signature |

Named per-operation tools with typed `inputSchema` would be even tighter (no `additionalProperties`). They stay out of v1 because of `MaxNamedTools` and deferred loading; the listing + one object map + fail-before-HTTP is the generic-tool version of the same idea.

---

## 4. Tool surface under deferred loading

Deferred tool loading is the **design default**. Generics stay eager (small, always needed). Named
CRUD tools are advertised as searchable and loaded when selected. Compact each payload that *does*
enter context.

1. **Stop echoing names as descriptions** in `BuildPropertySchema`, and **stop inserting null
   documentation keys** in `DescribeType` / `BuildTypeCard` / property maps (§3.5). Bare dictionary,
   add `description` / `longDescription` only when they carry information. `WhenWritingNull` on
   `SchemaSerializerOptions` as defense in depth. Immediate cut on every loaded `create_*` schema
   and every describe payload, zero information lost; documented APIs keep the XmlDoc-equivalent text.
2. **Keep typed `create_*` schemas** (they are the fill-in form when the tool is loaded). Add a
   `required` array per §3.6. Do **not** collapse create to `body: string`. Give `update_*` the same
   compact property map with `required: ["key"]` only.
3. **`MaxNamedTools`** remains a search-result / eager-fallback cap, not a context-dump cap. Keep 150
   as the searchable ceiling unless measurement says otherwise; eager-list clients still see the
   compact schemas.
4. **Publish generics eagerly, named tools deferred.** Do not wait on a future MCP revision — operate
   as if the harness already does this. Add `odata_describe_model` to that eager set (11 generics).
5. **`odata_describe_type.format`** optional `text` | `json`, default `text`. `name` stays required.
6. **Whole-model asks go to `odata_describe_model`**, not `$metadata`. `detail=summary` (default) is the
   map plus unbound operations at the end; `detail=complete` is every type (with bound operations) then
   unbound operations, one payload, so the caller is not forced to spam `odata_describe_type`. `format`
   is `text` | `json` | `mermaid`. `sets` scopes a large model. Over `MaxResponseBytes` → error, not a
   silent trim, not CSDL.
7. **`odata_list_operations` is unbound only.** Bound operations are on `odata_describe_type` / each
   type in `complete`. No 12th generic.
8. **`odata_call` is one object map.** `{ name, parameters, entitySet?, key? }`. No `body` string, no
   undeclared top-level args. Listing names = `parameters` keys. Validate against the EDM and error
   with the signature **before** any OData HTTP (§3.9).

---

## 5. What a spec would contain

Working title `TYPE-SHAPES.md`. Sections:

1. **Shape grammar** — the declaration format (§2.2), formally: header line, property lines
   (`Name: type` required on create; `Name?: type` optional; `// key` / `// key, store-generated`),
   navigation lines (`Name -> Type[]`), enum rendering, one-line legend (literals + required-on-create
   vs PATCH omit). Tool descriptions on create/update/describe carry the same rule in English so the
   model does not have to infer the grammar.
2. **Type mapping table** — EDM primitive → shape type name, and which get a literal hint.
3. **Facet policy** — the semantic/storage split (§3.3), the `MaxLength ≤ 16` rule, enum members always,
   required-on-create vs PATCH (§3.6).
4. **Where each shape appears** — `odata_describe_type` (text default, `format=json` compact, **bound
   operations on the type**); `odata_describe_model` (`detail=summary|complete`, unbound ops at the
   end, bound ops on each type when `complete`); type-card resources (compact JSON, same bound ops);
   `odata_list_entity_sets` (name index, unchanged JSON); `odata_list_operations` (**unbound only**,
   §3.2); `$metadata` resource remains CSDL.
5. **Tool-list policy** — deferred named tools, eager generics (11, including `odata_describe_model`),
   compact create/update schemas with `required` per §3.6, no name-as-description.
6. **Measurement** — required before merging: token counts (real tokenizer, not chars/4) for
   `tools/list`, `describe`, and a 5-question task suite on Northwind, TripPin, and one wide line-of-
   business model, before/after; plus a correctness check that the model's generated `$filter`s and
   create bodies succeed at least as often as today. Token savings that cost accuracy are a regression.
7. **Compatibility** — the CSDL resource remains the full-fidelity escape hatch; `ODataMcpCatalogOptions`
   gets `ShapeFormat` (Compact | Json) so the JSON cards can be kept for anyone parsing them.

---

## 6. Open decisions — **closed**

All rows were accepted and are now rules in [TYPE-SHAPES.md](./TYPE-SHAPES.md) and [OPTIMIZATION.md](./OPTIMIZATION.md). Left here as the decision record.

| # | Decision | Recommendation |
|---|---|---|
| 1 | Text shape vs compact JSON as the default for `describe` | Text. Optional `format=json` on the same tool. Nothing client-side parses the default; the model does. |
| 2 | Keep JSON for `resources/read` type cards? | Yes — resources are the one place a client might parse. Same content, compact keys. No `format` on resources. |
| 3 | `MaxLength` threshold | ≤ 16. Tune with data. |
| 4 | `update_*` schema vs `create_*` untyped body? | **Keep** compact typed schemas on both. Create gets `required`; update requires `key` only. Deferred loading makes the schema the fill-in form. |
| 5 | Default `MaxNamedTools` | Keep 150 as searchable ceiling; not a context-dump cap. IncludeEntitySets still wins ordering. |
| 6 | Show navigation cardinality only via `[]`, or also `1`/`0..1`? | `[]` only; the `?` on a single-valued nav covers optionality. Mermaid overview uses `||--o{` / `||--o|` from the same fact. |
| 7 | Namespace | Once, in the shape header, only when the model has more than one. |
| 8 | Bound operations listed under their type or in a flat list? | **On the type.** Bound = what you can do with the type (`odata_describe_type`, each type in `complete`, type cards). Unbound = what you can do with the service (`odata_list_operations` unbound-only, tail of `odata_describe_model`). No `Type.Name` prefix, no `isBound` boolean. Collection-bound marked `// collection`; instance-bound is the default. **Do not split functions vs actions** in the listing — one `operations` list; mutating ops get `// writes`. The executor already picks GET vs POST. |
| 9 | `format` on `odata_describe_type`? | **Yes.** `text` (default) \| `json`. Not mermaid. `name` stays required. One representation per call — do not dual-emit text + JSON into context. |
| 10 | Whole-model payload | New eager generic `odata_describe_model`. Never EDMX. `detail=summary` (default) is the map; `detail=complete` is every type in one request. `$metadata` stays CSDL. |
| 11 | Mermaid? | Optional `format=mermaid` on **`odata_describe_model` only**. Relationship-only, no attribute boxes — even when `detail=complete`. |
| 12 | Overload `odata_describe_type` with omitted `name`? | **No.** Accidental dump. Different job, different tool. |
| 13 | Grow `odata_list_entity_sets` into the map? | **No.** It stays the name index. Current JSON contract stays. |
| 14 | Default `detail` on `odata_describe_model`? | **`summary`.** Models omit optional args; complete-by-default would ingest Graph on “what’s here?”. The tool description tells them to pass `complete` instead of N `describe_type` calls. |
| 15 | `sets` filter on `odata_describe_model`? | **Yes.** Complete of a named slice is still one call. How Graph-sized models stay under `MaxResponseBytes`. |
| 16 | Keep `Description` / `LongDescription` on shapes? | **Yes, when present.** XmlDoc summary / remarks. Omit the key when null, when it equals the member name, or when long equals summary. Drop `entityTypeDescription`. Do not invent `"description": "CompanyName"`. |
| 17 | `odata_call` payload | **One format:** `parameters` as a JSON **object** (not a string), keys copied from the listing. No extra top-level args, no `body`. Fail against the EDM before HTTP; the error reprints the signature. Description may mention GET/POST only to stop the model inventing a `method` argument. |
