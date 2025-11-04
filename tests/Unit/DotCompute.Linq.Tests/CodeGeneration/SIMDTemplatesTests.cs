using System.Numerics;
using DotCompute.Linq.CodeGeneration;
using FluentAssertions;
using Xunit;

namespace DotCompute.Linq.Tests.CodeGeneration;

/// <summary>
/// Comprehensive unit tests for SIMDTemplates code generation.
/// Validates all 17 template methods with proper vectorization hints and edge case handling.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "CodeGeneration")]
public class SIMDTemplatesTests
{
    #region Map Operations Tests

    [Fact]
    public void VectorSelect_WithSimpleTransform_GeneratesCorrectTemplate()
    {
        // Arrange
        const string transformExpression = "x * 2.0f";

        // Act
        var result = SIMDTemplates.VectorSelect<float, float>(transformExpression);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Vector<Single>");
        result.Should().Contain("vectorSize");
        result.Should().Contain("i <= length - vectorSize");
        result.Should().Contain("CopyTo");
        result.Should().Contain("vec * 2.0f"); // Transform applied to vector
        result.Should().Contain("{INPUT}[i] * 2.0f"); // Scalar remainder transform
    }

    [Fact]
    public void VectorSelect_WithDifferentTypes_SubstitutesCorrectTypeNames()
    {
        // Arrange
        const string transformExpression = "(TResult)x";

        // Act
        var result = SIMDTemplates.VectorSelect<int, float>(transformExpression);

        // Assert
        result.Should().Contain("Vector<Int32>"); // Input type
        result.Should().Contain("(Single)"); // Output type cast
    }

    [Fact]
    public void VectorSelect_WithComplexExpression_PreservesExpression()
    {
        // Arrange
        const string transformExpression = "x * x + 2.0f * x + 1.0f";

        // Act
        var result = SIMDTemplates.VectorSelect<float, float>(transformExpression);

        // Assert
        result.Should().Contain("vec * vec + 2.0f * vec + 1.0f");
        result.Should().Contain("{INPUT}[i] * {INPUT}[i] + 2.0f * {INPUT}[i] + 1.0f");
    }

    [Fact]
    public void VectorSelect_WithUnsupportedType_ThrowsArgumentException()
    {
        // Act
        var act = () => SIMDTemplates.VectorSelect<decimal, decimal>("x * 2");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*decimal*not supported by Vector<T>*");
    }

    #endregion

    #region Filter Operations Tests

    [Fact]
    public void VectorWhere_WithSimplePredicate_GeneratesCorrectTemplate()
    {
        // Arrange
        const string predicateExpression = "x > 0";

        // Act
        var result = SIMDTemplates.VectorWhere<int>(predicateExpression);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Vector<Int32>");
        result.Should().Contain("List<Int32>");
        result.Should().Contain("EvaluatePredicate");
        result.Should().Contain("GetMaskElement");
        result.Should().Contain("vec > 0"); // Vectorized predicate
        result.Should().Contain("{INPUT}[i] > 0"); // Scalar predicate
        result.Should().Contain("result.Add");
    }

    [Fact]
    public void VectorWhere_WithComplexPredicate_PreservesLogic()
    {
        // Arrange
        const string predicateExpression = "x >= 10 && x <= 100";

        // Act
        var result = SIMDTemplates.VectorWhere<int>(predicateExpression);

        // Assert
        result.Should().Contain("vec >= 10 && vec <= 100");
        result.Should().Contain("{INPUT}[i] >= 10 && {INPUT}[i] <= 100");
    }

    [Fact]
    public void VectorWhere_IncludesCapacityHint()
    {
        // Act
        var result = SIMDTemplates.VectorWhere<float>("x > 0.5f");

        // Assert
        result.Should().Contain("capacity: {LENGTH}");
    }

    #endregion

    #region Aggregation Operations Tests

    [Fact]
    public void VectorSum_GeneratesCorrectTemplate()
    {
        // Act
        var result = SIMDTemplates.VectorSum<float>();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Vector<Single>.Zero");
        result.Should().Contain("accumulator += vec");
        result.Should().Contain("Horizontal sum across vector lanes");
        result.Should().Contain("sum += accumulator[j]");
        result.Should().Contain("sum += {INPUT}[i]"); // Scalar remainder
    }

    [Fact]
    public void VectorSum_ForDifferentTypes_UsesCorrectTypeName()
    {
        // Act
        var resultFloat = SIMDTemplates.VectorSum<float>();
        var resultDouble = SIMDTemplates.VectorSum<double>();
        var resultInt = SIMDTemplates.VectorSum<int>();

        // Assert
        resultFloat.Should().Contain("Single");
        resultDouble.Should().Contain("Double");
        resultInt.Should().Contain("Int32");
    }

    [Fact]
    public void VectorAggregate_WithCustomAccumulator_GeneratesTemplate()
    {
        // Arrange
        const string seed = "0";
        const string accumulatorFunc = "acc + x * x";

        // Act
        var result = SIMDTemplates.VectorAggregate<int, int>(seed, accumulatorFunc);

        // Assert
        result.Should().Contain("var accumulator = 0");
        result.Should().Contain("accumulator = acc + x * x");
        result.Should().Contain("return accumulator");
    }

    [Fact]
    public void VectorMin_GeneratesCorrectTemplate()
    {
        // Act
        var result = SIMDTemplates.VectorMin<float>();

        // Assert
        result.Should().Contain("Single.MaxValue");
        result.Should().Contain("Vector.Min(minVec, vec)");
        result.Should().Contain("Horizontal minimum");
        result.Should().Contain("throw new InvalidOperationException(\"Sequence contains no elements\")");
        result.Should().Contain("minVec[j].CompareTo(min) < 0");
    }

    [Fact]
    public void VectorMax_GeneratesCorrectTemplate()
    {
        // Act
        var result = SIMDTemplates.VectorMax<int>();

        // Assert
        result.Should().Contain("Int32.MinValue");
        result.Should().Contain("Vector.Max(maxVec, vec)");
        result.Should().Contain("Horizontal maximum");
        result.Should().Contain("maxVec[j].CompareTo(max) > 0");
    }

    #endregion

    #region Fused Operations Tests

    [Fact]
    public void FusedSelectWhere_GeneratesCorrectTemplate()
    {
        // Arrange
        const string transform = "x * 2";
        const string predicate = "y > 10";

        // Act
        var result = SIMDTemplates.FusedSelectWhere<int, int>(transform, predicate);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Fused vectorized loop");
        result.Should().Contain("Transform: vec * 2");
        result.Should().Contain("Filter: transformed > 10");
        result.Should().Contain("ApplyTransform");
        result.Should().Contain("EvaluatePredicate");
        result.Should().Contain("var transformed = (Int32)({INPUT}[i] * 2)");
    }

    [Fact]
    public void FusedSelectWhere_UsesSmallerVectorSize()
    {
        // Act
        var result = SIMDTemplates.FusedSelectWhere<byte, long>("x", "y > 0");

        // Assert
        result.Should().Contain("Math.Min(Vector<Byte>.Count, Vector<Int64>.Count)");
    }

    [Fact]
    public void FusedWhereSelect_GeneratesCorrectTemplate()
    {
        // Arrange
        const string predicate = "x > 0";
        const string transform = "x * 2";

        // Act
        var result = SIMDTemplates.FusedWhereSelect<int, int>(predicate, transform);

        // Assert
        result.Should().Contain("Fused vectorized loop: filter then transform");
        result.Should().Contain("Filter: vec > 0");
        result.Should().Contain("Transform only filtered elements");
        result.Should().Contain("capacity: {LENGTH} / 2"); // Optimistic capacity
    }

    [Fact]
    public void FusedWhereSelect_TransformsOnlyFilteredElements()
    {
        // Act
        var result = SIMDTemplates.FusedWhereSelect<float, double>("x > 0.5f", "x * 2.0");

        // Assert
        result.Should().Contain("if (GetMaskElement(mask, j))");
        result.Should().Contain("var x = vec[j]");
        result.Should().Contain("result.Add((Double)(x * 2.0))");
    }

    #endregion

    #region Binary Operations Tests

    [Fact]
    public void VectorBinaryOp_WithAddition_GeneratesCorrectTemplate()
    {
        // Act
        var result = SIMDTemplates.VectorBinaryOp<float>("+");

        // Assert
        result.Should().Contain("if ({INPUT1}.Length != {INPUT2}.Length)");
        result.Should().Contain("throw new ArgumentException(\"Arrays must have equal length\")");
        result.Should().Contain("var resultVec = vec1 + vec2");
        result.Should().Contain("result[i] = {INPUT1}[i] + {INPUT2}[i]");
    }

    [Fact]
    public void VectorBinaryOp_WithMultiplication_PreservesOperator()
    {
        // Act
        var result = SIMDTemplates.VectorBinaryOp<double>("*");

        // Assert
        result.Should().Contain("var resultVec = vec1 * vec2");
        result.Should().Contain("result[i] = {INPUT1}[i] * {INPUT2}[i]");
    }

    [Theory]
    [InlineData("+")]
    [InlineData("-")]
    [InlineData("*")]
    [InlineData("/")]
    public void VectorBinaryOp_WithVariousOperators_WorksCorrectly(string operatorSymbol)
    {
        // Act
        var result = SIMDTemplates.VectorBinaryOp<float>(operatorSymbol);

        // Assert
        result.Should().Contain($"vec1 {operatorSymbol} vec2");
        result.Should().Contain($"{{INPUT1}}[i] {operatorSymbol} {{INPUT2}}[i]");
    }

    #endregion

    #region Advanced Operations Tests

    [Fact]
    public void VectorScan_GeneratesCorrectPrefixSumTemplate()
    {
        // Act
        var result = SIMDTemplates.VectorScan<int>();

        // Assert
        result.Should().Contain("Phase 1: Compute prefix sums");
        result.Should().Contain("runningSum += {INPUT}[i + j]");
        result.Should().Contain("result[i + j] = runningSum");
        result.Should().Contain("if ({LENGTH} == 0) return result");
    }

    [Fact]
    public void VectorGroupBy_GeneratesCorrectTemplate()
    {
        // Arrange
        const string keySelector = "x % 10";

        // Act
        var result = SIMDTemplates.VectorGroupBy<int, int>(keySelector);

        // Assert
        result.Should().Contain("Dictionary<Int32, List<Int32>>");
        result.Should().Contain("var key = x % 10");
        result.Should().Contain("if (!groups.TryGetValue(key, out var group))");
        result.Should().Contain("group.Add(x)");
    }

    [Fact]
    public void VectorJoin_GeneratesCorrectHashJoinTemplate()
    {
        // Arrange
        const string outerKeySelector = "x.Id";
        const string innerKeySelector = "y.ForeignId";

        // Act
        var result = SIMDTemplates.VectorJoin<int, long, int>(outerKeySelector, innerKeySelector);

        // Assert
        result.Should().Contain("Build hash table from inner sequence");
        result.Should().Contain("Dictionary<Int32, List<Int64>>");
        result.Should().Contain("var key = x.Id");
        result.Should().Contain("var key = y.ForeignId");
        result.Should().Contain("Probe with outer sequence");
        result.Should().Contain("result.Add((x, match))");
    }

    #endregion

    #region CPU-Specific Optimizations Tests

    [Fact]
    public void AVX2VectorSum_GeneratesCorrectTemplate()
    {
        // Act
        var result = SIMDTemplates.AVX2VectorSum<float>();

        // Assert
        result.Should().Contain("AVX2 optimization: 256-bit vectors");
        result.Should().Contain("System.Runtime.Intrinsics.X86.Avx2.IsSupported");
        result.Should().Contain("throw new NotSupportedException(\"AVX2 not supported on this CPU\")");
        result.Should().Contain("8x float or 4x double");
    }

    [Fact]
    public void AVX512VectorSum_GeneratesCorrectTemplate()
    {
        // Act
        var result = SIMDTemplates.AVX512VectorSum<double>();

        // Assert
        result.Should().Contain("AVX-512 optimization: 512-bit vectors");
        result.Should().Contain("System.Runtime.Intrinsics.X86.Avx512F.IsSupported");
        result.Should().Contain("throw new NotSupportedException(\"AVX-512 not supported on this CPU\")");
        result.Should().Contain("16x float or 8x double");
        result.Should().Contain("mask registers");
    }

    [Fact]
    public void AVX2VectorSum_ForFloat_ReferencesCorrectVectorSize()
    {
        // Act
        var result = SIMDTemplates.AVX2VectorSum<float>();

        // Assert
        result.Should().Contain("8 for float");
    }

    [Fact]
    public void AVX512VectorSum_ForFloat_ReferencesCorrectVectorSize()
    {
        // Act
        var result = SIMDTemplates.AVX512VectorSum<float>();

        // Assert
        result.Should().Contain("16 for float");
    }

    #endregion

    #region Template Substitution Tests

    [Fact]
    public void AllTemplates_DoNotContainUnresolvedPlaceholders()
    {
        // Act & Assert - VectorSelect
        var select = SIMDTemplates.VectorSelect<int, int>("x + 1");
        select.Should().NotContain("{INPUT_TYPE}");
        select.Should().NotContain("{OUTPUT_TYPE}");
        select.Should().NotContain("{TRANSFORM}");

        // VectorWhere
        var where = SIMDTemplates.VectorWhere<int>("x > 0");
        where.Should().NotContain("{ELEMENT_TYPE}");
        where.Should().NotContain("{PREDICATE}");

        // VectorSum
        var sum = SIMDTemplates.VectorSum<float>();
        sum.Should().NotContain("{ELEMENT_TYPE}");
    }

    [Fact]
    public void Templates_PreserveRuntimePlaceholders()
    {
        // These placeholders should remain for runtime substitution
        var result = SIMDTemplates.VectorSelect<float, float>("x * 2");

        // Assert
        result.Should().Contain("{INPUT}");
        result.Should().Contain("{OUTPUT}");
        result.Should().Contain("{LENGTH}");
    }

    #endregion

    #region Vectorization Hints Tests

    [Fact]
    public void VectorSelect_ContainsVectorizationHints()
    {
        // Act
        var result = SIMDTemplates.VectorSelect<float, float>("x * 2");

        // Assert
        result.Should().Contain("processes Vector<T>.Count elements per iteration");
        result.Should().Contain("Scalar remainder");
    }

    [Fact]
    public void VectorWhere_ContainsVectorizationHints()
    {
        // Act
        var result = SIMDTemplates.VectorWhere<int>("x > 0");

        // Assert
        result.Should().Contain("Vectorized loop with conditional selection");
        result.Should().Contain("Compact: extract elements where mask is true");
    }

    [Fact]
    public void VectorSum_ContainsHorizontalReductionComment()
    {
        // Act
        var result = SIMDTemplates.VectorSum<double>();

        // Assert
        result.Should().Contain("Horizontal sum across vector lanes");
    }

    [Fact]
    public void FusedOperations_IndicateFusionBenefit()
    {
        // Act
        var selectWhere = SIMDTemplates.FusedSelectWhere<int, int>("x * 2", "y > 0");
        var whereSelect = SIMDTemplates.FusedWhereSelect<int, int>("x > 0", "x * 2");

        // Assert
        selectWhere.Should().Contain("Fused vectorized loop: transform then filter in single pass");
        whereSelect.Should().Contain("Fused vectorized loop: filter then transform");
    }

    #endregion

    #region Type Constraint Validation Tests

    [Fact]
    public void VectorSelect_AcceptsAllSupportedTypes()
    {
        // Act & Assert - Should not throw
        var act1 = () => SIMDTemplates.VectorSelect<byte, byte>("x + 1");
        var act2 = () => SIMDTemplates.VectorSelect<short, short>("x + 1");
        var act3 = () => SIMDTemplates.VectorSelect<int, int>("x + 1");
        var act4 = () => SIMDTemplates.VectorSelect<long, long>("x + 1");
        var act5 = () => SIMDTemplates.VectorSelect<float, float>("x + 1");
        var act6 = () => SIMDTemplates.VectorSelect<double, double>("x + 1");

        act1.Should().NotThrow();
        act2.Should().NotThrow();
        act3.Should().NotThrow();
        act4.Should().NotThrow();
        act5.Should().NotThrow();
        act6.Should().NotThrow();
    }

    [Fact]
    public void VectorSum_RejectsUnsupportedTypes()
    {
        // This would fail at compile time due to INumber<T> constraint
        // But we can test the ValidateVectorType logic through reflection
        var act = () => SIMDTemplates.VectorSum<decimal>();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*decimal*not supported*");
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public void VectorSelect_WithEmptyTransform_ThrowsOrHandlesGracefully()
    {
        // Act
        var result = SIMDTemplates.VectorSelect<int, int>("");

        // Assert - Empty transform should still generate valid template
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("var vec = new Vector");
    }

    [Fact]
    public void VectorWhere_WithEmptyPredicate_GeneratesValidTemplate()
    {
        // Act
        var result = SIMDTemplates.VectorWhere<int>("");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("EvaluatePredicate");
    }

    [Fact]
    public void VectorMin_IncludesEmptySequenceCheck()
    {
        // Act
        var result = SIMDTemplates.VectorMin<int>();

        // Assert
        result.Should().Contain("if (length == 0) throw new InvalidOperationException");
    }

    [Fact]
    public void VectorMax_IncludesEmptySequenceCheck()
    {
        // Act
        var result = SIMDTemplates.VectorMax<float>();

        // Assert
        result.Should().Contain("if (length == 0) throw new InvalidOperationException");
    }

    [Fact]
    public void VectorBinaryOp_ValidatesEqualLength()
    {
        // Act
        var result = SIMDTemplates.VectorBinaryOp<float>("+");

        // Assert
        result.Should().Contain("if ({INPUT1}.Length != {INPUT2}.Length)");
        result.Should().Contain("throw new ArgumentException");
    }

    #endregion

    #region Performance Characteristics Tests

    [Fact]
    public void AllTemplates_IncludeRemainderHandling()
    {
        // Act
        var select = SIMDTemplates.VectorSelect<int, int>("x + 1");
        var where = SIMDTemplates.VectorWhere<int>("x > 0");
        var sum = SIMDTemplates.VectorSum<float>();
        var binaryOp = SIMDTemplates.VectorBinaryOp<double>("+");

        // Assert
        select.Should().Contain("Scalar remainder");
        where.Should().Contain("Scalar remainder");
        sum.Should().Contain("Add scalar remainder");
        binaryOp.Should().Contain("Scalar remainder");
    }

    [Fact]
    public void AllTemplates_UseProperLoopBounds()
    {
        // Act
        var select = SIMDTemplates.VectorSelect<float, float>("x * 2");
        var where = SIMDTemplates.VectorWhere<int>("x > 0");
        var sum = SIMDTemplates.VectorSum<double>();

        // Assert - All should use safe loop bounds
        select.Should().Contain("i <= length - vectorSize");
        where.Should().Contain("i <= length - vectorSize");
        sum.Should().Contain("i <= length - vectorSize");
    }

    [Fact]
    public void VectorSum_UsesEfficientAccumulation()
    {
        // Act
        var result = SIMDTemplates.VectorSum<float>();

        // Assert
        result.Should().Contain("accumulator += vec");
        result.Should().NotContain("accumulator = accumulator + vec"); // Should use compound assignment
    }

    [Fact]
    public void FusedOperations_AvoidIntermediateAllocations()
    {
        // Act
        var selectWhere = SIMDTemplates.FusedSelectWhere<int, int>("x * 2", "y > 0");

        // Assert
        selectWhere.Should().Contain("in single pass");
        selectWhere.Should().NotContain("var intermediate ="); // No intermediate arrays
    }

    #endregion

    #region Complex Generic Type Tests

    [Fact]
    public void Templates_HandleSignedAndUnsignedTypes()
    {
        // Act
        var signed = SIMDTemplates.VectorSelect<int, long>("x");
        var unsigned = SIMDTemplates.VectorSelect<uint, ulong>("x");

        // Assert
        signed.Should().Contain("Int32");
        signed.Should().Contain("Int64");
        unsigned.Should().Contain("UInt32");
        unsigned.Should().Contain("UInt64");
    }

    [Fact]
    public void Templates_HandleFloatingPointTypes()
    {
        // Act
        var floatTemplate = SIMDTemplates.VectorSum<float>();
        var doubleTemplate = SIMDTemplates.VectorSum<double>();

        // Assert
        floatTemplate.Should().Contain("Single");
        doubleTemplate.Should().Contain("Double");
    }

    #endregion

    #region Loop Unrolling Tests

    [Fact]
    public void VectorSelect_IncludesVectorSizeCalculation()
    {
        // Act
        var result = SIMDTemplates.VectorSelect<float, float>("x * 2");

        // Assert
        result.Should().Contain("var vectorSize = Vector<Single>.Count");
        result.Should().Contain("i += vectorSize");
    }

    [Fact]
    public void VectorWhere_HandlesVectorSizeCorrectly()
    {
        // Act
        var result = SIMDTemplates.VectorWhere<int>("x > 0");

        // Assert
        result.Should().Contain("var vectorSize = Vector<Int32>.Count");
        result.Should().Contain("for (int j = 0; j < vectorSize; j++)");
    }

    [Fact]
    public void FusedSelectWhere_UsesMinimumVectorSize()
    {
        // Act
        var result = SIMDTemplates.FusedSelectWhere<byte, int>("x", "y > 0");

        // Assert
        result.Should().Contain("Math.Min(Vector<Byte>.Count, Vector<Int32>.Count)");
    }

    #endregion
}
