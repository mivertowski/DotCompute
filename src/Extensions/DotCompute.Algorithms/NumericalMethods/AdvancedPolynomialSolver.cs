
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;

namespace DotCompute.Algorithms.NumericalMethods;


/// <summary>
/// Advanced polynomial root-finding algorithms with high numerical accuracy and stability.
/// Implements state-of-the-art algorithms including Durand-Kerner method for high-degree polynomials.
/// </summary>
public static class AdvancedPolynomialSolver
{
    /// <summary>
    /// Finds all real roots of a polynomial using appropriate algorithm based on degree.
    /// </summary>
    /// <param name="coefficients">Polynomial coefficients in descending order [a_n, a_{n-1}, ..., a_1, a_0]</param>
    /// <param name="tolerance">Numerical tolerance for convergence (default: 1e-10)</param>
    /// <param name="maxIterations">Maximum iterations for iterative methods (default: 1000)</param>
    /// <returns>Array of real roots sorted in ascending order</returns>
    public static float[] FindRealRoots(float[] coefficients, float tolerance = 1e-10f, int maxIterations = 1000)
    {
        ArgumentNullException.ThrowIfNull(coefficients);

        var degree = GetEffectiveDegree(coefficients);
        if (degree <= 0)
        {
            return [];
        }

        // Normalize polynomial by leading coefficient
        var normalized = NormalizePolynomial(coefficients, degree + 1);

        return degree switch
        {
            1 => SolveLinear(normalized),
            2 => SolveQuadratic(normalized),
            3 => SolveCubic(normalized),
            4 => SolveQuartic(normalized),
            _ => SolveHighDegree(normalized, tolerance, maxIterations)
        };
    }

    /// <summary>
    /// Finds all complex roots using the Durand-Kerner method.
    /// </summary>
    /// <param name="coefficients">Polynomial coefficients</param>
    /// <param name="tolerance">Convergence tolerance</param>
    /// <param name="maxIterations">Maximum iterations</param>
    /// <returns>Array of complex roots</returns>
    public static Complex[] FindComplexRoots(float[] coefficients, float tolerance = 1e-10f, int maxIterations = 1000)
    {
        ArgumentNullException.ThrowIfNull(coefficients);

        var degree = GetEffectiveDegree(coefficients);
        if (degree <= 0)
        {
            return [];
        }

        var normalized = NormalizePolynomial(coefficients, degree + 1);
        return DurandKernerMethod(normalized, tolerance, maxIterations);
    }

    /// <summary>
    /// Evaluates polynomial at given point using Horner's method.
    /// </summary>
    /// <param name="coefficients">Polynomial coefficients</param>
    /// <param name="x">Evaluation point</param>
    /// <returns>Polynomial value at x</returns>
    public static float EvaluatePolynomial(float[] coefficients, float x)
    {
        if (coefficients.Length == 0)
        {
            return 0.0f;
        }

        var result = coefficients[0];
        for (var i = 1; i < coefficients.Length; i++)
        {
            result = result * x + coefficients[i];
        }
        return result;
    }

    /// <summary>
    /// Evaluates polynomial at complex point using Horner's method.
    /// </summary>
    /// <param name="coefficients">Polynomial coefficients</param>
    /// <param name="z">Complex evaluation point</param>
    /// <returns>Complex polynomial value at z</returns>
    public static Complex EvaluatePolynomial(float[] coefficients, Complex z)
    {
        if (coefficients.Length == 0)
        {
            return Complex.Zero;
        }

        Complex result = coefficients[0];
        for (var i = 1; i < coefficients.Length; i++)
        {
            result = result * z + coefficients[i];
        }
        return result;
    }

    private static int GetEffectiveDegree(float[] coefficients)
    {
        for (var i = 0; i < coefficients.Length; i++)
        {
            if (Math.Abs(coefficients[i]) > 1e-15f)
            {
                return coefficients.Length - i - 1;
            }
        }
        return -1;
    }

    private static float[] NormalizePolynomial(float[] coefficients, int length)
    {
        var result = new float[length];
        Array.Copy(coefficients, coefficients.Length - length, result, 0, length);

        var leadingCoeff = result[0];
        if (Math.Abs(leadingCoeff) > 1e-15f)
        {
            for (var i = 0; i < result.Length; i++)
            {
                result[i] /= leadingCoeff;
            }
        }

        return result;
    }

    private static float[] SolveLinear(float[] coeffs)
    {
        // ax + b = 0 => x = -b/a
        if (Math.Abs(coeffs[0]) < 1e-15f)
        {
            return [];
        }

        return [-coeffs[1] / coeffs[0]];
    }

    private static float[] SolveQuadratic(float[] coeffs)
    {
        // ax² + bx + c = 0
        var a = coeffs[0];
        var b = coeffs[1];
        var c = coeffs[2];

        if (Math.Abs(a) < 1e-15f)
        {
            return SolveLinear([b, c]);
        }

        var discriminant = b * b - 4 * a * c;
        var roots = new List<float>();

        if (discriminant > 0)
        {
            // Two real roots - use numerically stable formulation
            var sqrt_d = (float)Math.Sqrt(discriminant);
            var q = -0.5f * (b + Math.Sign(b) * sqrt_d);
            roots.Add(q / a);
            roots.Add(c / q);
        }
        else if (Math.Abs(discriminant) < 1e-10f)
        {
            // One repeated root
            roots.Add(-b / (2 * a));
        }
        // Complex roots not included in real root finding

        roots.Sort();
        return [.. roots];
    }

    private static float[] SolveCubic(float[] coeffs)
    {
        // Cardano's method for ax³ + bx² + cx + d = 0
        var a = coeffs[0];
        var b = coeffs[1];
        var c = coeffs[2];
        var d = coeffs[3];

        if (Math.Abs(a) < 1e-15f)
        {
            return SolveQuadratic([b, c, d]);
        }

        // Convert to depressed cubic t³ + pt + q = 0 using substitution x = t - b/(3a)
        var p = (3 * a * c - b * b) / (3 * a * a);
        var q = (2 * b * b * b - 9 * a * b * c + 27 * a * a * d) / (27 * a * a * a);
        var offset = -b / (3 * a);

        var discriminant = -(4 * p * p * p + 27 * q * q);
        var roots = new List<float>();

        if (discriminant > 0)
        {
            // Three distinct real roots (Casus irreducibilis)
            var m = 2 * (float)Math.Sqrt(-p / 3);
            var theta = (1.0f / 3.0f) * (float)Math.Acos(3 * q / p * (float)Math.Sqrt(-3 / p));

            for (var k = 0; k < 3; k++)
            {
                var angle = theta - (2 * Math.PI * k) / 3;
                roots.Add(m * (float)Math.Cos(angle) + offset);
            }
        }
        else if (Math.Abs(discriminant) < 1e-10f)
        {
            // Multiple roots
            if (Math.Abs(p) < 1e-10f)
            {
                // Triple root
                roots.Add(offset);
            }
            else
            {
                // One single and one double root
                roots.Add(3 * q / p + offset);
                roots.Add(-3 * q / (2 * p) + offset);
            }
        }
        else
        {
            // One real root
            var sqrt_term = (float)Math.Sqrt(-discriminant / 108);
            var cbrt_arg = -q / 2 + sqrt_term;
            var u = (float)Math.Pow(Math.Abs(cbrt_arg), 1.0 / 3.0) * Math.Sign(cbrt_arg);
            var v = (Math.Abs(u) > 1e-10f) ? p / (3 * u) : 0;
            roots.Add(u + v + offset);
        }

        roots.Sort();
        return [.. roots];
    }

    private static float[] SolveQuartic(float[] coeffs)
        // Ferrari's method - for now, delegate to high-degree solver
        // Full Ferrari implementation would be quite complex





        => SolveHighDegree(coeffs, 1e-10f, 1000);

    private static float[] SolveHighDegree(float[] coeffs, float tolerance, int maxIterations)
    {
        _ = coeffs.Length - 1;
        var complexRoots = DurandKernerMethod(coeffs, tolerance, maxIterations);

        // Extract real roots
        var realRoots = new List<float>();
        foreach (var root in complexRoots)
        {
            if (Math.Abs(root.Imaginary) < tolerance)
            {
                realRoots.Add((float)root.Real);
            }
        }

        realRoots.Sort();
        return [.. realRoots];
    }

    private static Complex[] DurandKernerMethod(float[] coeffs, float tolerance, int maxIterations)
    {
        var degree = coeffs.Length - 1;
        if (degree <= 0)
        {
            return [];
        }

        var roots = new Complex[degree];
        var newRoots = new Complex[degree];

        // Initialize roots using a sophisticated starting configuration
        InitializeDurandKernerRoots(roots, degree);

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            var maxChange = 0.0;

            for (var i = 0; i < degree; i++)
            {
                var numerator = EvaluatePolynomial(coeffs, roots[i]);
                var denominator = Complex.One;

                // Compute product of differences
                for (var j = 0; j < degree; j++)
                {
                    if (i != j)
                    {
                        denominator *= (roots[i] - roots[j]);
                    }
                }

                if (denominator.Magnitude > 1e-15)
                {
                    newRoots[i] = roots[i] - numerator / denominator;
                }
                else
                {
                    // Perturb slightly if denominator is too small
                    newRoots[i] = roots[i] + new Complex(tolerance, tolerance);
                }

                var change = (newRoots[i] - roots[i]).Magnitude;
                maxChange = Math.Max(maxChange, change);
            }

            Array.Copy(newRoots, roots, degree);

            if (maxChange < tolerance)
            {
                break;
            }
        }

        // Polish roots using Newton's method
        for (var i = 0; i < roots.Length; i++)
        {
            roots[i] = NewtonRefinement(coeffs, roots[i], tolerance, 10);
        }

        return roots;
    }

    private static void InitializeDurandKernerRoots(Complex[] roots, int degree)
    {
        // Use a more sophisticated initialization than simple equally-spaced points
        var goldenRatio = (1.0 + Math.Sqrt(5)) / 2.0;

        for (var i = 0; i < degree; i++)
        {
            // Combine geometric progression with angular distribution
            var radius = Math.Pow(goldenRatio, (i % 4) - 1.5);
            var angle = 2.0 * Math.PI * i / degree + Math.PI / degree;

            // Add small perturbation to avoid symmetries
            var perturbation = 0.1 * Math.Sin(goldenRatio * i);

            roots[i] = new Complex(
                radius * Math.Cos(angle) + perturbation,
                radius * Math.Sin(angle) + perturbation * 0.7
            );
        }
    }

    private static Complex NewtonRefinement(float[] coeffs, Complex initialGuess, float tolerance, int maxIter)
    {
        var z = initialGuess;

        for (var i = 0; i < maxIter; i++)
        {
            var f = EvaluatePolynomial(coeffs, z);
            var fp = EvaluatePolynomialDerivative(coeffs, z);

            if (fp.Magnitude < 1e-15)
            {
                break;
            }

            var correction = f / fp;
            z -= correction;

            if (correction.Magnitude < tolerance)
            {
                break;
            }
        }

        return z;
    }

    private static Complex EvaluatePolynomialDerivative(float[] coeffs, Complex z)
    {
        if (coeffs.Length <= 1)
        {
            return Complex.Zero;
        }

        Complex result = coeffs[0] * (coeffs.Length - 1);
        for (var i = 1; i < coeffs.Length - 1; i++)
        {
            result = result * z + coeffs[i] * (coeffs.Length - 1 - i);
        }
        return result;
    }

    /// <summary>
    /// Provides detailed analysis of polynomial characteristics.
    /// </summary>
    /// <param name="coefficients">Polynomial coefficients</param>
    /// <returns>Analysis results including condition number and root bounds</returns>
    public static PolynomialAnalysis AnalyzePolynomial(float[] coefficients)
    {
        ArgumentNullException.ThrowIfNull(coefficients);

        var degree = GetEffectiveDegree(coefficients);

        // Calculate root bounds and condition number before creating the analysis object
        var rootBounds = degree > 0 ? EstimateRootBounds(coefficients, degree) : (0f, 0f);
        var conditionNumber = degree > 0 ? EstimateConditionNumber(coefficients) : 0f;

        var analysis = new PolynomialAnalysis
        {
            Degree = degree,
            IsConstant = degree == 0,
            IsLinear = degree == 1,
            IsQuadratic = degree == 2,
            LeadingCoefficient = degree >= 0 ? coefficients[coefficients.Length - degree - 1] : 0,
            RootBounds = rootBounds,
            ConditionNumber = conditionNumber
        };

        return analysis;
    }

    private static (float lower, float upper) EstimateRootBounds(float[] coeffs, int degree)
    {
        // Cauchy bound: max|root| <= 1 + max|a_i/a_n| for i < n
        var leadingCoeff = Math.Abs(coeffs[0]);
        var maxRatio = 0.0f;

        for (var i = 1; i < coeffs.Length; i++)
        {
            maxRatio = Math.Max(maxRatio, Math.Abs(coeffs[i]) / leadingCoeff);
        }

        var cauchyBound = 1.0f + maxRatio;
        return (-cauchyBound, cauchyBound);
    }

    private static float EstimateConditionNumber(float[] coefficients)
    {
        // Simple estimate based on coefficient ratios
        var maxCoeff = coefficients.Max(Math.Abs);
        var minCoeff = coefficients.Where(c => Math.Abs(c) > 1e-15f).Min(Math.Abs);
        return maxCoeff / Math.Max(minCoeff, 1e-15f);
    }
}

/// <summary>
/// Results of polynomial analysis including mathematical properties and numerical characteristics.
/// </summary>
public class PolynomialAnalysis
{
    /// <summary>
    /// Gets or sets the degree.
    /// </summary>
    /// <value>The degree.</value>
    public int Degree { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether constant.
    /// </summary>
    /// <value>The is constant.</value>
    public bool IsConstant { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether linear.
    /// </summary>
    /// <value>The is linear.</value>
    public bool IsLinear { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether quadratic.
    /// </summary>
    /// <value>The is quadratic.</value>
    public bool IsQuadratic { get; init; }
    /// <summary>
    /// Gets or sets the leading coefficient.
    /// </summary>
    /// <value>The leading coefficient.</value>
    public float LeadingCoefficient { get; init; }
    /// <summary>
    /// Gets or sets the root bounds.
    /// </summary>
    /// <value>The root bounds.</value>
    public (float lower, float upper) RootBounds { get; init; }
    /// <summary>
    /// Gets or sets the condition number.
    /// </summary>
    /// <value>The condition number.</value>
    public float ConditionNumber { get; init; }
}
