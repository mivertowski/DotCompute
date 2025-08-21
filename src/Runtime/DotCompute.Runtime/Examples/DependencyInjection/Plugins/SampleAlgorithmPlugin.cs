// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Runtime.Examples.DependencyInjection.Logging;
using DotCompute.Runtime.Examples.DependencyInjection.Providers;
using DotCompute.Runtime.Examples.DependencyInjection.Services;
using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.Examples.DependencyInjection.Plugins;

/// <summary>
/// Example algorithm plugin with DI support
/// NOTE: This class is currently commented out due to missing IAlgorithmPlugin interface.
/// Uncomment when the algorithms framework is available.
/// </summary>
/*
public class SampleAlgorithmPlugin : IAlgorithmPlugin
{
    private readonly ICustomDataProvider _dataProvider;
    private readonly IComputationLogger _computationLogger;
    private readonly ILogger<SampleAlgorithmPlugin> _logger;
    private IAccelerator? _accelerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SampleAlgorithmPlugin"/> class
    /// </summary>
    /// <param name="dataProvider">The custom data provider</param>
    /// <param name="computationLogger">The computation logger</param>
    /// <param name="logger">The logger</param>
    public SampleAlgorithmPlugin(
        ICustomDataProvider dataProvider,
        IComputationLogger computationLogger,
        ILogger<SampleAlgorithmPlugin> logger)
    {
        _dataProvider = dataProvider;
        _computationLogger = computationLogger;
        _logger = logger;
    }

    /// <summary>
    /// Gets or sets the plugin-specific service (optional property injection)
    /// </summary>
    [Inject(Required = false)]
    public IPluginSpecificService? PluginService { get; set; }

    /// <inheritdoc />
    public string Id => "sample-algorithm-plugin";

    /// <inheritdoc />
    public string Name => "Sample Algorithm Plugin";

    /// <inheritdoc />
    public Version Version => new(1, 0, 0);

    /// <inheritdoc />
    public string Description => "Example plugin demonstrating DI integration";

    /// <inheritdoc />
    public AcceleratorType[] SupportedAccelerators => new[] { AcceleratorType.CPU };

    /// <inheritdoc />
    public Type[] InputTypes => new[] { typeof(float[]) };

    /// <inheritdoc />
    public Type OutputType => typeof(float[]);

    /// <inheritdoc />
    public async Task InitializeAsync(IAccelerator accelerator, CancellationToken cancellationToken = default)
    {
        _accelerator = accelerator;
        _logger.LogInformation("Initializing plugin with accelerator {AcceleratorName}", accelerator.Info.Name);
        
        // Use injected services
        var data = await _dataProvider.GetInitializationDataAsync();
        await _computationLogger.LogInitializationAsync(Id, data.Length);
        
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<object> ExecuteAsync(object[] inputs, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
    {
        if (_accelerator == null)
        {
            throw new InvalidOperationException("Plugin not initialized");
        }
        
        _logger.LogInformation("Executing algorithm plugin");
        
        // Use plugin-specific service if available
        if (PluginService != null)
        {
            await PluginService.ProcessAsync();
        }
        
        // Simulate computation
        var input = (float[])inputs[0];
        var result = new float[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            result[i] = input[i] * 2.0f;
        }
        
        await _computationLogger.LogExecutionAsync(Id, input.Length);
        return result;
    }

    /// <inheritdoc />
    public bool ValidateInputs(object[] inputs)
    {
        return inputs.Length == 1 && inputs[0] is float[];
    }

    /// <inheritdoc />
    public long EstimateMemoryRequirement(int[] inputSizes)
    {
        return inputSizes.Sum() * sizeof(float) * 2; // Input + output
    }

    /// <inheritdoc />
    public AlgorithmPerformanceProfile GetPerformanceProfile()
    {
        return new AlgorithmPerformanceProfile
        {
            Complexity = "O(n)",
            IsParallelizable = true,
            OptimalParallelism = Environment.ProcessorCount,
            IsMemoryBound = false,
            IsComputeBound = true,
            EstimatedFlops = 1000000
        };
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing plugin");
        return ValueTask.CompletedTask;
    }
}
*/