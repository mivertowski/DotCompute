using System.Numerics;
using System.Text;

namespace DotCompute.Linq.CodeGeneration;

/// <summary>
/// Provides SIMD code templates for common LINQ operations.
/// Generates optimized vectorized code using System.Numerics.Vector&lt;T&gt;.
/// </summary>
public static class SIMDTemplates
{
    #region Map Operations

    /// <summary>
    /// Generates a vectorized Select (map) operation template.
    /// </summary>
    /// <typeparam name="T">Input element type (must be unmanaged).</typeparam>
    /// <typeparam name="TResult">Output element type (must be unmanaged).</typeparam>
    /// <param name="transformExpression">C# expression to transform each element (use 'x' as variable).</param>
    /// <returns>C# code template with placeholders for input/output arrays.</returns>
    /// <exception cref="ArgumentException">Thrown if types are not supported by Vector&lt;T&gt;.</exception>
    /// <example>
    /// <code>
    /// var template = SIMDTemplates.VectorSelect&lt;float, float&gt;("x * 2.0f");
    /// // Generates: vectorized loop applying x => x * 2.0f
    /// </code>
    /// </example>
    public static string VectorSelect<T, TResult>(string transformExpression)
        where T : unmanaged
        where TResult : unmanaged
    {
        ValidateVectorType<T>();
        ValidateVectorType<TResult>();

        var template = """
            int i = 0;
            var vectorSize = Vector<{INPUT_TYPE}>.Count;
            var length = {LENGTH};

            // Vectorized loop - processes Vector<T>.Count elements per iteration
            for (; i <= length - vectorSize; i += vectorSize)
            {
                var vec = new Vector<{INPUT_TYPE}>({INPUT}, i);

                // Apply transformation: {TRANSFORM}
                var resultVec = ApplyTransform(vec);

                resultVec.CopyTo({OUTPUT}, i);
            }

            // Scalar remainder - handle remaining elements
            for (; i < length; i++)
            {
                {OUTPUT}[i] = ({OUTPUT_TYPE})({TRANSFORM_SCALAR});
            }
            """;

        var substitutions = new Dictionary<string, string>
        {
            { "INPUT_TYPE", typeof(T).Name },
            { "OUTPUT_TYPE", typeof(TResult).Name },
            { "TRANSFORM", transformExpression.Replace("x", "vec", StringComparison.Ordinal) },
            { "TRANSFORM_SCALAR", transformExpression.Replace("x", $"{{INPUT}}[i]", StringComparison.Ordinal) }
        };

        return FormatTemplate(template, substitutions);
    }

    #endregion

    #region Filter Operations

    /// <summary>
    /// Generates a vectorized Where (filter) operation template.
    /// </summary>
    /// <typeparam name="T">Element type (must be unmanaged and IComparable).</typeparam>
    /// <param name="predicateExpression">C# boolean expression to filter elements (use 'x' as variable).</param>
    /// <returns>C# code template that filters and compacts results.</returns>
    /// <example>
    /// <code>
    /// var template = SIMDTemplates.VectorWhere&lt;int&gt;("x > 0");
    /// // Generates: vectorized filter keeping only positive numbers
    /// </code>
    /// </example>
    public static string VectorWhere<T>(string predicateExpression)
        where T : unmanaged, IComparable<T>
    {
        ValidateVectorType<T>();

        var template = """
            var result = new List<{ELEMENT_TYPE}>(capacity: {LENGTH});
            int i = 0;
            var vectorSize = Vector<{ELEMENT_TYPE}>.Count;
            var length = {LENGTH};

            // Vectorized loop with conditional selection
            for (; i <= length - vectorSize; i += vectorSize)
            {
                var vec = new Vector<{ELEMENT_TYPE}>({INPUT}, i);

                // Evaluate predicate: {PREDICATE}
                var mask = EvaluatePredicate(vec);

                // Compact: extract elements where mask is true
                for (int j = 0; j < vectorSize; j++)
                {
                    if (GetMaskElement(mask, j))
                    {
                        result.Add(vec[j]);
                    }
                }
            }

            // Scalar remainder
            for (; i < length; i++)
            {
                if ({PREDICATE_SCALAR})
                {
                    result.Add({INPUT}[i]);
                }
            }

            return result.ToArray();
            """;

        var substitutions = new Dictionary<string, string>
        {
            { "ELEMENT_TYPE", typeof(T).Name },
            { "PREDICATE", predicateExpression.Replace("x", "vec", StringComparison.Ordinal) },
            { "PREDICATE_SCALAR", predicateExpression.Replace("x", $"{{INPUT}}[i]", StringComparison.Ordinal) }
        };

        return FormatTemplate(template, substitutions);
    }

    #endregion

    #region Aggregation Operations

    /// <summary>
    /// Generates a vectorized Sum aggregation template.
    /// </summary>
    /// <typeparam name="T">Numeric element type.</typeparam>
    /// <returns>C# code template for parallel horizontal reduction.</returns>
    /// <example>
    /// <code>
    /// var template = SIMDTemplates.VectorSum&lt;float&gt;();
    /// // Generates: parallel sum with accumulator combination
    /// </code>
    /// </example>
    public static string VectorSum<T>()
        where T : unmanaged, INumber<T>
    {
        ValidateVectorType<T>();

        var template = """
            var accumulator = Vector<{ELEMENT_TYPE}>.Zero;
            int i = 0;
            var vectorSize = Vector<{ELEMENT_TYPE}>.Count;
            var length = {LENGTH};

            // Vectorized accumulation
            for (; i <= length - vectorSize; i += vectorSize)
            {
                var vec = new Vector<{ELEMENT_TYPE}>({INPUT}, i);
                accumulator += vec;
            }

            // Horizontal sum across vector lanes
            {ELEMENT_TYPE} sum = {ELEMENT_TYPE}.Zero;
            for (int j = 0; j < vectorSize; j++)
            {
                sum += accumulator[j];
            }

            // Add scalar remainder
            for (; i < length; i++)
            {
                sum += {INPUT}[i];
            }

            return sum;
            """;

        var substitutions = new Dictionary<string, string>
        {
            { "ELEMENT_TYPE", typeof(T).Name }
        };

        return FormatTemplate(template, substitutions);
    }

    /// <summary>
    /// Generates a vectorized custom aggregation template.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <typeparam name="TAccumulate">Accumulator type.</typeparam>
    /// <param name="seedExpression">Initial accumulator value expression.</param>
    /// <param name="accumulatorExpression">Accumulation function (use 'acc' and 'x' as variables).</param>
    /// <returns>C# code template for custom reduction.</returns>
    public static string VectorAggregate<T, TAccumulate>(
        string seedExpression,
        string accumulatorExpression)
        where T : unmanaged
        where TAccumulate : unmanaged
    {
        var template = """
            var accumulator = {SEED};
            int i = 0;
            var length = {LENGTH};

            // Process all elements (vectorization depends on operation)
            for (; i < length; i++)
            {
                var x = {INPUT}[i];
                accumulator = {ACCUMULATOR_FUNC};
            }

            return accumulator;
            """;

        var substitutions = new Dictionary<string, string>
        {
            { "SEED", seedExpression },
            { "ACCUMULATOR_FUNC", accumulatorExpression }
        };

        return FormatTemplate(template, substitutions);
    }

    /// <summary>
    /// Generates a vectorized Min operation template.
    /// </summary>
    /// <typeparam name="T">Comparable element type.</typeparam>
    /// <returns>C# code template using Vector.Min intrinsics.</returns>
    public static string VectorMin<T>()
        where T : unmanaged, IComparable<T>, IMinMaxValue<T>
    {
        ValidateVectorType<T>();

        var template = """
            var minVec = new Vector<{ELEMENT_TYPE}>({ELEMENT_TYPE}.MaxValue);
            int i = 0;
            var vectorSize = Vector<{ELEMENT_TYPE}>.Count;
            var length = {LENGTH};

            if (length == 0) throw new InvalidOperationException("Sequence contains no elements");

            // Vectorized minimum
            for (; i <= length - vectorSize; i += vectorSize)
            {
                var vec = new Vector<{ELEMENT_TYPE}>({INPUT}, i);
                minVec = Vector.Min(minVec, vec);
            }

            // Horizontal minimum
            {ELEMENT_TYPE} min = minVec[0];
            for (int j = 1; j < vectorSize; j++)
            {
                if (minVec[j].CompareTo(min) < 0)
                    min = minVec[j];
            }

            // Check scalar remainder
            for (; i < length; i++)
            {
                if ({INPUT}[i].CompareTo(min) < 0)
                    min = {INPUT}[i];
            }

            return min;
            """;

        var substitutions = new Dictionary<string, string>
        {
            { "ELEMENT_TYPE", typeof(T).Name }
        };

        return FormatTemplate(template, substitutions);
    }

    /// <summary>
    /// Generates a vectorized Max operation template.
    /// </summary>
    /// <typeparam name="T">Comparable element type.</typeparam>
    /// <returns>C# code template using Vector.Max intrinsics.</returns>
    public static string VectorMax<T>()
        where T : unmanaged, IComparable<T>, IMinMaxValue<T>
    {
        ValidateVectorType<T>();

        var template = """
            var maxVec = new Vector<{ELEMENT_TYPE}>({ELEMENT_TYPE}.MinValue);
            int i = 0;
            var vectorSize = Vector<{ELEMENT_TYPE}>.Count;
            var length = {LENGTH};

            if (length == 0) throw new InvalidOperationException("Sequence contains no elements");

            // Vectorized maximum
            for (; i <= length - vectorSize; i += vectorSize)
            {
                var vec = new Vector<{ELEMENT_TYPE}>({INPUT}, i);
                maxVec = Vector.Max(maxVec, vec);
            }

            // Horizontal maximum
            {ELEMENT_TYPE} max = maxVec[0];
            for (int j = 1; j < vectorSize; j++)
            {
                if (maxVec[j].CompareTo(max) > 0)
                    max = maxVec[j];
            }

            // Check scalar remainder
            for (; i < length; i++)
            {
                if ({INPUT}[i].CompareTo(max) > 0)
                    max = {INPUT}[i];
            }

            return max;
            """;

        var substitutions = new Dictionary<string, string>
        {
            { "ELEMENT_TYPE", typeof(T).Name }
        };

        return FormatTemplate(template, substitutions);
    }

    #endregion

    #region Fused Operations

    /// <summary>
    /// Generates a fused Select then Where template (map then filter).
    /// Eliminates intermediate allocation by combining operations.
    /// </summary>
    /// <typeparam name="T">Input element type.</typeparam>
    /// <typeparam name="TResult">Transformed element type.</typeparam>
    /// <param name="transformExpression">Transformation expression (use 'x').</param>
    /// <param name="predicateExpression">Filter predicate on transformed value (use 'y').</param>
    /// <returns>C# code template for fused operation.</returns>
    public static string FusedSelectWhere<T, TResult>(
        string transformExpression,
        string predicateExpression)
        where T : unmanaged
        where TResult : unmanaged, IComparable<TResult>
    {
        ValidateVectorType<T>();
        ValidateVectorType<TResult>();

        var template = """
            var result = new List<{RESULT_TYPE}>(capacity: {LENGTH});
            int i = 0;
            var vectorSize = Math.Min(Vector<{INPUT_TYPE}>.Count, Vector<{RESULT_TYPE}>.Count);
            var length = {LENGTH};

            // Fused vectorized loop: transform then filter in single pass
            for (; i <= length - vectorSize; i += vectorSize)
            {
                var vec = new Vector<{INPUT_TYPE}>({INPUT}, i);

                // Transform: {TRANSFORM}
                var transformed = ApplyTransform(vec);

                // Filter: {PREDICATE}
                var mask = EvaluatePredicate(transformed);

                // Compact results
                for (int j = 0; j < vectorSize; j++)
                {
                    if (GetMaskElement(mask, j))
                    {
                        result.Add(transformed[j]);
                    }
                }
            }

            // Scalar remainder
            for (; i < length; i++)
            {
                var transformed = ({RESULT_TYPE})({TRANSFORM_SCALAR});
                if ({PREDICATE_SCALAR})
                {
                    result.Add(transformed);
                }
            }

            return result.ToArray();
            """;

        var substitutions = new Dictionary<string, string>
        {
            { "INPUT_TYPE", typeof(T).Name },
            { "RESULT_TYPE", typeof(TResult).Name },
            { "TRANSFORM", transformExpression.Replace("x", "vec", StringComparison.Ordinal) },
            { "TRANSFORM_SCALAR", transformExpression.Replace("x", $"{{INPUT}}[i]", StringComparison.Ordinal) },
            { "PREDICATE", predicateExpression.Replace("y", "transformed", StringComparison.Ordinal) },
            { "PREDICATE_SCALAR", predicateExpression.Replace("y", "transformed", StringComparison.Ordinal) }
        };

        return FormatTemplate(template, substitutions);
    }

    /// <summary>
    /// Generates a fused Where then Select template (filter then map).
    /// More memory efficient when filter is highly selective.
    /// </summary>
    /// <typeparam name="T">Input element type.</typeparam>
    /// <typeparam name="TResult">Transformed element type.</typeparam>
    /// <param name="predicateExpression">Filter predicate (use 'x').</param>
    /// <param name="transformExpression">Transformation expression on filtered value (use 'x').</param>
    /// <returns>C# code template for fused operation.</returns>
    public static string FusedWhereSelect<T, TResult>(
        string predicateExpression,
        string transformExpression)
        where T : unmanaged, IComparable<T>
        where TResult : unmanaged
    {
        ValidateVectorType<T>();
        ValidateVectorType<TResult>();

        var template = """
            var result = new List<{RESULT_TYPE}>(capacity: {LENGTH} / 2);
            int i = 0;
            var vectorSize = Vector<{INPUT_TYPE}>.Count;
            var length = {LENGTH};

            // Fused vectorized loop: filter then transform
            for (; i <= length - vectorSize; i += vectorSize)
            {
                var vec = new Vector<{INPUT_TYPE}>({INPUT}, i);

                // Filter: {PREDICATE}
                var mask = EvaluatePredicate(vec);

                // Transform only filtered elements: {TRANSFORM}
                for (int j = 0; j < vectorSize; j++)
                {
                    if (GetMaskElement(mask, j))
                    {
                        var x = vec[j];
                        result.Add(({RESULT_TYPE})({TRANSFORM_SCALAR}));
                    }
                }
            }

            // Scalar remainder
            for (; i < length; i++)
            {
                if ({PREDICATE_SCALAR})
                {
                    var x = {INPUT}[i];
                    result.Add(({RESULT_TYPE})({TRANSFORM_SCALAR}));
                }
            }

            return result.ToArray();
            """;

        var substitutions = new Dictionary<string, string>
        {
            { "INPUT_TYPE", typeof(T).Name },
            { "RESULT_TYPE", typeof(TResult).Name },
            { "PREDICATE", predicateExpression.Replace("x", "vec", StringComparison.Ordinal) },
            { "PREDICATE_SCALAR", predicateExpression.Replace("x", $"{{INPUT}}[i]", StringComparison.Ordinal) },
            { "TRANSFORM_SCALAR", transformExpression.Replace("x", "x", StringComparison.Ordinal) }
        };

        return FormatTemplate(template, substitutions);
    }

    #endregion

    #region Binary Operations

    /// <summary>
    /// Generates a vectorized binary operation template (element-wise).
    /// </summary>
    /// <typeparam name="T">Element type (must support the operation).</typeparam>
    /// <param name="operatorSymbol">C# operator symbol (+, -, *, /, %).</param>
    /// <returns>C# code template for element-wise binary operation.</returns>
    /// <example>
    /// <code>
    /// var template = SIMDTemplates.VectorBinaryOp&lt;float&gt;("*");
    /// // Generates: element-wise multiplication of two arrays
    /// </code>
    /// </example>
    public static string VectorBinaryOp<T>(string operatorSymbol)
        where T : unmanaged, INumber<T>
    {
        ValidateVectorType<T>();

        var template = """
            if ({INPUT1}.Length != {INPUT2}.Length)
                throw new ArgumentException("Arrays must have equal length");

            var result = new {ELEMENT_TYPE}[{INPUT1}.Length];
            int i = 0;
            var vectorSize = Vector<{ELEMENT_TYPE}>.Count;
            var length = {INPUT1}.Length;

            // Vectorized binary operation
            for (; i <= length - vectorSize; i += vectorSize)
            {
                var vec1 = new Vector<{ELEMENT_TYPE}>({INPUT1}, i);
                var vec2 = new Vector<{ELEMENT_TYPE}>({INPUT2}, i);
                var resultVec = vec1 {OPERATOR} vec2;
                resultVec.CopyTo(result, i);
            }

            // Scalar remainder
            for (; i < length; i++)
            {
                result[i] = {INPUT1}[i] {OPERATOR} {INPUT2}[i];
            }

            return result;
            """;

        var substitutions = new Dictionary<string, string>
        {
            { "ELEMENT_TYPE", typeof(T).Name },
            { "OPERATOR", operatorSymbol }
        };

        return FormatTemplate(template, substitutions);
    }

    #endregion

    #region Advanced Operations

    /// <summary>
    /// Generates a vectorized prefix sum (scan) template.
    /// Implements parallel inclusive prefix sum algorithm.
    /// </summary>
    /// <typeparam name="T">Numeric element type.</typeparam>
    /// <returns>C# code template for parallel scan.</returns>
    public static string VectorScan<T>()
        where T : unmanaged, INumber<T>
    {
        ValidateVectorType<T>();

        var template = """
            var result = new {ELEMENT_TYPE}[{LENGTH}];
            if ({LENGTH} == 0) return result;

            var vectorSize = Vector<{ELEMENT_TYPE}>.Count;
            var length = {LENGTH};

            // Phase 1: Compute prefix sums within vector blocks
            var blockSums = new List<{ELEMENT_TYPE}>();
            {ELEMENT_TYPE} runningSum = {ELEMENT_TYPE}.Zero;

            for (int i = 0; i < length; i += vectorSize)
            {
                int blockLength = Math.Min(vectorSize, length - i);

                for (int j = 0; j < blockLength; j++)
                {
                    runningSum += {INPUT}[i + j];
                    result[i + j] = runningSum;
                }

                if (i + vectorSize < length)
                {
                    blockSums.Add(runningSum);
                }
            }

            return result;
            """;

        var substitutions = new Dictionary<string, string>
        {
            { "ELEMENT_TYPE", typeof(T).Name }
        };

        return FormatTemplate(template, substitutions);
    }

    /// <summary>
    /// Generates a vectorized GroupBy operation template.
    /// Uses hash-based grouping with vectorized key extraction.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <param name="keySelectorExpression">Key selection expression (use 'x').</param>
    /// <returns>C# code template for grouping operation.</returns>
    public static string VectorGroupBy<T, TKey>(string keySelectorExpression)
        where T : unmanaged
        where TKey : notnull
    {
        var template = """
            var groups = new Dictionary<{KEY_TYPE}, List<{ELEMENT_TYPE}>>();
            var length = {LENGTH};

            // Process all elements (grouping is inherently scalar)
            for (int i = 0; i < length; i++)
            {
                var x = {INPUT}[i];
                var key = {KEY_SELECTOR};

                if (!groups.TryGetValue(key, out var group))
                {
                    group = new List<{ELEMENT_TYPE}>();
                    groups[key] = group;
                }
                group.Add(x);
            }

            return groups;
            """;

        var substitutions = new Dictionary<string, string>
        {
            { "ELEMENT_TYPE", typeof(T).Name },
            { "KEY_TYPE", typeof(TKey).Name },
            { "KEY_SELECTOR", keySelectorExpression }
        };

        return FormatTemplate(template, substitutions);
    }

    /// <summary>
    /// Generates a vectorized Join operation template.
    /// Implements hash join with vectorized key comparison.
    /// </summary>
    /// <typeparam name="T1">Outer sequence element type.</typeparam>
    /// <typeparam name="T2">Inner sequence element type.</typeparam>
    /// <typeparam name="TKey">Join key type.</typeparam>
    /// <param name="outerKeySelector">Outer key selector (use 'x').</param>
    /// <param name="innerKeySelector">Inner key selector (use 'y').</param>
    /// <returns>C# code template for hash join.</returns>
    public static string VectorJoin<T1, T2, TKey>(
        string outerKeySelector,
        string innerKeySelector)
        where T1 : unmanaged
        where T2 : unmanaged
        where TKey : notnull
    {
        var template = """
            var result = new List<({OUTER_TYPE} Outer, {INNER_TYPE} Inner)>();

            // Build hash table from inner sequence
            var innerLookup = new Dictionary<{KEY_TYPE}, List<{INNER_TYPE}>>();
            for (int i = 0; i < {INNER}.Length; i++)
            {
                var y = {INNER}[i];
                var key = {INNER_KEY_SELECTOR};

                if (!innerLookup.TryGetValue(key, out var list))
                {
                    list = new List<{INNER_TYPE}>();
                    innerLookup[key] = list;
                }
                list.Add(y);
            }

            // Probe with outer sequence
            for (int i = 0; i < {OUTER}.Length; i++)
            {
                var x = {OUTER}[i];
                var key = {OUTER_KEY_SELECTOR};

                if (innerLookup.TryGetValue(key, out var matches))
                {
                    foreach (var match in matches)
                    {
                        result.Add((x, match));
                    }
                }
            }

            return result.ToArray();
            """;

        var substitutions = new Dictionary<string, string>
        {
            { "OUTER_TYPE", typeof(T1).Name },
            { "INNER_TYPE", typeof(T2).Name },
            { "KEY_TYPE", typeof(TKey).Name },
            { "OUTER_KEY_SELECTOR", outerKeySelector },
            { "INNER_KEY_SELECTOR", innerKeySelector }
        };

        return FormatTemplate(template, substitutions);
    }

    #endregion

    #region CPU-Specific Optimizations

    /// <summary>
    /// Generates AVX2-specific vectorized sum template (256-bit vectors).
    /// Requires AVX2 CPU support at runtime.
    /// </summary>
    /// <typeparam name="T">Numeric element type (float or double recommended).</typeparam>
    /// <returns>C# code template using explicit AVX2 intrinsics.</returns>
    public static string AVX2VectorSum<T>()
        where T : unmanaged, INumber<T>
    {
        var template = """
            // AVX2 optimization: 256-bit vectors (8x float or 4x double)
            if (!System.Runtime.Intrinsics.X86.Avx2.IsSupported)
                throw new NotSupportedException("AVX2 not supported on this CPU");

            var accumulator = Vector<{ELEMENT_TYPE}>.Zero;
            int i = 0;
            var vectorSize = Vector<{ELEMENT_TYPE}>.Count; // 8 for float, 4 for double
            var length = {LENGTH};

            // Process with 256-bit vectors
            for (; i <= length - vectorSize; i += vectorSize)
            {
                var vec = new Vector<{ELEMENT_TYPE}>({INPUT}, i);
                accumulator += vec;
            }

            // Horizontal reduction
            {ELEMENT_TYPE} sum = {ELEMENT_TYPE}.Zero;
            for (int j = 0; j < vectorSize; j++)
            {
                sum += accumulator[j];
            }

            // Scalar remainder
            for (; i < length; i++)
            {
                sum += {INPUT}[i];
            }

            return sum;
            """;

        var substitutions = new Dictionary<string, string>
        {
            { "ELEMENT_TYPE", typeof(T).Name }
        };

        return FormatTemplate(template, substitutions);
    }

    /// <summary>
    /// Generates AVX512-specific vectorized sum template (512-bit vectors).
    /// Requires AVX-512 CPU support at runtime (high-end CPUs).
    /// </summary>
    /// <typeparam name="T">Numeric element type (float or double recommended).</typeparam>
    /// <returns>C# code template using AVX-512 intrinsics with mask registers.</returns>
    public static string AVX512VectorSum<T>()
        where T : unmanaged, INumber<T>
    {
        var template = """
            // AVX-512 optimization: 512-bit vectors (16x float or 8x double)
            if (!System.Runtime.Intrinsics.X86.Avx512F.IsSupported)
                throw new NotSupportedException("AVX-512 not supported on this CPU");

            var accumulator = Vector<{ELEMENT_TYPE}>.Zero;
            int i = 0;
            var vectorSize = Vector<{ELEMENT_TYPE}>.Count; // 16 for float, 8 for double
            var length = {LENGTH};

            // Process with 512-bit vectors
            for (; i <= length - vectorSize; i += vectorSize)
            {
                var vec = new Vector<{ELEMENT_TYPE}>({INPUT}, i);
                accumulator += vec;
            }

            // Horizontal reduction with mask registers
            {ELEMENT_TYPE} sum = {ELEMENT_TYPE}.Zero;
            for (int j = 0; j < vectorSize; j++)
            {
                sum += accumulator[j];
            }

            // Scalar remainder
            for (; i < length; i++)
            {
                sum += {INPUT}[i];
            }

            return sum;
            """;

        var substitutions = new Dictionary<string, string>
        {
            { "ELEMENT_TYPE", typeof(T).Name }
        };

        return FormatTemplate(template, substitutions);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets a standard vectorized loop template structure.
    /// </summary>
    /// <param name="mainLoopBody">Code for vectorized loop body.</param>
    /// <param name="remainderLoopBody">Code for scalar remainder loop.</param>
    /// <returns>Complete loop template with bounds calculation.</returns>
    private static string GetVectorizedLoopTemplate(
        string mainLoopBody,
        string remainderLoopBody)
    {
        return $$"""
            int i = 0;
            var vectorSize = Vector<{ELEMENT_TYPE}>.Count;
            var length = {LENGTH};

            // Vectorized main loop
            for (; i <= length - vectorSize; i += vectorSize)
            {
                {{mainLoopBody}}
            }

            // Scalar remainder loop
            for (; i < length; i++)
            {
                {{remainderLoopBody}}
            }
            """;
    }

    /// <summary>
    /// Formats a template by replacing placeholders with actual values.
    /// </summary>
    /// <param name="template">Template string with {PLACEHOLDER} markers.</param>
    /// <param name="substitutions">Dictionary of placeholder-to-value mappings.</param>
    /// <returns>Formatted template string.</returns>
    /// <exception cref="ArgumentException">Thrown if compile-time placeholders are missing substitutions.</exception>
    /// <remarks>
    /// Runtime placeholders (INPUT, OUTPUT, LENGTH, INPUT1, INPUT2, OUTER, INNER, ACCUMULATOR)
    /// are intentionally preserved for later substitution by the code generator.
    /// </remarks>
    private static string FormatTemplate(
        string template,
        Dictionary<string, string> substitutions)
    {
        var result = template;

        // Replace compile-time placeholders
        foreach (var kvp in substitutions)
        {
            var placeholder = $"{{{kvp.Key}}}";
            result = result.Replace(placeholder, kvp.Value, StringComparison.Ordinal);
        }

        // Define runtime placeholders that should be preserved for later substitution
        var runtimePlaceholders = new HashSet<string>(StringComparer.Ordinal)
        {
            "INPUT", "OUTPUT", "LENGTH",
            "INPUT1", "INPUT2",
            "OUTER", "INNER",
            "ACCUMULATOR",
            "TRANSFORM", "PREDICATE"  // Documentation placeholders in comments
        };

        // Validate that only expected placeholders remain
        // Match only uppercase placeholder patterns: {WORD_WITH_UNDERSCORES}
        // This avoids matching code blocks with curly braces
        var placeholderPattern = @"\{([A-Z][A-Z0-9_]*)\}";
        var remaining = System.Text.RegularExpressions.Regex.Matches(result, placeholderPattern);
        var unexpectedPlaceholders = new List<string>();

        foreach (System.Text.RegularExpressions.Match match in remaining)
        {
            var placeholderName = match.Groups[1].Value;
            if (!runtimePlaceholders.Contains(placeholderName))
            {
                unexpectedPlaceholders.Add(match.Value);
            }
        }

        if (unexpectedPlaceholders.Count > 0)
        {
            var unresolved = string.Join(", ", unexpectedPlaceholders.Distinct());
            throw new ArgumentException($"Template contains unresolved placeholders: {unresolved}");
        }

        return result;
    }

    /// <summary>
    /// Validates that a type is supported by Vector&lt;T&gt;.
    /// </summary>
    /// <typeparam name="T">Type to validate.</typeparam>
    /// <exception cref="ArgumentException">Thrown if type is not supported.</exception>
    private static void ValidateVectorType<T>() where T : unmanaged
    {
        // Vector<T> supports: byte, sbyte, short, ushort, int, uint, long, ulong, float, double
        var supportedTypes = new[]
        {
            typeof(byte), typeof(sbyte),
            typeof(short), typeof(ushort),
            typeof(int), typeof(uint),
            typeof(long), typeof(ulong),
            typeof(float), typeof(double)
        };

        if (!supportedTypes.Contains(typeof(T)))
        {
            throw new ArgumentException(
                $"Type {typeof(T).Name} is not supported by Vector<T>. " +
                $"Supported types: {string.Join(", ", supportedTypes.Select(t => t.Name))}");
        }
    }

    #endregion
}
