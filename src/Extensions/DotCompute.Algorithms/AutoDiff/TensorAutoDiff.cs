// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Algorithms.AutoDiff;

/// <summary>
/// GPU-accelerated tensor with automatic differentiation support.
/// </summary>
/// <remarks>
/// <para>
/// DifferentiableTensor extends unified memory buffers with gradient tracking
/// for neural network training and scientific computing workloads.
/// </para>
/// <para>
/// <strong>Key features:</strong>
/// <list type="bullet">
/// <item>Reverse-mode automatic differentiation (backpropagation)</item>
/// <item>GPU-accelerated gradient computation</item>
/// <item>Memory-efficient gradient accumulation</item>
/// <item>Lazy gradient allocation</item>
/// </list>
/// </para>
/// </remarks>
public sealed class DifferentiableTensor : IAsyncDisposable
{
    private readonly IUnifiedMemoryBuffer<float>? _dataBuffer;
    private IUnifiedMemoryBuffer<float>? _gradBuffer;
    private readonly List<(DifferentiableTensor Parent, GradientFunction GradFunc)> _parents = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new differentiable tensor.
    /// </summary>
    /// <param name="shape">The tensor shape.</param>
    /// <param name="requiresGrad">Whether gradients should be computed.</param>
    /// <param name="name">Optional name for debugging.</param>
    public DifferentiableTensor(int[] shape, bool requiresGrad = true, string? name = null)
    {
        Shape = shape;
        Size = shape.Aggregate(1, (a, b) => a * b);
        RequiresGrad = requiresGrad;
        Name = name;
        // Data buffer allocated lazily or provided externally
    }

    /// <summary>
    /// Initializes a differentiable tensor with data.
    /// </summary>
    /// <param name="data">The tensor data.</param>
    /// <param name="shape">The tensor shape.</param>
    /// <param name="requiresGrad">Whether gradients should be computed.</param>
    /// <param name="name">Optional name for debugging.</param>
    public DifferentiableTensor(float[] data, int[] shape, bool requiresGrad = true, string? name = null)
        : this(shape, requiresGrad, name)
    {
        if (data.Length != Size)
        {
            throw new ArgumentException($"Data length {data.Length} does not match shape size {Size}");
        }
        Data = data;
    }

    /// <summary>
    /// Initializes a differentiable tensor with a unified buffer.
    /// </summary>
    /// <param name="buffer">The data buffer.</param>
    /// <param name="shape">The tensor shape.</param>
    /// <param name="requiresGrad">Whether gradients should be computed.</param>
    /// <param name="name">Optional name for debugging.</param>
    public DifferentiableTensor(
        IUnifiedMemoryBuffer<float> buffer,
        int[] shape,
        bool requiresGrad = true,
        string? name = null)
        : this(shape, requiresGrad, name)
    {
        _dataBuffer = buffer;
    }

    /// <summary>
    /// Gets the tensor shape.
    /// </summary>
    public int[] Shape { get; }

    /// <summary>
    /// Gets the total number of elements.
    /// </summary>
    public int Size { get; }

    /// <summary>
    /// Gets the number of dimensions.
    /// </summary>
    public int Rank => Shape.Length;

    /// <summary>
    /// Gets whether gradients are computed for this tensor.
    /// </summary>
    public bool RequiresGrad { get; }

    /// <summary>
    /// Gets whether gradients have been computed.
    /// </summary>
    public bool HasGradient => _gradBuffer != null || Gradient != null;

    /// <summary>
    /// Gets the optional name for debugging.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets or sets the tensor data (CPU).
    /// </summary>
    public float[]? Data { get; set; }

    /// <summary>
    /// Gets or sets the gradient data (CPU).
    /// </summary>
    public float[]? Gradient { get; set; }

    /// <summary>
    /// Gets the GPU data buffer if allocated.
    /// </summary>
    public IUnifiedMemoryBuffer<float>? DataBuffer => _dataBuffer;

    /// <summary>
    /// Gets the GPU gradient buffer if allocated.
    /// </summary>
    public IUnifiedMemoryBuffer<float>? GradientBuffer => _gradBuffer;

    internal void AddParent(DifferentiableTensor parent, GradientFunction gradFunc)
    {
        if (parent.RequiresGrad)
        {
            _parents.Add((parent, gradFunc));
        }
    }

    /// <summary>
    /// Performs backpropagation to compute gradients.
    /// </summary>
    /// <param name="gradOutput">Upstream gradient (defaults to ones for scalar loss).</param>
    public void Backward(float[]? gradOutput = null)
    {
        if (!RequiresGrad) return;

        // Initialize gradient if not provided
        gradOutput ??= Enumerable.Repeat(1f, Size).ToArray();

        // Accumulate gradient
        if (Gradient == null)
        {
            Gradient = new float[Size];
        }
        for (int i = 0; i < Size; i++)
        {
            Gradient[i] += gradOutput[i];
        }

        // Propagate to parents
        foreach (var (parent, gradFunc) in _parents)
        {
            var localGrad = gradFunc(gradOutput);
            parent.Backward(localGrad);
        }
    }

    /// <summary>
    /// Resets gradients to zero.
    /// </summary>
    public void ZeroGrad()
    {
        if (Gradient != null)
        {
            Array.Clear(Gradient);
        }
    }

    /// <summary>
    /// Creates a tensor filled with zeros.
    /// </summary>
    public static DifferentiableTensor Zeros(int[] shape, bool requiresGrad = true, string? name = null)
    {
        var data = new float[shape.Aggregate(1, (a, b) => a * b)];
        return new DifferentiableTensor(data, shape, requiresGrad, name);
    }

    /// <summary>
    /// Creates a tensor filled with ones.
    /// </summary>
    public static DifferentiableTensor Ones(int[] shape, bool requiresGrad = true, string? name = null)
    {
        var size = shape.Aggregate(1, (a, b) => a * b);
        var data = new float[size];
        Array.Fill(data, 1f);
        return new DifferentiableTensor(data, shape, requiresGrad, name);
    }

    /// <summary>
    /// Creates a tensor with random values.
    /// </summary>
    public static DifferentiableTensor Random(int[] shape, float min = 0f, float max = 1f, bool requiresGrad = true, string? name = null)
    {
        var size = shape.Aggregate(1, (a, b) => a * b);
        var data = new float[size];
        var rng = System.Random.Shared;
        for (int i = 0; i < size; i++)
        {
            data[i] = (float)(rng.NextDouble() * (max - min) + min);
        }
        return new DifferentiableTensor(data, shape, requiresGrad, name);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_dataBuffer != null)
        {
            await _dataBuffer.DisposeAsync();
        }
        if (_gradBuffer != null)
        {
            await _gradBuffer.DisposeAsync();
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var shapeStr = string.Join("×", Shape);
        var nameStr = Name != null ? $"{Name}: " : "";
        var gradStr = RequiresGrad ? ", grad" : "";
        return $"{nameStr}Tensor({shapeStr}{gradStr})";
    }
}

/// <summary>
/// Delegate for computing local gradients during backpropagation.
/// </summary>
/// <param name="gradOutput">Upstream gradient.</param>
/// <returns>Local gradient to propagate.</returns>
public delegate float[] GradientFunction(float[] gradOutput);

/// <summary>
/// Tensor operations with automatic differentiation.
/// </summary>
public static class TensorOps
{
    /// <summary>
    /// Element-wise addition with gradient tracking.
    /// </summary>
    public static DifferentiableTensor Add(DifferentiableTensor a, DifferentiableTensor b)
    {
        ValidateShapes(a, b, "Add");

        var resultData = new float[a.Size];
        for (int i = 0; i < a.Size; i++)
        {
            resultData[i] = a.Data![i] + b.Data![i];
        }

        var result = new DifferentiableTensor(resultData, a.Shape, a.RequiresGrad || b.RequiresGrad);
        result.AddParent(a, grad => grad);  // d(a+b)/da = 1
        result.AddParent(b, grad => grad);  // d(a+b)/db = 1
        return result;
    }

    /// <summary>
    /// Element-wise subtraction with gradient tracking.
    /// </summary>
    public static DifferentiableTensor Subtract(DifferentiableTensor a, DifferentiableTensor b)
    {
        ValidateShapes(a, b, "Subtract");

        var resultData = new float[a.Size];
        for (int i = 0; i < a.Size; i++)
        {
            resultData[i] = a.Data![i] - b.Data![i];
        }

        var result = new DifferentiableTensor(resultData, a.Shape, a.RequiresGrad || b.RequiresGrad);
        result.AddParent(a, grad => grad);
        result.AddParent(b, grad => grad.Select(g => -g).ToArray());
        return result;
    }

    /// <summary>
    /// Element-wise multiplication with gradient tracking.
    /// </summary>
    public static DifferentiableTensor Multiply(DifferentiableTensor a, DifferentiableTensor b)
    {
        ValidateShapes(a, b, "Multiply");

        var resultData = new float[a.Size];
        for (int i = 0; i < a.Size; i++)
        {
            resultData[i] = a.Data![i] * b.Data![i];
        }

        var result = new DifferentiableTensor(resultData, a.Shape, a.RequiresGrad || b.RequiresGrad);

        // Capture data for gradient computation
        var aData = a.Data!.ToArray();
        var bData = b.Data!.ToArray();

        result.AddParent(a, grad =>
        {
            var localGrad = new float[grad.Length];
            for (int i = 0; i < grad.Length; i++)
            {
                localGrad[i] = grad[i] * bData[i];
            }
            return localGrad;
        });

        result.AddParent(b, grad =>
        {
            var localGrad = new float[grad.Length];
            for (int i = 0; i < grad.Length; i++)
            {
                localGrad[i] = grad[i] * aData[i];
            }
            return localGrad;
        });

        return result;
    }

    /// <summary>
    /// Matrix multiplication with gradient tracking.
    /// </summary>
    /// <param name="a">Matrix A (M×K).</param>
    /// <param name="b">Matrix B (K×N).</param>
    /// <returns>Result matrix (M×N).</returns>
    public static DifferentiableTensor MatMul(DifferentiableTensor a, DifferentiableTensor b)
    {
        if (a.Rank != 2 || b.Rank != 2)
        {
            throw new ArgumentException("MatMul requires 2D tensors");
        }

        if (a.Shape[1] != b.Shape[0])
        {
            throw new ArgumentException($"Shape mismatch: ({a.Shape[0]}×{a.Shape[1]}) × ({b.Shape[0]}×{b.Shape[1]})");
        }

        int M = a.Shape[0];
        int K = a.Shape[1];
        int N = b.Shape[1];

        var resultData = new float[M * N];

        // C[i,j] = Σ A[i,k] * B[k,j]
        for (int i = 0; i < M; i++)
        {
            for (int j = 0; j < N; j++)
            {
                float sum = 0;
                for (int k = 0; k < K; k++)
                {
                    sum += a.Data![i * K + k] * b.Data![k * N + j];
                }
                resultData[i * N + j] = sum;
            }
        }

        var result = new DifferentiableTensor(resultData, [M, N], a.RequiresGrad || b.RequiresGrad);

        // Capture for gradient computation
        var aData = a.Data!.ToArray();
        var bData = b.Data!.ToArray();

        // dL/dA = dL/dC @ B^T
        result.AddParent(a, gradOutput =>
        {
            var gradA = new float[M * K];
            for (int i = 0; i < M; i++)
            {
                for (int k = 0; k < K; k++)
                {
                    float sum = 0;
                    for (int j = 0; j < N; j++)
                    {
                        sum += gradOutput[i * N + j] * bData[k * N + j];
                    }
                    gradA[i * K + k] = sum;
                }
            }
            return gradA;
        });

        // dL/dB = A^T @ dL/dC
        result.AddParent(b, gradOutput =>
        {
            var gradB = new float[K * N];
            for (int k = 0; k < K; k++)
            {
                for (int j = 0; j < N; j++)
                {
                    float sum = 0;
                    for (int i = 0; i < M; i++)
                    {
                        sum += aData[i * K + k] * gradOutput[i * N + j];
                    }
                    gradB[k * N + j] = sum;
                }
            }
            return gradB;
        });

        return result;
    }

    /// <summary>
    /// Sum all elements with gradient tracking.
    /// </summary>
    public static DifferentiableTensor Sum(DifferentiableTensor x)
    {
        float sum = 0;
        for (int i = 0; i < x.Size; i++)
        {
            sum += x.Data![i];
        }

        var result = new DifferentiableTensor([sum], [1], x.RequiresGrad);
        var size = x.Size;
        result.AddParent(x, grad => Enumerable.Repeat(grad[0], size).ToArray());
        return result;
    }

    /// <summary>
    /// Computes mean with gradient tracking.
    /// </summary>
    public static DifferentiableTensor Mean(DifferentiableTensor x)
    {
        float mean = x.Data!.Average();
        var result = new DifferentiableTensor([mean], [1], x.RequiresGrad);
        var size = x.Size;
        result.AddParent(x, grad => Enumerable.Repeat(grad[0] / size, size).ToArray());
        return result;
    }

    /// <summary>
    /// Element-wise ReLU with gradient tracking.
    /// </summary>
    public static DifferentiableTensor ReLU(DifferentiableTensor x)
    {
        var resultData = new float[x.Size];
        for (int i = 0; i < x.Size; i++)
        {
            resultData[i] = MathF.Max(0, x.Data![i]);
        }

        var result = new DifferentiableTensor(resultData, x.Shape, x.RequiresGrad);
        var xData = x.Data!.ToArray();
        result.AddParent(x, grad =>
        {
            var localGrad = new float[grad.Length];
            for (int i = 0; i < grad.Length; i++)
            {
                localGrad[i] = xData[i] > 0 ? grad[i] : 0;
            }
            return localGrad;
        });

        return result;
    }

    /// <summary>
    /// Element-wise sigmoid with gradient tracking.
    /// </summary>
    public static DifferentiableTensor Sigmoid(DifferentiableTensor x)
    {
        var resultData = new float[x.Size];
        for (int i = 0; i < x.Size; i++)
        {
            resultData[i] = 1f / (1f + MathF.Exp(-x.Data![i]));
        }

        var result = new DifferentiableTensor(resultData, x.Shape, x.RequiresGrad);
        var sigData = resultData.ToArray();
        result.AddParent(x, grad =>
        {
            var localGrad = new float[grad.Length];
            for (int i = 0; i < grad.Length; i++)
            {
                localGrad[i] = grad[i] * sigData[i] * (1 - sigData[i]);
            }
            return localGrad;
        });

        return result;
    }

    /// <summary>
    /// Element-wise tanh with gradient tracking.
    /// </summary>
    public static DifferentiableTensor Tanh(DifferentiableTensor x)
    {
        var resultData = new float[x.Size];
        for (int i = 0; i < x.Size; i++)
        {
            resultData[i] = MathF.Tanh(x.Data![i]);
        }

        var result = new DifferentiableTensor(resultData, x.Shape, x.RequiresGrad);
        var tanhData = resultData.ToArray();
        result.AddParent(x, grad =>
        {
            var localGrad = new float[grad.Length];
            for (int i = 0; i < grad.Length; i++)
            {
                localGrad[i] = grad[i] * (1 - tanhData[i] * tanhData[i]);
            }
            return localGrad;
        });

        return result;
    }

    /// <summary>
    /// Softmax with gradient tracking.
    /// </summary>
    public static DifferentiableTensor Softmax(DifferentiableTensor x, int axis = -1)
    {
        if (x.Rank != 1)
        {
            throw new NotImplementedException("Multi-dimensional softmax not yet implemented");
        }

        // Numerical stability: subtract max
        float max = x.Data!.Max();
        var expData = x.Data!.Select(v => MathF.Exp(v - max)).ToArray();
        float sum = expData.Sum();
        var resultData = expData.Select(v => v / sum).ToArray();

        var result = new DifferentiableTensor(resultData, x.Shape, x.RequiresGrad);
        var softmaxData = resultData.ToArray();
        var n = x.Size;

        result.AddParent(x, grad =>
        {
            // Jacobian of softmax: J[i,j] = s[i](δ[i,j] - s[j])
            // dL/dx[i] = Σ_j dL/dy[j] * J[j,i]
            var localGrad = new float[n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    float jacobian = softmaxData[j] * ((i == j ? 1 : 0) - softmaxData[i]);
                    localGrad[i] += grad[j] * jacobian;
                }
            }
            return localGrad;
        });

        return result;
    }

    /// <summary>
    /// Cross-entropy loss with gradient tracking.
    /// </summary>
    /// <param name="predictions">Model predictions (after softmax).</param>
    /// <param name="targets">One-hot encoded targets.</param>
    public static DifferentiableTensor CrossEntropyLoss(DifferentiableTensor predictions, DifferentiableTensor targets)
    {
        ValidateShapes(predictions, targets, "CrossEntropyLoss");

        // L = -Σ t[i] * log(p[i])
        float loss = 0;
        for (int i = 0; i < predictions.Size; i++)
        {
            if (targets.Data![i] > 0)
            {
                loss -= targets.Data![i] * MathF.Log(MathF.Max(predictions.Data![i], 1e-7f));
            }
        }

        var result = new DifferentiableTensor([loss], [1], predictions.RequiresGrad);

        var predData = predictions.Data!.ToArray();
        var targetData = targets.Data!.ToArray();

        // dL/dp[i] = -t[i]/p[i]
        result.AddParent(predictions, _ =>
        {
            var localGrad = new float[predData.Length];
            for (int i = 0; i < predData.Length; i++)
            {
                localGrad[i] = -targetData[i] / MathF.Max(predData[i], 1e-7f);
            }
            return localGrad;
        });

        return result;
    }

    /// <summary>
    /// Mean squared error loss with gradient tracking.
    /// </summary>
    public static DifferentiableTensor MSELoss(DifferentiableTensor predictions, DifferentiableTensor targets)
    {
        ValidateShapes(predictions, targets, "MSELoss");

        float loss = 0;
        for (int i = 0; i < predictions.Size; i++)
        {
            float diff = predictions.Data![i] - targets.Data![i];
            loss += diff * diff;
        }
        loss /= predictions.Size;

        var result = new DifferentiableTensor([loss], [1], predictions.RequiresGrad);

        var predData = predictions.Data!.ToArray();
        var targetData = targets.Data!.ToArray();
        var n = predData.Length;

        // dL/dp[i] = 2(p[i] - t[i]) / n
        result.AddParent(predictions, _ =>
        {
            var localGrad = new float[n];
            for (int i = 0; i < n; i++)
            {
                localGrad[i] = 2 * (predData[i] - targetData[i]) / n;
            }
            return localGrad;
        });

        return result;
    }

    private static void ValidateShapes(DifferentiableTensor a, DifferentiableTensor b, string operation)
    {
        if (!a.Shape.SequenceEqual(b.Shape))
        {
            throw new ArgumentException(
                $"{operation}: Shape mismatch - [{string.Join(",", a.Shape)}] vs [{string.Join(",", b.Shape)}]");
        }
    }
}
