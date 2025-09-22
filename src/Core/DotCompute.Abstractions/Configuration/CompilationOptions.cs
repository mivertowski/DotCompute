// Copyright (c) 2025 Michael Ivertowski  
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;
using DotCompute.Abstractions.Types;

namespace DotCompute.Abstractions;

/// <summary>
/// Comprehensive compilation options for kernel compilation across different backends
/// </summary>
public class CompilationOptions
{
    /// <summary>
    /// Target optimization level
    /// </summary>
    public OptimizationLevel OptimizationLevel { get; set; } = OptimizationLevel.Default;

    /// <summary>
    /// Enable debug information generation
    /// </summary>
    public bool EnableDebugInfo { get; set; }


    /// <summary>
    /// Generate debug information (alias for EnableDebugInfo)
    /// </summary>
    public bool GenerateDebugInfo

    {
        get => EnableDebugInfo;
        set => EnableDebugInfo = value;
    }

    /// <summary>
    /// Generate line information for debugging
    /// </summary>
    public bool GenerateLineInfo { get; set; } = false;

    /// <summary>
    /// Enable profiling support during compilation and execution
    /// </summary>
    public bool EnableProfiling { get; set; }


    /// <summary>
    /// Enable fast math optimizations (may reduce precision)
    /// </summary>
    public bool EnableFastMath { get; set; } = true;


    /// <summary>
    /// Enable fast math optimizations (alias for compatibility)
    /// </summary>
    public bool FastMath
    {
        get => EnableFastMath;
        set => EnableFastMath = value;
    }


    /// <summary>
    /// Use fast math optimizations (alias for EnableFastMath)
    /// </summary>
    public bool UseFastMath
    {
        get => EnableFastMath;
        set => EnableFastMath = value;
    }

    /// <summary>
    /// Enable aggressive optimizations
    /// </summary>
    public bool AggressiveOptimizations { get; set; }


    /// <summary>
    /// Allow unsafe code during compilation
    /// </summary>
    public bool AllowUnsafeCode { get; set; }

    /// <summary>
    /// Target architecture specific optimizations
    /// </summary>
    public string? TargetArchitecture { get; set; }

    /// <summary>
    /// Compiler-specific defines
    /// </summary>
    public Dictionary<string, string> Defines { get; set; } = [];

    /// <summary>
    /// Include paths for headers
    /// </summary>
    public List<string> IncludePaths { get; set; } = [];

    /// <summary>
    /// Additional compiler flags
    /// </summary>
    public List<string> AdditionalFlags { get; set; } = [];

    /// <summary>
    /// Maximum compilation time before timeout
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan CompilationTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Enable warnings as errors
    /// </summary>
    public bool TreatWarningsAsErrors { get; set; }


    /// <summary>
    /// Warning level (0-4)
    /// </summary>
    [Range(0, 4)]
    public int WarningLevel { get; set; } = 2;

    /// <summary>
    /// Enable memory coalescing optimizations
    /// </summary>
    public bool EnableMemoryCoalescing { get; set; }



    /// <summary>
    /// Enable operator fusion optimizations
    /// </summary>
    public bool EnableOperatorFusion { get; set; }



    /// <summary>
    /// Enable parallel execution
    /// </summary>
    public bool EnableParallelExecution { get; set; }

    /// <summary>
    /// Enable dynamic parallelism support for CUDA kernels
    /// Allows kernels to launch other kernels from GPU code
    /// </summary>
    public bool EnableDynamicParallelism { get; set; }

    /// <summary>
    /// Enable shared memory register spilling for CUDA 13.0+ (Turing and newer)
    /// Helps reduce register pressure by spilling to shared memory instead of local memory
    /// </summary>
    public bool EnableSharedMemoryRegisterSpilling { get; set; } = true;

    /// <summary>
    /// Enable tile-based programming patterns for tensor cores (CUDA 13.0+)
    /// </summary>
    public bool EnableTileBasedProgramming { get; set; }

    /// <summary>
    /// Enable L2 cache residency control (CUDA 13.0+ on Ampere and newer)
    /// </summary>
    public bool EnableL2CacheResidencyControl { get; set; }

    /// <summary>
    /// Enable loop unrolling optimizations
    /// </summary>
    public bool EnableLoopUnrolling { get; set; } = true;

    /// <summary>
    /// Enable vectorization optimizations
    /// </summary>
    public bool EnableVectorization { get; set; } = true;

    /// <summary>
    /// Enable inline function optimizations
    /// </summary>
    public bool EnableInlining { get; set; } = true;

    /// <summary>
    /// Maximum register usage for GPU kernels
    /// </summary>
    [Range(1, 255)]
    public int? MaxRegisters { get; set; }

    /// <summary>
    /// Maximum registers per thread for GPU kernel execution (CUDA-specific)
    /// </summary>
    /// <remarks>
    /// Controls the maximum number of registers that each thread can use.
    /// Setting this to 0 allows the compiler to use any number of registers.
    /// Lower values can increase occupancy but may reduce performance.
    /// Typical values range from 32 to 255 registers per thread.
    /// </remarks>
    [Range(0, 255)]
    public int MaxRegistersPerThread { get; set; } = 0;

    /// <summary>
    /// Shared memory usage limit for GPU kernels (in bytes)
    /// </summary>
    [Range(0, int.MaxValue)]
    public int? SharedMemoryLimit { get; set; }

    /// <summary>
    /// Thread block size hint
    /// </summary>
    public int? ThreadBlockSize { get; set; }

    /// <summary>
    /// Preferred block dimensions for GPU kernel execution
    /// </summary>
    [Range(1, int.MaxValue)]
    public Dim3? PreferredBlockSize { get; set; } = new Dim3(256, 1, 1);

    /// <summary>
    /// Dynamic shared memory size allocation (in bytes)
    /// </summary>
    [Range(0, int.MaxValue)]
    public int? SharedMemorySize { get; set; } = 0;

    /// <summary>
    /// Enable loop unrolling optimizations
    /// </summary>
    public bool UnrollLoops { get; set; }


    /// <summary>
    /// Enable native math library usage
    /// </summary>
    public bool UseNativeMathLibrary { get; set; } = true;

    /// <summary>
    /// Floating point precision mode
    /// </summary>
    public FloatingPointMode FloatingPointMode { get; set; } = FloatingPointMode.Default;

    /// <summary>
    /// Enable profile-guided optimizations
    /// </summary>
    public bool EnableProfileGuidedOptimizations { get; set; }


    /// <summary>
    /// Profile data path for PGO
    /// </summary>
    public string? ProfileDataPath { get; set; }

    /// <summary>
    /// Enable strict floating point semantics
    /// </summary>
    public bool StrictFloatingPoint { get; set; }


    /// <summary>
    /// Compiler backend to use
    /// </summary>
    public string? CompilerBackend { get; set; }

    /// <summary>
    /// Force interpreted mode instead of compiled
    /// </summary>
    public bool ForceInterpretedMode { get; set; }

    /// <summary>
    /// Whether to compile to CUBIN format instead of PTX
    /// </summary>
    public bool CompileToCubin { get; set; }


    /// <summary>
    /// Enable fused multiply-add operations
    /// </summary>
    public bool FusedMultiplyAdd { get; set; } = true;


    /// <summary>
    /// Generate relocatable device code
    /// </summary>
    public bool RelocatableDeviceCode { get; set; }

    /// <summary>
    /// Target compute capability version for compilation
    /// </summary>
    public Version ComputeCapability { get; set; } = new Version(0, 0);


    /// <summary>
    /// Default compilation options
    /// </summary>
    public static CompilationOptions Default => new();

    /// <summary>
    /// Debug-optimized compilation options
    /// </summary>
    public static CompilationOptions Debug => new()
    {
        OptimizationLevel = OptimizationLevel.Minimal,
        EnableDebugInfo = true,
        EnableFastMath = false,
        TreatWarningsAsErrors = false,
        WarningLevel = 4
    };

    /// <summary>
    /// Release-optimized compilation options
    /// </summary>
    public static CompilationOptions Release => new()
    {
        OptimizationLevel = OptimizationLevel.Aggressive,
        EnableDebugInfo = false,
        EnableFastMath = true,
        EnableLoopUnrolling = true,
        EnableVectorization = true,
        EnableInlining = true,
        UseNativeMathLibrary = true,
        UnrollLoops = true,
        PreferredBlockSize = new Dim3(256, 1, 1),
        SharedMemorySize = 0
    };

    /// <summary>
    /// Creates a copy of the compilation options
    /// </summary>
    public CompilationOptions Clone()
    {
        return new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel,
            EnableDebugInfo = EnableDebugInfo,
            GenerateLineInfo = GenerateLineInfo,
            EnableFastMath = EnableFastMath,
            AggressiveOptimizations = AggressiveOptimizations,
            AllowUnsafeCode = AllowUnsafeCode,
            TargetArchitecture = TargetArchitecture,
            Defines = new Dictionary<string, string>(Defines),
            IncludePaths = [.. IncludePaths],
            AdditionalFlags = [.. AdditionalFlags],
            CompilationTimeout = CompilationTimeout,
            TreatWarningsAsErrors = TreatWarningsAsErrors,
            WarningLevel = WarningLevel,
            EnableLoopUnrolling = EnableLoopUnrolling,
            EnableVectorization = EnableVectorization,
            EnableInlining = EnableInlining,
            MaxRegisters = MaxRegisters,
            MaxRegistersPerThread = MaxRegistersPerThread,
            SharedMemoryLimit = SharedMemoryLimit,
            ThreadBlockSize = ThreadBlockSize,
            PreferredBlockSize = PreferredBlockSize,
            SharedMemorySize = SharedMemorySize,
            UnrollLoops = UnrollLoops,
            UseNativeMathLibrary = UseNativeMathLibrary,
            FloatingPointMode = FloatingPointMode,
            EnableProfileGuidedOptimizations = EnableProfileGuidedOptimizations,
            ProfileDataPath = ProfileDataPath,
            StrictFloatingPoint = StrictFloatingPoint,
            CompilerBackend = CompilerBackend,
            ForceInterpretedMode = ForceInterpretedMode,
            EnableProfiling = EnableProfiling,
            CompileToCubin = CompileToCubin,
            ComputeCapability = new Version(ComputeCapability.Major, ComputeCapability.Minor),
            FusedMultiplyAdd = FusedMultiplyAdd,
            RelocatableDeviceCode = RelocatableDeviceCode,
            EnableDynamicParallelism = EnableDynamicParallelism,
            EnableSharedMemoryRegisterSpilling = EnableSharedMemoryRegisterSpilling,
            EnableTileBasedProgramming = EnableTileBasedProgramming,
            EnableL2CacheResidencyControl = EnableL2CacheResidencyControl
        };
    }

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns>
    /// A <see cref="System.String" /> that represents this instance.
    /// </returns>
    public override string ToString()

        => $"OptLevel={OptimizationLevel}, FastMath={EnableFastMath}, Debug={EnableDebugInfo}, UnrollLoops={UnrollLoops}";
}
