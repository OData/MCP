// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using BenchmarkDotNet.Attributes;
using Microsoft.OData.Mcp.Core.Routing;
using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace Microsoft.OData.Mcp.Benchmarks
{
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 3, iterationCount: 10)]
    public class RouteMatchingBenchmarks
    {
        private McpRouteMatcher _routeMatcher = null!;
        private Dictionary<string, McpRouteEntry> _dictionaryMatcher = null!;
        private List<(Regex pattern, McpRouteEntry entry)> _regexMatchers = null!;
        
        private readonly string[] _testPaths = new[]
        {
            "/api/v1/mcp/info",
            "/api/v2/mcp/tools",
            "/odata/mcp/tools/Customer.query",
            "/service/data/mcp/tools/execute",
            "/api/v1/Customers", // Should not match
            "/unknown/mcp/info", // Should not match registered routes
        };

        [GlobalSetup]
        public void Setup()
        {
            var routes = new[]
            {
                new McpRouteEntry { RouteName = "v1", ODataRoutePrefix = "api/v1", McpBasePath = "/api/v1/mcp" },
                new McpRouteEntry { RouteName = "v2", ODataRoutePrefix = "api/v2", McpBasePath = "/api/v2/mcp" },
                new McpRouteEntry { RouteName = "main", ODataRoutePrefix = "odata", McpBasePath = "/odata/mcp" },
                new McpRouteEntry { RouteName = "service", ODataRoutePrefix = "service/data", McpBasePath = "/service/data/mcp" }
            };

            // Setup McpRouteMatcher
            var registry = new McpEndpointRegistry();
            foreach (var route in routes)
            {
                registry.RegisterEndpoint(route);
            }
            _routeMatcher = new McpRouteMatcher(registry.GetAllEndpoints().ToFrozenDictionary(r => r.ODataRoutePrefix));

            // Setup dictionary matcher
            _dictionaryMatcher = new Dictionary<string, McpRouteEntry>();
            foreach (var route in routes)
            {
                _dictionaryMatcher[route.McpBasePath] = route;
            }

            // Setup regex matchers
            _regexMatchers = new List<(Regex, McpRouteEntry)>();
            foreach (var route in routes)
            {
                var pattern = new Regex($"^{Regex.Escape(route.McpBasePath)}(/.*)?$", RegexOptions.Compiled);
                _regexMatchers.Add((pattern, route));
            }
        }

        [Benchmark(Baseline = true)]
        public int MatchWithMcpRouteMatcher()
        {
            var matched = 0;
            foreach (var path in _testPaths)
            {
                if (_routeMatcher.TryMatchRoute(path, out _, out _))
                {
                    matched++;
                }
            }
            return matched;
        }

        [Benchmark]
        public int MatchWithDictionary()
        {
            var matched = 0;
            foreach (var path in _testPaths)
            {
                // Try to find the MCP base path
                var mcpIndex = path.IndexOf("/mcp", StringComparison.Ordinal);
                if (mcpIndex > 0)
                {
                    var basePath = path.Substring(0, mcpIndex + 4);
                    if (_dictionaryMatcher.TryGetValue(basePath, out _))
                    {
                        matched++;
                    }
                }
            }
            return matched;
        }

        [Benchmark]
        public int MatchWithRegex()
        {
            var matched = 0;
            foreach (var path in _testPaths)
            {
                foreach (var (pattern, entry) in _regexMatchers)
                {
                    if (pattern.IsMatch(path))
                    {
                        matched++;
                        break;
                    }
                }
            }
            return matched;
        }

        [Benchmark]
        public int MatchWithLinearSearch()
        {
            var matched = 0;
            var routes = _dictionaryMatcher.Values.ToArray();
            
            foreach (var path in _testPaths)
            {
                foreach (var route in routes)
                {
                    if (path.StartsWith(route.McpBasePath, StringComparison.Ordinal))
                    {
                        matched++;
                        break;
                    }
                }
            }
            return matched;
        }

        [Benchmark]
        public void BulkMatchOperations()
        {
            // Simulate high-throughput matching scenario
            for (int i = 0; i < 1000; i++)
            {
                var path = $"/api/v{i % 2 + 1}/mcp/tools/Entity{i}.query";
                _routeMatcher.TryMatchRoute(path, out _, out _);
            }
        }
    }
}