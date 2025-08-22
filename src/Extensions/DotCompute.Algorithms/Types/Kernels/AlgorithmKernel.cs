// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Types.Kernels;


/// <summary>
/// Base class for algorithm kernels.
/// </summary>
public abstract class AlgorithmKernel
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public virtual bool IsVectorized => false;
}

