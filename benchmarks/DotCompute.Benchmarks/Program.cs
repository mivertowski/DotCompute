using BenchmarkDotNet.Running;
using DotCompute.Benchmarks;

// Run all benchmarks
var summary = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
    .Run(args);

// Alternative: Run specific benchmark
// var summary = BenchmarkRunner.Run<MemoryAllocationBenchmarks>();

Console.WriteLine("Benchmarks completed. Press any key to exit...");
Console.ReadKey();