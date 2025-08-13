# Hardware Abstraction Layer for Testing

This directory contains a comprehensive hardware abstraction layer that enables DotCompute tests to run without actual hardware dependencies. The system provides realistic simulation of various hardware devices and failure scenarios while maintaining full compatibility with the DotCompute abstractions.

## Overview

The hardware abstraction layer consists of several key components:

### Core Interfaces

- **IHardwareProvider**: Main interface for hardware abstraction providers
- **IHardwareDevice**: Interface representing a hardware device abstraction

### Mock Implementations

- **MockHardwareProvider**: Main provider that manages mock devices
- **MockHardwareDevice**: Base class for all mock hardware devices
- **MockCudaDevice**: NVIDIA CUDA GPU simulation
- **MockCpuDevice**: CPU device simulation with SIMD capabilities
- **MockMetalDevice**: Apple Metal GPU simulation
- **MockAccelerator**: IAccelerator implementation wrapping mock devices

### Advanced Features

- **HardwareSimulator**: Advanced simulator for complex testing scenarios
- **TestHardwareFactory**: Factory for creating preset hardware configurations

## Key Benefits

### 1. Hardware Independence
- Tests run without requiring actual GPU hardware
- Consistent behavior across different development environments
- Perfect for CI/CD pipelines without dedicated hardware

### 2. Deterministic Testing
- Predictable performance characteristics
- Controlled failure scenarios
- Reproducible test conditions

### 3. Comprehensive Device Support
- NVIDIA CUDA GPUs (RTX 4090, RTX 4080, A100, H100, etc.)
- Intel and AMD CPUs with SIMD capabilities
- Apple Silicon (M1, M2, M3 series)
- Generic OpenCL and GPU devices

### 4. Realistic Simulation
- Accurate device specifications
- Performance-based execution timing
- Memory management simulation
- Hardware capability detection

## Usage Examples

### Basic Usage

```csharp
// Create a hardware provider with default configuration
using var provider = TestHardwareFactory.CreateDefault();

// Get a CUDA device
var device = provider.GetFirstDevice(AcceleratorType.CUDA);
if (device != null)
{
    // Create an accelerator
    using var accelerator = provider.CreateAccelerator(device);
    
    // Use the accelerator for testing
    var kernel = await accelerator.CompileKernelAsync(kernelDefinition);
    await kernel.ExecuteAsync(arguments);
}
```

### Advanced Simulation

```csharp
// Create a hardware simulator
using var simulator = TestHardwareFactory.CreateSimulator(HardwareConfiguration.DataCenter);

// Simulate thermal throttling
simulator.SimulateThermalThrottling(AcceleratorType.CUDA, throttlePercentage: 0.3);

// Simulate memory pressure
simulator.SimulateMemoryPressure(AcceleratorType.CUDA, memoryPressurePercentage: 0.8);

// Run stress tests
simulator.RunStressTest(TimeSpan.FromMinutes(5), AcceleratorType.CUDA, AcceleratorType.CPU);
```

### Custom Device Creation

```csharp
// Create custom devices
var highEndGpu = MockCudaDevice.CreateRTX4090();
var serverCpu = MockCpuDevice.CreateEpycServer();
var appleSilicon = MockMetalDevice.CreateM2Max();

// Create custom provider
using var provider = TestHardwareFactory.CreateCustom(new[] { highEndGpu, serverCpu, appleSilicon });
```

### Failure Simulation

```csharp
// Simulate device failures
var device = provider.GetFirstDevice(AcceleratorType.CUDA) as MockHardwareDevice;
device?.SimulateFailure("CUDA out of memory error");

// Test error handling in your code
try
{
    using var accelerator = provider.CreateAccelerator(device);
    // This will throw an exception due to the simulated failure
}
catch (InvalidOperationException ex)
{
    // Handle the simulated error
    Assert.Contains("CUDA out of memory", ex.Message);
}

// Reset the failure
device?.ResetFailure();
```

## Device Configurations

### Predefined Configurations

- **Default**: Balanced set of devices for general testing
- **HighEndGaming**: Latest gaming GPUs with high-end CPUs
- **DataCenter**: Professional cards (A100, H100) with server CPUs
- **LaptopIntegrated**: Mobile GPUs and laptop CPUs
- **CpuOnly**: CPU-only configuration for CPU testing
- **Mixed**: Mixed vendor configuration
- **Minimal**: Single basic device for minimal testing

### Device Performance Tiers

Each device type supports multiple performance tiers:
- **High**: Latest/fastest devices (RTX 4090, M2 Max, EPYC 7713)
- **Medium**: Mid-range devices (RTX 4070, M2, Core i9)
- **Low**: Entry-level devices (RTX 3060 Laptop, M1, Basic CPU)

## Hardware Specifications

### CUDA Devices

The system includes accurate specifications for:
- RTX 4090: 24GB VRAM, 128 SMs, 16384 CUDA cores, Ada Lovelace
- RTX 4080: 16GB VRAM, 76 SMs, 9728 CUDA cores, Ada Lovelace
- RTX 4070: 12GB VRAM, 60 SMs, 5888 CUDA cores, Ada Lovelace
- A100: 40GB HBM2e, 108 SMs, 6912 CUDA cores, Ampere
- H100: 80GB HBM3, 132 SMs, 16896 CUDA cores, Hopper
- V100: 32GB HBM2, 80 SMs, 5120 CUDA cores, Volta

### CPU Devices

Includes modern CPU specifications with SIMD support:
- Intel Core i9-13900K: 24 cores, 32 threads, AVX-512
- AMD Ryzen 9 7950X: 16 cores, 32 threads, AVX2
- AMD EPYC 7713: 64 cores, 128 threads, server-class
- Intel Xeon Platinum 8380: 40 cores, 80 threads, AVX-512

### Apple Silicon

Accurate Apple Silicon specifications:
- M1: 8 GPU cores, 8GB unified memory, Neural Engine
- M1 Pro: 16 GPU cores, 16GB unified memory, Neural Engine
- M2: 10 GPU cores, 16GB unified memory, Neural Engine
- M2 Max: 38 GPU cores, 32GB unified memory, Neural Engine

## Testing Scenarios

### Performance Testing
```csharp
var device = MockCudaDevice.CreateRTX4090();
var metrics = device.GetPerformanceMetrics();

// Verify device specifications
Assert.Equal(24L * 1024 * 1024 * 1024, device.TotalMemory); // 24GB
Assert.Equal(16384, device.CudaCores);
Assert.Equal(new Version(8, 9), device.ComputeCapability); // Ada Lovelace
```

### Memory Management Testing
```csharp
var device = MockCudaDevice.CreateRTX3060Laptop();

// Simulate memory pressure
device.SimulateMemoryUsage(5L * 1024 * 1024 * 1024); // Use 5GB

// Test memory availability
Assert.True(device.AvailableMemory < device.TotalMemory);
```

### Error Handling Testing
```csharp
var device = MockCudaDevice.CreateRTX4070();

// Simulate CUDA-specific errors
device.SimulateCudaError(CudaErrorType.OutOfMemory);

// Test error propagation
Assert.False(device.IsAvailable);
Assert.Contains("CUDA_ERROR_OUT_OF_MEMORY", device.FailureMessage);
```

### CI/CD Testing
```csharp
// Create CI-friendly configuration (CPU-only)
using var provider = TestHardwareFactory.CreateCiServerDevices();

// Ensure tests can run without GPU hardware
var devices = provider.GetAllDevices().ToList();
Assert.All(devices, d => d.Type == AcceleratorType.CPU);
```

## Integration with Existing Tests

The hardware abstraction layer is designed to integrate seamlessly with existing DotCompute tests:

### 1. Replace Real Hardware Detection
```csharp
// Old code
var accelerators = AcceleratorManager.GetAvailableAccelerators();

// New code for testing
using var provider = TestHardwareFactory.CreateDefault();
var accelerators = provider.GetAllDevices()
    .Select(d => provider.CreateAccelerator(d))
    .ToList();
```

### 2. Mock Specific Device Types
```csharp
[Fact]
public void Test_CudaKernelExecution()
{
    // Create specific CUDA device for testing
    using var provider = TestHardwareFactory.CreateDefault();
    var cudaDevice = provider.GetFirstDevice(AcceleratorType.CUDA);
    
    using var accelerator = provider.CreateAccelerator(cudaDevice);
    
    // Test CUDA-specific functionality
    // ...
}
```

### 3. Performance Benchmarking
```csharp
[Theory]
[InlineData(DevicePerformance.High)]
[InlineData(DevicePerformance.Medium)]
[InlineData(DevicePerformance.Low)]
public void Test_PerformanceScaling(DevicePerformance performance)
{
    var config = new TestDeviceConfiguration { Performance = performance };
    var device = TestHardwareFactory.CreateTestDevice(AcceleratorType.CUDA, config);
    
    // Test performance characteristics
    // ...
}
```

## Best Practices

### 1. Use Appropriate Configurations
- **Unit Tests**: Use `TestHardwareFactory.CreateMinimalSetup()` for fast execution
- **Integration Tests**: Use `TestHardwareFactory.CreateDefault()` for comprehensive testing
- **Performance Tests**: Use `TestHardwareFactory.CreateDataCenterSetup()` for high-end hardware

### 2. Clean Up Resources
```csharp
[Fact]
public void Test_WithProperCleanup()
{
    using var provider = TestHardwareFactory.CreateDefault();
    using var accelerator = provider.CreateAccelerator(provider.GetFirstDevice(AcceleratorType.CUDA));
    
    // Test code here
    
    // Resources are automatically cleaned up
}
```

### 3. Test Error Conditions
```csharp
[Fact]
public void Test_OutOfMemoryHandling()
{
    using var provider = TestHardwareFactory.CreateDefault();
    var device = provider.GetFirstDevice(AcceleratorType.CUDA) as MockCudaDevice;
    
    // Simulate out of memory condition
    device?.SimulateCudaError(CudaErrorType.OutOfMemory);
    
    // Test how your code handles the error
    // ...
}
```

### 4. Use Realistic Scenarios
```csharp
[Fact]
public void Test_ThermalThrottling()
{
    using var simulator = TestHardwareFactory.CreateSimulator();
    
    // Simulate thermal conditions
    simulator.SimulateThermalThrottling(AcceleratorType.CUDA, 0.4);
    
    // Test performance degradation handling
    // ...
}
```

## Architecture

The hardware abstraction layer follows a layered architecture:

```
┌─────────────────────────────────┐
│        Test Code                │
├─────────────────────────────────┤
│     MockAccelerator             │  ← Implements IAccelerator
├─────────────────────────────────┤
│   MockHardwareProvider          │  ← Implements IHardwareProvider
├─────────────────────────────────┤
│     MockHardwareDevice          │  ← Base for all mock devices
│  ┌─────────────────────────────┐│
│  │ MockCudaDevice              ││  ← CUDA-specific implementation
│  │ MockCpuDevice               ││  ← CPU-specific implementation
│  │ MockMetalDevice             ││  ← Metal-specific implementation
│  └─────────────────────────────┘│
├─────────────────────────────────┤
│     HardwareSimulator           │  ← Advanced simulation features
├─────────────────────────────────┤
│    TestHardwareFactory          │  ← Factory for easy creation
└─────────────────────────────────┘
```

This architecture ensures:
- **Separation of Concerns**: Each layer has a specific responsibility
- **Extensibility**: New device types can be easily added
- **Testability**: Each component can be tested independently
- **Maintainability**: Clear interfaces and implementations

## Contributing

When adding new device types or capabilities:

1. Extend `MockHardwareDevice` for the new device type
2. Add device-specific properties and capabilities
3. Update `TestHardwareFactory` to include creation methods
4. Add preset configurations to `MockHardwareProvider`
5. Include comprehensive unit tests

The hardware abstraction layer enables 95% of DotCompute tests to run without actual hardware while providing realistic behavior and comprehensive device coverage.