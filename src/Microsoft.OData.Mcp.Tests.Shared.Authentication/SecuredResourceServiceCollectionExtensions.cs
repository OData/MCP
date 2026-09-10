// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.OData.Mcp.Tests.Shared.Authentication
{

    /// <summary>
    /// Turns an ordinary test host into an OAuth protected resource backed by a <see cref="LocalAuthorizationServer"/>.
    /// </summary>
    public static class SecuredResourceServiceCollectionExtensions
    {

        #region Public Methods

        /// <summary>
        /// Adds JWT bearer authentication keyed to a local authorization server and protects everything beneath a path prefix.
        /// </summary>
        /// <param name="services">The service collection to add to.</param>
        /// <param name="authorizationServer">The authorization server whose signing key and metadata URI the resource trusts.</param>
        /// <param name="protectedPathPrefix">The path prefix that requires a bearer token.</param>
        /// <param name="bareBearerChallenge">Whether the challenge is a bare <c>Bearer</c> with no RFC 9728 <c>resource_metadata</c> hint.</param>
        /// <param name="challengeOverride">The verbatim <c>WWW-Authenticate</c> value to answer with, or <see langword="null"/> for the built challenge.</param>
        /// <returns>
        /// The same service collection, so calls can be chained.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="authorizationServer"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="protectedPathPrefix"/> is <see langword="null"/>, empty, or whitespace.</exception>
        /// <example>
        /// <code>
        /// services.AddSecuredResource(authorizationServer);
        /// </code>
        /// </example>
        /// <remarks>
        /// Requests beneath <paramref name="protectedPathPrefix"/> that arrive without a valid bearer token are answered
        /// with <c>401</c> and <c>WWW-Authenticate: Bearer resource_metadata="&lt;PRM uri&gt;"</c>.
        /// <para>
        /// <paramref name="bareBearerChallenge"/> models the far more common deployment that answers with
        /// nothing but <c>Bearer</c>: discovery cannot find an authorization server from it at all, which is
        /// the case an operator has to be asked for an issuer URL.
        /// </para>
        /// <para>
        /// A resource whose protected resource metadata document is not published as <c>200</c> also advertises
        /// <c>authorization_uri</c> and <c>client_id</c>, which is exactly the shape Microsoft Graph answers
        /// with. Without them a client that follows <c>resource_metadata</c> into a <c>401</c> would have
        /// nothing left to discover from, and the Graph trap would be an unrecoverable dead end rather than the
        /// single wasted GET it actually is.
        /// </para>
        /// </remarks>
        public static IServiceCollection AddSecuredResource(
            this IServiceCollection services,
            LocalAuthorizationServer authorizationServer,
            string protectedPathPrefix = "/odata",
            bool bareBearerChallenge = false,
            string? challengeOverride = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(authorizationServer);
            ArgumentException.ThrowIfNullOrWhiteSpace(protectedPathPrefix);

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Challenge = string.IsNullOrWhiteSpace(challengeOverride)
                        ? CreateChallenge(authorizationServer, bareBearerChallenge)
                        : challengeOverride;
                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = context =>
                        {
                            if (context.SecurityToken is not null && authorizationServer.IsRevoked(context.SecurityToken))
                            {
                                context.Fail("revoked");
                            }

                            return Task.CompletedTask;
                        }
                    };
                    options.TokenValidationParameters = authorizationServer.CreateTokenValidationParameters();
                });
            services.AddAuthorization();
            services.AddTransient<IStartupFilter>(_ => new SecuredResourceStartupFilter(protectedPathPrefix));

            return services;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds the <c>WWW-Authenticate</c> value an unauthenticated request is answered with.
        /// </summary>
        /// <param name="authorizationServer">The authorization server whose metadata URIs the challenge points at.</param>
        /// <param name="bareBearerChallenge">Whether the challenge carries no discovery hints at all.</param>
        /// <returns>
        /// The challenge value.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="authorizationServer"/> is <see langword="null"/>.</exception>
        internal static string CreateChallenge(LocalAuthorizationServer authorizationServer, bool bareBearerChallenge)
        {
            ArgumentNullException.ThrowIfNull(authorizationServer);

            if (bareBearerChallenge)
            {
                return "Bearer";
            }

            if (authorizationServer.Options.ProtectedResourceMetadataMode is ProtectedResourceMetadataMode.Ok)
            {
                return $"Bearer resource_metadata=\"{authorizationServer.ProtectedResourceMetadataUri}\"";
            }

            return $"Bearer realm=\"\", authorization_uri=\"{authorizationServer.AuthorizationEndpoint}\", "
                + $"client_id=\"{LocalAuthorizationServer.GraphClientId}\", "
                + $"resource_metadata=\"{authorizationServer.ProtectedResourceMetadataUri}\"";
        }

        #endregion

    }

}
