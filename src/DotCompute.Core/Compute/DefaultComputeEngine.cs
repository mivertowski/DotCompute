// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Accelerators;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Compute;

/// <summary>
/// Simple implementation of IKernelSource for testing.
/// </summary>
internal class SimpleKernelSource : IKernelSource
{
    public SimpleKernelSource(string name, string code, KernelLanguage language, string entryPoint, string[]? dependencies = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Language = language;
        EntryPoint = entryPoint ?? "main";
        Dependencies = dependencies ?? Array.Empty<string>();
    }

    public string Name { get; }
    public string Code { get; }
    public KernelLanguage Language { get; }
    public string EntryPoint { get; }
    public string[] Dependencies { get; }
}

/// <summary>
/// Default implementation of the compute engine.
/// </summary>
public class DefaultComputeEngine : IComputeEngine
{
    private readonly IAcceleratorManager _acceleratorManager;
    private readonly ILogger<DefaultComputeEngine> _logger;
    private readonly List<ComputeBackendType> _availableBackends;
    private bool _disposed;

    public DefaultComputeEngine(
        IAcceleratorManager acceleratorManager,
        ILogger<DefaultComputeEngine> logger)
    {
        _acceleratorManager = acceleratorManager ?? throw new ArgumentNullException(nameof(acceleratorManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Initialize backends on first use (lazy initialization)
        _availableBackends = new List<ComputeBackendType>();
    }

    public ComputeBackendType[] AvailableBackends => _availableBackends.ToArray();

    public ComputeBackendType DefaultBackend => _availableBackends.FirstOrDefault();

    public async ValueTask<ICompiledKernel> CompileKernelAsync(
        string kernelSource,
        string? entryPoint = null,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(kernelSource))
            throw new ArgumentException("Kernel source cannot be null or empty", nameof(kernelSource));

        await EnsureInitializedAsync();

        _logger.LogInformation("Compiling kernel with entry point: {EntryPoint}", entryPoint ?? "main");

        // Get the first available accelerator
        var criteria = new AcceleratorSelectionCriteria
        {
            PreferredType = AcceleratorType.GPU,
            MinimumMemory = 0,
            RequiredFeatures = null
        };
        
        var accelerator = _acceleratorManager.SelectBest(criteria) ?? _acceleratorManager.Default;
        if (accelerator == null)
        {
            throw new InvalidOperationException("No accelerators available for kernel compilation");
        }

        // Create a simple kernel source implementation
        var kernelSourceImpl = new SimpleKernelSource(
            entryPoint ?? "kernel",
            kernelSource,
            KernelLanguage.OpenCL,
            entryPoint ?? "main"
        );

        // Create kernel definition
        var definition = new KernelDefinition(
            entryPoint ?? "kernel",
            kernelSourceImpl,
            options ?? new CompilationOptions()
        );

        // Compile the kernel
        var compiledKernel = await accelerator.CompileKernelAsync(definition, options, cancellationToken);
        
        _logger.LogInformation("Kernel compiled successfully: {KernelName}", compiledKernel.Name);
        
        return compiledKernel;
    }

    public async ValueTask ExecuteAsync(
        ICompiledKernel kernel,
        object[] arguments,
        ComputeBackendType backendType,
        ExecutionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (kernel == null)
            throw new ArgumentNullException(nameof(kernel));
        if (arguments == null)
            throw new ArgumentNullException(nameof(arguments));

        _logger.LogInformation("Executing kernel {KernelName} on backend {Backend}", 
            kernel.Name, backendType);

        // Convert arguments to KernelArguments
        var kernelArgs = new KernelArguments(arguments);

        // Execute the kernel
        await kernel.ExecuteAsync(kernelArgs, cancellationToken);
        
        _logger.LogInformation("Kernel {KernelName} executed successfully", kernel.Name);
    }

    private async ValueTask EnsureInitializedAsync()
    {
        if (_availableBackends.Count > 0)
            return;

        // Try to initialize the accelerator manager if not already done
        try
        {
            // Check if already initialized
            var _ = _acceleratorManager.Default;
        }
        catch (InvalidOperationException)
        {
            // Not initialized, try to initialize with a CPU provider
            _logger.LogInformation("Initializing accelerator manager with CPU provider");
            var cpuProvider = new CpuAcceleratorProvider(_logger as ILogger<CpuAcceleratorProvider> ?? 
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<CpuAcceleratorProvider>());
            _acceleratorManager.RegisterProvider(cpuProvider);
            await _acceleratorManager.InitializeAsync();
        }

        // Now determine available backends
        _availableBackends.Clear();
        _availableBackends.AddRange(DetermineAvailableBackends());
    }

    private List<ComputeBackendType> DetermineAvailableBackends()
    {
        var backends = new List<ComputeBackendType>();
        
        // CPU is always available
        backends.Add(ComputeBackendType.CPU);
        
        // Check for CUDA support
        try
        {
            // Simple check - in production, this would properly detect CUDA
            var cudaAvailable = System.IO.File.Exists("/usr/lib/wsl/lib/libcuda.so.1") ||
                              System.IO.File.Exists("/usr/local/cuda/lib64/libcudart.so");
            if (cudaAvailable)
            {
                backends.Add(ComputeBackendType.CUDA);
                _logger.LogInformation("CUDA backend detected");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "CUDA detection failed");
        }
        
        // OpenCL could be detected similarly
        // For now, just return what we have
        
        return backends;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        
        // Clean up any resources
        _logger.LogInformation("ComputeEngine disposed");
        
        await ValueTask.CompletedTask;
    }
}