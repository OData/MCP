// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Microsoft.OData.Mcp.AspNetCore.Hosting
{

    /// <summary>
    /// Captures the application <c>RequestDelegate</c> when MCP is on so tool calls can invoke
    /// routing in-process instead of looping back through the server.
    /// </summary>
    /// <remarks>
    /// Registered by <c>AddODataMcp</c> because <see cref="IStartupFilter"/> must be in DI before
    /// <c>Build()</c>. Capture always runs so it can wrap the app's <c>Configure</c> (including
    /// <c>UseODataMcp</c>). Execution still requires <see cref="ODataMcpPipeline.IsEnabled"/>.
    /// </remarks>
    public sealed class ODataMcpPipelineStartupFilter : IStartupFilter
    {

        #region Fields

        /// <summary>
        /// Host-lifetime pipeline capture.
        /// </summary>
        internal readonly ODataMcpPipeline _pipeline;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataMcpPipelineStartupFilter"/> class.
        /// </summary>
        /// <param name="pipeline">Host-lifetime pipeline capture.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pipeline"/> is <see langword="null"/>.</exception>
        public ODataMcpPipelineStartupFilter(ODataMcpPipeline pipeline)
        {
            ArgumentNullException.ThrowIfNull(pipeline);

            _pipeline = pipeline;
        }

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            ArgumentNullException.ThrowIfNull(next);

            return app =>
            {
                app.Use(following =>
                {
                    _pipeline.Pipeline = following;

                    return following;
                });

                next(app);
            };
        }

        #endregion

    }

}
