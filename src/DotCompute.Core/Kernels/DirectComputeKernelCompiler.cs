// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using System.Text;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Kernels;

/// <summary>
/// Production DirectCompute kernel compiler implementation using D3DCompile and DirectX 11 compute shaders.
/// Supports Windows 7+ with DirectX 11 runtime and D3DCompiler.
/// </summary>
public sealed class DirectComputeKernelCompiler : IKernelCompiler
{
    private readonly ILogger<DirectComputeKernelCompiler> _logger;
    private readonly bool _isSupported;

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectComputeKernelCompiler"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public DirectComputeKernelCompiler(ILogger<DirectComputeKernelCompiler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Check if DirectCompute is supported on this platform
        _isSupported = CheckDirectComputeSupport();
        
        if (_isSupported)
        {
            _logger.LogInformation("DirectCompute support detected - using production implementation");
        }
        else
        {
            _logger.LogWarning("DirectCompute not supported on this platform - functionality will be limited");
        }
    }

    /// <inheritdoc/>
    public AcceleratorType AcceleratorType => AcceleratorType.DirectML; // Using DirectML as the closest match for DirectCompute

    /// <inheritdoc/>
    public async ValueTask<ManagedCompiledKernel> CompileAsync(GeneratedKernel kernel, CompilationOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(options);

        if (kernel.Language != KernelLanguage.DirectCompute)
        {
            throw new ArgumentException($"Expected DirectCompute kernel but received {kernel.Language}", nameof(kernel));
        }

        _logger.LogInformation("Compiling DirectCompute kernel '{KernelName}'", kernel.Name);

        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

#if WINDOWS
            if (_isSupported)
            {
                return await CompileWithD3DCompileAsync(kernel, options, cancellationToken);
            }
#endif
            // Fall back to stub implementation for unsupported platforms
            _logger.LogWarning("Falling back to stub implementation for kernel '{KernelName}'", kernel.Name);
            return await CompileStubAsync(kernel, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compile DirectCompute kernel '{KernelName}'", kernel.Name);
            throw new InvalidOperationException($"DirectCompute kernel compilation failed: {ex.Message}", ex);
        }
    }

#if WINDOWS
    /// <summary>
    /// Compiles a kernel using the real D3DCompile API.
    /// </summary>
    private async ValueTask<ManagedCompiledKernel> CompileWithD3DCompileAsync(GeneratedKernel kernel, CompilationOptions options, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Extract entry point from HLSL source or use default
        var entryPoint = ExtractEntryPoint(kernel.Source) ?? "CSMain";
        var shaderTarget = options.TargetArchitecture ?? "cs_5_0";
        
        // Convert compilation options to D3D compile flags
        var compileFlags = GetD3DCompileFlags(options);
        
        try
        {
            // Perform compilation on thread pool to avoid blocking
            var (bytecode, log) = await Task.Run(() =>
                DirectComputeInterop.CompileComputeShader(kernel.Source, entryPoint, shaderTarget, compileFlags),
                cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation("DirectCompute kernel '{KernelName}' compiled successfully in {ElapsedMs}ms", 
                kernel.Name, stopwatch.ElapsedMilliseconds);

            var compiledKernel = new ManagedCompiledKernel
            {
                Name = kernel.Name,
                Binary = bytecode,
                Handle = IntPtr.Zero, // Will be set during execution setup
                Parameters = kernel.Parameters,
                RequiredWorkGroupSize = kernel.RequiredWorkGroupSize,
                SharedMemorySize = kernel.SharedMemorySize,
                CompilationLog = log,
                PerformanceMetadata = new Dictionary<string, object>
                {
                    ["CompilationTime"] = stopwatch.ElapsedMilliseconds,
                    ["Platform"] = "DirectCompute",
                    ["ShaderTarget"] = shaderTarget,
                    ["EntryPoint"] = entryPoint,
                    ["BytecodeSize"] = bytecode.Length,
                    ["OptimizationLevel"] = options.OptimizationLevel.ToString(),
                    ["CompileFlags"] = compileFlags
                }
            };

            return compiledKernel;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "D3DCompile failed for kernel '{KernelName}' after {ElapsedMs}ms", 
                kernel.Name, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
    
    /// <summary>
    /// Converts compilation options to D3D compile flags.
    /// </summary>
    private static uint GetD3DCompileFlags(CompilationOptions options)
    {
        uint flags = 0;
        
        // Debug information
        if (options.GenerateDebugInfo)
        {
            flags |= DirectComputeInterop.D3DCOMPILE_DEBUG;
            flags |= DirectComputeInterop.D3DCOMPILE_SKIP_OPTIMIZATION;
        }
        else
        {
            // Optimization level
            switch (options.OptimizationLevel)
            {
                case OptimizationLevel.O0:
                    flags |= DirectComputeInterop.D3DCOMPILE_SKIP_OPTIMIZATION;
                    break;
                case OptimizationLevel.O1:
                    flags |= DirectComputeInterop.D3DCOMPILE_OPTIMIZATION_LEVEL1;
                    break;
                case OptimizationLevel.O2:
                    flags |= DirectComputeInterop.D3DCOMPILE_OPTIMIZATION_LEVEL2;
                    break;
                case OptimizationLevel.O3:
                    flags |= DirectComputeInterop.D3DCOMPILE_OPTIMIZATION_LEVEL3;
                    break;
            }
        }
        
        // Matrix packing preference
        if (options.AdditionalFlags?.Contains("row_major") == true)
        {
            flags |= DirectComputeInterop.D3DCOMPILE_PACK_MATRIX_ROW_MAJOR;
        }
        else
        {
            flags |= DirectComputeInterop.D3DCOMPILE_PACK_MATRIX_COLUMN_MAJOR;
        }
        
        // Partial precision
        if (options.EnableFastMath)
        {
            flags |= DirectComputeInterop.D3DCOMPILE_PARTIAL_PRECISION;
        }
        
        return flags;
    }
    
    /// <summary>
    /// Extracts the entry point function name from HLSL source code.
    /// </summary>
    private static string? ExtractEntryPoint(string source)
    {
        // Look for [numthreads(...)] attribute followed by function definition
        var lines = source.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        for (var i = 0; i < lines.Length - 1; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("[numthreads(") && line.EndsWith("]"))
            {
                // Check next non-empty line for function signature
                for (var j = i + 1; j < lines.Length; j++)
                {
                    var nextLine = lines[j].Trim();
                    if (string.IsNullOrWhiteSpace(nextLine))
                    {
                        continue;
                    }

                    // Look for void functionName(...) pattern
                    var match = System.Text.RegularExpressions.Regex.Match(nextLine, @"void\s+([\w_][\w\d_]*)\s*\(");
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                    break;
                }
            }
        }
        
        return null;
    }
#endif
    
    /// <summary>
    /// Fallback stub compilation for unsupported platforms.
    /// </summary>
    private async ValueTask<ManagedCompiledKernel> CompileStubAsync(GeneratedKernel kernel, CompilationOptions options, CancellationToken cancellationToken)
    {
        // Simulate compilation time
        await Task.Delay(30, cancellationToken);

        var mockBytecode = GenerateMockHLSLBytecode(kernel, options);
        var log = GenerateStubCompilationLog(kernel, options);

        var compiledKernel = new ManagedCompiledKernel
        {
            Name = kernel.Name,
            Binary = mockBytecode,
            Handle = IntPtr.Zero,
            Parameters = kernel.Parameters,
            RequiredWorkGroupSize = kernel.RequiredWorkGroupSize,
            SharedMemorySize = kernel.SharedMemorySize,
            CompilationLog = log,
            PerformanceMetadata = new Dictionary<string, object>
            {
                ["CompilationTime"] = 30.0,
                ["IsStubImplementation"] = true,
                ["Platform"] = "DirectCompute (Stub)",
                ["SharedMemoryUsed"] = kernel.SharedMemorySize,
                ["OptimizationLevel"] = options.OptimizationLevel.ToString(),
                ["ShaderModel"] = options.TargetArchitecture ?? "cs_5_0"
            }
        };

        return compiledKernel;
    }

    /// <inheritdoc/>
    public KernelValidationResult Validate(GeneratedKernel kernel)
    {
        ArgumentNullException.ThrowIfNull(kernel);

        if (kernel.Language != KernelLanguage.DirectCompute)
        {
            return new KernelValidationResult
            {
                IsValid = false,
                Errors =
                [
                    new ValidationError
                    {
                        Code = "INVALID_LANGUAGE",
                        Message = $"Expected DirectCompute kernel but received {kernel.Language}"
                    }
                ]
            };
        }

        var warnings = new List<ValidationWarning>();
        var errors = new List<ValidationError>();

        // Platform support check
        if (!_isSupported)
        {
            warnings.Add(new ValidationWarning
            {
                Code = "PLATFORM_NOT_SUPPORTED",
                Message = "DirectCompute is not supported on this platform. Stub implementation will be used.",
                Severity = WarningSeverity.Serious
            });
        }
        
        // Basic validation for DirectCompute/HLSL kernels
        
        // Check for compute shader entry point
        if (!kernel.Source.Contains("[numthreads("))
        {
            errors.Add(new ValidationError
            {
                Code = "MISSING_NUMTHREADS_ATTRIBUTE",
                Message = "No [numthreads] attribute found in DirectCompute shader"
            });
        }

        // Validate HLSL syntax basics
        ValidateHLSLSyntax(kernel.Source, errors, warnings);

        // Check thread group size limits (DirectCompute-specific)
        if (kernel.RequiredWorkGroupSize != null)
        {
            ValidateThreadGroupSize(kernel.RequiredWorkGroupSize, errors, warnings);
        }

        // Estimate resource usage (simplified for DirectCompute)
        var resourceUsage = new ResourceUsageEstimate
        {
            RegistersPerThread = 32, // DirectCompute typical estimate
            SharedMemoryPerBlock = kernel.SharedMemorySize,
            ConstantMemoryUsage = 0,
            MaxThreadsPerBlock = 1024, // DirectCompute typical limit
            OccupancyEstimate = _isSupported ? 0.85f : 0.0f
        };

        return new KernelValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            ResourceUsage = resourceUsage
        };
    }

    /// <inheritdoc/>
    public CompilationOptions GetDefaultOptions()
    {
        var shaderModel = DetectOptimalShaderModel();
        var featureLevel = DetectDirectXFeatureLevel();
        
        return new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.O2,
            GenerateDebugInfo = false,
            EnableFastMath = true,
            FiniteMathOnly = true,
            EnableUnsafeOptimizations = false,
            TargetArchitecture = shaderModel,
            AdditionalFlags =
            [
                "/O3",                      // Maximum optimization
                "/Gfp",                     // Prefer flow control constructs
                "/Qstrip_reflect",          // Strip reflection information
                "/Qstrip_debug",            // Strip debug information in release
                "/all_resources_bound",     // Assume all resources are bound
                "/enable_unbounded_descriptor_tables", // Enable unbounded descriptor tables
                "/res_may_alias",           // Resources may alias
                "/Ges"                       // Enable strict mode
            ],
            IncludeDirectories =
            [
                "C:\\Program Files (x86)\\Windows Kits\\10\\Include\\10.0.22621.0\\um",
                "C:\\Program Files (x86)\\Windows Kits\\10\\Include\\10.0.22621.0\\shared",
                "C:\\Program Files (x86)\\Microsoft DirectX SDK (June 2010)\\Include"
            ],
            Defines = new Dictionary<string, string>
            {
                ["DIRECTCOMPUTE"] = "1",
                ["HLSL"] = "1",
                ["SHADER_MODEL"] = GetShaderModelDefine(shaderModel),
                ["FEATURE_LEVEL"] = GetFeatureLevelDefine(featureLevel),
                ["MAX_THREAD_GROUP_SIZE"] = "1024",
                ["WARP_SIZE"] = "32",
                ["DIRECTX_VERSION"] = "12"
            }
        };
    }
    
    /// <summary>
    /// Detects the optimal shader model for the current system.
    /// </summary>
    private string DetectOptimalShaderModel()
    {
        if (!_isSupported)
        {
            return "cs_5_0";
        }
        
        try
        {
#if WINDOWS
            var (device, context, featureLevel) = DirectComputeInterop.CreateDevice();
            
            if (device != IntPtr.Zero)
            {
                var shaderModel = featureLevel switch
                {
                    var level when level >= DirectComputeInterop.D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_12_1 => "cs_6_6",
                    var level when level >= DirectComputeInterop.D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_12_0 => "cs_6_0",
                    var level when level >= DirectComputeInterop.D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_1 => "cs_5_1",
                    _ => "cs_5_0"
                };
                
                Marshal.Release(context);
                Marshal.Release(device);
                return shaderModel;
            }
#endif
        }
        catch
        {
            // Fall back to default
        }
        
        return "cs_5_0";
    }
    
    /// <summary>
    /// Detects the DirectX feature level.
    /// </summary>
    private string DetectDirectXFeatureLevel()
    {
        try
        {
#if WINDOWS
            var (device, context, featureLevel) = DirectComputeInterop.CreateDevice();
            
            if (device != IntPtr.Zero)
            {
                var levelString = featureLevel switch
                {
                    var level when level >= DirectComputeInterop.D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_12_1 => "12_1",
                    var level when level >= DirectComputeInterop.D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_12_0 => "12_0",
                    var level when level >= DirectComputeInterop.D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_1 => "11_1",
                    _ => "11_0"
                };
                
                Marshal.Release(context);
                Marshal.Release(device);
                return levelString;
            }
#endif
        }
        catch
        {
            // Fall back to default
        }
        
        return "11_0";
    }
    
    /// <summary>
    /// Gets the shader model define value.
    /// </summary>
    private static string GetShaderModelDefine(string shaderModel)
    {
        return shaderModel switch
        {
            "cs_6_6" => "66",
            "cs_6_0" => "60",
            "cs_5_1" => "51",
            _ => "50"
        };
    }
    
    /// <summary>
    /// Gets the feature level define value.
    /// </summary>
    private static string GetFeatureLevelDefine(string featureLevel)
    {
        return featureLevel.Replace("_", ""); // "11_0" -> "110"
    }

    /// <summary>
    /// Checks if DirectCompute is supported on the current platform.
    /// </summary>
    private static bool CheckDirectComputeSupport()
    {
#if WINDOWS
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }
        
        try
        {
            // Try to create a DirectCompute device to verify support
            var (device, context, featureLevel) = DirectComputeInterop.CreateDevice();
            
            if (device != IntPtr.Zero)
            {
                Marshal.Release(context);
                Marshal.Release(device);
                return featureLevel >= DirectComputeInterop.D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0;
            }
        }
        catch
        {
            // DirectCompute not available
        }
#endif
        return false;
    }

    /// <summary>
    /// Validates comprehensive HLSL syntax and DirectCompute constructs with advanced analysis.
    /// </summary>
    private static void ValidateHLSLSyntax(string source, List<ValidationError> errors, List<ValidationWarning> warnings)
    {
        var lines = source.Split('\n');
        var braceCount = 0;
        var parenCount = 0;
        var bracketCount = 0;
        var inString = false;
        var inComment = false;
        var inBlockComment = false;
        var resourceBindings = new Dictionary<string, int>();
        var barrierLocations = new List<int>();
        var threadGroupSizes = new List<(int line, string size)>();
        
        for (var lineNum = 0; lineNum < lines.Length; lineNum++)
        {
            var line = lines[lineNum];
            var actualLineNum = lineNum + 1;
            var trimmedLine = line.Trim();
            
            // Parse numthreads attributes
            if (trimmedLine.StartsWith("[numthreads(") && trimmedLine.EndsWith(")]"))
            {
                var threadsSpec = trimmedLine.Substring(12, trimmedLine.Length - 14);
                threadGroupSizes.Add((actualLineNum, threadsSpec));
            }
            
            // Track balance and syntax
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                var prev = i > 0 ? line[i - 1] : '\0';
                var next = i < line.Length - 1 ? line[i + 1] : '\0';
                
                // Handle string literals
                if (c == '"' && prev != '\\' && !inComment && !inBlockComment)
                {
                    inString = !inString;
                    continue;
                }
                
                if (inString) continue;
                
                // Handle comments
                if (c == '/' && next == '/' && !inBlockComment)
                {
                    inComment = true;
                    continue;
                }
                
                if (c == '/' && next == '*' && !inComment)
                {
                    inBlockComment = true;
                    i++; // Skip the '*'
                    continue;
                }
                
                if (c == '*' && next == '/' && inBlockComment)
                {
                    inBlockComment = false;
                    i++; // Skip the '/'
                    continue;
                }
                
                if (inComment || inBlockComment) continue;
                
                // Track bracket balance
                switch (c)
                {
                    case '{':
                        braceCount++;
                        break;
                    case '}':
                        braceCount--;
                        if (braceCount < 0)
                        {
                            errors.Add(new ValidationError
                            {
                                Code = "UNBALANCED_BRACES",
                                Message = "Unbalanced closing brace",
                                Line = actualLineNum,
                                Column = i + 1
                            });
                        }
                        break;
                    case '(':
                        parenCount++;
                        break;
                    case ')':
                        parenCount--;
                        if (parenCount < 0)
                        {
                            errors.Add(new ValidationError
                            {
                                Code = "UNBALANCED_PARENTHESES",
                                Message = "Unbalanced closing parenthesis",
                                Line = actualLineNum,
                                Column = i + 1
                            });
                        }
                        break;
                    case '[':
                        bracketCount++;
                        break;
                    case ']':
                        bracketCount--;
                        if (bracketCount < 0)
                        {
                            errors.Add(new ValidationError
                            {
                                Code = "UNBALANCED_BRACKETS",
                                Message = "Unbalanced closing bracket",
                                Line = actualLineNum,
                                Column = i + 1
                            });
                        }
                        break;
                }
            }
            
            inComment = false; // Line comments end at line break
            
            // Line-specific validations
            if (trimmedLine.Contains("malloc") || trimmedLine.Contains("free"))
            {
                errors.Add(new ValidationError
                {
                    Code = "DYNAMIC_ALLOCATION_NOT_SUPPORTED",
                    Message = "Dynamic memory allocation is not supported in DirectCompute shaders",
                    Line = actualLineNum
                });
            }
            
            if (trimmedLine.Contains("recursion") || (trimmedLine.Contains("function") && trimmedLine.Contains("return")))
            {
                warnings.Add(new ValidationWarning
                {
                    Code = "RECURSION_WARNING",
                    Message = "Recursion should be avoided in DirectCompute shaders",
                    Line = actualLineNum,
                    Severity = WarningSeverity.Serious
                });
            }
            
            // Resource binding analysis
            if (trimmedLine.Contains("Texture") || trimmedLine.Contains("Buffer") || trimmedLine.Contains("StructuredBuffer"))
            {
                if (trimmedLine.Contains("register("))
                {
                    // Extract register binding
                    var registerMatch = System.Text.RegularExpressions.Regex.Match(trimmedLine, @"register\([^)]+\)");
                    if (registerMatch.Success)
                    {
                        var binding = registerMatch.Value;
                        if (resourceBindings.ContainsKey(binding))
                        {
                            errors.Add(new ValidationError
                            {
                                Code = "DUPLICATE_REGISTER_BINDING",
                                Message = $"Duplicate register binding {binding} found",
                                Line = actualLineNum
                            });
                        }
                        else
                        {
                            resourceBindings[binding] = actualLineNum;
                        }
                    }
                }
                else
                {
                    warnings.Add(new ValidationWarning
                    {
                        Code = "MISSING_REGISTER_BINDING",
                        Message = "Resources should specify explicit register bindings for optimal performance",
                        Line = actualLineNum,
                        Severity = WarningSeverity.Warning
                    });
                }
            }
            
            // Memory barrier analysis
            if (trimmedLine.Contains("GroupMemoryBarrier") || trimmedLine.Contains("AllMemoryBarrier") ||
                trimmedLine.Contains("DeviceMemoryBarrier"))
            {
                barrierLocations.Add(actualLineNum);
                warnings.Add(new ValidationWarning
                {
                    Code = "MEMORY_BARRIER_USAGE",
                    Message = "Memory barriers can impact performance - ensure they are necessary",
                    Line = actualLineNum,
                    Severity = WarningSeverity.Info
                });
            }
            
            // Shared memory (groupshared) analysis
            if (trimmedLine.Contains("groupshared"))
            {
                warnings.Add(new ValidationWarning
                {
                    Code = "GROUPSHARED_USAGE",
                    Message = "Group shared memory usage detected - ensure optimal access patterns",
                    Line = actualLineNum,
                    Severity = WarningSeverity.Info
                });
            }
            
            // UAV (Unordered Access View) usage
            if (trimmedLine.Contains("RWTexture") || trimmedLine.Contains("RWBuffer") ||
                trimmedLine.Contains("RWStructuredBuffer"))
            {
                warnings.Add(new ValidationWarning
                {
                    Code = "UAV_USAGE",
                    Message = "UAV resources require careful synchronization for correct results",
                    Line = actualLineNum,
                    Severity = WarningSeverity.Info
                });
            }
            
            // Texture sampling optimization hints
            if (trimmedLine.Contains(".Sample") || trimmedLine.Contains(".Load"))
            {
                warnings.Add(new ValidationWarning
                {
                    Code = "TEXTURE_SAMPLING",
                    Message = "Consider texture sampling patterns for optimal cache usage",
                    Line = actualLineNum,
                    Severity = WarningSeverity.Info
                });
            }
            
            // Branch analysis for compute shaders
            if (trimmedLine.StartsWith("if") && (trimmedLine.Contains("SV_DispatchThreadID") || trimmedLine.Contains("SV_GroupThreadID")))
            {
                warnings.Add(new ValidationWarning
                {
                    Code = "THREAD_DIVERGENCE",
                    Message = "Thread-dependent branching may cause performance issues",
                    Line = actualLineNum,
                    Severity = WarningSeverity.Warning
                });
            }
        }
        
        // Final balance checks
        if (braceCount > 0)
        {
            errors.Add(new ValidationError
            {
                Code = "UNBALANCED_BRACES",
                Message = $"Missing {braceCount} closing brace(s)"
            });
        }
        
        if (parenCount > 0)
        {
            errors.Add(new ValidationError
            {
                Code = "UNBALANCED_PARENTHESES",
                Message = $"Missing {parenCount} closing parenthesis(es)"
            });
        }
        
        if (bracketCount > 0)
        {
            errors.Add(new ValidationError
            {
                Code = "UNBALANCED_BRACKETS",
                Message = $"Missing {bracketCount} closing bracket(s)"
            });
        }
        
        // Thread group size analysis
        foreach (var (line, size) in threadGroupSizes)
        {
            var dimensions = size.Split(',').Select(s => s.Trim()).ToArray();
            if (dimensions.Length != 3)
            {
                warnings.Add(new ValidationWarning
                {
                    Code = "THREAD_GROUP_DIMENSIONS",
                    Message = "Thread group should specify all three dimensions (x, y, z)",
                    Line = line,
                    Severity = WarningSeverity.Info
                });
            }
            
            // Calculate total threads
            if (dimensions.All(d => int.TryParse(d, out _)))
            {
                var totalThreads = dimensions.Select(int.Parse).Aggregate(1, (a, b) => a * b);
                if (totalThreads > 1024)
                {
                    errors.Add(new ValidationError
                    {
                        Code = "THREAD_GROUP_SIZE_EXCEEDED",
                        Message = $"Total thread group size {totalThreads} exceeds DirectCompute maximum of 1024",
                        Line = line
                    });
                }
                
                if (totalThreads % 32 != 0)
                {
                    warnings.Add(new ValidationWarning
                    {
                        Code = "SUBOPTIMAL_THREAD_GROUP_SIZE",
                        Message = $"Thread group size {totalThreads} is not a multiple of warp size (32)",
                        Line = line,
                        Severity = WarningSeverity.Warning
                    });
                }
            }
        }
        
        // Barrier usage analysis
        if (barrierLocations.Count > 2)
        {
            warnings.Add(new ValidationWarning
            {
                Code = "EXCESSIVE_BARRIERS",
                Message = $"Multiple memory barriers detected at lines {string.Join(", ", barrierLocations)} - consider optimization",
                Severity = WarningSeverity.Warning
            });
        }
    }

    /// <summary>
    /// Validates DirectCompute thread group size.
    /// </summary>
    private static void ValidateThreadGroupSize(int[] threadGroupSize, List<ValidationError> errors, List<ValidationWarning> warnings)
    {
        if (threadGroupSize.Length > 3)
        {
            errors.Add(new ValidationError
            {
                Code = "INVALID_THREAD_GROUP_DIMENSIONS",
                Message = "DirectCompute thread group can have at most 3 dimensions"
            });
        }

        // DirectCompute thread group limits
        if (threadGroupSize.Length > 0 && threadGroupSize[0] > 1024)
        {
            errors.Add(new ValidationError
            {
                Code = "THREAD_GROUP_X_TOO_LARGE",
                Message = "Thread group X dimension exceeds DirectCompute maximum of 1024"
            });
        }

        if (threadGroupSize.Length > 1 && threadGroupSize[1] > 1024)
        {
            errors.Add(new ValidationError
            {
                Code = "THREAD_GROUP_Y_TOO_LARGE",
                Message = "Thread group Y dimension exceeds DirectCompute maximum of 1024"
            });
        }

        if (threadGroupSize.Length > 2 && threadGroupSize[2] > 64)
        {
            errors.Add(new ValidationError
            {
                Code = "THREAD_GROUP_Z_TOO_LARGE",
                Message = "Thread group Z dimension exceeds DirectCompute maximum of 64"
            });
        }

        // Total thread group size limit
        var totalSize = threadGroupSize.Aggregate(1, (a, b) => a * b);
        if (totalSize > 1024)
        {
            errors.Add(new ValidationError
            {
                Code = "TOTAL_THREAD_GROUP_SIZE_TOO_LARGE",
                Message = $"Total thread group size {totalSize} exceeds DirectCompute maximum of 1024"
            });
        }

        // Warn about suboptimal sizes
        if (totalSize > 0 && totalSize % 32 != 0)
        {
            warnings.Add(new ValidationWarning
            {
                Code = "SUBOPTIMAL_THREAD_GROUP_SIZE",
                Message = $"Thread group size {totalSize} is not a multiple of warp size (32), which may reduce performance",
                Severity = WarningSeverity.Warning
            });
        }
    }

    /// <summary>
    /// Generates mock HLSL bytecode for stub implementation.
    /// </summary>
    private static byte[] GenerateMockHLSLBytecode(GeneratedKernel kernel, CompilationOptions options)
    {
        // Mock DirectX shader bytecode - in reality this would be compiled DXBC
        var mockBytecode = new byte[1536]; // Typical size for compute shader bytecode
        
        // DXBC header magic (mock)
        mockBytecode[0] = 0x44; // 'D'
        mockBytecode[1] = 0x58; // 'X'
        mockBytecode[2] = 0x42; // 'B'
        mockBytecode[3] = 0x43; // 'C'
        
        // Fill rest with deterministic data based on kernel content
        var hash = kernel.Source.GetHashCode() ^ kernel.Name.GetHashCode();
        var random = new Random(hash);
        random.NextBytes(mockBytecode.AsSpan(4));
        
        return mockBytecode;
    }

    /// <summary>
    /// Generates compilation log for stub implementation.
    /// </summary>
    private static string GenerateStubCompilationLog(GeneratedKernel kernel, CompilationOptions options)
    {
        var log = new StringBuilder();
        log.AppendLine($"DirectCompute Shader Compilation Log (Stub Implementation) for '{kernel.Name}'");
        log.AppendLine($"Shader Model: {options.TargetArchitecture ?? "cs_5_0"}");
        log.AppendLine($"Optimization Level: {options.OptimizationLevel}");
        log.AppendLine($"Fast Math: {options.EnableFastMath}");
        log.AppendLine($"Debug Info: {options.GenerateDebugInfo}");
        log.AppendLine();
        log.AppendLine("WARNING: This is a stub implementation of the DirectCompute compiler.");
        log.AppendLine("DirectCompute is not supported on this platform or runtime.");
        log.AppendLine();
        log.AppendLine("Stub compilation successful.");
        log.AppendLine($"Kernel parameters: {kernel.Parameters.Length}");
        log.AppendLine($"Shared memory usage: {kernel.SharedMemorySize} bytes");
        
        if (kernel.RequiredWorkGroupSize != null)
        {
            log.AppendLine($"Thread group size: [{string.Join(", ", kernel.RequiredWorkGroupSize)}]");
        }

        log.AppendLine();
        log.AppendLine("For real DirectCompute support, ensure:");
        log.AppendLine("  - Windows platform with DirectX 11 runtime");
        log.AppendLine("  - DirectX-capable GPU with compute shader support");
        log.AppendLine("  - D3DCompiler_47.dll available in system");

        return log.ToString();
    }
}