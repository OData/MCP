using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.OData.Mcp.Authentication.Models
{

    /// <summary>
    /// Configuration for client certificate authentication.
    /// </summary>
    /// <remarks>
    /// Client certificates provide a secure method for authenticating the MCP server
    /// to authorization servers and downstream services. They offer better security
    /// than client secrets and support automatic rotation.
    /// </remarks>
    public sealed class ClientCertificate
    {

        #region Properties

        /// <summary>
        /// Gets or sets the source of the client certificate.
        /// </summary>
        /// <value>The method used to locate and load the client certificate.</value>
        /// <remarks>
        /// Different certificate sources provide different levels of security and
        /// management complexity. Store-based certificates are typically more secure
        /// in production environments.
        /// </remarks>
        public CertificateSource Source { get; set; } = CertificateSource.Store;

        /// <summary>
        /// Gets or sets the certificate store location.
        /// </summary>
        /// <value>The certificate store location (used when Source is Store).</value>
        /// <remarks>
        /// The store location determines which certificate store to search.
        /// CurrentUser is typically used for development, while LocalMachine
        /// is used for production services.
        /// </remarks>
        public StoreLocation StoreLocation { get; set; } = StoreLocation.CurrentUser;

        /// <summary>
        /// Gets or sets the certificate store name.
        /// </summary>
        /// <value>The certificate store name (used when Source is Store).</value>
        /// <remarks>
        /// The store name determines which certificate store to search within
        /// the specified location. "My" (Personal) is the most common store
        /// for client certificates.
        /// </remarks>
        public StoreName StoreName { get; set; } = StoreName.My;

        /// <summary>
        /// Gets or sets the certificate thumbprint for store-based lookup.
        /// </summary>
        /// <value>The thumbprint (SHA-1 hash) of the certificate to locate.</value>
        /// <remarks>
        /// The thumbprint uniquely identifies a certificate within a store.
        /// It should be specified without spaces or special characters.
        /// </remarks>
        public string? Thumbprint { get; set; }

        /// <summary>
        /// Gets or sets the certificate subject name for store-based lookup.
        /// </summary>
        /// <value>The subject name of the certificate to locate.</value>
        /// <remarks>
        /// The subject name provides an alternative way to locate certificates
        /// when the thumbprint is not known. It should match the certificate's
        /// subject field exactly.
        /// </remarks>
        public string? SubjectName { get; set; }

        /// <summary>
        /// Gets or sets the file path for file-based certificates.
        /// </summary>
        /// <value>The path to the certificate file (used when Source is File).</value>
        /// <remarks>
        /// The file path can point to various certificate formats including
        /// .pfx, .p12, .cer, and .crt files. Password-protected files require
        /// the Password property to be set.
        /// </remarks>
        public string? FilePath { get; set; }

        /// <summary>
        /// Gets or sets the password for encrypted certificate files.
        /// </summary>
        /// <value>The password to decrypt the certificate file.</value>
        /// <remarks>
        /// This password is used when loading encrypted certificate files such
        /// as .pfx or .p12 files. It should be stored securely and not logged.
        /// </remarks>
        public string? Password { get; set; }

        /// <summary>
        /// Gets or sets the Base64-encoded certificate data.
        /// </summary>
        /// <value>The certificate data in Base64 format (used when Source is Base64).</value>
        /// <remarks>
        /// This allows certificates to be embedded directly in configuration.
        /// While convenient for some scenarios, this method should be used
        /// carefully to avoid exposing internal keys in configuration files.
        /// </remarks>
        public string? Base64Data { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to validate the certificate chain.
        /// </summary>
        /// <value><c>true</c> if the certificate chain should be validated; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Chain validation ensures the certificate is issued by a trusted
        /// certificate authority. Disabling this should only be done in
        /// development environments with self-signed certificates.
        /// </remarks>
        public bool ValidateChain { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to check certificate revocation.
        /// </summary>
        /// <value><c>true</c> if certificate revocation should be checked; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Revocation checking ensures the certificate hasn't been revoked
        /// by the issuing authority. This requires network access to
        /// revocation services and may impact performance.
        /// </remarks>
        public bool CheckRevocation { get; set; } = true;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientCertificate"/> class.
        /// </summary>
        public ClientCertificate()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientCertificate"/> class for store-based lookup by thumbprint.
        /// </summary>
        /// <param name="thumbprint">The certificate thumbprint.</param>
        /// <param name="storeLocation">The certificate store location.</param>
        /// <param name="storeName">The certificate store name.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="thumbprint"/> is null or whitespace.</exception>
        public ClientCertificate(string thumbprint, StoreLocation storeLocation = StoreLocation.CurrentUser, StoreName storeName = StoreName.My)
        {
ArgumentException.ThrowIfNullOrWhiteSpace(thumbprint);

            Source = CertificateSource.Store;
            Thumbprint = thumbprint;
            StoreLocation = storeLocation;
            StoreName = storeName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientCertificate"/> class for file-based certificates.
        /// </summary>
        /// <param name="filePath">The path to the certificate file.</param>
        /// <param name="password">The password for encrypted files (optional).</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> is null or whitespace.</exception>
        public ClientCertificate(string filePath, string? password = null)
        {
ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            Source = CertificateSource.File;
            FilePath = filePath;
            Password = password;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Loads the certificate based on the configured source.
        /// </summary>
        /// <returns>The loaded X.509 certificate.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the certificate cannot be loaded.</exception>
        public X509Certificate2 LoadCertificate()
        {
            return Source switch
            {
                CertificateSource.Store => LoadFromStore(),
                CertificateSource.File => LoadFromFile(),
                CertificateSource.Base64 => LoadFromBase64(),
                _ => throw new InvalidOperationException($"Unsupported certificate source: {Source}")
            };
        }

        /// <summary>
        /// Validates the client certificate configuration for consistency and completeness.
        /// </summary>
        /// <returns>A collection of validation errors, or an empty collection if the configuration is valid.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            switch (Source)
            {
                case CertificateSource.Store:
                    if (string.IsNullOrWhiteSpace(Thumbprint) && string.IsNullOrWhiteSpace(SubjectName))
                    {
                        errors.Add("Either Thumbprint or SubjectName must be specified for store-based certificate lookup.");
                    }
                    break;

                case CertificateSource.File:
                    if (string.IsNullOrWhiteSpace(FilePath))
                    {
                        errors.Add("FilePath must be specified for file-based certificate loading.");
                    }
                    else if (!Path.IsPathRooted(FilePath))
                    {
                        errors.Add("Certificate FilePath should be an absolute path for security and reliability.");
                    }
                    break;

                case CertificateSource.Base64:
                    if (string.IsNullOrWhiteSpace(Base64Data))
                    {
                        errors.Add("Base64Data must be specified for Base64-encoded certificate loading.");
                    }
                    break;

                default:
                    errors.Add($"Unsupported certificate source: {Source}.");
                    break;
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the client certificate configuration.
        /// </summary>
        /// <returns>A summary of the certificate configuration.</returns>
        public override string ToString()
        {
            return Source switch
            {
                CertificateSource.Store => $"Store: {StoreLocation}/{StoreName}, Thumbprint: {Thumbprint?[..Math.Min(8, Thumbprint?.Length ?? 0)]}...",
                CertificateSource.File => $"File: {Path.GetFileName(FilePath)}, HasPassword: {!string.IsNullOrWhiteSpace(Password)}",
                CertificateSource.Base64 => "Base64 Data",
                _ => $"Unknown source: {Source}"
            };
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Loads a certificate from the certificate store.
        /// </summary>
        /// <returns>The loaded certificate.</returns>
        internal X509Certificate2 LoadFromStore()
        {
            using var store = new X509Store(StoreName, StoreLocation);
            store.Open(OpenFlags.ReadOnly);

            X509Certificate2Collection certificates;

            if (!string.IsNullOrWhiteSpace(Thumbprint))
            {
                certificates = store.Certificates.Find(X509FindType.FindByThumbprint, Thumbprint, ValidateChain);
            }
            else if (!string.IsNullOrWhiteSpace(SubjectName))
            {
                certificates = store.Certificates.Find(X509FindType.FindBySubjectName, SubjectName, ValidateChain);
            }
            else
            {
                throw new InvalidOperationException("Either Thumbprint or SubjectName must be specified for store-based certificate lookup.");
            }

            if (certificates.Count == 0)
            {
                var identifier = !string.IsNullOrWhiteSpace(Thumbprint) ? $"thumbprint '{Thumbprint}'" : $"subject '{SubjectName}'";
                throw new InvalidOperationException($"Certificate with {identifier} not found in store {StoreLocation}/{StoreName}.");
            }

            if (certificates.Count > 1)
            {
                var identifier = !string.IsNullOrWhiteSpace(Thumbprint) ? $"thumbprint '{Thumbprint}'" : $"subject '{SubjectName}'";
                throw new InvalidOperationException($"Multiple certificates found with {identifier} in store {StoreLocation}/{StoreName}.");
            }

            return certificates[0];
        }

        /// <summary>
        /// Loads a certificate from a file.
        /// </summary>
        /// <returns>The loaded certificate.</returns>
        internal X509Certificate2 LoadFromFile()
        {
            if (string.IsNullOrWhiteSpace(FilePath))
            {
                throw new InvalidOperationException("FilePath must be specified for file-based certificate loading.");
            }

            if (!File.Exists(FilePath))
            {
                throw new FileNotFoundException($"Certificate file not found: {FilePath}");
            }

            try
            {
                var certificate = string.IsNullOrWhiteSpace(Password)
                    ? new X509Certificate2(FilePath)
                    : new X509Certificate2(FilePath, Password);

                if (ValidateChain)
                {
                    ValidateCertificateChain(certificate);
                }

                return certificate;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load certificate from file '{FilePath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Loads a certificate from Base64-encoded data.
        /// </summary>
        /// <returns>The loaded certificate.</returns>
        internal X509Certificate2 LoadFromBase64()
        {
            if (string.IsNullOrWhiteSpace(Base64Data))
            {
                throw new InvalidOperationException("Base64Data must be specified for Base64-encoded certificate loading.");
            }

            try
            {
                var certificateBytes = Convert.FromBase64String(Base64Data);
                var certificate = string.IsNullOrWhiteSpace(Password)
                    ? new X509Certificate2(certificateBytes)
                    : new X509Certificate2(certificateBytes, Password);

                if (ValidateChain)
                {
                    ValidateCertificateChain(certificate);
                }

                return certificate;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load certificate from Base64 data: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Validates the certificate chain and revocation status.
        /// </summary>
        /// <param name="certificate">The certificate to validate.</param>
        internal void ValidateCertificateChain(X509Certificate2 certificate)
        {
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = CheckRevocation ? X509RevocationMode.Online : X509RevocationMode.NoCheck;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;

            if (!chain.Build(certificate))
            {
                var errors = string.Join(", ", chain.ChainStatus.Select(s => s.StatusInformation));
                throw new InvalidOperationException($"Certificate chain validation failed: {errors}");
            }
        }

        #endregion

    }

}
