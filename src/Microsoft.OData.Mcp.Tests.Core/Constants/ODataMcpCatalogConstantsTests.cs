// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using FluentAssertions;
using Microsoft.OData.Mcp.Core.Constants;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Constants
{

    /// <summary>
    /// Locks the catalog protocol identifiers.
    /// </summary>
    [TestClass]
    public class ODataMcpCatalogConstantsTests
    {

        #region Public Methods

        /// <summary>
        /// Generic tool names are the MCP wire names.
        /// </summary>
        [TestMethod]
        public void ToolNames_AreWireProtocol()
        {
            ODataMcpCatalogConstants.OdataCall.Should().Be("odata_call");
            ODataMcpCatalogConstants.OdataCreate.Should().Be("odata_create");
            ODataMcpCatalogConstants.OdataDelete.Should().Be("odata_delete");
            ODataMcpCatalogConstants.OdataDescribeModel.Should().Be("odata_describe_model");
            ODataMcpCatalogConstants.OdataDescribeType.Should().Be("odata_describe_type");
            ODataMcpCatalogConstants.OdataGet.Should().Be("odata_get");
            ODataMcpCatalogConstants.OdataListEntitySets.Should().Be("odata_list_entity_sets");
            ODataMcpCatalogConstants.OdataListOperations.Should().Be("odata_list_operations");
            ODataMcpCatalogConstants.OdataNavigate.Should().Be("odata_navigate");
            ODataMcpCatalogConstants.OdataQuery.Should().Be("odata_query");
            ODataMcpCatalogConstants.OdataUpdate.Should().Be("odata_update");
        }

        /// <summary>
        /// Type-card keys are the camelCase JSON properties the catalog emits.
        /// </summary>
        [TestMethod]
        public void CardKeys_AreCamelCaseJsonProperties()
        {
            ODataMcpCatalogConstants.Description.Should().Be("description");
            ODataMcpCatalogConstants.EntitySet.Should().Be("entitySet");
            ODataMcpCatalogConstants.EntityType.Should().Be("entityType");
            ODataMcpCatalogConstants.Keys.Should().Be("keys");
            ODataMcpCatalogConstants.LongDescription.Should().Be("longDescription");
            ODataMcpCatalogConstants.Navigations.Should().Be("navigations");
            ODataMcpCatalogConstants.Properties.Should().Be("properties");
            ODataMcpCatalogConstants.Type.Should().Be("type");
        }

        /// <summary>
        /// EDM primitive names match the OData specification.
        /// </summary>
        [TestMethod]
        public void EdmTypeNames_MatchODataSpecification()
        {
            ODataMcpCatalogConstants.EdmBinary.Should().Be("Edm.Binary");
            ODataMcpCatalogConstants.EdmBoolean.Should().Be("Edm.Boolean");
            ODataMcpCatalogConstants.EdmByte.Should().Be("Edm.Byte");
            ODataMcpCatalogConstants.EdmDecimal.Should().Be("Edm.Decimal");
            ODataMcpCatalogConstants.EdmDouble.Should().Be("Edm.Double");
            ODataMcpCatalogConstants.EdmInt16.Should().Be("Edm.Int16");
            ODataMcpCatalogConstants.EdmInt32.Should().Be("Edm.Int32");
            ODataMcpCatalogConstants.EdmInt64.Should().Be("Edm.Int64");
            ODataMcpCatalogConstants.EdmSByte.Should().Be("Edm.SByte");
            ODataMcpCatalogConstants.EdmSingle.Should().Be("Edm.Single");
            ODataMcpCatalogConstants.EdmStream.Should().Be("Edm.Stream");
        }

        #endregion

    }

}
