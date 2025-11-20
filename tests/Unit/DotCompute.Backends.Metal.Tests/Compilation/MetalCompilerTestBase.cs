// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.Kernels;
using DotCompute.Backends.Metal.Native;
using DotCompute.Backends.Metal.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace DotCompute.Backends.Metal.Tests.Compilation;

/// <summary>
/// Base class for Metal compilation tests providing common setup and utilities.
/// </summary>
public abstract class MetalCompilerTestBase : IDisposable
{
    protected readonly ITestOutputHelper Output;
    protected readonly ILogger Logger;
    protected readonly IntPtr Device;
    protected readonly IntPtr CommandQueue;
    protected readonly MetalCommandBufferPool? CommandBufferPool;
    protected readonly bool IsMetalAvailable;

    protected MetalCompilerTestBase(ITestOutputHelper output)
    {
        Output = output;
        Logger = new TestOutputLogger(output);
        IsMetalAvailable = MetalNative.IsMetalSupported();

        if (IsMetalAvailable)
        {
            Device = MetalNative.CreateSystemDefaultDevice();
            if (Device != IntPtr.Zero)
            {
                CommandQueue = MetalNative.CreateCommandQueue(Device);
                if (CommandQueue != IntPtr.Zero)
                {
                    CommandBufferPool = new MetalCommandBufferPool(CommandQueue,
                        NullLogger<MetalCommandBufferPool>.Instance, maxPoolSize: 16);
                }
            }
        }
    }

    /// <summary>
    /// Creates a compiler instance for testing.
    /// </summary>
    protected MetalKernelCompiler CreateCompiler(MetalKernelCache? cache = null)
    {
        Skip.IfNot(IsMetalAvailable, "Metal is not available on this system");
        Skip.If(Device == IntPtr.Zero, "Metal device could not be created");
        Skip.If(CommandQueue == IntPtr.Zero, "Metal command queue could not be created");

        return new MetalKernelCompiler(
            Device,
            CommandQueue,
            Logger,
            CommandBufferPool,
            cache);
    }

    /// <summary>
    /// Creates a cache instance for testing.
    /// </summary>
    protected MetalKernelCache CreateCache(
        int maxSize = 100,
        TimeSpan? ttl = null,
        string? persistentPath = null)
    {
        var logger = new LoggerFactory().CreateLogger<MetalKernelCache>();
        return new MetalKernelCache(logger, maxSize, ttl, persistentPath, Device);
    }

    /// <summary>
    /// Verifies that Metal is available and throws Skip if not.
    /// </summary>
    protected void RequireMetalSupport()
    {
        Skip.IfNot(IsMetalAvailable, "Metal is not available on this system");
        Skip.If(Device == IntPtr.Zero, "Metal device could not be created");
    }

    /// <summary>
    /// Logs test information to output.
    /// </summary>
    protected void LogTestInfo(string message)
    {
        Output.WriteLine($"[TEST] {message}");
    }

    public virtual void Dispose()
    {
        CommandBufferPool?.Dispose();

        if (CommandQueue != IntPtr.Zero)
        {
            MetalNative.ReleaseCommandQueue(CommandQueue);
        }

        if (Device != IntPtr.Zero)
        {
            MetalNative.ReleaseDevice(Device);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Simple test logger that writes to xUnit output.
    /// </summary>
    private class TestOutputLogger : ILogger
    {
        private readonly ITestOutputHelper _output;

        public TestOutputLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return NullDisposable.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            _output.WriteLine($"[{logLevel}] {message}");
            if (exception != null)
            {
                _output.WriteLine(exception.ToString());
            }
        }

        private class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();
            public void Dispose() { }
        }
    }
}

/// <summary>
/// Skip attribute helper for conditional test execution.
/// </summary>
public static class Skip
{
    public static void IfNot(bool condition, string reason)
    {
        if (!condition)
        {
            throw new Xunit.SkipException(reason);
        }
    }

    public static void If(bool condition, string reason)
    {
        if (condition)
        {
            throw new Xunit.SkipException(reason);
        }
    }
}
