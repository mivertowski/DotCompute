// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Memory.Tests;

/// <summary>
/// Comprehensive tests for AcceleratorContext covering all functionality.
/// Target: 100% coverage for 60-line data class.
/// </summary>
public class AcceleratorContextComprehensiveTests
{
    #region Constructor and Property Tests

    [Fact]
    public void Constructor_WithAllProperties_InitializesCorrectly()
    {
        // Arrange
        const int deviceId = 5;
        var stream = new IntPtr(12345);
        var type = AcceleratorType.CUDA;
        var customContext = new { Data = "test" };

        // Act
        var context = new AcceleratorContext
        {
            DeviceId = deviceId,
            Stream = stream,
            Type = type,
            CustomContext = customContext
        };

        // Assert
        _ = context.DeviceId.Should().Be(deviceId);
        _ = context.Stream.Should().Be(stream);
        _ = context.Type.Should().Be(type);
        _ = context.CustomContext.Should().Be(customContext);
    }

    [Fact]
    public void DeviceId_WithZero_InitializesCorrectly()
    {
        // Arrange & Act
        var context = new AcceleratorContext
        {
            DeviceId = 0,
            Stream = IntPtr.Zero,
            Type = AcceleratorType.CPU
        };

        // Assert
        _ = context.DeviceId.Should().Be(0);
    }

    [Fact]
    public void DeviceId_WithPositiveValue_InitializesCorrectly()
    {
        // Arrange & Act
        var context = new AcceleratorContext
        {
            DeviceId = 42,
            Stream = IntPtr.Zero,
            Type = AcceleratorType.CPU
        };

        // Assert
        _ = context.DeviceId.Should().Be(42);
    }

    [Fact]
    public void DeviceId_WithNegativeValue_InitializesCorrectly()
    {
        // Arrange & Act
        var context = new AcceleratorContext
        {
            DeviceId = -1,
            Stream = IntPtr.Zero,
            Type = AcceleratorType.CPU
        };

        // Assert
        _ = context.DeviceId.Should().Be(-1);
    }

    [Fact]
    public void Stream_WithZeroPointer_InitializesCorrectly()
    {
        // Arrange & Act
        var context = new AcceleratorContext
        {
            DeviceId = 0,
            Stream = IntPtr.Zero,
            Type = AcceleratorType.CPU
        };

        // Assert
        _ = context.Stream.Should().Be(IntPtr.Zero);
    }

    [Fact]
    public void Stream_WithValidPointer_InitializesCorrectly()
    {
        // Arrange
        var streamPtr = new IntPtr(0x1234567890ABCDEF);

        // Act
        var context = new AcceleratorContext
        {
            DeviceId = 0,
            Stream = streamPtr,
            Type = AcceleratorType.CUDA
        };

        // Assert
        _ = context.Stream.Should().Be(streamPtr);
    }

    [Fact]
    public void Type_WithCPU_InitializesCorrectly()
    {
        // Arrange & Act
        var context = new AcceleratorContext
        {
            DeviceId = 0,
            Stream = IntPtr.Zero,
            Type = AcceleratorType.CPU
        };

        // Assert
        _ = context.Type.Should().Be(AcceleratorType.CPU);
    }

    [Fact]
    public void Type_WithCUDA_InitializesCorrectly()
    {
        // Arrange & Act
        var context = new AcceleratorContext
        {
            DeviceId = 0,
            Stream = IntPtr.Zero,
            Type = AcceleratorType.CUDA
        };

        // Assert
        _ = context.Type.Should().Be(AcceleratorType.CUDA);
    }

    [Fact]
    public void Type_WithMetal_InitializesCorrectly()
    {
        // Arrange & Act
        var context = new AcceleratorContext
        {
            DeviceId = 0,
            Stream = IntPtr.Zero,
            Type = AcceleratorType.Metal
        };

        // Assert
        _ = context.Type.Should().Be(AcceleratorType.Metal);
    }

    [Fact]
    public void Type_WithROCm_InitializesCorrectly()
    {
        // Arrange & Act
        var context = new AcceleratorContext
        {
            DeviceId = 0,
            Stream = IntPtr.Zero,
            Type = AcceleratorType.ROCm
        };

        // Assert
        _ = context.Type.Should().Be(AcceleratorType.ROCm);
    }

    [Fact]
    public void Type_WithOpenCL_InitializesCorrectly()
    {
        // Arrange & Act
        var context = new AcceleratorContext
        {
            DeviceId = 0,
            Stream = IntPtr.Zero,
            Type = AcceleratorType.OpenCL
        };

        // Assert
        _ = context.Type.Should().Be(AcceleratorType.OpenCL);
    }

    [Fact]
    public void CustomContext_WithNull_InitializesCorrectly()
    {
        // Arrange & Act
        var context = new AcceleratorContext
        {
            DeviceId = 0,
            Stream = IntPtr.Zero,
            Type = AcceleratorType.CPU,
            CustomContext = null
        };

        // Assert
        _ = context.CustomContext.Should().BeNull();
    }

    [Fact]
    public void CustomContext_WithObject_InitializesCorrectly()
    {
        // Arrange
        var customData = new { Name = "TestDevice", Version = "1.0" };

        // Act
        var context = new AcceleratorContext
        {
            DeviceId = 0,
            Stream = IntPtr.Zero,
            Type = AcceleratorType.CUDA,
            CustomContext = customData
        };

        // Assert
        _ = context.CustomContext.Should().Be(customData);
        _ = context.CustomContext.Should().NotBeNull();
    }

    [Fact]
    public void CustomContext_WithString_InitializesCorrectly()
    {
        // Arrange
        const string customData = "TestContext";

        // Act
        var context = new AcceleratorContext
        {
            DeviceId = 0,
            Stream = IntPtr.Zero,
            Type = AcceleratorType.Metal,
            CustomContext = customData
        };

        // Assert
        _ = context.CustomContext.Should().Be(customData);
    }

    [Fact]
    public void CustomContext_WithComplexObject_InitializesCorrectly()
    {
        // Arrange
        var complexData = new CustomContextData
        {
            DeviceName = "NVIDIA RTX 4090",
            MemorySize = 24L * 1024 * 1024 * 1024,
            ComputeCapability = "8.9"
        };

        // Act
        var context = new AcceleratorContext
        {
            DeviceId = 0,
            Stream = IntPtr.Zero,
            Type = AcceleratorType.CUDA,
            CustomContext = complexData
        };

        // Assert
        _ = context.CustomContext.Should().Be(complexData);
        var retrievedData = context.CustomContext as CustomContextData;
        _ = retrievedData.Should().NotBeNull();
        _ = retrievedData!.DeviceName.Should().Be("NVIDIA RTX 4090");
        _ = retrievedData.MemorySize.Should().Be(24L * 1024 * 1024 * 1024);
        _ = retrievedData.ComputeCapability.Should().Be("8.9");
    }

    #endregion

    #region AcceleratorType Enum Tests

    [Fact]
    public void AcceleratorType_CPUValue_IsCorrect()
    {
        // Act
        var cpuType = AcceleratorType.CPU;

        // Assert
        _ = cpuType.Should().Be(AcceleratorType.CPU);
        _ = ((int)cpuType).Should().Be(0);
    }

    [Fact]
    public void AcceleratorType_CUDAValue_IsCorrect()
    {
        // Act
        var cudaType = AcceleratorType.CUDA;

        // Assert
        _ = cudaType.Should().Be(AcceleratorType.CUDA);
        _ = ((int)cudaType).Should().Be(1);
    }

    [Fact]
    public void AcceleratorType_MetalValue_IsCorrect()
    {
        // Act
        var metalType = AcceleratorType.Metal;

        // Assert
        _ = metalType.Should().Be(AcceleratorType.Metal);
        _ = ((int)metalType).Should().Be(2);
    }

    [Fact]
    public void AcceleratorType_ROCmValue_IsCorrect()
    {
        // Act
        var rocmType = AcceleratorType.ROCm;

        // Assert
        _ = rocmType.Should().Be(AcceleratorType.ROCm);
        _ = ((int)rocmType).Should().Be(3);
    }

    [Fact]
    public void AcceleratorType_OpenCLValue_IsCorrect()
    {
        // Act
        var openclType = AcceleratorType.OpenCL;

        // Assert
        _ = openclType.Should().Be(AcceleratorType.OpenCL);
        _ = ((int)openclType).Should().Be(4);
    }

    [Theory]
    [InlineData(AcceleratorType.CPU, "CPU")]
    [InlineData(AcceleratorType.CUDA, "CUDA")]
    [InlineData(AcceleratorType.Metal, "Metal")]
    [InlineData(AcceleratorType.ROCm, "ROCm")]
    [InlineData(AcceleratorType.OpenCL, "OpenCL")]
    public void AcceleratorType_ToString_ReturnsCorrectName(AcceleratorType type, string expectedName)
    {
        // Act
        var name = type.ToString();

        // Assert
        _ = name.Should().Be(expectedName);
    }

    [Fact]
    public void AcceleratorType_AllValuesAreDefined()
    {
        // Arrange
        var expectedTypes = new[]
        {
            AcceleratorType.CPU,
            AcceleratorType.CUDA,
            AcceleratorType.Metal,
            AcceleratorType.ROCm,
            AcceleratorType.OpenCL
        };

        // Act
        var definedTypes = Enum.GetValues<AcceleratorType>();

        // Assert
        _ = definedTypes.Should().BeEquivalentTo(expectedTypes);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Context_MultipleInstances_AreIndependent()
    {
        // Arrange & Act
        var context1 = new AcceleratorContext
        {
            DeviceId = 0,
            Stream = new IntPtr(1000),
            Type = AcceleratorType.CPU
        };

        var context2 = new AcceleratorContext
        {
            DeviceId = 1,
            Stream = new IntPtr(2000),
            Type = AcceleratorType.CUDA
        };

        // Assert
        _ = context1.DeviceId.Should().NotBe(context2.DeviceId);
        _ = context1.Stream.Should().NotBe(context2.Stream);
        _ = context1.Type.Should().NotBe(context2.Type);
    }

    [Fact]
    public void Context_CPUScenario_WorksCorrectly()
    {
        // Arrange & Act
        var context = new AcceleratorContext
        {
            DeviceId = 0,
            Stream = IntPtr.Zero,
            Type = AcceleratorType.CPU,
            CustomContext = new { ThreadCount = Environment.ProcessorCount }
        };

        // Assert
        _ = context.DeviceId.Should().Be(0);
        _ = context.Stream.Should().Be(IntPtr.Zero);
        _ = context.Type.Should().Be(AcceleratorType.CPU);
        _ = context.CustomContext.Should().NotBeNull();
    }

    [Fact]
    public void Context_GPUScenario_WorksCorrectly()
    {
        // Arrange & Act
        var context = new AcceleratorContext
        {
            DeviceId = 0,
            Stream = new IntPtr(0x12345),
            Type = AcceleratorType.CUDA,
            CustomContext = new { CudaStream = 0x12345, Device = "RTX 4090" }
        };

        // Assert
        _ = context.DeviceId.Should().Be(0);
        _ = context.Stream.Should().NotBe(IntPtr.Zero);
        _ = context.Type.Should().Be(AcceleratorType.CUDA);
        _ = context.CustomContext.Should().NotBeNull();
    }

    [Fact]
    public void Context_MetalScenario_WorksCorrectly()
    {
        // Arrange & Act
        var context = new AcceleratorContext
        {
            DeviceId = 0,
            Stream = new IntPtr(0xABCDEF),
            Type = AcceleratorType.Metal,
            CustomContext = new { MetalCommandQueue = 0xABCDEF, Device = "M3 Max" }
        };

        // Assert
        _ = context.DeviceId.Should().Be(0);
        _ = context.Stream.Should().NotBe(IntPtr.Zero);
        _ = context.Type.Should().Be(AcceleratorType.Metal);
        _ = context.CustomContext.Should().NotBeNull();
    }

    #endregion

    #region Helper Classes

    private class CustomContextData
    {
        public string DeviceName { get; set; } = string.Empty;
        public long MemorySize { get; set; }
        public string ComputeCapability { get; set; } = string.Empty;
    }

    #endregion
}
