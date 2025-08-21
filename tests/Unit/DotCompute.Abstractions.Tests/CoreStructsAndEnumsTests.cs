// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Xunit;
using FluentAssertions;

using DotCompute.Abstractions.Enums;
using DotCompute.Abstractions.Accelerators;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
namespace DotCompute.Abstractions.Tests;


/// <summary>
/// Comprehensive unit tests for core structs, enums, and supporting classes in DotCompute.Abstractions.
/// </summary>
public sealed class CoreStructsAndEnumsTests
{
    private static readonly string[] OptimizationFlags = new[] { "-O3", "-ffast-math" };

    #region CompiledKernel Struct Tests


    [Fact]
    public void CompiledKernel_Constructor_ShouldInitializeAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var handle = new IntPtr(0x12345678);
        const int sharedMemorySize = 1024;
        var configuration = new KernelConfiguration(
            new Dim3(256, 1, 1),
            new Dim3(32, 32, 1)
        );

        // Act
        using var compiledKernel = new CompiledKernel
        {
            Name = "TestKernel",
            Id = id.ToString(),
            CompiledBinary = null,
            Metadata = new Dictionary<string, object>
            {
                ["Id"] = id,
                ["NativeHandle"] = handle,
                ["SharedMemorySize"] = sharedMemorySize,
                ["Configuration"] = configuration
            }
        };

        // Assert
        _ = compiledKernel.Id.Should().Be(id.ToString());
        _ = compiledKernel.Metadata["NativeHandle"].Should().Be(handle);
        _ = compiledKernel.Metadata["SharedMemorySize"].Should().Be(sharedMemorySize);
        _ = compiledKernel.Metadata["Configuration"].Should().Be(configuration);
    }

    [Fact]
    public void CompiledKernel_Equality_WithIdenticalValues_ShouldReturnTrue()
    {
        // Arrange
        var id = Guid.NewGuid();
        var handle = new IntPtr(0x1000);
        const int sharedMemorySize = 512;
        var configuration = new KernelConfiguration(new Dim3(128), new Dim3(16, 16));

        using var kernel1 = new CompiledKernel
        {
            Name = "TestKernel1",
            Id = id.ToString(),
            Metadata = new Dictionary<string, object>
            {
                ["Id"] = id,
                ["NativeHandle"] = handle,
                ["SharedMemorySize"] = sharedMemorySize,
                ["Configuration"] = configuration
            }
        };
        using var kernel2 = new CompiledKernel
        {
            Name = "TestKernel2",
            Id = id.ToString(),
            Metadata = new Dictionary<string, object>
            {
                ["Id"] = id,
                ["NativeHandle"] = handle,
                ["SharedMemorySize"] = sharedMemorySize,
                ["Configuration"] = configuration
            }
        };

        // Act & Assert
        _ = kernel1.Equals(kernel2).Should().BeTrue();
        _ = (kernel1 == kernel2).Should().BeTrue();
        _ = (kernel1 != kernel2).Should().BeFalse();
    }

    [Fact]
    public void CompiledKernel_Equality_WithDifferentIds_ShouldReturnFalse()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var handle = new IntPtr(0x1000);
        var config = new KernelConfiguration(new Dim3(128), new Dim3(16, 16));

        using var kernel1 = new CompiledKernel
        {
            Name = "TestKernel1",
            Id = id1.ToString(),
            Metadata = new Dictionary<string, object>
            {
                ["Id"] = id1,
                ["NativeHandle"] = handle,
                ["SharedMemorySize"] = 512,
                ["Configuration"] = config
            }
        };
        using var kernel2 = new CompiledKernel
        {
            Name = "TestKernel2",
            Id = id2.ToString(),
            Metadata = new Dictionary<string, object>
            {
                ["Id"] = id2,
                ["NativeHandle"] = handle,
                ["SharedMemorySize"] = 512,
                ["Configuration"] = config
            }
        };

        // Act & Assert
        _ = kernel1.Equals(kernel2).Should().BeFalse();
        _ = (kernel1 == kernel2).Should().BeFalse();
        _ = (kernel1 != kernel2).Should().BeTrue();
    }

    [Fact]
    public void CompiledKernel_GetHashCode_WithEqualInstances_ShouldReturnSameHashCode()
    {
        // Arrange
        var id = Guid.NewGuid();
        var handle = new IntPtr(0x2000);
        var config = new KernelConfiguration(new Dim3(64), new Dim3(8, 8));

        using var kernel1 = new CompiledKernel
        {
            Name = "TestKernel1",
            Id = id.ToString(),
            Metadata = new Dictionary<string, object>
            {
                ["Id"] = id,
                ["NativeHandle"] = handle,
                ["SharedMemorySize"] = 256,
                ["Configuration"] = config
            }
        };
        using var kernel2 = new CompiledKernel
        {
            Name = "TestKernel2",
            Id = id.ToString(),
            Metadata = new Dictionary<string, object>
            {
                ["Id"] = id,
                ["NativeHandle"] = handle,
                ["SharedMemorySize"] = 256,
                ["Configuration"] = config
            }
        };

        // Act
        var hashCode1 = kernel1.GetHashCode();
        var hashCode2 = kernel2.GetHashCode();

        // Assert
        Assert.Equal(hashCode2, hashCode1);
    }

    #endregion

    #region KernelConfiguration Struct Tests

    [Fact]
    public void KernelConfiguration_Constructor_ShouldInitializeCorrectly()
    {
        // Arrange
        var gridDim = new Dim3(32, 32, 1);
        var blockDim = new Dim3(16, 16, 1);

        // Act
        var config = new KernelConfiguration(gridDim, blockDim);

        // Assert
        _ = ((Dim3)config.Options["GridDimension"]).Should().Be(gridDim);
        _ = ((Dim3)config.Options["BlockDimension"]).Should().Be(blockDim);
    }

    [Fact]
    public void KernelConfiguration_Equality_WithIdenticalValues_ShouldReturnTrue()
    {
        // Arrange
        var gridDim = new Dim3(64, 1, 1);
        var blockDim = new Dim3(256, 1, 1);

        var config1 = new KernelConfiguration(gridDim, blockDim);
        var config2 = new KernelConfiguration(gridDim, blockDim);

        // Act & Assert
        _ = config1.Equals(config2).Should().BeTrue();
        _ = (config1 == config2).Should().BeTrue();
        _ = (config1 != config2).Should().BeFalse();
    }

    [Fact]
    public void KernelConfiguration_GetHashCode_WithEqualInstances_ShouldReturnSameHashCode()
    {
        // Arrange
        var gridDim = new Dim3(128, 1, 1);
        var blockDim = new Dim3(32, 32, 1);

        var config1 = new KernelConfiguration(gridDim, blockDim);
        var config2 = new KernelConfiguration(gridDim, blockDim);

        // Act & Assert
        _ = config1.GetHashCode().Should().Be(config2.GetHashCode());
    }

    #endregion

    #region KernelDefinition Class Tests

    [Fact]
    public void KernelDefinition_Constructor_WithValidParameters_ShouldInitializeCorrectly()
    {
        // Arrange
        const string name = "TestKernel";
        var source = new TextKernelSource("__global__ void test() {}", "test", KernelLanguage.Cuda);

        // Act
        var definition = new KernelDefinition { Name = name, Source = source.Code };

        // Assert
        _ = definition.Name.Should().Be(name);
        _ = definition.Code.Should().NotBeNullOrEmpty();
        _ = definition.EntryPoint.Should().Be(source.EntryPoint);
        _ = definition.Metadata.Should().ContainKey("Language");
        _ = definition.Metadata.Should().ContainKey("Dependencies");
        _ = definition.Metadata.Should().ContainKey("CompilationOptions");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void KernelDefinition_Constructor_WithInvalidName_ShouldThrowArgumentException(string invalidName)
    {
        // Arrange
        var source = new TextKernelSource("code", "test");
        var options = new CompilationOptions();

        // Act & Assert
        var action = () => new KernelDefinition { Name = invalidName, Source = source.Code };
        _ = action.Should().Throw<ArgumentException>().And.ParamName.Should().Be("name");
    }

    [Fact]
    public void KernelDefinition_Constructor_WithNullSource_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = new CompilationOptions();

        // Act & Assert
        var action = () => new KernelDefinition { Name = "TestKernel", Source = null! };
        _ = action.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("source");
    }

    [Fact]
    public void KernelDefinition_Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange
        var source = new TextKernelSource("code", "test");

        // Act & Assert
        var action = () => new KernelDefinition { Name = "TestKernel", Source = source.Code };
        _ = action.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("options");
    }

    #endregion

    #region CompilationOptions Class Tests

    [Fact]
    public void CompilationOptions_DefaultValues_ShouldBeSetCorrectly()
    {
        // Act
        var options = new CompilationOptions();

        // Assert
        _ = options.OptimizationLevel.Should().Be(OptimizationLevel.Default);
        _ = options.EnableDebugInfo.Should().BeFalse();
        _ = options.AdditionalFlags.Should().NotBeNull().And.BeEmpty();
        _ = options.Defines.Should().NotBeNull().And.BeEmpty();
        _ = options.EnableFastMath.Should().BeTrue();
        _ = options.UnrollLoops.Should().BeFalse();
        _ = options.PreferredBlockSize.Should().Be(new Dim3(256, 1, 1));
        _ = options.SharedMemorySize.Should().Be(0);
    }

    [Fact]
    public void CompilationOptions_InitPropertySetters_ShouldWork()
    {
        // Arrange & Act
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Maximum,
            EnableDebugInfo = true,
            AdditionalFlags = ["-O3", "-ffast-math"],
            Defines = new Dictionary<string, string> { ["DEBUG"] = "1" },
            EnableFastMath = true,
            UnrollLoops = true,
            PreferredBlockSize = new Dim3(512, 1, 1),
            SharedMemorySize = 2048
        };

        // Assert
        _ = options.OptimizationLevel.Should().Be(OptimizationLevel.Maximum);
        _ = options.EnableDebugInfo.Should().BeTrue();
        _ = options.AdditionalFlags.Should().BeEquivalentTo(OptimizationFlags);
        _ = options.Defines.Should().ContainKey("DEBUG");
        _ = options.EnableFastMath.Should().BeTrue();
        _ = options.UnrollLoops.Should().BeTrue();
        _ = options.PreferredBlockSize.Should().Be(new Dim3(512, 1, 1));
        _ = options.SharedMemorySize.Should().Be(2048);
    }

    #endregion

    #region KernelParameter Class Tests

    [Fact]
    public void KernelParameter_Constructor_WithValidParameters_ShouldInitializeCorrectly()
    {
        // Arrange
        const string name = "inputBuffer";
        var type = typeof(float[]);
        const int index = 0;
        const bool isInput = true;
        const bool isOutput = false;
        const bool isConstant = false;

        // Act
        var parameter = new KernelParameter(name, type, index, isInput, isOutput, isConstant);

        // Assert
        _ = parameter.Name.Should().Be(name);
        _ = parameter.Type.Should().Be(type);
        _ = parameter.Index.Should().Be(index);
        _ = parameter.IsInput.Should().Be(isInput);
        _ = parameter.IsOutput.Should().Be(isOutput);
        _ = parameter.IsConstant.Should().Be(isConstant);
        _ = parameter.IsReadOnly.Should().Be(true); // IsInput && !IsOutput
        _ = parameter.MemorySpace.Should().Be(MemorySpace.Global); // Default
    }

    [Fact]
    public void KernelParameter_Constructor_WithNullName_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => new KernelParameter(null!, typeof(int), 0);
        _ = action.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("name");
    }

    [Fact]
    public void KernelParameter_Constructor_WithNullType_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => new KernelParameter("test", null!, 0);
        _ = action.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("type");
    }

    [Fact]
    public void KernelParameter_IsReadOnly_ShouldReturnCorrectValue()
    {
        // Arrange & Act
        var readOnlyParam = new KernelParameter("test1", typeof(int), 0, isInput: true, isOutput: false);
        var writeOnlyParam = new KernelParameter("test2", typeof(int), 1, isInput: false, isOutput: true);
        var readWriteParam = new KernelParameter("test3", typeof(int), 2, isInput: true, isOutput: true);

        // Assert
        _ = readOnlyParam.IsReadOnly.Should().BeTrue();
        _ = writeOnlyParam.IsReadOnly.Should().BeFalse();
        _ = readWriteParam.IsReadOnly.Should().BeFalse();
    }

    [Fact]
    public void KernelParameter_MemorySpace_CanBeSet()
    {
        // Arrange & Act
        var parameter = new KernelParameter("test", typeof(int), 0)
        {
            MemorySpace = MemorySpace.Shared
        };

        // Assert
        _ = parameter.MemorySpace.Should().Be(MemorySpace.Shared);
    }

    #endregion

    #region Enum Tests

    [Fact]
    public void AcceleratorType_ShouldHaveExpectedValues()
    {
        // Arrange
        var expectedValues = new[]
        {
        AcceleratorType.CPU,
        AcceleratorType.CUDA,
        AcceleratorType.ROCm,
        AcceleratorType.OneAPI,
        AcceleratorType.Metal,
        AcceleratorType.OpenCL,
        AcceleratorType.DirectML,
        AcceleratorType.GPU,
        AcceleratorType.FPGA,
        AcceleratorType.TPU,
        AcceleratorType.Custom
    };

        // Act
        var actualValues = Enum.GetValues<AcceleratorType>();

        // Assert
        _ = actualValues.Should().BeEquivalentTo(expectedValues);
    }

    [Theory]
    [InlineData(AcceleratorType.CPU, 1)]
    [InlineData(AcceleratorType.CUDA, 2)]
    [InlineData(AcceleratorType.ROCm, 3)]
    [InlineData(AcceleratorType.OneAPI, 4)]
    [InlineData(AcceleratorType.Metal, 5)]
    [InlineData(AcceleratorType.OpenCL, 6)]
    [InlineData(AcceleratorType.DirectML, 7)]
    [InlineData(AcceleratorType.GPU, 8)]
    [InlineData(AcceleratorType.FPGA, 9)]
    [InlineData(AcceleratorType.TPU, 10)]
    [InlineData(AcceleratorType.Custom, 100)]
    public void AcceleratorType_ShouldHaveCorrectIntegerValues(AcceleratorType type, int expectedValue)
        // Act & Assert

        => ((int)type).Should().Be(expectedValue);

    [Fact]
    public void MemoryOptions_ShouldBeFlagsEnum()
    {
        // Arrange
        var type = typeof(MemoryOptions);

        // Act & Assert
        _ = type.Should().BeDecoratedWith<FlagsAttribute>();

        // Test flag combinations
        var combined = MemoryOptions.ReadOnly | MemoryOptions.HostVisible | MemoryOptions.Cached;
        _ = combined.Should().HaveFlag(MemoryOptions.ReadOnly);
        _ = combined.Should().HaveFlag(MemoryOptions.HostVisible);
        _ = combined.Should().HaveFlag(MemoryOptions.Cached);
        _ = combined.Should().NotHaveFlag(MemoryOptions.WriteOnly);
    }

    [Fact]
    public void MapMode_ShouldBeFlagsEnum()
    {
        // Arrange
        var type = typeof(MapMode);

        // Act & Assert
        _ = type.Should().BeDecoratedWith<FlagsAttribute>();

        // Test flag combinations
        var readWrite = MapMode.Read | MapMode.Write;
        Assert.Equal(MapMode.ReadWrite, readWrite);

        var complexMode = MapMode.ReadWrite | MapMode.Discard | MapMode.NoWait;
        _ = complexMode.Should().HaveFlag(MapMode.Read);
        _ = complexMode.Should().HaveFlag(MapMode.Write);
        _ = complexMode.Should().HaveFlag(MapMode.Discard);
        _ = complexMode.Should().HaveFlag(MapMode.NoWait);
    }

    [Theory]
    [InlineData(OptimizationLevel.None)]
    [InlineData(OptimizationLevel.Debug)]
    [InlineData(OptimizationLevel.Default)]
    [InlineData(OptimizationLevel.Release)]
    [InlineData(OptimizationLevel.Maximum)]
    [InlineData(OptimizationLevel.Aggressive)]
    public void OptimizationLevel_ShouldHaveAllExpectedValues(OptimizationLevel level)
    {
        // Act
        var allValues = Enum.GetValues<OptimizationLevel>();

        // Assert
        Assert.Contains(level, allValues);
    }

    [Theory]
    [InlineData(KernelLanguage.Cuda)]
    [InlineData(KernelLanguage.OpenCL)]
    [InlineData(KernelLanguage.Ptx)]
    [InlineData(KernelLanguage.HLSL)]
    [InlineData(KernelLanguage.SPIRV)]
    [InlineData(KernelLanguage.Metal)]
    [InlineData(KernelLanguage.HIP)]
    [InlineData(KernelLanguage.SYCL)]
    [InlineData(KernelLanguage.CSharpIL)]
    [InlineData(KernelLanguage.Binary)]
    public void KernelLanguage_ShouldHaveAllExpectedValues(KernelLanguage language)
    {
        // Act
        var allValues = Enum.GetValues<KernelLanguage>();

        // Assert
        Assert.Contains(language, allValues);
    }

    [Theory]
    [InlineData(MemorySpace.Global)]
    [InlineData(MemorySpace.Local)]
    [InlineData(MemorySpace.Shared)]
    [InlineData(MemorySpace.Constant)]
    [InlineData(MemorySpace.Private)]
    public void MemorySpace_ShouldHaveAllExpectedValues(MemorySpace space)
    {
        // Act
        var allValues = Enum.GetValues<MemorySpace>();

        // Assert
        Assert.Contains(space, allValues);
    }

    [Fact]
    public void AcceleratorFeature_ShouldBeFlagsEnum()
    {
        // Arrange
        var type = typeof(AcceleratorFeature);

        // Act & Assert
        _ = type.Should().BeDecoratedWith<FlagsAttribute>();

        // Test feature combinations
        var features = AcceleratorFeature.Float16 | AcceleratorFeature.DoublePrecision | AcceleratorFeature.TensorCores;
        _ = features.Should().HaveFlag(AcceleratorFeature.Float16);
        _ = features.Should().HaveFlag(AcceleratorFeature.DoublePrecision);
        _ = features.Should().HaveFlag(AcceleratorFeature.TensorCores);
        _ = features.Should().NotHaveFlag(AcceleratorFeature.UnifiedMemory);
    }

    #endregion

    #region TextKernelSource and BytecodeKernelSource Tests

    [Fact]
    public void TextKernelSource_Constructor_WithValidParameters_ShouldInitializeCorrectly()
    {
        // Arrange
        const string code = "__global__ void vectorAdd(float* a, float* b, float* c) { }";
        const string name = "vectorAdd";
        const KernelLanguage language = KernelLanguage.Cuda;
        const string entryPoint = "vectorAdd";
        var dependencies = new[] { "math.h", "stdio.h" };

        // Act
        var source = new TextKernelSource(code, name, language, entryPoint, dependencies);

        // Assert
        _ = source.Code.Should().Be(code);
        _ = source.Name.Should().Be(name);
        _ = source.Language.Should().Be(language);
        _ = source.EntryPoint.Should().Be(entryPoint);
        _ = source.Dependencies.Should().BeEquivalentTo(dependencies);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TextKernelSource_Constructor_WithInvalidCode_ShouldThrowArgumentException(string invalidCode)
    {
        // Act & Assert
        var action = () => new TextKernelSource(invalidCode);
        _ = action.Should().Throw<ArgumentException>().And.ParamName.Should().Be("code");
    }

    [Fact]
    public void BytecodeKernelSource_Constructor_WithValidBytecode_ShouldInitializeCorrectly()
    {
        // Arrange
        var bytecode = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello" in bytes
        const string name = "compiledKernel";
        const KernelLanguage language = KernelLanguage.Binary;
        const string entryPoint = "main";

        // Act
        var source = new BytecodeKernelSource(bytecode, name, language, entryPoint);

        // Assert
        _ = source.Name.Should().Be(name);
        _ = source.Language.Should().Be(language);
        _ = source.EntryPoint.Should().Be(entryPoint);
        _ = source.Code.Should().Be(Convert.ToBase64String(bytecode));
        _ = source.Dependencies.Should().NotBeNull().And.HaveCount(0);
    }

    [Fact]
    public void BytecodeKernelSource_Constructor_WithNullBytecode_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => new BytecodeKernelSource(null!);
        _ = action.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("bytecode");
    }

    [Fact]
    public void BytecodeKernelSource_Constructor_WithEmptyBytecode_ShouldThrowArgumentException()
    {
        // Act & Assert
        var action = () => new BytecodeKernelSource([]);
        _ = action.Should().Throw<ArgumentException>().And.ParamName.Should().Be("bytecode");
    }

    #endregion

    #region ValidationResult Class Tests

    [Fact]
    public void ValidationResult_Success_ShouldReturnValidResult()
    {
        // Act
        var result = ValidationResult.Success();

        // Assert
        _ = result.IsValid.Should().BeTrue();
        _ = result.ErrorMessage.Should().BeNull();
        _ = result.Warnings.Should().NotBeNull().And.HaveCount(0);
    }

    [Fact]
    public void ValidationResult_SuccessWithWarnings_ShouldReturnValidResultWithWarnings()
    {
        // Arrange
        var warnings = new[] { "Warning 1", "Warning 2" };

        // Act
        var result = ValidationResult.SuccessWithWarnings(warnings);

        // Assert
        _ = result.IsValid.Should().BeTrue();
        _ = result.ErrorMessage.Should().BeNull();
        _ = result.Warnings.Should().BeEquivalentTo(warnings);
    }

    [Fact]
    public void ValidationResult_Failure_ShouldReturnInvalidResult()
    {
        // Arrange
        const string errorMessage = "Validation failed due to syntax error";

        // Act
        var result = ValidationResult.Failure(errorMessage);

        // Assert
        _ = result.IsValid.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be(errorMessage);
        _ = result.Warnings.Should().NotBeNull().And.HaveCount(0);
    }

    [Fact]
    public void ValidationResult_FailureWithWarnings_ShouldReturnInvalidResultWithWarnings()
    {
        // Arrange
        const string errorMessage = "Critical error";
        var warnings = new[] { "Minor issue 1", "Minor issue 2" };

        // Act
        var result = ValidationResult.FailureWithWarnings(errorMessage, warnings);

        // Assert
        _ = result.IsValid.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be(errorMessage);
        _ = result.Warnings.Should().BeEquivalentTo(warnings);
    }

    [Fact]
    public void ValidationResult_AddWarning_ShouldAddWarningToExistingResult()
    {
        // Arrange
        var result = ValidationResult.Success();
        const string warning = "New warning";

        // Act
        result.AddWarning(warning);

        // Assert
        _ = result.Warnings.Should().Contain(warning);
    }

    [Fact]
    public void ValidationResult_Equality_WithIdenticalResults_ShouldReturnTrue()
    {
        // Arrange
        var result1 = ValidationResult.SuccessWithWarnings("Warning 1", "Warning 2");
        var result2 = ValidationResult.SuccessWithWarnings("Warning 1", "Warning 2");

        // Act & Assert
        _ = result1.Equals(result2).Should().BeTrue();
        _ = (result1 == result2).Should().BeTrue();
        _ = (result1 != result2).Should().BeFalse();
    }

    [Fact]
    public void ValidationResult_GetHashCode_WithEqualResults_ShouldReturnSameHashCode()
    {
        // Arrange
        var result1 = ValidationResult.Failure("Error message");
        var result2 = ValidationResult.Failure("Error message");

        // Act & Assert
        _ = result1.GetHashCode().Should().Be(result2.GetHashCode());
    }

    #endregion

    #region CompilationMetadata Struct Tests

    [Fact]
    public void CompilationMetadata_Constructor_ShouldInitializeAllProperties()
    {
        // Arrange
        var compilationTime = TimeSpan.FromMilliseconds(500);
        const long codeSize = 2048;
        const int registersPerThread = 32;
        const long sharedMemoryPerBlock = 1024;
        var optimizationNotes = new[] { "Loop unrolled", "Memory coalesced" };

        // Act
        var metadata = new CompilationMetadata(compilationTime, codeSize, registersPerThread, sharedMemoryPerBlock, optimizationNotes);

        // Assert
        _ = metadata.CompilationTime.Should().Be(compilationTime);
        _ = metadata.CodeSize.Should().Be(codeSize);
        _ = metadata.RegistersPerThread.Should().Be(registersPerThread);
        _ = metadata.SharedMemoryPerBlock.Should().Be(sharedMemoryPerBlock);
        _ = metadata.OptimizationNotes.Should().BeEquivalentTo(optimizationNotes);
    }

    [Fact]
    public void CompilationMetadata_Equality_WithIdenticalValues_ShouldReturnTrue()
    {
        // Arrange
        var time = TimeSpan.FromSeconds(1);
        var metadata1 = new CompilationMetadata(time, 1024, 16, 512, ["note1"]);
        var metadata2 = new CompilationMetadata(time, 1024, 16, 512, ["note1"]);

        // Act & Assert
        _ = metadata1.Equals(metadata2).Should().BeTrue();
        _ = (metadata1 == metadata2).Should().BeTrue();
        _ = (metadata1 != metadata2).Should().BeFalse();
    }

    [Fact]
    public void CompilationMetadata_GetHashCode_WithEqualInstances_ShouldReturnSameHashCode()
    {
        // Arrange
        var time = TimeSpan.FromMilliseconds(250);
        var metadata1 = new CompilationMetadata(time, 512, 8, 256);
        var metadata2 = new CompilationMetadata(time, 512, 8, 256);

        // Act & Assert
        _ = metadata1.GetHashCode().Should().Be(metadata2.GetHashCode());
    }

    #endregion

    #region Edge Cases and Integration Tests

    [Fact]
    public void AllStructs_ShouldImplementIEquatable()
    {
        // Assert
        _ = typeof(CompiledKernel).Should().BeAssignableTo<IEquatable<CompiledKernel>>();
        _ = typeof(KernelConfiguration).Should().BeAssignableTo<IEquatable<KernelConfiguration>>();
        _ = typeof(CompilationMetadata).Should().BeAssignableTo<IEquatable<CompilationMetadata>>();
        _ = typeof(Dim3).Should().BeAssignableTo<IEquatable<Dim3>>();
        _ = typeof(KernelArguments).Should().BeAssignableTo<IEquatable<KernelArguments>>();
        _ = typeof(AcceleratorContext).Should().BeAssignableTo<IEquatable<AcceleratorContext>>();
    }

    [Fact]
    public void AllClassesWithInitSetters_ShouldHaveReadOnlyProperties()
    {
        // Test that init-only properties are properly implemented
        var kernelDefType = typeof(KernelDefinition);
        var acceleratorInfoType = typeof(AcceleratorInfo);

        // These properties should have init setters
        var kernelDefProperties = new[] { "Name", "Code", "EntryPoint", "Metadata" };
        var acceleratorInfoProperties = new[] { "Id", "Name", "DeviceType", "Vendor" };

        foreach (var propName in kernelDefProperties)
        {
            var prop = kernelDefType.GetProperty(propName);
            Assert.NotNull(prop);
            _ = prop.CanRead.Should().BeTrue();
            if (prop.CanWrite)
            {
                // Check if it's an init-only property by looking for init accessor
                var setMethod = prop.SetMethod;
                if (setMethod != null)
                {
                    // For init-only properties, the set method has special attributes
                    var isInitOnly = setMethod.ReturnParameter.GetRequiredCustomModifiers()
                        .Any(t => t.Name.Contains("IsExternalInit", StringComparison.Ordinal));
                    Assert.True(isInitOnly);
                }
            }
        }

        foreach (var propName in acceleratorInfoProperties)
        {
            var prop = acceleratorInfoType.GetProperty(propName);
            Assert.NotNull(prop);
            _ = prop.CanRead.Should().BeTrue();
            if (prop.CanWrite)
            {
                // Check if it's an init-only property by looking for init accessor
                var setMethod = prop.SetMethod;
                if (setMethod != null)
                {
                    // For init-only properties, the set method has special attributes
                    var isInitOnly = setMethod.ReturnParameter.GetRequiredCustomModifiers()
                        .Any(t => t.Name.Contains("IsExternalInit", StringComparison.Ordinal));
                    Assert.True(isInitOnly);
                }
            }
        }
    }

    [Fact]
    public void EnumValues_ShouldBeStableAcrossRuns()
    {
        // Test that enum values are consistent(important for serialization/compatibility)
        var acceleratorTypes = Enum.GetValues<AcceleratorType>();
        var memoryOptions = Enum.GetValues<MemoryOptions>();
        var kernelLanguages = Enum.GetValues<KernelLanguage>();

        // These should not change between runs
        _ = acceleratorTypes.Should().HaveCountGreaterThan(5);
        _ = memoryOptions.Should().HaveCountGreaterThan(3);
        _ = kernelLanguages.Should().HaveCountGreaterThan(5);

        // Test specific critical values
        _ = ((int)AcceleratorType.CPU).Should().Be(1);
        _ = ((int)AcceleratorType.CUDA).Should().Be(2);
        _ = ((int)MemoryOptions.None).Should().Be(0);
        _ = ((int)MemoryOptions.ReadOnly).Should().Be(1);
    }

    #endregion
}
