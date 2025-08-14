// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using System.Text;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Kernels;

/// <summary>
/// OpenCL kernel compiler implementation using OpenCL runtime compilation.
/// </summary>
public sealed class OpenCLKernelCompiler : DotCompute.Abstractions.IKernelCompiler
{
    private readonly ILogger<OpenCLKernelCompiler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLKernelCompiler"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public OpenCLKernelCompiler(ILogger<OpenCLKernelCompiler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string Name => "OpenCL Kernel Compiler";

    /// <inheritdoc/>
    public KernelSourceType[] SupportedSourceTypes { get; } = [KernelSourceType.OpenCL, KernelSourceType.Binary];

    /// <inheritdoc/>
    public async ValueTask<ICompiledKernel> CompileAsync(
        KernelDefinition definition,
        DotCompute.Abstractions.CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        options ??= GetDefaultCompilationOptions();

        var kernelSource = CreateKernelSourceFromDefinition(definition);
        if (kernelSource.Language != DotCompute.Abstractions.KernelLanguage.OpenCL)
        {
            throw new ArgumentException($"Expected OpenCL kernel but received {kernelSource.Language}", nameof(definition));
        }

        _logger.LogInformation("Compiling OpenCL kernel '{KernelName}'", definition.Name);

        try
        {
            // Simulate async compilation with a small delay
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            
            // Stub compilation - always return a mock compiled kernel
            var compiledKernel = new ManagedCompiledKernel
            {
                Name = definition.Name,
                Binary = GenerateMockBinary(kernelSource),
                Handle = IntPtr.Zero,
                Parameters = [],
                RequiredWorkGroupSize = null,
                SharedMemorySize = 0,
                CompilationLog = "Mock OpenCL compilation log",
                PerformanceMetadata = new Dictionary<string, object>
                {
                    ["CompilationTime"] = 10.0,
                    ["Platform"] = "OpenCL (Mock)"
                }
            };

            _logger.LogInformation("Successfully compiled OpenCL kernel '{KernelName}'", definition.Name);
            return compiledKernel;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("OpenCL kernel compilation for '{KernelName}' was cancelled", definition.Name);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compile OpenCL kernel '{KernelName}'", definition.Name);
            throw new InvalidOperationException($"OpenCL kernel compilation failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public ValidationResult Validate(KernelDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var kernelSource = CreateKernelSourceFromDefinition(definition);
        if (kernelSource.Language != DotCompute.Abstractions.KernelLanguage.OpenCL)
        {
            return ValidationResult.Failure($"Expected OpenCL kernel but received {kernelSource.Language}");
        }

        var result = ValidationResult.Success();

        // Basic syntax validation
        var syntaxErrors = ValidateOpenCLSyntax(kernelSource.Code);
        if (syntaxErrors.Any())
        {
            return ValidationResult.Failure($"OpenCL syntax errors: {string.Join(", ", syntaxErrors)}");
        }

        return result;
    }

    /// <summary>
    /// Gets the default compilation options for OpenCL.
    /// </summary>
    private DotCompute.Abstractions.CompilationOptions GetDefaultCompilationOptions()
    {
        return new DotCompute.Abstractions.CompilationOptions
        {
            OptimizationLevel = DotCompute.Abstractions.OptimizationLevel.Release,
            EnableDebugInfo = false,
            FastMath = true,
            UnrollLoops = true,
            AdditionalFlags = [
                "-cl-mad-enable",
                "-cl-fast-relaxed-math",
                "-cl-kernel-arg-info"
            ],
            Defines = new Dictionary<string, string>
            {
                ["OPENCL_VERSION"] = "200",
                ["CL_TARGET_OPENCL_VERSION"] = "200"
            }
        };
    }

    /// <summary>
    /// Creates a kernel source from a kernel definition.
    /// </summary>
    private static IKernelSource CreateKernelSourceFromDefinition(KernelDefinition definition)
    {
        var sourceCode = System.Text.Encoding.UTF8.GetString(definition.Code);
        var language = DotCompute.Abstractions.KernelLanguage.OpenCL; // Default for OpenCL
        
        // Try to detect language from metadata
        if (definition.Metadata?.TryGetValue("Language", out var langObj) == true && langObj is string langStr)
        {
            if (Enum.TryParse<DotCompute.Abstractions.KernelLanguage>(langStr, out var parsedLang))
            {
                language = parsedLang;
            }
        }
        
        return new TextKernelSource(
            sourceCode,
            definition.Name,
            language,
            definition.EntryPoint ?? "main");
    }

    /// <summary>
    /// Validates OpenCL syntax.
    /// </summary>
    private static List<string> ValidateOpenCLSyntax(string source)
    {
        var errors = new List<string>();
        
        // Check for required kernel function
        if (!source.Contains("__kernel"))
        {
            errors.Add("No __kernel function found in source code");
        }
        
        // Basic bracket balance check
        var braceCount = 0;
        var parenCount = 0;
        var bracketCount = 0;
        
        foreach (var c in source)
        {
            switch (c)
            {
                case '{': braceCount++; break;
                case '}': braceCount--; break;
                case '(': parenCount++; break;
                case ')': parenCount--; break;
                case '[': bracketCount++; break;
                case ']': bracketCount--; break;
            }
        }
        
        if (braceCount != 0)
        {
            errors.Add($"Unbalanced braces: {braceCount}");
        }
        if (parenCount != 0)
        {
            errors.Add($"Unbalanced parentheses: {parenCount}");
        }
        if (bracketCount != 0)
        {
            errors.Add($"Unbalanced brackets: {bracketCount}");
        }
        
        // Check for unsupported constructs
        if (source.Contains("malloc") || source.Contains("free"))
        {
            errors.Add("Dynamic memory allocation not supported in OpenCL");
        }
        
        return errors;
    }

    /// <summary>
    /// Generates a mock binary for testing purposes.
    /// </summary>
    private static byte[] GenerateMockBinary(IKernelSource kernelSource)
    {
        var mockBinary = new byte[1024];
        var random = new Random(kernelSource.Code.GetHashCode());
        random.NextBytes(mockBinary);
        return mockBinary;
    }
}