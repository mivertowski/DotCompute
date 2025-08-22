// DotCompute Project Template
// This template provides a starting structure for new DotCompute projects
// following Clean Architecture principles and standardized folder organization

using System;
using System.IO;
using System.Collections.Generic;

namespace DotCompute.Templates
{
    /// <summary>
    /// Template generator for creating new DotCompute projects with proper Clean Architecture structure
    /// </summary>
    public class ProjectTemplate
    {
        private readonly string _projectName;
        private readonly ProjectType _projectType;
        private readonly string _targetPath;

        public ProjectTemplate(string projectName, ProjectType projectType, string targetPath)
        {
            _projectName = projectName ?? throw new ArgumentNullException(nameof(projectName));
            _projectType = projectType;
            _targetPath = targetPath ?? throw new ArgumentNullException(nameof(targetPath));
        }

        /// <summary>
        /// Creates the complete project structure with all standard folders and files
        /// </summary>
        public void CreateProject()
        {
            CreateProjectStructure();
            CreateProjectFile();
            CreateGlobalUsings();
            CreateTemplateFiles();
            CreateReadme();
        }

        private void CreateProjectStructure()
        {
            var folders = GetStandardFolders();
            var projectSpecificFolders = GetProjectSpecificFolders();
            
            var allFolders = new List<string>(folders);
            allFolders.AddRange(projectSpecificFolders);

            foreach (var folder in allFolders)
            {
                var folderPath = Path.Combine(_targetPath, _projectName, folder);
                Directory.CreateDirectory(folderPath);
                
                // Create .gitkeep to ensure empty folders are tracked
                var gitKeepPath = Path.Combine(folderPath, ".gitkeep");
                File.WriteAllText(gitKeepPath, "# This file ensures the folder is tracked by Git\n");
            }
        }

        private List<string> GetStandardFolders()
        {
            return new List<string>
            {
                "Interfaces",
                "Enums", 
                "Types",
                "Models",
                "Configuration",
                "Exceptions",
                "Services",
                "Internal",
                "Extensions",
                "Utilities",
                "Abstractions",
                "Results",
                "Validation"
            };
        }

        private List<string> GetProjectSpecificFolders()
        {
            return _projectType switch
            {
                ProjectType.Core => new List<string> { "Planning", "Metrics", "Recovery", "Logging" },
                ProjectType.Backend => new List<string> { "Registration", "Metrics", "Advanced" },
                ProjectType.Runtime => new List<string>(),
                ProjectType.Plugin => new List<string>(),
                ProjectType.Extension => new List<string>(),
                ProjectType.Generator => new List<string> { "Templates" },
                _ => new List<string>()
            };
        }

        private void CreateProjectFile()
        {
            var projectContent = GenerateProjectFileContent();
            var projectFilePath = Path.Combine(_targetPath, _projectName, $"{_projectName}.csproj");
            File.WriteAllText(projectFilePath, projectContent);
        }

        private string GenerateProjectFileContent()
        {
            var targetFramework = "net9.0";
            var packageReferences = GetPackageReferences();
            var projectReferences = GetProjectReferences();

            var content = $@"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <TargetFramework>{targetFramework}</TargetFramework>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsAsErrors />
    <WarningsNotAsErrors>CS1591</WarningsNotAsErrors>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <PackageId>{_projectName}</PackageId>
    <Description>{GetProjectDescription()}</Description>
    <PackageTags>dotcompute;gpu;compute;{_projectType.ToString().ToLower()}</PackageTags>
  </PropertyGroup>

  <ItemGroup>
{packageReferences}
  </ItemGroup>

{(projectReferences.Length > 0 ? $@"  <ItemGroup>
{projectReferences}
  </ItemGroup>" : "")}

</Project>";

            return content;
        }

        private string GetPackageReferences()
        {
            var commonPackages = @"    <PackageReference Include=""Microsoft.Extensions.Logging.Abstractions"" Version=""9.0.0"" />
    <PackageReference Include=""Microsoft.Extensions.DependencyInjection.Abstractions"" Version=""9.0.0"" />";

            var typeSpecificPackages = _projectType switch
            {
                ProjectType.Backend => @"
    <PackageReference Include=""System.Numerics.Vectors"" Version=""4.5.0"" />",
                ProjectType.Generator => @"
    <PackageReference Include=""Microsoft.CodeAnalysis.Analyzers"" Version=""3.3.4"" PrivateAssets=""all"" />
    <PackageReference Include=""Microsoft.CodeAnalysis.CSharp"" Version=""4.5.0"" PrivateAssets=""all"" />",
                ProjectType.Extension => @"
    <PackageReference Include=""System.Linq.Expressions"" Version=""4.3.0"" />",
                _ => ""
            };

            return commonPackages + typeSpecificPackages;
        }

        private string GetProjectReferences()
        {
            return _projectType switch
            {
                ProjectType.Core => @"    <ProjectReference Include=""..\..\DotCompute.Abstractions\DotCompute.Abstractions.csproj"" />",
                ProjectType.Backend => @"    <ProjectReference Include=""..\..\Core\DotCompute.Abstractions\DotCompute.Abstractions.csproj"" />
    <ProjectReference Include=""..\..\Core\DotCompute.Core\DotCompute.Core.csproj"" />",
                ProjectType.Runtime => @"    <ProjectReference Include=""..\..\Core\DotCompute.Abstractions\DotCompute.Abstractions.csproj"" />
    <ProjectReference Include=""..\..\Core\DotCompute.Core\DotCompute.Core.csproj"" />",
                ProjectType.Extension => @"    <ProjectReference Include=""..\..\Core\DotCompute.Abstractions\DotCompute.Abstractions.csproj"" />
    <ProjectReference Include=""..\..\Core\DotCompute.Core\DotCompute.Core.csproj"" />",
                _ => ""
            };
        }

        private string GetProjectDescription()
        {
            return _projectType switch
            {
                ProjectType.Core => "Core application services and use cases for DotCompute",
                ProjectType.Backend => $"Backend implementation for {_projectName.Split('.').Last()} compute devices",
                ProjectType.Runtime => "Runtime services and dependency injection for DotCompute",
                ProjectType.Plugin => "Plugin architecture and extensibility for DotCompute", 
                ProjectType.Extension => $"Extension library providing {_projectName.Split('.').Last()} functionality for DotCompute",
                ProjectType.Generator => "Source generation and code analysis tools for DotCompute",
                _ => $"DotCompute {_projectType} component"
            };
        }

        private void CreateGlobalUsings()
        {
            var globalUsingsContent = GenerateGlobalUsingsContent();
            var globalUsingsPath = Path.Combine(_targetPath, _projectName, "GlobalUsings.cs");
            File.WriteAllText(globalUsingsPath, globalUsingsContent);
        }

        private string GenerateGlobalUsingsContent()
        {
            var commonUsings = @"// Global using statements for " + _projectName + @"
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.DependencyInjection;";

            var projectSpecificUsings = _projectType switch
            {
                ProjectType.Core => @"
global using DotCompute.Abstractions.Interfaces;
global using DotCompute.Abstractions.Types;
global using DotCompute.Abstractions.Exceptions;",
                ProjectType.Backend => @"
global using DotCompute.Abstractions.Interfaces;
global using DotCompute.Abstractions.Types;
global using DotCompute.Abstractions.Exceptions;
global using DotCompute.Core.Interfaces;",
                ProjectType.Runtime => @"
global using DotCompute.Abstractions.Interfaces;
global using DotCompute.Core.Interfaces;",
                ProjectType.Extension => @"
global using DotCompute.Abstractions.Interfaces;
global using DotCompute.Core.Interfaces;",
                _ => ""
            };

            return commonUsings + projectSpecificUsings;
        }

        private void CreateTemplateFiles()
        {
            CreateInterfaceTemplate();
            CreateServiceTemplate();
            CreateExceptionTemplate();
            CreateConfigurationTemplate();
        }

        private void CreateInterfaceTemplate()
        {
            var interfaceName = $"I{_projectName.Split('.').Last()}Service";
            var content = $@"namespace {_projectName}.Interfaces;

/// <summary>
/// Main service interface for {_projectName}
/// </summary>
public interface {interfaceName}
{{
    /// <summary>
    /// Initializes the service with the specified configuration
    /// </summary>
    /// <param name=""configuration"">Service configuration</param>
    /// <param name=""cancellationToken"">Cancellation token</param>
    /// <returns>A task that represents the asynchronous initialization operation</returns>
    Task InitializeAsync(object configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of the service
    /// </summary>
    /// <returns>Service status information</returns>
    ValueTask<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}}
";

            var interfacePath = Path.Combine(_targetPath, _projectName, "Interfaces", $"{interfaceName}.cs");
            File.WriteAllText(interfacePath, content);
        }

        private void CreateServiceTemplate()
        {
            var serviceName = $"{_projectName.Split('.').Last()}Service";
            var interfaceName = $"I{serviceName}";
            
            var content = $@"using {_projectName}.Interfaces;

namespace {_projectName}.Services;

/// <summary>
/// Default implementation of {interfaceName}
/// </summary>
public class {serviceName} : {interfaceName}
{{
    private readonly ILogger<{serviceName}> _logger;
    private bool _isInitialized;

    /// <summary>
    /// Initializes a new instance of the <see cref=""{serviceName}""/> class
    /// </summary>
    /// <param name=""logger"">Logger instance</param>
    public {serviceName}(ILogger<{serviceName}> logger)
    {{
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }}

    /// <inheritdoc />
    public async Task InitializeAsync(object configuration, CancellationToken cancellationToken = default)
    {{
        _logger.LogInformation(""Initializing {{ServiceName}}"", nameof({serviceName}));
        
        // TODO: Implement initialization logic
        await Task.CompletedTask;
        
        _isInitialized = true;
        _logger.LogInformation(""{{ServiceName}} initialized successfully"", nameof({serviceName}));
    }}

    /// <inheritdoc />
    public ValueTask<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {{
        return ValueTask.FromResult(_isInitialized);
    }}
}}
";

            var servicePath = Path.Combine(_targetPath, _projectName, "Services", $"{serviceName}.cs");
            File.WriteAllText(servicePath, content);
        }

        private void CreateExceptionTemplate()
        {
            var exceptionName = $"{_projectName.Split('.').Last()}Exception";
            
            var content = $@"namespace {_projectName}.Exceptions;

/// <summary>
/// Exception thrown by {_projectName} components
/// </summary>
public class {exceptionName} : Exception
{{
    /// <summary>
    /// Initializes a new instance of the <see cref=""{exceptionName}""/> class
    /// </summary>
    public {exceptionName}()
    {{
    }}

    /// <summary>
    /// Initializes a new instance of the <see cref=""{exceptionName}""/> class with a specified error message
    /// </summary>
    /// <param name=""message"">The message that describes the error</param>
    public {exceptionName}(string message) : base(message)
    {{
    }}

    /// <summary>
    /// Initializes a new instance of the <see cref=""{exceptionName}""/> class with a specified error message and inner exception
    /// </summary>
    /// <param name=""message"">The message that describes the error</param>
    /// <param name=""innerException"">The exception that is the cause of the current exception</param>
    public {exceptionName}(string message, Exception innerException) : base(message, innerException)
    {{
    }}
}}
";

            var exceptionPath = Path.Combine(_targetPath, _projectName, "Exceptions", $"{exceptionName}.cs");
            File.WriteAllText(exceptionPath, content);
        }

        private void CreateConfigurationTemplate()
        {
            var configName = $"{_projectName.Split('.').Last()}Options";
            
            var content = $@"namespace {_projectName}.Configuration;

/// <summary>
/// Configuration options for {_projectName}
/// </summary>
public class {configName}
{{
    /// <summary>
    /// Gets or sets a value indicating whether the service is enabled
    /// </summary>
    public bool Enabled {{ get; set; }} = true;

    /// <summary>
    /// Gets or sets the timeout for operations in milliseconds
    /// </summary>
    public int TimeoutMs {{ get; set; }} = 30000;

    /// <summary>
    /// Gets or sets the log level for the service
    /// </summary>
    public LogLevel LogLevel {{ get; set; }} = LogLevel.Information;

    /// <summary>
    /// Validates the configuration options
    /// </summary>
    /// <returns>True if the configuration is valid; otherwise, false</returns>
    public bool IsValid()
    {{
        return TimeoutMs > 0;
    }}
}}
";

            var configPath = Path.Combine(_targetPath, _projectName, "Configuration", $"{configName}.cs");
            File.WriteAllText(configPath, content);
        }

        private void CreateReadme()
        {
            var content = $@"# {_projectName}

{GetProjectDescription()}

## Architecture

This project follows Clean Architecture principles and is organized into the following folders:

### Standard Folders

- **Interfaces/**: All contracts and abstractions (I*.cs files)
- **Enums/**: Enumerations and constants  
- **Types/**: Value objects, DTOs, and data structures
- **Models/**: Domain entities and data models
- **Configuration/**: Settings, options, and configuration classes
- **Exceptions/**: Custom exception classes
- **Services/**: Service implementations and business logic
- **Internal/**: Internal implementation details
- **Extensions/**: Extension methods
- **Utilities/**: Helper classes and utilities
- **Abstractions/**: Base classes and abstract implementations
- **Results/**: Result types and response objects
- **Validation/**: Validation logic and rules

{(GetProjectSpecificFolders().Count > 0 ? $@"### Project-Specific Folders

{string.Join("\n", GetProjectSpecificFolders().Select(f => $"- **{f}/**: {GetFolderDescription(f)}"))}
" : "")}

## Usage

### Basic Setup

```csharp
// Register services
services.AddTransient<I{_projectName.Split('.').Last()}Service, {_projectName.Split('.').Last()}Service>();

// Configure options
services.Configure<{_projectName.Split('.').Last()}Options>(options =>
{{
    options.Enabled = true;
    options.TimeoutMs = 30000;
}});
```

### Example Usage

```csharp
public class ExampleUsage
{{
    private readonly I{_projectName.Split('.').Last()}Service _service;

    public ExampleUsage(I{_projectName.Split('.').Last()}Service service)
    {{
        _service = service;
    }}

    public async Task DoSomethingAsync()
    {{
        await _service.InitializeAsync(new {_projectName.Split('.').Last()}Options());
        var isHealthy = await _service.IsHealthyAsync();
        // Use the service...
    }}
}}
```

## Dependencies

This project depends on:

{GetDependencyDocumentation()}

## Contributing

When adding new functionality:

1. **Follow the folder structure**: Place files in the appropriate folders
2. **Implement interfaces**: Always define interfaces in the Interfaces/ folder
3. **Add tests**: Include comprehensive unit tests
4. **Update documentation**: Keep this README current
5. **Follow naming conventions**: Use consistent naming patterns

## Architecture Principles

- **Dependency Inversion**: Depend on abstractions, not concretions
- **Single Responsibility**: Each class has one reason to change
- **Interface Segregation**: Prefer small, focused interfaces
- **Open/Closed**: Open for extension, closed for modification

## Testing

Run tests with:

```bash
dotnet test
```

## Building

Build the project with:

```bash
dotnet build
```

## License

This project is part of the DotCompute solution and follows the same licensing terms.
";

            var readmePath = Path.Combine(_targetPath, _projectName, "README.md");
            File.WriteAllText(readmePath, content);
        }

        private string GetFolderDescription(string folder)
        {
            return folder switch
            {
                "Planning" => "Execution planning and orchestration logic",
                "Metrics" => "Performance monitoring and metrics collection",
                "Recovery" => "Error recovery and resilience patterns",
                "Logging" => "Structured logging and diagnostic information",
                "Registration" => "Plugin registration and discovery",
                "Advanced" => "Advanced features and optimizations",
                "Templates" => "Code generation templates and patterns",
                _ => "Project-specific functionality"
            };
        }

        private string GetDependencyDocumentation()
        {
            return _projectType switch
            {
                ProjectType.Core => "- DotCompute.Abstractions (core interfaces)",
                ProjectType.Backend => "- DotCompute.Abstractions (core interfaces)\n- DotCompute.Core (application services)",
                ProjectType.Runtime => "- DotCompute.Abstractions (core interfaces)\n- DotCompute.Core (application services)",
                ProjectType.Extension => "- DotCompute.Abstractions (core interfaces)\n- DotCompute.Core (application services)",
                _ => "- Microsoft.Extensions.Logging.Abstractions\n- Microsoft.Extensions.DependencyInjection.Abstractions"
            };
        }
    }

    /// <summary>
    /// Defines the type of project being created
    /// </summary>
    public enum ProjectType
    {
        /// <summary>
        /// Core domain or application layer project
        /// </summary>
        Core,

        /// <summary>
        /// Backend implementation project (Infrastructure layer)
        /// </summary>
        Backend,

        /// <summary>
        /// Runtime services project
        /// </summary>
        Runtime,

        /// <summary>
        /// Plugin architecture project
        /// </summary>
        Plugin,

        /// <summary>
        /// Extension library project
        /// </summary>
        Extension,

        /// <summary>
        /// Code generation and analysis project
        /// </summary>
        Generator
    }
}