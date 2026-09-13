// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// Protocol identifiers for outbound OAuth: header and scheme names, well-known suffixes, grant type
    /// identifiers, wire parameter and error names, and the named <c>HttpClient</c>s and Latchkey service
    /// name outbound OAuth wires into DI.
    /// </summary>
    /// <remarks>
    /// Names and values in this class are normative per <c>specs/v3/AUTHENTICATION.md</c> "Constants (excerpt)".
    /// <see cref="MicrosoftGraphResourceAppId"/> exists so tests and logs can detect the Microsoft Graph
    /// challenge trap, not so it can be used as a client id.
    /// </remarks>
    public static class ODataMcpAuthConstants
    {

        #region Fields

        /// <summary>
        /// RFC 6749 token response <c>access_token</c> property.
        /// </summary>
        public const string AccessTokenProperty = "access_token";

        /// <summary>
        /// RFC 7235 <c>Authorization</c> request header.
        /// </summary>
        public const string AuthorizationHeader = "Authorization";

        /// <summary>
        /// <c>WWW-Authenticate</c> challenge <c>authorization_uri</c> parameter.
        /// </summary>
        public const string AuthorizationUriParameter = "authorization_uri";

        /// <summary>
        /// Well-known authorize-path suffixes, tried in order, used to derive authorization server base URIs
        /// from a challenge <see cref="AuthorizationUriParameter"/>.
        /// </summary>
        public static readonly string[] AuthorizeSuffixes = ["/oauth2/v2.0/authorize", "/oauth2/authorize", "/authorize"];

        /// <summary>
        /// RFC 7617 <c>Basic</c> authentication scheme.
        /// </summary>
        public const string BasicScheme = "Basic";

        /// <summary>
        /// RFC 6750 <c>Bearer</c> authentication scheme.
        /// </summary>
        public const string BearerScheme = "Bearer";

        /// <summary>
        /// RFC 6749 / <c>WWW-Authenticate</c> challenge <c>client_id</c> parameter.
        /// </summary>
        public const string ClientIdParameter = "client_id";

        /// <summary>
        /// RFC 7591 dynamic client registration request <c>client_name</c> property.
        /// </summary>
        public const string ClientNameProperty = "client_name";

        /// <summary>
        /// Environment variable holding the client credentials grant client secret.
        /// </summary>
        public const string ClientSecretEnvironmentVariable = "ODATA_MCP_CLIENT_SECRET";

        /// <summary>
        /// RFC 6749 section 2.3.1 token request <c>client_secret</c> parameter.
        /// </summary>
        public const string ClientSecretParameter = "client_secret";

        /// <summary>
        /// RFC 7636 PKCE authorization request <c>code_challenge_method</c> parameter.
        /// </summary>
        public const string CodeChallengeMethodParameter = "code_challenge_method";

        /// <summary>
        /// RFC 7636 PKCE <c>S256</c> code challenge method.
        /// </summary>
        public const string CodeChallengeMethodS256 = "S256";

        /// <summary>
        /// RFC 7636 PKCE authorization request <c>code_challenge</c> parameter.
        /// </summary>
        public const string CodeChallengeParameter = "code_challenge";

        /// <summary>
        /// RFC 6749 authorization response / token request <c>code</c> parameter.
        /// </summary>
        public const string CodeParameter = "code";

        /// <summary>
        /// RFC 7636 PKCE token request <c>code_verifier</c> parameter.
        /// </summary>
        public const string CodeVerifierParameter = "code_verifier";

        /// <summary>
        /// RFC 8628 device authorization grant type URN.
        /// </summary>
        public const string DeviceCodeGrantType = "urn:ietf:params:oauth:grant-type:device_code";

        /// <summary>
        /// RFC 8628 token request <c>device_code</c> parameter.
        /// </summary>
        public const string DeviceCodeParameter = "device_code";

        /// <summary>
        /// The RFC 7591 <c>client_name</c> this process registers itself under when no <c>--client-id</c> was
        /// configured.
        /// </summary>
        public const string DynamicClientName = "odata-mcp";

        /// <summary>
        /// RFC 6749 <c>error</c> value <c>access_denied</c>.
        /// </summary>
        public const string ErrorAccessDenied = "access_denied";

        /// <summary>
        /// RFC 8628 <c>error</c> value <c>authorization_pending</c>.
        /// </summary>
        public const string ErrorAuthorizationPending = "authorization_pending";

        /// <summary>
        /// RFC 6749 <c>error_description</c> parameter.
        /// </summary>
        public const string ErrorDescriptionParameter = "error_description";

        /// <summary>
        /// RFC 8628 <c>error</c> value <c>expired_token</c>.
        /// </summary>
        public const string ErrorExpiredToken = "expired_token";

        /// <summary>
        /// RFC 6750 <c>error</c> value <c>invalid_token</c>.
        /// </summary>
        public const string ErrorInvalidToken = "invalid_token";

        /// <summary>
        /// RFC 6749 / <c>WWW-Authenticate</c> challenge <c>error</c> parameter.
        /// </summary>
        public const string ErrorParameter = "error";

        /// <summary>
        /// RFC 8628 <c>error</c> value <c>slow_down</c>.
        /// </summary>
        public const string ErrorSlowDown = "slow_down";

        /// <summary>
        /// RFC 6749 <c>authorization_code</c> grant type.
        /// </summary>
        public const string GrantTypeAuthorizationCode = "authorization_code";

        /// <summary>
        /// RFC 6749 <c>client_credentials</c> grant type.
        /// </summary>
        public const string GrantTypeClientCredentials = "client_credentials";

        /// <summary>
        /// RFC 8628 short-form <c>device_code</c> grant type.
        /// </summary>
        public const string GrantTypeDeviceCode = "device_code";

        /// <summary>
        /// RFC 6749 / RFC 8628 token request <c>grant_type</c> parameter.
        /// </summary>
        public const string GrantTypeParameter = "grant_type";

        /// <summary>
        /// RFC 6749 <c>refresh_token</c> grant type.
        /// </summary>
        public const string GrantTypeRefreshToken = "refresh_token";

        /// <summary>
        /// Environment variable holding an Identity Assertion Grant id token.
        /// </summary>
        public const string IdTokenEnvironmentVariable = "ODATA_MCP_ID_TOKEN";

        /// <summary>
        /// RFC 9207 authorization response <c>iss</c> parameter.
        /// </summary>
        public const string IssParameter = "iss";

        /// <summary>
        /// RFC 7523 JWT bearer grant type URN.
        /// </summary>
        public const string JwtBearerGrantType = "urn:ietf:params:oauth:grant-type:jwt-bearer";

        /// <summary>
        /// Latchkey <c>ServiceName</c> for the token cache backing store.
        /// </summary>
        public const string LatchkeyServiceName = "com.microsoft.odata.mcp";

        /// <summary>
        /// Microsoft Graph resource application id. A detection sentinel for the Graph challenge trap, never
        /// our public client id.
        /// </summary>
        public const string MicrosoftGraphResourceAppId = "00000003-0000-0000-c000-000000000000";

        /// <summary>
        /// RFC 8414 authorization server metadata well-known suffix.
        /// </summary>
        public const string OAuthAuthorizationServerSuffix = "/.well-known/oauth-authorization-server";

        /// <summary>
        /// Named <c>HttpClient</c> used for well-known discovery, device-code, token, and DCR requests.
        /// Carries no delegating handler and uses absolute URIs only.
        /// </summary>
        public const string OAuthHttpClientName = "OAuth";

        /// <summary>
        /// RFC 9728 protected resource metadata well-known suffix.
        /// </summary>
        public const string OAuthProtectedResourceSuffix = "/.well-known/oauth-protected-resource";

        /// <summary>
        /// Named <c>HttpClient</c> used for <c>$metadata</c> and OData data requests, carrying the outbound
        /// OAuth delegating handler.
        /// </summary>
        public const string ODataHttpClientName = "OData";

        /// <summary>
        /// RFC 6749 <c>offline_access</c> scope requesting a refresh token.
        /// </summary>
        public const string OfflineAccessScope = "offline_access";

        /// <summary>
        /// OpenID Connect discovery document well-known suffix.
        /// </summary>
        public const string OpenIdConfigurationSuffix = "/.well-known/openid-configuration";

        /// <summary>
        /// RFC 6750 <c>WWW-Authenticate</c> challenge <c>realm</c> parameter.
        /// </summary>
        public const string RealmParameter = "realm";

        /// <summary>
        /// RFC 6749 authorization and token request <c>redirect_uri</c> parameter.
        /// </summary>
        public const string RedirectUriParameter = "redirect_uri";

        /// <summary>
        /// RFC 6749 token response / request <c>refresh_token</c> property.
        /// </summary>
        public const string RefreshTokenProperty = "refresh_token";

        /// <summary>
        /// RFC 9728 <c>WWW-Authenticate</c> challenge <c>resource_metadata</c> parameter.
        /// </summary>
        public const string ResourceMetadataParameter = "resource_metadata";

        /// <summary>
        /// RFC 8707 resource indicator <c>resource</c> parameter.
        /// </summary>
        public const string ResourceParameter = "resource";

        /// <summary>
        /// RFC 6749 authorization request <c>code</c> response type, the only one this client requests.
        /// </summary>
        public const string ResponseTypeCode = "code";

        /// <summary>
        /// RFC 6749 authorization request <c>response_type</c> parameter.
        /// </summary>
        public const string ResponseTypeParameter = "response_type";

        /// <summary>
        /// RFC 6749 / <c>WWW-Authenticate</c> challenge <c>scope</c> parameter.
        /// </summary>
        public const string ScopeParameter = "scope";

        /// <summary>
        /// RFC 6749 authorization request and response <c>state</c> parameter.
        /// </summary>
        public const string StateParameter = "state";

        /// <summary>
        /// RFC 8693 token exchange grant type URN.
        /// </summary>
        public const string TokenExchangeGrantType = "urn:ietf:params:oauth:grant-type:token-exchange";

        /// <summary>
        /// Minimum remaining access-token lifetime before the outbound handler refreshes proactively
        /// instead of sending the request and risking a 401.
        /// </summary>
        public static readonly TimeSpan TokenRefreshSkew = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Microsoft identity platform version 2 well-known path segment.
        /// </summary>
        public const string V2Segment = "/v2.0";

        /// <summary>
        /// RFC 7235 <c>WWW-Authenticate</c> response header.
        /// </summary>
        public const string WwwAuthenticateHeader = "WWW-Authenticate";

        #endregion

    }

}
