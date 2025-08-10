// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.ComponentModel;

namespace Microsoft.OData.Mcp.Core.Models
{

    /// <summary>
    /// Represents the EDM primitive types as defined in the OData specification.
    /// </summary>
    /// <remarks>
    /// These types correspond to the primitive types defined in the Entity Data Model (EDM)
    /// and are used to represent the basic data types in OData services.
    /// </remarks>
    public enum EdmPrimitiveType
    {

        /// <summary>
        /// Represents binary data.
        /// </summary>
        [Description("Edm.Binary")]
        Binary,

        /// <summary>
        /// Represents a boolean value (true or false).
        /// </summary>
        [Description("Edm.Boolean")]
        Boolean,

        /// <summary>
        /// Represents a single byte unsigned integer.
        /// </summary>
        [Description("Edm.Byte")]
        Byte,

        /// <summary>
        /// Represents a date value without time information.
        /// </summary>
        [Description("Edm.Date")]
        Date,

        /// <summary>
        /// Represents a date and time value.
        /// </summary>
        [Description("Edm.DateTimeOffset")]
        DateTimeOffset,

        /// <summary>
        /// Represents a numeric value with fixed precision and scale.
        /// </summary>
        [Description("Edm.Decimal")]
        Decimal,

        /// <summary>
        /// Represents a 64-bit floating point value.
        /// </summary>
        [Description("Edm.Double")]
        Double,

        /// <summary>
        /// Represents a duration value.
        /// </summary>
        [Description("Edm.Duration")]
        Duration,

        /// <summary>
        /// Represents a 128-bit globally unique identifier.
        /// </summary>
        [Description("Edm.Guid")]
        Guid,

        /// <summary>
        /// Represents a 16-bit signed integer.
        /// </summary>
        [Description("Edm.Int16")]
        Int16,

        /// <summary>
        /// Represents a 32-bit signed integer.
        /// </summary>
        [Description("Edm.Int32")]
        Int32,

        /// <summary>
        /// Represents a 64-bit signed integer.
        /// </summary>
        [Description("Edm.Int64")]
        Int64,

        /// <summary>
        /// Represents a signed byte.
        /// </summary>
        [Description("Edm.SByte")]
        SByte,

        /// <summary>
        /// Represents a 32-bit floating point value.
        /// </summary>
        [Description("Edm.Single")]
        Single,

        /// <summary>
        /// Represents a stream value.
        /// </summary>
        [Description("Edm.Stream")]
        Stream,

        /// <summary>
        /// Represents a string value.
        /// </summary>
        [Description("Edm.String")]
        String,

        /// <summary>
        /// Represents a time of day value.
        /// </summary>
        [Description("Edm.TimeOfDay")]
        TimeOfDay,

        /// <summary>
        /// Represents geography data.
        /// </summary>
        [Description("Edm.Geography")]
        Geography,

        /// <summary>
        /// Represents geography point data.
        /// </summary>
        [Description("Edm.GeographyPoint")]
        GeographyPoint,

        /// <summary>
        /// Represents geography line string data.
        /// </summary>
        [Description("Edm.GeographyLineString")]
        GeographyLineString,

        /// <summary>
        /// Represents geography polygon data.
        /// </summary>
        [Description("Edm.GeographyPolygon")]
        GeographyPolygon,

        /// <summary>
        /// Represents geography multi-point data.
        /// </summary>
        [Description("Edm.GeographyMultiPoint")]
        GeographyMultiPoint,

        /// <summary>
        /// Represents geography multi-line string data.
        /// </summary>
        [Description("Edm.GeographyMultiLineString")]
        GeographyMultiLineString,

        /// <summary>
        /// Represents geography multi-polygon data.
        /// </summary>
        [Description("Edm.GeographyMultiPolygon")]
        GeographyMultiPolygon,

        /// <summary>
        /// Represents geography collection data.
        /// </summary>
        [Description("Edm.GeographyCollection")]
        GeographyCollection,

        /// <summary>
        /// Represents geometry data.
        /// </summary>
        [Description("Edm.Geometry")]
        Geometry,

        /// <summary>
        /// Represents geometry point data.
        /// </summary>
        [Description("Edm.GeometryPoint")]
        GeometryPoint,

        /// <summary>
        /// Represents geometry line string data.
        /// </summary>
        [Description("Edm.GeometryLineString")]
        GeometryLineString,

        /// <summary>
        /// Represents geometry polygon data.
        /// </summary>
        [Description("Edm.GeometryPolygon")]
        GeometryPolygon,

        /// <summary>
        /// Represents geometry multi-point data.
        /// </summary>
        [Description("Edm.GeometryMultiPoint")]
        GeometryMultiPoint,

        /// <summary>
        /// Represents geometry multi-line string data.
        /// </summary>
        [Description("Edm.GeometryMultiLineString")]
        GeometryMultiLineString,

        /// <summary>
        /// Represents geometry multi-polygon data.
        /// </summary>
        [Description("Edm.GeometryMultiPolygon")]
        GeometryMultiPolygon,

        /// <summary>
        /// Represents geometry collection data.
        /// </summary>
        [Description("Edm.GeometryCollection")]
        GeometryCollection

    }

}
