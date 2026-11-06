// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.Protocol;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// Golden projection of a compact form onto SDK <see cref="ElicitRequestParams"/>.
    /// </summary>
    [TestClass]
    public class ElicitFormMapperTests
    {

        #region Public Methods

        /// <summary>
        /// A kitchen-sink form maps onto the same SDK schema kinds McpEssentials uses.
        /// </summary>
        [TestMethod]
        public void ToRequestParams_KitchenSink_MatchesSdkSchema()
        {
            var form = new EdmElicitForm
            {
                Message = "Fill this out",
                Fields =
                [
                    new EdmElicitFormField
                    {
                        Name = "firstName",
                        Type = EdmElicitFormFieldType.String,
                        Required = true,
                        Title = "Given name",
                        MinLength = 1,
                        DefaultString = "Ada"
                    },
                    new EdmElicitFormField
                    {
                        Name = "age",
                        Type = EdmElicitFormFieldType.Integer,
                        Minimum = 0,
                        Maximum = 130,
                        DefaultNumber = 42
                    },
                    new EdmElicitFormField
                    {
                        Name = "subscribe",
                        Type = EdmElicitFormFieldType.Boolean,
                        DefaultBool = true
                    },
                    new EdmElicitFormField
                    {
                        Name = "color",
                        Type = EdmElicitFormFieldType.Enum,
                        DefaultString = "red",
                        Options =
                        [
                            new EdmElicitFormOption { Value = "red" },
                            new EdmElicitFormOption { Value = "blue" }
                        ]
                    },
                    new EdmElicitFormField
                    {
                        Name = "roles",
                        Type = EdmElicitFormFieldType.Multi,
                        MinItems = 1,
                        DefaultStrings = ["user"],
                        Options =
                        [
                            new EdmElicitFormOption { Value = "admin", Title = "Administrator" },
                            new EdmElicitFormOption { Value = "user", Title = "User", Selected = true }
                        ]
                    }
                ]
            };

            var request = ElicitFormMapper.ToRequestParams(form);
            request.Mode.Should().Be("form");
            request.Message.Should().Be("Fill this out");
            request.RequestedSchema!.Required.Should().Equal("firstName");

            request.RequestedSchema.Properties["firstName"].Should().BeOfType<ElicitRequestParams.StringSchema>()
                .Which.Default.Should().Be("Ada");
            request.RequestedSchema.Properties["age"].Should().BeOfType<ElicitRequestParams.NumberSchema>()
                .Which.Type.Should().Be("integer");
            request.RequestedSchema.Properties["subscribe"].Should().BeOfType<ElicitRequestParams.BooleanSchema>()
                .Which.Default.Should().BeTrue();
            request.RequestedSchema.Properties["color"].Should().BeOfType<ElicitRequestParams.UntitledSingleSelectEnumSchema>()
                .Which.Default.Should().Be("red");
            request.RequestedSchema.Properties["roles"].Should().BeOfType<ElicitRequestParams.TitledMultiSelectEnumSchema>()
                .Which.MinItems.Should().Be(1);
        }

        /// <summary>
        /// An empty message is rejected at map time.
        /// </summary>
        [TestMethod]
        public void ToRequestParams_EmptyMessage_ThrowsInvalidOperationException()
        {
            var act = () => ElicitFormMapper.ToRequestParams(new EdmElicitForm());
            act.Should().Throw<InvalidOperationException>().WithMessage("*message cannot be empty*");
        }

        /// <summary>
        /// A null form is rejected.
        /// </summary>
        [TestMethod]
        public void ToRequestParams_Null_ThrowsArgumentNullException()
        {
            var act = () => ElicitFormMapper.ToRequestParams(null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("form");
        }

        /// <summary>
        /// Choice fields without options are rejected.
        /// </summary>
        [TestMethod]
        public void ToRequestParams_EnumWithoutOptions_ThrowsInvalidOperationException()
        {
            var form = new EdmElicitForm
            {
                Message = "m",
                Fields = [new EdmElicitFormField { Name = "c", Type = EdmElicitFormFieldType.Enum }]
            };

            var act = () => ElicitFormMapper.ToRequestParams(form);
            act.Should().Throw<InvalidOperationException>().WithMessage("*no options*");
        }

        /// <summary>
        /// Duplicate field names fail first.
        /// </summary>
        [TestMethod]
        public void ToRequestParams_DuplicateFieldName_ThrowsInvalidOperationException()
        {
            var form = new EdmElicitForm
            {
                Message = "m",
                Fields =
                [
                    new EdmElicitFormField { Name = "a", Type = EdmElicitFormFieldType.String },
                    new EdmElicitFormField { Name = "a", Type = EdmElicitFormFieldType.Integer }
                ]
            };

            var act = () => ElicitFormMapper.ToRequestParams(form);
            act.Should().Throw<InvalidOperationException>().WithMessage("*already has a field named 'a'*");
        }

        /// <summary>
        /// A blank field name fails first.
        /// </summary>
        [TestMethod]
        public void ToRequestParams_BlankFieldName_ThrowsInvalidOperationException()
        {
            var form = new EdmElicitForm
            {
                Message = "m",
                Fields = [new EdmElicitFormField { Name = "  ", Type = EdmElicitFormFieldType.String }]
            };

            var act = () => ElicitFormMapper.ToRequestParams(form);
            act.Should().Throw<InvalidOperationException>().WithMessage("*name cannot be empty*");
        }

        /// <summary>
        /// A null field fails first.
        /// </summary>
        [TestMethod]
        public void ToRequestParams_NullField_ThrowsArgumentNullException()
        {
            var form = new EdmElicitForm
            {
                Message = "m",
                Fields = [null!]
            };

            var act = () => ElicitFormMapper.ToRequestParams(form);
            act.Should().Throw<ArgumentNullException>();
        }

        /// <summary>
        /// An unknown field type fails first.
        /// </summary>
        [TestMethod]
        public void ToSchema_UnknownType_ThrowsInvalidOperationException()
        {
            var field = new EdmElicitFormField { Name = "x", Type = (EdmElicitFormFieldType)99 };
            var act = () => ElicitFormMapper.ToSchema(field);
            act.Should().Throw<InvalidOperationException>().WithMessage("*Unknown form field type*");
        }

        /// <summary>
        /// Multi-choice without options fails first.
        /// </summary>
        [TestMethod]
        public void ToRequestParams_MultiWithoutOptions_ThrowsInvalidOperationException()
        {
            var form = new EdmElicitForm
            {
                Message = "m",
                Fields = [new EdmElicitFormField { Name = "roles", Type = EdmElicitFormFieldType.Multi }]
            };

            var act = () => ElicitFormMapper.ToRequestParams(form);
            act.Should().Throw<InvalidOperationException>().WithMessage("*no options*");
        }

        /// <summary>
        /// Titled choice options project onto oneOf.
        /// </summary>
        [TestMethod]
        public void ToRequestParams_TitledChoice_UsesOneOf()
        {
            var form = new EdmElicitForm
            {
                Message = "m",
                Fields =
                [
                    new EdmElicitFormField
                    {
                        Name = "color",
                        Type = EdmElicitFormFieldType.Enum,
                        Options =
                        [
                            new EdmElicitFormOption { Value = "red", Title = "Red" },
                            new EdmElicitFormOption { Value = "blue" }
                        ]
                    }
                ]
            };

            var request = ElicitFormMapper.ToRequestParams(form);
            request.RequestedSchema!.Properties["color"].Should().BeOfType<ElicitRequestParams.TitledSingleSelectEnumSchema>()
                .Which.OneOf.Should().HaveCount(2);
        }

        /// <summary>
        /// Untitled multi-choice projects onto items.enum.
        /// </summary>
        [TestMethod]
        public void ToRequestParams_UntitledMulti_UsesItemsEnum()
        {
            var form = new EdmElicitForm
            {
                Message = "m",
                Fields =
                [
                    new EdmElicitFormField
                    {
                        Name = "roles",
                        Type = EdmElicitFormFieldType.Multi,
                        Options =
                        [
                            new EdmElicitFormOption { Value = "admin" },
                            new EdmElicitFormOption { Value = "user" }
                        ]
                    }
                ]
            };

            var request = ElicitFormMapper.ToRequestParams(form);
            request.RequestedSchema!.Properties["roles"].Should().BeOfType<ElicitRequestParams.UntitledMultiSelectEnumSchema>();
        }

        /// <summary>
        /// Email format projects onto a string schema.
        /// </summary>
        [TestMethod]
        public void ToRequestParams_EmailFormat_SetsFormat()
        {
            var form = new EdmElicitForm
            {
                Message = "m",
                Fields =
                [
                    new EdmElicitFormField { Name = "email", Type = EdmElicitFormFieldType.String, Format = "email" }
                ]
            };

            ElicitFormMapper.ToRequestParams(form).RequestedSchema!.Properties["email"]
                .Should().BeOfType<ElicitRequestParams.StringSchema>()
                .Which.Format.Should().Be("email");
        }

        /// <summary>
        /// Number fields use type number.
        /// </summary>
        [TestMethod]
        public void ToRequestParams_Number_UsesNumberType()
        {
            var form = new EdmElicitForm
            {
                Message = "m",
                Fields =
                [
                    new EdmElicitFormField { Name = "price", Type = EdmElicitFormFieldType.Number, Minimum = 1, Maximum = 2 }
                ]
            };

            var schema = ElicitFormMapper.ToRequestParams(form).RequestedSchema!.Properties["price"]
                .Should().BeOfType<ElicitRequestParams.NumberSchema>().Subject;
            schema.Type.Should().Be("number");
            schema.Minimum.Should().Be(1);
            schema.Maximum.Should().Be(2);
        }

        /// <summary>
        /// Untitled multi-choice copies DefaultStrings onto the schema.
        /// </summary>
        [TestMethod]
        public void ToRequestParams_UntitledMulti_CopiesDefaultStrings()
        {
            var form = new EdmElicitForm
            {
                Message = "m",
                Fields =
                [
                    new EdmElicitFormField
                    {
                        Name = "roles",
                        Type = EdmElicitFormFieldType.Multi,
                        DefaultStrings = ["user"],
                        Options =
                        [
                            new EdmElicitFormOption { Value = "admin" },
                            new EdmElicitFormOption { Value = "user" }
                        ]
                    }
                ]
            };

            var schema = ElicitFormMapper.ToRequestParams(form).RequestedSchema!.Properties["roles"]
                .Should().BeOfType<ElicitRequestParams.UntitledMultiSelectEnumSchema>().Subject;
            schema.Default.Should().Equal("user");
        }

        /// <summary>
        /// Titled multi-choice without defaults leaves Default null.
        /// </summary>
        [TestMethod]
        public void ToRequestParams_TitledMulti_WithoutDefaults_HasNullDefault()
        {
            var form = new EdmElicitForm
            {
                Message = "m",
                Fields =
                [
                    new EdmElicitFormField
                    {
                        Name = "roles",
                        Type = EdmElicitFormFieldType.Multi,
                        Options =
                        [
                            new EdmElicitFormOption { Value = "admin", Title = "Administrator" }
                        ]
                    }
                ]
            };

            var schema = ElicitFormMapper.ToRequestParams(form).RequestedSchema!.Properties["roles"]
                .Should().BeOfType<ElicitRequestParams.TitledMultiSelectEnumSchema>().Subject;
            schema.Default.Should().BeNull();
        }

        /// <summary>
        /// A null field at ToSchema fails first.
        /// </summary>
        [TestMethod]
        public void ToSchema_Null_ThrowsArgumentNullException()
        {
            var act = () => ElicitFormMapper.ToSchema(null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("field");
        }

        #endregion

    }

}
