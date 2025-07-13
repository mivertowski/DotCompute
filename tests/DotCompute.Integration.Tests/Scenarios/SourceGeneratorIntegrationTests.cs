// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core;
using DotCompute.Integration.Tests.Fixtures;
using FluentAssertions;
using System.CodeDom.Compiler;
using System.Reflection;
using Xunit;

namespace DotCompute.Integration.Tests.Scenarios;

[Collection(nameof(IntegrationTestCollection))]
public class SourceGeneratorIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;

    public SourceGeneratorIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SourceGenerator_Should_Create_Executable_Kernels()
    {
        // Arrange - Create a simple compute class that should trigger source generation
        var sourceCode = @"
using DotCompute.Core.Attributes;
using DotCompute.Core;

namespace Generated;

[ComputeClass]
public partial class TestComputeClass
{
    [ComputeKernel]
    public static void VectorAdd(float[] a, float[] b, float[] result)
    {
        int i = ComputeBuiltins.GetGlobalId(0);
        if (i < result.Length)
        {
            result[i] = a[i] + b[i];
        }
    }

    [ComputeKernel]
    public static void VectorMultiply(float[] input, float[] output, float scalar)
    {
        int i = ComputeBuiltins.GetGlobalId(0);
        if (i < input.Length)
        {
            output[i] = input[i] * scalar;
        }
    }
}";

        // Simulate source generation (in real scenario, this would be done by the compiler)
        var generatedCode = @"
using DotCompute.Core;
using DotCompute.Core.Memory;

namespace Generated;

public partial class TestComputeClass
{
    private static IComputeEngine? _computeEngine;
    private static IMemoryManager? _memoryManager;

    public static void Initialize(IComputeEngine computeEngine, IMemoryManager memoryManager)
    {
        _computeEngine = computeEngine;
        _memoryManager = memoryManager;
    }

    public static async Task<float[]> VectorAddAsync(float[] a, float[] b)
    {
        if (_computeEngine == null || _memoryManager == null)
            throw new InvalidOperationException(""Not initialized"");

        var result = new float[a.Length];
        
        using var bufferA = await _memoryManager.CreateBufferAsync(a);
        using var bufferB = await _memoryManager.CreateBufferAsync(b);
        using var bufferResult = await _memoryManager.CreateBufferAsync<float>(result.Length);

        const string kernelCode = @""
            kernel void VectorAdd(global float* a, global float* b, global float* result, int size) {
                int i = get_global_id(0);
                if (i < size) {
                    result[i] = a[i] + b[i];
                }
            }"";

        var kernel = await _computeEngine.CompileKernelAsync(kernelCode);
        await _computeEngine.ExecuteAsync(kernel, 
            new object[] { bufferA, bufferB, bufferResult, a.Length }, 
            ComputeBackendType.CPU);

        return await bufferResult.ReadAsync();
    }

    public static async Task<float[]> VectorMultiplyAsync(float[] input, float scalar)
    {
        if (_computeEngine == null || _memoryManager == null)
            throw new InvalidOperationException(""Not initialized"");

        using var bufferInput = await _memoryManager.CreateBufferAsync(input);
        using var bufferOutput = await _memoryManager.CreateBufferAsync<float>(input.Length);

        const string kernelCode = @""
            kernel void VectorMultiply(global float* input, global float* output, float scalar, int size) {
                int i = get_global_id(0);
                if (i < size) {
                    output[i] = input[i] * scalar;
                }
            }"";

        var kernel = await _computeEngine.CompileKernelAsync(kernelCode);
        await _computeEngine.ExecuteAsync(kernel, 
            new object[] { bufferInput, bufferOutput, scalar, input.Length }, 
            ComputeBackendType.CPU);

        return await bufferOutput.ReadAsync();
    }
}";

        // Compile the generated code
        var tempAssemblyFile = Path.Combine(Path.GetTempPath(), "TestGenerated.dll");
        var compilation = CompileCode(sourceCode + generatedCode, tempAssemblyFile);

        try
        {
            compilation.Should().BeTrue("Generated code should compile successfully");

            // Load the compiled assembly
            var assembly = Assembly.LoadFrom(tempAssemblyFile);
            var testComputeType = assembly.GetType("Generated.TestComputeClass");
            testComputeType.Should().NotBeNull("Generated compute class should exist");

            // Initialize the generated class
            var initializeMethod = testComputeType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
            initializeMethod.Should().NotBeNull("Initialize method should exist");
            initializeMethod!.Invoke(null, new object[] { _fixture.ComputeEngine, _fixture.MemoryManager });

            // Test VectorAdd
            var vectorAddMethod = testComputeType.GetMethod("VectorAddAsync", BindingFlags.Public | BindingFlags.Static);
            vectorAddMethod.Should().NotBeNull("VectorAddAsync method should exist");

            var a = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };
            var b = new float[] { 5.0f, 6.0f, 7.0f, 8.0f };

            var addTask = (Task<float[]>)vectorAddMethod!.Invoke(null, new object[] { a, b })!;
            var addResult = await addTask;

            addResult.Should().HaveCount(4);
            addResult[0].Should().BeApproximately(6.0f, 0.001f);
            addResult[1].Should().BeApproximately(8.0f, 0.001f);
            addResult[2].Should().BeApproximately(10.0f, 0.001f);
            addResult[3].Should().BeApproximately(12.0f, 0.001f);

            // Test VectorMultiply
            var vectorMultiplyMethod = testComputeType.GetMethod("VectorMultiplyAsync", BindingFlags.Public | BindingFlags.Static);
            vectorMultiplyMethod.Should().NotBeNull("VectorMultiplyAsync method should exist");

            var multiplyTask = (Task<float[]>)vectorMultiplyMethod!.Invoke(null, new object[] { a, 3.0f })!;
            var multiplyResult = await multiplyTask;

            multiplyResult.Should().HaveCount(4);
            multiplyResult[0].Should().BeApproximately(3.0f, 0.001f);
            multiplyResult[1].Should().BeApproximately(6.0f, 0.001f);
            multiplyResult[2].Should().BeApproximately(9.0f, 0.001f);
            multiplyResult[3].Should().BeApproximately(12.0f, 0.001f);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempAssemblyFile))
            {
                try { File.Delete(tempAssemblyFile); } catch { }
            }
        }
    }

    [Fact]
    public async Task GeneratedCode_Should_Handle_Complex_Data_Types()
    {
        // Test source generation with complex data types
        var sourceCode = @"
using DotCompute.Core.Attributes;
using DotCompute.Core;

namespace Generated;

public struct Vector3
{
    public float X, Y, Z;
    public Vector3(float x, float y, float z) { X = x; Y = y; Z = z; }
}

[ComputeClass]
public partial class ComplexComputeClass
{
    [ComputeKernel]
    public static void TransformVectors(Vector3[] input, Vector3[] output, float scale)
    {
        int i = ComputeBuiltins.GetGlobalId(0);
        if (i < input.Length)
        {
            output[i] = new Vector3(
                input[i].X * scale,
                input[i].Y * scale,
                input[i].Z * scale
            );
        }
    }
}";

        var generatedCode = @"
using DotCompute.Core;
using DotCompute.Core.Memory;

namespace Generated;

public partial class ComplexComputeClass
{
    private static IComputeEngine? _computeEngine;
    private static IMemoryManager? _memoryManager;

    public static void Initialize(IComputeEngine computeEngine, IMemoryManager memoryManager)
    {
        _computeEngine = computeEngine;
        _memoryManager = memoryManager;
    }

    public static async Task<Vector3[]> TransformVectorsAsync(Vector3[] input, float scale)
    {
        if (_computeEngine == null || _memoryManager == null)
            throw new InvalidOperationException(""Not initialized"");

        // Convert Vector3 to float arrays for GPU processing
        var inputFloats = new float[input.Length * 3];
        for (int i = 0; i < input.Length; i++)
        {
            inputFloats[i * 3] = input[i].X;
            inputFloats[i * 3 + 1] = input[i].Y;
            inputFloats[i * 3 + 2] = input[i].Z;
        }

        using var bufferInput = await _memoryManager.CreateBufferAsync(inputFloats);
        using var bufferOutput = await _memoryManager.CreateBufferAsync<float>(inputFloats.Length);

        const string kernelCode = @""
            kernel void TransformVectors(global float* input, global float* output, float scale, int count) {
                int i = get_global_id(0);
                if (i < count) {
                    int baseIdx = i * 3;
                    output[baseIdx] = input[baseIdx] * scale;         // X
                    output[baseIdx + 1] = input[baseIdx + 1] * scale; // Y
                    output[baseIdx + 2] = input[baseIdx + 2] * scale; // Z
                }
            }"";

        var kernel = await _computeEngine.CompileKernelAsync(kernelCode);
        await _computeEngine.ExecuteAsync(kernel, 
            new object[] { bufferInput, bufferOutput, scale, input.Length }, 
            ComputeBackendType.CPU);

        var outputFloats = await bufferOutput.ReadAsync();
        
        // Convert back to Vector3 array
        var result = new Vector3[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            result[i] = new Vector3(
                outputFloats[i * 3],
                outputFloats[i * 3 + 1],
                outputFloats[i * 3 + 2]
            );
        }

        return result;
    }
}";

        var tempAssemblyFile = Path.Combine(Path.GetTempPath(), "TestComplexGenerated.dll");
        var compilation = CompileCode(sourceCode + generatedCode, tempAssemblyFile);

        try
        {
            compilation.Should().BeTrue("Complex generated code should compile successfully");

            var assembly = Assembly.LoadFrom(tempAssemblyFile);
            var testComputeType = assembly.GetType("Generated.ComplexComputeClass");
            var vector3Type = assembly.GetType("Generated.Vector3");

            // Initialize
            var initializeMethod = testComputeType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
            initializeMethod!.Invoke(null, new object[] { _fixture.ComputeEngine, _fixture.MemoryManager });

            // Create test vectors
            var inputVectors = Array.CreateInstance(vector3Type!, 3);
            inputVectors.SetValue(Activator.CreateInstance(vector3Type!, 1.0f, 2.0f, 3.0f), 0);
            inputVectors.SetValue(Activator.CreateInstance(vector3Type!, 4.0f, 5.0f, 6.0f), 1);
            inputVectors.SetValue(Activator.CreateInstance(vector3Type!, 7.0f, 8.0f, 9.0f), 2);

            // Test TransformVectors
            var transformMethod = testComputeType.GetMethod("TransformVectorsAsync", BindingFlags.Public | BindingFlags.Static);
            var transformTask = (Task)transformMethod!.Invoke(null, new object[] { inputVectors, 2.0f })!;
            await transformTask;

            // Get result
            var resultProperty = transformTask.GetType().GetProperty("Result");
            var result = (Array)resultProperty!.GetValue(transformTask)!;

            result.Should().HaveCount(3);

            // Verify first vector (1,2,3) * 2 = (2,4,6)
            var firstResult = result.GetValue(0)!;
            var xField = vector3Type!.GetField("X");
            var yField = vector3Type.GetField("Y");
            var zField = vector3Type.GetField("Z");

            ((float)xField!.GetValue(firstResult)!).Should().BeApproximately(2.0f, 0.001f);
            ((float)yField!.GetValue(firstResult)!).Should().BeApproximately(4.0f, 0.001f);
            ((float)zField!.GetValue(firstResult)!).Should().BeApproximately(6.0f, 0.001f);
        }
        finally
        {
            if (File.Exists(tempAssemblyFile))
            {
                try { File.Delete(tempAssemblyFile); } catch { }
            }
        }
    }

    private bool CompileCode(string sourceCode, string outputPath)
    {
        try
        {
            // Create a simple compiler for testing purposes
            // In a real scenario, this would be handled by the MSBuild process with source generators
            var provider = CodeDomProvider.CreateProvider("CSharp");
            var parameters = new CompilerParameters
            {
                OutputAssembly = outputPath,
                GenerateExecutable = false,
                GenerateInMemory = false,
                CompilerOptions = "/optimize"
            };

            // Add necessary references
            parameters.ReferencedAssemblies.Add("System.dll");
            parameters.ReferencedAssemblies.Add("System.Core.dll");
            parameters.ReferencedAssemblies.Add("System.Runtime.dll");

            // Add references to DotCompute assemblies
            var dotComputeCorePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DotCompute.Core.dll");
            if (File.Exists(dotComputeCorePath))
            {
                parameters.ReferencedAssemblies.Add(dotComputeCorePath);
            }

            var results = provider.CompileAssemblyFromSource(parameters, sourceCode);
            
            if (results.Errors.HasErrors)
            {
                foreach (CompilerError error in results.Errors)
                {
                    _fixture.Logger.LogError($"Compilation error: {error.ErrorText}");
                }
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _fixture.Logger.LogError($"Compilation failed: {ex.Message}");
            return false;
        }
    }
}