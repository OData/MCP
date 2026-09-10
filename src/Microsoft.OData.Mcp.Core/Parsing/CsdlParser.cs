// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Mcp.Core.Models;

namespace Microsoft.OData.Mcp.Core.Parsing
{

    /// <summary>
    /// Parses OData CSDL (Conceptual Schema Definition Language) XML documents into EDM models.
    /// </summary>
    /// <remarks>
    /// This parser handles CSDL XML documents that describe the structure of OData services,
    /// including entity types, complex types, entity containers, and their relationships.
    /// It supports OData specification versions 4.0 and later.
    /// </remarks>
    public sealed class CsdlParser : ICsdlMetadataParser
    {

        #region Fields

        internal readonly ILogger<CsdlParser>? _logger;

        internal static readonly XNamespace EdmNamespace = "http://docs.oasis-open.org/odata/ns/edm";
        internal static readonly XNamespace EdmxNamespace = "http://docs.oasis-open.org/odata/ns/edmx";

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="CsdlParser"/> class.
        /// </summary>
        public CsdlParser()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CsdlParser"/> class with the specified logger.
        /// </summary>
        /// <param name="logger">The logger to use for diagnostic messages.</param>
        public CsdlParser(ILogger<CsdlParser> logger)
        {
            _logger = logger;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Parses a CSDL XML document from a string.
        /// </summary>
        /// <param name="csdlXml">The CSDL XML content as a string.</param>
        /// <returns>The parsed EDM model.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="csdlXml"/> is null or whitespace.</exception>
        /// <exception cref="XmlException">Thrown when the XML is malformed.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the CSDL structure is invalid.</exception>
        public EdmModel ParseFromString(string csdlXml)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(csdlXml);

            _logger?.LogDebug("Parsing CSDL XML from string");

            try
            {
                var document = XDocument.Parse(csdlXml);
                return ParseDocument(document);
            }
            catch (XmlException ex)
            {
                _logger?.LogError(ex, "Failed to parse CSDL XML: invalid XML format");
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to parse CSDL XML");
                throw new InvalidOperationException("Failed to parse CSDL XML document", ex);
            }
        }

        /// <summary>
        /// Parses a CSDL XML document from a stream.
        /// </summary>
        /// <param name="stream">The stream containing the CSDL XML content.</param>
        /// <returns>The parsed EDM model.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
        /// <exception cref="XmlException">Thrown when the XML is malformed.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the CSDL structure is invalid.</exception>
        public EdmModel ParseFromStream(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            _logger?.LogDebug("Parsing CSDL XML from stream");

            try
            {
                var document = XDocument.Load(stream);
                return ParseDocument(document);
            }
            catch (XmlException ex)
            {
                _logger?.LogError(ex, "Failed to parse CSDL XML: invalid XML format");
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to parse CSDL XML");
                throw new InvalidOperationException("Failed to parse CSDL XML document", ex);
            }
        }

        /// <summary>
        /// Parses a CSDL XML document from a file.
        /// </summary>
        /// <param name="filePath">The path to the file containing the CSDL XML content.</param>
        /// <returns>The parsed EDM model.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> is null or whitespace.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
        /// <exception cref="XmlException">Thrown when the XML is malformed.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the CSDL structure is invalid.</exception>
        public EdmModel ParseFromFile(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"CSDL file not found: {filePath}");
            }

            _logger?.LogDebug("Parsing CSDL XML from file: {FilePath}", filePath);

            try
            {
                var document = XDocument.Load(filePath);
                return ParseDocument(document);
            }
            catch (XmlException ex)
            {
                _logger?.LogError(ex, "Failed to parse CSDL XML file {FilePath}: invalid XML format", filePath);
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to parse CSDL XML file {FilePath}", filePath);
                throw new InvalidOperationException($"Failed to parse CSDL XML file: {filePath}", ex);
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Parses the root EDMX document.
        /// </summary>
        /// <param name="document">The XML document to parse.</param>
        /// <returns>The parsed EDM model.</returns>
        internal EdmModel ParseDocument(XDocument document)
        {
            var root = document.Root ??
                throw new InvalidOperationException("CSDL document has no root element");

            if (root.Name != EdmxNamespace + "Edmx")
            {
                throw new InvalidOperationException($"Expected root element 'Edmx', found '{root.Name}'");
            }

            var versionAttr = root.Attribute("Version");
            var version = versionAttr?.Value ?? "4.0";

            _logger?.LogDebug("Parsing EDMX document version {Version}", version);

            var model = new EdmModel(version);

            var dataServices = root.Element(EdmxNamespace + "DataServices") ??
                throw new InvalidOperationException("EDMX document missing DataServices element");

            var schemas = dataServices.Elements(EdmNamespace + "Schema");
            foreach (var schema in schemas)
            {
                ParseSchema(schema, model);
            }

            _logger?.LogDebug("Parsed CSDL model with {EntityTypeCount} entity types, {ComplexTypeCount} complex types, {ContainerCount} containers",
                model.EntityTypes.Count, model.ComplexTypes.Count, model.EntityContainers.Count);

            return model;
        }

        /// <summary>
        /// Parses a schema element.
        /// </summary>
        /// <param name="schemaElement">The schema XML element.</param>
        /// <param name="model">The model to populate.</param>
        internal void ParseSchema(XElement schemaElement, EdmModel model)
        {
            var namespaceAttr = schemaElement.Attribute("Namespace") ??
                throw new InvalidOperationException("Schema element missing Namespace attribute");

            var schemaNamespace = namespaceAttr.Value;
            _logger?.LogDebug("Parsing schema namespace: {Namespace}", schemaNamespace);

            if (!model.Namespaces.Contains(schemaNamespace))
            {
                model.Namespaces.Add(schemaNamespace);
            }

            // Parse enum types first so property types can be resolved against them later.
            var enumTypes = schemaElement.Elements(EdmNamespace + "EnumType");
            foreach (var enumType in enumTypes)
            {
                model.AddEnumType(ParseEnumType(enumType, schemaNamespace));
            }

            // Parse entity types
            var entityTypes = schemaElement.Elements(EdmNamespace + "EntityType");
            foreach (var entityType in entityTypes)
            {
                var parsedEntityType = ParseEntityType(entityType, schemaNamespace);
                model.AddEntityType(parsedEntityType);
            }

            // Parse complex types
            var complexTypes = schemaElement.Elements(EdmNamespace + "ComplexType");
            foreach (var complexType in complexTypes)
            {
                var parsedComplexType = ParseComplexType(complexType, schemaNamespace);
                model.AddComplexType(parsedComplexType);
            }

            var functions = schemaElement.Elements(EdmNamespace + "Function");
            foreach (var function in functions)
            {
                model.Functions.Add(ParseFunction(function, schemaNamespace));
            }

            var actions = schemaElement.Elements(EdmNamespace + "Action");
            foreach (var action in actions)
            {
                model.Actions.Add(ParseAction(action, schemaNamespace));
            }

            // Parse entity containers
            var entityContainers = schemaElement.Elements(EdmNamespace + "EntityContainer");
            foreach (var entityContainer in entityContainers)
            {
                var parsedContainer = ParseEntityContainer(entityContainer, schemaNamespace);
                model.AddEntityContainer(parsedContainer);
            }

            foreach (var annotations in schemaElement.Elements(EdmNamespace + "Annotations"))
            {
                ApplyTargetedAnnotations(annotations, model, schemaNamespace);
            }
        }

        /// <summary>
        /// Parses an entity type element.
        /// </summary>
        /// <param name="entityTypeElement">The entity type XML element.</param>
        /// <param name="schemaNamespace">The namespace of the schema.</param>
        /// <returns>The parsed entity type.</returns>
        internal EdmEntityType ParseEntityType(XElement entityTypeElement, string schemaNamespace)
        {
            var nameAttr = entityTypeElement.Attribute("Name") ??
                throw new InvalidOperationException("EntityType element missing Name attribute");

            var entityType = new EdmEntityType(nameAttr.Value, schemaNamespace)
            {
                Name = nameAttr.Value,
                Namespace = schemaNamespace,
                BaseType = entityTypeElement.Attribute("BaseType")?.Value,
                Abstract = bool.Parse(entityTypeElement.Attribute("Abstract")?.Value ?? "false"),
                OpenType = bool.Parse(entityTypeElement.Attribute("OpenType")?.Value ?? "false"),
                HasStream = bool.Parse(entityTypeElement.Attribute("HasStream")?.Value ?? "false")
            };

            ApplyDocumentation(entityTypeElement, description => entityType.Description = description, longDescription => entityType.LongDescription = longDescription);

            _logger?.LogDebug("Parsing entity type: {FullName}", entityType.FullName);

            // Parse key
            var keyElement = entityTypeElement.Element(EdmNamespace + "Key");
            if (keyElement is not null)
            {
                var propertyRefs = keyElement.Elements(EdmNamespace + "PropertyRef");
                foreach (var propertyRef in propertyRefs)
                {
                    var keyPropertyName = propertyRef.Attribute("Name")?.Value;
                    if (!string.IsNullOrWhiteSpace(keyPropertyName))
                    {
                        entityType.Key.Add(keyPropertyName);
                    }
                }
            }

            // Parse properties
            var properties = entityTypeElement.Elements(EdmNamespace + "Property");
            foreach (var property in properties)
            {
                var parsedProperty = ParseProperty(property);
                parsedProperty.IsKey = entityType.Key.Contains(parsedProperty.Name);
                entityType.Properties.Add(parsedProperty);
            }

            // Parse navigation properties
            var navigationProperties = entityTypeElement.Elements(EdmNamespace + "NavigationProperty");
            foreach (var navigationProperty in navigationProperties)
            {
                var parsedNavProperty = ParseNavigationProperty(navigationProperty);
                entityType.NavigationProperties.Add(parsedNavProperty);
            }

            return entityType;
        }

        /// <summary>
        /// Parses a complex type element.
        /// </summary>
        /// <param name="complexTypeElement">The complex type XML element.</param>
        /// <param name="schemaNamespace">The namespace of the schema.</param>
        /// <returns>The parsed complex type.</returns>
        internal EdmComplexType ParseComplexType(XElement complexTypeElement, string schemaNamespace)
        {
            var nameAttr = complexTypeElement.Attribute("Name") ??
                throw new InvalidOperationException("ComplexType element missing Name attribute");

            var complexType = new EdmComplexType(nameAttr.Value, schemaNamespace)
            {
                Name = nameAttr.Value,
                Namespace = schemaNamespace,
                BaseType = complexTypeElement.Attribute("BaseType")?.Value,
                Abstract = bool.Parse(complexTypeElement.Attribute("Abstract")?.Value ?? "false"),
                OpenType = bool.Parse(complexTypeElement.Attribute("OpenType")?.Value ?? "false")
            };

            ApplyDocumentation(complexTypeElement, description => complexType.Description = description, longDescription => complexType.LongDescription = longDescription);

            _logger?.LogDebug("Parsing complex type: {FullName}", complexType.FullName);

            // Parse properties
            var properties = complexTypeElement.Elements(EdmNamespace + "Property");
            foreach (var property in properties)
            {
                var parsedProperty = ParseProperty(property);
                complexType.Properties.Add(parsedProperty);
            }

            // Parse navigation properties
            var navigationProperties = complexTypeElement.Elements(EdmNamespace + "NavigationProperty");
            foreach (var navigationProperty in navigationProperties)
            {
                var parsedNavProperty = ParseNavigationProperty(navigationProperty);
                complexType.NavigationProperties.Add(parsedNavProperty);
            }

            return complexType;
        }

        /// <summary>
        /// Parses an enumeration type element.
        /// </summary>
        /// <param name="enumTypeElement">The EnumType XML element.</param>
        /// <param name="schemaNamespace">The namespace of the schema.</param>
        /// <returns>The parsed enumeration type.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the element has no Name or declares no members.</exception>
        /// <remarks>
        /// Members without an explicit <c>Value</c> are numbered from zero in declaration order, per CSDL.
        /// </remarks>
        internal EdmEnumType ParseEnumType(XElement enumTypeElement, string schemaNamespace)
        {
            var nameAttr = enumTypeElement.Attribute("Name") ??
                throw new InvalidOperationException("EnumType element missing Name attribute");

            var enumType = new EdmEnumType(nameAttr.Value, schemaNamespace)
            {
                IsFlags = bool.Parse(enumTypeElement.Attribute("IsFlags")?.Value ?? "false")
            };

            var underlyingType = enumTypeElement.Attribute("UnderlyingType")?.Value;
            if (!string.IsNullOrWhiteSpace(underlyingType))
            {
                enumType.UnderlyingType = underlyingType;
            }

            ApplyDocumentation(enumTypeElement, description => enumType.Description = description, longDescription => enumType.LongDescription = longDescription);

            var index = 0;
            foreach (var memberElement in enumTypeElement.Elements(EdmNamespace + "Member"))
            {
                enumType.Members.Add(ParseEnumMember(memberElement, index));
                index++;
            }

            if (enumType.Members.Count == 0)
            {
                throw new InvalidOperationException($"EnumType '{enumType.FullName}' declares no members.");
            }

            return enumType;
        }

        /// <summary>
        /// Parses an enumeration member element.
        /// </summary>
        /// <param name="memberElement">The Member XML element.</param>
        /// <param name="index">The zero-based declaration index, used when <c>Value</c> is omitted.</param>
        /// <returns>The parsed member.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the element has no Name or a non-integral Value.</exception>
        internal static EdmEnumMember ParseEnumMember(XElement memberElement, int index)
        {
            var nameAttr = memberElement.Attribute("Name") ??
                throw new InvalidOperationException("EnumType Member element missing Name attribute");

            var valueText = memberElement.Attribute("Value")?.Value;
            long value = index;
            if (!string.IsNullOrWhiteSpace(valueText) && !long.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                throw new InvalidOperationException($"EnumType Member '{nameAttr.Value}' has a non-integer Value '{valueText}'.");
            }

            return new EdmEnumMember(nameAttr.Value, value);
        }

        /// <summary>
        /// Parses a property element.
        /// </summary>
        /// <param name="propertyElement">The property XML element.</param>
        /// <returns>The parsed property.</returns>
        internal EdmProperty ParseProperty(XElement propertyElement)
        {
            var nameAttr = propertyElement.Attribute("Name");
            var typeAttr = propertyElement.Attribute("Type");

            if (nameAttr is null)
            {
                throw new InvalidOperationException("Property element missing Name attribute");
            }
            if (typeAttr is null)
            {
                throw new InvalidOperationException($"Property '{nameAttr.Value}' missing Type attribute");
            }

            var property = new EdmProperty(nameAttr.Value, typeAttr.Value)
            {
                Computed = ReadComputed(propertyElement),
                Name = nameAttr.Value,
                Type = typeAttr.Value,
                Nullable = bool.Parse(propertyElement.Attribute("Nullable")?.Value ?? "true"),
                DefaultValue = propertyElement.Attribute("DefaultValue")?.Value,
                SRID = propertyElement.Attribute("SRID")?.Value
            };

            ApplyDocumentation(propertyElement, description => property.Description = description, longDescription => property.LongDescription = longDescription);

            // Parse MaxLength
            var maxLengthAttr = propertyElement.Attribute("MaxLength");
            if (maxLengthAttr is not null && maxLengthAttr.Value != "Max")
            {
                if (int.TryParse(maxLengthAttr.Value, out var maxLength))
                {
                    property.MaxLength = maxLength;
                }
            }

            // Parse Precision
            var precisionAttr = propertyElement.Attribute("Precision");
            if (precisionAttr is not null && int.TryParse(precisionAttr.Value, out var precision))
            {
                property.Precision = precision;
            }

            // Parse Scale
            var scaleAttr = propertyElement.Attribute("Scale");
            if (scaleAttr is not null && scaleAttr.Value != "Variable")
            {
                if (int.TryParse(scaleAttr.Value, out var scale))
                {
                    property.Scale = scale;
                }
            }

            // Parse Unicode
            var unicodeAttr = propertyElement.Attribute("Unicode");
            if (unicodeAttr is not null && bool.TryParse(unicodeAttr.Value, out var unicode))
            {
                property.Unicode = unicode;
            }

            return property;
        }

        /// <summary>
        /// Parses a navigation property element.
        /// </summary>
        /// <param name="navigationPropertyElement">The navigation property XML element.</param>
        /// <returns>The parsed navigation property.</returns>
        internal EdmNavigationProperty ParseNavigationProperty(XElement navigationPropertyElement)
        {
            var nameAttr = navigationPropertyElement.Attribute("Name");
            var typeAttr = navigationPropertyElement.Attribute("Type");

            if (nameAttr is null)
            {
                throw new InvalidOperationException("NavigationProperty element missing Name attribute");
            }
            if (typeAttr is null)
            {
                throw new InvalidOperationException($"NavigationProperty '{nameAttr.Value}' missing Type attribute");
            }

            var navigationProperty = new EdmNavigationProperty(nameAttr.Value, typeAttr.Value)
            {
                Name = nameAttr.Value,
                Type = typeAttr.Value,
                Nullable = bool.Parse(navigationPropertyElement.Attribute("Nullable")?.Value ?? "true"),
                Partner = navigationPropertyElement.Attribute("Partner")?.Value,
                ContainsTarget = bool.Parse(navigationPropertyElement.Attribute("ContainsTarget")?.Value ?? "false")
            };

            ApplyDocumentation(navigationPropertyElement, description => navigationProperty.Description = description, longDescription => navigationProperty.LongDescription = longDescription);

            // Parse OnDelete
            var onDeleteElement = navigationPropertyElement.Element(EdmNamespace + "OnDelete");
            if (onDeleteElement is not null)
            {
                navigationProperty.OnDelete = onDeleteElement.Attribute("Action")?.Value;
            }

            // Parse ReferentialConstraints
            var referentialConstraints = navigationPropertyElement.Elements(EdmNamespace + "ReferentialConstraint");
            foreach (var constraint in referentialConstraints)
            {
                var propertyAttr = constraint.Attribute("Property");
                var referencedPropertyAttr = constraint.Attribute("ReferencedProperty");

                if (propertyAttr is not null && referencedPropertyAttr is not null)
                {
                    var referentialConstraint = new EdmReferentialConstraint(
                        propertyAttr.Value,
                        referencedPropertyAttr.Value)
                    {
                        Property = propertyAttr.Value,
                        ReferencedProperty = referencedPropertyAttr.Value
                    };
                    navigationProperty.ReferentialConstraints.Add(referentialConstraint);
                }
            }

            return navigationProperty;
        }

        /// <summary>
        /// Parses an entity container element.
        /// </summary>
        /// <param name="containerElement">The entity container XML element.</param>
        /// <param name="schemaNamespace">The namespace of the schema.</param>
        /// <returns>The parsed entity container.</returns>
        internal EdmEntityContainer ParseEntityContainer(XElement containerElement, string schemaNamespace)
        {
            var nameAttr = containerElement.Attribute("Name") ?? throw new InvalidOperationException("EntityContainer element missing Name attribute");
            var container = new EdmEntityContainer(nameAttr.Value, schemaNamespace)
            {
                Name = nameAttr.Value,
                Namespace = schemaNamespace,
                Extends = containerElement.Attribute("Extends")?.Value
            };

            ApplyDocumentation(containerElement, description => container.Description = description, longDescription => container.LongDescription = longDescription);

            _logger?.LogDebug("Parsing entity container: {FullName}", container.FullName);

            // Parse entity sets
            var entitySets = containerElement.Elements(EdmNamespace + "EntitySet");
            foreach (var entitySet in entitySets)
            {
                var parsedEntitySet = ParseEntitySet(entitySet);
                container.AddEntitySet(parsedEntitySet);
            }

            // Parse singletons
            var singletons = containerElement.Elements(EdmNamespace + "Singleton");
            foreach (var singleton in singletons)
            {
                var parsedSingleton = ParseSingleton(singleton);
                container.AddSingleton(parsedSingleton);
            }

            // Parse function imports
            var functionImports = containerElement.Elements(EdmNamespace + "FunctionImport");
            foreach (var functionImport in functionImports)
            {
                var parsedFunctionImport = ParseFunctionImport(functionImport);
                container.FunctionImports.Add(parsedFunctionImport);
            }

            // Parse action imports
            var actionImports = containerElement.Elements(EdmNamespace + "ActionImport");
            foreach (var actionImport in actionImports)
            {
                var parsedActionImport = ParseActionImport(actionImport);
                container.ActionImports.Add(parsedActionImport);
            }

            return container;
        }

        /// <summary>
        /// Parses an entity set element.
        /// </summary>
        /// <param name="entitySetElement">The entity set XML element.</param>
        /// <returns>The parsed entity set.</returns>
        internal EdmEntitySet ParseEntitySet(XElement entitySetElement)
        {
            var nameAttr = entitySetElement.Attribute("Name");
            var entityTypeAttr = entitySetElement.Attribute("EntityType");

            if (nameAttr is null)
            {
                throw new InvalidOperationException("EntitySet element missing Name attribute");
            }
            if (entityTypeAttr is null)
            {
                throw new InvalidOperationException($"EntitySet '{nameAttr.Value}' missing EntityType attribute");
            }

            var entitySet = new EdmEntitySet(nameAttr.Value, entityTypeAttr.Value)
            {
                Name = nameAttr.Value,
                EntityType = entityTypeAttr.Value,
                IncludeInServiceDocument = bool.Parse(entitySetElement.Attribute("IncludeInServiceDocument")?.Value ?? "true")
            };

            ApplyDocumentation(entitySetElement, description => entitySet.Description = description, longDescription => entitySet.LongDescription = longDescription);

            // Parse navigation property bindings
            var navigationPropertyBindings = entitySetElement.Elements(EdmNamespace + "NavigationPropertyBinding");
            foreach (var binding in navigationPropertyBindings)
            {
                var pathAttr = binding.Attribute("Path");
                var targetAttr = binding.Attribute("Target");

                if (pathAttr is not null && targetAttr is not null)
                {
                    entitySet.AddNavigationPropertyBinding(pathAttr.Value, targetAttr.Value);
                }
            }

            return entitySet;
        }

        /// <summary>
        /// Parses a singleton element.
        /// </summary>
        /// <param name="singletonElement">The singleton XML element.</param>
        /// <returns>The parsed singleton.</returns>
        internal EdmSingleton ParseSingleton(XElement singletonElement)
        {
            var nameAttr = singletonElement.Attribute("Name");
            var typeAttr = singletonElement.Attribute("Type");

            if (nameAttr is null)
            {
                throw new InvalidOperationException("Singleton element missing Name attribute");
            }
            if (typeAttr is null)
            {
                throw new InvalidOperationException($"Singleton '{nameAttr.Value}' missing Type attribute");
            }

            var singleton = new EdmSingleton(nameAttr.Value, typeAttr.Value)
            {
                Name = nameAttr.Value,
                Type = typeAttr.Value
            };

            ApplyDocumentation(singletonElement, description => singleton.Description = description, longDescription => singleton.LongDescription = longDescription);

            // Parse navigation property bindings
            var navigationPropertyBindings = singletonElement.Elements(EdmNamespace + "NavigationPropertyBinding");
            foreach (var binding in navigationPropertyBindings)
            {
                var pathAttr = binding.Attribute("Path");
                var targetAttr = binding.Attribute("Target");

                if (pathAttr is not null && targetAttr is not null)
                {
                    singleton.AddNavigationPropertyBinding(pathAttr.Value, targetAttr.Value);
                }
            }

            return singleton;
        }

        /// <summary>
        /// Applies CSDL documentation and Core.Description / Core.LongDescription to a target.
        /// </summary>
        /// <param name="element">The CSDL element.</param>
        /// <param name="setDescription">Receives the summary or description when present.</param>
        /// <param name="setLongDescription">Receives the long description when present.</param>
        /// <remarks>
        /// Preference order is CSDL <c>Documentation/Summary</c> and <c>Documentation/LongDescription</c>,
        /// then <c>Org.OData.Core.V1.Description</c> and <c>Org.OData.Core.V1.LongDescription</c>.
        /// The description callback receives the summary, or the long description when no summary exists.
        /// </remarks>
        internal void ApplyDocumentation(XElement element, Action<string> setDescription, Action<string>? setLongDescription = null)
        {
            ArgumentNullException.ThrowIfNull(element);
            ArgumentNullException.ThrowIfNull(setDescription);

            var documentation = element.Element(EdmNamespace + "Documentation");
            var summary = documentation?.Element(EdmNamespace + "Summary")?.Value;
            var longDescription = documentation?.Element(EdmNamespace + "LongDescription")?.Value;

            if (string.IsNullOrWhiteSpace(summary) || string.IsNullOrWhiteSpace(longDescription))
            {
                ReadVocabularyDescriptions(element, out var vocabularyDescription, out var vocabularyLongDescription);

                if (string.IsNullOrWhiteSpace(summary))
                {
                    summary = vocabularyDescription;
                }

                if (string.IsNullOrWhiteSpace(longDescription))
                {
                    longDescription = vocabularyLongDescription;
                }
            }

            if (!string.IsNullOrWhiteSpace(summary))
            {
                setDescription(summary.Trim());
            }
            else if (!string.IsNullOrWhiteSpace(longDescription))
            {
                setDescription(longDescription.Trim());
            }

            if (setLongDescription is not null && !string.IsNullOrWhiteSpace(longDescription))
            {
                setLongDescription(longDescription.Trim());
            }
        }

        /// <summary>
        /// Applies schema-level <c>Annotations</c> whose <c>Target</c> addresses a model element.
        /// </summary>
        /// <param name="annotationsElement">The annotations element.</param>
        /// <param name="model">The model being built.</param>
        /// <param name="schemaNamespace">The schema namespace.</param>
        internal void ApplyTargetedAnnotations(XElement annotationsElement, EdmModel model, string schemaNamespace)
        {
            ArgumentNullException.ThrowIfNull(annotationsElement);
            ArgumentNullException.ThrowIfNull(model);
            ArgumentException.ThrowIfNullOrWhiteSpace(schemaNamespace);

            var target = annotationsElement.Attribute("Target")?.Value;
            if (string.IsNullOrWhiteSpace(target))
            {
                return;
            }

            ApplyDocumentation(annotationsElement, description => AssignDescription(model, target, description, isLong: false), longDescription => AssignDescription(model, target, longDescription, isLong: true));

            if (ReadComputed(annotationsElement))
            {
                AssignComputed(model, target);
            }
        }

        /// <summary>
        /// Marks the structural property identified by an annotation target as computed.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="target">The CSDL target path in <c>Namespace.Type/Property</c> form.</param>
        /// <remarks>
        /// Targets that do not resolve to a structural property on an entity or complex type are ignored.
        /// </remarks>
        internal static void AssignComputed(EdmModel model, string target)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentException.ThrowIfNullOrWhiteSpace(target);

            var slash = target.LastIndexOf('/');
            if (slash < 0)
            {
                return;
            }

            var owner = target[..slash];
            var member = target[(slash + 1)..];

            var entityType = model.GetEntityType(owner) ?? model.EntityTypes.FirstOrDefault(type => type.Name.Equals(owner, StringComparison.Ordinal));
            var property = entityType?.GetProperty(member);
            if (property is null)
            {
                var complexType = model.GetComplexType(owner) ?? model.ComplexTypes.FirstOrDefault(type => type.Name.Equals(owner, StringComparison.Ordinal));
                property = complexType?.GetProperty(member);
            }

            if (property is not null)
            {
                property.Computed = true;
            }
        }

        /// <summary>
        /// Assigns documentation onto the model element identified by an annotation target.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="target">The CSDL target path.</param>
        /// <param name="value">The documentation string.</param>
        /// <param name="isLong">Whether this is a long description.</param>
        internal static void AssignDescription(EdmModel model, string target, string value, bool isLong)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentException.ThrowIfNullOrWhiteSpace(target);
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            var slash = target.LastIndexOf('/');
            var owner = slash >= 0 ? target[..slash] : target;
            var member = slash >= 0 ? target[(slash + 1)..] : null;

            var entityType = model.GetEntityType(owner) ?? model.EntityTypes.FirstOrDefault(type => type.Name.Equals(owner, StringComparison.Ordinal));
            if (entityType is not null)
            {
                if (member is null)
                {
                    AssignIfEmpty(isLong ? () => entityType.LongDescription : () => entityType.Description, text =>
                    {
                        if (isLong)
                        {
                            entityType.LongDescription = text;
                        }
                        else
                        {
                            entityType.Description = text;
                        }
                    }, value);

                    return;
                }

                var property = entityType.GetProperty(member);
                if (property is not null)
                {
                    AssignIfEmpty(isLong ? () => property.LongDescription : () => property.Description, text =>
                    {
                        if (isLong)
                        {
                            property.LongDescription = text;
                        }
                        else
                        {
                            property.Description = text;
                        }
                    }, value);

                    return;
                }

                var navigation = entityType.GetNavigationProperty(member);
                if (navigation is not null)
                {
                    AssignIfEmpty(isLong ? () => navigation.LongDescription : () => navigation.Description, text =>
                    {
                        if (isLong)
                        {
                            navigation.LongDescription = text;
                        }
                        else
                        {
                            navigation.Description = text;
                        }
                    }, value);
                }

                return;
            }

            var complexType = model.ComplexTypes.FirstOrDefault(type => type.FullName.Equals(owner, StringComparison.Ordinal) || type.Name.Equals(owner, StringComparison.Ordinal));
            if (complexType is not null && member is null)
            {
                AssignIfEmpty(isLong ? () => complexType.LongDescription : () => complexType.Description, text =>
                {
                    if (isLong)
                    {
                        complexType.LongDescription = text;
                    }
                    else
                    {
                        complexType.Description = text;
                    }
                }, value);

                return;
            }

            var function = model.Functions.FirstOrDefault(item => item.FullName.Equals(owner, StringComparison.Ordinal) || item.Name.Equals(owner, StringComparison.Ordinal));
            if (function is not null && member is null)
            {
                AssignIfEmpty(isLong ? () => function.LongDescription : () => function.Description, text =>
                {
                    if (isLong)
                    {
                        function.LongDescription = text;
                    }
                    else
                    {
                        function.Description = text;
                    }
                }, value);

                return;
            }

            var action = model.Actions.FirstOrDefault(item => item.FullName.Equals(owner, StringComparison.Ordinal) || item.Name.Equals(owner, StringComparison.Ordinal));
            if (action is not null && member is null)
            {
                AssignIfEmpty(isLong ? () => action.LongDescription : () => action.Description, text =>
                {
                    if (isLong)
                    {
                        action.LongDescription = text;
                    }
                    else
                    {
                        action.Description = text;
                    }
                }, value);

                return;
            }

            foreach (var container in model.EntityContainers)
            {
                if (member is null && (container.FullName.Equals(owner, StringComparison.Ordinal) || container.Name.Equals(owner, StringComparison.Ordinal)))
                {
                    AssignIfEmpty(isLong ? () => container.LongDescription : () => container.Description, text =>
                    {
                        if (isLong)
                        {
                            container.LongDescription = text;
                        }
                        else
                        {
                            container.Description = text;
                        }
                    }, value);

                    return;
                }

                if (container.FullName.Equals(owner, StringComparison.Ordinal) || container.Name.Equals(owner, StringComparison.Ordinal) || $"{container.Namespace}.{container.Name}".Equals(owner, StringComparison.Ordinal))
                {
                    var set = container.EntitySets.FirstOrDefault(item => item.Name.Equals(member, StringComparison.Ordinal));
                    if (set is not null)
                    {
                        AssignIfEmpty(isLong ? () => set.LongDescription : () => set.Description, text =>
                        {
                            if (isLong)
                            {
                                set.LongDescription = text;
                            }
                            else
                            {
                                set.Description = text;
                            }
                        }, value);

                        return;
                    }

                    var singleton = container.Singletons.FirstOrDefault(item => item.Name.Equals(member, StringComparison.Ordinal));
                    if (singleton is not null)
                    {
                        AssignIfEmpty(isLong ? () => singleton.LongDescription : () => singleton.Description, text =>
                        {
                            if (isLong)
                            {
                                singleton.LongDescription = text;
                            }
                            else
                            {
                                singleton.Description = text;
                            }
                        }, value);
                    }
                }
            }
        }

        /// <summary>
        /// Writes documentation only when the target does not already have a value.
        /// </summary>
        /// <param name="current">Returns the current value.</param>
        /// <param name="assign">Assigns the new value.</param>
        /// <param name="value">The documentation string.</param>
        internal static void AssignIfEmpty(Func<string?> current, Action<string> assign, string value)
        {
            ArgumentNullException.ThrowIfNull(current);
            ArgumentNullException.ThrowIfNull(assign);
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            if (string.IsNullOrWhiteSpace(current()))
            {
                assign(value);
            }
        }

        /// <summary>
        /// Reads whether an element carries a true <c>Core.Computed</c> or <c>Core.ComputedDefaultValue</c> annotation.
        /// </summary>
        /// <param name="element">The element whose child <c>Annotation</c> elements are inspected.</param>
        /// <returns>
        /// <c>true</c> when either term is present with no value, <c>Bool="true"</c>, or a <c>&lt;Bool&gt;true&lt;/Bool&gt;</c> child; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// Both terms default to <c>true</c> in the Core vocabulary, so an annotation with no value is true.
        /// Term names are matched by their last segment so namespace aliases such as <c>Core.Computed</c> also match.
        /// </remarks>
        internal static bool ReadComputed(XElement element)
        {
            ArgumentNullException.ThrowIfNull(element);

            foreach (var annotation in element.Elements(EdmNamespace + "Annotation"))
            {
                var term = annotation.Attribute("Term")?.Value;
                if (string.IsNullOrWhiteSpace(term))
                {
                    continue;
                }

                var dot = term.LastIndexOf('.');
                var termName = dot >= 0 ? term[(dot + 1)..] : term;
                if (termName is not ("Computed" or "ComputedDefaultValue"))
                {
                    continue;
                }

                var value = annotation.Attribute("Bool")?.Value ?? annotation.Element(EdmNamespace + "Bool")?.Value;
                if (string.IsNullOrWhiteSpace(value) || (bool.TryParse(value.Trim(), out var parsed) && parsed))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Reads Core description annotations from an element in a single pass.
        /// </summary>
        /// <param name="element">The CSDL element.</param>
        /// <param name="description">The <c>Core.Description</c> value, if any.</param>
        /// <param name="longDescription">The <c>Core.LongDescription</c> value, if any.</param>
        /// <remarks>
        /// <c>LongDescription</c> is matched first so it is consumed before the shorter
        /// <c>Description</c> suffix. A remaining <c>EndsWith("Description")</c> hit is the
        /// real description. If more than one leftover hit remains, the term whose suffix is
        /// a token boundary (preceded by <c>.</c> or the start of the string) wins.
        /// </remarks>
        internal void ReadVocabularyDescriptions(XElement element, out string? description, out string? longDescription)
        {
            ArgumentNullException.ThrowIfNull(element);

            description = null;
            longDescription = null;
            string? descriptionExact = null;
            var descriptionHits = 0;

            foreach (var annotation in element.Elements(EdmNamespace + "Annotation"))
            {
                var term = annotation.Attribute("Term")?.Value;
                if (string.IsNullOrWhiteSpace(term))
                {
                    continue;
                }

                var value = annotation.Attribute("String")?.Value ?? annotation.Element(EdmNamespace + "String")?.Value;
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (term.EndsWith("LongDescription", StringComparison.Ordinal))
                {
                    longDescription ??= value;
                    continue;
                }

                if (!term.EndsWith("Description", StringComparison.Ordinal))
                {
                    continue;
                }

                descriptionHits++;
                description ??= value;

                var prefixLength = term.Length - "Description".Length;
                if (prefixLength == 0 || term[prefixLength - 1] == '.')
                {
                    descriptionExact = value;
                }
            }

            if (descriptionHits > 1)
            {
                description = descriptionExact;
            }
        }

        /// <summary>
        /// Parses a schema-level action.
        /// </summary>
        /// <param name="actionElement">The action XML element.</param>
        /// <param name="schemaNamespace">The schema namespace.</param>
        /// <returns>
        /// The parsed action.
        /// </returns>
        internal EdmAction ParseAction(XElement actionElement, string schemaNamespace)
        {
            ArgumentNullException.ThrowIfNull(actionElement);
            ArgumentException.ThrowIfNullOrWhiteSpace(schemaNamespace);

            var nameAttr = actionElement.Attribute("Name") ??
                throw new InvalidOperationException("Action element missing Name attribute");

            var action = new EdmAction(nameAttr.Value, schemaNamespace)
            {
                IsBound = bool.Parse(actionElement.Attribute("IsBound")?.Value ?? "false"),
                Name = nameAttr.Value,
                Namespace = schemaNamespace,
                ReturnType = actionElement.Element(EdmNamespace + "ReturnType")?.Attribute("Type")?.Value
            };

            ApplyDocumentation(actionElement, description => action.Description = description, longDescription => action.LongDescription = longDescription);

            foreach (var parameter in actionElement.Elements(EdmNamespace + "Parameter"))
            {
                action.Parameters.Add(ParseOperationParameter(parameter));
            }

            if (action.IsBound && action.Parameters.Count > 0)
            {
                action.BindingParameterType = action.Parameters[0].Type;
            }

            return action;
        }

        /// <summary>
        /// Parses a schema-level function.
        /// </summary>
        /// <param name="functionElement">The function XML element.</param>
        /// <param name="schemaNamespace">The schema namespace.</param>
        /// <returns>
        /// The parsed function.
        /// </returns>
        internal EdmFunction ParseFunction(XElement functionElement, string schemaNamespace)
        {
            ArgumentNullException.ThrowIfNull(functionElement);
            ArgumentException.ThrowIfNullOrWhiteSpace(schemaNamespace);

            var nameAttr = functionElement.Attribute("Name") ??
                throw new InvalidOperationException("Function element missing Name attribute");

            var function = new EdmFunction(nameAttr.Value, schemaNamespace)
            {
                IsBound = bool.Parse(functionElement.Attribute("IsBound")?.Value ?? "false"),
                IsComposable = bool.Parse(functionElement.Attribute("IsComposable")?.Value ?? "false"),
                Name = nameAttr.Value,
                Namespace = schemaNamespace,
                ReturnType = functionElement.Element(EdmNamespace + "ReturnType")?.Attribute("Type")?.Value
            };

            ApplyDocumentation(functionElement, description => function.Description = description, longDescription => function.LongDescription = longDescription);

            foreach (var parameter in functionElement.Elements(EdmNamespace + "Parameter"))
            {
                function.Parameters.Add(ParseOperationParameter(parameter));
            }

            if (function.IsBound && function.Parameters.Count > 0)
            {
                function.BindingParameterType = function.Parameters[0].Type;
            }

            return function;
        }

        /// <summary>
        /// Parses a function or action parameter.
        /// </summary>
        /// <param name="parameterElement">The parameter XML element.</param>
        /// <returns>
        /// The parsed parameter.
        /// </returns>
        internal EdmParameter ParseOperationParameter(XElement parameterElement)
        {
            ArgumentNullException.ThrowIfNull(parameterElement);

            var nameAttr = parameterElement.Attribute("Name") ??
                throw new InvalidOperationException("Parameter element missing Name attribute");
            var typeAttr = parameterElement.Attribute("Type") ??
                throw new InvalidOperationException($"Parameter '{nameAttr.Value}' missing Type attribute");

            var parameter = new EdmParameter(nameAttr.Value, typeAttr.Value)
            {
                Name = nameAttr.Value,
                Nullable = bool.Parse(parameterElement.Attribute("Nullable")?.Value ?? "true"),
                Type = typeAttr.Value
            };

            ApplyDocumentation(parameterElement, description => parameter.Description = description, longDescription => parameter.LongDescription = longDescription);

            return parameter;
        }

        /// <summary>
        /// Parses a function import element.
        /// </summary>
        /// <param name="functionImportElement">The function import XML element.</param>
        /// <returns>The parsed function import.</returns>
        internal EdmFunctionImport ParseFunctionImport(XElement functionImportElement)
        {
            var nameAttr = functionImportElement.Attribute("Name");
            var functionAttr = functionImportElement.Attribute("Function");

            if (nameAttr is null)
            {
                throw new InvalidOperationException("FunctionImport element missing Name attribute");
            }
            if (functionAttr is null)
            {
                throw new InvalidOperationException($"FunctionImport '{nameAttr.Value}' missing Function attribute");
            }

            var functionImport = new EdmFunctionImport(nameAttr.Value, functionAttr.Value)
            {
                Name = nameAttr.Value,
                Function = functionAttr.Value,
                EntitySet = functionImportElement.Attribute("EntitySet")?.Value,
                IncludeInServiceDocument = bool.Parse(functionImportElement.Attribute("IncludeInServiceDocument")?.Value ?? "true")
            };

            return functionImport;
        }

        /// <summary>
        /// Parses an action import element.
        /// </summary>
        /// <param name="actionImportElement">The action import XML element.</param>
        /// <returns>The parsed action import.</returns>
        internal EdmActionImport ParseActionImport(XElement actionImportElement)
        {
            var nameAttr = actionImportElement.Attribute("Name");
            var actionAttr = actionImportElement.Attribute("Action");

            if (nameAttr is null)
            {
                throw new InvalidOperationException("ActionImport element missing Name attribute");
            }
            if (actionAttr is null)
            {
                throw new InvalidOperationException($"ActionImport '{nameAttr.Value}' missing Action attribute");
            }

            var actionImport = new EdmActionImport(nameAttr.Value, actionAttr.Value)
            {
                Name = nameAttr.Value,
                Action = actionAttr.Value,
                EntitySet = actionImportElement.Attribute("EntitySet")?.Value
            };

            return actionImport;
        }

        #endregion

    }

}
