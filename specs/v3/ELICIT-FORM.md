# Form elicitation on annotated OData actions

**Status:** Authoritative for implementation  
**Date:** 2026-10-30  
**Applies to:** McpEssentials.Core (form object + compact XML); Microsoft.OData.Mcp.Core (vocab, CsdlParser, catalog); Microsoft.OData.Mcp.AspNetCore (stamp, host UX, before-invoke); Tools (remote `$metadata` → elicit)  
**Companion:** [ACTIONS.md](./ACTIONS.md) (Tasks + URL elicitation), [AUTHENTICATION.md](./AUTHENTICATION.md) (tokens never in forms)

URL elicitation is after OData invoke (`connectUri` on the Started ticket). Form elicitation is **before** invoke: collect operator fields, merge into tool arguments, then POST. Named `action_*` and `odata_call` of the same annotated action share the path. Functions are never form-elicited.

---

## 1. One form, three projections

McpEssentials `FormElicitation` is the live object. It already projects two ways:

| Method | Result |
|--------|--------|
| `Build()` | SDK `ElicitRequestParams` object graph (`mode: form`) |
| `ToInputRequest()` | that graph as JSON (`InputRequest.Params` via STJ `SerializeToElement`) |

This work adds a third:

| Method | Result |
|--------|--------|
| `ToDefinition()` | immutable snapshot (`FormDefinition`) |
| `ToXml()` | compact `<Form>` island from that snapshot (`XmlWriter`) |
| `FormElicitation.ParseXml` / `FromDefinition` | rebuild a builder |

`ToXml` is **not** `XmlSerializer`, sgen, or a general XML framework. It is a closed AOT codec: `XmlWriter` / `XmlReader` over three element names. Native AOT and trimming keep it. `From<T>()` / `Bind<T>()` without a `JsonSerializerContext` stay the only annotated reflection holes; XML does not reflect over `T`.

OData does not re-walk fields. AspNetCore calls `ToXml(xmlNamespace: McpVocabulary.ReferenceUri)` and wraps the island in `<Annotation Term="Mcp.ElicitForm">`.

---

## 2. Compact XML dialect

Namespace-agnostic on read (match `LocalName`). On write, OData passes the vocab document URI so EdmLib `SetIsSerializedAsElement` validation succeeds (root `NamespaceURI` + `LocalName` must equal the direct-value annotation’s namespace + name).

```xml
<Form xmlns="https://github.com/odata/mcp/vocabularies/Microsoft.OData.Mcp.V1.xml"
      Message="Ship this order">
  <Field Name="trackingNumber" Type="string" Required="true"
         Description="Carrier tracking number"/>
  <Field Name="priority" Type="enum" Title="Priority">
    <Option Value="low" Title="Low"/>
    <Option Value="high" Title="High" Selected="true"/>
  </Field>
</Form>
```

### 2.1 Elements (PascalCase)

| Element | Parent | Role |
|---------|--------|------|
| `Form` | `Annotation` (OData) or document root (McpEssentials) | One elicitation schema |
| `Field` | `Form` | One primitive / enum / multi field |
| `Option` | `Field` | One allowed value (`enum` / `multi`) |

No other children. No mixed content. No `<?xml`. No `xsi` / `xsd`.

### 2.2 Form attributes

| Attribute | Required | Maps to |
|-----------|----------|---------|
| `Message` | yes | `FormElicitationBuilder.Message` / `ElicitRequestParams.Message` |
| `Name` | no | keyed DI name when the island is a published copy of a named form (optional; stamp may omit) |

### 2.3 Field attributes

| Attribute | When written | Maps to |
|-----------|--------------|---------|
| `Name` | always | wire name (camelCase as `FormElicitationProperty.WireName`) |
| `Type` | always | `string` \| `integer` \| `number` \| `boolean` \| `enum` \| `multi` |
| `Required` | only `"true"` | `ElicitationField.Required` |
| `Format` | when set | `email` \| `uri` \| `date` \| `date-time` |
| `Title` | when set | title |
| `Description` | when set | description |
| `MinLength` / `MaxLength` | string constraints | `MinLength` / `MaxLength` |
| `Min` / `Max` | number/integer, or multi `MinItems`/`MaxItems` | `Minimum`/`Maximum` or `MinItems`/`MaxItems` |
| `Default` | non-enum default | string/number/boolean default (`true`/`false` for bool) |

Do not write `Required="false"`. Readers accept `true` / `True` / `1`.

`Type` mapping:

| `ElicitationFieldKind` | `Type` |
|------------------------|--------|
| `String` | `string` |
| `Integer` | `integer` |
| `Number` | `number` |
| `Boolean` | `boolean` |
| `Choice` | `enum` |
| `MultiChoice` | `multi` |

Unknown `Type` on read: throw `InvalidOperationException` (fail first).

### 2.4 Option attributes

| Attribute | When written | Maps to |
|-----------|--------------|---------|
| `Value` | always | option wire value |
| `Title` | when a display title was set | option title |
| `Selected` | only `"true"` | this value is the (or a) default |

For `enum`, at most one `Selected`. That value is `DefaultString`. For `multi`, every `Selected` option is in `DefaultStrings`. When `Selected` is present, do not also write `Default` on the field.

---

## 3. McpEssentials surface

Package: `McpEssentials.Core`. No EdmLib. No CSDL. No `Annotation Term`.

### 3.1 Snapshot (public, one type per file)

- `FormDefinition` — `Message`, `IReadOnlyList<FormFieldDefinition> Fields`, optional `Name`
- `FormFieldDefinition` — `Name`, `Type` (`FormFieldType` enum), `Required`, `Format`, `Title`, `Description`, `MinLength`, `MaxLength`, `Minimum`, `Maximum`, `MinItems`, `MaxItems`, `DefaultString`, `DefaultNumber`, `DefaultBool`, `DefaultStrings`, `IReadOnlyList<FormOptionDefinition> Options`
- `FormOptionDefinition` — `Value`, `Title`, `Selected`
- `FormFieldType` — `String`, `Integer`, `Number`, `Boolean`, `Enum`, `Multi`

`ElicitationField` / `ElicitationFieldKind` / `ElicitationOption` stay internal.

### 3.2 Codec

`FormXml` (public static):

```csharp
public static string Serialize(FormDefinition definition, string? xmlNamespace = null)
public static FormDefinition Parse(string xml)
```

`Serialize` uses `XmlWriter` (`OmitXmlDeclaration = true`, indent on). When `xmlNamespace` is non-whitespace, it is the `xmlns` on `Form`. `Parse` uses `XmlReader` with `DtdProcessing.Prohibit` and `XmlResolver = null`. Local names only.

### 3.3 Builder

On `IFormElicitationSource` (and every facade, forwarding like `Build` / `ToInputRequest`):

```csharp
FormDefinition ToDefinition();
string ToXml(string? xmlNamespace = null);
```

`ToXml` is `FormXml.Serialize(ToDefinition(), xmlNamespace)`.

On `FormElicitation`:

```csharp
public static FormElicitationBuilder FromDefinition(FormDefinition definition)
public static FormElicitationBuilder ParseXml(string xml) => FromDefinition(FormXml.Parse(xml));
```

`FromDefinition` uses the public starters (and `GetOrAdd` in-assembly). Empty `Message` throws. Duplicate field names throw.

### 3.4 Named DI

```csharp
public static IServiceCollection AddFormElicitation(
    this IServiceCollection services,
    string name,
    IFormElicitationSource form)
```

Keyed singleton `IFormElicitationSource` (`AddKeyedSingleton<IFormElicitationSource>(name, form)`). `name` and `form` fail first when null/whitespace. Same form instance may be registered under more than one name; the same name must not be registered twice (`TryAdd` is wrong — `AddKeyedSingleton` throws on duplicate in MS.DI; that is the fail-first we want if the host double-registers. Use `AddKeyedSingleton`. If tests need replace, they build a fresh collection.)

This is host-agnostic (HttpClient analog). OData resolves the name; it does not own the registration API.

---

## 4. OData CSDL

### 4.1 Vocabulary term

In `Microsoft.OData.Mcp.V1.xml`, next to `Task`:

```xml
<Term Name="ElicitForm" Type="Core.Tag" Nullable="false" DefaultValue="true" AppliesTo="Action">
  <Annotation Term="Core.Description"
    String="The action elicits an MCP form before invoke. The compact Form element on this annotation is the request schema." />
</Term>
```

Do not declare ComplexTypes for Form/Field/Option. Instances are the compact island, not `Record`/`PropertyValue`.

`McpVocabulary` grows `ElicitFormTermName` (`ElicitForm`) and `ElicitFormTerm`. `McpVocabularyModel` exposes `ElicitFormTerm`. `McpVocabularyTests` lock the identifiers and the embedded CSDL.

### 4.2 Published `$metadata` shape

```xml
<Action Name="Ship" IsBound="true">
  <Parameter Name="bindingParameter" Type="Sample.Order" />
  <Annotation Term="Mcp.Task" Bool="true" />
  <Annotation Term="Mcp.ElicitForm">
    <Form xmlns="https://github.com/odata/mcp/vocabularies/Microsoft.OData.Mcp.V1.xml"
          Message="Ship this order">
      <Field Name="trackingNumber" Type="string" Required="true"/>
    </Form>
  </Annotation>
</Action>
```

Mechanism: vocabulary annotation (Tag, `Bool="true"` like `Task`) **plus** a direct-value element annotation **on that vocabulary annotation** (`NamespaceUri = McpVocabulary.ReferenceUri`, `Name = "Form"`, `EdmStringConstant` of the island, `SetIsSerializedAsElement(true)`). EdmLib `ProcessAnnotation` → `EndElement` `WriteRaw`s it as a child of `<Annotation>`. Foreign-namespace child, so CsdlReader keeps it (CSDL XML 4.01). `FindTerm` / `FindVocabularyAnnotations` see `ElicitForm`.

Do not stamp a sibling `<Form>` on `<Action>`. Do not dump the island as `String="&lt;Form&gt;…"`. Do not emit OASIS `Record` soup.

### 4.3 Core model

`EdmAction.ElicitForm` is `EdmElicitForm?` (`null` when the term is omitted).

Top-level files (no nested types):

- `EdmElicitForm` — `Message`, `Name`, `List<EdmElicitFormField> Fields`
- `EdmElicitFormField` — same facets as `FormFieldDefinition` using `EdmElicitFormFieldType`
- `EdmElicitFormOption` — `Value`, `Title`, `Selected`
- `EdmElicitFormFieldType` — same six values

Core does **not** reference McpEssentials. `CsdlParser` walks `Form`/`Field`/`Option` itself (same grammar). Out-of-line `<Annotations Target>` must apply the term the same way `Task` already does.

`EdmModelAdapter` (AspNetCore): `FindVocabularyAnnotations` for `ElicitForm`, then `DirectValueAnnotations` on that annotation named `Form`, parse the string with a Core `ElicitFormXml` walker shared with `CsdlParser` (put the walker in Core `Parsing/ElicitFormXml.cs` so Tools and the adapter share one grammar).

`ODataToolDescriptor` copies `ElicitForm` for catalog/describe. Describe text may mention that the action elicits a form; do not embed the XML in the tool description.

---

## 5. Host UX

Fluent builders cannot sit on attributes. An attribute is a name pointer.

```csharp
services.AddFormElicitation("ship",
    FormElicitationBuilder.Create("Ship this order")
        .String("trackingNumber").Required()
        .Choice("priority").Option("low").Option("high").Default("low"));

builder.EntityType<Order>().Action("Ship")
    .HasMcpTask()
    .HasMcpElicitForm("ship");

builder.EntityType<Order>().Action("Ship")
    .HasMcpElicitForm(
        FormElicitationBuilder.Create("Ship this order")
            .String("trackingNumber").Required());

[McpTask]
[McpElicitForm("ship")]
public Order Ship(Order order) => order;
```

### 5.1 Convention (`HasMcpElicitForm`)

`Microsoft.OData.ModelBuilder.ODataModelBuilderMcpExtensions` (AspNetCore):

- `HasMcpElicitForm(this ActionConfiguration action, IFormElicitationSource form)` — stores the instance on `McpElicitFormTermConfiguration`. `GetEdmModel` stamps the term + island from `form.ToXml(McpVocabulary.ReferenceUri)`.
- `HasMcpElicitForm(this ActionConfiguration action, string name)` — stores the DI key. `GetEdmModel` cannot resolve DI. Stamp of the island is deferred to AspNetCore `ODataMcpSessionFactory` Rebuild (same window as `[McpTask]`), which resolves `IFormElicitationSource` keyed by `name` and fails first if missing. The Tag term may still be written at `GetEdmModel` so `FindTerm` is true; the island must be present on the model the session actually serves.

Missing name at Rebuild: `InvalidOperationException` naming the action and the key. Do not wait for the first `tools/call`.

### 5.2 Attribute (`[McpElicitForm]`)

McpEssentials owns `[McpElicitForm]` and `[McpElicitUrl]`. OData Core keeps `[McpTask]` and the CSDL terms. AspNetCore stamps `Mcp.ElicitForm` from the McpEssentials attribute after routes exist, resolving the form from `IServiceProvider`. Functions are never stamped.

### 5.3 Identity

Match `[McpTask]`: CLR method name + bound/unbound shape onto `IEdmAction`. One form per action. A second `HasMcpElicitForm` on the same `ActionConfiguration` replaces the configuration (like `HasMcpElicitUrl`).

`From<T>()` is how a DTO/ComplexType seeds a form. OData does not grow a second EDM-reflection form builder.

---

## 6. Runtime

### 6.1 Hook

`ODataMcpHandlers.CallToolAsync` gains `beforeInvoke`:

```csharp
Func<RequestContext<CallToolRequestParams>, CancellationToken, ValueTask<CallToolRequestParams?>>? beforeInvoke
```

Called after `tryHandleExtra` and after the tool name is known, **before** `InvokeAsync`. Non-null return replaces `request.Params` (merged arguments). Null means “no change”. Throwing `InputRequiredException` is the MRTR path (same as URL elicit).

`WithODataCatalogHandlers` takes `beforeInvoke`. AspNetCore wires `ODataMcpAnnotatedActionTasks.BeforeInvokeAsync`. Tools wires a DTO-based elicit when `EdmAction.ElicitForm` is set.

### 6.2 When to fire

Fire when **all** of:

1. The resolved tool is a named action or `odata_call` of an action whose `ElicitForm` is set (or the in-process live source is set).
2. The client advertised form elicitation: request `_meta` or `ClientCapabilities.Elicitation` includes form (mirror `ClientHasUrlElicitation`; add `ClientHasFormElicitation`).
3. This request is not the MRTR retry that already carries the accepted form content in `inputResponses` for the form key.

If the client omitted form elicitation: do **not** elicit; invoke with the arguments as sent (same spirit as “no URL elicitation → one-shot Started”).

### 6.3 Always elicit

Always show the form when (1)+(2) hold, including when every required value is already in the tool arguments. Schema `default` is the form author's `.Default(...)`. The harness may fill the widget from those arguments using field names; the server does not overlay instance values onto the schema. Decline / cancel: do not POST; return `isError` “The operator declined the form.” (parallel to URL decline).

### 6.4 Transport

| Session | Mechanism |
|---------|-----------|
| Stateless Streamable HTTP (`AddODataMcp`, `IsMrtrSupported`) | `InputRequiredException` with `InputRequest.ForElicitation(params)` (`mode: form`). Key: `odata.elicitForm`. `requestState` holds the original `CallToolRequestParams` arguments JSON so the retry can merge. |
| Task scope / stateful | `server.ElicitAsync(form)` (McpEssentials overload) or `ElicitAsync(ElicitRequestParams)` |

On accept: merge `ElicitResult.Content` into tool arguments (form field names overwrite / fill). Then `InvokeAsync`. Then existing `afterInvoke` URL path.

In-process prefers the live builder (instance or keyed). Remote Tools uses `EdmAction.ElicitForm` → Core projector → `ElicitRequestParams` (SDK types already in Core). Tools does not take a McpEssentials PackageReference for this.

### 6.5 Projector (Core)

`ElicitFormMapper.ToRequestParams(EdmElicitForm form) → ElicitRequestParams` with `Mode = "form"`. Maps the six field types onto the SDK nested schema types the same way `ElicitationField.ToSchema` does. Keep this mapper in Core `Catalog/` or `Vocabularies/`. Tests pin JSON of `ToInputRequest`-equivalent `RequestedSchema` for a golden form.

---

## 7. Package map

| Package | Owns |
|---------|------|
| McpEssentials.Core | snapshot, `FormXml`, `ToXml`/`ParseXml`/`ToDefinition`/`FromDefinition`, `AddFormElicitation` |
| Microsoft.OData.Mcp.Core | vocab term, `[McpElicitForm]`, `ElicitFormXml` parser, `EdmAction.ElicitForm`, catalog copy, `beforeInvoke`, `ElicitFormMapper` |
| Microsoft.OData.Mcp.AspNetCore | stamp (DVA-on-annotation), `HasMcpElicitForm`, attribute Apply + DI resolve, live-builder before-invoke |
| Microsoft.OData.Mcp.Tools | `beforeInvoke` using Core DTO + mapper (no McpEssentials) |

AspNetCore keeps `PackageReference McpEssentials.Core 1.*-*`. After McpEssentials ships the codec, pack a new preview and restore.

---

## 8. Tests (no mocks)

### 8.1 McpEssentials

- Round-trip: untyped form with string/email/integer/boolean/enum/multi → `ToXml` → `ParseXml` → `Build()` schema equals original `Build()`.
- `Required="true"` / `Selected="true"` appear; `Required="false"` does not.
- `Type` spelling: `enum` / `multi` / `integer`.
- `ToXml(xmlNamespace)` emits `xmlns` on `Form`.
- Parse ignores namespace; rejects unknown `Type`; rejects empty `Message`; DTD is refused.
- `AddFormElicitation` resolves keyed `IFormElicitationSource`.
- Facade `ToXml()` matches parent builder `ToXml()`.
- AOT-shaped: `ToXml`/`ParseXml` have no `[RequiresDynamicCode]`.

### 8.2 OData Core

- `McpVocabularyTests` include `ElicitForm`.
- `CsdlParser` reads aliased `Mcp.ElicitForm` with inner `Form`/`Field`/`Option`.
- Term omitted → `ElicitForm` is null. Term without island → empty fields, `Message` empty or throw on elicit (parser may store a form with empty fields; runtime fail-first if `Message` is blank).
- `ElicitFormMapper` golden schema.

### 8.3 OData AspNetCore

- `HasMcpElicitForm(form).GetEdmModel()` HTTP `$metadata` contains `Term="Mcp.ElicitForm"` and `<Form xmlns="https://github.com/odata/mcp/vocabularies/Microsoft.OData.Mcp.V1.xml"`.
- Core adapter / `CsdlParser` of that `$metadata` round-trips field names.
- `[McpElicitForm("ship")]` + `AddFormElicitation("ship", …)` stamps after Rebuild.
- Missing name at Rebuild throws.
- Functions with the attribute are not stamped.
- Before-invoke: client with form elicitation + annotated action → MRTR `mode: form` (or test host `ElicitAsync`) **before** the OData POST; accept merges `trackingNumber` into arguments; decline does not POST.
- Client without form capability: POST proceeds with given arguments.

---

## 9. Non-goals

- Auto-building a form from every unbound parameter.
- Form elicitation on functions or CRUD tools.
- Rebuilding `IEdmModel` per request.
- JSON dump of the schema as a CSDL `String` term.
- OASIS `Record` taxonomy for fields.
- `System.Xml.Serialization.XmlSerializer` / sgen / ModernXmlSerializer.
- Tokens, passwords, `device_code` in form fields ([AUTHENTICATION.md](./AUTHENTICATION.md)).

---

## 10. Implementation order

1. McpEssentials snapshot + `FormXml` + builder methods + `AddFormElicitation` + tests. Pack `1.0.0-preview.5`.
2. Core vocab + parser + models + mapper + `beforeInvoke` plumbing (hook may be no-op until hosts wire it).
3. AspNetCore stamp + `HasMcpElicitForm` + attribute Apply + `$metadata` tests.
4. AspNetCore / Tools before-invoke runtime + host tests.
5. Docs: `elicitations.mdx` / McpEssentials `form-elicitation.mdx`. Do not hand-edit generated API MDX.
