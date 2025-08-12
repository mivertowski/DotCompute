using System.Xml.Linq;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace DotCompute.Tools.coverage-enhancement;

/// <summary>
/// Analyzes coverage gaps and suggests test improvements
/// </summary>
public class CoverageEnhancer
{
    public record UncoveredMethod(
        string ClassName,
        string MethodName,
        string FullSignature,
        int LineNumber,
        string FilePath,
        CriticalityLevel Criticality,
        string Reason);

    public record CoverageGap(
        string ProjectName,
        string Area,
        double CurrentCoverage,
        double TargetCoverage,
        int UncoveredLines,
        List<UncoveredMethod> CriticalMethods);

    public record TestSuggestion(
        string ProjectName,
        TestType Type,
        string Description,
        List<string> ExampleTests,
        Priority Priority);

    public enum CriticalityLevel { Low, Medium, High, Critical }
    public enum TestType { Unit, Integration, Property, Performance, Error }
    public enum Priority { Low, Medium, High, Critical }

    private readonly string _solutionPath;
    private readonly string _coverageDirectory;
    
    // Critical method patterns that should always be tested
    private readonly List<(Regex Pattern, CriticalityLevel Level, string Reason)> _criticalPatterns = new()
    {
        (new Regex(@"\.Dispose\(", RegexOptions.IgnoreCase), CriticalityLevel.Critical, "Resource cleanup is safety-critical"),
        (new Regex(@"\.Execute", RegexOptions.IgnoreCase), CriticalityLevel.High, "Execution methods are core functionality"),
        (new Regex(@"\.Compile", RegexOptions.IgnoreCase), CriticalityLevel.High, "Compilation methods are complex and error-prone"),
        (new Regex(@"\.Allocate", RegexOptions.IgnoreCase), CriticalityLevel.High, "Memory allocation methods need thorough testing"),
        (new Regex(@"\.Validate", RegexOptions.IgnoreCase), CriticalityLevel.High, "Validation methods prevent runtime errors"),
        (new Regex(@"\.Free|\.Release", RegexOptions.IgnoreCase), CriticalityLevel.High, "Resource management methods"),
        (new Regex(@"\.Launch|\.Start", RegexOptions.IgnoreCase), CriticalityLevel.High, "Initialization methods"),
        (new Regex(@"\.Process|\.Handle", RegexOptions.IgnoreCase), CriticalityLevel.Medium, "Processing methods often contain business logic"),
        (new Regex(@"\.Parse|\.Convert", RegexOptions.IgnoreCase), CriticalityLevel.Medium, "Parsing methods prone to edge cases"),
        (new Regex(@"\.Create|\.Build", RegexOptions.IgnoreCase), CriticalityLevel.Medium, "Factory methods create important objects"),
    };

    public CoverageEnhancer(string solutionPath, string coverageDirectory)
    {
        _solutionPath = solutionPath;
        _coverageDirectory = coverageDirectory;
    }

    public async Task<(List<CoverageGap> Gaps, List<TestSuggestion> Suggestions)> AnalyzeGapsAsync()
    {
        Console.WriteLine("Analyzing coverage gaps and generating test suggestions...");
        
        var gaps = new List<CoverageGap>();
        var suggestions = new List<TestSuggestion>();

        // Find all coverage files
        var coverageFiles = Directory.GetFiles(_coverageDirectory, "coverage.cobertura.xml", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/bin/") && !f.Contains("/obj/"))
            .ToList();

        if (!coverageFiles.Any())
        {
            Console.WriteLine("No coverage files found for analysis");
            return (gaps, suggestions);
        }

        foreach (var coverageFile in coverageFiles)
        {
            try
            {
                var projectGaps = await AnalyzeProjectGapsAsync(coverageFile);
                var projectSuggestions = await GenerateProjectSuggestionsAsync(coverageFile, projectGaps);
                
                gaps.AddRange(projectGaps);
                suggestions.AddRange(projectSuggestions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error analyzing {coverageFile}: {ex.Message}");
            }
        }

        // Add general suggestions based on project types
        var generalSuggestions = GenerateGeneralSuggestions(gaps);
        suggestions.AddRange(generalSuggestions);

        return (gaps, suggestions);
    }

    private async Task<List<CoverageGap>> AnalyzeProjectGapsAsync(string coverageFile)
    {
        var gaps = new List<CoverageGap>();
        
        var doc = await Task.Run(() => XDocument.Load(coverageFile));
        var projectName = ExtractProjectName(coverageFile);
        
        // Get target coverage for this project type
        var targetCoverage = GetTargetCoverage(projectName);
        
        var packages = doc.Descendants("package");
        
        foreach (var package in packages)
        {
            var packageName = package.Attribute("name")?.Value ?? "Unknown";
            var lineRate = double.Parse(package.Attribute("line-rate")?.Value ?? "0");
            var currentCoverage = lineRate * 100;
            
            if (currentCoverage < targetCoverage)
            {
                var uncoveredMethods = await AnalyzeUncoveredMethodsAsync(package);
                var uncoveredLines = CalculateUncoveredLines(package);
                
                gaps.Add(new CoverageGap(
                    projectName,
                    packageName,
                    currentCoverage,
                    targetCoverage,
                    uncoveredLines,
                    uncoveredMethods));
            }
        }

        return gaps;
    }

    private async Task<List<UncoveredMethod>> AnalyzeUncoveredMethodsAsync(XElement package)
    {
        var uncoveredMethods = new List<UncoveredMethod>();
        
        var classes = package.Descendants("class");
        
        foreach (var classElement in classes)
        {
            var className = classElement.Attribute("name")?.Value ?? "Unknown";
            var methods = classElement.Descendants("method")
                .Where(m => double.Parse(m.Attribute("line-rate")?.Value ?? "1") == 0);
            
            foreach (var method in methods)
            {
                var methodName = method.Attribute("name")?.Value ?? "Unknown";
                var signature = method.Attribute("signature")?.Value ?? methodName;
                
                // Determine criticality
                var criticality = DetermineCriticality(methodName, signature);
                var reason = GetCriticalityReason(methodName, signature);
                
                // Extract line information
                var lines = method.Descendants("line").FirstOrDefault();
                var lineNumber = int.Parse(lines?.Attribute("number")?.Value ?? "0");
                
                uncoveredMethods.Add(new UncoveredMethod(
                    className,
                    methodName,
                    signature,
                    lineNumber,
                    ExtractFilePath(classElement),
                    criticality,
                    reason));
            }
        }

        return uncoveredMethods.OrderByDescending(m => m.Criticality).ToList();
    }

    private async Task<List<TestSuggestion>> GenerateProjectSuggestionsAsync(string coverageFile, List<CoverageGap> projectGaps)
    {
        var suggestions = new List<TestSuggestion>();
        var projectName = ExtractProjectName(coverageFile);

        // Analyze project type and suggest appropriate test strategies
        if (projectName.Contains("Core"))
        {
            suggestions.AddRange(GenerateCoreSuggestions(projectName, projectGaps));
        }
        else if (projectName.Contains("Memory"))
        {
            suggestions.AddRange(GenerateMemorySuggestions(projectName, projectGaps));
        }
        else if (projectName.Contains("Hardware"))
        {
            suggestions.AddRange(GenerateHardwareSuggestions(projectName, projectGaps));
        }
        else if (projectName.Contains("Integration"))
        {
            suggestions.AddRange(GenerateIntegrationSuggestions(projectName, projectGaps));
        }
        else if (projectName.Contains("Algorithms"))
        {
            suggestions.AddRange(GenerateAlgorithmSuggestions(projectName, projectGaps));
        }

        // Add method-specific suggestions for critical uncovered methods
        foreach (var gap in projectGaps)
        {
            var criticalMethods = gap.CriticalMethods.Where(m => m.Criticality >= CriticalityLevel.High).ToList();
            if (criticalMethods.Any())
            {
                var examples = criticalMethods.Take(3).Select(m => 
                    $"Test_{m.MethodName}_{GetTestScenario(m)}()").ToList();

                suggestions.Add(new TestSuggestion(
                    projectName,
                    TestType.Unit,
                    $"Add tests for critical methods in {gap.Area}",
                    examples,
                    Priority.High));
            }
        }

        return suggestions;
    }

    private List<TestSuggestion> GenerateCoreSuggestions(string projectName, List<CoverageGap> gaps)
    {
        var suggestions = new List<TestSuggestion>();

        suggestions.Add(new TestSuggestion(
            projectName,
            TestType.Unit,
            "Add comprehensive unit tests for core computation logic",
            new List<string>
            {
                "Test_KernelExecution_WithValidInput_ReturnsCorrectResult()",
                "Test_KernelExecution_WithInvalidInput_ThrowsException()",
                "Test_MemoryAllocation_WithLargeSize_SucceedsOrThrows()"
            },
            Priority.High));

        suggestions.Add(new TestSuggestion(
            projectName,
            TestType.Error,
            "Add error handling tests for edge cases",
            new List<string>
            {
                "Test_Execute_WithNullKernel_ThrowsArgumentNullException()",
                "Test_Execute_WithInsufficientMemory_ThrowsMemoryException()",
                "Test_Execute_WithInvalidDimensions_ThrowsArgumentException()"
            },
            Priority.High));

        if (gaps.Any(g => g.UncoveredLines > 50))
        {
            suggestions.Add(new TestSuggestion(
                projectName,
                TestType.Integration,
                "Add integration tests for complex workflows",
                new List<string>
                {
                    "Test_CompleteWorkflow_FromKernelToExecution()",
                    "Test_MultiStepPipeline_WithRealData()",
                    "Test_ErrorRecovery_InLongRunningOperations()"
                },
                Priority.Medium));
        }

        return suggestions;
    }

    private List<TestSuggestion> GenerateMemorySuggestions(string projectName, List<CoverageGap> gaps)
    {
        return new List<TestSuggestion>
        {
            new(projectName, TestType.Unit, "Add memory allocation/deallocation tests",
                new List<string>
                {
                    "Test_Allocate_ValidSize_ReturnsValidPointer()",
                    "Test_Free_ValidPointer_SuccessfullyFrees()", 
                    "Test_Allocate_ZeroSize_HandlesGracefully()"
                }, Priority.Critical),
            
            new(projectName, TestType.Property, "Add property-based tests for memory operations",
                new List<string>
                {
                    "Property_AllocateThenFree_AlwaysSucceeds()",
                    "Property_MultipleAllocations_DontOverlap()",
                    "Property_MemoryAlignment_AlwaysCorrect()"
                }, Priority.High),

            new(projectName, TestType.Performance, "Add memory performance and stress tests",
                new List<string>
                {
                    "Test_LargeAllocation_CompletesInReasonableTime()",
                    "Test_ManySmallAllocations_ManagesMemoryEfficiently()",
                    "Test_MemoryPressure_HandlesGracefully()"
                }, Priority.Medium)
        };
    }

    private List<TestSuggestion> GenerateHardwareSuggestions(string projectName, List<CoverageGap> gaps)
    {
        return new List<TestSuggestion>
        {
            new(projectName, TestType.Unit, "Add mock-based hardware tests",
                new List<string>
                {
                    "Test_DeviceDetection_WithMockDevice_ReturnsExpected()",
                    "Test_KernelExecution_WithMockBackend_Succeeds()",
                    "Test_MemoryTransfer_WithSimulatedHardware_WorksCorrectly()"
                }, Priority.High),
            
            new(projectName, TestType.Integration, "Add hardware availability tests",
                new List<string>
                {
                    "Test_RealHardware_IfAvailable_ExecutesCorrectly()",
                    "Test_FallbackToMock_WhenHardwareUnavailable()",
                    "Test_HardwareCapabilities_MatchExpectedSpecs()"
                }, Priority.Medium)
        };
    }

    private List<TestSuggestion> GenerateIntegrationSuggestions(string projectName, List<CoverageGap> gaps)
    {
        return new List<TestSuggestion>
        {
            new(projectName, TestType.Integration, "Add end-to-end workflow tests",
                new List<string>
                {
                    "Test_FullWorkflow_FromInputToOutput()",
                    "Test_MultipleComponents_WorkTogether()",
                    "Test_ErrorPropagation_ThroughPipeline()"
                }, Priority.High),

            new(projectName, TestType.Performance, "Add performance integration tests",
                new List<string>
                {
                    "Test_LargeDataset_ProcessingPerformance()",
                    "Test_ConcurrentOperations_ScaleCorrectly()",
                    "Test_MemoryUsage_StaysWithinBounds()"
                }, Priority.Medium)
        };
    }

    private List<TestSuggestion> GenerateAlgorithmSuggestions(string projectName, List<CoverageGap> gaps)
    {
        return new List<TestSuggestion>
        {
            new(projectName, TestType.Property, "Add property-based tests for mathematical correctness",
                new List<string>
                {
                    "Property_MatrixMultiplication_IsAssociative()",
                    "Property_FFT_InverseIsCorrect()",
                    "Property_LinearAlgebra_IdentityOperations()"
                }, Priority.High),

            new(projectName, TestType.Unit, "Add numerical accuracy tests",
                new List<string>
                {
                    "Test_Algorithm_WithKnownInput_ProducesExpectedOutput()",
                    "Test_EdgeCases_NaN_Infinity_ZeroInputs()",
                    "Test_NumericalStability_WithSmallNumbers()"
                }, Priority.High)
        };
    }

    private List<TestSuggestion> GenerateGeneralSuggestions(List<CoverageGap> allGaps)
    {
        var suggestions = new List<TestSuggestion>();

        // Find projects with consistently low coverage
        var lowCoverageProjects = allGaps
            .GroupBy(g => g.ProjectName)
            .Where(g => g.Average(gap => gap.CurrentCoverage) < 60)
            .Select(g => g.Key)
            .ToList();

        foreach (var project in lowCoverageProjects)
        {
            suggestions.Add(new TestSuggestion(
                project,
                TestType.Unit,
                "Priority: Add basic unit tests to improve overall coverage",
                new List<string>
                {
                    "Focus on public methods and properties",
                    "Test happy path scenarios first",
                    "Add parameter validation tests"
                },
                Priority.Critical));
        }

        // Suggest property-based testing for appropriate projects
        var algorithmProjects = allGaps
            .Where(g => g.ProjectName.Contains("Algorithm") || g.ProjectName.Contains("Math") || g.ProjectName.Contains("Core"))
            .Select(g => g.ProjectName)
            .Distinct()
            .ToList();

        foreach (var project in algorithmProjects)
        {
            suggestions.Add(new TestSuggestion(
                project,
                TestType.Property,
                "Consider property-based testing for mathematical operations",
                new List<string>
                {
                    "Install FsCheck or similar property-based testing framework",
                    "Test mathematical properties (associativity, commutativity, etc.)",
                    "Generate random test cases for edge case discovery"
                },
                Priority.Medium));
        }

        return suggestions;
    }

    private CriticalityLevel DetermineCriticality(string methodName, string signature)
    {
        foreach (var (pattern, level, _) in _criticalPatterns)
        {
            if (pattern.IsMatch(methodName) || pattern.IsMatch(signature))
            {
                return level;
            }
        }

        // Additional heuristics
        if (methodName.StartsWith("get_") || methodName.StartsWith("set_"))
            return CriticalityLevel.Low; // Properties are typically less critical

        if (methodName.Contains("Test") || methodName.Contains("Mock"))
            return CriticalityLevel.Low; // Test utilities

        return CriticalityLevel.Medium; // Default for uncategorized methods
    }

    private string GetCriticalityReason(string methodName, string signature)
    {
        foreach (var (pattern, _, reason) in _criticalPatterns)
        {
            if (pattern.IsMatch(methodName) || pattern.IsMatch(signature))
            {
                return reason;
            }
        }

        return "Standard method requiring test coverage";
    }

    private string GetTestScenario(UncoveredMethod method)
    {
        var scenarios = new[]
        {
            "Success", "WithNullInput", "WithInvalidInput", "WithBoundaryValues", 
            "ThrowsException", "ReturnsExpected", "HandlesEdgeCase"
        };

        // Simple hash-based selection for consistency
        var hash = (method.MethodName + method.ClassName).GetHashCode();
        return scenarios[Math.Abs(hash) % scenarios.Length];
    }

    private double GetTargetCoverage(string projectName)
    {
        return projectName.ToLowerInvariant() switch
        {
            var name when name.Contains("core") => 85.0,
            var name when name.Contains("abstractions") => 90.0,
            var name when name.Contains("memory") => 80.0,
            var name when name.Contains("plugins") => 75.0,
            var name when name.Contains("hardware") => 50.0,
            var name when name.Contains("integration") => 60.0,
            _ => 70.0
        };
    }

    private int CalculateUncoveredLines(XElement package)
    {
        var linesValid = int.Parse(package.Attribute("lines-valid")?.Value ?? "0");
        var linesCovered = int.Parse(package.Attribute("lines-covered")?.Value ?? "0");
        return linesValid - linesCovered;
    }

    private string ExtractProjectName(string filePath)
    {
        var parts = filePath.Split(Path.DirectorySeparatorChar);
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i] == "TestResults" && i > 0)
                return parts[i - 1];
        }
        return Path.GetFileName(Path.GetDirectoryName(filePath)) ?? "Unknown";
    }

    private string ExtractFilePath(XElement classElement)
    {
        return classElement.Attribute("filename")?.Value ?? "Unknown";
    }

    public async Task GenerateEnhancementReportAsync(
        List<CoverageGap> gaps, 
        List<TestSuggestion> suggestions, 
        string outputPath)
    {
        var report = new StringBuilder();
        
        report.AppendLine("# Coverage Enhancement Analysis Report");
        report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine();

        // Executive Summary
        report.AppendLine("## Executive Summary");
        report.AppendLine($"- Projects with coverage gaps: {gaps.GroupBy(g => g.ProjectName).Count()}");
        report.AppendLine($"- Total coverage areas analyzed: {gaps.Count}");
        report.AppendLine($"- Critical uncovered methods: {gaps.SelectMany(g => g.CriticalMethods).Count(m => m.Criticality >= CriticalityLevel.High)}");
        report.AppendLine($"- Test suggestions generated: {suggestions.Count}");
        report.AppendLine();

        // Coverage Gaps by Project
        if (gaps.Any())
        {
            report.AppendLine("## Coverage Gaps by Project");
            
            var projectGroups = gaps.GroupBy(g => g.ProjectName).OrderBy(g => g.Key);
            
            foreach (var projectGroup in projectGroups)
            {
                var project = projectGroup.Key;
                var projectGaps = projectGroup.ToList();
                
                report.AppendLine($"### {project}");
                
                foreach (var gap in projectGaps)
                {
                    report.AppendLine($"#### {gap.Area}");
                    report.AppendLine($"- Current Coverage: {gap.CurrentCoverage:F1}%");
                    report.AppendLine($"- Target Coverage: {gap.TargetCoverage:F1}%");
                    report.AppendLine($"- Gap: {gap.TargetCoverage - gap.CurrentCoverage:F1} percentage points");
                    report.AppendLine($"- Uncovered Lines: {gap.UncoveredLines}");
                    
                    if (gap.CriticalMethods.Any())
                    {
                        report.AppendLine("- **Critical Uncovered Methods:**");
                        foreach (var method in gap.CriticalMethods.Take(10))
                        {
                            report.AppendLine($"  - `{method.MethodName}` ({method.Criticality}) - {method.Reason}");
                        }
                        
                        if (gap.CriticalMethods.Count > 10)
                        {
                            report.AppendLine($"  - ... and {gap.CriticalMethods.Count - 10} more");
                        }
                    }
                    report.AppendLine();
                }
            }
        }

        // Test Suggestions
        if (suggestions.Any())
        {
            report.AppendLine("## Test Enhancement Suggestions");
            
            var suggestionGroups = suggestions
                .GroupBy(s => s.Priority)
                .OrderByDescending(g => g.Key);
            
            foreach (var priorityGroup in suggestionGroups)
            {
                report.AppendLine($"### {priorityGroup.Key} Priority");
                
                var projectGroups = priorityGroup.GroupBy(s => s.ProjectName).OrderBy(g => g.Key);
                
                foreach (var projectGroup in projectGroups)
                {
                    report.AppendLine($"#### {projectGroup.Key}");
                    
                    foreach (var suggestion in projectGroup)
                    {
                        report.AppendLine($"**{suggestion.Type} Tests: {suggestion.Description}**");
                        
                        if (suggestion.ExampleTests.Any())
                        {
                            report.AppendLine("Example tests:");
                            foreach (var example in suggestion.ExampleTests)
                            {
                                report.AppendLine($"- `{example}`");
                            }
                        }
                        report.AppendLine();
                    }
                }
            }
        }

        // Implementation Guide
        report.AppendLine("## Implementation Guide");
        report.AppendLine();
        
        report.AppendLine("### Quick Wins (High Impact, Low Effort)");
        var quickWins = suggestions.Where(s => s.Priority >= Priority.High && s.Type == TestType.Unit).ToList();
        if (quickWins.Any())
        {
            foreach (var win in quickWins.Take(5))
            {
                report.AppendLine($"- **{win.ProjectName}**: {win.Description}");
            }
        }
        else
        {
            report.AppendLine("- Focus on adding basic unit tests for public methods");
            report.AppendLine("- Prioritize testing methods with complex logic or error handling");
        }
        report.AppendLine();
        
        report.AppendLine("### Property-Based Testing Opportunities");
        var propertyTests = suggestions.Where(s => s.Type == TestType.Property).ToList();
        if (propertyTests.Any())
        {
            report.AppendLine("Consider implementing property-based tests for:");
            foreach (var test in propertyTests)
            {
                report.AppendLine($"- **{test.ProjectName}**: Mathematical operations and algorithms");
            }
        }
        report.AppendLine();
        
        report.AppendLine("### Performance Testing Candidates");
        var perfTests = suggestions.Where(s => s.Type == TestType.Performance).ToList();
        if (perfTests.Any())
        {
            foreach (var test in perfTests)
            {
                report.AppendLine($"- **{test.ProjectName}**: {test.Description}");
            }
        }
        report.AppendLine();

        report.AppendLine("## Next Steps");
        report.AppendLine("1. **Immediate Actions:**");
        report.AppendLine("   - Address critical uncovered methods first");
        report.AppendLine("   - Add basic unit tests for projects below 60% coverage");
        report.AppendLine("   - Focus on error handling and edge cases");
        report.AppendLine();
        report.AppendLine("2. **Medium-term Goals:**");
        report.AppendLine("   - Implement property-based testing for mathematical operations");
        report.AppendLine("   - Add integration tests for complex workflows");
        report.AppendLine("   - Set up performance benchmarking tests");
        report.AppendLine();
        report.AppendLine("3. **Tools and Setup:**");
        report.AppendLine("   - Consider FsCheck for property-based testing");
        report.AppendLine("   - Set up BenchmarkDotNet for performance tests");
        report.AppendLine("   - Implement parameterized tests for edge case coverage");

        await File.WriteAllTextAsync(outputPath, report.ToString());
        Console.WriteLine($"Coverage enhancement report generated: {outputPath}");
    }
}