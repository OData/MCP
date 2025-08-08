using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.OData.Mcp.Core.Tools;
using System.Collections.Concurrent;
using System.Collections.Frozen;

namespace Microsoft.OData.Mcp.Benchmarks
{
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 3, iterationCount: 10)]
    public class ToolCachingBenchmarks
    {
        private StartupToolCache _startupCache = null!;
        private IMemoryCache _memoryCache = null!;
        private ConcurrentDictionary<string, McpTool> _concurrentCache = null!;
        private Dictionary<string, McpTool> _dictionaryCache = null!;
        private FrozenDictionary<string, McpTool> _frozenCache = null!;
        
        private readonly string[] _toolNames = new string[100];
        private readonly McpTool[] _tools = new McpTool[100];

        [GlobalSetup]
        public void Setup()
        {
            // Create test tools
            for (int i = 0; i < 100; i++)
            {
                _toolNames[i] = $"Route{i % 10}.Entity{i % 20}.query";
                _tools[i] = new McpTool
                {
                    Name = _toolNames[i],
                    Description = $"Query Entity{i % 20} from Route{i % 10}",
                    Parameters = new Dictionary<string, McpToolParameter>
                    {
                        ["filter"] = new() { Type = "string", Description = "OData filter expression" },
                        ["orderby"] = new() { Type = "string", Description = "OData orderby expression" },
                        ["top"] = new() { Type = "integer", Description = "Number of items to return" }
                    }
                };
            }

            // Setup caches
            var services = new ServiceCollection();
            services.AddMemoryCache();
            services.AddSingleton<IOptions<McpToolGenerationOptions>>(Options.Create(new McpToolGenerationOptions()));
            services.AddSingleton<StartupToolCache>();
            
            var provider = services.BuildServiceProvider();
            _startupCache = provider.GetRequiredService<StartupToolCache>();
            _memoryCache = provider.GetRequiredService<IMemoryCache>();
            
            _concurrentCache = new ConcurrentDictionary<string, McpTool>();
            _dictionaryCache = new Dictionary<string, McpTool>();
            
            // Initialize caches
            foreach (var tool in _tools)
            {
                _startupCache.CacheTool("test", tool);
                _memoryCache.Set($"mcp:tool:{tool.Name}", tool);
                _concurrentCache[tool.Name] = tool;
                _dictionaryCache[tool.Name] = tool;
            }
            
            _frozenCache = _dictionaryCache.ToFrozenDictionary();
        }

        [Benchmark(Baseline = true)]
        public int StartupCacheLookup()
        {
            var found = 0;
            foreach (var name in _toolNames)
            {
                if (_startupCache.TryGetTool("test", name, out _))
                    found++;
            }
            return found;
        }

        [Benchmark]
        public int FrozenDictionaryLookup()
        {
            var found = 0;
            foreach (var name in _toolNames)
            {
                if (_frozenCache.TryGetValue(name, out _))
                    found++;
            }
            return found;
        }

        [Benchmark]
        public int MemoryCacheLookup()
        {
            var found = 0;
            foreach (var name in _toolNames)
            {
                if (_memoryCache.TryGetValue($"mcp:tool:{name}", out McpTool? _))
                    found++;
            }
            return found;
        }

        [Benchmark]
        public int ConcurrentDictionaryLookup()
        {
            var found = 0;
            foreach (var name in _toolNames)
            {
                if (_concurrentCache.TryGetValue(name, out _))
                    found++;
            }
            return found;
        }

        [Benchmark]
        public int DictionaryLookup()
        {
            var found = 0;
            foreach (var name in _toolNames)
            {
                if (_dictionaryCache.TryGetValue(name, out _))
                    found++;
            }
            return found;
        }

        [Benchmark]
        public void BulkCacheTools()
        {
            var cache = new StartupToolCache(_memoryCache, Options.Create(new McpToolGenerationOptions()));
            
            foreach (var tool in _tools)
            {
                cache.CacheTool("bulk", tool);
            }
        }

        [Benchmark]
        public McpTool[] GetAllToolsFromStartupCache()
        {
            return _startupCache.GetAllTools("test").ToArray();
        }

        [Benchmark]
        public McpTool[] GetAllToolsFromFrozenDictionary()
        {
            return _frozenCache.Values.ToArray();
        }
    }
}