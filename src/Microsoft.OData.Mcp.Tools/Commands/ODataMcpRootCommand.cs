using McMaster.Extensions.CommandLineUtils;
using System;

namespace Microsoft.OData.Mcp.Tools.Commands
{

    /// <summary>
    /// Root command for the OData MCP CLI tool.
    /// </summary>
    [Command(Name = "odata-mcp", Description = "OData MCP Server - Turn any OData API into an MCP service for AI assistants")]
    [Subcommand(typeof(StartCommand))]
    [Subcommand(typeof(AddCommand))]
    [HelpOption("-?|-h|--help")]
    public class ODataMcpRootCommand
    {

        /// <summary>
        /// Executes when the root command is invoked without subcommands.
        /// </summary>
        /// <param name="app">The command line application instance.</param>
        /// <returns>Exit code 0 for success.</returns>
        public int OnExecute(CommandLineApplication app)
        {
            ArgumentNullException.ThrowIfNull(app);

            app.ShowHelp();
            return 0;
        }

    }

}
