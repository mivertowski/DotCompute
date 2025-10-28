// TODO: Implement SimdMathOperations.Sqrt method
//
// // Copyright (c) 2025 Michael Ivertowski
// // Licensed under the MIT License. See LICENSE file in the project root for license information.
// 
// using DotCompute.Algorithms.Optimized.Simd;
// 
// namespace DotCompute.Algorithms.Tests.Optimized.Simd;
// 
// /// <summary>
// /// Comprehensive tests for SimdMathOperations.
// /// </summary>
// public sealed class SimdMathOperationsTests
// {
//     #region Addition Tests
// 
//     [Fact]
//     public void Add_SimpleArrays_ReturnsSum()
//     {
//         // Arrange
//         var left = new float[] { 1, 2, 3, 4 };
//         var right = new float[] { 5, 6, 7, 8 };
//         var result = new float[4];
// 
//         // Act
//         SimdMathOperations.Add(left, right, result);
// 
//         // Assert
//         result[0].Should().Be(6);
//         result[1].Should().Be(8);
//         result[2].Should().Be(10);
//         result[3].Should().Be(12);
//     }
// 
//     [Fact]
//     public void Add_EmptyArrays_DoesNothing()
//     {
//         // Arrange
//         var left = Array.Empty<float>();
//         var right = Array.Empty<float>();
//         var result = Array.Empty<float>();
// 
//         // Act & Assert
//         SimdMathOperations.Add(left, right, result);
//         // Should complete without error
//     }
// 
//     [Fact]
//     public void Add_LargeArrays_HandlesCorrectly()
//     {
//         // Arrange
//         var size = 10000;
//         var left = Enumerable.Repeat(1.0f, size).ToArray();
//         var right = Enumerable.Repeat(2.0f, size).ToArray();
//         var result = new float[size];
// 
//         // Act
//         SimdMathOperations.Add(left, right, result);
// 
//         // Assert
//         result.All(x => Math.Abs(x - 3.0f) < 0.0001f).Should().BeTrue();
//     }
// 
//     [Fact]
//     public void Add_NegativeValues_HandlesCorrectly()
//     {
//         // Arrange
//         var left = new float[] { -1, -2, -3 };
//         var right = new float[] { 4, 5, 6 };
//         var result = new float[3];
// 
//         // Act
//         SimdMathOperations.Add(left, right, result);
// 
//         // Assert
//         result[0].Should().Be(3);
//         result[1].Should().Be(3);
//         result[2].Should().Be(3);
//     }
// 
//     [Fact]
//     public void Add_DifferentLengths_ThrowsArgumentException()
//     {
//         // Arrange
//         var left = new float[] { 1, 2, 3 };
//         var right = new float[] { 1, 2 };
//         var result = new float[3];
// 
//         // Act & Assert
//         Assert.Throws<ArgumentException>(() =>
//             SimdMathOperations.Add(left, right, result));
//     }
// 
//     [Fact]
//     public void Add_VectorizedSize_UsesOptimizedPath()
//     {
//         // Arrange - Size that triggers vectorization
//         var size = 512;
//         var left = Enumerable.Range(0, size).Select(i => (float)i).ToArray();
//         var right = Enumerable.Repeat(1.0f, size).ToArray();
//         var result = new float[size];
// 
//         // Act
//         SimdMathOperations.Add(left, right, result);
// 
//         // Assert
//         for (int i = 0; i < size; i++)
//         {
//             result[i].Should().BeApproximately(i + 1, 0.0001f);
//         }
//     }
// 
//     #endregion
// 
//     #region Multiplication Tests
// 
//     [Fact]
//     public void Multiply_SimpleArrays_ReturnsProduct()
//     {
//         // Arrange
//         var left = new float[] { 2, 3, 4, 5 };
//         var right = new float[] { 3, 4, 5, 6 };
//         var result = new float[4];
// 
//         // Act
//         SimdMathOperations.Multiply(left, right, result);
// 
//         // Assert
//         result[0].Should().Be(6);
//         result[1].Should().Be(12);
//         result[2].Should().Be(20);
//         result[3].Should().Be(30);
//     }
// 
//     [Fact]
//     public void Multiply_ZeroValues_ReturnsZero()
//     {
//         // Arrange
//         var left = new float[] { 1, 2, 3 };
//         var right = new float[] { 0, 0, 0 };
//         var result = new float[3];
// 
//         // Act
//         SimdMathOperations.Multiply(left, right, result);
// 
//         // Assert
//         result.All(x => x == 0).Should().BeTrue();
//     }
// 
//     [Fact]
//     public void Multiply_NegativeValues_HandlesCorrectly()
//     {
//         // Arrange
//         var left = new float[] { -2, 3, -4 };
//         var right = new float[] { 3, -4, 5 };
//         var result = new float[3];
// 
//         // Act
//         SimdMathOperations.Multiply(left, right, result);
// 
//         // Assert
//         result[0].Should().Be(-6);
//         result[1].Should().Be(-12);
//         result[2].Should().Be(-20);
//     }
// 
//     [Fact]
//     public void Multiply_LargeArrays_HandlesCorrectly()
//     {
//         // Arrange
//         var size = 10000;
//         var left = Enumerable.Repeat(2.0f, size).ToArray();
//         var right = Enumerable.Repeat(3.0f, size).ToArray();
//         var result = new float[size];
// 
//         // Act
//         SimdMathOperations.Multiply(left, right, result);
// 
//         // Assert
//         result.All(x => Math.Abs(x - 6.0f) < 0.0001f).Should().BeTrue();
//     }
// 
//     #endregion
// 
//     #region Subtraction Tests
// 
//     [Fact]
//     public void Subtract_SimpleArrays_ReturnsDifference()
//     {
//         // Arrange
//         var left = new float[] { 10, 20, 30, 40 };
//         var right = new float[] { 5, 6, 7, 8 };
//         var result = new float[4];
// 
//         // Act
//         SimdMathOperations.Subtract(left, right, result);
// 
//         // Assert
//         result[0].Should().Be(5);
//         result[1].Should().Be(14);
//         result[2].Should().Be(23);
//         result[3].Should().Be(32);
//     }
// 
//     [Fact]
//     public void Subtract_SameArrays_ReturnsZero()
//     {
//         // Arrange
//         var values = new float[] { 1, 2, 3, 4 };
//         var result = new float[4];
// 
//         // Act
//         SimdMathOperations.Subtract(values, values, result);
// 
//         // Assert
//         result.All(x => Math.Abs(x) < 0.0001f).Should().BeTrue();
//     }
// 
//     [Fact]
//     public void Subtract_NegativeResults_HandlesCorrectly()
//     {
//         // Arrange
//         var left = new float[] { 1, 2, 3 };
//         var right = new float[] { 5, 6, 7 };
//         var result = new float[3];
// 
//         // Act
//         SimdMathOperations.Subtract(left, right, result);
// 
//         // Assert
//         result[0].Should().Be(-4);
//         result[1].Should().Be(-4);
//         result[2].Should().Be(-4);
//     }
// 
//     #endregion
// 
//     #region Division Tests
// 
//     [Fact]
//     public void Divide_SimpleArrays_ReturnsQuotient()
//     {
//         // Arrange
//         var left = new float[] { 10, 20, 30, 40 };
//         var right = new float[] { 2, 4, 5, 8 };
//         var result = new float[4];
// 
//         // Act
//         SimdMathOperations.Divide(left, right, result);
// 
//         // Assert
//         result[0].Should().Be(5);
//         result[1].Should().Be(5);
//         result[2].Should().Be(6);
//         result[3].Should().Be(5);
//     }
// 
//     [Fact]
//     public void Divide_ByOne_ReturnsSameValue()
//     {
//         // Arrange
//         var left = new float[] { 7, 14, 21 };
//         var right = new float[] { 1, 1, 1 };
//         var result = new float[3];
// 
//         // Act
//         SimdMathOperations.Divide(left, right, result);
// 
//         // Assert
//         result[0].Should().Be(7);
//         result[1].Should().Be(14);
//         result[2].Should().Be(21);
//     }
// 
//     [Fact]
//     public void Divide_NegativeValues_HandlesCorrectly()
//     {
//         // Arrange
//         var left = new float[] { -10, 20, -30 };
//         var right = new float[] { 2, -4, 5 };
//         var result = new float[3];
// 
//         // Act
//         SimdMathOperations.Divide(left, right, result);
// 
//         // Assert
//         result[0].Should().Be(-5);
//         result[1].Should().Be(-5);
//         result[2].Should().Be(-6);
//     }
// 
//     #endregion
// 
//     #region FMA (Fused Multiply-Add) Tests
// 
//     [Fact]
//     public void FusedMultiplyAdd_SimpleArrays_ReturnsCorrectResult()
//     {
//         // Arrange
//         var a = new float[] { 2, 3, 4 };
//         var b = new float[] { 5, 6, 7 };
//         var c = new float[] { 1, 2, 3 };
//         var result = new float[3];
//         // Expected: a * b + c = [11, 20, 31]
// 
//         // Act
//         SimdMathOperations.FusedMultiplyAdd(a, b, c, result);
// 
//         // Assert
//         result[0].Should().Be(11);
//         result[1].Should().Be(20);
//         result[2].Should().Be(31);
//     }
// 
//     [Fact]
//     public void FusedMultiplyAdd_ZeroMultiplier_ReturnsAddend()
//     {
//         // Arrange
//         var a = new float[] { 0, 0, 0 };
//         var b = new float[] { 5, 6, 7 };
//         var c = new float[] { 1, 2, 3 };
//         var result = new float[3];
// 
//         // Act
//         SimdMathOperations.FusedMultiplyAdd(a, b, c, result);
// 
//         // Assert
//         result[0].Should().Be(1);
//         result[1].Should().Be(2);
//         result[2].Should().Be(3);
//     }
// 
//     [Fact]
//     public void FusedMultiplyAdd_ZeroAddend_ReturnsProduct()
//     {
//         // Arrange
//         var a = new float[] { 2, 3, 4 };
//         var b = new float[] { 5, 6, 7 };
//         var c = new float[] { 0, 0, 0 };
//         var result = new float[3];
// 
//         // Act
//         SimdMathOperations.FusedMultiplyAdd(a, b, c, result);
// 
//         // Assert
//         result[0].Should().Be(10);
//         result[1].Should().Be(18);
//         result[2].Should().Be(28);
//     }
// 
//     [Fact]
//     public void FusedMultiplyAdd_LargeArrays_HandlesCorrectly()
//     {
//         // Arrange
//         var size = 1000;
//         var a = Enumerable.Repeat(2.0f, size).ToArray();
//         var b = Enumerable.Repeat(3.0f, size).ToArray();
//         var c = Enumerable.Repeat(1.0f, size).ToArray();
//         var result = new float[size];
//         var expected = 7.0f; // 2 * 3 + 1
// 
//         // Act
//         SimdMathOperations.FusedMultiplyAdd(a, b, c, result);
// 
//         // Assert
//         result.All(x => Math.Abs(x - expected) < 0.0001f).Should().BeTrue();
//     }
// 
//     #endregion
// 
//     #region Absolute Value Tests
// 
//     [Fact]
//     public void Abs_MixedValues_ReturnsAbsoluteValues()
//     {
//         // Arrange
//         var input = new float[] { -1, 2, -3, 4, -5 };
//         var result = new float[5];
// 
//         // Act
// //         SimdMathOperations.Abs(input, result);
// // 
// //         // Assert
// //         result[0].Should().Be(1);
// //         result[1].Should().Be(2);
// //         result[2].Should().Be(3);
// //         result[3].Should().Be(4);
// //         result[4].Should().Be(5);
// //     }
// // 
// //     [Fact]
// //     public void Abs_PositiveValues_ReturnsSameValues()
// //     {
// //         // Arrange
// //         var input = new float[] { 1, 2, 3 };
// //         var result = new float[3];
// // 
// //         // Act
// //         SimdMathOperations.Abs(input, result);
// // 
// //         // Assert
// //         result[0].Should().Be(1);
// //         result[1].Should().Be(2);
// //         result[2].Should().Be(3);
// //     }
// // 
// //     [Fact]
// //     public void Abs_Zero_ReturnsZero()
// //     {
// //         // Arrange
// //         var input = new float[] { 0, -0, 0 };
// //         var result = new float[3];
// // 
// //         // Act
// //         SimdMathOperations.Abs(input, result);
// // 
// //         // Assert
// //         result.All(x => x == 0).Should().BeTrue();
// //     }
// // 
// //     #endregion
// // 
// //     #region Square Root Tests
// // 
// //     [Fact]
// //     public void Sqrt_SimpleValues_ReturnsSquareRoots()
// //     {
// //         // Arrange
// //         var input = new float[] { 4, 9, 16, 25 };
// //         var result = new float[4];
// // 
// //         // Act
// //         SimdMathOperations.Sqrt(input, result);
// // 
// //         // Assert
// //         result[0].Should().BeApproximately(2, 0.0001f);
// //         result[1].Should().BeApproximately(3, 0.0001f);
// //         result[2].Should().BeApproximately(4, 0.0001f);
// //         result[3].Should().BeApproximately(5, 0.0001f);
// //     }
// // 
// //     [Fact]
// //     public void Sqrt_Zero_ReturnsZero()
// //     {
// //         // Arrange
// //         var input = new float[] { 0, 0, 0 };
// //         var result = new float[3];
// // 
// //         // Act
// //         SimdMathOperations.Sqrt(input, result);
// // 
// //         // Assert
// //         result.All(x => x == 0).Should().BeTrue();
// //     }
// // 
// //     [Fact]
// //     public void Sqrt_One_ReturnsOne()
// //     {
// //         // Arrange
// //         var input = new float[] { 1, 1, 1 };
// //         var result = new float[3];
// // 
// //         // Act
// //         SimdMathOperations.Sqrt(input, result);
// // 
// //         // Assert
// //         result.All(x => Math.Abs(x - 1) < 0.0001f).Should().BeTrue();
// //     }
// // 
// //     #endregion
// // 
// //     #region Cross-Platform SIMD Tests
// // 
// //     [Fact]
// //     public void Add_Avx512Size_HandlesCorrectly()
// //     {
// //         // Arrange - Size aligned to AVX-512 (16 floats)
//         var size = 512;
//         var left = Enumerable.Repeat(1.0f, size).ToArray();
//         var right = Enumerable.Repeat(2.0f, size).ToArray();
//         var result = new float[size];
// 
//         // Act
//         SimdMathOperations.Add(left, right, result);
// 
//         // Assert
//         result.All(x => Math.Abs(x - 3.0f) < 0.0001f).Should().BeTrue();
//     }
// 
//     [Fact]
//     public void Multiply_Avx2Size_HandlesCorrectly()
//     {
//         // Arrange - Size aligned to AVX2 (8 floats)
//         var size = 256;
//         var left = Enumerable.Repeat(2.0f, size).ToArray();
//         var right = Enumerable.Repeat(4.0f, size).ToArray();
//         var result = new float[size];
// 
//         // Act
//         SimdMathOperations.Multiply(left, right, result);
// 
//         // Assert
//         result.All(x => Math.Abs(x - 8.0f) < 0.0001f).Should().BeTrue();
//     }
// 
//     [Fact]
//     public void FusedMultiplyAdd_FallbackPath_ProducesCorrectResult()
//     {
//         // Arrange - Small size that may use fallback
//         var a = new float[] { 1, 2 };
//         var b = new float[] { 3, 4 };
//         var c = new float[] { 5, 6 };
//         var result = new float[2];
// 
//         // Act
//         SimdMathOperations.FusedMultiplyAdd(a, b, c, result);
// 
//         // Assert
//         result[0].Should().Be(8); // 1*3 + 5
//         result[1].Should().Be(14); // 2*4 + 6
//     }
// 
//     #endregion
// 
//     #region Edge Cases and Numerical Stability Tests
// 
//     [Fact]
//     public void Add_VerySmallValues_MaintainsPrecision()
//     {
//         // Arrange
//         var left = new float[] { 1e-10f, 1e-10f };
//         var right = new float[] { 1e-10f, 1e-10f };
//         var result = new float[2];
// 
//         // Act
//         SimdMathOperations.Add(left, right, result);
// 
//         // Assert
//         result[0].Should().BeApproximately(2e-10f, 1e-11f);
//     }
// 
//     [Fact]
//     public void Multiply_VeryLargeValues_HandlesOverflow()
//     {
//         // Arrange
//         var left = new float[] { 1e20f, 1e20f };
//         var right = new float[] { 10, 10 };
//         var result = new float[2];
// 
//         // Act
//         SimdMathOperations.Multiply(left, right, result);
// 
//         // Assert
//         result[0].Should().BeGreaterThan(1e20f);
//     }
// 
//     [Fact]
//     public void Divide_ByVerySmallNumber_HandlesCorrectly()
//     {
//         // Arrange
//         var left = new float[] { 1, 1 };
//         var right = new float[] { 1e-6f, 1e-6f };
//         var result = new float[2];
// 
//         // Act
//         SimdMathOperations.Divide(left, right, result);
// 
//         // Assert
//         result[0].Should().BeApproximately(1e6f, 100);
//     }
// 
//     [Fact]
//     public void FusedMultiplyAdd_MixedMagnitudes_HandlesCorrectly()
//     {
//         // Arrange
//         var a = new float[] { 1e10f, 1e-10f };
//         var b = new float[] { 1, 1 };
//         var c = new float[] { 1, 1 };
//         var result = new float[2];
// 
//         // Act
//         SimdMathOperations.FusedMultiplyAdd(a, b, c, result);
// 
//         // Assert
//         result[0].Should().BeGreaterThan(1e9f);
//         result[1].Should().BeApproximately(1, 0.1f);
//     }
// 
//     #endregion
// }
