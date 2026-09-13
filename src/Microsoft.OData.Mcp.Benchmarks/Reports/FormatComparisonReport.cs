// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.Mcp.Benchmarks.Infrastructure;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.Shared;

namespace Microsoft.OData.Mcp.Benchmarks.Reports
{

    /// <summary>
    /// The same model in the three formats a model could be handed: CSDL XML as the service serves it, the
    /// OData CSDL JSON representation of the same document, and the MCP shapes this project emits. Writes
    /// <c>FORMATS.md</c> plus every measured artifact under <c>Formats/</c> so readers can see what was counted.
    /// </summary>
    public static class FormatComparisonReport
    {

        #region Fields

        /// <summary>
        /// The report file name.
        /// </summary>
        public const string FileName = "FORMATS.md";

        /// <summary>
        /// The folder, under the output directory, that receives every measured artifact.
        /// </summary>
        public const string FormatsFolder = "Formats";

        #endregion

        #region Public Methods

        /// <summary>
        /// Measures both live services and writes the report and its artifacts.
        /// </summary>
        /// <param name="outputDirectory">An existing directory.</param>
        /// <returns>
        /// A task that completes when everything is written.
        /// </returns>
        public static async Task WriteAsync(string outputDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

            var formats = Path.Combine(outputDirectory, FormatsFolder);
            Directory.CreateDirectory(formats);

            var sections = new List<ServiceFormats>();
            foreach (var name in LiveModels.Names())
            {
                var service = await LiveModels.LoadAsync(name).ConfigureAwait(false);
                var measured = await MeasureAsync(service).ConfigureAwait(false);
                foreach (var artifact in measured.Type.Concat(measured.Model))
                {
                    await File.WriteAllTextAsync(Path.Combine(formats, artifact.FileName), artifact.Content, new UTF8Encoding(false)).ConfigureAwait(false);
                }

                sections.Add(measured);
            }

            await File.WriteAllTextAsync(Path.Combine(outputDirectory, FileName), Render(sections), new UTF8Encoding(false)).ConfigureAwait(false);
            Console.WriteLine($"  {FileName} (+ {FormatsFolder}/)");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Appends one format table; the first artifact is the CSDL XML baseline the others are compared with.
        /// </summary>
        /// <param name="report">The report being built.</param>
        /// <param name="artifacts">The artifacts, CSDL XML first.</param>
        internal static void AppendTable(StringBuilder report, IReadOnlyList<Artifact> artifacts)
        {
            var baseline = artifacts[0].Tokens;
            report.Append("| Format | Tokens | vs CSDL XML | File |\n");
            report.Append("|---|---:|---:|---|\n");
            foreach (var artifact in artifacts)
            {
                var change = ReferenceEquals(artifact, artifacts[0]) ? "—" : TokenReport.FormatPercent(baseline, artifact.Tokens);
                report.Append($"| {artifact.Label} | {artifact.Tokens} | {change} | [`{artifact.FileName}`](./{FormatsFolder}/{artifact.FileName}) |\n");
            }
        }

        /// <summary>
        /// Decides whether a CSDL JSON operation overload is bound to the given type.
        /// </summary>
        /// <param name="overload">One element of an operation's overload array.</param>
        /// <param name="fullName">The qualified type name.</param>
        /// <returns>
        /// <c>true</c> when <c>$IsBound</c> is set and the binding parameter's <c>$Type</c> is <paramref name="fullName"/>.
        /// </returns>
        internal static bool IsBoundTo(JsonElement overload, string fullName)
        {
            if (!overload.TryGetProperty("$IsBound", out var isBound) || isBound.ValueKind != JsonValueKind.True)
            {
                return false;
            }

            if (!overload.TryGetProperty("$Parameter", out var parameters) || parameters.ValueKind != JsonValueKind.Array || parameters.GetArrayLength() == 0)
            {
                return false;
            }

            return parameters[0].TryGetProperty("$Type", out var type) && type.GetString() == fullName;
        }

        /// <summary>
        /// Decides whether a CSDL XML <c>Action</c> or <c>Function</c> element is bound to the given type.
        /// </summary>
        /// <param name="operation">The operation element.</param>
        /// <param name="fullName">The qualified type name.</param>
        /// <returns>
        /// <c>true</c> when <c>IsBound="true"</c> and the first parameter's type is the type or a collection of it.
        /// </returns>
        internal static bool IsBoundTo(XElement operation, string fullName)
        {
            if (!string.Equals((string?)operation.Attribute("IsBound"), "true", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var binding = operation.Elements().FirstOrDefault(element => element.Name.LocalName == "Parameter");
            var type = (string?)binding?.Attribute("Type");

            return type == fullName || type == $"Collection({fullName})";
        }

        /// <summary>
        /// Extracts the CSDL JSON members that describe one entity type: the type itself, the enums it uses, the
        /// operations bound to it, and its entity set.
        /// </summary>
        /// <param name="csdlJson">The whole-model CSDL JSON.</param>
        /// <param name="service">The service.</param>
        /// <param name="names">The member names to keep.</param>
        /// <returns>
        /// A compact JSON object keyed by member name.
        /// </returns>
        internal static string JsonFragment(string csdlJson, LiveService service, HashSet<string> names)
        {
            using var document = JsonDocument.Parse(csdlJson);
            var root = document.RootElement;
            var fullName = service.EntityType.FullName;
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            {
                writer.WriteStartObject();
                foreach (var schema in root.EnumerateObject().Where(property => !property.Name.StartsWith('$') && property.Value.ValueKind == JsonValueKind.Object))
                {
                    foreach (var member in schema.Value.EnumerateObject().Where(member => names.Contains(member.Name)))
                    {
                        if (member.Value.ValueKind == JsonValueKind.Array)
                        {
                            var overloads = member.Value.EnumerateArray().Where(overload => IsBoundTo(overload, fullName)).ToList();
                            if (overloads.Count == 0)
                            {
                                continue;
                            }

                            writer.WritePropertyName(member.Name);
                            writer.WriteStartArray();
                            foreach (var overload in overloads)
                            {
                                overload.WriteTo(writer);
                            }

                            writer.WriteEndArray();
                            continue;
                        }

                        writer.WritePropertyName(member.Name);
                        member.Value.WriteTo(writer);
                    }
                }

                if (root.TryGetProperty("$EntityContainer", out var containerName) && containerName.GetString() is { } qualified)
                {
                    var dot = qualified.LastIndexOf('.');
                    if (dot > 0
                        && root.TryGetProperty(qualified[..dot], out var containerSchema)
                        && containerSchema.TryGetProperty(qualified[(dot + 1)..], out var container)
                        && container.TryGetProperty(service.EntitySet, out var entitySet))
                    {
                        writer.WritePropertyName(service.EntitySet);
                        entitySet.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>
        /// Measures one service: four renderings of the reference type and five of the whole model.
        /// </summary>
        /// <param name="service">The service.</param>
        /// <returns>
        /// The measured artifacts.
        /// </returns>
        internal static async Task<ServiceFormats> MeasureAsync(LiveService service)
        {
            var prefix = service.Name.ToLowerInvariant();
            var typeName = service.EntityType.Name;
            var slug = typeName.ToLowerInvariant();
            var catalog = new ODataMcpCatalog(service.Model, new ODataMcpCatalogOptions());
            var runtime = new ODataToolRuntime(catalog, new NoopODataExecutor());
            var shape = catalog.GetShape(service.EntityType);
            var csdlJson = WriteCsdlJson(ParseEdm(service.Xml));
            var names = RelatedNames(service, shape);

            var type = new List<Artifact>
            {
                new("CSDL XML (EDMX)", $"{prefix}.{slug}.edmx.xml", XmlFragment(service.Xml, service, names)),
                new("CSDL JSON", $"{prefix}.{slug}.csdl.json", JsonFragment(csdlJson, service, names)),
                new("odata_describe_type (text, default)", $"{prefix}.{slug}.shape.txt", await TextAsync(runtime, "odata_describe_type", ToolArguments.Of("name", service.EntitySet)).ConfigureAwait(false)),
                new("odata_describe_type (format=json)", $"{prefix}.{slug}.shape.json", await StructuredAsync(runtime, "odata_describe_type", ToolArguments.Of("name", service.EntitySet, "format", "json")).ConfigureAwait(false))
            };

            var model = new List<Artifact>
            {
                new("CSDL XML (EDMX)", $"{prefix}.model.edmx.xml", service.Xml),
                new("CSDL JSON", $"{prefix}.model.csdl.json", csdlJson),
                new("odata_describe_model (detail=complete, text)", $"{prefix}.model.complete.txt", await TextAsync(runtime, "odata_describe_model", ToolArguments.Of("detail", "complete")).ConfigureAwait(false)),
                new("odata_describe_model (detail=complete, format=json)", $"{prefix}.model.complete.json", await StructuredAsync(runtime, "odata_describe_model", ToolArguments.Of("detail", "complete", "format", "json")).ConfigureAwait(false)),
                new("odata_describe_model (summary, default)", $"{prefix}.model.summary.txt", await TextAsync(runtime, "odata_describe_model", null).ConfigureAwait(false))
            };

            return new ServiceFormats(service.Name, typeName, service.EntitySet, service.Model.AllEntitySets.Count(), type, model);
        }

        /// <summary>
        /// Parses CSDL XML with EdmLib so it can be re-serialized as CSDL JSON.
        /// </summary>
        /// <param name="xml">The CSDL document.</param>
        /// <returns>
        /// The EdmLib model.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown when EdmLib reports errors.</exception>
        internal static IEdmModel ParseEdm(string xml)
        {
            using var reader = XmlReader.Create(new StringReader(xml));
            if (!CsdlReader.TryParse(reader, out var model, out var errors))
            {
                throw new InvalidOperationException("EdmLib could not parse the CSDL: " + string.Join("; ", errors.Select(error => error.ErrorMessage)));
            }

            return model;
        }

        /// <summary>
        /// Collects the schema member names that belong with one entity type.
        /// </summary>
        /// <param name="service">The service.</param>
        /// <param name="shape">The type shape.</param>
        /// <returns>
        /// The type name, its entity set, every enum its exposed properties use, and every bound operation.
        /// </returns>
        internal static HashSet<string> RelatedNames(LiveService service, EdmTypeShape shape)
        {
            var names = new HashSet<string>(StringComparer.Ordinal) { service.EntityType.Name, service.EntitySet };
            foreach (var property in shape.ExposedProperties)
            {
                var typeName = property.Type;
                if (typeName.StartsWith("Collection(", StringComparison.Ordinal) && typeName.EndsWith(')'))
                {
                    typeName = typeName[11..^1];
                }

                if (service.Model.GetEnumType(typeName) is { } enumType)
                {
                    names.Add(enumType.Name);
                }
            }

            foreach (var action in shape.BoundActions)
            {
                names.Add(action.Name);
            }

            foreach (var function in shape.BoundFunctions)
            {
                names.Add(function.Name);
            }

            return names;
        }

        /// <summary>
        /// Renders the markdown report.
        /// </summary>
        /// <param name="sections">One entry per service.</param>
        /// <returns>
        /// Markdown with LF line endings.
        /// </returns>
        internal static string Render(IReadOnlyList<ServiceFormats> sections)
        {
            var report = new StringBuilder();
            report.Append("# Same model, three formats\n\n");
            report.Append("An MCP client that wants to know what an OData service looks like can be handed the raw CSDL XML (`$metadata`), the same document as OData CSDL JSON, or the shapes this project emits. ");
            report.Append($"These are `{Tokenizer.EncodingName}` token counts of each, measured against the public Northwind and TripPin services. ");
            report.Append("Every counted artifact is in [`Formats/`](./Formats); generated by `dotnet run -c Release -- --tokens` in `Microsoft.OData.Mcp.Benchmarks`.\n\n");
            report.Append("CSDL JSON is written by EdmLib from the same model, compact (no whitespace), which is how a service would serve it. CSDL XML is counted exactly as the service returns it.\n");

            report.Append("\n## One entity type\n\n");
            report.Append("What a model receives when it asks about one type. The XML and JSON rows are the fragments of the full document that describe that type: the entity type, the enums it uses, the operations bound to it, and its entity set.\n");
            foreach (var section in sections)
            {
                report.Append($"\n### {section.Service} `{section.TypeName}`\n\n");
                AppendTable(report, section.Type);
            }

            report.Append("\n## Whole service\n\n");
            report.Append("What a model receives when it asks for the entire model.\n");
            foreach (var section in sections)
            {
                report.Append($"\n### {section.Service} ({section.EntitySetCount} entity sets)\n\n");
                AppendTable(report, section.Model);
            }

            report.Append("\n## Side by side\n");
            foreach (var section in sections)
            {
                report.Append($"\n### {section.Service} `{section.TypeName}`\n");
                foreach (var artifact in section.Type.Take(3))
                {
                    var fence = artifact.FileName.EndsWith(".xml", StringComparison.Ordinal) ? "xml" : artifact.FileName.EndsWith(".json", StringComparison.Ordinal) ? "json" : "text";
                    report.Append($"\n**{artifact.Label}** — {artifact.Tokens} tokens\n\n```{fence}\n{artifact.Content.TrimEnd()}\n```\n");
                }
            }

            return report.ToString();
        }

        /// <summary>
        /// Invokes a tool and returns its structured content.
        /// </summary>
        /// <param name="runtime">The runtime.</param>
        /// <param name="name">The tool name.</param>
        /// <param name="arguments">The arguments.</param>
        /// <returns>
        /// The structured content JSON.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown when the tool errors or returns no structured content.</exception>
        internal static async Task<string> StructuredAsync(ODataToolRuntime runtime, string name, Dictionary<string, JsonElement>? arguments)
        {
            var result = await runtime.InvokeAsync(name, arguments, CancellationToken.None).ConfigureAwait(false);
            if (result.IsError || string.IsNullOrWhiteSpace(result.StructuredContent))
            {
                throw new InvalidOperationException($"{name} did not return structured content: {result.Text}");
            }

            return result.StructuredContent;
        }

        /// <summary>
        /// Invokes a tool and returns its text.
        /// </summary>
        /// <param name="runtime">The runtime.</param>
        /// <param name="name">The tool name.</param>
        /// <param name="arguments">The arguments.</param>
        /// <returns>
        /// The text content.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown when the tool errors.</exception>
        internal static async Task<string> TextAsync(ODataToolRuntime runtime, string name, Dictionary<string, JsonElement>? arguments)
        {
            var result = await runtime.InvokeAsync(name, arguments, CancellationToken.None).ConfigureAwait(false);
            if (result.IsError || string.IsNullOrWhiteSpace(result.Text))
            {
                throw new InvalidOperationException($"{name} did not return text: {result.Text}");
            }

            return result.Text;
        }

        /// <summary>
        /// Serializes an EdmLib model as compact CSDL JSON.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns>
        /// The CSDL JSON document.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown when EdmLib reports errors.</exception>
        internal static string WriteCsdlJson(IEdmModel model)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            {
                if (!CsdlWriter.TryWriteCsdl(model, writer, new CsdlJsonWriterSettings(), out var errors))
                {
                    throw new InvalidOperationException("EdmLib could not write CSDL JSON: " + string.Join("; ", errors.Select(error => error.ErrorMessage)));
                }
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>
        /// Extracts the CSDL XML elements that describe one entity type, keeping the service's own formatting.
        /// </summary>
        /// <param name="xml">The whole CSDL document.</param>
        /// <param name="service">The service.</param>
        /// <param name="names">The element names to keep.</param>
        /// <returns>
        /// The matching elements, one per line group.
        /// </returns>
        internal static string XmlFragment(string xml, LiveService service, HashSet<string> names)
        {
            var document = XDocument.Parse(xml);
            var fullName = service.EntityType.FullName;
            var elements = document.Descendants()
                .Where(element => element.Name.LocalName is "EntityType" or "EnumType" or "Action" or "Function" or "EntitySet")
                .Where(element => names.Contains((string?)element.Attribute("Name") ?? string.Empty))
                .Where(element => element.Name.LocalName is not ("Action" or "Function") || IsBoundTo(element, fullName))
                .Where(element => element.Name.LocalName != "EntityType" || (string?)element.Parent?.Attribute("Namespace") == service.EntityType.Namespace);

            // A detached element re-declares its namespace on serialization; the service's own document declares it
            // once on <Schema>, so strip it to count what the service actually sends.
            var edmNamespace = $" xmlns=\"{elements.FirstOrDefault()?.Name.NamespaceName}\"";

            return string.Join("\n", elements.Select(element => element.ToString().Replace(edmNamespace, string.Empty, StringComparison.Ordinal)));
        }

        #endregion

    }

}
