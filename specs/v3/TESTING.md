# Testing — OData MCP Platform v3

**Status:** Living (authoritative)  
**Revised:** 2026-09-06

---

## 1. Non-negotiables

- **Never mock** `HttpClient`, OData, MCP, or metadata.
- Breakdance for DI and TestServer. MSTest + FluentAssertions.
- Live **Northwind** (read) and **TripPin** (read/write) from `https://services.odata.org`.
- Always pass `-c Debug` or `-c Release` to `dotnet`.
- Do not change `global.json` unless asked.

---

## 2. Why host tests cannot share a process

`Microsoft.AspNetCore.OData` **7** and **8** are the same package ID with breaking public types (`Microsoft.AspNet.OData.ODataController` vs `Microsoft.AspNetCore.OData.Routing.Controllers.ODataController`). Loading both in one test process fails at type load.

That is **not** a product decision that Restier must upgrade. It is a test isolation rule: **one OData hosting major version per test project / process.**

The product host must not `PackageReference` either version ([ODATA-HOSTING.md](./ODATA-HOSTING.md)). Test projects **do** reference the version of the **app under test**.

---

## 3. Test projects

| Project | OData hosting in-process | Purpose |
|---------|--------------------------|---------|
| `Microsoft.OData.Mcp.Tests.Core` | None | CSDL, Core EDM, catalogs, remote executor vs live Northwind/TripPin |
| `Microsoft.OData.Mcp.Tests.Tools` | None | CLI, stdio host, `shutdown_server`; live metadata |
| `Microsoft.OData.Mcp.Tests.AspNetCore` | **OData 8 only** | Convention `AddOData` / `AddRouteComponents`, `AddODataMcp`, auth, rate limiting, in-process HTTP executor |
| `Microsoft.OData.Mcp.Tests.AspNetCore.Restier` | **OData 7 only** | Restier `MapApiRoute` (endpoint routing) + MCP against that API |
| `Microsoft.OData.Mcp.Tests.Authentication` | None | JWT / claims helpers |
| `Microsoft.OData.Mcp.Tests.Shared` | Must not pull OData 7 **and** 8 | Fixtures, live URLs, entities. If a shared helper needs AspNetCore.OData, split it. |

Do **not** add Restier or `Microsoft.AspNetCore.OData` 7.\* to `Tests.AspNetCore`. Do **not** add `Microsoft.AspNetCore.OData` 8.\* to `Tests.AspNetCore.Restier`.

An OData 7 **convention** (non-Restier) suite may be added later as `Tests.AspNetCore.OData7` if needed. It still must not share a process with OData 8.

---

## 4. Restier (for now: OData 7 only)

Restier.AspNetCore is compiled against **Microsoft.AspNetCore.OData 7.\***. Until Restier ships an OData 8 host, **Restier is tested only in the OData 7 project.**

- Base type: `Microsoft.Restier.Breakdance.RestierBreakdanceTestBase<TApi>` (`D:\GitHub\RESTier\src\Microsoft.Restier.Breakdance`).
- Use **endpoint routing** (`useEndpointRouting: true`). Restier maps `{prefix}/{**ODataEndpointPath_{routeName}}`.
- Seed a small in-memory API (for example `Customers` with one row).
- `AddODataMcp()` in that process must not pull OData 8.
- Assert: GET `{prefix}/Customers` returns the seed; MCP `odata_query` on `{prefix}/mcp` returns the same payload via the in-process executor.
- Never `[Ignore]` Restier because of OData 8. If the test cannot run, the **project graph** is wrong — fix the graph.

When Restier supports OData 8, add a **new** test project (or TFM) for that cell. Do not merge Restier-on-7 and Restier-on-8 into `Tests.AspNetCore`.

---

## 5. OData 8 convention suite

`Tests.AspNetCore` remains the OData 8 app:

- `AspNetCoreBreakdanceTestBase`, `AddRouteComponents`, convention controllers.
- Discovery from `EndpointDataSource` (not `ODataOptions` as a required product dependency).
- Auth, rate limiting, malformed payloads, large catalogs, operations-only models — as specified for the host.

---

## 6. Shared and live tests

`Tests.Shared` holds `LiveOData.Northwind` / `TripPin` and CLR fixtures. It may reference **EdmLib 7.x** and at most **one** AspNetCore.OData major version. Prefer no AspNetCore.OData in Shared; keep convention model builders in the suite that owns that OData version.

Core and Tools tests never reference Restier or AspNetCore.OData.

---

## 7. Forbidden

- Mocking HttpClient, OData, MCP, or metadata.
- `[Ignore]` on Restier “until Restier moves to 8.”
- One test project that PackageReferences both OData 7 and OData 8.
- Asserting that Restier must upgrade for MCP to support it.
