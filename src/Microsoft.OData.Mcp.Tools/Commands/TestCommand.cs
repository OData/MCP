// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.OData.Mcp.Tools.Hosting;

namespace Microsoft.OData.Mcp.Tools.Commands
{

    /// <summary>
    /// Fetches live metadata and prints the declared entity set count.
    /// </summary>
    [Command(Name = "test", Description = "Fetch $metadata from an OData service and print the entity set count.")]
    public class TestCommand
    {

        #region Properties

        /// <summary>
        /// Gets or sets the OData service URL.
        /// </summary>
        [Argument(0, "OData service URL")]
        [Required]
        public string? Url { get; set; }

        /// <summary>
        /// Gets or sets the authentication token.
        /// </summary>
        [Option("-t|--auth-token", Description = "Authentication token for the OData service")]
        public string? AuthToken { get; set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Executes the test command.
        /// </summary>
        /// <returns>
        /// Exit code 0 when metadata parses.
        /// </returns>
        public async Task<int> OnExecuteAsync()
        {
            if (string.IsNullOrWhiteSpace(Url))
            {
                Console.Error.WriteLine("Error: OData service URL is required.");

                return 1;
            }

            try
            {
                var host = await ToolsMcpHost.CreateAsync(Url, AuthToken, CancellationToken.None).ConfigureAwait(false);
                var count = host.Catalog.CompleteEntitySetNames(string.Empty).Count;
                Console.Error.WriteLine($"Entity sets: {count}");

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");

                return 1;
            }
        }

        #endregion

    }

}
