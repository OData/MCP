// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Xml;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Parsing
{

    /// <summary>
    /// Compact form island reader used by <see cref="CsdlParser"/>.
    /// </summary>
    [TestClass]
    public class ElicitFormXmlTests
    {

        #region Public Methods

        /// <summary>
        /// A kitchen-sink island copies every declared facet.
        /// </summary>
        [TestMethod]
        public void Parse_KitchenSink_CopiesFacets()
        {
            const string xml =
                """
                <Form xmlns="https://github.com/odata/mcp/vocabularies/Microsoft.OData.Mcp.V1.xml"
                      Message="Fill this out" Name="ship">
                  <Field Name="firstName" Type="string" Required="true" Title="Given name"
                         Description="Legal first name" MinLength="1" MaxLength="40" Default="Ada"/>
                  <Field Name="age" Type="integer" Min="0" Max="130" Default="42"/>
                  <Field Name="on" Type="boolean" Default="true"/>
                  <Field Name="color" Type="enum">
                    <Option Value="red" Title="Red" Selected="true"/>
                    <Option Value="blue"/>
                  </Field>
                  <Field Name="roles" Type="multi" Min="1" Max="2">
                    <Option Value="admin" Title="Administrator"/>
                    <Option Value="user" Selected="true"/>
                  </Field>
                </Form>
                """;

            var form = ElicitFormXml.Parse(xml);
            form.Message.Should().Be("Fill this out");
            form.Name.Should().Be("ship");
            form.Fields.Should().HaveCount(5);
            form.Fields[0].Required.Should().BeTrue();
            form.Fields[0].DefaultString.Should().Be("Ada");
            form.Fields[1].Type.Should().Be(EdmElicitFormFieldType.Integer);
            form.Fields[1].DefaultNumber.Should().Be(42);
            form.Fields[2].DefaultBool.Should().BeTrue();
            form.Fields[3].DefaultString.Should().Be("red");
            form.Fields[4].MinItems.Should().Be(1);
            form.Fields[4].MaxItems.Should().Be(2);
            form.Fields[4].DefaultStrings.Should().Equal("user");
        }

        /// <summary>
        /// Blank XML is rejected.
        /// </summary>
        [TestMethod]
        public void Parse_BlankXml_ThrowsArgumentException()
        {
            var act = () => ElicitFormXml.Parse("  ");
            act.Should().Throw<ArgumentException>().WithParameterName("xml");
        }

        /// <summary>
        /// A missing Form root is rejected.
        /// </summary>
        [TestMethod]
        public void Parse_NoFormRoot_ThrowsInvalidOperationException()
        {
            var act = () => ElicitFormXml.Parse("""<Nope Message="m"/>""");
            act.Should().Throw<InvalidOperationException>().WithMessage("*Form root*");
        }

        /// <summary>
        /// Unknown field types are rejected.
        /// </summary>
        [TestMethod]
        public void Parse_UnknownType_ThrowsInvalidOperationException()
        {
            const string xml = """<Form Message="m"><Field Name="a" Type="object"/></Form>""";
            var act = () => ElicitFormXml.Parse(xml);
            act.Should().Throw<InvalidOperationException>().WithMessage("*Unknown form field Type 'object'*");
        }

        /// <summary>
        /// A DTD is refused.
        /// </summary>
        [TestMethod]
        public void Parse_Dtd_ThrowsXmlException()
        {
            const string xml =
                """
                <!DOCTYPE Form [
                  <!ENTITY xxe SYSTEM "file:///etc/passwd">
                ]>
                <Form Message="x">&xxe;</Form>
                """;

            var act = () => ElicitFormXml.Parse(xml);
            act.Should().Throw<XmlException>();
        }

        /// <summary>
        /// Duplicate field names are rejected.
        /// </summary>
        [TestMethod]
        public void Parse_DuplicateFieldName_ThrowsInvalidOperationException()
        {
            const string xml =
                """
                <Form Message="m">
                  <Field Name="a" Type="string"/>
                  <Field Name="a" Type="integer"/>
                </Form>
                """;

            var act = () => ElicitFormXml.Parse(xml);
            act.Should().Throw<InvalidOperationException>().WithMessage("*already has a field named 'a'*");
        }

        /// <summary>
        /// Mixed content is rejected.
        /// </summary>
        [TestMethod]
        public void Parse_MixedContent_ThrowsInvalidOperationException()
        {
            const string xml = """<Form Message="m">hello<Field Name="a" Type="string"/></Form>""";
            var act = () => ElicitFormXml.Parse(xml);
            act.Should().Throw<InvalidOperationException>().WithMessage("*mixed content*");
        }

        /// <summary>
        /// A processing instruction inside Form is an unexpected node.
        /// </summary>
        [TestMethod]
        public void Parse_ProcessingInstruction_ThrowsInvalidOperationException()
        {
            const string xml = """<Form Message="m"><?pi data?><Field Name="a" Type="string"/></Form>""";
            var act = () => ElicitFormXml.Parse(xml);
            act.Should().Throw<InvalidOperationException>().WithMessage("*unexpected node*");
        }

        /// <summary>
        /// Default on enum is rejected.
        /// </summary>
        [TestMethod]
        public void Parse_DefaultOnEnum_ThrowsInvalidOperationException()
        {
            const string xml =
                """
                <Form Message="m">
                  <Field Name="color" Type="enum" Default="red">
                    <Option Value="red"/>
                  </Field>
                </Form>
                """;

            var act = () => ElicitFormXml.Parse(xml);
            act.Should().Throw<InvalidOperationException>().WithMessage("*Selected on Option*");
        }

        /// <summary>
        /// Two Selected options on enum fail first.
        /// </summary>
        [TestMethod]
        public void Parse_TwoSelectedOnEnum_ThrowsInvalidOperationException()
        {
            const string xml =
                """
                <Form Message="m">
                  <Field Name="color" Type="enum">
                    <Option Value="red" Selected="true"/>
                    <Option Value="blue" Selected="true"/>
                  </Field>
                </Form>
                """;

            var act = () => ElicitFormXml.Parse(xml);
            act.Should().Throw<InvalidOperationException>().WithMessage("*at most one Selected*");
        }

        /// <summary>
        /// An unexpected Form child is rejected.
        /// </summary>
        [TestMethod]
        public void Parse_UnexpectedFormChild_ThrowsInvalidOperationException()
        {
            const string xml = """<Form Message="m"><Nope/></Form>""";
            var act = () => ElicitFormXml.Parse(xml);
            act.Should().Throw<InvalidOperationException>().WithMessage("*unexpected child 'Nope'*");
        }

        /// <summary>
        /// An unknown Field attribute is rejected.
        /// </summary>
        [TestMethod]
        public void Parse_UnknownFieldAttribute_ThrowsInvalidOperationException()
        {
            const string xml = """<Form Message="m"><Field Name="a" Type="string" Pattern="x+"/></Form>""";
            var act = () => ElicitFormXml.Parse(xml);
            act.Should().Throw<InvalidOperationException>().WithMessage("*Unknown form Field attribute*");
        }

        /// <summary>
        /// A missing Field Name is rejected.
        /// </summary>
        [TestMethod]
        public void Parse_MissingFieldName_ThrowsInvalidOperationException()
        {
            const string xml = """<Form Message="m"><Field Type="string"/></Form>""";
            var act = () => ElicitFormXml.Parse(xml);
            act.Should().Throw<InvalidOperationException>().WithMessage("*Field Name is required*");
        }

        /// <summary>
        /// A missing Option Value is rejected.
        /// </summary>
        [TestMethod]
        public void Parse_MissingOptionValue_ThrowsInvalidOperationException()
        {
            const string xml = """<Form Message="m"><Field Name="c" Type="enum"><Option Title="Red"/></Field></Form>""";
            var act = () => ElicitFormXml.Parse(xml);
            act.Should().Throw<InvalidOperationException>().WithMessage("*Option Value is required*");
        }

        /// <summary>
        /// Non-finite Min fails first.
        /// </summary>
        [TestMethod]
        public void Parse_NonFiniteMin_ThrowsInvalidOperationException()
        {
            const string xml = """<Form Message="m"><Field Name="age" Type="integer" Min="INF"/></Form>""";
            var act = () => ElicitFormXml.Parse(xml);
            act.Should().Throw<InvalidOperationException>().WithMessage("*not a number*");
        }

        /// <summary>
        /// Multi Min overflow fails first.
        /// </summary>
        [TestMethod]
        public void Parse_MultiMinOutOfRange_ThrowsInvalidOperationException()
        {
            const string xml =
                """
                <Form Message="m">
                  <Field Name="roles" Type="multi" Min="1E+20">
                    <Option Value="admin"/>
                  </Field>
                </Form>
                """;

            var act = () => ElicitFormXml.Parse(xml);
            act.Should().Throw<InvalidOperationException>().WithMessage("*out of range*");
        }

        /// <summary>
        /// An unknown Form attribute is rejected.
        /// </summary>
        [TestMethod]
        public void Parse_UnknownFormAttribute_ThrowsInvalidOperationException()
        {
            var act = () => ElicitFormXml.Parse("""<Form Message="m" Mode="dialog"/>""");
            act.Should().Throw<InvalidOperationException>().WithMessage("*Unknown form Form attribute*");
        }

        /// <summary>
        /// An unexpected Option child is rejected.
        /// </summary>
        [TestMethod]
        public void Parse_UnexpectedOptionChild_ThrowsInvalidOperationException()
        {
            const string xml = """<Form Message="m"><Field Name="c" Type="enum"><Option Value="red"><Nope/></Option></Field></Form>""";
            var act = () => ElicitFormXml.Parse(xml);
            act.Should().Throw<InvalidOperationException>().WithMessage("*unexpected child*");
        }

        /// <summary>
        /// An invalid boolean token is rejected.
        /// </summary>
        [TestMethod]
        public void Parse_InvalidBoolean_ThrowsInvalidOperationException()
        {
            const string xml = """<Form Message="m"><Field Name="a" Type="string" Required="yes"/></Form>""";
            var act = () => ElicitFormXml.Parse(xml);
            act.Should().Throw<InvalidOperationException>().WithMessage("*not a boolean*");
        }

        /// <summary>
        /// Empty message is rejected.
        /// </summary>
        [TestMethod]
        public void Parse_EmptyMessage_ThrowsInvalidOperationException()
        {
            var act = () => ElicitFormXml.Parse("""<Form Message="  "/>""");
            act.Should().Throw<InvalidOperationException>().WithMessage("*message cannot be empty*");
        }

        /// <summary>
        /// False boolean tokens are not required.
        /// </summary>
        [TestMethod]
        [DataRow("false")]
        [DataRow("0")]
        public void Parse_RequiredFalse_IsNotRequired(string token)
        {
            var xml = $"""<Form Message="m"><Field Name="a" Type="string" Required="{token}"/></Form>""";
            ElicitFormXml.Parse(xml).Fields[0].Required.Should().BeFalse();
        }

        /// <summary>
        /// Invalid and overflowing integers fail first.
        /// </summary>
        [TestMethod]
        public void Parse_InvalidAndOverflowMinLength_Throw()
        {
            var invalid = () => ElicitFormXml.Parse("""<Form Message="m"><Field Name="a" Type="string" MinLength="nope"/></Form>""");
            var overflow = () => ElicitFormXml.Parse("""<Form Message="m"><Field Name="a" Type="string" MinLength="2147483648"/></Form>""");

            invalid.Should().Throw<InvalidOperationException>().WithMessage("*not an integer*");
            overflow.Should().Throw<InvalidOperationException>().WithMessage("*not an integer*");
        }

        /// <summary>
        /// Email format, number type, and option titles round-trip.
        /// </summary>
        [TestMethod]
        public void Parse_FormatNumberAndOptionTitle()
        {
            const string xml =
                """
                <Form Message="m" xml:lang="en">
                  <Field Name="email" Type="string" Format="email" Description="Work"/>
                  <Field Name="price" Type="number" Min="0.5" Max="9.9" Default="1.5"/>
                  <Field Name="on" Type="boolean" Default="false"/>
                  <Field Name="color" Type="enum">
                    <Option Value="red" Title="Red"/>
                  </Field>
                </Form>
                """;

            var form = ElicitFormXml.Parse(xml);
            form.Fields[0].Format.Should().Be("email");
            form.Fields[0].Description.Should().Be("Work");
            form.Fields[1].Type.Should().Be(EdmElicitFormFieldType.Number);
            form.Fields[1].DefaultNumber.Should().Be(1.5);
            form.Fields[2].DefaultBool.Should().BeFalse();
            form.Fields[3].Options[0].Title.Should().Be("Red");
        }

        /// <summary>
        /// An unexpected Field child and Option mixed content fail first.
        /// </summary>
        [TestMethod]
        public void Parse_UnexpectedFieldChildAndOptionMixed_Throw()
        {
            var child = () => ElicitFormXml.Parse("""<Form Message="m"><Field Name="a" Type="string"><Nope/></Field></Form>""");
            var mixed = () => ElicitFormXml.Parse("""<Form Message="m"><Field Name="c" Type="enum"><Option Value="red">x</Option></Field></Form>""");
            var unknownOption = () => ElicitFormXml.Parse("""<Form Message="m"><Field Name="c" Type="enum"><Option Value="red" Extra="1"/></Field></Form>""");

            child.Should().Throw<InvalidOperationException>().WithMessage("*unexpected child*");
            mixed.Should().Throw<InvalidOperationException>().WithMessage("*mixed content*");
            unknownOption.Should().Throw<InvalidOperationException>().WithMessage("*Unknown form Option attribute*");
        }

        /// <summary>
        /// Invalid number tokens fail first.
        /// </summary>
        [TestMethod]
        public void Parse_InvalidMin_ThrowsInvalidOperationException()
        {
            const string xml = """<Form Message="m"><Field Name="age" Type="integer" Min="nope"/></Form>""";
            var act = () => ElicitFormXml.Parse(xml);
            act.Should().Throw<InvalidOperationException>().WithMessage("*not a number*");
        }

        /// <summary>
        /// A missing Field Type is rejected.
        /// </summary>
        [TestMethod]
        public void Parse_MissingFieldType_ThrowsInvalidOperationException()
        {
            const string xml = """<Form Message="m"><Field Name="a"/></Form>""";
            var act = () => ElicitFormXml.Parse(xml);
            act.Should().Throw<InvalidOperationException>().WithMessage("*missing Type*");
        }

        /// <summary>
        /// xmlns and xml:lang on Field are skipped.
        /// </summary>
        [TestMethod]
        public void Parse_FieldXmlnsAndLang_AreIgnored()
        {
            const string xml =
                """
                <Form Message="m">
                  <Field Name="a" Type="string" xmlns:foo="http://example.com" xml:lang="en"/>
                </Form>
                """;

            ElicitFormXml.Parse(xml).Fields[0].Name.Should().Be("a");
        }

        /// <summary>
        /// An Option with no attributes is rejected.
        /// </summary>
        [TestMethod]
        public void Parse_OptionNoAttributes_ThrowsInvalidOperationException()
        {
            const string xml = """<Form Message="m"><Field Name="c" Type="enum"><Option/></Field></Form>""";
            var act = () => ElicitFormXml.Parse(xml);
            act.Should().Throw<InvalidOperationException>().WithMessage("*Option Value is required*");
        }

        /// <summary>
        /// xmlns on Option is skipped.
        /// </summary>
        [TestMethod]
        public void Parse_OptionXmlns_IsIgnored()
        {
            const string xml =
                """
                <Form Message="m">
                  <Field Name="c" Type="enum">
                    <Option Value="red" xmlns:x="http://example.com"/>
                  </Field>
                </Form>
                """;

            ElicitFormXml.Parse(xml).Fields[0].Options[0].Value.Should().Be("red");
        }

        /// <summary>
        /// Default on multi is rejected.
        /// </summary>
        [TestMethod]
        public void Parse_DefaultOnMulti_ThrowsInvalidOperationException()
        {
            const string xml =
                """
                <Form Message="m">
                  <Field Name="roles" Type="multi" Default="admin">
                    <Option Value="admin"/>
                  </Field>
                </Form>
                """;

            var act = () => ElicitFormXml.Parse(xml);
            act.Should().Throw<InvalidOperationException>().WithMessage("*Selected on Option*");
        }

        /// <summary>
        /// CDATA mixed content is rejected.
        /// </summary>
        [TestMethod]
        public void Parse_CDataMixedContent_ThrowsInvalidOperationException()
        {
            const string xml = """<Form Message="m"><![CDATA[hello]]><Field Name="a" Type="string"/></Form>""";
            var act = () => ElicitFormXml.Parse(xml);
            act.Should().Throw<InvalidOperationException>().WithMessage("*mixed content*");
        }

        /// <summary>
        /// Non-Form elements before the island are skipped.
        /// </summary>
        [TestMethod]
        public void Parse_LeadingNonFormElement_FindsForm()
        {
            const string xml =
                """
                <?xml version="1.0"?>
                <Wrapper>
                  <Nope/>
                  <Form Message="m"><Field Name="a" Type="string"/></Form>
                </Wrapper>
                """;

            ElicitFormXml.Parse(xml).Fields[0].Name.Should().Be("a");
        }

        /// <summary>
        /// Selected false is not selected.
        /// </summary>
        [TestMethod]
        [DataRow("false")]
        [DataRow("0")]
        public void Parse_SelectedFalse_IsNotSelected(string token)
        {
            var xml = $"""<Form Message="m"><Field Name="c" Type="enum"><Option Value="red" Selected="{token}"/></Field></Form>""";
            ElicitFormXml.Parse(xml).Fields[0].DefaultString.Should().BeNull();
        }

        /// <summary>
        /// Boolean Default of 1 is true.
        /// </summary>
        [TestMethod]
        public void Parse_BooleanDefaultOne_IsTrue()
        {
            const string xml = """<Form Message="m"><Field Name="on" Type="boolean" Default="1"/></Form>""";
            ElicitFormXml.Parse(xml).Fields[0].DefaultBool.Should().BeTrue();
        }

        /// <summary>
        /// An invalid integer Default fails first.
        /// </summary>
        [TestMethod]
        public void Parse_InvalidIntegerDefault_ThrowsInvalidOperationException()
        {
            const string xml = """<Form Message="m"><Field Name="age" Type="integer" Default="nope"/></Form>""";
            var act = () => ElicitFormXml.Parse(xml);
            act.Should().Throw<InvalidOperationException>().WithMessage("*not a number*");
        }

        /// <summary>
        /// NaN Min fails first.
        /// </summary>
        [TestMethod]
        public void Parse_NaNMin_ThrowsInvalidOperationException()
        {
            const string xml = """<Form Message="m"><Field Name="age" Type="integer" Min="NaN"/></Form>""";
            var act = () => ElicitFormXml.Parse(xml);
            act.Should().Throw<InvalidOperationException>().WithMessage("*not a number*");
        }

        /// <summary>
        /// Multi Max overflow fails first.
        /// </summary>
        [TestMethod]
        public void Parse_MultiMaxOutOfRange_ThrowsInvalidOperationException()
        {
            const string xml =
                """
                <Form Message="m">
                  <Field Name="roles" Type="multi" Max="1E+20">
                    <Option Value="admin"/>
                  </Field>
                </Form>
                """;

            var act = () => ElicitFormXml.Parse(xml);
            act.Should().Throw<InvalidOperationException>().WithMessage("*out of range*");
        }

        /// <summary>
        /// An empty Field element is missing Name.
        /// </summary>
        [TestMethod]
        public void Parse_EmptyField_ThrowsInvalidOperationException()
        {
            const string xml = """<Form Message="m"><Field/></Form>""";
            var act = () => ElicitFormXml.Parse(xml);
            act.Should().Throw<InvalidOperationException>().WithMessage("*Field Name is required*");
        }

        /// <summary>
        /// A processing instruction inside Option is an unexpected node.
        /// </summary>
        [TestMethod]
        public void Parse_OptionProcessingInstruction_ThrowsInvalidOperationException()
        {
            const string xml = """<Form Message="m"><Field Name="c" Type="enum"><Option Value="red"><?pi data?></Option></Field></Form>""";
            var act = () => ElicitFormXml.Parse(xml);
            act.Should().Throw<InvalidOperationException>().WithMessage("*unexpected node*");
        }

        /// <summary>
        /// Multi Min without Max records only MinItems.
        /// </summary>
        [TestMethod]
        public void Parse_MultiMinWithoutMax_SetsMinItemsOnly()
        {
            const string xml =
                """
                <Form Message="m">
                  <Field Name="roles" Type="multi" Min="1">
                    <Option Value="admin"/>
                  </Field>
                </Form>
                """;

            var field = ElicitFormXml.Parse(xml).Fields[0];
            field.MinItems.Should().Be(1);
            field.MaxItems.Should().BeNull();
        }

        /// <summary>
        /// MaxLength overflow fails first.
        /// </summary>
        [TestMethod]
        public void Parse_OverflowMaxLength_ThrowsInvalidOperationException()
        {
            const string xml = """<Form Message="m"><Field Name="a" Type="string" MaxLength="2147483648"/></Form>""";
            var act = () => ElicitFormXml.Parse(xml);
            act.Should().Throw<InvalidOperationException>().WithMessage("*not an integer*");
        }

        #endregion

    }

}
