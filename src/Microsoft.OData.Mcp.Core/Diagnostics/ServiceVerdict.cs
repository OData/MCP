// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Core.Diagnostics
{

    /// <summary>
    /// What a <see cref="ServiceProbe"/> concluded about a service as a whole.
    /// </summary>
    public enum ServiceVerdict
    {

        /// <summary>
        /// Metadata parsed, a row came back, and the row matched the model. The catalog can serve this service as-is.
        /// </summary>
        Ready = 0,

        /// <summary>
        /// Metadata is public but the data answered 401 or 403. Sign-in is needed before any tool call succeeds.
        /// </summary>
        DataSecured,

        /// <summary>
        /// The metadata document itself answered 401 or 403. Sign-in is needed before the catalog can be built.
        /// </summary>
        MetadataSecured,

        /// <summary>
        /// The metadata document or the first entity set could not be fetched: 404, 5xx, or a transport failure.
        /// </summary>
        Unreachable,

        /// <summary>
        /// The service answered but not in a form the catalog understands: non-CSDL metadata, non-JSON data, or a
        /// row whose properties do not match the model.
        /// </summary>
        Unreadable

    }

}
