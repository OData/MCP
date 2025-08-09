using System;
using System.IO;
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

            // Parse entity containers
            var entityContainers = schemaElement.Elements(EdmNamespace + "EntityContainer");
            foreach (var entityContainer in entityContainers)
            {
                var parsedContainer = ParseEntityContainer(entityContainer, schemaNamespace);
                model.AddEntityContainer(parsedContainer);
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
                Name = nameAttr.Value,
                Type = typeAttr.Value,
                Nullable = bool.Parse(propertyElement.Attribute("Nullable")?.Value ?? "true"),
                DefaultValue = propertyElement.Attribute("DefaultValue")?.Value,
                SRID = propertyElement.Attribute("SRID")?.Value
            };

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
            var nameAttr = containerElement.Attribute("Name");
            if (nameAttr is null)
            {
                throw new InvalidOperationException("EntityContainer element missing Name attribute");
            }

            var container = new EdmEntityContainer(nameAttr.Value, schemaNamespace)
            {
                Name = nameAttr.Value,
                Namespace = schemaNamespace,
                Extends = containerElement.Attribute("Extends")?.Value
            };

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
