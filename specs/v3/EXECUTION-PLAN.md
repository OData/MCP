# OData MCP Platform v3 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship one Core catalog (MCP 2 resources + tools) and two hosts (AOT `odata-mcp`, AspNetCore `AddODataMcp` / `.WithMcp()`) that speak official MCP 2.x and execute real OData HTTP.

**Architecture:** Keep `Microsoft.OData.Mcp.Core.Models` as the IEdmModel-shaped projection (do **not** invent a parallel `Edm/` folder). CSDL parser fills those types from `$metadata`; AspNetCore adapter copies **declared** `IEdmModel` members into the same types. Catalogs emit only what the model contains. Tools host uses remote `HttpClient`. AspNetCore maps SDK MCP at `{prefix}/mcp` internally and routes tool calls into the app’s OData endpoints. `shutdown_server` is local-only.

**Tech Stack:** .NET 8/9/10, C# latest, ModelContextProtocol 2.x, MSTest v3/v4, Breakdance, FluentAssertions, official Northwind + TripPin.

**Spec:** `specs/v3/` (README, ARCHITECTURE, PROTOCOL, METADATA-STRATEGY, TOOL-SURFACE, INVENTORY). Read those before any task. This plan argues from that spec.

## Global Constraints

- Pin `ModelContextProtocol` and `ModelContextProtocol.AspNetCore` to **2.x** (no `0.*-*`).
- Core **must not** reference `Microsoft.OData.Edm`, `Microsoft.OData.Core`, ASP.NET Core, or Authentication.
- Tools **must not** reference EdmLib.
- Official MCP only. No `/mcp/tools/execute`.
- Resources use `odata://` (not `https://`).
- Query tool parameters have **no `$`**; the executor adds them.
- `shutdown_server` on Tools host only.
- `AddODataMcp()` and `.WithMcp()` are mutually exclusive; calling both throws.
- SDK `MapMcp` is internal, never public product API.
- Tests: Breakdance + **live** `https://services.odata.org/V4/Northwind/Northwind.svc` and `https://services.odata.org/TripPinRESTierService`. **OData 7 and OData 8 must not share a test process.** Restier lives in `Microsoft.OData.Mcp.Tests.AspNetCore.Restier` (OData 7 only, `RestierBreakdanceTestBase<TApi>`, endpoint routing). Convention OData 8 tests live in `Microsoft.OData.Mcp.Tests.AspNetCore`. See `specs/v3/TESTING.md` and `specs/v3/ODATA-HOSTING.md`.
- AspNetCore host **must not** `PackageReference` `Microsoft.AspNetCore.OData`. Discover prefix + `IEdmModel` from `EndpointDataSource`.
- **Never mock** HttpClient, OData, MCP, or metadata. If a test uses a mock, it is wrong.
- Always pass `-c Debug` (or `-c Release`) to `dotnet` commands.
- XML docs on every public API; `<param>` on the same line as content; `<remarks>` last. Examples on `AddODataMcp` / `WithMcp`.
- Normal namespaces (not file-scoped). Newline before `{`. `is null` / `is not null`. `ArgumentNullException.ThrowIfNull`. `ArgumentException.ThrowIfNullOrWhiteSpace` on strings.
- No `private` members except fields. Members alphabetical within visibility. `#region` Fields, Properties, Constructors, Public Methods, Private Methods.
- Prefer `.IsNullOrWhiteSpace`. Do not change `global.json` unless the user asks.
- **IEdmModel declared surface:** catalogs, tools, resources, schemas, completions, and generated JSON include only members that exist on the EDM. `Ignore()` / absent-from-CSDL properties never appear. Do not reflect over CLR entity types.
- **Existing `Core/Models` is the EDM.** Evolve those files. Do not add `Core/Edm/` or new type names for the same job.
- Every C# snippet in this plan is **normative style**. Copy the shape: copyright header on production types, normal namespaces, newline before `{`, `#region` order (Fields, Properties, Constructors, Public Methods, Private Methods), public members before internal, alphabetical within a group, XML docs, `is null`, `ThrowIfNull` / `ThrowIfNullOrWhiteSpace`. No file-scoped namespaces. No `private` members except fields.

---

## Target file map

Do **not** recreate `Models/`. New work is catalogs, execution, hosts, and adapter.

```
src/Microsoft.OData.Mcp.Core/
  Models/          # EXISTING — evolve in place (IEdmModel-shaped)
  Parsing/         # EXISTING CsdlParser — evolve
  Catalog/         # NEW — ODataMcpCatalog, ODataMcpCatalogOptions
  Execution/       # NEW — IOdataExecutor, RemoteODataExecutor
  Json/            # NEW — source-gen context
  Extensions/      # EXISTING — rewrite registration

src/Microsoft.OData.Mcp.Tools/
  Commands/        # EXISTING — rewrite StartCommand; add TestCommand
  Hosting/         # NEW — ToolsMcpHost, ShutdownServerTool

src/Microsoft.OData.Mcp.AspNetCore/
  Adaptation/      # NEW — EdmModelAdapter (declared members only)
  Hosting/         # NEW — AddODataMcp, WithMcp, endpoint data source
  Execution/       # NEW — InProcessODataExecutor

src/Microsoft.OData.Mcp.Tests.Core/Parsing/, Catalog/, Execution/, Models/
src/Microsoft.OData.Mcp.Tests.Tools/
src/Microsoft.OData.Mcp.Tests.AspNetCore/           # OData 8 convention host only
src/Microsoft.OData.Mcp.Tests.AspNetCore.Restier/  # OData 7 + Restier only
src/Microsoft.OData.Mcp.Tests.Integration/
```

Live service constants (Tests.Shared):

```csharp
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Tests.Shared
{

    /// <summary>
    /// Official OData sample service URLs used by live tests.
    /// </summary>
    /// <remarks>
    /// Tests must call these services directly. Do not substitute mocks or local stubs
    /// for Northwind or TripPin.
    /// </remarks>
    public static class LiveOData
    {

        #region Fields

        /// <summary>
        /// Gets the Northwind V4 service root (read-only).
        /// </summary>
        public const string Northwind = "https://services.odata.org/V4/Northwind/Northwind.svc";

        /// <summary>
        /// Gets the TripPin RESTier service root (read/write).
        /// </summary>
        public const string TripPin = "https://services.odata.org/TripPinRESTierService";

        #endregion

    }

}
```

---

### Task 1: Pin SDK 2.x and fail the dual-pipeline build contract

**Files:**
- Modify: `src/Microsoft.OData.Mcp.Core/Microsoft.OData.Mcp.Core.csproj`
- Modify: `src/Microsoft.OData.Mcp.Tools/Microsoft.OData.Mcp.Tools.csproj`
- Modify: `src/Microsoft.OData.Mcp.AspNetCore/Microsoft.OData.Mcp.AspNetCore.csproj`
- Modify: `src/Directory.Build.props` only if needed for `IsAotCompatible` later (Task 7)
- Test: `src/Microsoft.OData.Mcp.Tests.Core/PackageContractTests.cs`

**Interfaces:**
- Consumes: none
- Produces: packages restore ModelContextProtocol **2.x**

- [ ] **Step 1: Write a test that the Core csproj does not contain `0.*-*`**

```csharp
using System;
using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core
{

    [TestClass]
    public class PackageContractTests
    {

        #region Public Methods

        [TestMethod]
        public void CoreCsproj_PinsModelContextProtocol2()
        {
            var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Microsoft.OData.Mcp.Core", "Microsoft.OData.Mcp.Core.csproj"));
            var xml = File.ReadAllText(path);

            xml.Should().NotContain("Version=\"0.*-*\"");
            xml.Should().Contain("ModelContextProtocol");
            xml.Should().MatchRegex(@"ModelContextProtocol""\s+Version=""2\.");
        }

        #endregion

    }

}
```

- [ ] **Step 2: Run test — expect FAIL** (csproj still has `0.*-*`)

```
dotnet test src/Microsoft.OData.Mcp.Tests.Core/Microsoft.OData.Mcp.Tests.Core.csproj -c Debug --filter FullyQualifiedName~PackageContractTests
```

- [ ] **Step 3: Set `PackageReference Include="ModelContextProtocol" Version="2.*"` on Core and Tools; `ModelContextProtocol.AspNetCore` Version=`2.*` on AspNetCore. Restore. Fix compile breaks from SDK 2 API only as far as needed to build (obsolete warnings allowed until Task 5).**

- [ ] **Step 4: Re-run test — expect PASS**

- [ ] **Step 5: Commit** `chore: pin ModelContextProtocol 2.x`

---

### Task 2: Align `Core/Models` with IEdmModel declared surface

**Why this is not a new EDM:** `src/Microsoft.OData.Mcp.Core/Models/` already has `EdmModel`, `EdmEntityType`, `EdmProperty`, containers, sets, navs, functions, actions. Recreating them under `Edm/` would be a third schema. This task **edits those files** so they match `IEdmModel` semantics (declared members only, documentation, operations actually populated later by the parser).

**Files:**
- Modify: `src/Microsoft.OData.Mcp.Core/Models/*.cs` as needed (same type names, same namespace `Microsoft.OData.Mcp.Core.Models`)
- Test: `src/Microsoft.OData.Mcp.Tests.Core/Models/EdmModelDeclaredSurfaceTests.cs`

**Interfaces:**
- Consumes: existing `EdmModel` / `EdmEntityType` / `EdmProperty`
- Produces: same types; `Properties` and `NavigationProperties` mean **declared** members only

- [ ] **Step 1: Write tests against existing types (they should already construct)**

```csharp
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Models
{

    [TestClass]
    public class EdmModelDeclaredSurfaceTests
    {

        #region Public Methods

        [TestMethod]
        public void EdmModel_EntityContainer_ReturnsFirstContainer()
        {
            var model = new EdmModel
            {
                EntityContainers =
                [
                    new EdmEntityContainer
                    {
                        Name = "Default",
                        Namespace = "Sample"
                    }
                ]
            };

            model.EntityContainer.Should().NotBeNull();
            model.EntityContainer!.Name.Should().Be("Default");
        }

        [TestMethod]
        public void EdmEntityType_Properties_AreTheDeclaredSurface()
        {
            var type = new EdmEntityType
            {
                Name = "Customer",
                Namespace = "Sample",
                Properties =
                [
                    new EdmProperty
                    {
                        Name = "Id",
                        Type = "Edm.Int32",
                        Nullable = false
                    }
                ]
            };

            type.Properties.Should().ContainSingle(property => property.Name == "Id");
            type.Properties.Should().NotContain(property => property.Name == "InternalSecret");
        }

        #endregion

    }

}
```

- [ ] **Step 2: Run**

```
dotnet test src/Microsoft.OData.Mcp.Tests.Core/Microsoft.OData.Mcp.Tests.Core.csproj -c Debug --filter FullyQualifiedName~EdmModelDeclaredSurfaceTests
```

Expected: PASS. `EdmModel.EntityContainer` already aliases `PrimaryContainer`. **Do not** add `Microsoft.OData.Mcp.Core.Edm`. If the test fails, you omitted `EdmEntityContainer.Namespace` (`required`) — set it. Keys are `EdmEntityType.Key`, not `Keys`.

- [ ] **Step 3: Document on `EdmEntityType.Properties` and `EdmProperty` XML remarks: these collections are the declared EDM surface; ignored CLR properties must never be added. Fill function/action lists only from parser/adapter (Task 3 / 9).**

- [ ] **Step 4: Re-run tests — PASS**

- [ ] **Step 5: Commit** `docs: treat Core.Models as the IEdmModel declared surface`

---

### Task 3: CSDL parser against live Northwind and TripPin

**Files:**
- Modify: `src/Microsoft.OData.Mcp.Core/Parsing/ICsdlMetadataParser.cs`, `CsdlParser.cs` (already exist — evolve)
- Test: `src/Microsoft.OData.Mcp.Tests.Core/Parsing/CsdlParserLiveTests.cs`

**Interfaces:**
- Consumes: `Microsoft.OData.Mcp.Core.Models.EdmModel`
- Produces: existing `ICsdlMetadataParser` filled so functions/actions/docs parse; output is still `EdmModel`

```csharp
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.IO;
using Microsoft.OData.Mcp.Core.Models;

namespace Microsoft.OData.Mcp.Core.Parsing
{

    /// <summary>
    /// Parses CSDL into the Core EDM projection.
    /// </summary>
    /// <remarks>
    /// Only members declared in CSDL are added to the model. Properties absent from
    /// metadata must not appear on <see cref="EdmEntityType.Properties"/>.
    /// </remarks>
    public interface ICsdlMetadataParser
    {

        /// <summary>
        /// Parses a CSDL XML document from a string.
        /// </summary>
        /// <param name="csdlXml">The CSDL XML content as a string.</param>
        /// <returns>
        /// The parsed EDM model.
        /// </returns>
        EdmModel ParseFromString(string csdlXml);

        /// <summary>
        /// Parses a CSDL XML document from a stream.
        /// </summary>
        /// <param name="stream">The stream containing the CSDL XML content.</param>
        /// <returns>
        /// The parsed EDM model.
        /// </returns>
        EdmModel ParseFromStream(Stream stream);

        /// <summary>
        /// Parses a CSDL XML document from a file.
        /// </summary>
        /// <param name="filePath">The path to the file containing the CSDL XML content.</param>
        /// <returns>
        /// The parsed EDM model.
        /// </returns>
        EdmModel ParseFromFile(string filePath);

    }

}
```

Keep these three names. Do **not** add `Parse(string)`. Existing `CsdlParserTests` already call `ParseFromString`.

- [ ] **Step 1: Write live tests (no mocks, no `HttpMessageHandler` fakes)**

```csharp
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Parsing
{

    [TestClass]
    public class CsdlParserLiveTests
    {

        #region Public Methods

        [TestMethod]
        public async Task Parse_Northwind_ContainsProductsEntitySet()
        {
            using var http = new HttpClient();
            var xml = await http.GetStringAsync($"{LiveOData.Northwind.TrimEnd('/')}/$metadata");

            xml.Should().NotBeNullOrWhiteSpace();

            var model = new CsdlParser().ParseFromString(xml);

            model.EntityContainer.Should().NotBeNull();
            model.EntityContainer!.EntitySets.Select(set => set.Name).Should().Contain("Products");

            var product = model.EntityTypes.Single(type => type.Name == "Product");

            product.Key.Should().NotBeEmpty();
            product.Properties.Should().Contain(property => property.Name == "ProductName");
            product.Properties.Select(property => property.Name).Should().NotContain("NotInCsdl");
        }

        [TestMethod]
        public async Task Parse_TripPin_ContainsPeople()
        {
            using var http = new HttpClient();
            var xml = await http.GetStringAsync($"{LiveOData.TripPin.TrimEnd('/')}/$metadata");
            var model = new CsdlParser().ParseFromString(xml);

            model.EntityContainer!.EntitySets.Select(set => set.Name).Should().Contain("People");
            model.Functions.Should().NotBeNull();
            model.Actions.Should().NotBeNull();
        }

        #endregion

    }

}
```

- [ ] **Step 2: Run — expect FAIL**

```
dotnet test src/Microsoft.OData.Mcp.Tests.Core/Microsoft.OData.Mcp.Tests.Core.csproj -c Debug --filter FullyQualifiedName~CsdlParserLiveTests
```

- [ ] **Step 3: Evolve the existing `CsdlParser` (today `XDocument`). Prefer `XmlReader` if you touch the hot path. Fill schema-level `Functions` / `Actions` (currently left empty), imports, `<Documentation>`, and `Core.Description`. Do not rename `ParseFromString` / `ParseFromStream` / `ParseFromFile`. Keep `CsdlParserTests` green (remove any new `// Arrange` comments; do not add them).**

- [ ] **Step 4: Run live tests — PASS.** If TripPin URL redirects, follow redirects with `HttpClient` default handler; do not stub.

- [ ] **Step 5: Commit** `feat: parse CSDL into Core EDM from live OData services`

---

### Task 4: Remote `IOdataExecutor`

**Files:**
- Create: `src/Microsoft.OData.Mcp.Core/Execution/IOdataExecutor.cs`, `ODataExecuteRequest.cs`, `ODataExecuteResult.cs`, `RemoteODataExecutor.cs`
- Test: `src/Microsoft.OData.Mcp.Tests.Core/Execution/RemoteODataExecutorLiveTests.cs`

**Interfaces:**
- Consumes: `IHttpClientFactory` named client `"OData"`
- Produces:

```csharp
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.OData.Mcp.Core.Execution
{

    /// <summary>
    /// Executes one OData HTTP request. Query option keys must not include '$';
    /// implementations add the prefix on the wire.
    /// </summary>
    public interface IOdataExecutor
    {

        /// <summary>
        /// Executes the request.
        /// </summary>
        /// <param name="request">The OData request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The HTTP result.
        /// </returns>
        Task<ODataExecuteResult> ExecuteAsync(ODataExecuteRequest request, CancellationToken cancellationToken);

    }

    /// <summary>
    /// An OData HTTP request built from MCP tool arguments.
    /// </summary>
    public sealed class ODataExecuteRequest
    {

        #region Properties

        /// <summary>
        /// Gets or sets the JSON body for create/update.
        /// </summary>
        public string? JsonBody { get; set; }

        /// <summary>
        /// Gets or sets the HTTP method.
        /// </summary>
        public HttpMethod Method { get; set; } = HttpMethod.Get;

        /// <summary>
        /// Gets or sets query options without the '$' prefix.
        /// </summary>
        public Dictionary<string, string> QueryOptions { get; set; } = [];

        /// <summary>
        /// Gets or sets the path relative to the service root (for example, "Products").
        /// </summary>
        public string RelativePath { get; set; } = string.Empty;

        #endregion

    }

    /// <summary>
    /// The HTTP result of an OData call.
    /// </summary>
    public sealed class ODataExecuteResult
    {

        #region Properties

        /// <summary>
        /// Gets or sets the response body.
        /// </summary>
        public string Body { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the status code is success.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Gets or sets the response media type.
        /// </summary>
        public string? MediaType { get; set; }

        /// <summary>
        /// Gets or sets the HTTP status code.
        /// </summary>
        public int StatusCode { get; set; }

        #endregion

    }

}
```

`RemoteODataExecutor` prefixes query keys with `$` when sending (`filter` → `$filter`). Never send `$$filter`.

- [ ] **Step 1: Write live GET Products `$top=1`**

```csharp
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Core.Execution;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Execution
{

    [TestClass]
    public class RemoteODataExecutorLiveTests
    {

        #region Public Methods

        [TestMethod]
        public async Task ExecuteAsync_NorthwindProductsTop1_ReturnsJson()
        {
            var services = new ServiceCollection();

            services.AddHttpClient("OData", client =>
            {
                client.BaseAddress = new Uri(LiveOData.Northwind.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            });

            var executor = new RemoteODataExecutor(services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>());
            var result = await executor.ExecuteAsync(
                new ODataExecuteRequest
                {
                    QueryOptions =
                    {
                        ["top"] = "1"
                    },
                    RelativePath = "Products"
                },
                CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Body.Should().NotBeNullOrWhiteSpace();
            result.Body.Should().Contain("Product");
        }

        #endregion

    }

}
```

- [ ] **Step 2: Run — FAIL**

```
dotnet test src/Microsoft.OData.Mcp.Tests.Core/Microsoft.OData.Mcp.Tests.Core.csproj -c Debug --filter FullyQualifiedName~RemoteODataExecutorLiveTests
```

- [ ] **Step 3: Implement executor. Apply outbound auth headers from a small `ODataClientOptions` (Bearer/API key/Basic) when configured. Map 4xx/5xx to `IsSuccess = false` with body preserved.**

- [ ] **Step 4: PASS**

- [ ] **Step 5: Commit** `feat: remote OData executor with $less query options`

---

### Task 5: Resource catalog + templates + completions data

**Files:**
- Create: `src/Microsoft.OData.Mcp.Core/Catalog/ODataMcpCatalog.cs`, `ODataMcpCatalogOptions.cs`
- Test: `src/Microsoft.OData.Mcp.Tests.Core/Catalog/ODataMcpCatalogResourceTests.cs`

**Interfaces:**
- Consumes: `EdmModel` (Task 2–3)
- Produces: catalog methods used by hosts to fill SDK resource APIs

```csharp
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OData.Mcp.Core.Models;

namespace Microsoft.OData.Mcp.Core.Catalog
{

    /// <summary>
    /// Builds MCP resources and tools from a Core EDM. Only declared model members
    /// are serialized into catalogs, schemas, and completions.
    /// </summary>
    public sealed class ODataMcpCatalog
    {

        #region Fields

        internal readonly EdmModel _model;
        internal readonly ODataMcpCatalogOptions _options;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the resource descriptors.
        /// </summary>
        public IReadOnlyList<ODataResourceDescriptor> Resources { get; }

        /// <summary>
        /// Gets the resource templates.
        /// </summary>
        public IReadOnlyList<ODataResourceTemplateDescriptor> ResourceTemplates { get; }

        /// <summary>
        /// Gets the tool descriptors (generic first, then named).
        /// </summary>
        public IReadOnlyList<ODataToolDescriptor> Tools { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataMcpCatalog"/> class.
        /// </summary>
        /// <param name="model">The Core EDM.</param>
        /// <param name="options">Catalog options.</param>
        public ODataMcpCatalog(EdmModel model, ODataMcpCatalogOptions options)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(options);

            _model = model;
            _options = options;
            Resources = [];
            ResourceTemplates = [];
            Tools = [];
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Completes entity set names for resource templates.
        /// </summary>
        /// <param name="prefix">The prefix typed so far. Empty means all declared sets.</param>
        /// <returns>
        /// Matching declared entity set names from the EDM. Never invents names.
        /// </returns>
        public IReadOnlyList<string> CompleteEntitySetNames(string prefix)
        {
            ArgumentNullException.ThrowIfNull(prefix);

            var names = _model.EntityContainer?.EntitySets.Select(set => set.Name) ?? [];

            if (prefix.Length == 0)
            {
                return [.. names];
            }

            return [.. names.Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))];
        }

        #endregion

    }

}
```

`ODataMcpCatalogOptions` lives in the same folder: public properties alphabetical (`ExcludeEntitySets`, `IncludeCreate`, `IncludeDelete`, `IncludeEntitySets`, `IncludeUpdate`, `MaxNamedTools`, `RouteName`) with XML docs and `#region Properties`.

Resource URIs: `odata://{RouteName}/$metadata`, `odata://{RouteName}/{entitySet}`. Templates include `odata://{RouteName}/{entitySet}({key})`.

Shared helper (put in Tests.Core, used by Tasks 5–7). Do not invent a second parser API:

```csharp
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Tests.Shared;

namespace Microsoft.OData.Mcp.Tests.Core
{

    internal static class LiveMetadata
    {

        #region Public Methods

        internal static async Task<EdmModel> LoadNorthwindModelAsync()
        {
            using var http = new HttpClient();
            var xml = await http.GetStringAsync($"{LiveOData.Northwind.TrimEnd('/')}/$metadata");

            return new CsdlParser().ParseFromString(xml);
        }

        #endregion

    }

}
```

Catalog tests call `LiveMetadata.LoadNorthwindModelAsync()`, not a free function.

- [ ] **Step 1: Write Northwind catalog tests**

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    [TestClass]
    public class ODataMcpCatalogResourceTests
    {

        #region Public Methods

        [TestMethod]
        public async Task Catalog_Northwind_ListsMetadataAndProductsResource()
        {
            var model = await LiveMetadata.LoadNorthwindModelAsync();
            var catalog = new ODataMcpCatalog(
                model,
                new ODataMcpCatalogOptions
                {
                    RouteName = "remote"
                });

            catalog.Resources.Select(resource => resource.Uri).Should().Contain("odata://remote/$metadata");
            catalog.Resources.Select(resource => resource.Uri).Should().Contain("odata://remote/Products");
            catalog.ResourceTemplates.Select(template => template.UriTemplate).Should().Contain("odata://remote/{entitySet}({key})");
            catalog.CompleteEntitySetNames("Pro").Should().Contain("Products");

            var products = catalog.Resources.Single(resource => resource.Uri.EndsWith("/Products", StringComparison.Ordinal));

            products.Description.Should().NotBeNullOrWhiteSpace();
        }

        #endregion

    }

}
```

- [ ] **Step 2: FAIL then implement the resource half of `ODataMcpCatalog`. Type-card JSON for `resources/read` of a set (no collection dump) must list **only** `EdmEntityType.Properties` / `NavigationProperties`. `$metadata` read returns CSDL (host supplies bytes from cache). Never walk CLR types.**

- [ ] **Step 3: PASS + commit** `feat: MCP resource catalog from EDM`

---

### Task 6: Generic tools in the catalog

**Files:**
- Modify: `ODataMcpCatalog.cs`
- Test: `src/Microsoft.OData.Mcp.Tests.Core/Catalog/ODataMcpCatalogToolTests.cs`
- Test: `src/Microsoft.OData.Mcp.Tests.Integration/NorthwindGenericQueryTests.cs`

**Interfaces:**
- Consumes: `IOdataExecutor`, catalog from Task 5
- Produces: tool descriptors named exactly: `odata_list_entity_sets`, `odata_describe_type`, `odata_query`, `odata_get`, `odata_create`, `odata_update`, `odata_delete`, `odata_navigate`

Each descriptor includes `title`, `inputSchema`, `outputSchema`, `readOnlyHint` / `destructiveHint` / `idempotentHint`, `openWorldHint: true`.

- [ ] **Step 1: Assert generic names and `$`-less `filter` property on `odata_query` input schema**

```csharp
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    [TestClass]
    public class ODataMcpCatalogToolTests
    {

        #region Public Methods

        [TestMethod]
        public async Task Catalog_GenericQueryTool_HasFilterWithoutDollar()
        {
            var catalog = new ODataMcpCatalog(await LiveMetadata.LoadNorthwindModelAsync(), new ODataMcpCatalogOptions());
            var query = catalog.Tools.Single(tool => tool.Name == "odata_query");

            query.InputSchema.GetRawText().Should().Contain("\"filter\"");
            query.InputSchema.GetRawText().Should().NotContain("\"$filter\"");
            query.ReadOnlyHint.Should().BeTrue();
        }

        #endregion

    }

}
```

- [ ] **Step 2: Integration — MCP-unaware: call catalog handler for `odata_query` entitySet=Products top=1 against live Northwind via `RemoteODataExecutor`. Body contains a product.**

- [ ] **Step 3: Implement handlers. Size-guard (default 1 MB) — if exceeded, `isError` text tells the agent to add `select`/`top`.**

- [ ] **Step 4: PASS + commit** `feat: generic OData MCP tools`

---

### Task 7: Named tools + cap

**Files:**
- Modify: `ODataMcpCatalog.cs`
- Test: `src/Microsoft.OData.Mcp.Tests.Core/Catalog/NamedToolCapTests.cs`

**Interfaces:**
- Produces: `list_products`, `get_product`, etc. snake_case. Never partial CRUD for a set.

- [ ] **Step 1:**

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    [TestClass]
    public class NamedToolCapTests
    {

        #region Public Methods

        [TestMethod]
        public async Task Catalog_MaxNamedTools_DoesNotSplitCrudFamily()
        {
            var catalog = new ODataMcpCatalog(
                await LiveMetadata.LoadNorthwindModelAsync(),
                new ODataMcpCatalogOptions
                {
                    MaxNamedTools = 12
                });
            var named = catalog.Tools.Where(tool => !tool.Name.StartsWith("odata_", StringComparison.Ordinal)).ToList();

            named.Should().NotBeEmpty();

            foreach (var group in named.GroupBy(tool => tool.EntitySetName))
            {
                var ops = group.Select(tool => tool.Name).ToList();

                if (ops.Any(name => name.StartsWith("list_", StringComparison.Ordinal)))
                {
                    ops.Should().Contain(name => name.StartsWith("get_", StringComparison.Ordinal));
                }
            }
        }

        #endregion

    }

}
```

- [ ] **Step 2: Implement cap: IncludeEntitySets first, then alphabetical sets. ExcludeEntitySets applied. Named-tool `inputSchema` properties = declared EDM properties minus binary/stream. A property that is not on the entity type must not appear in the schema.**

- [ ] **Step 3: CSDL docs flow into `title`/`description` when present (TripPin People).**

- [ ] **Step 4: PASS + commit** `feat: capped named OData tools`

---

### Task 8: Tools host — stdio, `shutdown_server`, `test` command

**Files:**
- Rewrite: `src/Microsoft.OData.Mcp.Tools/Commands/StartCommand.cs`, `Program.cs`, `ODataMcpRootCommand.cs`
- Create: `TestCommand.cs`, `Hosting/ToolsMcpHost.cs`, `Hosting/ShutdownServerTool.cs`
- Test: `src/Microsoft.OData.Mcp.Tests.Tools/ToolsHostTests.cs`
- Modify: `Microsoft.OData.Mcp.Tools.csproj` — `ToolCommandName` = `odata-mcp`; `IsAotCompatible` = true

**Interfaces:**
- Consumes: catalog + `RemoteODataExecutor` + SDK 2 `AddMcpServer().WithTools(...).WithResources(...).WithStdioServerTransport()`
- Produces: running stdio server; `shutdown_server` tool

**Do not** call `WithODataTools()` or `WithToolsFromAssembly`.

- [ ] **Step 1: Test catalog of a `ToolsMcpHost` built for Northwind includes `shutdown_server` and `odata_query`, excludes nothing required. Test `ShutdownServerTool` cancels a `CancellationTokenSource` after delay 0.**

```csharp
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Tools.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Tools
{

    [TestClass]
    public class ShutdownServerToolTests
    {

        #region Public Methods

        [TestMethod]
        public async Task ShutdownServerTool_DelayZero_CancelsHost()
        {
            using var cts = new CancellationTokenSource();
            var tool = new ShutdownServerTool(cts);

            await tool.InvokeAsync("test", 0, CancellationToken.None);

            cts.IsCancellationRequested.Should().BeTrue();
        }

        #endregion

    }

}
```

- [ ] **Step 2: `odata-mcp test <northwind>` prints entity set count to stderr and exits 0. Cover with a test that constructs `TestCommand` and runs against live Northwind (not Process.Start if Breakdance can invoke `OnExecuteAsync` directly — prefer direct invoke).**

- [ ] **Step 3: `StartCommand` fetches `$metadata` with `HttpClient`, parses, builds catalog, registers SDK tools/resources/completions from catalog, registers `shutdown_server`, stdio transport, logs to stderr.**

- [ ] **Step 4: Set `IsAotCompatible>true` on Core and Tools. Add `McpJsonContext` source-gen for request DTOs. Fix AOT warnings introduced by this host (no reflection scan).**

- [ ] **Step 5: PASS + commit** `feat: odata-mcp host with shutdown_server`

---

### Task 9: `IEdmModel` adapter (AspNetCore)

**Files:**
- Create: `src/Microsoft.OData.Mcp.AspNetCore/Adaptation/EdmModelAdapter.cs`
- Test: `src/Microsoft.OData.Mcp.Tests.AspNetCore/EdmModelAdapterTests.cs`
- Test project already references AspNetCore OData — build a real `IEdmModel` with `ODataConventionModelBuilder` + a `Customer` type with `[Key]` and XML-doc-equivalent annotation if the builder supports `HasCoreAnnotation` / description. **No mocks.**

**Interfaces:**
- Consumes: `Microsoft.OData.Edm.IEdmModel`
- Produces: `Microsoft.OData.Mcp.Core.Edm.EdmModel`

- [ ] **Step 1:**

```csharp
using System.Linq;
using FluentAssertions;
using Microsoft.OData.Mcp.AspNetCore.Adaptation;
using Microsoft.OData.ModelBuilder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore
{

    [TestClass]
    public class EdmModelAdapterTests
    {

        #region Public Methods

        [TestMethod]
        public void Adapter_ConventionModel_MapsEntitySetAndKey()
        {
            var builder = new ODataConventionModelBuilder();

            builder.EntitySet<Customer>("Customers");

            var core = EdmModelAdapter.ToCoreModel(builder.GetEdmModel());
            var customer = core.EntityTypes.Single(type => type.Name == "Customer");

            core.EntityContainer!.EntitySets.Select(set => set.Name).Should().Contain("Customers");
            customer.Keys.Should().NotBeEmpty();
        }

        [TestMethod]
        public void Adapter_IgnoredProperty_IsNotOnCoreModel()
        {
            var builder = new ODataConventionModelBuilder();

            builder.EntitySet<Customer>("Customers");
            builder.EntityType<Customer>().Ignore(customer => customer.InternalSecret);

            var core = EdmModelAdapter.ToCoreModel(builder.GetEdmModel());
            var customer = core.EntityTypes.Single(type => type.Name == "Customer");

            customer.Properties.Select(property => property.Name).Should().NotContain("InternalSecret");
        }

        #endregion

    }

}
```

Add `InternalSecret` to `src/Microsoft.OData.Mcp.Tests.Shared/Entities/Customer.cs` (alphabetical with the other properties, XML docs). `Ignore` is meaningless without it. Catalog tests that use this builder must assert tool/resource JSON does not contain `InternalSecret`.

- [ ] **Step 2: Implement adapter in AspNetCore package only. Map documentation annotations when present on `IEdmModel`.**

- [ ] **Step 3: PASS + commit** `feat: adapt IEdmModel into Core EDM`

---

### Task 10: `AddODataMcp()` and `.WithMcp()`

**Files:**
- Create: AspNetCore Hosting types from the file map
- Test: `src/Microsoft.OData.Mcp.Tests.AspNetCore/AddODataMcpTests.cs`, `WithMcpTests.cs`

**Interfaces:**
- Consumes: adapter, catalog, SDK `MapMcp`, `IEndpointRouteBuilder`
- Produces:

```csharp
public static IServiceCollection AddODataMcp(this IServiceCollection services);
public static IServiceCollection AddODataMcp(this IServiceCollection services, Action<ODataMcpHostOptions> configure);

public static ODataOptions WithMcp(this ODataOptions options); // marks the last AddRouteComponents prefix
```

XML docs **required** (copy into code):

`AddODataMcp`: *Enables MCP for every OData route component registered with `AddRouteComponents`, including components added after this call. Prefer this unless a route must stay hidden from agents. Do not call `WithMcp` in the same application — startup will throw `InvalidOperationException`.*

`WithMcp`: *Enables MCP for this OData route only. Use when some routes must not be exposed to agents. Do not also call `AddODataMcp`.*

Internal: endpoint data source reads prefixes and `IEdmModel`. `AddODataMcp` registers services. `UseODataMcp` maps SDK MCP at `{prefix}/mcp` after OData routes exist. Do not call SDK `MapMcp` in app code.

- [ ] **Step 1: Breakdance TestServer**

Convention-model host tests subclass `CloudNimble.Breakdance.AspNetCore.AspNetCoreBreakdanceTestBase` the same way `ODataMcpRouteConventionTests` does (`TestHostBuilder`, `AddMinimalMvc()`, `TestSetup()`). Do **not** call `UseODataMcp()`. In-process Restier OData tests (Task 11) subclass `Microsoft.Restier.Breakdance.RestierBreakdanceTestBase<TApi>` — set `AddRestierAction` and `MapRestierAction` before `TestSetup()`. Package: `Microsoft.Restier.Breakdance`.

```csharp
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore
{

    [TestClass]
    public class AddODataMcpTests : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices(services =>
            {
                services
                    .AddControllers()
                    .AddOData(options =>
                    {
                        options.AddRouteComponents("odata", TestModels.GetSimpleModel());
                        options.AddRouteComponents("internal", TestModels.GetMinimalModel());
                    });

                services.AddODataMcp();
            });

            AddMinimalMvc();
            TestSetup();
        }

        [TestCleanup]
        public void TearDown()
        {
            TestTearDown();
        }

        #endregion

        #region Public Methods

        [TestMethod]
        public async Task AddODataMcp_ToolsList_ContainsGenericQuery_OmitsShutdown()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/odata/mcp");

            request.Headers.TryAddWithoutValidation("Mcp-Method", "tools/list");
            request.Content = new StringContent(
                """{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""",
                Encoding.UTF8,
                "application/json");

            using var client = TestServer.CreateClient();
            var response = await client.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            json.Should().Contain("odata_query");
            json.Should().NotContain("shutdown_server");
        }

        [TestMethod]
        public void AddODataMcp_AndWithMcp_Throws()
        {
            var services = new ServiceCollection();

            services.AddLogging();
            services
                .AddControllers()
                .AddOData(options =>
                {
                    options.AddRouteComponents("odata", TestModels.GetSimpleModel()).WithMcp();
                });
            services.AddODataMcp();

            var act = () => services.BuildServiceProvider();

            act.Should().Throw<InvalidOperationException>().WithMessage("*AddODataMcp*WithMcp*");
        }

        #endregion

    }

}
```

`WithMcpTests`: same `AspNetCoreBreakdanceTestBase` host, `AddRouteComponents("odata", …).WithMcp()` only — POST `/odata/mcp` is 200, POST `/internal/mcp` is 404. Send MCP with `TestServer.CreateClient()` (existing tests use `TestServer.CreateRequest` for GET).

- [ ] **Step 2: Implement marker + data source + XML docs + mutual exclusion.**

- [ ] **Step 3: PASS + commit** `feat: AddODataMcp and WithMcp host API`

---

### Task 11: In-app executor + end-to-end query

**Files:**
- Create: `src/Microsoft.OData.Mcp.AspNetCore/Execution/InProcessODataExecutor.cs`
- Test (OData 8 convention): `src/Microsoft.OData.Mcp.Tests.AspNetCore/`
- Test (Restier / OData 7): `src/Microsoft.OData.Mcp.Tests.AspNetCore.Restier/` — **separate project; must not reference OData 8**

**Interfaces:**
- Consumes: same-host HTTP to the app’s OData route (not a remote URL)
- Produces: `tools/call` `odata_query` on `{prefix}/mcp` returns JSON from that route

- [ ] **Step 1 (OData 8):** Convention `AddOData` host in `Tests.AspNetCore`. `AddODataMcp()`. POST `odata_query` against the in-app entity set. No Restier references in this project.

- [ ] **Step 2 (Restier / OData 7):** New project `Microsoft.OData.Mcp.Tests.AspNetCore.Restier`. Subclass `RestierBreakdanceTestBase<TApi>` with **endpoint routing**. Seed `Customers`. `AddODataMcp()` must not pull OData 8. POST `odata_query` entitySet=Customers; JSON contains the seeded name. No mocks. Do **not** `[Ignore]` this class.

- [ ] **Step 3:** Executor hits the same `TestServer`, not a remote URL. Preserve `Authorization` if present.

- [ ] **Step 4: PASS + commit** `feat: in-process OData executor for AspNetCore MCP`

---

### Task 12: Delete the dual prototype

**Files:** delete or empty per [INVENTORY.md](./INVENTORY.md)

**Minimum delete list:**
- `Core/Legacy/**`
- `Core/Routing/**` (custom REST)
- `Core/Server/ODataMcpTools.cs`, `DynamicODataMcpTools.cs`
- `Core/Services/DynamicModelRefreshService.cs`
- `Core/Configuration` unused trees
- `AspNetCore/Middleware/ODataMcpMiddleware.cs`
- `AspNetCore/Routing/ODataMcpRouteConvention.cs` 501 path
- `AspNetCore/Extensions/DEPRECATE_ServiceCollectionExtensions.cs`
- `Tools/Services/DynamicToolGeneratorService.cs`
- `Tests.Core/Server/TestHttpMessageHandler.cs` and any mock handlers
- `TestHttpClientFactory` if it exists to fake HTTP — replace with live/TestServer

- [ ] **Step 1: Solution builds with `-c Debug` after deletes. `dotnet test src/Microsoft.OData.Mcp.slnx -c Debug` PASS. Grep the src tree for `tools/execute`, `WithODataTools`, `0.*-*` — zero hits in csproj/source (docs/archive allowed).**

- [ ] **Step 2: Commit** `chore: remove dual-prototype MCP and REST façades`

---

### Task 13: Docs and public API baseline

**Files:**
- XML docs already required on public APIs
- `src/Microsoft.OData.Mcp.Docs` regenerates from new surface
- Breakdance.Assemblies baseline once the surface is stable

- [ ] **Step 1: `dotnet build src/Microsoft.OData.Mcp.Docs/Microsoft.OData.Mcp.Docs.docsproj -c Debug`**
- [ ] **Step 2: Confirm generated docs do not include `ODataMcpMiddleware` or `AddODataMcpServer`.**
- [ ] **Step 3: Commit** `docs: regenerate API reference for v3 surface`

---

## Sequencing

```
1 pin SDK → 2 EDM types → 3 CSDL live parse → 4 remote executor
       → 5 resources → 6 generic tools → 7 named tools
       → 8 Tools host + shutdown_server
       → 9 IEdmModel adapter → 10 AddODataMcp/WithMcp → 11 in-app executor
       → 12 delete dual stacks → 13 docs
```

Do not start Task 12 until Tasks 8 and 11 are green. Do not register attribute tools at any point.

## Spec coverage

| Spec rule | Task |
|-----------|------|
| Pin SDK 2.x | 1 |
| Core/Models is the EDM; no second folder; declared surface only | 2, 3, 5, 7, 9 |
| CSDL parse + docs + operations | 3 |
| Forward OData, `$`-less query | 4, 6, 11 |
| `odata://` resources/templates | 5 |
| Generic + named one builder | 6, 7 |
| `shutdown_server` local only | 8, 10 (assert absent) |
| AOT-friendly host | 8 |
| `AddODataMcp` / `WithMcp` + XML docs | 10 |
| In-app routed OData | 11 |
| Delete dual pipelines / REST | 12 |
| No mocks, Breakdance, live services | 3, 4, 6, 8, 9, 10, 11 |
