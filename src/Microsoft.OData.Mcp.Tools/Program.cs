using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.OData.Mcp.Tools.Commands;

namespace Microsoft.OData.Mcp.Tools
{

    /// <summary>
    /// Main entry point for the OData MCP Tools CLI.
    /// </summary>
    public class Program
    {

        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Exit code.</returns>
        public static Task<int> Main(string[] args)
        {
            var app = new CommandLineApplication<ODataMcpRootCommand>();
            app.Conventions
                .UseDefaultConventions()
                .UseConstructorInjection();

            return app.ExecuteAsync(args);
        }

    }

}
