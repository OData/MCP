using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.OData.Mcp.Authentication.Models
{
    /// <summary>
    /// Represents client credentials for OAuth2 authentication.
    /// </summary>
    /// <remarks>
    /// These credentials identify the MCP server to authorization servers when performing
    /// OAuth2 flows that require client authentication, such as token exchange or
    /// on-behalf-of flows.
    /// </remarks>
    public sealed class ClientCredentials
    {
        #region Properties

        /// <summary>
        /// Gets or sets the client identifier.
        /// </summary>
        /// <value>The client ID registered with the authorization server.</value>
        /// <remarks>
        /// The client ID uniquely identifies the MCP server application to the
        /// authorization server. It is typically a GUID or other unique string
        /// assigned during application registration.
        /// </remarks>
        public required string ClientId { get; set; }

        /// <summary>
        /// Gets or sets the client authentication method.
        /// </summary>
        /// <value>The method used to authenticate the client to the authorization server.</value>
        /// <remarks>
        /// Different authorization servers support different client authentication methods.
        /// The most common are client secrets and certificate-based authentication.
        /// </remarks>
        public ClientAuthenticationMethod AuthenticationMethod { get; set; } = ClientAuthenticationMethod.ClientSecret;

        /// <summary>
        /// Gets or sets the client secret for secret-based authentication.
        /// </summary>
        /// <value>The client secret registered with the authorization server.</value>
        /// <remarks>
        /// This secret is used when the authentication method is ClientSecret or ClientSecretPost.
        /// It should be kept secure and rotated regularly for security best practices.
        /// </remarks>
        public string? ClientSecret { get; set; }

        /// <summary>
        /// Gets or sets the certificate for certificate-based authentication.
        /// </summary>
        /// <value>Configuration for client certificate authentication.</value>
        /// <remarks>
        /// This certificate is used when the authentication method requires certificate-based
        /// authentication. It provides a more secure alternative to client secrets.
        /// </remarks>
        public ClientCertificate? Certificate { get; set; }

        /// <summary>
        /// Gets or sets the assertion for JWT-based client authentication.
        /// </summary>
        /// <value>The JWT assertion used for client authentication.</value>
        /// <remarks>
        /// This is used when the authentication method is PrivateKeyJwt or ClientSecretJwt.
        /// The assertion must be properly signed and contain the required claims.
        /// </remarks>
        public string? ClientAssertion { get; set; }

        /// <summary>
        /// Gets or sets the assertion type for JWT-based client authentication.
        /// </summary>
        /// <value>The type of the client assertion.</value>
        /// <remarks>
        /// This is typically "urn:ietf:params:oauth:client-assertion-type:jwt-bearer"
        /// for JWT-based client authentication methods.
        /// </remarks>
        public string? ClientAssertionType { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientCredentials"/> class.
        /// </summary>
        public ClientCredentials()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientCredentials"/> class with client secret authentication.
        /// </summary>
        /// <param name="clientId">The client identifier.</param>
        /// <param name="clientSecret">The client secret.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="clientId"/> or <paramref name="clientSecret"/> is null or whitespace.</exception>
        public ClientCredentials(string clientId, string clientSecret)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
            ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);
#else
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new ArgumentException("Client ID cannot be null or whitespace.", nameof(clientId));
            }
            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new ArgumentException("Client secret cannot be null or whitespace.", nameof(clientSecret));
            }
#endif

            ClientId = clientId;
            ClientSecret = clientSecret;
            AuthenticationMethod = ClientAuthenticationMethod.ClientSecret;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientCredentials"/> class with certificate authentication.
        /// </summary>
        /// <param name="clientId">The client identifier.</param>
        /// <param name="certificate">The client certificate configuration.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="clientId"/> is null or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="certificate"/> is null.</exception>
        public ClientCredentials(string clientId, ClientCertificate certificate)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
            ArgumentNullException.ThrowIfNull(certificate);
#else
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new ArgumentException("Client ID cannot be null or whitespace.", nameof(clientId));
            }
            if (certificate is null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }
#endif

            ClientId = clientId;
            Certificate = certificate;
            AuthenticationMethod = ClientAuthenticationMethod.TlsClientAuth;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Validates the client credentials for consistency and completeness.
        /// </summary>
        /// <returns>A collection of validation errors, or an empty collection if the credentials are valid.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(ClientId))
            {
                errors.Add("Client ID cannot be null or empty.");
            }

            switch (AuthenticationMethod)
            {
                case ClientAuthenticationMethod.ClientSecret:
                case ClientAuthenticationMethod.ClientSecretPost:
                    if (string.IsNullOrWhiteSpace(ClientSecret))
                    {
                        errors.Add($"Client secret is required for authentication method {AuthenticationMethod}.");
                    }
                    break;

                case ClientAuthenticationMethod.TlsClientAuth:
                case ClientAuthenticationMethod.SelfSignedTlsClientAuth:
                    if (Certificate is null)
                    {
                        errors.Add($"Client certificate is required for authentication method {AuthenticationMethod}.");
                    }
                    else
                    {
                        var certificateErrors = Certificate.Validate();
                        errors.AddRange(certificateErrors);
                    }
                    break;

                case ClientAuthenticationMethod.PrivateKeyJwt:
                case ClientAuthenticationMethod.ClientSecretJwt:
                    if (string.IsNullOrWhiteSpace(ClientAssertion))
                    {
                        errors.Add($"Client assertion is required for authentication method {AuthenticationMethod}.");
                    }
                    if (string.IsNullOrWhiteSpace(ClientAssertionType))
                    {
                        errors.Add($"Client assertion type is required for authentication method {AuthenticationMethod}.");
                    }
                    break;

                case ClientAuthenticationMethod.None:
                    // No additional validation required for public clients
                    break;

                default:
                    errors.Add($"Unsupported client authentication method: {AuthenticationMethod}.");
                    break;
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the client credentials.
        /// </summary>
        /// <returns>A summary of the client credentials configuration.</returns>
        public override string ToString()
        {
            return $"Client: {ClientId}, Method: {AuthenticationMethod}";
        }

        #endregion
    }

    /// <summary>
    /// Defines the client authentication methods supported by OAuth2.
    /// </summary>
    public enum ClientAuthenticationMethod
    {
        /// <summary>
        /// No client authentication (public client).
        /// </summary>
        None,

        /// <summary>
        /// Client secret sent in the Authorization header using HTTP Basic authentication.
        /// </summary>
        ClientSecret,

        /// <summary>
        /// Client secret sent in the request body as a form parameter.
        /// </summary>
        ClientSecretPost,

        /// <summary>
        /// Client authentication using TLS client certificates.
        /// </summary>
        TlsClientAuth,

        /// <summary>
        /// Client authentication using self-signed TLS client certificates.
        /// </summary>
        SelfSignedTlsClientAuth,

        /// <summary>
        /// Client authentication using JWT signed with the client's private key.
        /// </summary>
        PrivateKeyJwt,

        /// <summary>
        /// Client authentication using JWT signed with the client secret.
        /// </summary>
        ClientSecretJwt
    }
}