// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Models
{

    /// <summary>
    /// Represents a parameter for a kernel method, including its name, type, and buffer status.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="KernelParameter"/> class.
    /// </remarks>
    /// <param name="name">The parameter name.</param>
    /// <param name="type">The parameter type as a string representation.</param>
    /// <param name="isBuffer">Indicates whether this parameter represents a buffer.</param>
    public class KernelParameter(string name, string type, bool isBuffer)
    {
        /// <summary>
        /// Gets the parameter name.
        /// </summary>
        public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

        /// <summary>
        /// Gets the parameter type as a string representation.
        /// </summary>
        public string Type { get; } = type ?? throw new ArgumentNullException(nameof(type));

        /// <summary>
        /// Gets a value indicating whether this parameter represents a buffer (array, span, or pointer).
        /// </summary>
        public bool IsBuffer { get; } = isBuffer;

        /// <summary>
        /// Validates the kernel parameter.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the parameter is invalid.</exception>
        /// <example>
        /// <code>
        /// var param = new KernelParameter("data", "float[]", true);
        /// param.Validate(); // Passes validation
        ///
        /// var invalidParam = new KernelParameter("", "float[]", true);
        /// invalidParam.Validate(); // Throws ArgumentException
        /// </code>
        /// </example>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                throw new ArgumentException(
                    "Kernel parameter validation failed: Parameter name cannot be null or empty. " +
                    "Please provide a valid parameter name (e.g., 'input', 'output', 'data').",
                    nameof(Name));
            }

            if (string.IsNullOrWhiteSpace(Type))
            {
                throw new ArgumentException(
                    $"Kernel parameter validation failed for '{Name}': Parameter type cannot be null or empty. " +
                    "Please provide a valid type (e.g., 'float[]', 'int', 'Span<double>').",
                    nameof(Type));
            }

            // Additional validation: buffer types should be marked as buffers
            var bufferTypeIndicators = new[] { "[]", "*", "Span", "Memory", "ReadOnlySpan", "ReadOnlyMemory" };
            var shouldBeBuffer = bufferTypeIndicators.Any(Type.Contains);

            if (shouldBeBuffer && !IsBuffer)
            {
                throw new ArgumentException(
                    $"Kernel parameter validation failed for '{Name}': " +
                    $"Parameter with type '{Type}' appears to be a buffer type but IsBuffer is set to false. " +
                    $"Buffer types include arrays (e.g., 'float[]'), pointers (e.g., 'float*'), " +
                    $"and spans (e.g., 'Span<T>', 'ReadOnlySpan<T>'). " +
                    $"To fix this, set IsBuffer to true when creating the KernelParameter: " +
                    $"new KernelParameter(\"{Name}\", \"{Type}\", true)",
                    nameof(IsBuffer));
            }
        }

        /// <summary>
        /// Gets the parameter declaration string for method signatures in the format "Type Name".
        /// </summary>
        public string Declaration => $"{Type} {Name}";

        /// <summary>
        /// Gets a value indicating whether this parameter requires null checking.
        /// </summary>
        public bool RequiresNullCheck => IsBuffer && !Type.Contains("*") && !Type.Contains("Span") && !Type.Contains("Memory");

        /// <summary>
        /// Gets a value indicating whether this parameter requires empty check for spans/memory.
        /// </summary>
        public bool RequiresEmptyCheck => Type.Contains("Span") || Type.Contains("Memory");

        /// <summary>
        /// Returns a string representation of the kernel parameter.
        /// </summary>
        /// <returns>A string representation of the parameter.</returns>
        public override string ToString() => $"KernelParameter {{ Name = {Name}, Type = {Type}, IsBuffer = {IsBuffer} }}";
    }
}
