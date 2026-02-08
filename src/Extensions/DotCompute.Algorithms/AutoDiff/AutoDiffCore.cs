// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;

#pragma warning disable CA2225 // Operator overloads have named alternates

namespace DotCompute.Algorithms.AutoDiff;

/// <summary>
/// Automatic differentiation mode.
/// </summary>
public enum AutoDiffMode
{
    /// <summary>
    /// Forward mode AD (directional derivatives).
    /// Efficient when outputs >> inputs.
    /// </summary>
    Forward,

    /// <summary>
    /// Reverse mode AD (backpropagation).
    /// Efficient when inputs >> outputs (common in ML).
    /// </summary>
    Reverse
}

/// <summary>
/// Represents a differentiable value that tracks gradients.
/// </summary>
/// <typeparam name="T">The numeric type.</typeparam>
public interface IDifferentiable<T> where T : unmanaged
{
    /// <summary>
    /// Gets the current value.
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// Gets the gradient with respect to this variable.
    /// </summary>
    public T Gradient { get; }

    /// <summary>
    /// Gets whether this is a leaf variable (requires grad).
    /// </summary>
    public bool RequiresGrad { get; }

    /// <summary>
    /// Gets whether the gradient has been computed.
    /// </summary>
    public bool HasGradient { get; }
}

/// <summary>
/// A differentiable scalar value with automatic gradient tracking.
/// </summary>
/// <remarks>
/// <para>
/// DualNumber implements forward-mode automatic differentiation for scalar values.
/// Each DualNumber carries both the primal value and its derivative.
/// </para>
/// <para>
/// <strong>Mathematical basis:</strong>
/// Dual numbers extend reals: a + bε where ε² = 0.
/// Arithmetic on dual numbers automatically computes derivatives.
/// </para>
/// </remarks>
public readonly struct DualNumber : IDifferentiable<float>, IEquatable<DualNumber>
{
    /// <summary>
    /// Initializes a new dual number.
    /// </summary>
    /// <param name="value">The primal value.</param>
    /// <param name="derivative">The derivative (default: 0).</param>
    public DualNumber(float value, float derivative = 0f)
    {
        Value = value;
        Derivative = derivative;
    }

    /// <summary>
    /// Gets the primal value.
    /// </summary>
    public float Value { get; }

    /// <summary>
    /// Gets the derivative (dual part).
    /// </summary>
    public float Derivative { get; }

    /// <inheritdoc />
    public float Gradient => Derivative;

    /// <inheritdoc />
    public bool RequiresGrad => true;

    /// <inheritdoc />
    public bool HasGradient => true;

    /// <summary>
    /// Creates a variable (derivative = 1).
    /// </summary>
    public static DualNumber Variable(float value) => new(value, 1f);

    /// <summary>
    /// Creates a constant (derivative = 0).
    /// </summary>
    public static DualNumber Constant(float value) => new(value, 0f);

    // Arithmetic operators with automatic differentiation

    public static DualNumber operator +(DualNumber a, DualNumber b)
        => new(a.Value + b.Value, a.Derivative + b.Derivative);

    public static DualNumber operator -(DualNumber a, DualNumber b)
        => new(a.Value - b.Value, a.Derivative - b.Derivative);

    public static DualNumber operator *(DualNumber a, DualNumber b)
        => new(a.Value * b.Value, a.Derivative * b.Value + a.Value * b.Derivative);

    public static DualNumber operator /(DualNumber a, DualNumber b)
    {
        var denominator = b.Value * b.Value;
        return new(
            a.Value / b.Value,
            (a.Derivative * b.Value - a.Value * b.Derivative) / denominator);
    }

    public static DualNumber operator -(DualNumber a)
        => new(-a.Value, -a.Derivative);

    public static DualNumber operator +(DualNumber a, float b)
        => new(a.Value + b, a.Derivative);

    public static DualNumber operator +(float a, DualNumber b)
        => new(a + b.Value, b.Derivative);

    public static DualNumber operator *(DualNumber a, float b)
        => new(a.Value * b, a.Derivative * b);

    public static DualNumber operator *(float a, DualNumber b)
        => new(a * b.Value, a * b.Derivative);

    // Implicit conversions
    public static implicit operator DualNumber(float value) => Constant(value);

    /// <inheritdoc />
    public bool Equals(DualNumber other)
        => Value == other.Value && Derivative == other.Derivative;

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is DualNumber other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
        => HashCode.Combine(Value, Derivative);

    /// <inheritdoc />
    public override string ToString()
        => $"{Value} + {Derivative}ε";

    public static bool operator ==(DualNumber left, DualNumber right) => left.Equals(right);
    public static bool operator !=(DualNumber left, DualNumber right) => !left.Equals(right);
}

/// <summary>
/// Mathematical functions for dual numbers (forward-mode AD).
/// </summary>
public static class DualMath
{
    /// <summary>
    /// Computes sin(x) with automatic differentiation.
    /// </summary>
    public static DualNumber Sin(DualNumber x)
        => new(MathF.Sin(x.Value), x.Derivative * MathF.Cos(x.Value));

    /// <summary>
    /// Computes cos(x) with automatic differentiation.
    /// </summary>
    public static DualNumber Cos(DualNumber x)
        => new(MathF.Cos(x.Value), -x.Derivative * MathF.Sin(x.Value));

    /// <summary>
    /// Computes tan(x) with automatic differentiation.
    /// </summary>
    public static DualNumber Tan(DualNumber x)
    {
        var cosX = MathF.Cos(x.Value);
        return new(MathF.Tan(x.Value), x.Derivative / (cosX * cosX));
    }

    /// <summary>
    /// Computes exp(x) with automatic differentiation.
    /// </summary>
    public static DualNumber Exp(DualNumber x)
    {
        var expX = MathF.Exp(x.Value);
        return new(expX, x.Derivative * expX);
    }

    /// <summary>
    /// Computes ln(x) with automatic differentiation.
    /// </summary>
    public static DualNumber Log(DualNumber x)
        => new(MathF.Log(x.Value), x.Derivative / x.Value);

    /// <summary>
    /// Computes log10(x) with automatic differentiation.
    /// </summary>
    public static DualNumber Log10(DualNumber x)
        => new(MathF.Log10(x.Value), x.Derivative / (x.Value * MathF.Log(10)));

    /// <summary>
    /// Computes sqrt(x) with automatic differentiation.
    /// </summary>
    public static DualNumber Sqrt(DualNumber x)
    {
        var sqrtX = MathF.Sqrt(x.Value);
        return new(sqrtX, x.Derivative / (2 * sqrtX));
    }

    /// <summary>
    /// Computes x^n with automatic differentiation.
    /// </summary>
    public static DualNumber Pow(DualNumber x, float n)
        => new(MathF.Pow(x.Value, n), x.Derivative * n * MathF.Pow(x.Value, n - 1));

    /// <summary>
    /// Computes x^y with automatic differentiation (both differentiable).
    /// </summary>
    public static DualNumber Pow(DualNumber x, DualNumber y)
    {
        var powXY = MathF.Pow(x.Value, y.Value);
        var derivative = powXY * (y.Derivative * MathF.Log(x.Value) + y.Value * x.Derivative / x.Value);
        return new(powXY, derivative);
    }

    /// <summary>
    /// Computes tanh(x) with automatic differentiation.
    /// </summary>
    public static DualNumber Tanh(DualNumber x)
    {
        var tanhX = MathF.Tanh(x.Value);
        return new(tanhX, x.Derivative * (1 - tanhX * tanhX));
    }

    /// <summary>
    /// Computes sigmoid(x) with automatic differentiation.
    /// </summary>
    public static DualNumber Sigmoid(DualNumber x)
    {
        var sigX = 1f / (1f + MathF.Exp(-x.Value));
        return new(sigX, x.Derivative * sigX * (1 - sigX));
    }

    /// <summary>
    /// Computes ReLU(x) with automatic differentiation.
    /// </summary>
    public static DualNumber ReLU(DualNumber x)
        => x.Value > 0 ? x : new DualNumber(0, 0);

    /// <summary>
    /// Computes LeakyReLU(x) with automatic differentiation.
    /// </summary>
    public static DualNumber LeakyReLU(DualNumber x, float alpha = 0.01f)
        => x.Value > 0 ? x : new DualNumber(alpha * x.Value, alpha * x.Derivative);

    /// <summary>
    /// Computes abs(x) with automatic differentiation.
    /// </summary>
    public static DualNumber Abs(DualNumber x)
        => x.Value >= 0 ? x : new DualNumber(-x.Value, -x.Derivative);

    /// <summary>
    /// Computes max(a, b) with automatic differentiation.
    /// </summary>
    public static DualNumber Max(DualNumber a, DualNumber b)
        => a.Value >= b.Value ? a : b;

    /// <summary>
    /// Computes min(a, b) with automatic differentiation.
    /// </summary>
    public static DualNumber Min(DualNumber a, DualNumber b)
        => a.Value <= b.Value ? a : b;
}

/// <summary>
/// Node in the computational graph for reverse-mode AD.
/// </summary>
public sealed class Variable : IDifferentiable<float>
{
    private readonly List<(Variable Parent, float LocalGradient)> _parents = new();
    private float _gradient;
    private bool _hasGradient;

    /// <summary>
    /// Creates a new variable.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="requiresGrad">Whether gradients should be computed.</param>
    /// <param name="name">Optional name for debugging.</param>
    public Variable(float value, bool requiresGrad = true, string? name = null)
    {
        Value = value;
        RequiresGrad = requiresGrad;
        Name = name;
    }

    /// <inheritdoc />
    public float Value { get; }

    /// <inheritdoc />
    public float Gradient => _gradient;

    /// <inheritdoc />
    public bool RequiresGrad { get; }

    /// <inheritdoc />
    public bool HasGradient => _hasGradient;

    /// <summary>
    /// Gets the optional name for debugging.
    /// </summary>
    public string? Name { get; }

    internal void AddParent(Variable parent, float localGradient)
    {
        if (parent.RequiresGrad)
        {
            _parents.Add((parent, localGradient));
        }
    }

    /// <summary>
    /// Computes gradients via backpropagation.
    /// </summary>
    /// <param name="gradOutput">Gradient from downstream (default: 1.0).</param>
    public void Backward(float gradOutput = 1f)
    {
        // Accumulate gradient
        _gradient += gradOutput;
        _hasGradient = true;

        // Propagate to parents
        foreach (var (parent, localGrad) in _parents)
        {
            parent.Backward(gradOutput * localGrad);
        }
    }

    /// <summary>
    /// Resets gradient to zero.
    /// </summary>
    public void ZeroGrad()
    {
        _gradient = 0f;
        _hasGradient = false;
    }

    /// <inheritdoc />
    public override string ToString()
        => Name != null ? $"{Name}={Value}" : $"Var({Value})";

    // Operator overloads for building computational graph

    public static Variable operator +(Variable a, Variable b)
    {
        var result = new Variable(a.Value + b.Value, a.RequiresGrad || b.RequiresGrad);
        result.AddParent(a, 1f);  // d(a+b)/da = 1
        result.AddParent(b, 1f);  // d(a+b)/db = 1
        return result;
    }

    public static Variable operator -(Variable a, Variable b)
    {
        var result = new Variable(a.Value - b.Value, a.RequiresGrad || b.RequiresGrad);
        result.AddParent(a, 1f);   // d(a-b)/da = 1
        result.AddParent(b, -1f);  // d(a-b)/db = -1
        return result;
    }

    public static Variable operator *(Variable a, Variable b)
    {
        var result = new Variable(a.Value * b.Value, a.RequiresGrad || b.RequiresGrad);
        result.AddParent(a, b.Value);  // d(a*b)/da = b
        result.AddParent(b, a.Value);  // d(a*b)/db = a
        return result;
    }

    public static Variable operator /(Variable a, Variable b)
    {
        var result = new Variable(a.Value / b.Value, a.RequiresGrad || b.RequiresGrad);
        result.AddParent(a, 1f / b.Value);              // d(a/b)/da = 1/b
        result.AddParent(b, -a.Value / (b.Value * b.Value));  // d(a/b)/db = -a/b²
        return result;
    }

    public static Variable operator -(Variable a)
    {
        var result = new Variable(-a.Value, a.RequiresGrad);
        result.AddParent(a, -1f);
        return result;
    }

    public static Variable operator +(Variable a, float b)
    {
        var result = new Variable(a.Value + b, a.RequiresGrad);
        result.AddParent(a, 1f);
        return result;
    }

    public static Variable operator +(float a, Variable b)
        => b + a;

    public static Variable operator *(Variable a, float b)
    {
        var result = new Variable(a.Value * b, a.RequiresGrad);
        result.AddParent(a, b);
        return result;
    }

    public static Variable operator *(float a, Variable b)
        => b * a;
}

/// <summary>
/// Mathematical functions for Variables (reverse-mode AD).
/// </summary>
public static class VariableMath
{
    /// <summary>
    /// Computes sin(x) with gradient tracking.
    /// </summary>
    public static Variable Sin(Variable x)
    {
        var result = new Variable(MathF.Sin(x.Value), x.RequiresGrad);
        result.AddParent(x, MathF.Cos(x.Value));
        return result;
    }

    /// <summary>
    /// Computes cos(x) with gradient tracking.
    /// </summary>
    public static Variable Cos(Variable x)
    {
        var result = new Variable(MathF.Cos(x.Value), x.RequiresGrad);
        result.AddParent(x, -MathF.Sin(x.Value));
        return result;
    }

    /// <summary>
    /// Computes exp(x) with gradient tracking.
    /// </summary>
    public static Variable Exp(Variable x)
    {
        var expX = MathF.Exp(x.Value);
        var result = new Variable(expX, x.RequiresGrad);
        result.AddParent(x, expX);
        return result;
    }

    /// <summary>
    /// Computes ln(x) with gradient tracking.
    /// </summary>
    public static Variable Log(Variable x)
    {
        var result = new Variable(MathF.Log(x.Value), x.RequiresGrad);
        result.AddParent(x, 1f / x.Value);
        return result;
    }

    /// <summary>
    /// Computes sqrt(x) with gradient tracking.
    /// </summary>
    public static Variable Sqrt(Variable x)
    {
        var sqrtX = MathF.Sqrt(x.Value);
        var result = new Variable(sqrtX, x.RequiresGrad);
        result.AddParent(x, 0.5f / sqrtX);
        return result;
    }

    /// <summary>
    /// Computes x^n with gradient tracking.
    /// </summary>
    public static Variable Pow(Variable x, float n)
    {
        var result = new Variable(MathF.Pow(x.Value, n), x.RequiresGrad);
        result.AddParent(x, n * MathF.Pow(x.Value, n - 1));
        return result;
    }

    /// <summary>
    /// Computes tanh(x) with gradient tracking.
    /// </summary>
    public static Variable Tanh(Variable x)
    {
        var tanhX = MathF.Tanh(x.Value);
        var result = new Variable(tanhX, x.RequiresGrad);
        result.AddParent(x, 1 - tanhX * tanhX);
        return result;
    }

    /// <summary>
    /// Computes sigmoid(x) with gradient tracking.
    /// </summary>
    public static Variable Sigmoid(Variable x)
    {
        var sigX = 1f / (1f + MathF.Exp(-x.Value));
        var result = new Variable(sigX, x.RequiresGrad);
        result.AddParent(x, sigX * (1 - sigX));
        return result;
    }

    /// <summary>
    /// Computes ReLU(x) with gradient tracking.
    /// </summary>
    public static Variable ReLU(Variable x)
    {
        var result = new Variable(MathF.Max(0, x.Value), x.RequiresGrad);
        result.AddParent(x, x.Value > 0 ? 1f : 0f);
        return result;
    }

    /// <summary>
    /// Computes LeakyReLU(x) with gradient tracking.
    /// </summary>
    public static Variable LeakyReLU(Variable x, float alpha = 0.01f)
    {
        var value = x.Value > 0 ? x.Value : alpha * x.Value;
        var grad = x.Value > 0 ? 1f : alpha;
        var result = new Variable(value, x.RequiresGrad);
        result.AddParent(x, grad);
        return result;
    }

    /// <summary>
    /// Computes softplus(x) = ln(1 + exp(x)) with gradient tracking.
    /// </summary>
    public static Variable Softplus(Variable x)
    {
        var result = new Variable(MathF.Log(1 + MathF.Exp(x.Value)), x.RequiresGrad);
        result.AddParent(x, 1f / (1f + MathF.Exp(-x.Value)));  // sigmoid(x)
        return result;
    }
}

/// <summary>
/// Gradient tape for recording operations in reverse-mode AD.
/// </summary>
/// <remarks>
/// <para>
/// GradientTape records operations as they occur, building a computational
/// graph that can be differentiated via backpropagation.
/// </para>
/// <para>
/// <strong>Usage pattern:</strong>
/// <code>
/// using var tape = new GradientTape();
/// var x = tape.Variable(3.0f, "x");
/// var y = x * x + 2 * x;  // y = x² + 2x
/// var grads = tape.Gradient(y, x);  // dy/dx = 2x + 2 = 8
/// </code>
/// </para>
/// </remarks>
public sealed class GradientTape : IDisposable
{
    private readonly List<Variable> _watchedVariables = new();
    private readonly ConcurrentDictionary<string, Variable> _namedVariables = new();
    private bool _disposed;

    /// <summary>
    /// Creates a watched variable.
    /// </summary>
    /// <param name="value">The initial value.</param>
    /// <param name="name">Optional name for the variable.</param>
    /// <returns>A variable that tracks gradients.</returns>
    public Variable Variable(float value, string? name = null)
    {
        var variable = new Variable(value, requiresGrad: true, name: name);
        _watchedVariables.Add(variable);
        if (name != null)
        {
            _namedVariables[name] = variable;
        }
        return variable;
    }

    /// <summary>
    /// Gets a named variable.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <returns>The variable, or null if not found.</returns>
    public Variable? GetVariable(string name)
        => _namedVariables.TryGetValue(name, out var v) ? v : null;

    /// <summary>
    /// Computes gradients of target with respect to sources.
    /// </summary>
    /// <param name="target">The output variable to differentiate.</param>
    /// <param name="sources">The input variables to compute gradients for.</param>
    /// <returns>Dictionary mapping variables to their gradients.</returns>
    public IReadOnlyDictionary<Variable, float> Gradient(Variable target, params Variable[] sources)
    {
        // Zero all gradients first
        foreach (var v in _watchedVariables)
        {
            v.ZeroGrad();
        }

        // Run backpropagation
        target.Backward(1f);

        // Collect gradients for requested sources
        var result = new Dictionary<Variable, float>();
        foreach (var source in sources)
        {
            result[source] = source.Gradient;
        }
        return result;
    }

    /// <summary>
    /// Computes gradient of target with respect to a single source.
    /// </summary>
    public float Gradient(Variable target, Variable source)
    {
        foreach (var v in _watchedVariables)
        {
            v.ZeroGrad();
        }
        target.Backward(1f);
        return source.Gradient;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _watchedVariables.Clear();
        _namedVariables.Clear();
    }
}
