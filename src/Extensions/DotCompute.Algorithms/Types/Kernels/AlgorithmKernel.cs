
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Types.Kernels;


/// <summary>
/// Base class for algorithm kernels.
/// </summary>
public abstract class AlgorithmKernel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>
    public abstract string Name { get; }
    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    /// <value>The description.</value>
    public abstract string Description { get; }
    /// <summary>
    /// Gets or sets a value indicating whether vectorized.
    /// </summary>
    /// <value>The is vectorized.</value>
    public virtual bool IsVectorized => false;
}

