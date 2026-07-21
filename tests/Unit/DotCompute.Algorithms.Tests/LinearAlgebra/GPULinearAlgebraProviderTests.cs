// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Algorithms.LinearAlgebra;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace DotCompute.Algorithms.Tests.LinearAlgebra;

/// <summary>
/// Regression coverage for GH #182 (linear-algebra API usability):
/// <list type="bullet">
///   <item><description><see cref="GPULinearAlgebraProvider"/> must be constructible. It used to
///   require an <c>IKernelManager</c> — an interface with no implementation anywhere — which made
///   the type (and the documented example) impossible to use.</description></item>
///   <item><description>Its SVD must be mathematically correct. The former "simplified A^T*A"
///   fallback returned unsorted singular values and a factorization that did not reconstruct the
///   input at all.</description></item>
/// </list>
/// </summary>
public sealed class GPULinearAlgebraProviderTests
{
    private static IAccelerator CreateCpuAccelerator()
    {
        var accelerator = Substitute.For<IAccelerator>();
        accelerator.Info.Returns(new AcceleratorInfo
        {
            DeviceType = "CPU",
            Name = "Mock CPU",
            Id = "mock-cpu-0",
            MaxComputeUnits = 8
        });
        return accelerator;
    }

    private static Matrix CreateTestMatrix(int n, int seed = 42)
    {
        var rng = new Random(seed);
        var matrix = new Matrix(n, n);
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                matrix[i, j] = (float)((rng.NextDouble() * 4.0) - 2.0);
            }
        }

        return matrix;
    }

    /// <summary>Largest |U*S*V^T - A| element, i.e. how well the factorization reproduces the input.</summary>
    private static double ReconstructionError(Matrix a, Matrix u, Matrix s, Matrix vt)
    {
        var n = a.Rows;
        var maxError = 0.0;
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                var sum = 0.0;
                for (var k = 0; k < n; k++)
                {
                    sum += u[i, k] * s[k, k] * vt[k, j];
                }

                maxError = Math.Max(maxError, Math.Abs(sum - a[i, j]));
            }
        }

        return maxError;
    }

    [Fact]
    public void Provider_IsConstructible_WithoutAKernelManager()
    {
        // The documented usage must actually compile and run. No IKernelManager implementation
        // ships, so requiring one made this type unconstructible.
        using var provider = new GPULinearAlgebraProvider(NullLogger<GPULinearAlgebraProvider>.Instance);

        Assert.NotNull(provider);
    }

    [Fact]
    public void Provider_ResolvesFromDependencyInjection()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddDotComputeAlgorithms();
        using var serviceProvider = services.BuildServiceProvider();

        using var provider = serviceProvider.GetRequiredService<GPULinearAlgebraProvider>();

        Assert.NotNull(provider);
    }

    [Fact]
    public async Task Provider_SVD_ReconstructsTheInputMatrix()
    {
        using var provider = new GPULinearAlgebraProvider(NullLogger<GPULinearAlgebraProvider>.Instance);
        var matrix = CreateTestMatrix(6);

        var (u, s, vt) = await provider.SVDAsync(matrix, CreateCpuAccelerator());

        Assert.True(ReconstructionError(matrix, u, s, vt) < 1e-3,
            "U*S*V^T must reproduce the input matrix; the old simplified fallback did not.");
    }

    [Fact]
    public async Task Provider_SVD_ProducesDescendingNonNegativeSingularValues()
    {
        using var provider = new GPULinearAlgebraProvider(NullLogger<GPULinearAlgebraProvider>.Instance);
        var matrix = CreateTestMatrix(6);

        var (_, s, _) = await provider.SVDAsync(matrix, CreateCpuAccelerator());

        for (var k = 0; k < s.Rows; k++)
        {
            Assert.True(s[k, k] >= 0, "singular values must be non-negative");
            if (k > 0)
            {
                Assert.True(s[k - 1, k - 1] >= s[k, k], "singular values must be in descending order");
            }
        }
    }

    [Fact]
    public async Task Provider_And_MatrixMath_AgreeOnSVD()
    {
        // Both documented entry points must share the same (correct) implementation.
        using var provider = new GPULinearAlgebraProvider(NullLogger<GPULinearAlgebraProvider>.Instance);
        var matrix = CreateTestMatrix(6);
        var accelerator = CreateCpuAccelerator();

        var (_, providerS, _) = await provider.SVDAsync(matrix, accelerator);
        var (_, mathS, _) = await MatrixMath.SVDAsync(matrix, accelerator);

        for (var k = 0; k < providerS.Rows; k++)
        {
            Assert.Equal(mathS[k, k], providerS[k, k], 3);
        }
    }
}
