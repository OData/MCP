// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Running;
using Microsoft.OData.Mcp.Benchmarks.Infrastructure;
using Microsoft.OData.Mcp.Benchmarks.Reports;

namespace Microsoft.OData.Mcp.Benchmarks
{

    /// <summary>
    /// Entry point. Two modes: BenchmarkDotNet compute suites (default) and the token reports (<c>--tokens</c>).
    /// </summary>
    /// <example>
    /// <code>
    /// dotnet run -c Release -- --filter *Describe*
    /// dotnet run -c Release -- --tokens
    /// dotnet run -c Release -- --tokens --out C:\reports
    /// </code>
    /// </example>
    public static class Program
    {

        #region Public Methods

        /// <summary>
        /// Runs the benchmarks or writes the token reports.
        /// </summary>
        /// <param name="args">Command line. <c>--tokens</c> selects the report mode; everything else goes to BenchmarkDotNet.</param>
        /// <returns>
        /// Zero on success.
        /// </returns>
        public static async Task<int> Main(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);

            if (args.Contains("--tokens", StringComparer.OrdinalIgnoreCase))
            {
                var output = RepositoryPaths.ReportsDirectory(args);
                Console.WriteLine($"Writing token reports to {output}");
                await TokenReport.WriteAsync(output).ConfigureAwait(false);
                await FormatComparisonReport.WriteAsync(output).ConfigureAwait(false);
                Console.WriteLine("Done.");

                return 0;
            }

            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

            return 0;
        }

        #endregion

    }

}
