// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;

namespace DotCompute.Tests.Common.Hardware;

/// <summary>
/// Advanced hardware simulator for complex testing scenarios.
/// Simulates realistic hardware behaviors, performance characteristics, and failure modes.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class HardwareSimulator : IDisposable
{
    private readonly ILogger<HardwareSimulator> _logger;
    private readonly MockHardwareProvider _hardwareProvider;
    private readonly Dictionary<string, SimulationScenario> _activeScenarios;
    private readonly Timer? _simulationTimer;
    private bool _disposed;

    /// <summary>
    /// Gets the hardware provider managed by this simulator.
    /// </summary>
    public IHardwareProvider HardwareProvider => _hardwareProvider;

    /// <summary>
    /// Gets whether the simulator is currently running.
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Initializes a new instance of the HardwareSimulator class.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    public HardwareSimulator(ILogger<HardwareSimulator>? logger = null)
    {
        _logger = logger ?? NullLogger<HardwareSimulator>.Instance;
        _hardwareProvider = new MockHardwareProvider(logger as ILogger<MockHardwareProvider>);
        _activeScenarios = new Dictionary<string, SimulationScenario>();
        
        // Create a timer for continuous simulation updates
        _simulationTimer = new Timer(UpdateSimulations, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        
        _logger.LogInformation("Hardware simulator initialized");
    }

    /// <summary>
    /// Starts the hardware simulation with specified parameters.
    /// </summary>
    /// <param name="configuration">The hardware configuration to simulate.</param>
    /// <param name="updateInterval">How frequently to update simulations.</param>
    public void Start(HardwareConfiguration configuration = HardwareConfiguration.Default, 
                     TimeSpan? updateInterval = null)
    {
        ThrowIfDisposed();

        _hardwareProvider.CreateConfiguration(configuration);
        IsRunning = true;
        
        var interval = updateInterval ?? TimeSpan.FromSeconds(1);
        _simulationTimer?.Change(interval, interval);
        
        _logger.LogInformation("Hardware simulation started with {Configuration} configuration, update interval: {Interval}ms", 
            configuration, interval.TotalMilliseconds);
    }

    /// <summary>
    /// Stops the hardware simulation.
    /// </summary>
    public void Stop()
    {
        if (!IsRunning)
            return;

        _simulationTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        IsRunning = false;
        
        // Clear all active scenarios
        _activeScenarios.Clear();
        _hardwareProvider.ResetAllFailures();
        
        _logger.LogInformation("Hardware simulation stopped");
    }

    /// <summary>
    /// Adds a simulation scenario to be executed continuously.
    /// </summary>
    /// <param name="scenario">The simulation scenario to add.</param>
    public void AddScenario(SimulationScenario scenario)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(scenario);

        _activeScenarios[scenario.Id] = scenario;
        _logger.LogDebug("Added simulation scenario: {ScenarioId}", scenario.Id);
    }

    /// <summary>
    /// Removes a simulation scenario.
    /// </summary>
    /// <param name="scenarioId">The ID of the scenario to remove.</param>
    /// <returns>True if the scenario was found and removed, false otherwise.</returns>
    public bool RemoveScenario(string scenarioId)
    {
        ThrowIfDisposed();

        if (_activeScenarios.Remove(scenarioId))
        {
            _logger.LogDebug("Removed simulation scenario: {ScenarioId}", scenarioId);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Creates and executes a performance stress test scenario.
    /// </summary>
    /// <param name="duration">How long to run the stress test.</param>
    /// <param name="targetDeviceTypes">Device types to stress test.</param>
    public void RunStressTest(TimeSpan duration, params AcceleratorType[] targetDeviceTypes)
    {
        ThrowIfDisposed();

        var scenario = new SimulationScenario
        {
            Id = $"stress_test_{Guid.NewGuid():N}",
            Name = "Performance Stress Test",
            Duration = duration,
            Actions = CreateStressTestActions(targetDeviceTypes)
        };

        AddScenario(scenario);
        _logger.LogInformation("Started stress test scenario for {Duration}ms targeting {DeviceTypes}", 
            duration.TotalMilliseconds, string.Join(", ", targetDeviceTypes));
    }

    /// <summary>
    /// Simulates thermal throttling on devices of the specified type.
    /// </summary>
    /// <param name="deviceType">The device type to throttle.</param>
    /// <param name="throttlePercentage">The throttling percentage (0.0 to 1.0).</param>
    public void SimulateThermalThrottling(AcceleratorType deviceType, double throttlePercentage = 0.3)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfLessThan(throttlePercentage, 0.0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(throttlePercentage, 1.0);

        foreach (var device in _hardwareProvider.GetDevices(deviceType).OfType<MockHardwareDevice>())
        {
            var originalFreq = device.MaxClockFrequency;
            var throttledFreq = (int)(originalFreq * (1.0 - throttlePercentage));
            device.SimulateClockChange(throttledFreq);
            
            _logger.LogWarning("Applied thermal throttling to device {DeviceId}: {Original}MHz -> {Throttled}MHz", 
                device.Id, originalFreq, throttledFreq);
        }
    }

    /// <summary>
    /// Simulates memory pressure by consuming memory on devices.
    /// </summary>
    /// <param name="deviceType">The device type to apply memory pressure to.</param>
    /// <param name="memoryPressurePercentage">Percentage of memory to consume (0.0 to 1.0).</param>
    public void SimulateMemoryPressure(AcceleratorType deviceType, double memoryPressurePercentage = 0.8)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfLessThan(memoryPressurePercentage, 0.0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(memoryPressurePercentage, 1.0);

        foreach (var device in _hardwareProvider.GetDevices(deviceType).OfType<MockHardwareDevice>())
        {
            var memoryToConsume = (long)(device.TotalMemory * memoryPressurePercentage);
            device.SimulateMemoryUsage(memoryToConsume);
            
            _logger.LogWarning("Applied memory pressure to device {DeviceId}: {Consumed}MB consumed", 
                device.Id, memoryToConsume / (1024 * 1024));
        }
    }

    /// <summary>
    /// Simulates random device failures for chaos testing.
    /// </summary>
    /// <param name="failureProbability">Probability of failure per device (0.0 to 1.0).</param>
    /// <param name="deviceTypes">Device types to consider for failures.</param>
    public void SimulateRandomFailures(double failureProbability = 0.1, params AcceleratorType[] deviceTypes)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfLessThan(failureProbability, 0.0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(failureProbability, 1.0);

        var random = new Random();
        var targetTypes = deviceTypes.Length > 0 ? deviceTypes : Enum.GetValues<AcceleratorType>();

        foreach (var deviceType in targetTypes)
        {
            foreach (var device in _hardwareProvider.GetDevices(deviceType).OfType<MockHardwareDevice>())
            {
                if (random.NextDouble() < failureProbability)
                {
                    var errorMessages = new[]
                    {
                        "Hardware fault detected",
                        "Device driver crashed",
                        "Thermal protection triggered",
                        "Power supply instability",
                        "Memory corruption detected"
                    };
                    
                    var errorMessage = errorMessages[random.Next(errorMessages.Length)];
                    device.SimulateFailure(errorMessage);
                    
                    _logger.LogWarning("Simulated random failure on device {DeviceId}: {Error}", 
                        device.Id, errorMessage);
                }
            }
        }
    }

    /// <summary>
    /// Resets all simulated conditions and restores devices to normal operation.
    /// </summary>
    public void ResetAllConditions()
    {
        ThrowIfDisposed();

        _hardwareProvider.ResetAllFailures();
        
        // Reset all device states
        foreach (var device in _hardwareProvider.GetAllDevices().OfType<MockHardwareDevice>())
        {
            // Reset memory to full availability
            device.SimulateMemoryUsage(device.AvailableMemory - device.TotalMemory);
            
            // Reset clock frequencies to reasonable defaults
            device.SimulateClockChange(device.Type switch
            {
                AcceleratorType.CUDA => 1500,
                AcceleratorType.CPU => 3000,
                AcceleratorType.Metal => 1200,
                _ => 1500
            });
        }

        _activeScenarios.Clear();
        _logger.LogInformation("Reset all simulation conditions");
    }

    /// <summary>
    /// Gets comprehensive simulation statistics.
    /// </summary>
    /// <returns>A dictionary containing simulation statistics.</returns>
    public Dictionary<string, object> GetStatistics()
    {
        ThrowIfDisposed();

        var allDevices = _hardwareProvider.GetAllDevices().ToList();
        var availableDevices = allDevices.Count(d => d.IsAvailable);
        var failedDevices = allDevices.Count - availableDevices;

        var stats = new Dictionary<string, object>
        {
            ["IsRunning"] = IsRunning,
            ["TotalDevices"] = allDevices.Count,
            ["AvailableDevices"] = availableDevices,
            ["FailedDevices"] = failedDevices,
            ["ActiveScenarios"] = _activeScenarios.Count,
            ["DevicesByType"] = allDevices.GroupBy(d => d.Type)
                .ToDictionary(g => g.Key.ToString(), g => g.Count()),
            ["FailuresByType"] = allDevices.Where(d => !d.IsAvailable)
                .GroupBy(d => d.Type)
                .ToDictionary(g => g.Key.ToString(), g => g.Count()),
            ["TotalMemory"] = allDevices.Sum(d => d.TotalMemory),
            ["AvailableMemory"] = allDevices.Sum(d => d.AvailableMemory),
            ["AverageMemoryUtilization"] = allDevices.Count > 0 
                ? allDevices.Average(d => d.TotalMemory > 0 ? (d.TotalMemory - d.AvailableMemory) / (double)d.TotalMemory : 0)
                : 0,
            ["StatisticsTimestamp"] = DateTime.UtcNow
        };

        return stats;
    }

    private List<SimulationAction> CreateStressTestActions(AcceleratorType[] targetDeviceTypes)
    {
        var actions = new List<SimulationAction>();
        
        foreach (var deviceType in targetDeviceTypes)
        {
            actions.Add(new SimulationAction
            {
                ActionType = SimulationActionType.MemoryPressure,
                TargetDeviceType = deviceType,
                Parameters = new Dictionary<string, object> { ["Percentage"] = 0.9 }
            });
            
            actions.Add(new SimulationAction
            {
                ActionType = SimulationActionType.ThermalThrottling,
                TargetDeviceType = deviceType,
                Parameters = new Dictionary<string, object> { ["Percentage"] = 0.4 }
            });
        }

        return actions;
    }

    private void UpdateSimulations(object? state)
    {
        if (!IsRunning || _disposed)
            return;

        try
        {
            var currentTime = DateTime.UtcNow;
            var scenariosToRemove = new List<string>();

            foreach (var (scenarioId, scenario) in _activeScenarios)
            {
                if (currentTime - scenario.StartTime > scenario.Duration)
                {
                    scenariosToRemove.Add(scenarioId);
                    continue;
                }

                ExecuteScenarioActions(scenario);
            }

            // Remove expired scenarios
            foreach (var scenarioId in scenariosToRemove)
            {
                RemoveScenario(scenarioId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during simulation update");
        }
    }

    private void ExecuteScenarioActions(SimulationScenario scenario)
    {
        foreach (var action in scenario.Actions)
        {
            try
            {
                switch (action.ActionType)
                {
                    case SimulationActionType.MemoryPressure:
                        if (action.Parameters.TryGetValue("Percentage", out var memPercent))
                        {
                            SimulateMemoryPressure(action.TargetDeviceType, Convert.ToDouble(memPercent));
                        }
                        break;
                        
                    case SimulationActionType.ThermalThrottling:
                        if (action.Parameters.TryGetValue("Percentage", out var thermalPercent))
                        {
                            SimulateThermalThrottling(action.TargetDeviceType, Convert.ToDouble(thermalPercent));
                        }
                        break;
                        
                    case SimulationActionType.RandomFailures:
                        if (action.Parameters.TryGetValue("Probability", out var probability))
                        {
                            SimulateRandomFailures(Convert.ToDouble(probability), action.TargetDeviceType);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error executing simulation action {ActionType} for scenario {ScenarioId}", 
                    action.ActionType, scenario.Id);
            }
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        
        _simulationTimer?.Dispose();
        _hardwareProvider?.Dispose();
        _activeScenarios.Clear();
        
        _disposed = true;
        _logger.LogDebug("Hardware simulator disposed");
    }
}

/// <summary>
/// Represents a simulation scenario with multiple actions.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class SimulationScenario
{
    /// <summary>
    /// Gets or sets the unique identifier for this scenario.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the friendly name of the scenario.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the duration to run this scenario.
    /// </summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets when this scenario started.
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the actions to execute in this scenario.
    /// </summary>
    public List<SimulationAction> Actions { get; set; } = new();
}

/// <summary>
/// Represents a single simulation action.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class SimulationAction
{
    /// <summary>
    /// Gets or sets the type of action to execute.
    /// </summary>
    public SimulationActionType ActionType { get; set; }

    /// <summary>
    /// Gets or sets the target device type for this action.
    /// </summary>
    public AcceleratorType TargetDeviceType { get; set; }

    /// <summary>
    /// Gets or sets the parameters for this action.
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Types of simulation actions that can be executed.
/// </summary>
public enum SimulationActionType
{
    MemoryPressure,
    ThermalThrottling,
    RandomFailures,
    ClockSpeedChange,
    DeviceFailure,
    DeviceRecovery
}
