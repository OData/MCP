# Optimization report

Token measurements of the Breakdance baselines in `src/Microsoft.OData.Mcp.Tests.Core/Baselines/TypeShapes/` ([OPTIMIZATION.md](./OPTIMIZATION.md) §7). Tokenizer: SharpToken `cl100k_base`. The raw tables are produced by `src/Microsoft.OData.Mcp.Benchmarks` (`dotnet run -c Release -- --tokens`) into its `Reports/` folder: `TOKENS.md` for the baselines and `FORMATS.md` for CSDL XML vs CSDL JSON vs our shapes. `TokenBaselineTests` in Tests.Core is the build-time guard. Sections are appended per round; nothing here is a merge gate.

`Before/` holds the output of the pre-optimization build (commit `288ccd6`) against live Northwind and TripPin. `Current/` is what the tests assert and is regenerated after every intended change. `Baselines/TypeShapes/README.md` records how every `Before/` file was produced.

---

## 2026-09-09 — Round 1: Tasks 1–10 plus the two-model review fixes

### What changed between the snapshots

Tasks 1–10 of [OPTIMIZATION-PLAN.md](./OPTIMIZATION-PLAN.md): declaration-grammar type shapes, compact JSON shapes, `odata_describe_model`, `initialize.instructions`, first-call tool descriptions, typed named schemas, the `odata_call` contract, pre-HTTP EDM validation, enums, the type cache. Then the wording and schema fixes from two independent no-context model reviews (key literal form, object `body`, advertised `select`/`expand`/navigate options, enum filter hint on named `list_*`, `$metadata` reword, one rule in one place).

### Per-call tool results

Each row is one tool result as a model receives it. `Before` is the old build's output for the same request, or for `describe_model`, the old way of getting the same information (see below).

| Baseline | Before | Current | Delta | Change |
|---|---:|---:|---:|---:|
| `northwind.describe_model.summary.txt` | 5941 | 654 | -5287 | -89.0% |
| `northwind.describe_type.customer.json` | 286 | 81 | -205 | -71.7% |
| `northwind.describe_type.customer.txt` (default form) | n/a | 88 | | -69% vs the old JSON |
| `northwind.list_entity_sets.json` | 760 | 681 | -79 | -10.4% |
| `northwind.list_operations.json` | 4 | 1 | -3 | -75.0% |
| `trippin.describe_model.complete.txt` | 598 | 346 | -252 | -42.1% |
| `trippin.describe_model.summary.txt` | 598 | 91 | -507 | -84.8% |
| `trippin.describe_type.person.json` | 312 | 199 | -113 | -36.2% |
| `trippin.describe_type.person.txt` (default form) | n/a | 194 | | -38% vs the old JSON |
| `trippin.list_entity_sets.json` | 75 | 65 | -10 | -13.3% |
| `trippin.list_operations.json` | 267 | 37 | -230 | -86.1% |

The two text rows have no like-for-like Before: the old tool returned the bare type name as text (one token) and put everything in `structuredContent`. Compare against the `.json` row.

**What `describe_model` replaced.** The old build had no whole-service map. A model that wanted one had to call `odata_list_entity_sets` and then `odata_describe_type` once per set. Measured by running the old build against the live services:

| Service | Sets | Old: list + describe every set | Old: raw `$metadata` | Now: `describe_model` |
|---|---:|---:|---:|---:|
| Northwind | 26 | 5,941 | 4,802 | 654 (summary) |
| TripPin | 3 | 598 | 1,859 | 91 (summary), 346 (complete) |

Two things stand out. The old per-set path cost more on Northwind than reading the raw CSDL would have, which is why "do not read $metadata" was not a safe instruction to give before this work. And `describe_model complete` on TripPin, which carries every property, enum, and bound operation, is still 42% smaller than the old map that carried none of the bound operations.

### Against the raw metadata formats

The comparison people outside this repository will care about: the same information as CSDL XML (what `$metadata` serves), as OData CSDL JSON (EdmLib's rendering of the same model, compact), and as our shapes. Full tables and every counted artifact are in `src/Microsoft.OData.Mcp.Benchmarks/Reports/FORMATS.md`.

| What | CSDL XML | CSDL JSON | Ours (text) | Ours (JSON) |
|---|---:|---:|---:|---:|
| Northwind `Customer` (type, set) | 286 | 182 (-36%) | 88 (-69%) | 81 (-72%) |
| TripPin `Person` (type, 2 enums, 4 bound operations, set) | 726 | 558 (-23%) | 194 (-73%) | 199 (-73%) |
| Northwind, whole service (26 sets) | 4,802 | 3,718 (-23%) | 1,824 complete / 654 summary (-62% / -86%) | 1,648 (-66%) |
| TripPin, whole service (3 sets) | 1,859 | 1,602 (-14%) | 346 complete / 91 summary (-81% / -95%) | 349 (-81%) |

CSDL JSON saves a quarter to a third over XML and nothing more, because it still spells out every `$Kind`, `$Type`, and `$Nullable`. The shapes save two thirds to three quarters at the same level of detail, and the `Person` row is the one to quote: it carries the enums and the bound operations that a model needs before it can filter or call anything, and it is still a quarter of the XML.

### Input schemas

Typed named schemas are larger than the old `{ key, body: string }` by design; that is the trade the spec makes to move validation before HTTP.

| Baseline | Before | Current | Delta | Change |
|---|---:|---:|---:|---:|
| `northwind.create_customer.inputschema.json` | 126 | 113 | -13 | -10.3% |
| `northwind.update_customer.inputschema.json` | 26 | 118 | +92 | typed |
| `trippin.create_person.inputschema.json` | 128 | 167 | +39 | +30.5% |
| `trippin.update_person.inputschema.json` | 26 | 164 | +138 | typed |

The old `create_*` schemas already listed properties, so `create_customer` shrank and `create_person` grew only by what it now says: enum member lists, the collection `items`, and `null` on nullable types. The old `update_*` schemas were `{ key, body }` with no properties at all.

### Fixed per-session cost

What every session pays once, through `tools/list` and `initialize`, for the generic surface. Before values come from the Before aggregate plus the generic descriptions at `288ccd6`; there were no server instructions and no output schemas then. Descriptions and instructions are counted as plain text; schemas as JSON.

| Component | Before (10 generics) | Current (11 generics) | Delta |
|---|---:|---:|---:|
| Generic input schemas | 349 | 707 | +358 |
| Generic descriptions | 155 | 435 | +280 |
| `initialize.instructions` | 0 | 131 | +131 |
| Output schemas | 0 | 101 | +101 |
| **Total** | **504** | **1374** | **+870** |

Where the schema growth went:

| Generic schema | Before | Current | Delta | Why |
|---|---:|---:|---:|---|
| `odata_navigate` | 36 | 112 | +76 | key description; full `odata_query` option set advertised |
| `odata_describe_model` | — | 64 | +64 | new tool |
| `odata_call` | 37 | 96 | +59 | key description; `parameters` object with description; `additionalProperties:false` |
| `odata_update` | 36 | 84 | +48 | key description; `body` object with description |
| `odata_get` | 28 | 74 | +46 | key description; `select`, `expand` |
| `odata_delete` | 28 | 62 | +34 | key description |
| `odata_create` | 28 | 42 | +14 | `body` object with description |
| `odata_describe_type` | 31 | 44 | +13 | `format` enum; `name` description |
| `odata_query`, `odata_list_entity_sets`, `odata_list_operations` | 90 | 90 | 0 | unchanged |

Where the description growth went:

| Description | Before | Current | Delta |
|---|---:|---:|---:|
| `odata_call` | 23 | 130 | +107 |
| `odata_describe_model` | — | 75 | +75 |
| `odata_describe_type` | 21 | 69 | +48 |
| `odata_update` | 6 | 33 | +27 |
| `odata_create` | 8 | 33 | +25 |
| `odata_navigate` | 9 | 20 | +11 |
| `odata_get` | 6 | 14 | +8 |
| `odata_query`, `odata_delete` | 43 | 43 | 0 |
| `odata_list_operations` | 15 | 7 | -8 |
| `odata_list_entity_sets` | 24 | 11 | -13 |

### Net effect

Across the paired baselines the round removed 5,067 tokens (-49.7%), almost all of it from the per-call results a model actually spends on: the whole-service map is one call at a tenth of the old cost, `describe_type` is a third to a quarter of what it was, and `list_operations` no longer repeats bound operations that the type shape already lists.

The round also moved roughly 870 tokens into the fixed per-session cost, on purpose. On Northwind, a single `describe_model` call repays that six times over; on any service, four or five `describe_type` calls repay it.

The larger saving is not in these tables. The `odata_call` description (130 tokens) and the `key` and `body` descriptions exist to prevent failed calls: a wrong payload shape, a guessed key literal, a stringified body, an enum member without its qualified name. Each avoided failure saves a round trip whose error text alone is typically 100 to 300 tokens plus the retry. Both model reviews produced correct first calls for the scenarios those descriptions cover, and both independently guessed on the two items (key literal form, store-generated keys) that had no description at the time. Those are now described.

### Guard

`TokenBaselineTests.PairedToolResults_DoNotCostMoreThanBefore` fails the build if any paired tool result in `Current/` costs more tokens than its `Before/` counterpart. Input schemas, the `tools.list` aggregate, and the `describe_type` text stubs are excluded for the reasons given above.

### Not measured in this round

- **Named tools beyond the four schemas above.** `list_*`, `get_*`, and `delete_*` are not in the baselines. The review fixes added roughly 15 to 20 tokens per entity set: the enum filter hint on `list_*` (only when the type has enums) and the type-specific key description on `get_*`. On Northwind's 26 sets that is on the order of 400 to 500 tokens per session.
- **Failed-call avoidance.** The claim above is qualitative. A live harness that counts round trips per scenario would make it a number.
- **Other tokenizers.** `cl100k_base` is the spec's choice. Claude and Gemini tokenizers differ, mostly in the same direction.

### Method

`Microsoft.OData.Mcp.Benchmarks` tokenizes every file in both folders with SharpToken 2.0.3 `GptEncoding.GetEncoding("cl100k_base")`; the raw output is that project's `Reports/TOKENS.md`. The `Before/` files that the first-task generator did not produce were written by running the `288ccd6` build of `Microsoft.OData.Mcp.Core` against the live services through its public `ODataToolRuntime.InvokeAsync`; every file that already existed came out byte-identical from that run. The Before descriptions were read from `ODataMcpCatalog.BuildGenericTools` at the same commit. The `$metadata` counts are the live CSDL documents tokenized as fetched.
