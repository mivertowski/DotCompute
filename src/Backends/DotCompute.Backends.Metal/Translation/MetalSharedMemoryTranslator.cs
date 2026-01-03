// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DotCompute.Backends.Metal.Translation;

/// <summary>
/// Translates C# shared memory declarations and accesses to Metal threadgroup memory.
/// Handles the [SharedMemory] attribute and Kernel.SharedMemory&lt;T&gt;(name) calls.
/// </summary>
/// <remarks>
/// <para>
/// Metal uses threadgroup memory for fast on-chip shared storage accessible by all
/// threads in a threadgroup. This translator handles:
/// </para>
/// <list type="bullet">
/// <item><description>Parsing [SharedMemory] attribute declarations</description></item>
/// <item><description>Generating threadgroup parameter declarations</description></item>
/// <item><description>Translating Kernel.SharedMemory access patterns</description></item>
/// <item><description>Managing threadgroup binding indices</description></item>
/// </list>
/// </remarks>
public sealed class MetalSharedMemoryTranslator
{
    /// <summary>
    /// Represents a shared memory declaration extracted from attributes.
    /// </summary>
    public sealed record SharedMemoryDeclaration
    {
        /// <summary>Gets the name of the shared memory block.</summary>
        public required string Name { get; init; }

        /// <summary>Gets the element type (e.g., "float", "int").</summary>
        public required string ElementType { get; init; }

        /// <summary>Gets the size in elements.</summary>
        public required int Size { get; init; }

        /// <summary>Gets the alignment requirement in bytes (0 for natural).</summary>
        public int Alignment { get; init; }

        /// <summary>Gets whether to zero-initialize the memory.</summary>
        public bool ZeroInitialize { get; init; }

        /// <summary>Gets the Metal binding index (-1 for auto).</summary>
        public int BindingIndex { get; init; } = -1;
    }

    /// <summary>
    /// Regex to match [SharedMemory(...)] attribute declarations.
    /// </summary>
    private static readonly Regex SharedMemoryAttributePattern = new(
        @"\[SharedMemory\s*\(\s*typeof\s*\(\s*(\w+)\s*\)\s*,\s*""(\w+)""(?:\s*,\s*Size\s*=\s*(\d+))?(?:\s*,\s*Alignment\s*=\s*(\d+))?(?:\s*,\s*ZeroInitialize\s*=\s*(true|false))?(?:\s*,\s*MetalBindingIndex\s*=\s*(\d+))?\s*\)\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Regex to match Kernel.SharedMemory&lt;T&gt;("name") access patterns.
    /// </summary>
    private static readonly Regex SharedMemoryAccessPattern = new(
        @"Kernel\.SharedMemory<(\w+)>\s*\(\s*""(\w+)""\s*\)",
        RegexOptions.Compiled);

    /// <summary>
    /// Regex to match Kernel.Barrier() calls that need synchronization.
    /// </summary>
    private static readonly Regex BarrierPattern = new(
        @"Kernel\.Barrier\s*\(\s*\)",
        RegexOptions.Compiled);

    /// <summary>
    /// Extracts shared memory declarations from C# source code with [SharedMemory] attributes.
    /// </summary>
    /// <param name="csharpSource">The C# source code containing [SharedMemory] attributes.</param>
    /// <returns>A list of shared memory declarations found in the source.</returns>
    public List<SharedMemoryDeclaration> ExtractDeclarations(string csharpSource)
    {
        ArgumentNullException.ThrowIfNull(csharpSource);

        var declarations = new List<SharedMemoryDeclaration>();
        var matches = SharedMemoryAttributePattern.Matches(csharpSource);

        foreach (Match match in matches)
        {
            var elementType = match.Groups[1].Value;
            var name = match.Groups[2].Value;
            var size = match.Groups[3].Success ? int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture) : 256;
            var alignment = match.Groups[4].Success ? int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture) : 0;
            var zeroInit = match.Groups[5].Success && bool.Parse(match.Groups[5].Value);
            var bindingIndex = match.Groups[6].Success ? int.Parse(match.Groups[6].Value, CultureInfo.InvariantCulture) : -1;

            declarations.Add(new SharedMemoryDeclaration
            {
                Name = name,
                ElementType = TranslateToCType(elementType),
                Size = size,
                Alignment = alignment,
                ZeroInitialize = zeroInit,
                BindingIndex = bindingIndex
            });
        }

        return declarations;
    }

    /// <summary>
    /// Generates Metal threadgroup memory parameter declarations.
    /// </summary>
    /// <param name="declarations">The shared memory declarations to generate.</param>
    /// <param name="startingBufferIndex">The buffer index to start threadgroup bindings from.</param>
    /// <returns>MSL parameter declarations for the kernel signature.</returns>
    public string GenerateThreadgroupParameters(
        IReadOnlyList<SharedMemoryDeclaration> declarations,
        int startingBufferIndex)
    {
        ArgumentNullException.ThrowIfNull(declarations);

        if (declarations.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        var currentIndex = startingBufferIndex;

        foreach (var decl in declarations)
        {
            var bindingIndex = decl.BindingIndex >= 0 ? decl.BindingIndex : currentIndex++;

            sb.Append("    threadgroup ");
            sb.Append(decl.ElementType);
            sb.Append("* ");
            sb.Append(decl.Name);
            sb.Append(" [[threadgroup(");
            sb.Append(bindingIndex.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine(")]],");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates threadgroup memory size calculations for kernel dispatch.
    /// </summary>
    /// <param name="declarations">The shared memory declarations.</param>
    /// <returns>Total threadgroup memory size in bytes.</returns>
    public int CalculateThreadgroupMemorySize(IReadOnlyList<SharedMemoryDeclaration> declarations)
    {
        ArgumentNullException.ThrowIfNull(declarations);

        var totalSize = 0;

        foreach (var decl in declarations)
        {
            var elementSize = GetElementSize(decl.ElementType);
            var blockSize = decl.Size * elementSize;

            // Apply alignment if specified
            if (decl.Alignment > 0)
            {
                blockSize = ((blockSize + decl.Alignment - 1) / decl.Alignment) * decl.Alignment;
            }

            totalSize += blockSize;
        }

        return totalSize;
    }

    /// <summary>
    /// Translates Kernel.SharedMemory&lt;T&gt;("name") access patterns in MSL code.
    /// </summary>
    /// <param name="mslCode">The MSL code containing C# shared memory access patterns.</param>
    /// <returns>MSL code with translated shared memory accesses.</returns>
    public string TranslateSharedMemoryAccess(string mslCode)
    {
        ArgumentNullException.ThrowIfNull(mslCode);

        // Replace Kernel.SharedMemory<T>("name") with just 'name'
        // The actual shared memory array is passed as a threadgroup parameter
        var result = SharedMemoryAccessPattern.Replace(mslCode, "$2");

        // Translate Kernel.Barrier() to threadgroup_barrier
        result = BarrierPattern.Replace(result, "threadgroup_barrier(mem_flags::mem_threadgroup)");

        return result;
    }

    /// <summary>
    /// Generates MSL initialization code for zero-initialized shared memory.
    /// </summary>
    /// <param name="declarations">The shared memory declarations.</param>
    /// <returns>MSL code to zero-initialize the shared memory.</returns>
    public string GenerateInitializationCode(IReadOnlyList<SharedMemoryDeclaration> declarations)
    {
        ArgumentNullException.ThrowIfNull(declarations);

        var sb = new StringBuilder();
        var hasZeroInit = declarations.Any(d => d.ZeroInitialize);

        if (!hasZeroInit)
            return string.Empty;

        sb.AppendLine("    // Zero-initialize shared memory");
        sb.AppendLine("    uint tid = thread_position_in_grid.x;");

        foreach (var decl in declarations.Where(d => d.ZeroInitialize))
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"    if (tid < {decl.Size}u) {decl.Name}[tid] = 0;");
        }

        sb.AppendLine("    threadgroup_barrier(mem_flags::mem_threadgroup);");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Translates a C# type name to Metal type name.
    /// </summary>
    private static string TranslateToCType(string csharpType)
    {
        return csharpType.ToLowerInvariant() switch
        {
            "single" or "float" => "float",
            "double" => "float", // Metal prefers float over double
            "int32" or "int" => "int",
            "uint32" or "uint" => "uint",
            "int16" or "short" => "short",
            "uint16" or "ushort" => "ushort",
            "int64" or "long" => "long",
            "uint64" or "ulong" => "ulong",
            "sbyte" or "int8" => "char",
            "byte" or "uint8" => "uchar",
            "half" => "half",
            "bool" or "boolean" => "bool",
            _ => csharpType.ToLowerInvariant()
        };
    }

    /// <summary>
    /// Gets the size in bytes for a Metal type.
    /// </summary>
    private static int GetElementSize(string metalType)
    {
        return metalType switch
        {
            "float" => 4,
            "double" => 8,
            "int" => 4,
            "uint" => 4,
            "short" => 2,
            "ushort" => 2,
            "long" => 8,
            "ulong" => 8,
            "char" => 1,
            "uchar" => 1,
            "half" => 2,
            "bool" => 1,
            "float2" => 8,
            "float3" => 16, // Aligned to 16
            "float4" => 16,
            "int2" => 8,
            "int3" => 16,
            "int4" => 16,
            _ => 4 // Default assumption
        };
    }
}
