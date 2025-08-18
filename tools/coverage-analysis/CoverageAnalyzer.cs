using System.Xml.Linq;
using System.Text.Json;
using System.Text;
using System.Globalization;

namespace CoverageAnalysis;

/// <summary>
/// Analyzes code coverage reports and generates comprehensive analysis
/// </summary>
internal sealed class CoverageAnalyzer
{
    internal sealed record CoverageMetrics(
        double LineRate,
        double BranchRate,
        int LinesTotal,
        int LinesCovered,
        int BranchesTotal,
        int BranchesCovered,
        string ProjectName);

    internal sealed record CoverageAnalysis(
        CoverageMetrics Overall,
        Dictionary<string, CoverageMetrics> ByProject,
        List<string> LowCoverageAreas,
        List<string> UncoveredMethods,
        List<string> Recommendations);

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _coverageDirectory;
    private readonly double _lineThreshold;
    private readonly double _branchThreshold;

    public CoverageAnalyzer(string coverageDirectory, double lineThreshold = 80.0, double branchThreshold = 70.0)
    {
        _coverageDirectory = coverageDirectory;
        _lineThreshold = lineThreshold;
        _branchThreshold = branchThreshold;
    }

    public async Task<CoverageAnalysis> AnalyzeCoverageAsync()
    {
        var coverageFiles = Directory.GetFiles(_coverageDirectory, "coverage.cobertura.xml", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/bin/", StringComparison.OrdinalIgnoreCase) && !f.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (coverageFiles.Count == 0)
        {
            Console.WriteLine($"No coverage files found in {_coverageDirectory}");
            return new CoverageAnalysis(
                new CoverageMetrics(0, 0, 0, 0, 0, 0, "No Data"),
                [],
                ["No coverage data found"],
                [],
                ["Run tests with coverage collection enabled"]);
        }

        var projectMetrics = new Dictionary<string, CoverageMetrics>();
        var lowCoverageAreas = new List<string>();
        var uncoveredMethods = new List<string>();

        int totalLines = 0, totalLinesCovered = 0;
        int totalBranches = 0, totalBranchesCovered = 0;

        foreach (var file in coverageFiles)
        {
            try
            {
                var metrics = await AnalyzeCoverageFileAsync(file);
                if (metrics != null)
                {
                    projectMetrics[metrics.ProjectName] = metrics;
                    totalLines += metrics.LinesTotal;
                    totalLinesCovered += metrics.LinesCovered;
                    totalBranches += metrics.BranchesTotal;
                    totalBranchesCovered += metrics.BranchesCovered;

                    // Check for low coverage
                    if (metrics.LineRate * 100 < _lineThreshold)
                    {
                        lowCoverageAreas.Add($"{metrics.ProjectName}: {metrics.LineRate:P1} line coverage");
                    }
                    if (metrics.BranchRate * 100 < _branchThreshold)
                    {
                        lowCoverageAreas.Add($"{metrics.ProjectName}: {metrics.BranchRate:P1} branch coverage");
                    }

                    // Find uncovered methods
                    var uncovered = await FindUncoveredMethodsAsync(file);
                    uncoveredMethods.AddRange(uncovered);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error analyzing {file}: {ex.Message}");
            }
        }

        var overallLineRate = totalLines > 0 ? (double)totalLinesCovered / totalLines : 0;
        var overallBranchRate = totalBranches > 0 ? (double)totalBranchesCovered / totalBranches : 0;

        var overall = new CoverageMetrics(
            overallLineRate,
            overallBranchRate,
            totalLines,
            totalLinesCovered,
            totalBranches,
            totalBranchesCovered,
            "Overall");

        var recommendations = GenerateRecommendations(overall, projectMetrics, lowCoverageAreas);

        return new CoverageAnalysis(overall, projectMetrics, lowCoverageAreas, uncoveredMethods, recommendations);
    }

    private static async Task<CoverageMetrics?> AnalyzeCoverageFileAsync(string filePath)
    {
        try
        {
            var doc = await Task.Run(() => XDocument.Load(filePath));
            var coverage = doc.Root;

            if (coverage?.Name.LocalName != "coverage")
            {
                return null;
            }

            var lineRate = double.Parse(coverage.Attribute("line-rate")?.Value ?? "0", CultureInfo.InvariantCulture);
            var branchRate = double.Parse(coverage.Attribute("branch-rate")?.Value ?? "0", CultureInfo.InvariantCulture);
            var linesTotal = int.Parse(coverage.Attribute("lines-valid")?.Value ?? "0", CultureInfo.InvariantCulture);
            var linesCovered = int.Parse(coverage.Attribute("lines-covered")?.Value ?? "0", CultureInfo.InvariantCulture);
            var branchesTotal = int.Parse(coverage.Attribute("branches-valid")?.Value ?? "0", CultureInfo.InvariantCulture);
            var branchesCovered = int.Parse(coverage.Attribute("branches-covered")?.Value ?? "0", CultureInfo.InvariantCulture);

            // Extract project name from file path
            var projectName = ExtractProjectName(filePath);

            return new CoverageMetrics(lineRate, branchRate, linesTotal, linesCovered,
                                     branchesTotal, branchesCovered, projectName);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<List<string>> FindUncoveredMethodsAsync(string filePath)
    {
        var uncovered = new List<string>();

        try
        {
            var doc = await Task.Run(() => XDocument.Load(filePath));
            var methods = doc.Descendants("method")
                .Where(m => double.Parse(m.Attribute("line-rate")?.Value ?? "1", CultureInfo.InvariantCulture) == 0)
                .Select(m => $"{m.Parent?.Parent?.Attribute("name")?.Value}.{m.Attribute("name")?.Value}")
                .Where(name => !string.IsNullOrEmpty(name))
                .Take(20) // Limit to prevent overwhelming output
                .ToList();

            uncovered.AddRange(methods);
        }
        catch
        {
            // Ignore errors in uncovered method detection
        }

        return uncovered;
    }

    private static string ExtractProjectName(string filePath)
    {
        var parts = filePath.Split(Path.DirectorySeparatorChar);

        // Look for TestResults folder and extract project name
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i] == "TestResults" && i > 0)
            {
                return parts[i - 1];
            }
        }

        // Fallback: extract from file path
        var testResultsIndex = Array.LastIndexOf(parts, "TestResults");
        if (testResultsIndex > 0 && testResultsIndex < parts.Length - 2)
        {
            return parts[testResultsIndex + 1];
        }

        return Path.GetFileName(Path.GetDirectoryName(filePath)) ?? "Unknown";
    }

    private List<string> GenerateRecommendations(
        CoverageMetrics overall,
        Dictionary<string, CoverageMetrics> byProject,
        List<string> lowCoverageAreas)
    {
        var recommendations = new List<string>();

        if (overall.LineRate * 100 < _lineThreshold)
        {
            recommendations.Add($"Overall line coverage ({overall.LineRate:P1}) is below threshold ({_lineThreshold}%)");
            recommendations.Add("Focus on adding unit tests for core business logic");
        }

        if (overall.BranchRate * 100 < _branchThreshold)
        {
            recommendations.Add($"Overall branch coverage ({overall.BranchRate:P1}) is below threshold ({_branchThreshold}%)");
            recommendations.Add("Add tests for error conditions and edge cases");
        }

        // Project-specific recommendations
        var coreProjects = byProject.Where(p =>
            p.Key.Contains("Core", StringComparison.OrdinalIgnoreCase) || p.Key.Contains("Abstractions", StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var project in coreProjects)
        {
            if (project.Value.LineRate * 100 < 80)
            {
                recommendations.Add($"Core project {project.Key} needs higher coverage (currently {project.Value.LineRate:P1})");
            }
        }

        // Hardware project recommendations
        var hardwareProjects = byProject.Where(p => p.Key.Contains("Hardware", StringComparison.OrdinalIgnoreCase)).ToList();
        if (hardwareProjects.Count != 0 && hardwareProjects.Average(p => p.Value.LineRate) * 100 < 60)
        {
            recommendations.Add("Consider adding more mock-based tests for hardware components");
        }

        // Integration test recommendations
        var integrationProjects = byProject.Where(p => p.Key.Contains("Integration", StringComparison.OrdinalIgnoreCase)).ToList();
        if (integrationProjects.Count != 0 && integrationProjects.Average(p => p.Value.LineRate) * 100 < 70)
        {
            recommendations.Add("Add more end-to-end integration test scenarios");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("Coverage looks good! Consider adding property-based tests for enhanced quality");
        }

        return recommendations;
    }

    public static async Task GenerateReportAsync(CoverageAnalysis analysis, string outputPath)
    {
        var report = new StringBuilder();

        report.AppendLine("# DotCompute Code Coverage Analysis Report");
        report.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}"));
        report.AppendLine();

        // Overall metrics
        report.AppendLine("## Overall Coverage Metrics");
        report.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- Line Coverage: {analysis.Overall.LineRate:P2} ({analysis.Overall.LinesCovered:N0}/{analysis.Overall.LinesTotal:N0})"));
        report.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- Branch Coverage: {analysis.Overall.BranchRate:P2} ({analysis.Overall.BranchesCovered:N0}/{analysis.Overall.BranchesTotal:N0})"));
        report.AppendLine();

        // By project
        if (analysis.ByProject.Count != 0)
        {
            report.AppendLine("## Coverage By Project");
            report.AppendLine("| Project | Line Coverage | Branch Coverage | Lines (Covered/Total) | Branches (Covered/Total) |");
            report.AppendLine("|---------|---------------|-----------------|----------------------|--------------------------|");

            foreach (var project in analysis.ByProject.OrderBy(p => p.Key))
            {
                var p = project.Value;
                report.AppendLine(string.Create(CultureInfo.InvariantCulture, $"| {project.Key} | {p.LineRate:P1} | {p.BranchRate:P1} | {p.LinesCovered:N0}/{p.LinesTotal:N0} | {p.BranchesCovered:N0}/{p.BranchesTotal:N0} |"));
            }
            report.AppendLine();
        }

        // Low coverage areas
        if (analysis.LowCoverageAreas.Count != 0)
        {
            report.AppendLine("## Areas Needing Attention");
            foreach (var area in analysis.LowCoverageAreas)
            {
                report.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- {area}"));
            }
            report.AppendLine();
        }

        // Uncovered methods
        if (analysis.UncoveredMethods.Count != 0)
        {
            report.AppendLine("## Uncovered Methods (Sample)");
            foreach (var method in analysis.UncoveredMethods.Take(20))
            {
                report.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- {method}"));
            }
            if (analysis.UncoveredMethods.Count > 20)
            {
                report.AppendLine(string.Create(CultureInfo.InvariantCulture, $"... and {analysis.UncoveredMethods.Count - 20} more"));
            }
            report.AppendLine();
        }

        // Recommendations
        if (analysis.Recommendations.Count != 0)
        {
            report.AppendLine("## Recommendations");
            foreach (var recommendation in analysis.Recommendations)
            {
                report.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- {recommendation}"));
            }
            report.AppendLine();
        }

        await File.WriteAllTextAsync(outputPath, report.ToString());
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Coverage analysis report generated: {outputPath}"));
    }

    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("JSON serialization may require types that cannot be statically analyzed.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("JSON serialization may require runtime code generation.")]
    public static async Task GenerateJsonReportAsync(CoverageAnalysis analysis, string outputPath)
    {
        var json = JsonSerializer.Serialize(analysis, JsonOptions);
        await File.WriteAllTextAsync(outputPath, json);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Coverage JSON report generated: {outputPath}"));
    }
}
