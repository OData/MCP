// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Microsoft.OData.Mcp.Core.Routing;
using System.Text.RegularExpressions;

namespace Microsoft.OData.Mcp.Benchmarks
{
    [MemoryDiagnoser]
    [SimpleJob(RunStrategy.ColdStart, launchCount: 1, warmupCount: 5, iterationCount: 100)]
    public class RouteParsingBenchmarks
    {
        private static readonly Regex McpRouteRegex = new(@"^/(.+)/mcp/(.+)$", RegexOptions.Compiled);
        
        private readonly string[] _testPaths = new[]
        {
            "/api/v1/mcp/info",
            "/api/v1/mcp/tools",
            "/api/v1/mcp/tools/Customer.query",
            "/api/v1/mcp/tools/execute",
            "/odata/mcp/info",
            "/very/long/nested/path/to/odata/service/mcp/tools/execute",
            "/api/v2/$metadata", // Non-MCP path
            "/api/v1/Customers", // Non-MCP path
        };

        [Benchmark(Baseline = true)]
        public int ParseWithSpan()
        {
            var successCount = 0;
            
            foreach (var path in _testPaths)
            {
                var parser = new SpanRouteParser(path);
                if (parser.TryParseMcpRoute(out _, out _))
                {
                    successCount++;
                }
            }
            
            return successCount;
        }

        [Benchmark]
        public int ParseWithRegex()
        {
            var successCount = 0;
            
            foreach (var path in _testPaths)
            {
                var match = McpRouteRegex.Match(path);
                if (match.Success)
                {
                    var odataRoute = match.Groups[1].Value;
                    var mcpCommand = match.Groups[2].Value;
                    successCount++;
                }
            }
            
            return successCount;
        }

        [Benchmark]
        public int ParseWithStringSplit()
        {
            var successCount = 0;
            
            foreach (var path in _testPaths)
            {
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                
                var mcpIndex = -1;
                for (int i = 0; i < segments.Length; i++)
                {
                    if (segments[i] == "mcp")
                    {
                        mcpIndex = i;
                        break;
                    }
                }
                
                if (mcpIndex > 0 && mcpIndex < segments.Length - 1)
                {
                    var odataRoute = string.Join('/', segments.Take(mcpIndex));
                    var mcpCommand = string.Join('/', segments.Skip(mcpIndex + 1));
                    successCount++;
                }
            }
            
            return successCount;
        }

        [Benchmark]
        public int ParseWithIndexOf()
        {
            var successCount = 0;
            
            foreach (var path in _testPaths)
            {
                var mcpIndex = path.IndexOf("/mcp/", StringComparison.Ordinal);
                if (mcpIndex > 0)
                {
                    var odataRoute = path.Substring(1, mcpIndex - 1);
                    var mcpCommand = path.Substring(mcpIndex + 5);
                    successCount++;
                }
            }
            
            return successCount;
        }
    }
}