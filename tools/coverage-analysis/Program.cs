using System.CommandLine;
using DotCompute.Tools.CoverageAnalysis;

internal sealed class Program
{
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("JSON serialization may require types that cannot be statically analyzed.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("JSON serialization may require runtime code generation.")]
    private static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("DotCompute Coverage Analysis Tool");

        var directoryOption = new Option<DirectoryInfo>(
            ["--directory", "-d"],
            "Directory containing coverage files")
        {
            IsRequired = true
        };

        var outputOption = new Option<FileInfo>(
            ["--output", "-o"],
            () => new FileInfo("coverage-report.md"),
            "Output file path for the report");

        var jsonOutputOption = new Option<FileInfo>(
            ["--json", "-j"],
            () => new FileInfo("coverage-report.json"),
            "JSON output file path for the report");

        var lineThresholdOption = new Option<double>(
            ["--line-threshold", "-l"],
            () => 80.0,
            "Line coverage threshold percentage");

        var branchThresholdOption = new Option<double>(
            ["--branch-threshold", "-b"],
            () => 70.0,
            "Branch coverage threshold percentage");

        rootCommand.Add(directoryOption);
        rootCommand.Add(outputOption);
        rootCommand.Add(jsonOutputOption);
        rootCommand.Add(lineThresholdOption);
        rootCommand.Add(branchThresholdOption);

        rootCommand.SetHandler(async (directory, output, jsonOutput, lineThreshold, branchThreshold) =>
        {
            Console.WriteLine("DotCompute Coverage Analysis Tool");
            Console.WriteLine("=================================");
            Console.WriteLine($"Analyzing coverage in: {directory.FullName}");

            if (!directory.Exists)
            {
                await Console.Error.WriteLineAsync($"Directory does not exist: {directory.FullName}");
                Environment.Exit(1);
            }

            var analyzer = new CoverageAnalyzer(directory.FullName, lineThreshold, branchThreshold);

            try
            {
                var analysis = await analyzer.AnalyzeCoverageAsync();

                // Display summary
                Console.WriteLine();
                Console.WriteLine("Coverage Summary:");
                Console.WriteLine($"  Overall Line Coverage: {analysis.Overall.LineRate:P2}");
                Console.WriteLine($"  Overall Branch Coverage: {analysis.Overall.BranchRate:P2}");
                Console.WriteLine($"  Projects Analyzed: {analysis.ByProject.Count}");
                Console.WriteLine($"  Low Coverage Areas: {analysis.LowCoverageAreas.Count}");
                Console.WriteLine();

                // Generate reports
                await CoverageAnalyzer.GenerateReportAsync(analysis, output.FullName);
                await CoverageAnalyzer.GenerateJsonReportAsync(analysis, jsonOutput.FullName);

                Console.WriteLine("Coverage analysis completed successfully!");

                // Set exit code based on coverage
                if (analysis.Overall.LineRate * 100 < lineThreshold ||
                    analysis.Overall.BranchRate * 100 < branchThreshold)
                {
                    Console.WriteLine("Warning: Coverage below thresholds");
                    Environment.Exit(2);
                }
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error during coverage analysis: {ex.Message}");
                Environment.Exit(1);
            }
        }, directoryOption, outputOption, jsonOutputOption, lineThresholdOption, branchThresholdOption);

        return await rootCommand.InvokeAsync(args);
    }
}
