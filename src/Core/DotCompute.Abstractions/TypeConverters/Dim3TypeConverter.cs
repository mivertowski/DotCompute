// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.ComponentModel;
using System.Globalization;
using DotCompute.Abstractions.Types;

namespace DotCompute.Abstractions.TypeConverters;

/// <summary>
/// Provides type conversion support for the <see cref="Dim3"/> structure.
/// </summary>
/// <remarks>
/// This converter enables string-to-Dim3 conversions for configuration files and property grids.
/// Supported string formats include:
/// - Single number: "256" -> Dim3(256, 1, 1)
/// - Two numbers: "256,128" -> Dim3(256, 128, 1)
/// - Three numbers: "256,128,64" -> Dim3(256, 128, 64)
/// - Parentheses format: "(256, 128, 64)" -> Dim3(256, 128, 64)
/// </remarks>
public class Dim3TypeConverter : TypeConverter
{
    /// <summary>
    /// Determines whether this converter can convert an object of the given type to a <see cref="Dim3"/>.
    /// </summary>
    /// <param name="context">An <see cref="ITypeDescriptorContext"/> that provides a format context.</param>
    /// <param name="sourceType">The type to convert from.</param>
    /// <returns><c>true</c> if conversion is possible; otherwise, <c>false</c>.</returns>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    /// <summary>
    /// Determines whether this converter can convert a <see cref="Dim3"/> to the specified type.
    /// </summary>
    /// <param name="context">An <see cref="ITypeDescriptorContext"/> that provides a format context.</param>
    /// <param name="destinationType">The type to convert to.</param>
    /// <returns><c>true</c> if conversion is possible; otherwise, <c>false</c>.</returns>
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    /// <summary>
    /// Converts the given object to a <see cref="Dim3"/>.
    /// </summary>
    /// <param name="context">An <see cref="ITypeDescriptorContext"/> that provides a format context.</param>
    /// <param name="culture">The <see cref="CultureInfo"/> to use for conversion.</param>
    /// <param name="value">The object to convert.</param>
    /// <returns>A <see cref="Dim3"/> that represents the converted value.</returns>
    /// <exception cref="NotSupportedException">The conversion cannot be performed.</exception>
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string stringValue)
        {
            // Remove parentheses and whitespace
            stringValue = stringValue.Trim().Trim('(', ')');

            // Split by comma or space

            var parts = stringValue.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);


            try
            {
                return parts.Length switch
                {
                    1 when int.TryParse(parts[0], out var x) => new Dim3(x),
                    2 when int.TryParse(parts[0], out var x) && int.TryParse(parts[1], out var y) => new Dim3(x, y),
                    3 when int.TryParse(parts[0], out var x) && int.TryParse(parts[1], out var y) && int.TryParse(parts[2], out var z) => new Dim3(x, y, z),
                    _ => throw new FormatException($"Cannot convert '{value}' to Dim3. Expected format: 'x' or 'x,y' or 'x,y,z'")
                };
            }
            catch (FormatException)
            {
                throw new NotSupportedException($"Cannot convert '{value}' to Dim3. Values must be valid integers.");
            }
        }

        return base.ConvertFrom(context, culture, value);
    }

    /// <summary>
    /// Converts a <see cref="Dim3"/> to the specified type.
    /// </summary>
    /// <param name="context">An <see cref="ITypeDescriptorContext"/> that provides a format context.</param>
    /// <param name="culture">The <see cref="CultureInfo"/> to use for conversion.</param>
    /// <param name="value">The <see cref="Dim3"/> to convert.</param>
    /// <param name="destinationType">The type to convert to.</param>
    /// <returns>An object that represents the converted value.</returns>
    /// <exception cref="NotSupportedException">The conversion cannot be performed.</exception>
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is Dim3 dim3)
        {
            // Return in a clean format
            if (dim3.Y == 1 && dim3.Z == 1)
            {

                return dim3.X.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }


            if (dim3.Z == 1)
            {

                return $"{dim3.X}, {dim3.Y}";
            }


            return $"{dim3.X}, {dim3.Y}, {dim3.Z}";
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
