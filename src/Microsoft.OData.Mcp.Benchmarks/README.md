# OData MCP Benchmarks

Two kinds of measurement for the OData MCP catalog, in one console app:

- **Compute** — BenchmarkDotNet suites for the hot paths: CSDL parse, catalog build, the describe/list tools, and the pre-HTTP validation on create and update.
- **Tokens** — how many `cl100k_base` tokens a model spends on what we send it, compared with what it used to spend and with the raw metadata formats.

Both run against the public Northwind and TripPin services, the same ones the tests use.

## Running

```bash
cd src/Microsoft.OData.Mcp.Benchmarks

# every compute suite (takes a while)
dotnet run -c Release

# one suite
dotnet run -c Release -- --filter *Describe*

# token reports (writes Reports/TOKENS.md, Reports/FORMATS.md, Reports/Formats/*)
dotnet run -c Release -- --tokens

# token reports somewhere else
dotnet run -c Release -- --tokens --out C:\temp\reports
```

BenchmarkDotNet results land in `BenchmarkDotNet.Artifacts/` (git-ignored). Token reports are committed under `Reports/` so the numbers are reviewable in pull requests.

## Compute suites

| Suite | Measures | Parameterized by |
|---|---|---|
| `CsdlParseBenchmarks` | `CsdlParser.ParseFromString` on a live `$metadata` document | service |
| `CatalogBuildBenchmarks` | `new ODataMcpCatalog(...)`: resources, tools, typed schemas, and (static model) every type shape | service, static vs dynamic model |
| `DescribeBenchmarks` | `odata_describe_type` (text, json), `odata_describe_model` (summary, complete text, complete json), `odata_list_entity_sets`, `odata_list_operations` | service |
| `WritePathBenchmarks` | `odata_create` (valid, missing required) and `odata_update` through a canned executor, so only validation and formatting are timed | service |

All suites use `[MemoryDiagnoser]`. Read **Mean** for time and **Allocated** for heap pressure.

## Token reports

| Report | What it says |
|---|---|
| [`Reports/TOKENS.md`](./Reports/TOKENS.md) | Every Breakdance baseline file, Before vs Current, plus a per-section breakdown of the `tools/list` aggregate. |
| [`Reports/FORMATS.md`](./Reports/FORMATS.md) | The same model as CSDL XML, CSDL JSON, and our shapes, for one entity type and for the whole service, with every counted artifact in `Reports/Formats/`. |

The baselines themselves live in [`../Microsoft.OData.Mcp.Tests.Core/Baselines/TypeShapes`](../Microsoft.OData.Mcp.Tests.Core/Baselines/TypeShapes) because the tests assert them. This project links them into its output (see the `Content` item in the project file) rather than copying, so a regenerated baseline is picked up on the next build. `Before/` provenance is documented in that folder's `README.md`.

The build-time guard that fails when a paired tool result grows is `TokenBaselineTests` in Tests.Core; this project only reports.

## Adding a suite

1. Add a class under `Compute/` with `[MemoryDiagnoser]`, a `[Params(LiveModels.Northwind, LiveModels.TripPin)]` property, and an async `[GlobalSetup]` that calls `LiveModels.LoadAsync`.
2. Keep everything that is not the measured call in setup. Use `NoopODataExecutor` for model-only tools and `CannedODataExecutor` for write paths.
3. `BenchmarkSwitcher` finds it by reflection; nothing to register.
