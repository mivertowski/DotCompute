using System.Globalization;
using DotCompute.Tools.Coverage;
using AnalysisResult = DotCompute.Tools.Coverage.CoverageAnalyzer.CoverageAnalysis;

internal sealed class Program
{
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("JSON serialization may require types that cannot be statically analyzed.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("JSON serialization may require runtime code generation.")]
    private static async Task<int> Main(string[] args)
    {
        var directoryPath = args.Length > 0 ? args[0] : Environment.CurrentDirectory;
        var outputPath = args.Length > 1 ? args[1] : "coverage-report.md";
        var jsonOutputPath = args.Length > 2 ? args[2] : "coverage-report.json";
        var lineThreshold = args.Length > 3 && double.TryParse(args[3], out var lt) ? lt : 80.0;
        var branchThreshold = args.Length > 4 && double.TryParse(args[4], out var bt) ? bt : 70.0;

        Console.WriteLine("DotCompute Coverage Analysis Tool");
        Console.WriteLine("=================================");
        Console.WriteLine($"Analyzing coverage in: {directoryPath}");

        var directory = new DirectoryInfo(directoryPath);
        if (!directory.Exists)
        {
            await Console.Error.WriteLineAsync($"Directory does not exist: {directory.FullName}");
            return 1;
        }

        try
        {
            var analyzer = new CoverageAnalyzer(directory.FullName, lineThreshold, branchThreshold);
            var analysis = await analyzer.AnalyzeCoverageAsync();

            // Generate markdown report
            var output = new FileInfo(outputPath);
            var markdownReport = GenerateMarkdownReport(analysis);
            await File.WriteAllTextAsync(output.FullName, markdownReport);
            Console.WriteLine($"Markdown report written to: {output.FullName}");

            // Generate JSON report
            var jsonOutput = new FileInfo(jsonOutputPath);
            var jsonReport = System.Text.Json.JsonSerializer.Serialize(analysis, CoverageAnalyzer.JsonOptions);
            await File.WriteAllTextAsync(jsonOutput.FullName, jsonReport);
            Console.WriteLine($"JSON report written to: {jsonOutput.FullName}");

            // Print summary to console
            Console.WriteLine($"Overall Coverage: {analysis.Overall.LineRate:P1} lines, {analysis.Overall.BranchRate:P1} branches");

            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error during analysis: {ex.Message}");
            return 1;
        }
    }

    private static string GenerateMarkdownReport(AnalysisResult analysis)
    {
        var report = new System.Text.StringBuilder();

        _ = report.AppendLine("# DotCompute Coverage Analysis Report");
        _ = report.AppendLine();
        _ = report.AppendLine("## Overall Coverage");
        _ = report.AppendLine(CultureInfo.InvariantCulture, $"- **Line Coverage**: {analysis.Overall.LineRate:P1} ({analysis.Overall.LinesCovered}/{analysis.Overall.LinesTotal})");
        _ = report.AppendLine(CultureInfo.InvariantCulture, $"- **Branch Coverage**: {analysis.Overall.BranchRate:P1} ({analysis.Overall.BranchesCovered}/{analysis.Overall.BranchesTotal})");
        _ = report.AppendLine();
        
        if (analysis.ByProject.Count > 0)
        {
            _ = report.AppendLine("## Coverage by Project");
            _ = report.AppendLine("| Project | Line Coverage | Branch Coverage |");
            _ = report.AppendLine("|---------|---------------|-----------------|");
            
            foreach (var project in analysis.ByProject.OrderBy(p => p.Key))
            {
                var metrics = project.Value;
                _ = report.AppendLine(CultureInfo.InvariantCulture, $"| {project.Key} | {metrics.LineRate:P1} | {metrics.BranchRate:P1} |");
            }
            _ = report.AppendLine();
        }

        if (analysis.LowCoverageAreas.Count > 0)
        {
            _ = report.AppendLine("## Low Coverage Areas");
            foreach (var area in analysis.LowCoverageAreas)
            {
                _ = report.AppendLine(CultureInfo.InvariantCulture, $"- {area}");
            }
            _ = report.AppendLine();
        }

        if (analysis.Recommendations.Count > 0)
        {
            _ = report.AppendLine("## Recommendations");
            foreach (var recommendation in analysis.Recommendations)
            {
                _ = report.AppendLine(CultureInfo.InvariantCulture, $"- {recommendation}");
            }
        }

        return report.ToString();
    }
}