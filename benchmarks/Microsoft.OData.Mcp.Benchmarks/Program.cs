using BenchmarkDotNet.Running;
using Microsoft.OData.Mcp.Benchmarks;

var summary = BenchmarkRunner.Run<RouteParsingBenchmarks>();
Console.WriteLine("Route parsing benchmarks completed.");

summary = BenchmarkRunner.Run<ToolCachingBenchmarks>();
Console.WriteLine("Tool caching benchmarks completed.");

summary = BenchmarkRunner.Run<RouteMatchingBenchmarks>();
Console.WriteLine("Route matching benchmarks completed.");