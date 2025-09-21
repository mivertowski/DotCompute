// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading.Tasks;
namespace DotCompute.Linq.Pipelines.Interfaces.Compute
{
    /// <summary>
    /// Interface for compute device (bridges to Core interface).
    /// </summary>
    public interface IComputeDevice : IDisposable
    {
        /// <summary>Device name.</summary>
        string Name { get; }
        /// <summary>Device type.</summary>
        string Type { get; }
        /// <summary>Whether device is available.</summary>
        bool IsAvailable { get; }
        /// <summary>Initialize the device.</summary>
        Task InitializeAsync();
    }
}
