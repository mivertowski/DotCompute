// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Algorithms.LinearAlgebra;
using DotCompute.Algorithms.LinearAlgebra.Operations;

namespace DotCompute.Algorithms.Tests.LinearAlgebra.Operations;

/// <summary>
/// Comprehensive tests for MatrixTransforms operations.
/// </summary>
public sealed class MatrixTransformsTests
{
    private IAccelerator CreateMockAccelerator()
    {
        var mockAccelerator = Substitute.For<IAccelerator>();
        var mockInfo = Substitute.For<AcceleratorInfo>();
        mockInfo.Id.Returns("test_accelerator");
        mockInfo.Name.Returns("Test Accelerator");
        mockAccelerator.Info.Returns(mockInfo);
        return mockAccelerator;
    }

    #region Scaling Transform Tests

    [Fact]
    public void CreateScaling_UniformScale_CreatesCorrectMatrix()
    {
        // Act
        var result = MatrixTransforms.CreateScaling(2, 2, 2);

        // Assert
        result[0, 0].Should().Be(2);
        result[1, 1].Should().Be(2);
        result[2, 2].Should().Be(2);
        result[3, 3].Should().Be(1);
    }

    [Fact]
    public void CreateScaling_NonUniformScale_CreatesCorrectMatrix()
    {
        // Act
        var result = MatrixTransforms.CreateScaling(1, 2, 3);

        // Assert
        result[0, 0].Should().Be(1);
        result[1, 1].Should().Be(2);
        result[2, 2].Should().Be(3);
        result[3, 3].Should().Be(1);
    }

    [Fact]
    public void CreateScaling_2DScale_UsesDefaultZ()
    {
        // Act
        var result = MatrixTransforms.CreateScaling(3, 4);

        // Assert
        result[0, 0].Should().Be(3);
        result[1, 1].Should().Be(4);
        result[2, 2].Should().Be(1);
    }

    [Fact]
    public void CreateScaling_ZeroScale_CreatesZeroScaleMatrix()
    {
        // Act
        var result = MatrixTransforms.CreateScaling(0, 0, 0);

        // Assert
        result[0, 0].Should().Be(0);
        result[1, 1].Should().Be(0);
        result[2, 2].Should().Be(0);
    }

    [Fact]
    public void CreateScaling_NegativeScale_CreatesReflectionMatrix()
    {
        // Act
        var result = MatrixTransforms.CreateScaling(-1, 1, 1);

        // Assert
        result[0, 0].Should().Be(-1);
        result[1, 1].Should().Be(1);
        result[2, 2].Should().Be(1);
    }

    #endregion

    #region Translation Transform Tests

    [Fact]
    public void CreateTranslation_2DTranslation_CreatesCorrectMatrix()
    {
        // Act
        var result = MatrixTransforms.CreateTranslation(5, 10);

        // Assert
        result[0, 3].Should().Be(5);
        result[1, 3].Should().Be(10);
        result[2, 3].Should().Be(0);
        result[3, 3].Should().Be(1);
    }

    [Fact]
    public void CreateTranslation_3DTranslation_CreatesCorrectMatrix()
    {
        // Act
        var result = MatrixTransforms.CreateTranslation(1, 2, 3);

        // Assert
        result[0, 3].Should().Be(1);
        result[1, 3].Should().Be(2);
        result[2, 3].Should().Be(3);
        result[3, 3].Should().Be(1);
    }

    [Fact]
    public void CreateTranslation_ZeroTranslation_CreatesIdentityMatrix()
    {
        // Act
        var result = MatrixTransforms.CreateTranslation(0, 0, 0);

        // Assert
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                var expected = i == j ? 1.0f : 0.0f;
                result[i, j].Should().Be(expected);
            }
        }
    }

    [Fact]
    public void CreateTranslation_NegativeValues_CreatesCorrectMatrix()
    {
        // Act
        var result = MatrixTransforms.CreateTranslation(-5, -10, -15);

        // Assert
        result[0, 3].Should().Be(-5);
        result[1, 3].Should().Be(-10);
        result[2, 3].Should().Be(-15);
    }

    #endregion

    #region Rotation Transform Tests

    [Fact]
    public void CreateRotationZ_ZeroAngle_CreatesIdentityMatrix()
    {
        // Act
        var result = MatrixTransforms.CreateRotationZ(0);

        // Assert
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                var expected = i == j ? 1.0f : 0.0f;
                result[i, j].Should().BeApproximately(expected, 0.0001f);
            }
        }
    }

    [Fact]
    public void CreateRotationZ_90Degrees_CreatesCorrectMatrix()
    {
        // Arrange
        var angle = MathF.PI / 2; // 90 degrees

        // Act
        var result = MatrixTransforms.CreateRotationZ(angle);

        // Assert
        result[0, 0].Should().BeApproximately(0, 0.0001f);
        result[0, 1].Should().BeApproximately(-1, 0.0001f);
        result[1, 0].Should().BeApproximately(1, 0.0001f);
        result[1, 1].Should().BeApproximately(0, 0.0001f);
    }

    [Fact]
    public void CreateRotationZ_180Degrees_CreatesCorrectMatrix()
    {
        // Arrange
        var angle = MathF.PI; // 180 degrees

        // Act
        var result = MatrixTransforms.CreateRotationZ(angle);

        // Assert
        result[0, 0].Should().BeApproximately(-1, 0.0001f);
        result[1, 1].Should().BeApproximately(-1, 0.0001f);
    }

    [Fact]
    public void CreateRotationX_ZeroAngle_CreatesIdentityMatrix()
    {
        // Act
        var result = MatrixTransforms.CreateRotationX(0);

        // Assert
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                var expected = i == j ? 1.0f : 0.0f;
                result[i, j].Should().BeApproximately(expected, 0.0001f);
            }
        }
    }

    [Fact]
    public void CreateRotationX_90Degrees_CreatesCorrectMatrix()
    {
        // Arrange
        var angle = MathF.PI / 2;

        // Act
        var result = MatrixTransforms.CreateRotationX(angle);

        // Assert
        result[1, 1].Should().BeApproximately(0, 0.0001f);
        result[1, 2].Should().BeApproximately(-1, 0.0001f);
        result[2, 1].Should().BeApproximately(1, 0.0001f);
        result[2, 2].Should().BeApproximately(0, 0.0001f);
    }

    [Fact]
    public void CreateRotationY_ZeroAngle_CreatesIdentityMatrix()
    {
        // Act
        var result = MatrixTransforms.CreateRotationY(0);

        // Assert
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                var expected = i == j ? 1.0f : 0.0f;
                result[i, j].Should().BeApproximately(expected, 0.0001f);
            }
        }
    }

    [Fact]
    public void CreateRotationY_90Degrees_CreatesCorrectMatrix()
    {
        // Arrange
        var angle = MathF.PI / 2;

        // Act
        var result = MatrixTransforms.CreateRotationY(angle);

        // Assert
        result[0, 0].Should().BeApproximately(0, 0.0001f);
        result[0, 2].Should().BeApproximately(1, 0.0001f);
        result[2, 0].Should().BeApproximately(-1, 0.0001f);
        result[2, 2].Should().BeApproximately(0, 0.0001f);
    }

    [Fact]
    public void CreateRotationZ_NegativeAngle_RotatesClockwise()
    {
        // Arrange
        var angle = -MathF.PI / 2; // -90 degrees

        // Act
        var result = MatrixTransforms.CreateRotationZ(angle);

        // Assert
        result[0, 0].Should().BeApproximately(0, 0.0001f);
        result[0, 1].Should().BeApproximately(1, 0.0001f);
        result[1, 0].Should().BeApproximately(-1, 0.0001f);
    }

    #endregion

    #region Perspective and View Transform Tests

    [Fact]
    public void CreatePerspective_ValidParameters_CreatesCorrectMatrix()
    {
        // Act
        var result = MatrixTransforms.CreatePerspective(90, 16f / 9f, 0.1f, 1000f);

        // Assert
        result.Should().NotBeNull();
        result.Rows.Should().Be(4);
        result.Columns.Should().Be(4);
        result[3, 3].Should().Be(0); // Perspective projection
    }

    [Fact]
    public void CreatePerspective_45DegreeFOV_CreatesStandardProjection()
    {
        // Act
        var result = MatrixTransforms.CreatePerspective(45, 1, 1, 100);

        // Assert
        result[2, 2].Should().BeLessThan(0); // Z mapping
        result[2, 3].Should().BeLessThan(0); // Perspective divide
    }

    [Fact]
    public void CreateLookAt_StandardView_CreatesViewMatrix()
    {
        // Arrange
        var eye = new[] { 0f, 0f, 5f };
        var target = new[] { 0f, 0f, 0f };
        var up = new[] { 0f, 1f, 0f };

        // Act
        var result = MatrixTransforms.CreateLookAt(eye, target, up);

        // Assert
        result.Should().NotBeNull();
        result.Rows.Should().Be(4);
        result.Columns.Should().Be(4);
    }

    [Fact]
    public void CreateOrtho_ValidParameters_CreatesOrthographicMatrix()
    {
        // Act
        var result = MatrixTransforms.CreateOrtho(-10, 10, -10, 10, 0.1f, 100);

        // Assert
        result.Should().NotBeNull();
        result[3, 3].Should().Be(1); // Orthographic projection
    }

    #endregion

    #region Composite Transform Tests

    [Fact]
    public async Task ComposeTransforms_ScaleThenTranslate_CreatesCorrectMatrix()
    {
        // Arrange
        var scale = MatrixTransforms.CreateScaling(2, 2, 2);
        var translate = MatrixTransforms.CreateTranslation(5, 10, 15);
        var mockAccelerator = CreateMockAccelerator();

        // Act
        var result = await MatrixOperations.MultiplyAsync(translate, scale, mockAccelerator);

        // Assert
        result[0, 0].Should().Be(2); // Scale preserved
        result[0, 3].Should().Be(5); // Translation preserved
    }

    [Fact]
    public async Task ComposeTransforms_RotateThenScale_CreatesCorrectMatrix()
    {
        // Arrange
        var rotate = MatrixTransforms.CreateRotationZ(MathF.PI / 4);
        var scale = MatrixTransforms.CreateScaling(2, 2, 2);
        var mockAccelerator = CreateMockAccelerator();

        // Act
        var result = await MatrixOperations.MultiplyAsync(scale, rotate, mockAccelerator);

        // Assert
        result.Should().NotBeNull();
        // After scaling, rotation values should be scaled
        Math.Abs(result[0, 0]).Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task ComposeTransforms_AllThree_CreatesCorrectMatrix()
    {
        // Arrange
        var scale = MatrixTransforms.CreateScaling(2, 2, 2);
        var rotate = MatrixTransforms.CreateRotationZ(MathF.PI / 6);
        var translate = MatrixTransforms.CreateTranslation(10, 20, 30);
        var mockAccelerator = CreateMockAccelerator();

        // Act
        var temp = await MatrixOperations.MultiplyAsync(rotate, scale, mockAccelerator);
        var result = await MatrixOperations.MultiplyAsync(translate, temp, mockAccelerator);

        // Assert
        result.Should().NotBeNull();
        result[0, 3].Should().Be(10);
        result[1, 3].Should().Be(20);
        result[2, 3].Should().Be(30);
    }

    #endregion

    #region Edge Cases and Validation Tests

    [Fact]
    public void CreateScaling_ExtremeValues_HandlesCorrectly()
    {
        // Act
        var result = MatrixTransforms.CreateScaling(1e6f, 1e-6f, 1);

        // Assert
        result[0, 0].Should().Be(1e6f);
        result[1, 1].Should().Be(1e-6f);
    }

    [Fact]
    public void CreateRotationZ_FullCircle_ReturnsIdentity()
    {
        // Act
        var result = MatrixTransforms.CreateRotationZ(MathF.PI * 2);

        // Assert
        result[0, 0].Should().BeApproximately(1, 0.0001f);
        result[0, 1].Should().BeApproximately(0, 0.0001f);
        result[1, 0].Should().BeApproximately(0, 0.0001f);
        result[1, 1].Should().BeApproximately(1, 0.0001f);
    }

    [Fact]
    public void CreatePerspective_InvalidAspectRatio_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            MatrixTransforms.CreatePerspective(90, 0, 1, 100));
    }

    [Fact]
    public void CreatePerspective_NearGreaterThanFar_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            MatrixTransforms.CreatePerspective(90, 1, 100, 1));
    }

    // TODO: Re-enable when parameter types are fixed
    // [Fact]
    // public void CreateLookAt_EyeEqualsTarget_ThrowsArgumentException()
    // {
    //     // Arrange
    //     var pos = new[] { 0f, 0f, 0f };
    //     var up = new[] { 0f, 1f, 0f };
    //
    //     // Act & Assert
    //     Assert.Throws<ArgumentException>(() =>
    //         MatrixTransforms.CreateLookAt(pos, pos, up));
    // }

    #endregion

    // private static IAccelerator CreateMockAccelerator()
    // {
    //     var mock = Substitute.For<IAccelerator>();
    //     mock.Info.Returns(new AcceleratorInfo
    //     {
    //         DeviceType = "CPU",
    //         Name = "Mock CPU",
    //         Id = "mock-cpu-0",
    //         MaxComputeUnits = 8
    //     });
    //     return mock;
    // }
}
