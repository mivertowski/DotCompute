// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using global::System.Runtime.CompilerServices;

namespace DotCompute.Generators.Utils
{

    /// <summary>
    /// Provides argument validation helpers compatible with netstandard2.0.
    /// These are polyfills for .NET 6+ ArgumentNullException and ArgumentException methods.
    /// </summary>
    internal static class ArgumentValidation
    {
        /// <summary>
        /// Throws an ArgumentNullException if the argument is null.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfNull(object? argument, [CallerArgumentExpression("argument")] string? paramName = null)
        {
            if (argument is null)
            {
                throw new ArgumentNullException(paramName);
            }
        }

        /// <summary>
        /// Throws an ArgumentException if the string argument is null or empty.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfNullOrEmpty(string? argument, [CallerArgumentExpression("argument")] string? paramName = null)
        {
            if (string.IsNullOrEmpty(argument))
            {
                throw new ArgumentException("Value cannot be null or empty.", paramName);
            }
        }

        /// <summary>
        /// Throws an ArgumentException if the string argument is null, empty, or whitespace.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfNullOrWhiteSpace(string? argument, [CallerArgumentExpression("argument")] string? paramName = null)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", paramName);
            }
        }

#if NETSTANDARD2_0
        /// <summary>
        /// Provides CallerArgumentExpression attribute for netstandard2.0.
        /// </summary>
        [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
        internal sealed class CallerArgumentExpressionAttribute : Attribute
        {
            public CallerArgumentExpressionAttribute(string parameterName)
            {
                ParameterName = parameterName;
            }

            public string ParameterName { get; }
        }
#endif
    }
}