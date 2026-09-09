// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.IO;

namespace Microsoft.OData.Mcp.Benchmarks.Infrastructure
{

    /// <summary>
    /// Locates the project directory and the linked baselines from wherever the binary runs.
    /// </summary>
    public static class RepositoryPaths
    {

        #region Fields

        /// <summary>
        /// The project file name, used to find the project directory by walking up from the binary.
        /// </summary>
        public const string ProjectFileName = "Microsoft.OData.Mcp.Benchmarks.csproj";

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the folder that holds the linked <c>Before/</c> and <c>Current/</c> baselines next to the binary.
        /// </summary>
        /// <returns>
        /// <c>{bin}/Baselines/TypeShapes</c>.
        /// </returns>
        /// <exception cref="DirectoryNotFoundException">Thrown when the build did not copy the linked baselines.</exception>
        public static string BaselinesDirectory()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Baselines", "TypeShapes");
            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException($"Linked baselines not found at {path}. Build the project so the Content links are copied.");
            }

            return path;
        }

        /// <summary>
        /// Finds the project directory by walking up from the binary until <see cref="ProjectFileName"/> appears.
        /// </summary>
        /// <returns>
        /// The directory that contains the project file.
        /// </returns>
        /// <exception cref="DirectoryNotFoundException">Thrown when no ancestor holds the project file.</exception>
        public static string ProjectDirectory()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, ProjectFileName)))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException($"Could not find {ProjectFileName} above {AppContext.BaseDirectory}. Pass --out <directory>.");
        }

        /// <summary>
        /// Resolves where reports are written: <c>--out &lt;dir&gt;</c> when given, otherwise <c>Reports/</c> under the project.
        /// </summary>
        /// <param name="args">The command line.</param>
        /// <returns>
        /// An existing directory.
        /// </returns>
        public static string ReportsDirectory(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);

            var output = Path.Combine(ProjectDirectoryOrNull() ?? Directory.GetCurrentDirectory(), "Reports");
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], "--out", StringComparison.OrdinalIgnoreCase))
                {
                    output = Path.GetFullPath(args[index + 1]);
                }
            }

            Directory.CreateDirectory(output);

            return output;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// <see cref="ProjectDirectory"/> without the exception.
        /// </summary>
        /// <returns>
        /// The project directory, or <c>null</c> when the binary runs outside the repository.
        /// </returns>
        internal static string? ProjectDirectoryOrNull()
        {
            try
            {
                return ProjectDirectory();
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
        }

        #endregion

    }

}
