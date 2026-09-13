# Type-shape baselines

Two folders, one file name per payload, compared by `TypeShapeBaselineTests` and guarded for token growth by `TokenBaselineTests`. The token tables (`TOKENS.md`, `FORMATS.md`) are produced by `src/Microsoft.OData.Mcp.Benchmarks` (`dotnet run -c Release -- --tokens`), which links these folders into its output. Spec: `specs/v3/OPTIMIZATION.md` §7.

| Folder | Role |
|---|---|
| `Current/` | What the tests assert. Regenerated after every intended change through the Breakdance ritual (uncomment `[DataRow]` and `[TestMethod]` on the generator, run it, comment them again). |
| `Before/` | Pre-optimization payloads. Write-once. The generator only fills it when the folder is empty, so nothing produced after the optimization can land here. |

## Where `Before/` came from

Every `Before/` file is the output of the code at commit `288ccd6` (the last commit before the optimization tasks) run against live Northwind and TripPin:

- The first seven files were written by the generator at the first task commit.
- The rest were produced later by running the `288ccd6` build directly, using `odata_list_entity_sets`, `odata_describe_type`, `odata_list_operations`, and the named `create_*` / `update_*` schemas of that build. Every file that already existed came out byte-identical from that run, which is the evidence that the reproduction is faithful.

Three kinds of `Before/` file need a word of explanation:

- **`*.describe_model.*.txt`.** The old code had no `odata_describe_model`. The only way to get the whole-service map was `odata_list_entity_sets` followed by `odata_describe_type` for every set, so the Before payload is exactly that: the list result, then one describe result per set, in ordinal set order, one per line. TripPin's `summary` and `complete` Before files are the same bytes because the old tool had a single level of detail.
- **`*.describe_type.*.txt`.** The old tool put everything in `structuredContent` and returned only the bare type name as text (`Customer`, `Person`). The Current text form is the default declaration grammar. Compare the `.json` pair for a like-for-like number.
- **`northwind.tools.list.json`.** The Before aggregate holds only tool names and generic input schemas; the Current one also records descriptions, `initialize.instructions`, and output schemas because the generator grew with the tasks. `TOKENS.md` breaks that file down by section so the shared sections can be compared directly.

`TokenBaselineTests.PairedToolResults_DoNotCostMoreThanBefore` guards every paired tool result against growing back. Input schemas, the `tools.list` aggregate, and the `describe_type` text stubs are excluded for the reasons above.
