// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Recovery.Types;
using DotCompute.Abstractions;
namespace DotCompute.Core.Recovery.Compilation;

/// <summary>
/// Tracks compilation history for a specific kernel, maintaining records of failures,
/// successes, and patterns that can inform future compilation attempts.
/// </summary>
/// <remarks>
/// The compilation history serves as a learning mechanism for the recovery system,
/// tracking:
/// - Recent compilation errors and their patterns
/// - Successful strategies for this specific kernel
/// - Failure counts and success rates over time
/// - Strategy effectiveness for optimization
/// 
/// This historical data enables the system to make intelligent decisions about
/// which fallback strategies to try first for a given kernel, improving
/// overall compilation success rates and reducing recovery time.
/// 
/// The class implements IDisposable to ensure proper cleanup of tracked data.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="CompilationHistory"/> class.
/// </remarks>
/// <param name="kernelName">The name of the kernel to track.</param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="kernelName"/> is null.</exception>
public class CompilationHistory(string kernelName) : IDisposable
{
    private readonly string _kernelName = kernelName ?? throw new ArgumentNullException(nameof(kernelName));
    private readonly List<Exception> _recentErrors = [];
    private readonly List<CompilationFallbackStrategy> _attemptedStrategies = [];
    private int _failureCount;
    private int _totalAttempts;
    private CompilationFallbackStrategy? _successfulStrategy;
    private bool _disposed;

    /// <summary>
    /// Gets the name of the kernel being tracked.
    /// </summary>
    /// <value>The kernel identifier.</value>
    public string KernelName => _kernelName;


    /// <summary>
    /// Gets the current number of consecutive compilation failures.
    /// </summary>
    /// <value>The count of failures since the last successful compilation.</value>
    public int FailureCount => _failureCount;


    /// <summary>
    /// Gets the total number of compilation attempts made for this kernel.
    /// </summary>
    /// <value>The cumulative count of all compilation attempts.</value>
    public int TotalAttempts => _totalAttempts;


    /// <summary>
    /// Gets a value indicating whether the last compilation attempt was successful.
    /// </summary>
    /// <value>true if the most recent compilation succeeded; otherwise, false.</value>
    public bool LastCompilationSuccessful => _successfulStrategy.HasValue;


    /// <summary>
    /// Gets the fallback strategy that was most recently successful for this kernel.
    /// </summary>
    /// <value>The successful strategy, or null if no successful compilation has occurred.</value>
    public CompilationFallbackStrategy? SuccessfulStrategy => _successfulStrategy;


    /// <summary>
    /// Gets a read-only list of recent compilation errors.
    /// The list is limited to the most recent errors to prevent unbounded growth.
    /// </summary>
    /// <value>A read-only collection of recent exceptions.</value>
    public IReadOnlyList<Exception> RecentErrors => _recentErrors.AsReadOnly();

    /// <summary>
    /// Records a compilation failure with the associated error and compilation options.
    /// </summary>
    /// <param name="error">The exception that caused the compilation failure.</param>
    /// <param name="options">The compilation options that were used for the failed attempt.</param>
    /// <remarks>
    /// This method maintains a rolling window of recent errors (up to 20) to prevent
    /// unbounded memory growth while preserving enough history for pattern analysis.
    /// </remarks>
    public void RecordFailure(Exception error, CompilationOptions options)
    {
        _failureCount++;
        _totalAttempts++;
        _recentErrors.Add(error);

        // Keep only recent errors to prevent unbounded growth
        if (_recentErrors.Count > 20)
        {
            _recentErrors.RemoveAt(0);
        }
    }

    /// <summary>
    /// Records a successful compilation with the strategy that achieved success.
    /// </summary>
    /// <param name="strategy">The fallback strategy that resulted in successful compilation.</param>
    /// <remarks>
    /// Recording a success resets the consecutive failure count and updates the
    /// most recently successful strategy for future optimization.
    /// </remarks>
    public void RecordSuccess(CompilationFallbackStrategy strategy)
    {
        _successfulStrategy = strategy;
        _totalAttempts++;
        _failureCount = 0; // Reset consecutive failures
    }

    /// <summary>
    /// Releases all resources used by the <see cref="CompilationHistory"/> instance.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _recentErrors.Clear();
            _attemptedStrategies.Clear();
            _disposed = true;
        }
    }
}