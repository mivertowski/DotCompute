using System.CommandLine;
using DotCompute.Tools.CoverageEnhancement;

var rootCommand = new RootCommand("DotCompute Coverage Enhancement Tool");

var solutionOption = new Option<DirectoryInfo>("--solution", "Path to the solution directory");
solutionOption.AddAlias("-s");
solutionOption.IsRequired = true;

var coverageOption = new Option<DirectoryInfo>("--coverage", "Directory containing coverage files");
coverageOption.AddAlias("-c");
coverageOption.IsRequired = true;

var outputOption = new Option<FileInfo>("--output", () => new FileInfo("coverage-enhancement-report.md"), "Output file path for the enhancement report");
outputOption.AddAlias("-o");

var jsonOutputOption = new Option<FileInfo>("--json", () => new FileInfo("coverage-enhancement.json"), "JSON output file path for the enhancement data");
jsonOutputOption.AddAlias("-j");

rootCommand.Add(solutionOption);
rootCommand.Add(coverageOption);
rootCommand.Add(outputOption);
rootCommand.Add(jsonOutputOption);

rootCommand.SetHandler(async (solution, coverage, output, jsonOutput) =>
{
    Console.WriteLine("DotCompute Coverage Enhancement Tool");
    Console.WriteLine("====================================");
    Console.WriteLine($"Solution: {solution.FullName}");
    Console.WriteLine($"Coverage Directory: {coverage.FullName}");
    
    if (!solution.Exists)
    {
        Console.Error.WriteLine($"Solution directory does not exist: {solution.FullName}");
        Environment.Exit(1);
    }
    
    if (!coverage.Exists)
    {
        Console.Error.WriteLine($"Coverage directory does not exist: {coverage.FullName}");
        Environment.Exit(1);
    }

    var enhancer = new CoverageEnhancer(solution.FullName, coverage.FullName);
    
    try
    {
        var (gaps, suggestions) = await enhancer.AnalyzeGapsAsync();
        
        // Display summary
        Console.WriteLine();
        Console.WriteLine("Enhancement Analysis Summary:");
        Console.WriteLine($"  Coverage Gaps Found: {gaps.Count}");
        Console.WriteLine($"  Projects Analyzed: {gaps.Select(g => g.ProjectName).Distinct().Count()}");
        Console.WriteLine($"  Critical Methods Uncovered: {gaps.SelectMany(g => g.CriticalMethods).Count(m => m.Criticality >= CoverageEnhancer.CriticalityLevel.High)}");
        Console.WriteLine($"  Test Suggestions Generated: {suggestions.Count}");
        Console.WriteLine();

        // Show top gaps
        if (gaps.Any())
        {
            Console.WriteLine("Top Coverage Gaps:");
            var topGaps = gaps.OrderBy(g => g.CurrentCoverage).Take(5);
            foreach (var gap in topGaps)
            {
                Console.WriteLine($"  - {gap.ProjectName}/{gap.Area}: {gap.CurrentCoverage:F1}% (target: {gap.TargetCoverage:F1}%)");
            }
            Console.WriteLine();
        }

        // Show priority suggestions
        var highPrioritySuggestions = suggestions.Where(s => s.Priority >= CoverageEnhancer.Priority.High).ToList();
        if (highPrioritySuggestions.Any())
        {
            Console.WriteLine("High Priority Suggestions:");
            foreach (var suggestion in highPrioritySuggestions.Take(3))
            {
                Console.WriteLine($"  - {suggestion.ProjectName}: {suggestion.Description}");
            }
            Console.WriteLine();
        }

        // Generate reports
        await enhancer.GenerateEnhancementReportAsync(gaps, suggestions, output.FullName);
        
        // Generate JSON for tooling
        var enhancementData = new
        {
            Timestamp = DateTime.Now,
            Summary = new
            {
                TotalGaps = gaps.Count,
                ProjectsAnalyzed = gaps.Select(g => g.ProjectName).Distinct().Count(),
                CriticalMethodsUncovered = gaps.SelectMany(g => g.CriticalMethods).Count(m => m.Criticality >= CoverageEnhancer.CriticalityLevel.High),
                SuggestionsGenerated = suggestions.Count
            },
            Gaps = gaps,
            Suggestions = suggestions
        };
        
        var json = System.Text.Json.JsonSerializer.Serialize(enhancementData, new System.Text.Json.JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        
        await File.WriteAllTextAsync(jsonOutput.FullName, json);
        Console.WriteLine($"Enhancement JSON data saved: {jsonOutput.FullName}");
        
        Console.WriteLine("Coverage enhancement analysis completed successfully!");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error during enhancement analysis: {ex.Message}");
        Environment.Exit(1);
    }
}, solutionOption, coverageOption, outputOption, jsonOutputOption);

return await rootCommand.InvokeAsync(args);