using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using DotCompute.Benchmarks;
using System.Diagnostics.CodeAnalysis;

internal class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("=== DotCompute Performance Benchmarks ===");
        Console.WriteLine();
        Console.WriteLine("Available benchmark suites:");
        Console.WriteLine("1.  MemoryOperationsBenchmarks - Memory allocation/deallocation performance");
        Console.WriteLine("2.  EnhancedDataTransferBenchmarks - Host-device data transfer throughput");
        Console.WriteLine("3.  EnhancedKernelCompilationBenchmarks - Kernel compilation performance");
        Console.WriteLine("4.  ComputeKernelsBenchmarks - Compute kernel execution performance");
        Console.WriteLine("5.  PipelineExecutionBenchmarks - Pipeline orchestration overhead");
        Console.WriteLine("6.  PluginSystemBenchmarks - Plugin loading and initialization");
        Console.WriteLine("7.  MultiAcceleratorBenchmarks - Multi-accelerator scaling");
        Console.WriteLine("8.  SimdOperationsBenchmarks - SIMD operations performance");
        Console.WriteLine("9.  BackendComparisonBenchmarks - CPU vs GPU backend comparison");
        Console.WriteLine("10. RealWorldAlgorithmsBenchmarks - FFT, convolution, sorting algorithms");
        Console.WriteLine("11. ConcurrentOperationsBenchmarks - Concurrent operations performance");
        Console.WriteLine();

        // Configure BenchmarkDotNet for better results
        var config = ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance))
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);

        if (args.Length == 0)
        {
            Console.WriteLine("Running all benchmark suites...");
            Console.WriteLine("This may take several minutes. Press Ctrl+C to cancel.");
            Console.WriteLine();

            // Run all benchmarks
            var summary = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
                .RunAll(config);
        }
        else if (args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            // List available benchmarks
            ListBenchmarks();
            return;
        }
        else if (args[0].Equals("interactive", StringComparison.OrdinalIgnoreCase))
        {
            // Interactive mode
            Console.WriteLine("Select benchmark suite to run:");
            Console.WriteLine("1 - Memory Operations");
            Console.WriteLine("2 - Data Transfer");
            Console.WriteLine("3 - Kernel Compilation");
            Console.WriteLine("4 - Compute Kernels");
            Console.WriteLine("5 - Pipeline Execution");
            Console.WriteLine("6 - Plugin System");
            Console.WriteLine("7 - Multi-Accelerator");
            Console.WriteLine("8 - SIMD Operations");
            Console.WriteLine("9 - Backend Comparison");
            Console.WriteLine("10 - Real-World Algorithms");
            Console.WriteLine("11 - Concurrent Operations");
            Console.WriteLine("A - All benchmarks");

            var choice = Console.ReadLine();

            switch (choice?.ToUpperInvariant())
            {
                case "1":
                    _ = BenchmarkRunner.Run<MemoryOperationsBenchmarks>(config);
                    break;
                case "2":
                    _ = BenchmarkRunner.Run<EnhancedDataTransferBenchmarks>(config);
                    break;
                case "3":
                    _ = BenchmarkRunner.Run<EnhancedKernelCompilationBenchmarks>(config);
                    break;
                case "4":
                    _ = BenchmarkRunner.Run<ComputeKernelsBenchmarks>(config);
                    break;
                case "5":
                    _ = BenchmarkRunner.Run<PipelineExecutionBenchmarks>(config);
                    break;
                case "6":
                    _ = BenchmarkRunner.Run<PluginSystemBenchmarks>(config);
                    break;
                case "7":
                    _ = BenchmarkRunner.Run<MultiAcceleratorBenchmarks>(config);
                    break;
                case "8":
                    _ = BenchmarkRunner.Run<SimdOperationsBenchmarks>(config);
                    break;
                case "9":
                    _ = BenchmarkRunner.Run<BackendComparisonBenchmarks>(config);
                    break;
                case "10":
                    _ = BenchmarkRunner.Run<RealWorldAlgorithmsBenchmarks>(config);
                    break;
                case "11":
                    _ = BenchmarkRunner.Run<ConcurrentOperationsBenchmarks>(config);
                    break;
                case "A":
                    _ = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).RunAll(config);
                    break;
                default:
                    Console.WriteLine("Invalid selection.");
                    return;
            }
        }
        else
        {
            // Run specific benchmark by class name
            var benchmarkType = args[0];
            var summary = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
                .Run([benchmarkType], config);
        }

        Console.WriteLine();
        Console.WriteLine("Benchmarks completed. Results are available in the BenchmarkDotNet.Artifacts folder.");
        Console.WriteLine("Press any key to exit...");
        _ = Console.ReadKey();

        [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "BenchmarkDotNet requires reflection for benchmark discovery")]
        static void ListBenchmarks()
        {
            var types = typeof(Program).Assembly.GetTypes()
                .Where(t => t.Name.EndsWith("Benchmarks", StringComparison.Ordinal) && !t.IsAbstract)
                .OrderBy(t => t.Name);

            Console.WriteLine("Available benchmark classes:");
            foreach (var type in types)
            {
                Console.WriteLine($"  {type.Name}");
            }
        }
    }
}
