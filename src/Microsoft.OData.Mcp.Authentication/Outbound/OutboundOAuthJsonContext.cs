// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Text.Json.Serialization;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// The source-generated <see cref="JsonSerializerContext"/> outbound OAuth uses for every wire and cache
    /// DTO it (de)serializes: our own metadata and error DTOs, and the SDK types this package reuses as-is.
    /// </summary>
    /// <example>
    /// <code>
    /// var metadata = JsonSerializer.Deserialize(json, OutboundOAuthJsonContext.Default.AuthorizationServerMetadata);
    /// </code>
    /// </example>
    /// <remarks>
    /// AOT: JSON source-gen; no reflection. Every type outbound OAuth serializes or deserializes must be
    /// registered here and read through <c>OutboundOAuthJsonContext.Default</c>; no member of this package
    /// calls a reflection-based <see cref="System.Text.Json.JsonSerializer"/> overload. Never
    /// <c>using ModelContextProtocol.Authentication;</c> unqualified in a file that also references this
    /// package's own <see cref="AuthorizationServerMetadata"/> or <see cref="TokenEndpointResponse"/> by
    /// simple name; use the <c>SdkAuth</c> alias shown above instead, because the SDK does not export types of
    /// those names publicly but a future SDK version could.
    /// </remarks>
    [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(AuthorizationServerMetadata))]
    [JsonSerializable(typeof(DeviceAuthorizationResponse))]
    [JsonSerializable(typeof(DynamicClientRegistrationRequest))]
    [JsonSerializable(typeof(SdkAuth.DynamicClientRegistrationResponse))]
    [JsonSerializable(typeof(OAuthErrorPayload))]
    [JsonSerializable(typeof(SdkAuth.ProtectedResourceMetadata))]
    [JsonSerializable(typeof(SdkAuth.TokenContainer))]
    [JsonSerializable(typeof(TokenEndpointResponse))]
    internal sealed partial class OutboundOAuthJsonContext : JsonSerializerContext
    {
    }

}
