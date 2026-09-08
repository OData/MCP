# Testing — OData MCP Platform v3

**Status:** Living (authoritative)  
**Revised:** 2026-09-07

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
| `Microsoft.OData.Mcp.Tests.Authentication` | **OData 8 only** | Outbound OAuth unit tests (parser, discovery, cache, grants, handler) + the **secured** rich convention API driven through `ToolsMcpHost`. See §8. |
| `Microsoft.OData.Mcp.Tests.Authentication.Restier` | **OData 7 only** | The **secured** Restier API driven through `ToolsMcpHost`. See §8. |
| `Microsoft.OData.Mcp.Tests.Shared` | Must not pull OData 7 **and** 8 | Fixtures, live URLs, entities. If a shared helper needs AspNetCore.OData, split it. |
| `Microsoft.OData.Mcp.Tests.Shared.Authentication` | None (no OData package) | Local authorization server, JWT key material, outbound host fixture. See §8. |

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

---

## 8. Authenticated OData end to end (outbound OAuth, hop 2)

[`AUTHENTICATION.md`](./AUTHENTICATION.md) tests discovery and grants against a **static-CSDL protected resource** and drives only the startup handshake through `ToolsMcpHost`. That leaves the handler's real job — attaching and refreshing `Authorization: Bearer` on every **data** call — untested against a real OData service unless live credentials are present (`Assert.Inconclusive` otherwise). This section closes that gap. It is authoritative for the layout below; `AUTHENTICATION.md` §Testing defers to it.

### 8.1 Principle: link the tests, swap the base

The existing tool suites already assert what every OData tool must do against a real OData 8 convention host (`Tests.AspNetCore/Tools/Odata*HostTests.cs`, `NamedCrudHostTests.cs`) and a real OData 7 Restier host (`Tests.AspNetCore.Restier/Restier{GenericRead,GenericWrite,NamedCrud,Protocol}ToolTests.cs`). We do not write a second copy of those assertions. The test **files** are `<Compile Include="…" Link="…" />`-linked verbatim into the authenticated projects; only the **base class** changes.

For that to work, a tool test may touch its host **only through the base class**:

| Member | In-process suite (today) | Authenticated suite (linked copy) |
|--------|--------------------------|-----------------------------------|
| `Session()` / `Runtime()` / `InvokeAsync(...)` | `ODataMcpSessionFactory.Sessions["odata"]` (AspNetCore in-process executor) | `ToolsMcpHost` session: `RemoteODataExecutor` on the named `"OData"` client **with `ODataOutboundAuthHandler`**, wrapped in `CapturingODataExecutor` |
| `CreateClient()` | `TestServer.CreateClient()` | `TestServer.CreateClient()` **plus** a Bearer minted by the local authorization server, so the direct-OData sanity calls the tests make still succeed |
| `TestServer.Services` | Breakdance | Breakdance (the secured host) |

Direct `TestServer.CreateClient()` calls inside tool tests are therefore replaced by `CreateClient()` on the base, and each Restier tool test class derives from an extracted `RestierToolTestBase` instead of carrying its own host wiring and private `Session()` / `Runtime()`. This is a mechanical refactor; assertions do not change.

The authenticated project defines a class with the **same fully-qualified name and member surface** as the original base (`Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures.ConventionRichHost`, `Microsoft.OData.Mcp.Tests.AspNetCore.Restier.RestierToolTestBase`). The linked files compile against that class unchanged. Neither authenticated project references the in-process test assembly it links from, so there is no type clash.

### 8.2 Projects

| Project | OData in-process | Contents |
|---------|------------------|----------|
| `Microsoft.OData.Mcp.Tests.Shared.Authentication` | **None** (no OData package; `IsTestProject=false`) | `LocalAuthorizationServer` (Breakdance `TestServer`): RFC 8414 + OIDC metadata (v1 **and** `/v2.0` documents), RFC 9728 PRM with `200` / `401` (Graph trap) / `404` modes, RFC 8628 device authorization + token polling (`authorization_pending` → success, `slow_down`), authorization code + PKCE `S256` auto-approve, `client_credentials`, `refresh_token` with rotation, RFC 7591 DCR (on/off), RFC 8693/7523 endpoints for identity assertion, configurable access-token lifetime, and **per-endpoint hit counters**. JWT signing key + `TokenValidationParameters` the secured hosts share. `AutoApproveConsentPresenter` (completes the pending device-code / PKCE grant without a human). `OutboundToolsHostFixture`: builds a real `ToolsMcpHost` against a `TestServer` handler, leaving the CLI's own `LatchkeyTokenCache` registration in place and pointing it at a throwaway per-fixture directory (so a second `CreateSessionAsync` is a genuine restart), and wraps the runtime in `CapturingODataExecutor`. `BearerStampingHandler` (the client `CreateClient` returns) so a linked test that assigns its own placeholder `Authorization` still reaches the controller it is asserting on. |
| `Microsoft.OData.Mcp.Tests.Authentication` | **OData 8 only** | Outbound unit tests per `AUTHENTICATION.md` (parser, discovery incl. v1/v2 ordering, `LatchkeyTokenCache`, grants, handler, restart/refresh, redaction, elicitation). The **secured rich convention API**: linked controllers/stores/fixtures from `Tests.AspNetCore`, `AddJwtBearer` against the local AS key, and a startup-filter middleware (`SecuredResourceMiddleware`) that authenticates with the JwtBearer scheme and challenges for every request under `/odata`, so subclasses that build their own pipeline stay protected so every OData route — `$metadata` included — `401`s with `WWW-Authenticate: Bearer resource_metadata="…"` without editing linked controller source. Same-FQN `ConventionRichHost` backed by `OutboundToolsHostFixture`. Linked `Tools/Odata*HostTests.cs` + `NamedCrudHostTests.cs`. |
| `Microsoft.OData.Mcp.Tests.Authentication.Restier` | **OData 7 only** | The **secured Restier API**: linked `McpCustomerApi` / `McpProductApi` / contexts / entities / seed from `Tests.AspNetCore.Restier`, `AddJwtBearer` against the local AS key plus the same `SecuredResourceMiddleware` startup filter, so the Restier route is protected without touching Restier's own pipeline. Same-FQN `RestierToolTestBase` backed by `OutboundToolsHostFixture`. Linked `RestierGenericReadToolTests`, `RestierGenericWriteToolTests`, `RestierNamedCrudToolTests`, `RestierProtocolToolTests`. |

OData 7 and OData 8 still never share a process. `Tests.Shared.Authentication` has no OData dependency, so both authenticated projects may reference it.

**Not linked** (hop 1, host-only behavior): JSON-RPC protocol suites, multi-prefix, include/exclude prefix, rate limiting, paging, `EndpointDataSource` discovery, `ODataMcpSessionFactory` tests. Hop-1 JSON-RPC calls that appear inside an otherwise-linked file run against the secured AspNetCore MCP endpoint (which requires the same Bearer); they are a regression check, not outbound coverage.

### 8.3 How the CLI reaches a `TestServer`

`ToolsMcpHost.CreateAsync(serviceUrl, options, includeStdioMcp, cancellationToken, configureServices)` accepts an optional `Action<IServiceCollection>`. The fixture uses it to call `ConfigurePrimaryHttpMessageHandler(() => testServer.CreateHandler())` on the named `"OData"` client (secured API) and `"OAuth"` client (local AS). That is the real ASP.NET Core pipeline behind a real `HttpClient`; nothing is mocked. The same hook lets an embedding host add logging or telemetry, so it is product API, not a test seam. Kestrel on `127.0.0.1:0` remains an acceptable alternative for any test that needs real sockets.

### 8.4 What the authenticated run proves

1. Empty cache → `$metadata` `401` → discovery (PRM 200, or PRM 401/404 + `authorization_uri`) → advertised grant (device code by default) → `TokenContainer` in `LatchkeyTokenCache` → `$metadata` `200`.
2. Every linked tool test then runs with a Bearer attached by `ODataOutboundAuthHandler`; `CapturingODataExecutor` proves the request went out through the outbound path.
3. Fixture variants (same linked suite, different `LocalAuthorizationServer` settings): 20-second token so the suite crosses a **coalesced refresh** on the send path; File-backed cache so a second `ToolsMcpHost` **restart** issues zero token-endpoint POSTs; `--auth-token` / API key / Basic short-circuits; PKCE loopback and client credentials as their PRs land.
4. Hit counters assert **one** PRM GET on the Graph-trap 401, **zero** token POSTs while idle, **one** refresh POST when two calls race the skew window.

### 8.5 Forbidden here

- Copying assertions from the in-process suites instead of linking the files.
- Editing a linked test to make it pass under auth; fix the base class or the fixture.
- Adding `Microsoft.AspNetCore.OData` (any major) to `Tests.Shared.Authentication`.
- `[Ignore]` on an authenticated suite because a grant PR has not landed; use `Assert.Inconclusive` from the base class until it does, then remove it.
- Mocking `HttpClient`, the token endpoint, Latchkey, or MCP.
