using System;
using Microsoft.AspNetCore.Mvc.Filters;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Microsoft.OData.Mcp.AspNetCore.Hosting
{

    /// <summary>
    /// Keeps <see cref="InputRequiredException"/> from becoming an OData 500 so in-process
    /// <c>RequireForm</c> can return Multi Round-Trip Requests on <c>tools/call</c>.
    /// </summary>
    public class McpInputRequiredExceptionFilter : IExceptionFilter
    {

        #region Public Methods

        /// <inheritdoc />
        public void OnException(ExceptionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Exception is InputRequiredException)
            {
                context.ExceptionHandled = false;
            }
        }

        #endregion

    }

}
