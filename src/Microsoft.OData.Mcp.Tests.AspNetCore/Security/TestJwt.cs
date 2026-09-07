// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Security
{

    /// <summary>
    /// HMAC test tokens for authenticated MCP hosts.
    /// </summary>
    internal static class TestJwt
    {

        #region Fields

        /// <summary>
        /// Gets a 256-bit HMAC key.
        /// </summary>
        internal static readonly byte[] SigningKey = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef");

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a compact JWT signed with <see cref="SigningKey"/>.
        /// </summary>
        /// <returns>
        /// The token.
        /// </returns>
        public static string CreateToken()
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.CreateToken(new SecurityTokenDescriptor
            {
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(SigningKey), SecurityAlgorithms.HmacSha256),
                Subject = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "tester")
                ])
            });

            return handler.WriteToken(token);
        }

        #endregion

    }

}
