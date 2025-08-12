using System.CommandLine;
using DotCompute.Tools.coverage-analysis;

var rootCommand = new RootCommand("DotCompute Coverage Analysis Tool");

var directoryOption = new Option<DirectoryInfo>("--directory", "Directory containing coverage files");
directoryOption.AddAlias("-d");
directoryOption.IsRequired = true;

var outputOption = new Option<FileInfo>("--output", () => new FileInfo("coverage-report.md"), "Output file path for the report");
outputOption.AddAlias("-o");

var jsonOutputOption = new Option<FileInfo>("--json", () => new FileInfo("coverage-report.json"), "JSON output file path for the report");
jsonOutputOption.AddAlias("-j");

var lineThresholdOption = new Option<double>("--line-threshold", () => 80.0, "Line coverage threshold percentage");
lineThresholdOption.AddAlias("-l");

var branchThresholdOption = new Option<double>("--branch-threshold", () => 70.0, "Branch coverage threshold percentage");
branchThresholdOption.AddAlias("-b");

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
        Console.Error.WriteLine($"Directory does not exist: {directory.FullName}");
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
        await analyzer.GenerateReportAsync(analysis, output.FullName);
        await analyzer.GenerateJsonReportAsync(analysis, jsonOutput.FullName);
        
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
        Console.Error.WriteLine($"Error during coverage analysis: {ex.Message}");
        Environment.Exit(1);
    }
}, directoryOption, outputOption, jsonOutputOption, lineThresholdOption, branchThresholdOption);

return await rootCommand.InvokeAsync(args);