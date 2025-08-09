using System;

namespace Microsoft.OData.Mcp.Core.Configuration
{
    /// <summary>
    /// Build information for the MCP server.
    /// </summary>
    public sealed class BuildInfo
    {
        /// <summary>
        /// Gets or sets the build number or identifier.
        /// </summary>
        /// <value>The build number or identifier from the CI/CD system.</value>
        public string? BuildNumber { get; set; }

        /// <summary>
        /// Gets or sets the commit hash of the source code.
        /// </summary>
        /// <value>The Git commit hash or similar version control identifier.</value>
        public string? CommitHash { get; set; }

        /// <summary>
        /// Gets or sets the branch name from which the build was created.
        /// </summary>
        /// <value>The source control branch name.</value>
        public string? Branch { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the build was created.
        /// </summary>
        /// <value>The UTC timestamp of the build.</value>
        public DateTime? BuildTimestamp { get; set; }

        /// <summary>
        /// Gets or sets the build configuration (e.g., Debug, Release).
        /// </summary>
        /// <value>The build configuration used to compile the server.</value>
        public string? Configuration { get; set; }

        /// <summary>
        /// Gets or sets the target framework for which the server was built.
        /// </summary>
        /// <value>The .NET target framework (e.g., "net8.0", "net9.0").</value>
        public string? TargetFramework { get; set; }

        /// <summary>
        /// Creates a copy of this build information.
        /// </summary>
        /// <returns>A new instance with the same values.</returns>
        public BuildInfo Clone()
        {
            return new BuildInfo
            {
                BuildNumber = BuildNumber,
                CommitHash = CommitHash,
                Branch = Branch,
                BuildTimestamp = BuildTimestamp,
                Configuration = Configuration,
                TargetFramework = TargetFramework
            };
        }
    }
}
