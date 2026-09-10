// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Diagnostics;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Diagnostics
{

    /// <summary>
    /// <see cref="ServiceProbe"/> against the live reference services, plus the pure row interpretation and verdict rules.
    /// </summary>
    [TestClass]
    public class ServiceProbeTests
    {

        #region Fields

        internal const string ThingsCsdl = """
            <?xml version="1.0" encoding="utf-8"?>
            <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
              <edmx:DataServices>
                <Schema Namespace="Probe" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                  <EntityType Name="Thing">
                    <Key><PropertyRef Name="Id" /></Key>
                    <Property Name="Id" Type="Edm.Int32" Nullable="false" />
                    <Property Name="Name" Type="Edm.String" />
                    <NavigationProperty Name="Parts" Type="Collection(Probe.Thing)" />
                  </EntityType>
                  <EntityContainer Name="Container">
                    <EntitySet Name="Things" EntityType="Probe.Thing" />
                  </EntityContainer>
                </Schema>
              </edmx:DataServices>
            </edmx:Edmx>
            """;

        #endregion

        #region Public Methods

        /// <summary>
        /// The first declared set whose type resolves is the one sampled.
        /// </summary>
        [TestMethod]
        public void ChooseEntitySet_PicksFirstDeclaredSet()
        {
            var model = new CsdlParser().ParseFromString(ThingsCsdl);

            var chosen = ServiceProbe.ChooseEntitySet(model);

            chosen.Should().NotBeNull();
            chosen!.Value.Set.Name.Should().Be("Things");
            chosen.Value.Type.FullName.Should().Be("Probe.Thing");
        }

        /// <summary>
        /// Security beats reachability beats readability when deriving the verdict.
        /// </summary>
        [TestMethod]
        public void Decide_OrdersConcernsSecurityFirst()
        {
            var ok = new ProbeStep(ProbeOutcome.Passed, "ok", 200);
            var unauthorized = new ProbeStep(ProbeOutcome.Unauthorized, "401", 401, ["Bearer"]);
            var notFound = new ProbeStep(ProbeOutcome.NotFound, "404", 404);
            var bad = new ProbeStep(ProbeOutcome.Unreadable, "bad", 200);

            ServiceProbeResult.Decide(unauthorized, ProbeStep.NotRun, ProbeStep.NotRun).Should().Be(ServiceVerdict.MetadataSecured);
            ServiceProbeResult.Decide(notFound, ProbeStep.NotRun, ProbeStep.NotRun).Should().Be(ServiceVerdict.Unreachable);
            ServiceProbeResult.Decide(bad, ProbeStep.NotRun, ProbeStep.NotRun).Should().Be(ServiceVerdict.Unreadable);
            ServiceProbeResult.Decide(ok, unauthorized, ProbeStep.NotRun).Should().Be(ServiceVerdict.DataSecured);
            ServiceProbeResult.Decide(ok, notFound, ProbeStep.NotRun).Should().Be(ServiceVerdict.Unreachable);
            ServiceProbeResult.Decide(ok, ok, bad).Should().Be(ServiceVerdict.Unreadable);
            ServiceProbeResult.Decide(ok, ok, ok).Should().Be(ServiceVerdict.Ready);
        }

        /// <summary>
        /// A row whose properties are all declared on the type passes and says so.
        /// </summary>
        [TestMethod]
        public void InterpretRow_AllPropertiesKnown_Passes()
        {
            var type = new CsdlParser().ParseFromString(ThingsCsdl).GetEntityType("Probe.Thing")!;

            var step = ServiceProbe.InterpretRow(type, """{"@odata.context":"$metadata#Things","value":[{"@odata.etag":"W/1","Id":1,"Name":"a"}]}""");

            step.Outcome.Should().Be(ProbeOutcome.Passed);
            step.Detail.Should().Be("1 Thing row; all 2 properties match the model");
        }

        /// <summary>
        /// An empty set passes with a note instead of failing on a missing row.
        /// </summary>
        [TestMethod]
        public void InterpretRow_EmptySet_PassesWithNote()
        {
            var type = new CsdlParser().ParseFromString(ThingsCsdl).GetEntityType("Probe.Thing")!;

            var step = ServiceProbe.InterpretRow(type, """{"value":[]}""");

            step.Outcome.Should().Be(ProbeOutcome.Passed);
            step.Detail.Should().Contain("empty");
        }

        /// <summary>
        /// Bodies that are not JSON, not collections, or share no property names with the type are unreadable.
        /// </summary>
        [TestMethod]
        public void InterpretRow_UnexpectedBodies_AreUnreadable()
        {
            var type = new CsdlParser().ParseFromString(ThingsCsdl).GetEntityType("Probe.Thing")!;

            ServiceProbe.InterpretRow(type, "<html>login</html>").Outcome.Should().Be(ProbeOutcome.Unreadable);
            ServiceProbe.InterpretRow(type, """{"d":{"results":[]}}""").Outcome.Should().Be(ProbeOutcome.Unreadable, "OData v3 verbose JSON has no value array");
            ServiceProbe.InterpretRow(type, """{"value":[{"foo":1,"bar":2}]}""").Outcome.Should().Be(ProbeOutcome.Unreadable, "no property is declared on the type");
            ServiceProbe.InterpretRow(type, """{"value":[1,2]}""").Outcome.Should().Be(ProbeOutcome.Unreadable);
        }

        /// <summary>
        /// A row with some undeclared properties still passes but names them.
        /// </summary>
        [TestMethod]
        public void InterpretRow_SomeUnknownProperties_PassesAndListsThem()
        {
            var type = new CsdlParser().ParseFromString(ThingsCsdl).GetEntityType("Probe.Thing")!;

            var step = ServiceProbe.InterpretRow(type, """{"value":[{"Id":1,"Name":"a","Extra":true}]}""");

            step.Outcome.Should().Be(ProbeOutcome.Passed);
            step.Detail.Should().Be("1 Thing row; 2 of 3 properties match the model, unknown: Extra");
        }

        /// <summary>
        /// Live Northwind is open end to end.
        /// </summary>
        [TestMethod]
        public async Task ProbeAsync_Northwind_IsReady()
        {
            using var client = new HttpClient();

            var result = await new ServiceProbe(client, new CsdlParser()).ProbeAsync(new Uri(LiveOData.Northwind), CancellationToken.None);

            result.Verdict.Should().Be(ServiceVerdict.Ready, result.ToString());
            result.ServiceRoot.ToString().Should().EndWith("/");
            result.Metadata.Outcome.Should().Be(ProbeOutcome.Passed);
            result.Metadata.Detail.Should().Contain("26 entity sets");
            result.EntitySet.Should().Be("Categories");
            result.Data.StatusCode.Should().Be(200);
            result.Results.Detail.Should().Be("1 Category row; all 4 properties match the model");
            result.Challenges.Should().BeEmpty();
        }

        /// <summary>
        /// Live TripPin is open end to end, enums and complex types included.
        /// </summary>
        [TestMethod]
        public async Task ProbeAsync_TripPin_IsReady()
        {
            using var client = new HttpClient();

            var result = await new ServiceProbe(client, new CsdlParser()).ProbeAsync(new Uri(LiveOData.TripPin), CancellationToken.None);

            result.Verdict.Should().Be(ServiceVerdict.Ready, result.ToString());
            result.EntitySet.Should().Be("People");
            result.EntityType.Should().Be("Trippin.Person");
            result.Results.Outcome.Should().Be(ProbeOutcome.Passed);
        }

        /// <summary>
        /// A root that is not an OData service at all is unreachable or unreadable, never an exception.
        /// </summary>
        [TestMethod]
        public async Task ProbeAsync_NotAnODataService_DoesNotThrow()
        {
            using var client = new HttpClient();

            var result = await new ServiceProbe(client, new CsdlParser()).ProbeAsync(new Uri("https://www.example.com/"), CancellationToken.None);

            result.Verdict.Should().BeOneOf(ServiceVerdict.Unreachable, ServiceVerdict.Unreadable);
            result.Model.Should().BeNull();
            result.Data.Outcome.Should().Be(ProbeOutcome.Skipped);
        }

        /// <summary>
        /// A model without an entity set skips the data step instead of guessing a URL.
        /// </summary>
        [TestMethod]
        public async Task ProbeWithModelAsync_NoEntitySets_SkipsData()
        {
            var model = new CsdlParser().ParseFromString(ThingsCsdl);
            model.EntityContainers.Clear();
            using var client = new HttpClient();

            var result = await new ServiceProbe(client, new CsdlParser()).ProbeWithModelAsync(new Uri("https://localhost:1/never/"), model, CancellationToken.None);

            result.Data.Outcome.Should().Be(ProbeOutcome.Skipped);
            result.Verdict.Should().Be(ServiceVerdict.Ready, "nothing failed; there was simply nothing to sample");
        }

        #endregion

    }

}
