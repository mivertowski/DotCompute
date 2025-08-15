// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Xunit;
using FluentAssertions;

namespace DotCompute.Abstractions.Tests;

/// <summary>
/// Comprehensive unit tests for core structs, enums, and supporting classes in DotCompute.Abstractions.
/// </summary>
public class CoreStructsAndEnumsTests
{
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
        var compiledKernel = new CompiledKernel(id, handle, sharedMemorySize, configuration);

        // Assert
        compiledKernel.Id.Should().Be(id);
        compiledKernel.NativeHandle.Should().Be(handle);
        compiledKernel.SharedMemorySize.Should().Be(sharedMemorySize);
        compiledKernel.Configuration.Should().Be(configuration);
    }

    [Fact]
    public void CompiledKernel_Equality_WithIdenticalValues_ShouldReturnTrue()
    {
        // Arrange
        var id = Guid.NewGuid();
        var handle = new IntPtr(0x1000);
        const int sharedMemorySize = 512;
        var configuration = new KernelConfiguration(new Dim3(128), new Dim3(16, 16));

        var kernel1 = new CompiledKernel(id, handle, sharedMemorySize, configuration);
        var kernel2 = new CompiledKernel(id, handle, sharedMemorySize, configuration);

        // Act & Assert
        kernel1.Equals(kernel2).Should().BeTrue();
        (kernel1 == kernel2).Should().BeTrue();
        (kernel1 != kernel2).Should().BeFalse();
    }

    [Fact]
    public void CompiledKernel_Equality_WithDifferentIds_ShouldReturnFalse()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var handle = new IntPtr(0x1000);
        var config = new KernelConfiguration(new Dim3(128), new Dim3(16, 16));

        var kernel1 = new CompiledKernel(id1, handle, 512, config);
        var kernel2 = new CompiledKernel(id2, handle, 512, config);

        // Act & Assert
        kernel1.Equals(kernel2).Should().BeFalse();
        (kernel1 == kernel2).Should().BeFalse();
        (kernel1 != kernel2).Should().BeTrue();
    }

    [Fact]
    public void CompiledKernel_GetHashCode_WithEqualInstances_ShouldReturnSameHashCode()
    {
        // Arrange
        var id = Guid.NewGuid();
        var handle = new IntPtr(0x2000);
        var config = new KernelConfiguration(new Dim3(64), new Dim3(8, 8));

        var kernel1 = new CompiledKernel(id, handle, 256, config);
        var kernel2 = new CompiledKernel(id, handle, 256, config);

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
        config.GridDimensions.Should().Be(gridDim);
        config.BlockDimensions.Should().Be(blockDim);
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
        config1.Equals(config2).Should().BeTrue();
        (config1 == config2).Should().BeTrue();
        (config1 != config2).Should().BeFalse();
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
        config1.GetHashCode().Should().Be(config2.GetHashCode());
    }

    #endregion

    #region KernelDefinition Class Tests

    [Fact]
    public void KernelDefinition_Constructor_WithValidParameters_ShouldInitializeCorrectly()
    {
        // Arrange
        const string name = "TestKernel";
        var source = new TextKernelSource("__global__ void test() {}", "test", KernelLanguage.Cuda);
        var options = new CompilationOptions();

        // Act
        var definition = new KernelDefinition(name, source, options);

        // Assert
        definition.Name.Should().Be(name);
        definition.Code.Should().NotBeNullOrEmpty();
        definition.EntryPoint.Should().Be(source.EntryPoint);
        definition.Metadata.Should().ContainKey("Language");
        definition.Metadata.Should().ContainKey("Dependencies");
        definition.Metadata.Should().ContainKey("CompilationOptions");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void KernelDefinition_Constructor_WithInvalidName_ShouldThrowArgumentException(string invalidName)
    {
        // Arrange
        var source = new TextKernelSource("code", "test");
        var options = new CompilationOptions();

        // Act & Assert
        var action = () => new KernelDefinition(invalidName, source, options);
        action.Should().Throw<ArgumentException>().And.ParamName.Should().Be("name");
    }

    [Fact]
    public void KernelDefinition_Constructor_WithNullSource_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = new CompilationOptions();

        // Act & Assert
        var action = () => new KernelDefinition("TestKernel", null!, options);
        action.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("source");
    }

    [Fact]
    public void KernelDefinition_Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange
        var source = new TextKernelSource("code", "test");

        // Act & Assert
        var action = () => new KernelDefinition("TestKernel", source, null!);
        action.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("options");
    }

    #endregion

    #region CompilationOptions Class Tests

    [Fact]
    public void CompilationOptions_DefaultValues_ShouldBeSetCorrectly()
    {
        // Act
        var options = new CompilationOptions();

        // Assert
        options.OptimizationLevel.Should().Be(OptimizationLevel.Default);
        options.EnableDebugInfo.Should().BeFalse();
        options.AdditionalFlags.Should().BeNull();
        options.Defines.Should().BeNull();
        options.FastMath.Should().BeFalse();
        options.UnrollLoops.Should().BeFalse();
        options.PreferredBlockSize.Should().Be(new Dim3(256, 1, 1));
        options.SharedMemorySize.Should().Be(0);
        options.MaxThreadsPerBlock.Should().Be(1024);
        options.EnableMemoryCoalescing.Should().BeTrue();
        options.EnableOperatorFusion.Should().BeTrue();
        options.EnableParallelExecution.Should().BeTrue();
        options.GenerateDebugInfo.Should().BeFalse();
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
            FastMath = true,
            UnrollLoops = true,
            PreferredBlockSize = new Dim3(512, 1, 1),
            SharedMemorySize = 2048,
            MaxThreadsPerBlock = 512,
            EnableMemoryCoalescing = false,
            EnableOperatorFusion = false,
            EnableParallelExecution = false,
            GenerateDebugInfo = true
        };

        // Assert
        options.OptimizationLevel.Should().Be(OptimizationLevel.Maximum);
        options.EnableDebugInfo.Should().BeTrue();
        options.AdditionalFlags.Should().BeEquivalentTo(["-O3", "-ffast-math"]);
        options.Defines.Should().ContainKey("DEBUG");
        options.FastMath.Should().BeTrue();
        options.UnrollLoops.Should().BeTrue();
        options.PreferredBlockSize.Should().Be(new Dim3(256, 1, 1));
        options.SharedMemorySize.Should().Be(2048);
        options.MaxThreadsPerBlock.Should().Be(512);
        options.EnableMemoryCoalescing.Should().BeFalse();
        options.EnableOperatorFusion.Should().BeFalse();
        options.EnableParallelExecution.Should().BeFalse();
        options.GenerateDebugInfo.Should().BeTrue();
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
        parameter.Name.Should().Be(name);
        parameter.Type.Should().Be(type);
        parameter.Index.Should().Be(index);
        parameter.IsInput.Should().Be(isInput);
        parameter.IsOutput.Should().Be(isOutput);
        parameter.IsConstant.Should().Be(isConstant);
        parameter.IsReadOnly.Should().Be(true); // IsInput && !IsOutput
        parameter.MemorySpace.Should().Be(MemorySpace.Global); // Default
    }

    [Fact]
    public void KernelParameter_Constructor_WithNullName_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => new KernelParameter(null!, typeof(int), 0);
        action.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("name");
    }

    [Fact]
    public void KernelParameter_Constructor_WithNullType_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => new KernelParameter("test", null!, 0);
        action.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("type");
    }

    [Fact]
    public void KernelParameter_IsReadOnly_ShouldReturnCorrectValue()
    {
        // Arrange & Act
        var readOnlyParam = new KernelParameter("test1", typeof(int), 0, isInput: true, isOutput: false);
        var writeOnlyParam = new KernelParameter("test2", typeof(int), 1, isInput: false, isOutput: true);
        var readWriteParam = new KernelParameter("test3", typeof(int), 2, isInput: true, isOutput: true);

        // Assert
        readOnlyParam.IsReadOnly.Should().BeTrue();
        writeOnlyParam.IsReadOnly.Should().BeFalse();
        readWriteParam.IsReadOnly.Should().BeFalse();
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
        parameter.MemorySpace.Should().Be(MemorySpace.Shared);
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
        actualValues.Should().BeEquivalentTo(expectedValues);
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
        type.Should().BeDecoratedWith<FlagsAttribute>();

        // Test flag combinations
        var combined = MemoryOptions.ReadOnly | MemoryOptions.HostVisible | MemoryOptions.Cached;
        combined.Should().HaveFlag(MemoryOptions.ReadOnly);
        combined.Should().HaveFlag(MemoryOptions.HostVisible);
        combined.Should().HaveFlag(MemoryOptions.Cached);
        combined.Should().NotHaveFlag(MemoryOptions.WriteOnly);
    }

    [Fact]
    public void MapMode_ShouldBeFlagsEnum()
    {
        // Arrange
        var type = typeof(MapMode);

        // Act & Assert
        type.Should().BeDecoratedWith<FlagsAttribute>();

        // Test flag combinations
        var readWrite = MapMode.Read | MapMode.Write;
        Assert.Equal(MapMode.ReadWrite, readWrite);

        var complexMode = MapMode.ReadWrite | MapMode.Discard | MapMode.NoWait;
        complexMode.Should().HaveFlag(MapMode.Read);
        complexMode.Should().HaveFlag(MapMode.Write);
        complexMode.Should().HaveFlag(MapMode.Discard);
        complexMode.Should().HaveFlag(MapMode.NoWait);
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
        type.Should().BeDecoratedWith<FlagsAttribute>();

        // Test feature combinations
        var features = AcceleratorFeature.Float16 | AcceleratorFeature.DoublePrecision | AcceleratorFeature.TensorCores;
        features.Should().HaveFlag(AcceleratorFeature.Float16);
        features.Should().HaveFlag(AcceleratorFeature.DoublePrecision);
        features.Should().HaveFlag(AcceleratorFeature.TensorCores);
        features.Should().NotHaveFlag(AcceleratorFeature.UnifiedMemory);
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
        source.Code.Should().Be(code);
        source.Name.Should().Be(name);
        source.Language.Should().Be(language);
        source.EntryPoint.Should().Be(entryPoint);
        source.Dependencies.Should().BeEquivalentTo(dependencies);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TextKernelSource_Constructor_WithInvalidCode_ShouldThrowArgumentException(string invalidCode)
    {
        // Act & Assert
        var action = () => new TextKernelSource(invalidCode);
        action.Should().Throw<ArgumentException>().And.ParamName.Should().Be("code");
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
        source.Name.Should().Be(name);
        source.Language.Should().Be(language);
        source.EntryPoint.Should().Be(entryPoint);
        source.Code.Should().Be(Convert.ToBase64String(bytecode));
        source.Dependencies.Should().NotBeNull().And.HaveCount(0);
    }

    [Fact]
    public void BytecodeKernelSource_Constructor_WithNullBytecode_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => new BytecodeKernelSource(null!);
        action.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("bytecode");
    }

    [Fact]
    public void BytecodeKernelSource_Constructor_WithEmptyBytecode_ShouldThrowArgumentException()
    {
        // Act & Assert
        var action = () => new BytecodeKernelSource([]);
        action.Should().Throw<ArgumentException>().And.ParamName.Should().Be("bytecode");
    }

    #endregion

    #region ValidationResult Class Tests

    [Fact]
    public void ValidationResult_Success_ShouldReturnValidResult()
    {
        // Act
        var result = ValidationResult.Success();

        // Assert
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.Warnings.Should().NotBeNull().And.HaveCount(0);
    }

    [Fact]
    public void ValidationResult_SuccessWithWarnings_ShouldReturnValidResultWithWarnings()
    {
        // Arrange
        var warnings = new[] { "Warning 1", "Warning 2" };

        // Act
        var result = ValidationResult.SuccessWithWarnings(warnings);

        // Assert
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.Warnings.Should().BeEquivalentTo(warnings);
    }

    [Fact]
    public void ValidationResult_Failure_ShouldReturnInvalidResult()
    {
        // Arrange
        const string errorMessage = "Validation failed due to syntax error";

        // Act
        var result = ValidationResult.Failure(errorMessage);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be(errorMessage);
        result.Warnings.Should().NotBeNull().And.HaveCount(0);
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
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be(errorMessage);
        result.Warnings.Should().BeEquivalentTo(warnings);
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
        result.Warnings.Should().Contain(warning);
    }

    [Fact]
    public void ValidationResult_Equality_WithIdenticalResults_ShouldReturnTrue()
    {
        // Arrange
        var result1 = ValidationResult.SuccessWithWarnings("Warning 1", "Warning 2");
        var result2 = ValidationResult.SuccessWithWarnings("Warning 1", "Warning 2");

        // Act & Assert
        result1.Equals(result2).Should().BeTrue();
        (result1 == result2).Should().BeTrue();
        (result1 != result2).Should().BeFalse();
    }

    [Fact]
    public void ValidationResult_GetHashCode_WithEqualResults_ShouldReturnSameHashCode()
    {
        // Arrange
        var result1 = ValidationResult.Failure("Error message");
        var result2 = ValidationResult.Failure("Error message");

        // Act & Assert
        result1.GetHashCode().Should().Be(result2.GetHashCode());
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
        metadata.CompilationTime.Should().Be(compilationTime);
        metadata.CodeSize.Should().Be(codeSize);
        metadata.RegistersPerThread.Should().Be(registersPerThread);
        metadata.SharedMemoryPerBlock.Should().Be(sharedMemoryPerBlock);
        metadata.OptimizationNotes.Should().BeEquivalentTo(optimizationNotes);
    }

    [Fact]
    public void CompilationMetadata_Equality_WithIdenticalValues_ShouldReturnTrue()
    {
        // Arrange
        var time = TimeSpan.FromSeconds(1);
        var metadata1 = new CompilationMetadata(time, 1024, 16, 512, ["note1"]);
        var metadata2 = new CompilationMetadata(time, 1024, 16, 512, ["note1"]);

        // Act & Assert
        metadata1.Equals(metadata2).Should().BeTrue();
        (metadata1 == metadata2).Should().BeTrue();
        (metadata1 != metadata2).Should().BeFalse();
    }

    [Fact]
    public void CompilationMetadata_GetHashCode_WithEqualInstances_ShouldReturnSameHashCode()
    {
        // Arrange
        var time = TimeSpan.FromMilliseconds(250);
        var metadata1 = new CompilationMetadata(time, 512, 8, 256);
        var metadata2 = new CompilationMetadata(time, 512, 8, 256);

        // Act & Assert
        metadata1.GetHashCode().Should().Be(metadata2.GetHashCode());
    }

    #endregion

    #region Edge Cases and Integration Tests

    [Fact]
    public void AllStructs_ShouldImplementIEquatable()
    {
        // Assert
        typeof(CompiledKernel).Should().BeAssignableTo<IEquatable<CompiledKernel>>();
        typeof(KernelConfiguration).Should().BeAssignableTo<IEquatable<KernelConfiguration>>();
        typeof(CompilationMetadata).Should().BeAssignableTo<IEquatable<CompilationMetadata>>();
        typeof(Dim3).Should().BeAssignableTo<IEquatable<Dim3>>();
        typeof(KernelArguments).Should().BeAssignableTo<IEquatable<KernelArguments>>();
        typeof(AcceleratorContext).Should().BeAssignableTo<IEquatable<AcceleratorContext>>();
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
            prop.CanRead.Should().BeTrue();
            if (prop.CanWrite)
            {
                // Check if it's an init-only property by looking for init accessor
                var setMethod = prop.SetMethod;
                if (setMethod != null)
                {
                    // For init-only properties, the set method has special attributes
                    var isInitOnly = setMethod.ReturnParameter.GetRequiredCustomModifiers()
                        .Any(t => t.Name.Contains("IsExternalInit"));
                    Assert.True(isInitOnly);
                }
            }
        }

        foreach (var propName in acceleratorInfoProperties)
        {
            var prop = acceleratorInfoType.GetProperty(propName);
            Assert.NotNull(prop);
            prop.CanRead.Should().BeTrue();
            if (prop.CanWrite)
            {
                // Check if it's an init-only property by looking for init accessor
                var setMethod = prop.SetMethod;
                if (setMethod != null)
                {
                    // For init-only properties, the set method has special attributes
                    var isInitOnly = setMethod.ReturnParameter.GetRequiredCustomModifiers()
                        .Any(t => t.Name.Contains("IsExternalInit"));
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
        acceleratorTypes.Should().HaveCountGreaterThan(5);
        memoryOptions.Should().HaveCountGreaterThan(3);
        kernelLanguages.Should().HaveCountGreaterThan(5);

        // Test specific critical values
        ((int)AcceleratorType.CPU).Should().Be(1);
        ((int)AcceleratorType.CUDA).Should().Be(2);
        ((int)MemoryOptions.None).Should().Be(0);
        ((int)MemoryOptions.ReadOnly).Should().Be(1);
    }

    #endregion
}
