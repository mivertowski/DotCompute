// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Abstractions.Interfaces.Kernels;
using Microsoft.Extensions.Logging;
using AbstractionsICompiledKernel = DotCompute.Abstractions.ICompiledKernel;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core;

/// <summary>
/// Base abstract class for accelerator implementations, consolidating common patterns.
/// This addresses the critical issue of 240+ lines of duplicate code across accelerator implementations.
/// </summary>
public abstract partial class BaseAccelerator : IAccelerator
{
    // LoggerMessage delegates - Event ID range 21000-21099 for BaseAccelerator (Core module)
    private static readonly Action<ILogger, string, double, string, Exception?> _logCompilationMetrics =
        LoggerMessage.Define<string, double, string>(
            MsLogLevel.Debug,
            new EventId(21000, nameof(LogCompilationMetrics)),
            "Kernel '{KernelName}' compilation metrics: Time={CompilationTime}ms, Size={ByteCodeSize}");

    // Wrapper method
    private static void LogCompilationMetrics(ILogger logger, string kernelName, double compilationTimeMs, string byteCodeSize)
        => _logCompilationMetrics(logger, kernelName, compilationTimeMs, byteCodeSize, null);

    private volatile int _disposed;
    private readonly ILogger _logger;


    /// <summary>
    /// Initializes a new instance of the <see cref="BaseAccelerator"/> class.
    /// </summary>
    protected BaseAccelerator(
        AcceleratorInfo info,
        AcceleratorType type,
        IUnifiedMemoryManager memory,
        AcceleratorContext context,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);


        Info = info;
        Type = type;
        Memory = memory;
        Context = context;
        _logger = logger;

        // Use AcceleratorUtilities for consistent initialization logging
        _ = AcceleratorUtilities.InitializeWithLogging(
            _logger,
            Type.ToString(),
            InitializeCore,
            Info.Name);
    }


    /// <inheritdoc/>
    public AcceleratorInfo Info { get; }


    /// <inheritdoc/>
    public AcceleratorType Type { get; }

    /// <inheritdoc/>
    public string DeviceType => Type.ToString();

    /// <inheritdoc/>
    public IUnifiedMemoryManager Memory { get; }

    /// <inheritdoc/>
    public IUnifiedMemoryManager MemoryManager => Memory;


    /// <inheritdoc/>
    public AcceleratorContext Context { get; }

    /// <inheritdoc/>
    public virtual bool IsAvailable => !IsDisposed;

    /// <summary>
    /// Gets whether this accelerator has been disposed.
    /// </summary>
    public bool IsDisposed => _disposed != 0;


    /// <inheritdoc/>
    public virtual ValueTask<AbstractionsICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ValidateKernelDefinition(definition);

        // Use AcceleratorUtilities for consistent compilation pattern

        return AcceleratorUtilities.CompileKernelWithLoggingAsync(
            definition,
            options,
            _logger,
            Type.ToString(),
            CompileKernelCoreAsync,
            cancellationToken);
    }


    /// <inheritdoc/>
    public virtual ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Use AcceleratorUtilities for consistent synchronization pattern

        return AcceleratorUtilities.SynchronizeWithLoggingAsync(
            _logger,
            Type.ToString(),
            SynchronizeCoreAsync,
            cancellationToken);
    }


    /// <summary>
    /// Core initialization logic to be implemented by derived classes.
    /// </summary>
    /// <returns>Initialization result (typically null or status object)</returns>
    protected virtual object? InitializeCore()
        // Default implementation - derived classes can override


        => null;


    /// <summary>
    /// Core kernel compilation logic to be implemented by derived classes.
    /// </summary>
    protected abstract ValueTask<AbstractionsICompiledKernel> CompileKernelCoreAsync(
        KernelDefinition definition,
        CompilationOptions options,
        CancellationToken cancellationToken);


    /// <summary>
    /// Core synchronization logic to be implemented by derived classes.
    /// </summary>
    protected abstract ValueTask SynchronizeCoreAsync(CancellationToken cancellationToken);


    /// <summary>
    /// Validates kernel definition parameters.
    /// Common validation logic that was duplicated across implementations.
    /// </summary>
    protected virtual void ValidateKernelDefinition(KernelDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);


        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            throw new InvalidOperationException("Kernel validation failed: Kernel name cannot be empty");
        }


        if (string.IsNullOrWhiteSpace(definition.Source))
        {
            throw new InvalidOperationException("Kernel validation failed: Kernel source cannot be empty");
        }

        // Parameters might be stored in metadata or parsed from source
        // For now, we'll skip this validation as it depends on the specific kernel format
    }


    /// <summary>
    /// Creates compilation options with defaults if not provided.
    /// </summary>
    protected virtual CompilationOptions GetEffectiveOptions(CompilationOptions? options)
    {
        return options ?? new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Default,
            EnableDebugInfo = false
        };
    }


    /// <summary>
    /// Logs performance metrics for kernel compilation.
    /// </summary>
    protected void LogCompilationMetrics(string kernelName, TimeSpan compilationTime, long? byteCodeSize = null)
    {
        LogCompilationMetrics(_logger, kernelName, compilationTime.TotalMilliseconds,
            byteCodeSize?.ToString(global::System.Globalization.CultureInfo.InvariantCulture) ?? "N/A");
    }


    /// <summary>
    /// Throws if the accelerator has been disposed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(IsDisposed, GetType());


    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
        {
            await AcceleratorUtilities.DisposeWithSynchronizationAsync(
                _logger,
                Type.ToString(),
                async () => await SynchronizeAsync().ConfigureAwait(false),
                Memory,
                Context).ConfigureAwait(false);


            await DisposeCoreAsync().ConfigureAwait(false);


            GC.SuppressFinalize(this);
        }
    }


    /// <summary>
    /// Core disposal logic to be implemented by derived classes.
    /// </summary>
    protected virtual ValueTask DisposeCoreAsync()
        // Default implementation - derived classes can override


        => ValueTask.CompletedTask;
}

/// <summary>
/// Base class for compiled kernels, consolidating common patterns.
/// </summary>
public abstract class BaseCompiledKernel : AbstractionsICompiledKernel
{
    private volatile int _disposed;


    /// <summary>
    /// Gets the kernel unique identifier.
    /// </summary>
    public Guid Id { get; protected init; } = Guid.NewGuid();


    /// <summary>
    /// Initializes a new instance of the <see cref="BaseCompiledKernel"/> class.
    /// </summary>
    protected BaseCompiledKernel(
        string name,
        IReadOnlyList<KernelParameter> parameters,
        IAccelerator accelerator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(accelerator);


        Name = name;
        Parameters = parameters;
        Accelerator = accelerator;
    }


    /// <inheritdoc/>
    public string Name { get; }


    /// <inheritdoc/>
    public IReadOnlyList<KernelParameter> Parameters { get; }


    /// <inheritdoc/>
    public IAccelerator Accelerator { get; }


    /// <summary>
    /// Gets whether this kernel has been disposed.
    /// </summary>
    public bool IsDisposed => _disposed != 0;


    /// <inheritdoc/>
    public virtual ValueTask<KernelExecutionResult> ExecuteAsync(
        KernelArguments arguments,
        KernelExecutionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ValidateArguments(arguments);


        return ExecuteCoreAsync(arguments, options ?? new KernelExecutionOptions(), cancellationToken);
    }


    /// <summary>
    /// Core execution logic to be implemented by derived classes.
    /// </summary>
    protected abstract ValueTask<KernelExecutionResult> ExecuteCoreAsync(
        KernelArguments arguments,
        KernelExecutionOptions options,
        CancellationToken cancellationToken);


    /// <summary>
    /// Validates kernel arguments against parameters.
    /// </summary>
    protected virtual void ValidateArguments(KernelArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);


        if (arguments.Count != Parameters.Count)
        {
            throw new ArgumentException(
                $"Argument count mismatch. Expected {Parameters.Count}, got {arguments.Count}",
                nameof(arguments));
        }

        // Additional validation can be added by derived classes

    }


    /// <summary>
    /// Throws if the kernel has been disposed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(IsDisposed, GetType());


    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }


    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }


    /// <summary>
    /// Disposes the kernel.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
        {
            if (disposing)
            {
                DisposeCore();
            }
        }
    }


    /// <summary>
    /// Core disposal logic to be implemented by derived classes.
    /// </summary>
    protected virtual void DisposeCore()
    {
        // Default implementation - derived classes can override
    }

    /// <summary>
    /// Executes the kernel with given arguments.
    /// Derived classes must implement this to provide their specific execution logic.
    /// </summary>
    /// <param name="arguments">The kernel arguments for execution.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous execution operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when arguments is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the accelerator has been disposed.</exception>
    public abstract ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default);
}
