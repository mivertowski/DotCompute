// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Linq.Operators.Adapters;
{
/// <summary>
/// Adapter to convert LINQ IKernel to Core IKernel.
/// </summary>
internal class CoreKernelAdapter : DotCompute.Abstractions.IKernel
{
    private readonly Interfaces.IKernel _linqKernel;
    public CoreKernelAdapter(Interfaces.IKernel linqKernel)
    {
        _linqKernel = linqKernel ?? throw new ArgumentNullException(nameof(linqKernel));
    public int RequiredSharedMemory => _linqKernel.Properties.SharedMemorySize;
