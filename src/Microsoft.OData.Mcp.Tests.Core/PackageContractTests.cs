// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core
{

    /// <summary>
    /// Guards package versions required by the v3 specification.
    /// </summary>
    [TestClass]
    public class PackageContractTests
    {

        #region Public Methods

        /// <summary>
        /// Core must pin ModelContextProtocol 2.x, not a floating 0.*-* preview.
        /// </summary>
        [TestMethod]
        public void CoreCsproj_PinsModelContextProtocol2()
        {
            var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Microsoft.OData.Mcp.Core", "Microsoft.OData.Mcp.Core.csproj"));
            var xml = File.ReadAllText(path);

            xml.Should().NotContain("Version=\"0.*-*\"");
            xml.Should().Contain("ModelContextProtocol");
            xml.Should().MatchRegex(@"ModelContextProtocol""\s+Version=""2\.");
        }

        #endregion

    }

}
