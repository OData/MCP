# OData MCP Performance Benchmarks

This project contains performance benchmarks for critical components of the OData MCP system.

## Running Benchmarks

```bash
cd benchmarks/Microsoft.OData.Mcp.Benchmarks
dotnet run -c Release
```

## Benchmark Categories

### Route Parsing Benchmarks
Compares different approaches to parsing MCP routes:
- **SpanRouteParser** (our implementation) - Zero-allocation parsing using `ReadOnlySpan<char>`
- Regex parsing - Traditional regex-based approach
- String.Split - Allocation-heavy splitting approach
- IndexOf - Simple string searching

### Tool Caching Benchmarks
Evaluates different caching strategies:
- **StartupToolCache** (our implementation) - Frozen dictionary-based caching
- FrozenDictionary - Direct frozen dictionary usage
- IMemoryCache - ASP.NET Core memory cache
- ConcurrentDictionary - Thread-safe dictionary
- Dictionary - Simple dictionary (not thread-safe)

### Route Matching Benchmarks
Tests route matching performance:
- **McpRouteMatcher** (our implementation) - Optimized route matching
- Dictionary lookup - Simple dictionary-based matching
- Regex matching - Pattern-based matching
- Linear search - Brute force approach

## Expected Results

Based on our zero-allocation design:

1. **Route Parsing**: SpanRouteParser should be 5-10x faster than regex and allocate zero heap memory
2. **Tool Caching**: StartupToolCache with FrozenDictionary should provide O(1) lookups with minimal overhead
3. **Route Matching**: McpRouteMatcher should outperform regex by 3-5x for typical workloads

## Interpreting Results

Look for:
- **Mean** - Average execution time (lower is better)
- **Allocated** - Heap allocations (zero is ideal for hot paths)
- **Gen 0/1/2** - Garbage collection pressure (lower is better)

## Adding New Benchmarks

1. Create a new class with `[MemoryDiagnoser]` attribute
2. Add `[Benchmark]` methods to compare approaches
3. Use `[GlobalSetup]` for initialization
4. Add to Program.cs to include in benchmark runs