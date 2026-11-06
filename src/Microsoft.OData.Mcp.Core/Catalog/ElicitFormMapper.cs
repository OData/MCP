// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.OData.Mcp.Core.Models;
using ModelContextProtocol.Protocol;

namespace Microsoft.OData.Mcp.Core.Catalog
{

    /// <summary>
    /// Projects a Core <see cref="EdmElicitForm"/> onto SDK <see cref="ElicitRequestParams"/>.
    /// </summary>
    /// <remarks>
    /// Tools uses this mapper so remote <c>$metadata</c> can elicit without a McpEssentials
    /// package reference. Field mapping matches McpEssentials <c>ElicitationField.ToSchema</c>.
    /// </remarks>
    public static class ElicitFormMapper
    {

        #region Public Methods

        /// <summary>
        /// Builds an MCP form elicitation request from a catalog snapshot.
        /// </summary>
        /// <param name="form">The compact form from CSDL.</param>
        /// <returns>The SDK request params with <c>mode: form</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="form"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the form message is empty or a field is invalid.</exception>
        public static ElicitRequestParams ToRequestParams(EdmElicitForm form)
        {
            ArgumentNullException.ThrowIfNull(form);
            if (string.IsNullOrWhiteSpace(form.Message))
            {
                throw new InvalidOperationException("Form elicitation message cannot be empty.");
            }

            var properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>(StringComparer.Ordinal);
            List<string>? required = null;
            foreach (var field in form.Fields)
            {
                ArgumentNullException.ThrowIfNull(field);
                if (string.IsNullOrWhiteSpace(field.Name))
                {
                    throw new InvalidOperationException("Form field name cannot be empty.");
                }

                if (!properties.TryAdd(field.Name, ToSchema(field)))
                {
                    throw new InvalidOperationException($"Form already has a field named '{field.Name}'.");
                }

                if (field.Required)
                {
                    required ??= [];
                    required.Add(field.Name);
                }
            }

            return new ElicitRequestParams
            {
                Mode = "form",
                Message = form.Message,
                RequestedSchema = new ElicitRequestParams.RequestSchema
                {
                    Properties = properties,
                    Required = required
                }
            };
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Projects one catalog field onto an SDK primitive schema.
        /// </summary>
        /// <param name="field">The catalog field.</param>
        /// <returns>The SDK schema node.</returns>
        internal static ElicitRequestParams.PrimitiveSchemaDefinition ToSchema(EdmElicitFormField field)
        {
            ArgumentNullException.ThrowIfNull(field);

            return field.Type switch
            {
                EdmElicitFormFieldType.String => new ElicitRequestParams.StringSchema
                {
                    Title = field.Title,
                    Description = field.Description,
                    MinLength = field.MinLength,
                    MaxLength = field.MaxLength,
                    Format = field.Format,
                    Default = field.DefaultString
                },
                EdmElicitFormFieldType.Integer => ToNumberSchema(field, "integer"),
                EdmElicitFormFieldType.Number => ToNumberSchema(field, "number"),
                EdmElicitFormFieldType.Boolean => new ElicitRequestParams.BooleanSchema
                {
                    Title = field.Title,
                    Description = field.Description,
                    Default = field.DefaultBool
                },
                EdmElicitFormFieldType.Enum => ToChoiceSchema(field),
                EdmElicitFormFieldType.Multi => ToMultiChoiceSchema(field),
                _ => throw new InvalidOperationException($"Unknown form field type '{field.Type}'.")
            };
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Copies option wire values in insertion order.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns>The untitled enum list.</returns>
        private static List<string> CopyOptionValues(EdmElicitFormField field)
        {
            var values = new List<string>(field.Options.Count);
            for (var i = 0; i < field.Options.Count; i++)
            {
                values.Add(field.Options[i].Value);
            }

            return values;
        }

        /// <summary>
        /// Copies default multi-select values, or <see langword="null"/> when omitted.
        /// </summary>
        /// <param name="values">The catalog defaults.</param>
        /// <returns>A mutable copy, or <see langword="null"/>.</returns>
        private static List<string>? CopyStrings(IReadOnlyList<string>? values)
        {
            if (values is null)
            {
                return null;
            }

            var copy = new List<string>(values.Count);
            for (var i = 0; i < values.Count; i++)
            {
                copy.Add(values[i]);
            }

            return copy;
        }

        /// <summary>
        /// Returns whether any option carries a display title.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns><see langword="true"/> when a title is present.</returns>
        private static bool HasTitles(EdmElicitFormField field)
        {
            for (var i = 0; i < field.Options.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(field.Options[i].Title))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Builds a single-select enum schema, titled when any option has a title.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns>The SDK enum schema.</returns>
        private static ElicitRequestParams.PrimitiveSchemaDefinition ToChoiceSchema(EdmElicitFormField field)
        {
            if (field.Options.Count == 0)
            {
                throw new InvalidOperationException($"Field '{field.Name}' has no options.");
            }

            if (HasTitles(field))
            {
                return new ElicitRequestParams.TitledSingleSelectEnumSchema
                {
                    Title = field.Title,
                    Description = field.Description,
                    Default = field.DefaultString,
                    OneOf = ToEnumOptions(field)
                };
            }

            return new ElicitRequestParams.UntitledSingleSelectEnumSchema
            {
                Title = field.Title,
                Description = field.Description,
                Default = field.DefaultString,
                Enum = CopyOptionValues(field)
            };
        }

        /// <summary>
        /// Builds titled enum option schemas.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns>The option list.</returns>
        private static List<ElicitRequestParams.EnumSchemaOption> ToEnumOptions(EdmElicitFormField field)
        {
            var options = new List<ElicitRequestParams.EnumSchemaOption>(field.Options.Count);
            for (var i = 0; i < field.Options.Count; i++)
            {
                var option = field.Options[i];
                options.Add(new ElicitRequestParams.EnumSchemaOption
                {
                    Const = option.Value,
                    Title = option.Title ?? option.Value
                });
            }

            return options;
        }

        /// <summary>
        /// Builds a multi-select enum schema, titled when any option has a title.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns>The SDK multi-select schema.</returns>
        private static ElicitRequestParams.PrimitiveSchemaDefinition ToMultiChoiceSchema(EdmElicitFormField field)
        {
            if (field.Options.Count == 0)
            {
                throw new InvalidOperationException($"Field '{field.Name}' has no options.");
            }

            if (HasTitles(field))
            {
                return new ElicitRequestParams.TitledMultiSelectEnumSchema
                {
                    Title = field.Title,
                    Description = field.Description,
                    MinItems = field.MinItems,
                    MaxItems = field.MaxItems,
                    Default = CopyStrings(field.DefaultStrings),
                    Items = new ElicitRequestParams.TitledEnumItemsSchema
                    {
                        AnyOf = ToEnumOptions(field)
                    }
                };
            }

            return new ElicitRequestParams.UntitledMultiSelectEnumSchema
            {
                Title = field.Title,
                Description = field.Description,
                MinItems = field.MinItems,
                MaxItems = field.MaxItems,
                Default = CopyStrings(field.DefaultStrings),
                Items = new ElicitRequestParams.UntitledEnumItemsSchema
                {
                    Enum = CopyOptionValues(field)
                }
            };
        }

        /// <summary>
        /// Builds a number or integer schema.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="type"><c>number</c> or <c>integer</c>.</param>
        /// <returns>The SDK number schema.</returns>
        private static ElicitRequestParams.NumberSchema ToNumberSchema(EdmElicitFormField field, string type)
        {
            return new ElicitRequestParams.NumberSchema
            {
                Type = type,
                Title = field.Title,
                Description = field.Description,
                Minimum = field.Minimum,
                Maximum = field.Maximum,
                Default = field.DefaultNumber
            };
        }

        #endregion

    }

}
