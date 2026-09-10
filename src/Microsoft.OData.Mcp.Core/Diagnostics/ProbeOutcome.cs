// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Core.Diagnostics
{

    /// <summary>
    /// How one step of a <see cref="ServiceProbe"/> ended.
    /// </summary>
    public enum ProbeOutcome
    {

        /// <summary>
        /// The step was not attempted because an earlier step ruled it out.
        /// </summary>
        Skipped = 0,

        /// <summary>
        /// The step succeeded.
        /// </summary>
        Passed,

        /// <summary>
        /// The service answered <c>401 Unauthorized</c>; the challenge is on the step.
        /// </summary>
        Unauthorized,

        /// <summary>
        /// The service answered <c>403 Forbidden</c>.
        /// </summary>
        Forbidden,

        /// <summary>
        /// The service answered <c>404 Not Found</c>.
        /// </summary>
        NotFound,

        /// <summary>
        /// The service answered, but not with something the catalog can use: HTML instead of CSDL, a body that is
        /// not JSON, or a row whose properties do not match the model.
        /// </summary>
        Unreadable,

        /// <summary>
        /// The request failed for another reason: a 5xx, a transport error, or a timeout.
        /// </summary>
        Failed

    }

}
