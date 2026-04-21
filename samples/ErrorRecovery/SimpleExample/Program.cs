// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.ErrorHandling;
using DotCompute.Backends.CUDA.Native.Exceptions;
using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Samples.ErrorRecovery;

/// <summary>
/// Error-recovery sample - demonstrates how to use the CudaException classification
/// model to drive a simple retry policy. The classification is the same one Ring Kernels
/// and IComputeOrchestrator use under the covers; this sample shows the call site
/// directly so you can see how it works without a real GPU.
///
/// Every CUDA error maps to exactly one of four classes:
///   Transient  - retry without changing state (NotReady, Timeout, ...)
///   Resource   - free memory / wait, then retry (OOM, OutOfResources, ...)
///   Programmer - caller bug, do not retry (InvalidValue, NotSupported, ...)
///   Fatal      - device is hosed, fail fast (IllegalAddress, NoDevice, ...)
///
/// This sample runs on any machine - no GPU required. It fabricates CudaException
/// instances and walks them through a retry policy so you can see the decisions the
/// framework makes without triggering real GPU faults.
/// </summary>
internal static class Program
{
    private static async Task<int> Main()
    {
        Console.WriteLine("===========================================================");
        Console.WriteLine("  DotCompute Error Recovery - Classification & Retry Demo");
        Console.WriteLine("===========================================================");
        Console.WriteLine();

        PrintClassificationTable();

        // Scenario 1: A transient error recovers on the first retry.
        Console.WriteLine("-- Scenario 1: transient error (cudaErrorNotReady) --");
        var attemptCounter1 = 0;
        var result1 = await ExecuteWithRetryAsync(
            operationName: "LaunchVectorAdd",
            maxAttempts: 3,
            operation: attempt =>
            {
                attemptCounter1 = attempt;
                if (attempt == 1)
                {
                    throw new CudaException(
                        "Stream not ready on first attempt",
                        CudaError.NotReady);
                }
                return "Kernel completed";
            });
        Console.WriteLine($"Result after {attemptCounter1} attempt(s): {result1}");
        Console.WriteLine();

        // Scenario 2: A resource error - caller drains caches between retries.
        Console.WriteLine("-- Scenario 2: resource error (cudaErrorMemoryAllocation) --");
        var cacheSize = 4;
        var attemptCounter2 = 0;
        var result2 = await ExecuteWithRetryAsync(
            operationName: "AllocateHugeBuffer",
            maxAttempts: 6,
            operation: attempt =>
            {
                attemptCounter2 = attempt;
                if (cacheSize > 0)
                {
                    cacheSize--;
                    throw new CudaException(
                        $"cudaMalloc failed (attempt {attempt}, cache still held {cacheSize + 1} blocks)",
                        CudaError.MemoryAllocation);
                }
                return "Allocation succeeded after cache drain";
            },
            onResource: () =>
            {
                Console.WriteLine($"  [recovery] freeing 1 cached buffer (cache size now {cacheSize})");
            });
        Console.WriteLine($"Result after {attemptCounter2} attempt(s): {result2}");
        Console.WriteLine();

        // Scenario 3: A programmer error - the retry loop must NOT retry.
        Console.WriteLine("-- Scenario 3: programmer error (cudaErrorInvalidValue) --");
        try
        {
            _ = await ExecuteWithRetryAsync<string>(
                operationName: "LaunchBadArgs",
                maxAttempts: 5,
                operation: _ => throw new CudaException(
                    "Negative kernel argument passed to launch",
                    CudaError.InvalidValue));
        }
        catch (CudaException ex)
        {
            Console.WriteLine($"Surfaced to caller (correct): {ex.ErrorCode} - {ex.Message}");
        }
        Console.WriteLine();

        // Scenario 4: A fatal error - circuit-break, do NOT retry.
        Console.WriteLine("-- Scenario 4: fatal error (cudaErrorIllegalAddress) --");
        try
        {
            _ = await ExecuteWithRetryAsync<string>(
                operationName: "BrokenKernel",
                maxAttempts: 5,
                operation: _ => throw new CudaException(
                    "Kernel wrote to unmapped address",
                    CudaError.IllegalAddress));
        }
        catch (CudaException ex)
        {
            Console.WriteLine($"Surfaced to caller (circuit-breaker open): {ex.ErrorCode} - {ex.Message}");
            Console.WriteLine("  Classification: " + ex.Classification);
            Console.WriteLine("  IsFatal:        " + ex.IsFatal);
            Console.WriteLine("  IsRetryable:    " + ex.IsRetryable);
        }
        Console.WriteLine();

        Console.WriteLine("Done. See CudaErrorClassification for the full error->class mapping.");
        return 0;
    }

    /// <summary>
    /// Minimal retry policy driven entirely by CudaException classification.
    /// Real production code would add exponential backoff, jitter, and circuit-breaker
    /// state; this version is deliberately tiny so you can read it top-to-bottom quickly.
    /// </summary>
    private static async Task<T> ExecuteWithRetryAsync<T>(
        string operationName,
        int maxAttempts,
        Func<int, T> operation,
        Action? onResource = null)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var result = operation(attempt);
                if (attempt > 1)
                {
                    Console.WriteLine($"  [{operationName}] succeeded on attempt {attempt}");
                }
                return result;
            }
            catch (CudaException ex) when (ex.IsRecoverable)
            {
                Console.WriteLine($"  [{operationName}] attempt {attempt} transient: {ex.ErrorCode}; retrying");
                await Task.Delay(TimeSpan.FromMilliseconds(10 * attempt));
            }
            catch (CudaException ex) when (ex.IsResourceError)
            {
                Console.WriteLine($"  [{operationName}] attempt {attempt} resource: {ex.ErrorCode}; invoking cleanup");
                onResource?.Invoke();
                await Task.Delay(TimeSpan.FromMilliseconds(10 * attempt));
            }
            catch (CudaException ex) when (ex.IsFatal)
            {
                Console.WriteLine($"  [{operationName}] attempt {attempt} fatal: {ex.ErrorCode}; failing fast");
                throw;
            }
            catch (CudaException ex)
            {
                Console.WriteLine($"  [{operationName}] attempt {attempt} programmer: {ex.ErrorCode}; failing fast");
                throw;
            }
        }
        throw new InvalidOperationException(
            $"{operationName} exhausted {maxAttempts} attempts without success");
    }

    private static void PrintClassificationTable()
    {
        Console.WriteLine("CudaErrorClass mapping (from DotCompute.Backends.CUDA.ErrorHandling):");
        Console.WriteLine("  NotReady            -> " + CudaError.NotReady.Classify());
        Console.WriteLine("  MemoryAllocation    -> " + CudaError.MemoryAllocation.Classify());
        Console.WriteLine("  InvalidValue        -> " + CudaError.InvalidValue.Classify());
        Console.WriteLine("  IllegalAddress      -> " + CudaError.IllegalAddress.Classify());
        Console.WriteLine("  NoDevice            -> " + CudaError.NoDevice.Classify());
        Console.WriteLine();
    }
}
