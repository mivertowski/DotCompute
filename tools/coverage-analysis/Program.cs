using System.Globalization;
using CoverageAnalysis;
using AnalysisResult = CoverageAnalysis.CoverageAnalyzer.CoverageAnalysis;

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
        
        report.AppendLine("# DotCompute Coverage Analysis Report");
        report.AppendLine();
        report.AppendLine("## Overall Coverage");
        report.AppendLine(CultureInfo.InvariantCulture, $"- **Line Coverage**: {analysis.Overall.LineRate:P1} ({analysis.Overall.LinesCovered}/{analysis.Overall.LinesTotal})");
        report.AppendLine(CultureInfo.InvariantCulture, $"- **Branch Coverage**: {analysis.Overall.BranchRate:P1} ({analysis.Overall.BranchesCovered}/{analysis.Overall.BranchesTotal})");
        report.AppendLine();
        
        if (analysis.ByProject.Count > 0)
        {
            report.AppendLine("## Coverage by Project");
            report.AppendLine("| Project | Line Coverage | Branch Coverage |");
            report.AppendLine("|---------|---------------|-----------------|");
            
            foreach (var project in analysis.ByProject.OrderBy(p => p.Key))
            {
                var metrics = project.Value;
                report.AppendLine(CultureInfo.InvariantCulture, $"| {project.Key} | {metrics.LineRate:P1} | {metrics.BranchRate:P1} |");
            }
            report.AppendLine();
        }

        if (analysis.LowCoverageAreas.Count > 0)
        {
            report.AppendLine("## Low Coverage Areas");
            foreach (var area in analysis.LowCoverageAreas)
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"- {area}");
            }
            report.AppendLine();
        }

        if (analysis.Recommendations.Count > 0)
        {
            report.AppendLine("## Recommendations");
            foreach (var recommendation in analysis.Recommendations)
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"- {recommendation}");
            }
        }

        return report.ToString();
    }
}