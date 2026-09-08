# Outbound OAuth (CLI → remote OData) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `odata-mcp start <url>` authenticates to a protected OData service by discovering its authorization server and completing an advertised OAuth grant, with tokens in an OS credential store, so no bearer is pasted at startup — and that path is proven by running the existing OData 7 and OData 8 tool suites against **secured** copies of their APIs through the CLI host.

**Architecture:** Core only surfaces raw `WWW-Authenticate`. `Microsoft.OData.Mcp.Authentication/Outbound` owns discovery (RFC 9728/8414), grants (RFC 8628/8252+7636/6749/7591/8693), a Latchkey-backed SDK `ITokenCache`, and a `DelegatingHandler` on the named `"OData"` `HttpClient` that attaches, refreshes (on the send path, no timer), and on 401 discovers/acquires. Tools wires flags, one `IHost` with two named clients (`"OData"` with handler, `"OAuth"` without), stderr consent, and mid-session URL elicitation. Tests: a real local authorization server + secured OData 8 and OData 7 hosts on Breakdance `TestServer`; the existing tool test files are **linked** into the authenticated projects and only their **base class** is swapped.

**Tech Stack:** .NET 10/9/8 multi-target, C# 14, ModelContextProtocol 2.2 (`ModelContextProtocol.Authentication` types), Latchkey 0.1.0, McMaster CommandLineUtils, MSTest v3 + Breakdance + FluentAssertions, Microsoft.AspNetCore.OData 8 (A8 suite), Restier on OData 7 (Restier suite), `System.IdentityModel.Tokens.Jwt` via `Microsoft.AspNetCore.Authentication.JwtBearer`.

**Spec:** `specs/v3/AUTHENTICATION.md` (rev 8) and `specs/v3/TESTING.md` §8. Read both before any task.

## Global Constraints

- **AUTH-1** Official MCP only. `ModelContextProtocol` `2.*` pinned. No `0.*-*`.
- **AUTH-2** Hop 2 only. Never wire SDK `ClientOAuthProvider`/`ClientOAuthOptions` onto `"OData"`. Reuse SDK **types** (`ProtectedResourceMetadata`, `ITokenCache`, `TokenContainer`, `DynamicClientRegistrationOptions/Response`, `AuthorizationCallbackContext`, `AuthorizationResult`, `ScopeSelectorDelegate`, `IdentityAssertionGrantProvider*`). Do not use obsolete `AuthorizationRedirectDelegate`.
- **AUTH-3** `Microsoft.OData.Mcp.Core` never `ProjectReference`s `Microsoft.OData.Mcp.Authentication`. Core may `using ModelContextProtocol.Authentication`.
- **AUTH-4** Outbound types live in namespace `Microsoft.OData.Mcp.Authentication.Outbound`, folder `src/Microsoft.OData.Mcp.Authentication/Outbound/`. `AspNetCore` never references that package.
- **AUTH-5** One class per file. No nested types in our source (source-generated `JsonSerializerContext` nesting is the compiler's).
- **AUTH-6** Protocol strings in one flat `ODataMcpAuthConstants` class, `const string` fields alphabetical, XML-doc'd, same shape as `ODataMcpCatalogConstants`.
- **AUTH-7** No new DI interfaces. Implement SDK `ITokenCache` as `LatchkeyTokenCache`; do not wrap it; no `IOutboundOAuthClient`.
- **AUTH-8** Fail first: `ArgumentException.ThrowIfNullOrWhiteSpace`, `ArgumentNullException.ThrowIfNull`, `is null` / `is not null`, `.IsNullOrWhiteSpace()`.
- **AUTH-9** Copyright header; normal (non-file-scoped) namespaces; single-line usings; newline before every `{`; `#region Fields / Properties / Constructors / Public Methods / Internal Methods / Private Methods` (blank lines around region lines; Private region holds nothing since only fields may be private); public → protected → internal; alphabetical within a visibility group; final `return` on its own line.
- **AUTH-10** XML docs on every public and internal API; `<param>` on one line; `<remarks>` last; `<example>`/`<code>` where useful.
- **AUTH-11** MCP is a passthrough. Do not inject OData query values the caller did not specify.
- **AUTH-12** Access/refresh tokens, `client_secret`, `device_code`, `code_verifier`, `id_token` never in logs, tool results, elicitation payloads, MCP config, or env values written by `add`.
- **AUTH-13** Challenge `client_id` → `OAuthChallenge.ResourceClientId` only. Never copied to `OutboundOAuthOptions.ClientId`.
- **AUTH-14** Well-known 401/403/404/unparseable are non-fatal (continue). Timeout/5xx: retry once, then fail with URL + status.
- **AUTH-15** Tests: Breakdance + real HTTP. No mocking of `HttpClient`, OData, MCP, Latchkey, Graph. No Arrange/Act/Assert comments. `Assert.Inconclusive` (never `[Ignore]`) when a prerequisite is absent. Run `dotnet test <project> -c Debug` sequentially. Solution is `src/Microsoft.OData.Mcp.slnx` (XML); never look for `.sln`.
- `global.json` is not touched. Test-project naming: `Microsoft.OData.Mcp.Tests.<Subject>`. `.csproj`: reference `ItemGroup`s first (`Reference`, `PackageReference`, `ProjectReference`), each kind in its own group, alphabetical.
- No Python, Node, or Perl. One-off logic: `dotnet run app.cs` (csharp-scripts skill).
- Commit only when the user asks. Never include a session link in commit messages.

---

## Phase 0 — Linked-suite scaffold (DEFERRED: executes after Phase 9 as a skeleton only)

**Sequencing ruling (2026-09-07):** the user asked for the *entire product implementation first* and the linked-suite test infrastructure only as a **scaffold** to gauge the approach. Execution order is therefore Task 0.1 (already done — mechanical, harmless) → Phases 1–9 → Phase 10 → then Tasks 0.2–0.6 as a skeleton (projects, same-FQN bases, linked `Compile` items, `Assert.Inconclusive` gates; `OutboundToolsHostFixture` may be wired live if 4b is already in place). Task 0.3's `LocalAuthorizationServer` + `Tests.Shared.Authentication` project are **not** deferred: the spec's PR 2 tests need a real local AS, so they are built as **Task 2.0** (below) using Task 0.3's Steps 1–3 and 5 verbatim; Task 0.3 Step 4 (`OutboundToolsHostFixture`) stays in the deferred scaffold.

Deliverable (when executed): both existing tool suites refactored so tests touch their host only through a base class (still green), two authenticated test projects with linked suites and same-FQN swapped bases, green sanity tests proving the secured hosts challenge and accept tokens.

### Task 0.1: A8 suite — route direct `TestServer.CreateClient()` through the base

**Files:**
- Modify: `src/Microsoft.OData.Mcp.Tests.AspNetCore/Fixtures/ConventionRichHost.cs`
- Modify: `src/Microsoft.OData.Mcp.Tests.AspNetCore/Tools/NamedCrudHostTests.cs`, `OdataCallHostTests.cs`, `OdataCreateHostTests.cs`, `OdataCreateUpdateDeleteHostTests.cs`, `OdataDeleteHostTests.cs`, `OdataDescribeTypeHostTests.cs`, `OdataGetHostTests.cs`, `OdataListEntitySetsHostTests.cs`, `OdataListOperationsHostTests.cs`, `OdataNavigateHostTests.cs`, `OdataQueryHostTests.cs`, `OdataUpdateHostTests.cs`

**Interfaces:**
- Produces: `internal virtual HttpClient CreateClient()` on `ConventionRichHost`; a non-virtual `internal HttpClient CreateClient()` on every class in those files that derives directly from `AspNetCoreBreakdanceTestBase` (`OperationsOnlyToolHost`, `FewTablesToolHost`, `WideModelToolHost`, `NamedCrudPagingHostTests`, `NamedCrudCapHostTests`, `OdataCreateWideHostTests`, `OdataDeleteAuthFixtureHostTests`).

- [ ] **Step 1: Add `CreateClient()` to `ConventionRichHost`** (Internal Methods region, alphabetical — after `CreateCapturingRuntime`):

```csharp
        /// <summary>
        /// Creates an HTTP client for direct OData calls against the host under test.
        /// </summary>
        /// <returns>
        /// The client.
        /// </returns>
        /// <remarks>
        /// Tool tests must use this instead of <c>TestServer.CreateClient()</c> so a linked copy of the
        /// test can attach credentials (see <c>specs/v3/TESTING.md</c> §8).
        /// </remarks>
        internal virtual HttpClient CreateClient()
        {
            return TestServer.CreateClient();
        }
```
Add `using System.Net.Http;` if missing.

- [ ] **Step 2: Replace calls** in the 12 files: `sed -i 's/TestServer\.CreateClient()/CreateClient()/g' <files>` from `src/Microsoft.OData.Mcp.Tests.AspNetCore/Tools/`. Do **not** touch `JsonRpcProtocolHostTests.cs` or `ShutdownAbsentHostTests.cs`.

- [ ] **Step 3: Add the same member (non-virtual, XML-doc'd) to each class in those files that derives from `AspNetCoreBreakdanceTestBase` directly.** Find them: `grep -n "class .* : AspNetCoreBreakdanceTestBase" Tools/*.cs`.

- [ ] **Step 4: Build and run the A8 suite:** `dotnet build src/Microsoft.OData.Mcp.Tests.AspNetCore -c Debug` then `dotnet test src/Microsoft.OData.Mcp.Tests.AspNetCore -c Debug --no-build`. Expected: same pass count as before the change (record the baseline first with `dotnet test ... -c Debug` on a clean tree).

### Task 0.2: Restier suite — extract `RestierToolTestBase`

**Files:**
- Create: `src/Microsoft.OData.Mcp.Tests.AspNetCore.Restier/RestierToolTestBase.cs`
- Modify: `RestierGenericReadToolTests.cs`, `RestierGenericWriteToolTests.cs`, `RestierNamedCrudToolTests.cs` (5 classes), `RestierProtocolToolTests.cs` (3 classes)

**Interfaces:**
- Produces (exact surface the authenticated project must mirror):

```csharp
namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier
{
    public abstract class RestierToolTestBase : RestierBreakdanceTestBase<McpCustomerApi>
    {
        internal readonly string _databaseName;                       // "{prefix}-{guid:N}"
        protected RestierToolTestBase(string databasePrefix);        // base(useEndpointRouting: true); sets AddRestierAction / MapRestierAction ("odata","odata"), EnsureCustomers seed
        [TestInitialize] public void Setup();                         // TestHostBuilder.ConfigureServices((_, s) => ConfigureServices(s)); TestSetup();
        [TestCleanup] public void TearDown();                          // TestTearDown();
        internal ODataMcpCatalog Catalog();                            // Session().Catalog
        internal virtual void ConfigureODataMcp(ODataMcpHostOptions options); // no-op
        internal virtual void ConfigureServices(IServiceCollection services);  // services.AddODataMcp(ConfigureODataMcp);
        internal HttpClient CreateClient();                            // TestServer.CreateClient()
        internal ODataToolRuntime Runtime();                           // Session().Runtime
        internal virtual ODataMcpSession Session();                    // TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"]
    }
}
```

- [ ] **Step 1: Write `RestierToolTestBase.cs`** with the members above (copyright header, regions, XML docs). Constructor body is the existing per-class wiring:

```csharp
            AddRestierAction = apiBuilder =>
            {
                apiBuilder.AddRestierApi<McpCustomerApi>(restierServices =>
                {
                    restierServices.AddEFCoreProviderServices<McpCustomerContext>((_, options) =>
                    {
                        options.UseInMemoryDatabase(_databaseName);
                    });
                    RestierTestSeed.EnsureCustomers(restierServices);
                });
            };
            MapRestierAction = routeBuilder =>
            {
                routeBuilder.MapApiRoute<McpCustomerApi>("odata", "odata");
            };
```

- [ ] **Step 2: Convert each test class.** For every class currently `: RestierBreakdanceTestBase<McpCustomerApi>` in the four files: change base to `RestierToolTestBase`; constructor becomes `: base("RestierRead")` (keep each class's existing prefix string); delete the `_databaseName` field, the `AddRestierAction`/`MapRestierAction` assignments, the `[TestInitialize] Setup` / `[TestCleanup] TearDown`, and the private `Session()` / `Runtime()` / `Catalog()` helpers. Variants: `services.AddODataMcp(options => options.Catalog.MaxNamedTools = 10)` → `internal override void ConfigureODataMcp(ODataMcpHostOptions options) { options.Catalog.MaxNamedTools = 10; }` (same for `ExcludeEntitySets`, `IncludeCreate`, `IncludeUpdate/Delete`). `RestierProtocolRateLimitTests` keeps `ApplicationBuilderAction = app => app.UseRateLimiter();` in its ctor and overrides `ConfigureServices` to `services.AddRateLimiter(...)` then `base.ConfigureServices(services)`. Replace inline `TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Catalog` with `Catalog()`; `TestServer.CreateClient()` with `CreateClient()` (`sed` over the four files).

- [ ] **Step 3: Build and run:** `dotnet test src/Microsoft.OData.Mcp.Tests.AspNetCore.Restier -c Debug`. Expected: same pass count as baseline. Also confirm no test class still declares `[TestInitialize]` (base owns it): `grep -c TestInitialize Restier*ToolTests.cs` → 0.

### Task 0.3: `Microsoft.OData.Mcp.Tests.Shared.Authentication` — local authorization server + secured-resource helpers

**Files:**
- Create: `src/Microsoft.OData.Mcp.Tests.Shared.Authentication/Microsoft.OData.Mcp.Tests.Shared.Authentication.csproj`
- Create: `LocalAuthorizationServer.cs`, `LocalAuthorizationServerOptions.cs`, `ProtectedResourceMetadataMode.cs`, `DeviceCodeGrantState.cs`, `AuthorizationCodeGrantState.cs`, `SecuredResourceMiddleware.cs`, `SecuredResourceStartupFilter.cs`, `SecuredResourceServiceCollectionExtensions.cs`, `OutboundToolsHostFixture.cs`
- Modify: `src/Microsoft.OData.Mcp.slnx` (add under `/Tests/`)

**Interfaces (Produces):**

```csharp
public enum ProtectedResourceMetadataMode { Ok, Unauthorized, NotFound }

public sealed class LocalAuthorizationServerOptions
{
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromHours(1);
    public bool EnableDynamicClientRegistration { get; set; }
    public int PendingPollsBeforeSuccess { get; set; } = 1;   // used only when RequireApproval == false
    public ProtectedResourceMetadataMode ProtectedResourceMetadataMode { get; set; } = ProtectedResourceMetadataMode.Ok;
    public bool PublishVersionedDocuments { get; set; } = true; // also publish v1 doc whose token endpoint rejects everything
    public bool RequireApproval { get; set; } = true;          // device/authorize succeed only after Approve()/GET verification_uri
    public string ResourceUri { get; set; } = "http://localhost";
    public List<string> Scopes { get; } = ["read", "write", "offline_access"];
    public bool SlowDownOnce { get; set; }
}

public sealed class LocalAuthorizationServer : IDisposable
{
    public LocalAuthorizationServer(LocalAuthorizationServerOptions options);   // builds the TestServer immediately
    public Uri AuthorizationEndpoint { get; }      // http://localhost/oauth/v2.0/authorize
    public Uri DeviceAuthorizationEndpoint { get; } // http://localhost/oauth/v2.0/devicecode
    public HttpMessageHandler Handler { get; }      // Server.CreateHandler()
    public Uri Issuer { get; }                     // http://localhost/oauth/v2.0
    public Uri LegacyIssuer { get; }               // http://localhost/oauth   (v1 document; its token endpoint always 400s)
    public LocalAuthorizationServerOptions Options { get; }
    public Uri ProtectedResourceMetadataUri { get; } // http://localhost/.well-known/oauth-protected-resource/odata
    public Uri RegistrationEndpoint { get; }       // http://localhost/oauth/v2.0/register
    public TestServer Server { get; }
    public byte[] SigningKey { get; }              // 32 random bytes per instance
    public Uri TokenEndpoint { get; }              // http://localhost/oauth/v2.0/token
    public IReadOnlyDictionary<string, string>? LastTokenRequest { get; } // form fields of the last POST /token
    public void Approve(string userCode);          // approves a pending device code (same as GET verification_uri)
    public TokenValidationParameters CreateTokenValidationParameters(); // key, ValidIssuers [Issuer, LegacyIssuer], ValidAudience Options.ResourceUri
    public int HitCount(string endpoint);          // "prm", "metadata-v1", "metadata-v2", "metadata-rfc8414-v2", "devicecode", "device-approve", "authorize", "token", "token:<grant_type>", "register"
    public string IssueAccessToken(string scope);  // mints a valid JWT directly (for CreateClient in fixtures)
    public void RegisterClient(string clientId, string? clientSecret); // seeds confidential/public clients; "daemon"/"daemon-secret" and "cli" (public) pre-seeded
}

public static class SecuredResourceServiceCollectionExtensions
{
    // JwtBearer with the AS key; IStartupFilter prepending SecuredResourceMiddleware; everything under protectedPathPrefix
    // (default "/odata") must carry a valid Bearer or gets 401 + WWW-Authenticate: Bearer resource_metadata="<PRM uri>"
    public static IServiceCollection AddSecuredResource(this IServiceCollection services, LocalAuthorizationServer authorizationServer, string protectedPathPrefix = "/odata");
}

public sealed class OutboundToolsHostFixture : IDisposable
{
    public const string OutboundNotAvailableMessage = "ToolsMcpHost outbound OAuth is not implemented yet (specs/v3/AUTHENTICATION.md PR 4b); linked suite is scaffolded.";
    public static bool IsOutboundAvailable => false;        // flips to true in Task 4b.9
    public OutboundToolsHostFixture(LocalAuthorizationServer authorizationServer, TestServer resourceServer, Uri serviceRoot);
    public LocalAuthorizationServer AuthorizationServer { get; }
    public CapturingODataExecutor Capture { get; }           // set by CreateSessionAsync
    public TestServer ResourceServer { get; }
    public Uri ServiceRoot { get; }
    public ODataMcpSession Session { get; }                  // set by CreateSessionAsync
    public HttpClient CreateClient();                        // ResourceServer.CreateClient() + Authorization: Bearer IssueAccessToken("read write")
    public Task CreateSessionAsync(ODataMcpCatalogOptions catalogOptions, CancellationToken cancellationToken); // Phase 0: throw new NotSupportedException(OutboundNotAvailableMessage)
}
```

- [ ] **Step 1: csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFrameworks>net10.0;net9.0;net8.0</TargetFrameworks>
        <IsPackable>false</IsPackable>
        <IsTestProject>false</IsTestProject>
    </PropertyGroup>

    <ItemGroup>
        <FrameworkReference Include="Microsoft.AspNetCore.App" />
    </ItemGroup>

    <ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
        <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.*" />
        <PackageReference Include="Microsoft.AspNetCore.TestHost" Version="10.*" />
    </ItemGroup>

    <ItemGroup Condition="'$(TargetFramework)' == 'net9.0'">
        <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="9.*" />
        <PackageReference Include="Microsoft.AspNetCore.TestHost" Version="9.*" />
    </ItemGroup>

    <ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
        <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.*" />
        <PackageReference Include="Microsoft.AspNetCore.TestHost" Version="8.*" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\Microsoft.OData.Mcp.Authentication\Microsoft.OData.Mcp.Authentication.csproj" />
        <ProjectReference Include="..\Microsoft.OData.Mcp.Core\Microsoft.OData.Mcp.Core.csproj" />
        <ProjectReference Include="..\Microsoft.OData.Mcp.Tests.Shared\Microsoft.OData.Mcp.Tests.Shared.csproj" />
        <ProjectReference Include="..\Microsoft.OData.Mcp.Tools\Microsoft.OData.Mcp.Tools.csproj" />
    </ItemGroup>

</Project>
```

- [ ] **Step 2: `LocalAuthorizationServer`.** Build with `new TestServer(new WebHostBuilder().ConfigureServices(s => s.AddRouting()).Configure(app => { app.UseRouting(); app.UseEndpoints(MapEndpoints); }))`. Endpoints (all record a hit before anything else):
  - `GET /oauth/.well-known/openid-configuration` → v1 doc: `issuer` `LegacyIssuer`, `token_endpoint` `http://localhost/oauth/token` (that endpoint always returns 400 `{"error":"invalid_request","error_description":"v1 endpoint"}`), `authorization_endpoint`, `grant_types_supported` same list. Only when `PublishVersionedDocuments`.
  - `GET /oauth/v2.0/.well-known/openid-configuration` and `GET /oauth/v2.0/.well-known/oauth-authorization-server` → v2 doc: `issuer`, `authorization_endpoint`, `token_endpoint`, `device_authorization_endpoint`, `registration_endpoint` (only if `EnableDynamicClientRegistration`), `grant_types_supported` `["authorization_code","client_credentials","refresh_token","urn:ietf:params:oauth:grant-type:device_code","urn:ietf:params:oauth:grant-type:jwt-bearer"]`, `code_challenge_methods_supported ["S256"]`, `token_endpoint_auth_methods_supported ["client_secret_post","client_secret_basic"]`, `scopes_supported` = `Options.Scopes`, `response_types_supported ["code"]`, `authorization_response_iss_parameter_supported true`.
  - `GET /.well-known/oauth-protected-resource` and `GET /.well-known/oauth-protected-resource/odata` → by mode: `Ok` → `{"resource":"<ResourceUri>","authorization_servers":["<Issuer>"],"scopes_supported":["read","write","offline_access"],"bearer_methods_supported":["header"]}`; `Unauthorized` → 401, `WWW-Authenticate: Bearer realm="", authorization_uri="http://localhost/oauth/v2.0/authorize", client_id="00000003-0000-0000-c000-000000000000"`, body `{"error":{"code":"InvalidAuthenticationToken"}}`; `NotFound` → 404.
  - `POST /oauth/v2.0/devicecode` (form `client_id`, `scope`) → create `DeviceCodeGrantState { DeviceCode = Guid N, UserCode = 8 upper alnum, Scope, ClientId, Approved = false, Polls = 0 }`; return `{device_code,user_code,verification_uri:"http://localhost/oauth/v2.0/device",verification_uri_complete:"...?user_code=<code>",interval:1,expires_in:300}`.
  - `GET /oauth/v2.0/device?user_code=` → `Approve(userCode)`; 200 text `approved`.
  - `GET /oauth/v2.0/authorize` → require `response_type=code`, `code_challenge`, `code_challenge_method=S256`, `client_id`, `redirect_uri`, `state`; store `AuthorizationCodeGrantState { Code, CodeChallenge, RedirectUri, ClientId, Scope, Resource }`; respond `302 Location: {redirect_uri}?code=...&state=...&iss={Issuer}`.
  - `POST /oauth/v2.0/token` (form) → record `LastTokenRequest`, hit `token` and `token:{grant_type}`; branch:
    - `urn:ietf:params:oauth:grant-type:device_code`: unknown → 400 `invalid_grant`; `SlowDownOnce` and first poll → 400 `slow_down`; not approved and `RequireApproval` → 400 `authorization_pending`; not approved and `!RequireApproval` → `Polls++`, pending until `Polls > PendingPollsBeforeSuccess`; approved → tokens.
    - `authorization_code`: verify `code`, `redirect_uri` equal, `Base64Url(SHA256(code_verifier)) == CodeChallenge`; consume code → tokens.
    - `client_credentials`: client id/secret from form or Basic header; must match `RegisterClient` entry with a secret → tokens without refresh.
    - `refresh_token`: must exist; rotate (remove old, issue new) → tokens.
    - `urn:ietf:params:oauth:grant-type:jwt-bearer`: Task 9.x adds; until then 400 `unsupported_grant_type`.
    - Tokens: JWT via `JwtSecurityTokenHandler` (`SymmetricSecurityKey(SigningKey)`, HS256): `iss` = `Issuer`, `aud` = form `resource` if present else `Options.ResourceUri`, `sub` = client id, `scope` claim, `exp` = now + `AccessTokenLifetime`, `jti`. JSON: `access_token`, `token_type "Bearer"`, `expires_in` (seconds), `scope`, `refresh_token` only when granted scope contains `offline_access`.
  - `POST /oauth/v2.0/register` (only if enabled; else 404) → `{"client_id":"dcr-<guid>","client_id_issued_at":<unix>,"redirect_uris":[...echo],"grant_types":[...echo],"token_endpoint_auth_method":"none"}` and `RegisterClient(id, null)`.
  - State: `ConcurrentDictionary`s; counters `ConcurrentDictionary<string,int>`; `LastTokenRequest` guarded by a lock.

- [ ] **Step 3: `SecuredResourceMiddleware`** (constructor `(RequestDelegate next, string protectedPathPrefix)`): if `context.Request.Path.StartsWithSegments(prefix)`: `var result = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme)`; if `!result.Succeeded` → `await context.ChallengeAsync(JwtBearerDefaults.AuthenticationScheme); return;` else `context.User = result.Principal`. Then `await _next(context)`. `SecuredResourceStartupFilter : IStartupFilter` returns `next => app => { app.UseMiddleware<SecuredResourceMiddleware>(prefix); next(app); }`. `AddSecuredResource` registers `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => { o.TokenValidationParameters = authorizationServer.CreateTokenValidationParameters(); o.Challenge = $"Bearer resource_metadata=\"{authorizationServer.ProtectedResourceMetadataUri}\""; })`, `AddAuthorization()`, and `services.AddTransient<IStartupFilter>(_ => new SecuredResourceStartupFilter(protectedPathPrefix))`.

- [ ] **Step 4: `OutboundToolsHostFixture`** as specified (Phase 0 body of `CreateSessionAsync` throws `NotSupportedException(OutboundNotAvailableMessage)`; `Session`/`Capture` throw `InvalidOperationException` if read before creation).

- [ ] **Step 5: Add to `src/Microsoft.OData.Mcp.slnx`** under `<Folder Name="/Tests/">`: `<Project Path="Microsoft.OData.Mcp.Tests.Shared.Authentication/Microsoft.OData.Mcp.Tests.Shared.Authentication.csproj" />`. Build: `dotnet build src/Microsoft.OData.Mcp.Tests.Shared.Authentication -c Debug`.

### Task 0.4: `Microsoft.OData.Mcp.Tests.Authentication` — secured OData 8 host + linked A8 suite + sanity tests

**Files:**
- Modify: `src/Microsoft.OData.Mcp.Tests.Authentication/Microsoft.OData.Mcp.Tests.Authentication.csproj`
- Create: `Fixtures/ConventionRichHost.cs` (namespace `Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures`), `LocalAuthorizationServerTests.cs`, `SecuredConventionHostTests.cs`

- [ ] **Step 1: csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFrameworks>net10.0;net9.0;net8.0</TargetFrameworks>
        <IsPackable>false</IsPackable>
        <NoWarn>$(NoWarn);NU1608</NoWarn>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Breakdance.AspNetCore" Version="8.*-*" />
        <PackageReference Include="Microsoft.AspNetCore.OData" Version="8.*" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\Microsoft.OData.Mcp.AspNetCore\Microsoft.OData.Mcp.AspNetCore.csproj" />
        <ProjectReference Include="..\Microsoft.OData.Mcp.Authentication\Microsoft.OData.Mcp.Authentication.csproj" />
        <ProjectReference Include="..\Microsoft.OData.Mcp.Core\Microsoft.OData.Mcp.Core.csproj" />
        <ProjectReference Include="..\Microsoft.OData.Mcp.Tests.Shared\Microsoft.OData.Mcp.Tests.Shared.csproj" />
        <ProjectReference Include="..\Microsoft.OData.Mcp.Tests.Shared.Authentication\Microsoft.OData.Mcp.Tests.Shared.Authentication.csproj" />
        <ProjectReference Include="..\Microsoft.OData.Mcp.Tools\Microsoft.OData.Mcp.Tools.csproj" />
    </ItemGroup>

    <!-- Linked verbatim from the in-process OData 8 suite; only ConventionRichHost is redefined here (TESTING.md §8). -->
    <ItemGroup>
        <Compile Include="..\Microsoft.OData.Mcp.Tests.AspNetCore\CustomersController.cs" Link="Linked\CustomersController.cs" />
        <Compile Include="..\Microsoft.OData.Mcp.Tests.AspNetCore\Fixtures\DocumentsController.cs" Link="Linked\Fixtures\DocumentsController.cs" />
        <Compile Include="..\Microsoft.OData.Mcp.Tests.AspNetCore\Fixtures\FlagsController.cs" Link="Linked\Fixtures\FlagsController.cs" />
        <Compile Include="..\Microsoft.OData.Mcp.Tests.AspNetCore\Fixtures\McpJsonRpc.cs" Link="Linked\Fixtures\McpJsonRpc.cs" />
        <Compile Include="..\Microsoft.OData.Mcp.Tests.AspNetCore\Fixtures\OperationControllers.cs" Link="Linked\Fixtures\OperationControllers.cs" />
        <Compile Include="..\Microsoft.OData.Mcp.Tests.AspNetCore\Fixtures\OrderDetailsController.cs" Link="Linked\Fixtures\OrderDetailsController.cs" />
        <Compile Include="..\Microsoft.OData.Mcp.Tests.AspNetCore\Fixtures\OrdersController.cs" Link="Linked\Fixtures\OrdersController.cs" />
        <Compile Include="..\Microsoft.OData.Mcp.Tests.AspNetCore\Fixtures\StatusControllers.cs" Link="Linked\Fixtures\StatusControllers.cs" />
        <Compile Include="..\Microsoft.OData.Mcp.Tests.AspNetCore\Fixtures\WidgetsController.cs" Link="Linked\Fixtures\WidgetsController.cs" />
        <Compile Include="..\Microsoft.OData.Mcp.Tests.AspNetCore\Paging\*.cs" Link="Linked\Paging\%(Filename)%(Extension)" />
        <Compile Include="..\Microsoft.OData.Mcp.Tests.AspNetCore\RateLimit\*.cs" Link="Linked\RateLimit\%(Filename)%(Extension)" />
        <Compile Include="..\Microsoft.OData.Mcp.Tests.AspNetCore\Security\McpJsonContent.cs" Link="Linked\Security\McpJsonContent.cs" />
        <Compile Include="..\Microsoft.OData.Mcp.Tests.AspNetCore\Security\TestJwt.cs" Link="Linked\Security\TestJwt.cs" />
        <Compile Include="..\Microsoft.OData.Mcp.Tests.AspNetCore\Tools\NamedCrudHostTests.cs" Link="Linked\Tools\NamedCrudHostTests.cs" />
        <Compile Include="..\Microsoft.OData.Mcp.Tests.AspNetCore\Tools\Odata*HostTests.cs" Link="Linked\Tools\%(Filename)%(Extension)" />
    </ItemGroup>

</Project>
```
If a linked file fails to compile because of a `<see cref>` into a type that is not linked (e.g. `Restier.McpCustomerApi`), link the file that defines it or change the cref in the **source** file to `<c>` text — do not fork the linked file.

- [ ] **Step 2: Swapped `ConventionRichHost`** (same FQN; keeps every member the original exposes: `ConfigureApp`, `ConfigureServices`, `CreateCapturingRuntime`, `CreateClient`, `InvokeAsync`, `Session`):

```csharp
    public abstract class ConventionRichHost : AspNetCoreBreakdanceTestBase
    {
        internal LocalAuthorizationServer? AuthorizationServer { get; set; }
        internal OutboundToolsHostFixture? Outbound { get; set; }

        [TestInitialize]
        public void Setup()
        {
            AuthorizationServer = new LocalAuthorizationServer(CreateAuthorizationServerOptions());
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services.AddSecuredResource(AuthorizationServer);
                ConfigureServices(services);
            });
            AddMinimalMvc();
            TestHostBuilder.ConfigureWebHost(web => web.Configure(app => ConfigureApp(app)));
            TestSetup();
            Outbound = new OutboundToolsHostFixture(AuthorizationServer, TestServer, new Uri("http://localhost/odata/"));
            if (!OutboundToolsHostFixture.IsOutboundAvailable)
            {
                Assert.Inconclusive(OutboundToolsHostFixture.OutboundNotAvailableMessage);
            }
            Outbound.CreateSessionAsync(HostCatalogOptions(), CancellationToken.None).GetAwaiter().GetResult();
        }

        [TestCleanup]
        public void TearDown()
        {
            Outbound?.Dispose();
            TestTearDown();
            AuthorizationServer?.Dispose();
        }

        internal virtual void ConfigureApp(IApplicationBuilder app)            // identical to original (UseRouting, MapControllers, UseODataMcp)
        internal virtual void ConfigureServices(IServiceCollection services)   // identical to original
        internal virtual LocalAuthorizationServerOptions CreateAuthorizationServerOptions() => new();
        internal (ODataToolRuntime Runtime, CapturingODataExecutor Capture) CreateCapturingRuntime() => (Session().Runtime, Outbound!.Capture);
        internal virtual HttpClient CreateClient() => Outbound!.CreateClient();
        internal ODataMcpCatalogOptions HostCatalogOptions() => TestServer.Services.GetRequiredService<IOptions<ODataMcpHostOptions>>().Value.Catalog;
        internal Task<ODataToolInvocationResult> InvokeAsync(string name, IEnumerable<KeyValuePair<string, JsonElement>>? arguments = null) => Session().Runtime.InvokeAsync(name, arguments, CancellationToken.None);
        internal ODataMcpSession Session() => Outbound!.Session;
    }
```
`HostCatalogOptions()` is how a linked subclass's `services.AddODataMcp(o => o.Catalog.MaxNamedTools = 10)` reaches the outbound session: the AspNetCore host still registers the options; the fixture builds the outbound catalog from the same `ODataMcpCatalogOptions`.

- [ ] **Step 3: `LocalAuthorizationServerTests`** (all against `server.Server.CreateClient()`; green now):
  - `Metadata_V2Document_HasTokenEndpointUnderV2` — GET v2 openid-configuration → `token_endpoint` contains `/v2.0/`; RFC 8414 doc identical.
  - `Metadata_V1Document_TokenEndpointRejects` — POST v1 token → 400.
  - `DeviceCode_PendingUntilApproved_ThenIssuesRefreshToken` — start with `scope=read offline_access`; poll → `authorization_pending`; `Approve(user_code)`; poll → 200 with `refresh_token`; `HitCount("token:urn:ietf:params:oauth:grant-type:device_code")` is 2.
  - `DeviceCode_WithoutOfflineAccess_NoRefreshToken`.
  - `ClientCredentials_Daemon_IssuesToken` and `ClientCredentials_WrongSecret_401`.
  - `RefreshToken_Rotates_OldOneRejected`.
  - `Authorize_S256_RedirectsWithCodeStateIss_ThenExchanges` (compute verifier/challenge in the test; follow `Location` manually with `AllowAutoRedirect=false` handler via `server.Server.CreateHandler()`).
  - `Prm_Modes_ReturnExpected` (Ok 200 JSON / Unauthorized 401 with `authorization_uri` and Graph client_id / NotFound 404).
  - `Register_WhenDisabled_404_WhenEnabled_IssuesClientId`.

- [ ] **Step 4: `SecuredConventionHostTests : AspNetCoreBreakdanceTestBase`** (own setup: `AddSecuredResource` + rich model + controllers + `AddODataMcp`, pipeline `UseRouting/MapControllers/UseODataMcp`):
  - `Metadata_WithoutToken_401_WithResourceMetadataChallenge` — `WWW-Authenticate` contains `resource_metadata="http://localhost/.well-known/oauth-protected-resource/odata"`.
  - `Metadata_WithAsToken_200_Xml`.
  - `Customers_WithoutToken_401`, `Customers_WithToken_200_ContainsContoso`.
  - `Mcp_WithoutToken_401` (hop-1 endpoint under `/odata/mcp` is protected too).
  - `WrongAudienceToken_401` (mint with `aud` `http://localhost/odata` via a second `LocalAuthorizationServer` whose `ResourceUri` differs? No — call `IssueAccessToken` then assert a token minted for another server instance (different key) is rejected).

- [ ] **Step 5: Build + run:** `dotnet test src/Microsoft.OData.Mcp.Tests.Authentication -c Debug`. Expected: every linked tool test **Inconclusive** with the PR 4b message; `LocalAuthorizationServerTests` and `SecuredConventionHostTests` **pass**.

### Task 0.5: `Microsoft.OData.Mcp.Tests.Authentication.Restier` — secured Restier host + linked suite + sanity

**Files:**
- Create: `src/Microsoft.OData.Mcp.Tests.Authentication.Restier/Microsoft.OData.Mcp.Tests.Authentication.Restier.csproj`, `RestierToolTestBase.cs` (namespace `Microsoft.OData.Mcp.Tests.AspNetCore.Restier`), `SecuredRestierHostTests.cs`
- Modify: `src/Microsoft.OData.Mcp.slnx`

- [ ] **Step 1: csproj** — same package/TFM shape as `Tests.AspNetCore.Restier` (Breakdance.AspNetCore `8.*-*`, `Microsoft.EntityFrameworkCore.InMemory` per TFM) plus ProjectReferences: AspNetCore, Authentication, Core, Tests.Shared, Tests.Shared.Authentication, Tools, `..\..\..\RESTier\src\Microsoft.Restier.Breakdance\Microsoft.Restier.Breakdance.csproj`, `..\..\..\RESTier\src\Microsoft.Restier.EntityFrameworkCore\Microsoft.Restier.EntityFrameworkCore.csproj`. Linked `Compile` from `..\Microsoft.OData.Mcp.Tests.AspNetCore.Restier\`: `McpCustomer.cs`, `McpCustomerApi.cs`, `McpCustomerContext.cs`, `McpOrder.cs`, `McpProduct.cs`, `McpProductApi.cs`, `McpProductContext.cs`, `RestierJsonContent.cs`, `RestierMcpJsonRpc.cs`, `RestierPartitionedLimiter.cs`, `RestierRateLimitBudget.cs`, `RestierTestSeed.cs`, `RestierGenericReadToolTests.cs`, `RestierGenericWriteToolTests.cs`, `RestierNamedCrudToolTests.cs`, `RestierProtocolToolTests.cs` (Link `Linked\%(Filename)%(Extension)`). Add to slnx `/Tests/`.

- [ ] **Step 2: Swapped `RestierToolTestBase`** — same ctor/members as Task 0.2; differences: `Setup()` creates `AuthorizationServer`, `TestHostBuilder.ConfigureServices((_, s) => { s.AddSecuredResource(AuthorizationServer); ConfigureServices(s); })`, `TestSetup()`, then `Outbound = new OutboundToolsHostFixture(AuthorizationServer, TestServer, new Uri("http://localhost/odata/"))`, Inconclusive gate, `CreateSessionAsync(HostCatalogOptions(), ...)`. `CreateClient()` → `Outbound.CreateClient()`. `Session()` → `Outbound.Session`. `HostCatalogOptions()` reads `IOptions<ODataMcpHostOptions>` as in 0.4.

- [ ] **Step 3: `SecuredRestierHostTests : RestierBreakdanceTestBase<McpCustomerApi>`** (own wiring + `AddSecuredResource`): `Metadata_WithoutToken_401_WithChallenge`, `Metadata_WithToken_200`, `Customers_WithToken_200_Contoso`.

- [ ] **Step 4: Build + run:** `dotnet test src/Microsoft.OData.Mcp.Tests.Authentication.Restier -c Debug`. Expected: linked suites Inconclusive; sanity tests pass.

### Task 0.6: Docs + full verification

- [ ] **Step 1:** Update `specs/v3/TESTING.md` §8.2 wording from "fallback authorization policy" to "a startup-filter middleware (`SecuredResourceMiddleware`) that challenges with the JwtBearer scheme for every request under `/odata`, so subclasses that build their own pipeline stay protected", and §8.3 unchanged.
- [ ] **Step 2:** `dotnet build src/Microsoft.OData.Mcp.slnx -c Debug` → 0 warnings/errors. Run `Tests.AspNetCore`, `Tests.AspNetCore.Restier`, `Tests.Authentication`, `Tests.Authentication.Restier` sequentially with `-c Debug`; report counts.
- [ ] **Step 3:** Stop and present the scaffold to the user for gauging (this is the checkpoint the user asked for).

---

## Phase 1 — PR 1: Core surfaces `WWW-Authenticate`

### Task 1.1: `ODataExecuteResult.WwwAuthenticate`

**Files:** Modify `src/Microsoft.OData.Mcp.Core/Execution/ODataExecuteResult.cs`; Create `src/Microsoft.OData.Mcp.Tests.Core/Execution/ODataExecuteResultTests.cs`.

**Interfaces:** `public IReadOnlyList<string> WwwAuthenticate { get; set; } = [];` and `internal static IReadOnlyList<string> ReadWwwAuthenticate(HttpResponseMessage response)`.

- [ ] Test (real `HttpResponseMessage`, no client): add two identical `WWW-Authenticate` header values with quoted params (`Bearer realm="", resource_metadata="https://x/.well-known/oauth-protected-resource"`) and one `Basic realm="r"`; `FromHttp` → `WwwAuthenticate` has exactly 2 entries, raw quoting preserved, order preserved. Second test: no header → empty list.
- [ ] Implement: `response.Headers.TryGetValues("WWW-Authenticate", out var values)` → `values.Distinct(StringComparer.Ordinal).ToList()`; never read `Headers.WwwAuthenticate`. `RemoteODataExecutor` untouched.
- [ ] `dotnet test src/Microsoft.OData.Mcp.Tests.Core -c Debug --filter ODataExecuteResultTests`.

---

## Phase 2 — PR 2: Authentication constants + challenge parser + discovery

All new files under `src/Microsoft.OData.Mcp.Authentication/Outbound/`, namespace `Microsoft.OData.Mcp.Authentication.Outbound`. Tests under `src/Microsoft.OData.Mcp.Tests.Authentication/Outbound/`.

### Task 2.0: `Microsoft.OData.Mcp.Tests.Shared.Authentication` + `LocalAuthorizationServer` (spec-required test AS)

Execute Task 0.3 **Steps 1, 2, 3, and 5** exactly as written there (csproj, `LocalAuthorizationServer` + options + enum + grant-state classes, `SecuredResourceMiddleware`/`StartupFilter`/`AddSecuredResource`, slnx entry). Skip Step 4 (`OutboundToolsHostFixture`) — it belongs to the deferred scaffold. Then add to `Microsoft.OData.Mcp.Tests.Authentication.csproj` the `ProjectReference`s to `Tests.Shared`, `Tests.Shared.Authentication`, and `Tools` (Task 0.4 Step 1's reference group, **without** the linked `Compile` items or the OData 8 package), and write `LocalAuthorizationServerTests` (Task 0.4 Step 3, verbatim) — green before any product code exists. Tasks 2.4–2.6, 3.1, 4b.x, 5.1, 6.1, 8.2, 9.1 use this server.

### Task 2.1: Package references + `ODataMcpAuthConstants`

- [ ] `Microsoft.OData.Mcp.Authentication.csproj`: `TreatWarningsAsErrors` → remove the `false` override (inherit `true`); add `<PackageReference Include="ModelContextProtocol" Version="2.*" />`, `Microsoft.Extensions.Http` per TFM (10/9/8 like Core), `Microsoft.Extensions.Logging.Abstractions 10.*`; `Description` as today.
- [ ] `ODataMcpAuthConstants` (static, flat, alphabetical, all XML-doc'd): the spec's normative excerpt **plus** `ApiKeyHeaderDefault`? — no (fail first). Add: `AccessTokenProperty = "access_token"`, `BasicScheme = "Basic"`, `ClientCredentialsGrantType`, `CodeChallengeMethodS256 = "S256"`, `DeviceCodeParameter = "device_code"`, `ErrorAuthorizationPending = "authorization_pending"`, `ErrorInvalidToken = "invalid_token"`, `ErrorParameter = "error"`, `ErrorDescriptionParameter = "error_description"`, `ErrorSlowDown = "slow_down"`, `ErrorExpiredToken = "expired_token"`, `ErrorAccessDenied = "access_denied"`, `GrantTypeParameter = "grant_type"`, `IdTokenEnvironmentVariable = "ODATA_MCP_ID_TOKEN"`, `ClientSecretEnvironmentVariable = "ODATA_MCP_CLIENT_SECRET"`, `JwtBearerGrantType = "urn:ietf:params:oauth:grant-type:jwt-bearer"`, `RealmParameter = "realm"`, `RefreshTokenProperty = "refresh_token"`, `TokenExchangeGrantType = "urn:ietf:params:oauth:grant-type:token-exchange"`, `V2Segment = "/v2.0"`, `AuthorizeSuffixes` (`static readonly string[] { "/oauth2/v2.0/authorize", "/oauth2/authorize", "/authorize" }`), `TokenRefreshSkew = TimeSpan.FromSeconds(30)`.
- [ ] Test `ODataMcpAuthConstantsTests`: names/values from the spec excerpt; `TokenRefreshSkew == 30s`; `MicrosoftGraphResourceAppId` is the Graph sentinel.

### Task 2.2: `OAuthChallenge` + `WwwAuthenticateParser`

**Interfaces:** `OAuthChallenge { string Scheme; string? Realm; string? Scope; Uri? ResourceMetadata; Uri? AuthorizationUri; string? Error; string? ErrorDescription; string? ResourceClientId; IReadOnlyDictionary<string,string> Parameters; }`; `WwwAuthenticateParser.Parse(string headerValue) → OAuthChallenge` (throws `ArgumentException` on whitespace; `FormatException` on malformed); `WwwAuthenticateParser.ParseAll(IEnumerable<string> values) → IReadOnlyList<OAuthChallenge>` (multiple challenges per header separated by `, scheme ` per RFC 9110 — split on `,` followed by a token then space then `=`-less token); `WwwAuthenticateParser.SelectBearer(IReadOnlyList<OAuthChallenge>) → OAuthChallenge?`.

- [ ] Tests: Graph challenge string → `Scheme Bearer`, `Realm ""`, `AuthorizationUri`, `ResourceClientId == MicrosoftGraphResourceAppId`; RFC 9728 challenge → `ResourceMetadata` Uri, `Scope "read"`; quoted values with escaped quotes; unquoted token values; `error="invalid_token"`; `Basic realm="x"` → `Scheme Basic`, no OAuth fields; two challenges in one header; whitespace → `ArgumentException`.

### Task 2.3: `AuthorizationServerMetadata` + `OutboundOAuthJsonContext`

**Interfaces:** `AuthorizationServerMetadata` (snake_case `[JsonPropertyName]`): `Issuer`, `AuthorizationEndpoint`, `TokenEndpoint`, `DeviceAuthorizationEndpoint`, `RegistrationEndpoint`, `RevocationEndpoint`, `GrantTypesSupported`, `ScopesSupported`, `ResponseTypesSupported`, `CodeChallengeMethodsSupported`, `TokenEndpointAuthMethodsSupported`, `ClientIdMetadataDocumentSupported (bool?)`, `AuthorizationResponseIssParameterSupported (bool?)`. Helper `bool AdvertisesGrant(string grantType)` (exact match, `device_code` also matches the URN). `OutboundOAuthJsonContext : JsonSerializerContext` with `[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, DefaultIgnoreCondition = WhenWritingNull)]` and `[JsonSerializable]` for `AuthorizationServerMetadata`, `ProtectedResourceMetadata` (SDK), `TokenContainer` (SDK), `DeviceAuthorizationResponse`, `TokenEndpointResponse`, `OAuthErrorPayload` (our tiny `error`/`error_description` DTO — one file), `DynamicClientRegistrationResponse` (SDK). Name clash: never `using ModelContextProtocol.Authentication;` in a file that also references ours by simple name; alias `using SdkAuth = ModelContextProtocol.Authentication;`.

- [ ] Tests: round-trip the local AS v2 document through the context; `AdvertisesGrant("device_code")` true for the URN.

### Task 2.4: `ProtectedResourceMetadataClient`

**Interfaces:** ctor `(IHttpClientFactory factory, ILogger<ProtectedResourceMetadataClient> logger)`; `Task<ProtectedResourceMetadata?> TryGetAsync(Uri url, CancellationToken)` — 200+parse → object; 401/403/404/parse failure → `null` with Information log `PRM {Status} at {Url}, continuing`; timeout/5xx → retry once → `OutboundDiscoveryException(url, status)` (new exception class file). Uses `factory.CreateClient(ODataMcpAuthConstants.OAuthHttpClientName)`, absolute URIs only (throw if relative).

- [ ] Tests against `LocalAuthorizationServer` (`"OAuth"` named client with `ConfigurePrimaryHttpMessageHandler(() => server.Handler)`): Ok → object with `AuthorizationServers[0] == Issuer`; Unauthorized → null and `HitCount("prm") == 1`; NotFound → null. 5xx path: a tiny extra `TestServer` in the test that returns 503 twice → exception mentions URL and 503, `HitCount == 2`.

### Task 2.5: `AuthorizationServerMetadataClient`

**Interfaces:** `Task<AuthorizationServerMetadata?> TryGetAsync(Uri documentUrl, CancellationToken)` (same non-fatal/5xx rules; returns null if no `token_endpoint`); `static IReadOnlyList<Uri> CandidateDocuments(Uri baseUri)` → in order: `{B}/v2.0/.well-known/oauth-authorization-server`, `{B}/v2.0/.well-known/openid-configuration`, `{B}/.well-known/oauth-authorization-server`, `{origin}/.well-known/oauth-authorization-server{path}` (path-prefixed, only when B has a path), `{B}/.well-known/openid-configuration`; `Task<AuthorizationServerMetadata?> DiscoverAsync(Uri baseUri, CancellationToken)` (first candidate with `token_endpoint`; each probe hit is logged).

- [ ] Tests: `CandidateDocuments(https://login.microsoftonline.com/common)` yields the 5 URLs in order; `DiscoverAsync(http://localhost/oauth)` against the local AS (v1 and v2 both 200) → selected `TokenEndpoint` contains `/v2.0/`; `PublishVersionedDocuments=false` → v2 doc served at `/oauth/.well-known/...`? (no — when false only the v2-path docs exist; then `DiscoverAsync(http://localhost/oauth/v2.0)` works and `DiscoverAsync(http://localhost/oauth)` returns null).

### Task 2.6: `OAuthDiscovery` + `OAuthDiscoveryResult`

**Interfaces:** `OAuthDiscoveryResult { OAuthChallenge? Challenge; ProtectedResourceMetadata? ProtectedResource; AuthorizationServerMetadata AuthorizationServer; Uri AuthorizationServerBase; IReadOnlyList<string> AdvertisedGrants; string? ChallengeScope; Uri Resource; }`. `OAuthDiscovery` ctor `(ProtectedResourceMetadataClient prm, AuthorizationServerMetadataClient asClient, ILogger<OAuthDiscovery>)`; `Task<OAuthDiscoveryResult> DiscoverAsync(Uri serviceRoot, IReadOnlyList<string> wwwAuthenticate, int statusCode, OutboundOAuthOptions options, CancellationToken)`; `static IReadOnlyList<Uri> AuthorizationServerBases(OAuthChallenge? challenge, ProtectedResourceMetadata? prm, Uri? authServerOverride)` (override → only; else PRM servers then every strip remainder of `authorization_uri` for `AuthorizeSuffixes`, deduped in order); `static Uri PrmCandidates(...)` (`resource_metadata` → else origin well-known → else path-prefixed). Failure messages verbatim from spec: `{status} with no WWW-Authenticate; pass --auth-token / --api-key-header / --auth-server.` and `Could not discover an authorization server; pass --auth-server.` (throw `OutboundDiscoveryException`). Resource (step 8b): PRM `resource` → `options.Resource` → origin of service root. Log challenge client_id as *resource app id* (Information) and Warning when it equals the Graph sentinel.

- [ ] Tests (local AS in each mode): PRM Ok → AS from PRM; PRM 401 + Graph-like challenge → bases `[.../oauth/v2.0]` computed from `authorization_uri "http://localhost/oauth/v2.0/authorize"` (strip `/authorize`) and discovery succeeds with exactly one PRM GET; PRM 404 + `authorization_uri` → succeeds; PRM 404 + no challenge + no `--auth-server` → throws with the exact message; empty `WWW-Authenticate` → exact message; `AuthorizationServerBases` for the Graph URL yields `https://login.microsoftonline.com/common` then `.../common/oauth2`; resource for `http://localhost/odata/` is `http://localhost` unless PRM says otherwise.

### Task 2.7: `OutboundAuthLogRedactor`

**Interfaces:** `static string Redact(string text)` — replaces values of JSON properties `access_token|refresh_token|client_secret|device_code|code_verifier|id_token` and any `Authorization: <scheme> <value>` with `***`; `static string DescribeToken(TokenContainer)` → `"type={TokenType} expiresIn={ExpiresIn} hasRefresh={bool} scope={Scope}"` (no token value). `Tests.Shared.Authentication/CapturingLoggerProvider.cs` (real `ILoggerProvider` collecting messages) is created here for reuse.

- [ ] Test: a `TokenContainer` with `AccessToken = "SECRET-ACCESS"` logged through `DescribeToken` → captured text lacks `SECRET-ACCESS`; `Redact` of a token JSON lacks the value.

---

## Phase 3 — PR 3: `LatchkeyTokenCache`

### Task 3.1

**Files:** `Outbound/LatchkeyTokenCache.cs`; csproj `<PackageReference Include="Latchkey" Version="0.1.0" />`; tests `Outbound/LatchkeyTokenCacheTests.cs`.

**Interfaces:** `public sealed class LatchkeyTokenCache : ITokenCache` — ctor `(Uri serviceRoot, string clientId, string? tokenCachePath, ILogger<LatchkeyTokenCache> logger)` and an internal ctor `(ILatchkey store, string key, ILogger)` for tests (InMemory backend); `static string ComputeKey(Uri serviceRoot, string clientId)` = lowercase hex SHA-256 of `serviceRoot.AbsoluteUri + "\n" + clientId`; `static LatchkeyOptions CreateOptions(string? tokenCachePath)` — `ServiceName = ODataMcpAuthConstants.LatchkeyServiceName`, `Backends = new BackendMap().For(OSPlatform.Windows, LatchkeyBackend.Dpapi).For(OSPlatform.OSX, LatchkeyBackend.MacOSKeychain).For(OSPlatform.Linux, LatchkeyBackend.SecretService).ForAll(LatchkeyBackend.File)`, `BackendOptions = [DpapiBackendOption.Default with { Path = dir }, FileBackendOption.Default with { Path = dir }]` where `dir` = `tokenCachePath` ?? (`%LocalAppData%/odata-mcp/tokens` on Windows, `$HOME/.local/share/odata-mcp/tokens` elsewhere); `static bool VerifyPersistence(string? tokenCachePath)` → `Latchkey.Latchkey.VerifyPersistence(CreateOptions(path))`; `GetTokensAsync` → `GetBytesAsync(key)` → deserialize `TokenContainer` via `OutboundOAuthJsonContext` (corrupt → delete key, return null, Warning without payload); `StoreTokensAsync` → serialize → `SetAsync(key, bytes)`; `Task ClearAsync(ct)`; `SemaphoreSlim(1,1)` around both. Never log payload.

- [ ] Tests: key is deterministic and differs by client id; round-trip on `LatchkeyBackend.InMemory` and on `File` in a temp dir (`ClientId`/`ClientSecret`/`AuthorizationServer` persisted); corrupt bytes → null + key removed; concurrent 20× `StoreTokensAsync`/`GetTokensAsync` never throw and last write wins; `VerifyPersistence(tempDir)` true; redactor test from 2.7 extended: store then read with a capturing logger → no token text.

---

## Phase 4a — PR 4a: `OutboundOAuthOptions`, `OutboundGrantKind`, CLI flags, `--auth-token` short-circuit

### Task 4a.1: Options + enum

**Interfaces:** `public enum OutboundGrantKind { DeviceCode, AuthorizationCode, ClientCredentials, IdentityAssertion, RefreshToken }` with a `static OutboundGrantKindParser.TryParse(string, out OutboundGrantKind)` (accepts `device_code`, `authorization_code`, `client_credentials`, `identity_assertion`, case-insensitive). `OutboundOAuthOptions` properties exactly per spec (alphabetical): `ApiKey, ApiKeyHeader, AuthServer (Uri?), AuthTimeout (TimeSpan = 300s), AuthToken, BasicPassword, BasicUser, ClientId, ClientMetadataDocumentUri (Uri?), ClientSecret, ConsentPresenter (Func<OutboundConsentRequest, CancellationToken, Task>?), Grant (OutboundGrantKind?), IdpClientId, IdpClientSecret, IdpIdTokenFile, IdpScope, IdpTokenEndpoint (Uri?), IdpUrl (Uri?), RedirectUri (Uri?), Resource (Uri?), ScopeSelector (ScopeSelectorDelegate?), Scopes (List<string>), TokenCachePath`; `bool HasExplicitCredentials => !IsNullOrWhiteSpace(AuthToken) || !IsNullOrWhiteSpace(ApiKey) || !IsNullOrWhiteSpace(BasicUser)`; `void Validate()` throwing `ArgumentException` with the spec's rules (API key w/o header; Basic user w/o password; `AuthTimeout <= 0`; undefined enum; identity assertion without IdP endpoint or id-token source; identity assertion without `IdpClientId`/`ClientId`); `static OutboundOAuthOptions FromEnvironment(OutboundOAuthOptions)` binds `ODATA_MCP_CLIENT_SECRET` / `ODATA_MCP_ID_TOKEN`-presence when unset. `OutboundConsentRequest` (`Url (Uri)`, `ElicitationId (string)`, `Message`, `UserCode?`, `VerificationUri?`, `Kind (OutboundGrantKind)`) and `OAuthConsentRequiredException` (same properties + `Request`) land here too (they are the presenter contract).

- [ ] Tests: each `Validate` rule; env binding; `TryParse` table.

### Task 4a.2: CLI flags on `StartCommand` / `TestCommand`

McMaster `[Option]`s for every flag in the spec table (`--client-id`, `--client-secret`, `--scopes`, `--auth-server`, `--resource`, `--grant`, `--redirect-uri`, `--token-cache`, `--auth-timeout`, `--api-key`, `--api-key-header`, `--basic-user`, `--basic-password`, `--client-metadata-document`, `--idp-url`, `--idp-token-endpoint`, `--idp-client-id`, `--idp-client-secret`, `--idp-scope`, `--idp-id-token-file`), new properties alphabetical after existing ones; `internal OutboundOAuthOptions BuildOptions()` on each command (shared helper `OutboundOptionsBinder.Bind(...)` in `Tools/Commands/OutboundOptionsBinder.cs` to avoid duplication) that maps, applies `FromEnvironment`, calls `Validate()`.

- [ ] `ToolsMcpHost.CreateAsync(string serviceUrl, OutboundOAuthOptions options, CancellationToken)` replaces the `authToken` overload (delete it). In 4a it still builds today's dual clients and pastes `options.AuthToken` as Bearer / `ApiKeyHeader` / Basic. `StartCommand.BuildHostAsync` and `TestCommand` call it. Update every Tests.Tools call site (`ToolsMcpHost.CreateAsync(LiveOData.Northwind, new OutboundOAuthOptions(), ...)`).
- [ ] Tests.Tools: `StartCommand_ParsesAllFlags` (McMaster `CommandLineApplication<StartCommand>` parse → `BuildOptions()` values), `ApiKeyWithoutHeader_Fails`, `Grant_AuthorizationCode_ThrowsNotImplementedUntilPr5` (assert message `Grant 'authorization_code' is not implemented in this build.` from `GrantSelector` placeholder throw — implemented in 4b; in 4a `BuildOptions` accepts it and `CreateAsync` throws when `options.Grant` is set to a not-yet-implemented kind), Northwind without flags still works.

---

## Phase 4b — PR 4b: unified host, two clients, handler, device code, refresh, authenticated suites go live

### Task 4b.1: Wire DTOs + grants (`DeviceAuthorizationResponse`, `TokenEndpointResponse`, `OAuthErrorPayload`, `DeviceCodeGrant`, `RefreshTokenGrant`, `TokenEndpointClient`)

**Interfaces:** `TokenEndpointResponse` (snake_case DTO) + `TokenContainer ToTokenContainer(string? previousRefreshToken, string? clientId, string? clientSecret, string authorizationServer, string? tokenEndpointAuthMethod)` (`ObtainedAt = DateTimeOffset.UtcNow`, keep previous refresh when omitted). `TokenEndpointClient` (ctor `IHttpClientFactory`, logger): `Task<TokenEndpointResponse> PostAsync(Uri tokenEndpoint, IReadOnlyDictionary<string,string> form, string? clientId, string? clientSecret, string authMethod, CancellationToken)` — `client_secret_post` adds fields, `client_secret_basic` sets header; non-2xx → parse `OAuthErrorPayload` → throw `OAuthTokenException(error, description, status)` (new file). `DeviceCodeGrant` (ctor `TokenEndpointClient`, logger): `Task<DeviceAuthorizationResponse> StartAsync(AuthorizationServerMetadata, string clientId, string? scope, Uri? resource, CancellationToken)` (uses `DeviceAuthorizationEndpoint`; if absent but grant advertised → Warning + `{Issuer}/devicecode` fallback); `Task<TokenEndpointResponse> PollAsync(AuthorizationServerMetadata, DeviceAuthorizationResponse, string clientId, CancellationToken)` honoring `interval`, `slow_down` (+5s), `authorization_pending`, `expired_token`/`access_denied` → throw. `RefreshTokenGrant.RefreshAsync(AuthorizationServerMetadata, TokenContainer current, Uri? resource, CancellationToken) → TokenContainer`.

- [ ] Tests (local AS): device flow with `RequireApproval=false, PendingPollsBeforeSuccess=2` → 3 token POSTs; `SlowDownOnce` → still succeeds; `expires_in` mapped, `ObtainedAt` ~now; refresh rotates and keeps previous refresh when omitted (AS variant: add `Options.OmitRotatedRefreshToken`); `resource` sent only when non-null; error payload → `OAuthTokenException.Error == "invalid_grant"`.

### Task 4b.2: `GrantSelector`, `ScopeResolver`

**Interfaces:** `GrantSelector.Select(OAuthDiscoveryResult discovery, OutboundOAuthOptions options, bool interactive) → OutboundGrantKind` per spec §Steps 6 (override must be advertised; unimplemented → `NotSupportedException("Grant '{x}' is not implemented in this build.")`; interactive default device code else auth code; non-interactive client credentials when secret present; identity assertion when IdP configured). `IsImplemented(kind)` uses a `static readonly HashSet<OutboundGrantKind> Implemented` that grows per PR. `ScopeResolver.Resolve(OAuthDiscoveryResult, OutboundOAuthOptions) → string?` per §Step 8 (challenge scope → PRM → `--scopes` → null; append `offline_access` when advertised via `scopes_supported` or `refresh_token` grant; then `ScopeSelector`; Warning if it removed `offline_access`).

- [ ] Tests: selection table; `--grant authorization_code` in 4b → not implemented message; scope ordering + `offline_access` append + selector removal warning (capturing logger).

### Task 4b.3: `OutboundOAuthClient`

**Interfaces:** ctor `(IHttpClientFactory, OutboundOAuthOptions, ITokenCache, OAuthDiscovery, ILoggerFactory)`; `Task<TokenContainer?> GetValidTokenAsync(CancellationToken)` (cache → remaining > skew → return; ≤ skew with refresh → coalesced refresh (`SemaphoreSlim`), store, return; else null); `Task<TokenContainer> AcquireAsync(Uri serviceRoot, IReadOnlyList<string> wwwAuthenticate, int status, bool interactiveAllowed, CancellationToken)` (discover → client id: `options.ClientId` ?? DCR (Task 6) ?? throw `Authorization server does not advertise dynamic client registration; pass --client-id.` → scope → grant; for interactive grants: `StartInteractiveGrantAsync` then, if `options.ConsentPresenter` is non-null, `await presenter(request, ct)` then `CompleteInteractiveGrantAsync`; if null → throw `OAuthConsentRequiredException(request)`); `Task<OutboundConsentRequest> StartInteractiveGrantAsync(...)` (device start → request with `Url = verification_uri_complete ?? verification_uri`, `UserCode`, new GUID `ElicitationId`; pending state in internal fields `_pendingDevice`, `_pendingDiscovery`, guarded by `_pendingGrant` `SemaphoreSlim(1,1)` — second Start throws `InvalidOperationException`); `Task<TokenContainer> CompleteInteractiveGrantAsync(CancellationToken)` (poll → map → store → clear pending; `finally` clears). Refresh failure → falls back to `AcquireAsync`. Everything logged through `OutboundAuthLogRedactor.DescribeToken`.

- [ ] Tests (local AS, `InMemoryTokenCache` from SDK or `LatchkeyTokenCache` InMemory): first acquire via device code with an auto-approving presenter (`AutoApproveConsentPresenter` in Tests.Shared.Authentication: `GET request.Url` through the AS handler → approves) → token stored with `refresh_token`; `GetValidTokenAsync` with 1h token → no token POST (`HitCount("token") unchanged`); 20-second token → exactly one refresh POST when 5 concurrent calls race; second `StartInteractiveGrantAsync` while pending → throws; presenter null → `OAuthConsentRequiredException` with `ElicitationId` GUID and no token fields.

### Task 4b.4: `ODataOutboundAuthHandler`

**Interfaces:** `sealed class ODataOutboundAuthHandler : DelegatingHandler` ctor `(OutboundOAuthClient client, OutboundOAuthOptions options, ILogger<ODataOutboundAuthHandler>)`. `SendAsync`: explicit credentials → attach (`Bearer`/`ApiKeyHeader`/`Basic base64`) → send, no OAuth. Else `GetValidTokenAsync` → attach if non-null → send. After response: (a) no `Authorization` sent and 401/403 → `AcquireAsync(root, WwwAuthenticate raw values, status, interactiveAllowed: options.ConsentPresenter is not null || true)` → clone request (new `HttpRequestMessage`, copy method/URI/headers/content via buffered bytes) → send once. (b) Bearer sent and 401 with `error="invalid_token"` (parse via `WwwAuthenticateParser`) → if this send did not already refresh, `ForceRefreshAsync` → retry once; else fall through to (a)-style acquire. (c) failure → exception propagates. Read `options.ConsentPresenter` at use time. `static HttpRequestMessage CloneAsync(HttpRequestMessage)` internal.

- [ ] Tests (local AS + a secured `TestServer` from `SecuredConventionHostTests` wiring, named clients with primary handlers): empty cache → `$metadata` GET 401 → device flow (auto presenter) → 200; handler never calls token endpoint when token fresh; API key → header name honored, no OAuth; Basic → `Authorization: Basic`; `invalid_token` with unexpired `ExpiresIn` (AS option `RevokeIssuedTokens()` so resource rejects) → one refresh + retry; `"OAuth"` client never has the handler (resolve both clients and assert handler chain via a request to PRM 401 producing exactly one hit).

### Task 4b.5: `StdioConsentPresenter` (Tools)

`Tools/Hosting/StdioConsentPresenter.cs`: `static Task PresentAsync(OutboundConsentRequest request, CancellationToken ct)` — writes to stderr `Sign in at {Url}` and `Code: {UserCode}` when present; tries `Process.Start` with `UseShellExecute` (browser) swallowing failures; returns. (The client awaits the presenter then completes the grant; blocking stdio at startup is expected.)

### Task 4b.6: `ToolsMcpSessionHolder` + unified `ToolsMcpHost.CreateAsync`

**Interfaces:** `public sealed class ToolsMcpSessionHolder { public ODataMcpSession? Session { get; set; } }`. `ToolsMcpHost`: props `Catalog`, `Host (IHost)`, `Session`, `ServiceRoot`; ctor `(IHost host, Uri serviceRoot, ODataMcpSession session)`; `public static Task<ToolsMcpHost> CreateAsync(string serviceUrl, OutboundOAuthOptions options, bool includeStdioMcp, CancellationToken cancellationToken, Action<IServiceCollection>? configureServices = null)` exactly per spec §Unify (one `Host.CreateApplicationBuilder()`; logging to stderr with `verbose` from `options`? — add `public bool Verbose` to `OutboundOAuthOptions`? No: pass `bool verbose` via a new `ToolsMcpHostOptions`? Keep minimal: `CreateAsync(..., bool includeStdioMcp, bool verbose, CancellationToken, Action<IServiceCollection>?)`); registers parser, `"OData"` (+handler, BaseAddress root) and `"OAuth"` (no handler), executor, options, `LatchkeyTokenCache` (keyed by root + `options.ClientId ?? "anonymous"`), discovery clients, `OutboundOAuthClient`, `ToolsMcpSessionHolder`; `AddMcpServer().WithStdioServerTransport().WithODataCatalogHandlers(resolveSession, listExtra, shutdown)` when `includeStdioMcp`; `configureServices?.Invoke(services)` **last** before `Build()`; `Latchkey` `VerifyPersistence` fail-first when no explicit credentials (`Credential store is not usable on this host; unlock the OS keyring or pass --token-cache.`); GET `$metadata` with `Accept: application/xml` through the factory; parse; catalog `RouteName "remote"`; session; assign holder. `BuildStdioHost` **deleted**. `StartCommand.BuildHostAsync` → `CreateAsync(..., includeStdioMcp: true, ...)` returns `toolsHost.Host`. `TestCommand` → `false`. `RemoteODataExecutor` sets `Accept: application/json` per request (Core change in this PR).
- [ ] Tests.Tools: rewrite `BuildStdioHost_*` as `CreateAsync_IncludeStdio_RegistersMcpServer` (resolve `McpServerOptions`/handlers from `Host.Services`); `CreateAsync_Test_NoMcpServer`; Northwind via `configureServices` capturing handler → `Accept` headers correct on `$metadata` (xml) and data (json).

### Task 4b.7: `OutboundToolsHostFixture` goes live + suites

`CreateSessionAsync(catalogOptions, ct)`: `options = new OutboundOAuthOptions { ClientId = "cli", ConsentPresenter = AutoApproveConsentPresenter.Create(AuthorizationServer).PresentAsync, Scopes = ["read","write"], TokenCachePath = <temp dir per fixture> }`; `Host = await ToolsMcpHost.CreateAsync(ServiceRoot.AbsoluteUri, options, includeStdioMcp: false, verbose: false, ct, services => { services.AddHttpClient(ODataMcpAuthConstants.ODataHttpClientName).ConfigurePrimaryHttpMessageHandler(() => ResourceServer.CreateHandler()); services.AddHttpClient(ODataMcpAuthConstants.OAuthHttpClientName).ConfigurePrimaryHttpMessageHandler(() => AuthorizationServer.Handler); services.AddSingleton<ITokenCache>(new LatchkeyTokenCache(... InMemory ...)); })`; then build the outbound session with the **host's** catalog options: `var model = new CsdlParser().ParseFromString(Host.Session.MetadataXml!)`, `catalog = new ODataMcpCatalog(model, catalogOptions)`, `Capture = new CapturingODataExecutor(new RemoteODataExecutor(Host.Host.Services.GetRequiredService<IHttpClientFactory>()))`, `Session = new ODataMcpSession(catalog, new ODataToolRuntime(catalog, Capture), Host.Session.MetadataXml)`. Flip `IsOutboundAvailable` to `true`; delete the Inconclusive gates in both swapped bases.
- [ ] Run `Tests.Authentication` and `Tests.Authentication.Restier`: all linked tests pass. Triage any failure by fixing the base/fixture or the product, never the linked file. Add fixture variants as subclasses of the swapped bases in `Tests.Authentication/Variants/`: `RefreshWindowConventionHost` (AS `AccessTokenLifetime = 20s` → assert `HitCount("token:refresh_token") >= 1` after any tool call), `RestartConventionHostTests` (File-backed cache; second `CreateSessionAsync` → `HitCount("token")` unchanged and `HitCount("devicecode") == 1`), `GraphTrapConventionHostTests` (PRM Unauthorized → `HitCount("prm") == 1`, tools still work), `ExplicitBearerConventionHostTests` (`AuthToken = IssueAccessToken(...)` → zero AS hits).

### Task 4b.8: Spec + inventory touch-ups for what shipped (`AUTHENTICATION.md` §Testing points at TESTING.md §8; note `configureServices` hook).

---

## Phase 5 — PR 5: Authorization code + PKCE + loopback

### Task 5.1: `LoopbackAuthorizationCallback` + `AuthorizationCodePkceGrant`

**Interfaces:** `LoopbackAuthorizationCallback : IDisposable` — `static LoopbackAuthorizationCallback Start(Uri? redirectOverride)` (`HttpListener` on `http://127.0.0.1:{free port}/callback/`), `Uri RedirectUri`, `Task<AuthorizationResult> WaitAsync(string expectedState, CancellationToken)` (validates exact `state`; returns SDK `AuthorizationResult { Code, State, Iss }`; writes a small HTML "You can close this window"). `AuthorizationCodePkceGrant`: `(Uri AuthorizationUri, string State, string CodeVerifier) Begin(AuthorizationServerMetadata, string clientId, string? scope, Uri? resource, Uri redirectUri)` (S256 challenge; fail if `code_challenge_methods_supported` exists without `S256`); `Task<TokenEndpointResponse> ExchangeAsync(AuthorizationServerMetadata, AuthorizationResult, string codeVerifier, string clientId, Uri redirectUri, Uri? resource, CancellationToken)` (RFC 9207: if `Iss` present it must equal `Issuer`). `OutboundOAuthClient` gains the auth-code branch in Start/Complete (pending fields `_pendingLoopback`, `_pendingVerifier`, `_pendingState`); `GrantSelector.Implemented` += `AuthorizationCode`.
- [ ] Tests: `AutoApproveConsentPresenter` for auth code: GET `request.Url` via AS handler with `AllowAutoRedirect=false`, then `new HttpClient()` GET on the returned `Location` (real socket to 127.0.0.1) → grant completes; wrong `state` → rejected; `iss` mismatch → rejected; `--redirect-uri` honored; secured A8 variant `PkceConventionHostTests` (options `Grant = AuthorizationCode`) runs a handful of linked tools green (subclass the swapped base and override `CreateOutboundOptions()` — add that virtual to the bases in this task).

---

## Phase 6 — PR 6: Client credentials + DCR + CIMD

### Task 6.1: `ClientCredentialsGrant`, `DynamicClientRegistrar`
**Interfaces:** `ClientCredentialsGrant.AcquireAsync(AuthorizationServerMetadata, string clientId, string clientSecret, string? scope, Uri? resource, CancellationToken) → TokenEndpointResponse` (`client_secret_post` if advertised else `client_secret_basic`). `DynamicClientRegistrar.RegisterAsync(AuthorizationServerMetadata, DynamicClientRegistrationOptions, Uri redirectUri?, IReadOnlyList<string> grantTypes, CancellationToken) → DynamicClientRegistrationResponse` (SDK types; POST on `"OAuth"`). `OutboundOAuthClient` client-id resolution: `--client-id` → DCR when `registration_endpoint` (persist `ClientId`/`ClientSecret` on the `TokenContainer`) → CIMD when `client_id_metadata_document_supported` and `--client-metadata-document` (client_id = document URI) → fail message. `Implemented` += `ClientCredentials`.
- [ ] Tests: daemon secret from env `ODATA_MCP_CLIENT_SECRET`; wrong secret error; DCR on (AS `EnableDynamicClientRegistration`) → no `--client-id` needed and second start reuses persisted client id (`HitCount("register") == 1`); DCR off + no client id → exact failure message; `ClientCredentialsConventionHostTests` variant.

---

## Phase 7 — PR 7: `add` wizard

### Task 7.1
Replace `PromptForAuthentication` with `PromptForAuthenticationMode()` returning `AddAuthChoice` (enum file: `None, Discover, DeviceCode, AuthorizationCode, ClientCredentials, ApiKey, Basic, PasteToken`) and `AddCommandAuthSettings` (one class: `ClientId`, `Scopes`, `Grant`, `ApiKey`, `ApiKeyHeader`, `BasicUser`, `BasicPassword`, `AuthToken`, `TokenCachePath`). `Discover` probes `$metadata` with the `"OAuth"`-style handler-free client, parses the challenge, runs `OAuthDiscovery`, and recommends a grant. `BuildMcpCommand(name, url, settings, scope, verbose)` emits `--client-id/--scopes/--grant`, `--api-key --api-key-header`, `--basic-user --basic-password`, `--token-cache` only when non-default, `--auth-token` only for `PasteToken`, **never** `--env`; prints the client-credentials env instruction and the API key/Basic warning verbatim from the spec. `TestConnection` uses the same `OutboundOAuthOptions` path via `ToolsMcpHost.CreateAsync(..., includeStdioMcp: false)`.
- [ ] Rewrite `AddCommandTests.OnExecuteAsync_BearerToken_IncludesAuth` → `OnExecuteAsync_PasteToken_IncludesAuthTokenOnlyWhenChosen` and `OnExecuteAsync_DeviceCode_EmitsClientIdAndGrant_NoToken`; `TestCommandMoreTests.AddCommand_DerivesNameAndBuildsCommand` asserts the new shape; client credentials → no `--env` and instruction text present; API key → warning text present.

---

## Phase 8 — PR 8: Mid-session URL elicitation

### Task 8.1: Core `tryHandleCallException`
`WithODataCatalogHandlers(..., Func<RequestContext<CallToolRequestParams>, Exception, CancellationToken, ValueTask<CallToolResult?>>? tryHandleCallException = null)`; `CallToolAsync` wraps `session.Runtime.InvokeAsync` in `try/catch`; non-null → result; null → rethrow. Tests.Core: a `RecordingODataExecutor` that throws → callback receives the exception and its result is returned; null → exception propagates.

### Task 8.2: Tools elicitation
`ToolsMcpHost.CreateAsync`: after metadata, when `includeStdioMcp`, set `options.ConsentPresenter = null`. Callback `ToolsMcpHost.HandleConsentAsync(request, ex, ct)`: only `OAuthConsentRequiredException`; `McpServer` from `request.Server`; if client capabilities include elicitation → `ElicitAsync(new ElicitRequestParams { Mode = "url", Url = req.Url.AbsoluteUri, ElicitationId = req.ElicitationId, Message = "Open the URL to sign in. Enter the code shown on stderr if asked." })` concurrently with `OutboundOAuthClient.CompleteInteractiveGrantAsync(linkedCts.Token)`; accept → await complete → `InvokeAsync` once → result; decline/timeout/failure → cancel, `isError` text `Sign-in declined or timed out; re-run 'odata-mcp start' if needed.`; no elicitation capability → stderr device code (presenter) then complete, or `isError`.
- [ ] Tests.Tools: real SDK `McpClient` over stdio to a child process? Simplest real path: `McpClient` connected via in-process stream transport to the host's server (SDK `StreamServerTransport`/`StreamClientTransport` over pipes) with an `ElicitationHandler` recording params and returning `accept`; host points at the secured A8 `TestServer` via `configureServices`; token revoked mid-session (AS `RevokeIssuedTokens()` + refresh disabled by AS option `RejectRefresh`) → tool call elicits (assert `Mode`, `Url`, `ElicitationId`, no token/`device_code` in JSON) and the same call **succeeds**.

---

## Phase 9 — PR 9: Identity Assertion Grant

### Task 9.1
`FileIdTokenCallback` (`IdentityAssertionGrantIdTokenCallback` reading `--idp-id-token-file` or `ODATA_MCP_ID_TOKEN`), `OutboundOAuthClient` branch building `IdentityAssertionGrantProviderOptions` and calling `IdentityAssertionGrantProvider.GetAccessTokenAsync(resource, authorizationServer)` on the `"OAuth"` client; if the SDK provider requests MCP well-known paths against the resource (observe via AS/resource hit logs in the test), implement `IdentityAssertionGrant.cs` (RFC 8693 token exchange at IdP then RFC 7523 `jwt-bearer` at AS) in the same task. Local AS adds `/oauth/v2.0/idp/token` (issues `id_token`/exchange) and the `jwt-bearer` branch. `Implemented` += `IdentityAssertion`.
- [ ] Tests: env vs file source; missing both → `Validate` message; end-to-end token; `IdentityAssertionConventionHostTests` variant.

---

## Phase 10 — PR 10: Spec/docs inventory

- [ ] `specs/v3/README.md` documents table (AUTHENTICATION.md present; TESTING.md §8), `INVENTORY.md` Authentication rows (Outbound types, raw Core strings, the two authenticated test projects + fixture library), `ARCHITECTURE.md` §5 outbound line, `TOOL-TEST-MANIFEST.md` rule 9, Tools `readme.md` `--auth-token` section → OAuth flags; `AUTHENTICATION.md` "Testing" section: replace the static-CSDL protected resource paragraph with a pointer to TESTING.md §8 and record the `configureServices` hook and `verbose` parameter as spec revisions (rev 9).
- [ ] `dotnet build src/Microsoft.OData.Mcp.slnx -c Debug`; AOT smoke: `dotnet publish src/Microsoft.OData.Mcp.Tools -c Debug -r win-x64 -p:PublishAot=true` (report warnings; fix trim warnings in outbound JSON by adding types to `OutboundOAuthJsonContext`).

---

## Self-review notes

- Spec coverage: every AUTHENTICATION.md type has a task (Core: 1.1, 4b.6, 8.1; Outbound: 2.1–2.7, 3.1, 4a.1, 4b.1–4b.4, 5.1, 6.1, 9.1; Tools: 4a.2, 4b.5, 4b.6, 7.1, 8.2). TESTING.md §8 is Phase 0 + 4b.7 + per-PR variants. Deviations recorded for PR 10: `CreateAsync(..., bool verbose, ..., Action<IServiceCollection>? configureServices)`; RFC 8707 `resource` is sent when PRM `resource` or `--resource` is set (RFC 8414 has no "resource indicators supported" field).
- Type consistency: `OutboundConsentRequest`/`OAuthConsentRequiredException` defined in 4a.1 and used in 4b.3/4b.4/8.2; `OutboundToolsHostFixture` surface fixed in 0.3 and filled in 4b.7; `RestierToolTestBase` surface fixed in 0.2 and mirrored in 0.5; `ConventionRichHost` surface mirrored in 0.4.
