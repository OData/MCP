// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.IO;

namespace Microsoft.OData.Mcp.Tests.Shared.Entities
{

    /// <summary>
    /// Binary and stream document used to assert those properties stay out of MCP schemas.
    /// </summary>
    public sealed class Document
    {

        #region Properties

        /// <summary>
        /// Gets or sets the key.
        /// </summary>
        public int DocumentId { get; set; }

        /// <summary>
        /// Gets or sets an <c>Edm.Stream</c> property that must never appear in describe or named create.
        /// </summary>
        public Stream? File { get; set; }

        /// <summary>
        /// Gets or sets an <c>Edm.Binary</c> property that must never appear in describe or named create.
        /// </summary>
        public byte[] Photo { get; set; } = [];

        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        #endregion

    }

    /// <summary>
    /// Boolean-keyed entity used to lock <c>FormatKey</c> unquoted <c>true</c>/<c>false</c>.
    /// </summary>
    public sealed class Flag
    {

        #region Properties

        /// <summary>
        /// Gets or sets the boolean key.
        /// </summary>
        public bool FlagId { get; set; }

        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        #endregion

    }

    /// <summary>
    /// Composite-keyed line item matching the Northwind <c>Order_Details</c> shape.
    /// </summary>
    public sealed class OrderDetail
    {

        #region Properties

        /// <summary>
        /// Gets or sets the order key part.
        /// </summary>
        public int OrderID { get; set; }

        /// <summary>
        /// Gets or sets the product key part.
        /// </summary>
        public int ProductID { get; set; }

        /// <summary>
        /// Gets or sets the quantity.
        /// </summary>
        public int Quantity { get; set; }

        #endregion

    }

    /// <summary>
    /// Guid-keyed widget used to lock unquoted guid keys on the wire.
    /// </summary>
    public sealed class Widget
    {

        #region Fields

        /// <summary>
        /// Seeded widget key used by convention tests.
        /// </summary>
        public static readonly Guid SeedId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the guid key.
        /// </summary>
        public Guid WidgetId { get; set; }

        #endregion

    }

}
