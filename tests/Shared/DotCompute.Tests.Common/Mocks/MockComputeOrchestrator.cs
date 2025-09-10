// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Memory;
using DotCompute.Core.Kernels;

namespace DotCompute.Tests.Common.Mocks;

/// <summary>
/// Mock implementation of IComputeOrchestrator for pipeline testing.
/// Provides controllable behavior for testing scenarios.
/// </summary>
public class MockComputeOrchestrator : IComputeOrchestrator
{
    private readonly Dictionary<string, Func<object[], Task<object>>> _kernelMocks = new();
    private readonly List<KernelExecutionRecord> _executionHistory = new();

    /// <summary>
    /// Gets the execution history for verification in tests.
    /// </summary>
    public IReadOnlyList<KernelExecutionRecord> ExecutionHistory => _executionHistory;

    /// <summary>
    /// Registers a mock kernel implementation for testing.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to mock</param>
    /// <param name="implementation">Mock implementation function</param>
    public void RegisterMockKernel(string kernelName, Func<object[], Task<object>> implementation)
    {
        _kernelMocks[kernelName] = implementation;
    }

    /// <summary>
    /// Registers a synchronous mock kernel implementation.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to mock</param>
    /// <param name="implementation">Synchronous mock implementation function</param>
    public void RegisterMockKernel(string kernelName, Func<object[], object> implementation)
    {
        _kernelMocks[kernelName] = args => Task.FromResult(implementation(args));
    }

    /// <summary>
    /// Clears all registered mock kernels and execution history.
    /// </summary>
    public void Reset()
    {
        _kernelMocks.Clear();
        _executionHistory.Clear();
    }

    #region IComputeOrchestrator Implementation

    public async Task<T> ExecuteAsync<T>(string kernelName, params object[] args)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            if (!_kernelMocks.TryGetValue(kernelName, out var mockImplementation))
            {
                throw new InvalidOperationException($"Mock kernel '{kernelName}' not registered");
            }

            var result = await mockImplementation(args);
            
            // Record successful execution
            _executionHistory.Add(new KernelExecutionRecord
            {
                KernelName = kernelName,
                Arguments = args,
                StartTime = startTime,
                EndTime = DateTime.UtcNow,
                Success = true,
                Result = result
            });

            return (T)result;
        }
        catch (Exception ex)
        {
            // Record failed execution
            _executionHistory.Add(new KernelExecutionRecord
            {
                KernelName = kernelName,
                Arguments = args,
                StartTime = startTime,
                EndTime = DateTime.UtcNow,
                Success = false,
                Error = ex
            });
            
            throw;
        }
    }

    public async Task<T> ExecuteAsync<T>(string kernelName, string preferredBackend, params object[] args)
    {
        // Mock implementation ignores preferred backend for simplicity
        return await ExecuteAsync<T>(kernelName, args);
    }

    public async Task<T> ExecuteAsync<T>(string kernelName, IAccelerator accelerator, params object[] args)
    {
        // Mock implementation ignores specific accelerator for simplicity
        return await ExecuteAsync<T>(kernelName, args);
    }

    public async Task<T> ExecuteWithBuffersAsync<T>(string kernelName, IEnumerable<IUnifiedMemoryBuffer> buffers, params object[] scalarArgs)
    {
        // Convert buffers to objects for mock execution
        var allArgs = buffers.Cast<object>().Concat(scalarArgs).ToArray();
        return await ExecuteAsync<T>(kernelName, allArgs);
    }

    public Task<IAccelerator?> GetOptimalAcceleratorAsync(string kernelName)
    {
        // Mock returns null (no specific accelerator preference)
        return Task.FromResult<IAccelerator?>(null);
    }

    public Task PrecompileKernelAsync(string kernelName, IAccelerator? accelerator = null)
    {
        // Mock pre-compilation does nothing
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<IAccelerator>> GetSupportedAcceleratorsAsync(string kernelName)
    {
        // Mock returns empty list
        return Task.FromResult<IReadOnlyList<IAccelerator>>(Array.Empty<IAccelerator>());
    }

    public Task<bool> ValidateKernelArgsAsync(string kernelName, params object[] args)
    {
        // Mock validation always returns true if kernel is registered
        return Task.FromResult(_kernelMocks.ContainsKey(kernelName));
    }

    #endregion

    #region Legacy Methods (for backward compatibility with existing tests)

    /// <summary>
    /// Legacy method for backward compatibility. Use ExecuteAsync instead.
    /// </summary>
    [Obsolete("Use ExecuteAsync instead")]
    public async Task<T> ExecuteKernelAsync<T>(
        string kernelName, 
        object[] arguments, 
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync<T>(kernelName, arguments);
    }

    /// <summary>
    /// Legacy method for backward compatibility. Use ExecuteAsync instead.
    /// </summary>
    [Obsolete("Use ExecuteAsync instead")]
    public async Task<T> ExecuteKernelAsync<T>(
        KernelDefinition kernelDefinition, 
        object[] arguments, 
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync<T>(kernelDefinition.Name, arguments);
    }

    /// <summary>
    /// Legacy method for backward compatibility. Use ValidateKernelArgsAsync instead.
    /// </summary>
    [Obsolete("Use ValidateKernelArgsAsync instead")]
    public Task<bool> IsKernelAvailableAsync(string kernelName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_kernelMocks.ContainsKey(kernelName));
    }

    /// <summary>
    /// Legacy method for backward compatibility. Use GetSupportedAcceleratorsAsync instead.
    /// </summary>
    [Obsolete("This method is no longer supported")]
    public Task<IEnumerable<string>> GetAvailableKernelsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<string>>(_kernelMocks.Keys);
    }

    #endregion

    /// <summary>
    /// Creates a mock for vector addition operation.
    /// </summary>
    /// <returns>Mock function for vector addition</returns>
    public static Func<object[], object> CreateVectorAddMock()
    {
        return args =>
        {
            if (args.Length != 3)
                throw new ArgumentException("VectorAdd requires 3 arguments");
                
            var a = (float[])args[0];
            var b = (float[])args[1];
            var result = (float[])args[2];
            
            if (a.Length != b.Length || a.Length != result.Length)
                throw new ArgumentException("Array lengths must match");
                
            for (int i = 0; i < a.Length; i++)
            {
                result[i] = a[i] + b[i];
            }
            
            return result;
        };
    }

    /// <summary>
    /// Creates a mock for vector multiplication operation.
    /// </summary>
    /// <returns>Mock function for vector multiplication</returns>
    public static Func<object[], object> CreateVectorMultiplyMock()
    {
        return args =>
        {
            if (args.Length != 3)
                throw new ArgumentException("VectorMultiply requires 3 arguments");
                
            var a = (float[])args[0];
            var b = (float[])args[1];
            var result = (float[])args[2];
            
            if (a.Length != b.Length || a.Length != result.Length)
                throw new ArgumentException("Array lengths must match");
                
            for (int i = 0; i < a.Length; i++)
            {
                result[i] = a[i] * b[i];
            }
            
            return result;
        };
    }

    /// <summary>
    /// Creates a mock for aggregation/reduction operations.
    /// </summary>
    /// <returns>Mock function for aggregation</returns>
    public static Func<object[], object> CreateAggregateMock()
    {
        return args =>
        {
            if (args.Length != 2)
                throw new ArgumentException("Aggregate requires 2 arguments");
                
            var input = (float[])args[0];
            var operation = (Func<float, float, float>)args[1];
            
            return input.Aggregate(operation);
        };
    }

    /// <summary>
    /// Creates a mock that simulates slow execution for performance testing.
    /// </summary>
    /// <param name="delayMs">Delay in milliseconds</param>
    /// <returns>Mock function with simulated delay</returns>
    public static Func<object[], Task<object>> CreateSlowMock(int delayMs)
    {
        return async args =>
        {
            await Task.Delay(delayMs);
            return args[0]; // Just return first argument
        };
    }

    /// <summary>
    /// Creates a mock that throws an exception for error handling testing.
    /// </summary>
    /// <param name="exceptionType">Type of exception to throw</param>
    /// <param name="message">Exception message</param>
    /// <returns>Mock function that throws exception</returns>
    public static Func<object[], object> CreateErrorMock([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type exceptionType, string message = "Mock error")
    {
        return _ => throw (Exception)Activator.CreateInstance(exceptionType, message)!;
    }
}

/// <summary>
/// Record of kernel execution for test verification.
/// </summary>
public class KernelExecutionRecord
{
    /// <summary>
    /// Gets the name of the executed kernel.
    /// </summary>
    public required string KernelName { get; init; }

    /// <summary>
    /// Gets the arguments passed to the kernel.
    /// </summary>
    public required object[] Arguments { get; init; }

    /// <summary>
    /// Gets the execution start time.
    /// </summary>
    public required DateTime StartTime { get; init; }

    /// <summary>
    /// Gets the execution end time.
    /// </summary>
    public required DateTime EndTime { get; init; }

    /// <summary>
    /// Gets the execution duration.
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>
    /// Gets whether the execution was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the execution result (if successful).
    /// </summary>
    public object? Result { get; init; }

    /// <summary>
    /// Gets the execution error (if failed).
    /// </summary>
    public Exception? Error { get; init; }
}