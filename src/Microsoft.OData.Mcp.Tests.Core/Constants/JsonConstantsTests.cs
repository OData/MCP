// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using FluentAssertions;
using Microsoft.OData.Mcp.Core.Constants;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Constants
{

    /// <summary>
    /// Coverage for reusable JSON serializer options.
    /// </summary>
    [TestClass]
    public class JsonConstantsTests
    {

        #region Public Methods

        /// <summary>
        /// Static option instances are distinct and configured.
        /// </summary>
        [TestMethod]
        public void JsonConstants_Instances_AreConfigured()
        {
            JsonConstants.Default.WriteIndented.Should().BeFalse();
            JsonConstants.PrettyPrint.WriteIndented.Should().BeTrue();
            JsonConstants.ApiResponse.PropertyNamingPolicy.Should().NotBeNull();
            JsonConstants.CaseInsensitive.PropertyNameCaseInsensitive.Should().BeTrue();
            JsonConstants.Compact.WriteIndented.Should().BeFalse();
            JsonConstants.WithReferenceHandling.ReferenceHandler.Should().NotBeNull();
        }

        #endregion

    }

}
