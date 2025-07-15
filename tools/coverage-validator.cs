using System;
using System.IO;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotCompute.Tools
{
    /// <summary>
    /// Coverage Validator Tool - Validates code coverage against quality targets
    /// </summary>
    public class CoverageValidator
    {
        private readonly CoverageTargets _targets;
        private readonly string _projectRoot;
        
        public CoverageValidator(string projectRoot)
        {
            _projectRoot = projectRoot;
            _targets = new CoverageTargets
            {
                LineCoverage = 95.0,
                BranchCoverage = 90.0,
                MethodCoverage = 95.0,
                ClassCoverage = 90.0
            };
        }

        public CoverageValidationResult ValidateCoverage()
        {
            var result = new CoverageValidationResult();
            
            // Find all coverage files
            var coverageFiles = FindCoverageFiles();
            
            if (!coverageFiles.Any())
            {
                result.ValidationErrors.Add("No coverage files found. Run tests with coverage collection first.");
                return result;
            }

            // Analyze each coverage file
            foreach (var file in coverageFiles)
            {
                var coverage = AnalyzeCoverageFile(file);
                if (coverage != null)
                {
                    result.ProjectCoverages.Add(coverage);
                }
            }

            // Calculate overall coverage
            result.OverallCoverage = CalculateOverallCoverage(result.ProjectCoverages);
            
            // Validate against targets
            ValidateAgainstTargets(result);
            
            return result;
        }

        private List<string> FindCoverageFiles()
        {
            var coverageFiles = new List<string>();
            var coverageDir = Path.Combine(_projectRoot, "coverage");
            
            if (Directory.Exists(coverageDir))
            {
                // Look for Cobertura XML files
                coverageFiles.AddRange(Directory.GetFiles(coverageDir, "*.cobertura.xml", SearchOption.AllDirectories));
                
                // Look for OpenCover XML files
                coverageFiles.AddRange(Directory.GetFiles(coverageDir, "coverage.opencover.xml", SearchOption.AllDirectories));
                
                // Look for coverage.json files
                coverageFiles.AddRange(Directory.GetFiles(coverageDir, "coverage.json", SearchOption.AllDirectories));
            }
            
            return coverageFiles;
        }

        private ProjectCoverage AnalyzeCoverageFile(string filePath)
        {
            try
            {
                if (filePath.EndsWith(".cobertura.xml"))
                {
                    return AnalyzeCoberturaFile(filePath);
                }
                else if (filePath.EndsWith(".opencover.xml"))
                {
                    return AnalyzeOpenCoverFile(filePath);
                }
                else if (filePath.EndsWith(".json"))
                {
                    return AnalyzeJsonFile(filePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error analyzing coverage file {filePath}: {ex.Message}");
            }
            
            return null;
        }

        private ProjectCoverage AnalyzeCoberturaFile(string filePath)
        {
            var doc = XDocument.Load(filePath);
            var coverage = doc.Root?.Element("coverage");
            
            if (coverage == null) return null;

            var projectCoverage = new ProjectCoverage
            {
                ProjectName = Path.GetFileNameWithoutDirectory(filePath),
                FilePath = filePath
            };

            // Extract overall metrics
            var lineRate = coverage.Attribute("line-rate")?.Value;
            var branchRate = coverage.Attribute("branch-rate")?.Value;
            
            if (double.TryParse(lineRate, out var line))
                projectCoverage.LineCoverage = line * 100;
            
            if (double.TryParse(branchRate, out var branch))
                projectCoverage.BranchCoverage = branch * 100;

            // Extract package/class details
            var packages = coverage.Descendants("package");
            foreach (var package in packages)
            {
                var packageName = package.Attribute("name")?.Value ?? "Unknown";
                var classes = package.Descendants("class");
                
                foreach (var cls in classes)
                {
                    var className = cls.Attribute("name")?.Value ?? "Unknown";
                    var classLineRate = cls.Attribute("line-rate")?.Value;
                    var classBranchRate = cls.Attribute("branch-rate")?.Value;
                    
                    var classInfo = new ClassCoverage
                    {
                        Name = className,
                        Package = packageName
                    };
                    
                    if (double.TryParse(classLineRate, out var classLine))
                        classInfo.LineCoverage = classLine * 100;
                    
                    if (double.TryParse(classBranchRate, out var classBranch))
                        classInfo.BranchCoverage = classBranch * 100;
                        
                    projectCoverage.Classes.Add(classInfo);
                }
            }

            return projectCoverage;
        }

        private ProjectCoverage AnalyzeOpenCoverFile(string filePath)
        {
            // Similar implementation for OpenCover format
            var doc = XDocument.Load(filePath);
            var results = doc.Root?.Element("CoverageSession");
            
            if (results == null) return null;

            var projectCoverage = new ProjectCoverage
            {
                ProjectName = Path.GetFileNameWithoutDirectory(filePath),
                FilePath = filePath
            };

            // Extract modules and calculate coverage
            var modules = results.Descendants("Module");
            foreach (var module in modules)
            {
                var classes = module.Descendants("Class");
                foreach (var cls in classes)
                {
                    var className = cls.Element("FullName")?.Value ?? "Unknown";
                    var methods = cls.Descendants("Method");
                    
                    var classInfo = new ClassCoverage
                    {
                        Name = className,
                        Package = module.Element("ModuleName")?.Value ?? "Unknown"
                    };
                    
                    // Calculate method coverage
                    var totalMethods = methods.Count();
                    var coveredMethods = methods.Count(m => 
                        m.Descendants("SequencePoint").Any(sp => 
                            int.TryParse(sp.Attribute("vc")?.Value, out var visits) && visits > 0));
                    
                    if (totalMethods > 0)
                        classInfo.MethodCoverage = (coveredMethods / (double)totalMethods) * 100;
                    
                    projectCoverage.Classes.Add(classInfo);
                }
            }

            return projectCoverage;
        }

        private ProjectCoverage AnalyzeJsonFile(string filePath)
        {
            var content = File.ReadAllText(filePath);
            var jsonDoc = JsonDocument.Parse(content);
            
            var projectCoverage = new ProjectCoverage
            {
                ProjectName = Path.GetFileNameWithoutDirectory(filePath),
                FilePath = filePath
            };

            // Extract coverage data from JSON structure
            if (jsonDoc.RootElement.TryGetProperty("summary", out var summary))
            {
                if (summary.TryGetProperty("linecoverage", out var lineElement))
                    projectCoverage.LineCoverage = lineElement.GetDouble();
                
                if (summary.TryGetProperty("branchcoverage", out var branchElement))
                    projectCoverage.BranchCoverage = branchElement.GetDouble();
                
                if (summary.TryGetProperty("methodcoverage", out var methodElement))
                    projectCoverage.MethodCoverage = methodElement.GetDouble();
            }

            return projectCoverage;
        }

        private CoverageMetrics CalculateOverallCoverage(List<ProjectCoverage> projectCoverages)
        {
            if (!projectCoverages.Any())
                return new CoverageMetrics();

            return new CoverageMetrics
            {
                LineCoverage = projectCoverages.Average(p => p.LineCoverage),
                BranchCoverage = projectCoverages.Average(p => p.BranchCoverage),
                MethodCoverage = projectCoverages.Average(p => p.MethodCoverage),
                ClassCoverage = projectCoverages.Average(p => p.ClassCoverage)
            };
        }

        private void ValidateAgainstTargets(CoverageValidationResult result)
        {
            var overall = result.OverallCoverage;
            
            if (overall.LineCoverage < _targets.LineCoverage)
            {
                result.ValidationErrors.Add($"Line coverage {overall.LineCoverage:F1}% below target {_targets.LineCoverage:F1}%");
            }
            
            if (overall.BranchCoverage < _targets.BranchCoverage)
            {
                result.ValidationErrors.Add($"Branch coverage {overall.BranchCoverage:F1}% below target {_targets.BranchCoverage:F1}%");
            }
            
            if (overall.MethodCoverage < _targets.MethodCoverage)
            {
                result.ValidationErrors.Add($"Method coverage {overall.MethodCoverage:F1}% below target {_targets.MethodCoverage:F1}%");
            }
            
            if (overall.ClassCoverage < _targets.ClassCoverage)
            {
                result.ValidationErrors.Add($"Class coverage {overall.ClassCoverage:F1}% below target {_targets.ClassCoverage:F1}%");
            }

            result.IsValid = !result.ValidationErrors.Any();
        }

        public void GenerateReport(CoverageValidationResult result)
        {
            Console.WriteLine("📊 Coverage Validation Report");
            Console.WriteLine("=============================");
            
            if (!result.ProjectCoverages.Any())
            {
                Console.WriteLine("❌ No coverage data found");
                return;
            }
            
            Console.WriteLine($"📈 Overall Coverage Metrics:");
            Console.WriteLine($"   Line Coverage:   {result.OverallCoverage.LineCoverage:F1}%");
            Console.WriteLine($"   Branch Coverage: {result.OverallCoverage.BranchCoverage:F1}%");
            Console.WriteLine($"   Method Coverage: {result.OverallCoverage.MethodCoverage:F1}%");
            Console.WriteLine($"   Class Coverage:  {result.OverallCoverage.ClassCoverage:F1}%");
            
            Console.WriteLine();
            Console.WriteLine("🎯 Target Validation:");
            if (result.IsValid)
            {
                Console.WriteLine("✅ All coverage targets met!");
            }
            else
            {
                Console.WriteLine("❌ Coverage targets not met:");
                foreach (var error in result.ValidationErrors)
                {
                    Console.WriteLine($"   • {error}");
                }
            }
            
            Console.WriteLine();
            Console.WriteLine("📋 Project Coverage Details:");
            foreach (var project in result.ProjectCoverages)
            {
                Console.WriteLine($"   {project.ProjectName}:");
                Console.WriteLine($"     Line: {project.LineCoverage:F1}%, Branch: {project.BranchCoverage:F1}%");
                
                // Show low coverage classes
                var lowCoverageClasses = project.Classes.Where(c => c.LineCoverage < 80).ToList();
                if (lowCoverageClasses.Any())
                {
                    Console.WriteLine($"     Low coverage classes ({lowCoverageClasses.Count}):");
                    foreach (var cls in lowCoverageClasses.Take(5))
                    {
                        Console.WriteLine($"       • {cls.Name}: {cls.LineCoverage:F1}%");
                    }
                }
            }
        }
    }

    public class CoverageValidationResult
    {
        public List<ProjectCoverage> ProjectCoverages { get; set; } = new();
        public CoverageMetrics OverallCoverage { get; set; } = new();
        public List<string> ValidationErrors { get; set; } = new();
        public bool IsValid { get; set; }
    }

    public class ProjectCoverage
    {
        public string ProjectName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public double LineCoverage { get; set; }
        public double BranchCoverage { get; set; }
        public double MethodCoverage { get; set; }
        public double ClassCoverage { get; set; }
        public List<ClassCoverage> Classes { get; set; } = new();
    }

    public class ClassCoverage
    {
        public string Name { get; set; } = string.Empty;
        public string Package { get; set; } = string.Empty;
        public double LineCoverage { get; set; }
        public double BranchCoverage { get; set; }
        public double MethodCoverage { get; set; }
    }

    public class CoverageMetrics
    {
        public double LineCoverage { get; set; }
        public double BranchCoverage { get; set; }
        public double MethodCoverage { get; set; }
        public double ClassCoverage { get; set; }
    }

    public class CoverageTargets
    {
        public double LineCoverage { get; set; }
        public double BranchCoverage { get; set; }
        public double MethodCoverage { get; set; }
        public double ClassCoverage { get; set; }
    }

    // Console application entry point
    class Program
    {
        static void Main(string[] args)
        {
            var projectRoot = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
            var validator = new CoverageValidator(projectRoot);
            
            Console.WriteLine("🔍 Coverage Validator - Starting Analysis");
            Console.WriteLine("========================================");
            
            var result = validator.ValidateCoverage();
            validator.GenerateReport(result);
            
            // Exit code based on validation result
            Environment.Exit(result.IsValid ? 0 : 1);
        }
    }
}

// Extensions for path handling
namespace System.IO
{
    public static class PathExtensions
    {
        public static string GetFileNameWithoutDirectory(this string path)
        {
            return Path.GetFileName(path);
        }
    }
}