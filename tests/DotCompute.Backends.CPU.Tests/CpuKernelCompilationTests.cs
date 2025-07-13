// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotCompute.Backends.CPU.Accelerators;
using DotCompute.Backends.CPU.Kernels;
using DotCompute.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DotCompute.Backends.CPU.Tests;

/// <summary>
/// Tests for CPU kernel compilation functionality.
/// </summary>
public class CpuKernelCompilationTests
{
    private readonly ILogger<CpuKernelCompiler> _logger;

    public CpuKernelCompilationTests()
    {
        _logger = new TestLogger<CpuKernelCompiler>();
    }

    [Fact]
    public async Task CompileAsync_WithTextKernelSource_ShouldSucceed()
    {
        // Arrange
        var kernelSource = @"
            kernel void vector_add(global float* a, global float* b, global float* c) {
                int idx = get_global_id(0);
                c[idx] = a[idx] + b[idx];
            }";

        var definition = new KernelDefinition
        {
            Name = "vector_add",
            Source = new TextKernelSource { Code = kernelSource, Language = "opencl" },
            WorkDimensions = 1,
            Parameters = new List<KernelParameter>
            {
                new() { Name = "a", Type = KernelParameterType.Buffer, ElementType = typeof(float), Access = Memory.MemoryAccess.ReadOnly },
                new() { Name = "b", Type = KernelParameterType.Buffer, ElementType = typeof(float), Access = Memory.MemoryAccess.ReadOnly },
                new() { Name = "c", Type = KernelParameterType.Buffer, ElementType = typeof(float), Access = Memory.MemoryAccess.WriteOnly }
            }
        };

        var compiler = new CpuKernelCompiler();
        var context = new CpuKernelCompilationContext
        {
            Definition = definition,
            Options = CompilationOptions.Default,
            SimdCapabilities = new Intrinsics.SimdSummary
            {
                IsHardwareAccelerated = true,
                PreferredVectorWidth = 256,
                SupportsAvx2 = true,
                SupportedInstructionSets = new HashSet<string> { "AVX2", "SSE4.2" }
            },
            ThreadPool = new Threading.CpuThreadPool(4, _logger),
            Logger = _logger
        };

        // Act
        var compiledKernel = await compiler.CompileAsync(context);

        // Assert
        compiledKernel.Should().NotBeNull();
        compiledKernel.Definition.Should().Be(definition);
        compiledKernel.Name.Should().Be("vector_add");
        compiledKernel.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CompileAsync_WithOptimizationLevels_ShouldApplyOptimizations()
    {
        // Arrange
        var definition = CreateSimpleKernelDefinition();
        var compiler = new CpuKernelCompiler();

        var optimizationLevels = new[]
        {
            OptimizationLevel.None,
            OptimizationLevel.Debug,
            OptimizationLevel.Release,
            OptimizationLevel.Maximum
        };

        foreach (var level in optimizationLevels)
        {
            var context = new CpuKernelCompilationContext
            {
                Definition = definition,
                Options = new CompilationOptions { OptimizationLevel = level },
                SimdCapabilities = new Intrinsics.SimdSummary
                {
                    IsHardwareAccelerated = true,
                    PreferredVectorWidth = 256,
                    SupportsAvx2 = true,
                    SupportedInstructionSets = new HashSet<string> { "AVX2" }
                },
                ThreadPool = new Threading.CpuThreadPool(4, _logger),
                Logger = _logger
            };

            // Act
            var compiledKernel = await compiler.CompileAsync(context);

            // Assert
            compiledKernel.Should().NotBeNull();
            
            // Check metadata for optimization info
            compiledKernel.Definition.Metadata.Should().ContainKey("OptimizationNotes");
            var notes = compiledKernel.Definition.Metadata!["OptimizationNotes"] as string[];
            notes.Should().NotBeNull();

            if (level >= OptimizationLevel.Release)
            {
                notes.Should().Contain(n => n.Contains("vectorization") || n.Contains("optimiz"));
            }
        }
    }

    [Fact]
    public async Task CompileAsync_WithComplexKernel_ShouldAnalyzeCorrectly()
    {
        // Arrange
        var kernelSource = @"
            kernel void matrix_multiply(global float* a, global float* b, global float* c, int width) {
                int row = get_global_id(0);
                int col = get_global_id(1);
                float sum = 0.0f;
                
                for (int k = 0; k < width; k++) {
                    sum += a[row * width + k] * b[k * width + col];
                }
                
                c[row * width + col] = sum;
            }";

        var definition = new KernelDefinition
        {
            Name = "matrix_multiply",
            Source = new TextKernelSource { Code = kernelSource, Language = "opencl" },
            WorkDimensions = 2,
            Parameters = new List<KernelParameter>
            {
                new() { Name = "a", Type = KernelParameterType.Buffer, ElementType = typeof(float) },
                new() { Name = "b", Type = KernelParameterType.Buffer, ElementType = typeof(float) },
                new() { Name = "c", Type = KernelParameterType.Buffer, ElementType = typeof(float) },
                new() { Name = "width", Type = KernelParameterType.Scalar, ElementType = typeof(int) }
            }
        };

        var compiler = new CpuKernelCompiler();
        var context = CreateCompilationContext(definition);

        // Act
        var compiledKernel = await compiler.CompileAsync(context);

        // Assert
        compiledKernel.Should().NotBeNull();
        
        // The kernel should be analyzed as having loops
        var metadata = compiledKernel.Definition.Metadata;
        metadata.Should().NotBeNull();
        metadata.Should().ContainKey("CompilationTime");
    }

    [Fact]
    public void ValidateKernelDefinition_WithInvalidDefinition_ShouldFail()
    {
        // Arrange
        var invalidDefinitions = new[]
        {
            new KernelDefinition
            {
                Name = "", // Empty name
                Source = new TextKernelSource { Code = "test", Language = "c" },
                WorkDimensions = 1,
                Parameters = new List<KernelParameter> { new() { Name = "a", Type = KernelParameterType.Buffer } }
            },
            new KernelDefinition
            {
                Name = "test",
                Source = null!, // Null source
                WorkDimensions = 1,
                Parameters = new List<KernelParameter> { new() { Name = "a", Type = KernelParameterType.Buffer } }
            },
            new KernelDefinition
            {
                Name = "test",
                Source = new TextKernelSource { Code = "test", Language = "c" },
                WorkDimensions = 4, // Invalid dimensions
                Parameters = new List<KernelParameter> { new() { Name = "a", Type = KernelParameterType.Buffer } }
            },
            new KernelDefinition
            {
                Name = "test",
                Source = new TextKernelSource { Code = "test", Language = "c" },
                WorkDimensions = 1,
                Parameters = new List<KernelParameter>() // No parameters
            }
        };

        var compiler = new CpuKernelCompiler();

        foreach (var definition in invalidDefinitions)
        {
            var context = CreateCompilationContext(definition);

            // Act & Assert
            Func<Task> act = async () => await compiler.CompileAsync(context);
            act.Should().ThrowAsync<InvalidOperationException>();
        }
    }

    [Fact]
    public async Task CompileAsync_WithBytecodeSource_ShouldSucceed()
    {
        // Arrange
        var bytecode = new byte[] { 0x01, 0x02, 0x03, 0x04 }; // Mock bytecode
        var definition = new KernelDefinition
        {
            Name = "bytecode_kernel",
            Source = new BytecodeKernelSource { Bytecode = bytecode, Format = "custom" },
            WorkDimensions = 1,
            Parameters = new List<KernelParameter>
            {
                new() { Name = "buffer", Type = KernelParameterType.Buffer, ElementType = typeof(float) }
            }
        };

        var compiler = new CpuKernelCompiler();
        var context = CreateCompilationContext(definition);

        // Act
        var compiledKernel = await compiler.CompileAsync(context);

        // Assert
        compiledKernel.Should().NotBeNull();
        compiledKernel.Source.Should().Be("[Bytecode]");
    }

    private KernelDefinition CreateSimpleKernelDefinition()
    {
        return new KernelDefinition
        {
            Name = "simple_add",
            Source = new TextKernelSource 
            { 
                Code = "c[idx] = a[idx] + b[idx];", 
                Language = "c" 
            },
            WorkDimensions = 1,
            Parameters = new List<KernelParameter>
            {
                new() { Name = "a", Type = KernelParameterType.Buffer, ElementType = typeof(float) },
                new() { Name = "b", Type = KernelParameterType.Buffer, ElementType = typeof(float) },
                new() { Name = "c", Type = KernelParameterType.Buffer, ElementType = typeof(float) }
            }
        };
    }

    private CpuKernelCompilationContext CreateCompilationContext(KernelDefinition definition)
    {
        return new CpuKernelCompilationContext
        {
            Definition = definition,
            Options = CompilationOptions.Default,
            SimdCapabilities = new Intrinsics.SimdSummary
            {
                IsHardwareAccelerated = true,
                PreferredVectorWidth = 256,
                SupportsAvx2 = true,
                SupportedInstructionSets = new HashSet<string> { "AVX2", "SSE4.2" }
            },
            ThreadPool = new Threading.CpuThreadPool(4, _logger),
            Logger = _logger
        };
    }

    private class TestLogger<T> : ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state) => null!;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            // Simple console output for testing
            Console.WriteLine($"[{logLevel}] {formatter(state, exception)}");
        }
    }
}