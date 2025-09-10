// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.OData.Mcp.Core.Configuration
{

    /// <summary>
    /// Basic authentication credentials.
    /// </summary>
    public sealed class BasicAuthenticationCredentials
    {

        /// <summary>
        /// Gets or sets the username.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the password.
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Validates the credentials.
        /// </summary>
        /// <returns>Validation errors.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(Username)) errors.Add("Username is required");
            if (string.IsNullOrWhiteSpace(Password)) errors.Add("Password is required");
            return errors;
        }

        /// <summary>
        /// Creates a copy of these credentials.
        /// </summary>
        /// <returns>A new instance with the same values.</returns>
        public BasicAuthenticationCredentials Clone()
        {
            return new BasicAuthenticationCredentials { Username = Username, Password = Password };
        }

    }

}
