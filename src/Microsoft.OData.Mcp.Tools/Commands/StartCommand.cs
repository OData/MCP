// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Hosting;
using Microsoft.OData.Mcp.Tools.Hosting;

namespace Microsoft.OData.Mcp.Tools.Commands
{

    /// <summary>
    /// Command to start the OData MCP server over stdio.
    /// </summary>
    [Command(Name = "start", Description = "Start the OData MCP server")]
    public class StartCommand
    {

        #region Properties

        /// <summary>
        /// Gets or sets the authentication token.
        /// </summary>
        [Option("-t|--auth-token", Description = "Authentication token for the OData service")]
        public string? AuthToken { get; set; }

        /// <summary>
        /// Gets or sets the OData service URL.
        /// </summary>
        [Argument(0, "OData service URL")]
        [Required]
        public string? Url { get; set; }

        /// <summary>
        /// Gets or sets whether to enable verbose logging.
        /// </summary>
        [Option("-v|--verbose", Description = "Enable verbose logging")]
        public bool Verbose { get; set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Executes the start command.
        /// </summary>
        /// <returns>
        /// Exit code.
        /// </returns>
        public async Task<int> OnExecuteAsync()
        {
            if (string.IsNullOrWhiteSpace(Url))
            {
                Console.Error.WriteLine("Error: OData service URL is required.");

                return 1;
            }

            using var lifetime = new CancellationTokenSource();
            Console.CancelKeyPress += (_, args) =>
            {
                args.Cancel = true;
                lifetime.Cancel();
            };

            return await ExecuteAsync(lifetime).ConfigureAwait(false);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Fetches metadata and builds the stdio host without running it.
        /// </summary>
        /// <param name="lifetime">The lifetime cancelled by <c>shutdown_server</c> or Ctrl+C.</param>
        /// <returns>
        /// The built host.
        /// </returns>
        internal async Task<IHost> BuildHostAsync(CancellationTokenSource lifetime)
        {
            ArgumentNullException.ThrowIfNull(lifetime);
            ArgumentException.ThrowIfNullOrWhiteSpace(Url);

            var toolsHost = await ToolsMcpHost.CreateAsync(Url, AuthToken, lifetime.Token).ConfigureAwait(false);
            Console.Error.WriteLine($"Registered {toolsHost.Catalog.Tools.Count} OData tools plus shutdown_server.");

            return toolsHost.BuildStdioHost(lifetime, Verbose, AuthToken);
        }

        /// <summary>
        /// Builds and runs the stdio host, mapping expected failures to exit codes.
        /// </summary>
        /// <param name="lifetime">The host lifetime.</param>
        /// <returns>
        /// Exit code 0 on shutdown or cancel; 1 on unexpected errors.
        /// </returns>
        internal async Task<int> ExecuteAsync(CancellationTokenSource lifetime)
        {
            ArgumentNullException.ThrowIfNull(lifetime);

            try
            {
                using var host = await BuildHostAsync(lifetime).ConfigureAwait(false);

                return await RunHostAsync(host, lifetime.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                if (Verbose)
                {
                    Console.Error.WriteLine(ex.ToString());
                }

                return 1;
            }
        }

        /// <summary>
        /// Runs a built host until it stops or <paramref name="cancellationToken"/> fires.
        /// </summary>
        /// <param name="host">The generic host.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// Exit code 0.
        /// </returns>
        internal static async Task<int> RunHostAsync(IHost host, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(host);

            try
            {
                await host.RunAsync(cancellationToken).ConfigureAwait(false);

                return 0;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
        }

        #endregion

    }

}
