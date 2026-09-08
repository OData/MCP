// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Text.Json.Serialization;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.AspNetCore.Authentication
{

    /// <summary>
    /// The source-generated <see cref="JsonSerializerContext"/> the RFC 9728 protected resource metadata
    /// document is written through.
    /// </summary>
    /// <example>
    /// <code>
    /// await JsonSerializer.SerializeAsync(
    ///     context.Response.Body,
    ///     metadata,
    ///     ODataProtectedResourceJsonContext.Default.ProtectedResourceMetadata,
    ///     context.RequestAborted);
    /// </code>
    /// </example>
    /// <remarks>
    /// AOT: JSON source-gen; no reflection. The document's property names come from the SDK type's own
    /// <see cref="JsonPropertyNameAttribute"/> values, so no naming policy is configured here — the wire
    /// shape is RFC 9728 snake_case either way.
    /// </remarks>
    [JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(SdkAuth.ProtectedResourceMetadata))]
    internal sealed partial class ODataProtectedResourceJsonContext : JsonSerializerContext
    {
    }

}
