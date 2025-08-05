using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.OData.Mcp.Core.Configuration;

namespace Microsoft.OData.Mcp.Sidecar.Extensions
{
    /// <summary>
    /// Extensions for configuring security headers.
    /// </summary>
    public static class SecurityExtensions
    {
        /// <summary>
        /// Adds security headers to the application pipeline.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="config">The security headers configuration.</param>
        /// <returns>The application builder for chaining.</returns>
        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, SecurityHeadersConfiguration config)
        {
            return app.Use(async (context, next) =>
            {
                var response = context.Response;
                
                if (config.EnableHsts && context.Request.IsHttps)
                {
                    response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
                }
                
                if (config.EnableXContentTypeOptions)
                {
                    response.Headers["X-Content-Type-Options"] = "nosniff";
                }
                
                if (config.EnableXFrameOptions)
                {
                    response.Headers["X-Frame-Options"] = config.XFrameOptions;
                }
                
                response.Headers["X-XSS-Protection"] = "1; mode=block";
                response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                
                await next();
            });
        }
    }
}