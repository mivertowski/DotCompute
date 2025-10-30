// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Core.Types
{

    /// <summary>
    /// Represents a half-precision floating-point number (16-bit).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 2)]
    public readonly struct Half : IComparable<Half>, IEquatable<Half>
    {
        private readonly ushort _value;

        /// <summary>
        /// Initializes a new instance of the <see cref="Half"/> struct from a float value.
        /// </summary>
        public Half(float value)
        {
            _value = FloatToHalf(value);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Half"/> struct from raw bits.
        /// </summary>
        private Half(ushort bits)
        {
            _value = bits;
        }

        /// <summary>
        /// Gets the zero value.
        /// </summary>
        public static Half Zero => new(0);

        /// <summary>
        /// Gets the one value.
        /// </summary>
        public static Half One => new(0x3C00);

        /// <summary>
        /// Gets the minimum value.
        /// </summary>
        public static Half MinValue => new(0xFBFF);

        /// <summary>
        /// Gets the maximum value.
        /// </summary>
        public static Half MaxValue => new(0x7BFF);

        /// <summary>
        /// Gets the epsilon value.
        /// </summary>
        public static Half Epsilon => new(0x0001);

        /// <summary>
        /// Gets the positive infinity value.
        /// </summary>
        public static Half PositiveInfinity => new(0x7C00);

        /// <summary>
        /// Gets the negative infinity value.
        /// </summary>
        public static Half NegativeInfinity => new(0xFC00);

        /// <summary>
        /// Gets the NaN value.
        /// </summary>
        public static Half NaN => new(0x7E00);

        /// <summary>
        /// Converts the half to a float.
        /// </summary>
        public float ToSingle() => HalfToFloat(_value);

        /// <summary>
        /// Implicitly converts a Half to a float.
        /// </summary>
        public static implicit operator float(Half value) => value.ToSingle();

        /// <summary>
        /// Explicitly converts a float to a Half.
        /// </summary>
        public static explicit operator Half(float value) => new(value);

        /// <summary>
        /// Adds two Half values.
        /// </summary>
        public static Half operator +(Half left, Half right) => new Half(left.ToSingle() + right.ToSingle());

        /// <summary>
        /// Subtracts two Half values.
        /// </summary>
        public static Half operator -(Half left, Half right) => new Half(left.ToSingle() - right.ToSingle());

        /// <summary>
        /// Multiplies two Half values.
        /// </summary>
        public static Half operator *(Half left, Half right) => new Half(left.ToSingle() * right.ToSingle());

        /// <summary>
        /// Divides two Half values.
        /// </summary>
        public static Half operator /(Half left, Half right) => new Half(left.ToSingle() / right.ToSingle());

        /// <summary>
        /// Negates a Half value.
        /// </summary>
        public static Half operator -(Half value) => new Half((ushort)(value._value ^ 0x8000));

        /// <summary>
        /// Compares two Half values for equality.
        /// </summary>
        public static bool operator ==(Half left, Half right) => left._value == right._value;

        /// <summary>
        /// Compares two Half values for inequality.
        /// </summary>
        public static bool operator !=(Half left, Half right) => left._value != right._value;

        /// <summary>
        /// Checks if the left value is less than the right value.
        /// </summary>
        public static bool operator <(Half left, Half right) => left.ToSingle() < right.ToSingle();

        /// <summary>
        /// Checks if the left value is greater than the right value.
        /// </summary>
        public static bool operator >(Half left, Half right) => left.ToSingle() > right.ToSingle();

        /// <summary>
        /// Checks if the left value is less than or equal to the right value.
        /// </summary>
        public static bool operator <=(Half left, Half right) => left.ToSingle() <= right.ToSingle();

        /// <summary>
        /// Checks if the left value is greater than or equal to the right value.
        /// </summary>
        public static bool operator >=(Half left, Half right) => left.ToSingle() >= right.ToSingle();

        /// <inheritdoc/>
        public int CompareTo(Half other) => ToSingle().CompareTo(other.ToSingle());

        /// <inheritdoc/>
        public bool Equals(Half other) => _value == other._value;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is Half other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => _value.GetHashCode();

        /// <inheritdoc/>
        public override string ToString() => ToSingle().ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// Checks if the value is NaN.
        /// </summary>
        public bool IsNaN() => (_value & 0x7FFF) > 0x7C00;

        /// <summary>
        /// Checks if the value is infinity.
        /// </summary>
        public bool IsInfinity() => (_value & 0x7FFF) == 0x7C00;

        /// <summary>
        /// Checks if the value is positive infinity.
        /// </summary>
        public bool IsPositiveInfinity() => _value == 0x7C00;

        /// <summary>
        /// Checks if the value is negative infinity.
        /// </summary>
        public bool IsNegativeInfinity() => _value == 0xFC00;

        /// <summary>
        /// Gets the absolute value.
        /// </summary>
        public static Half Abs(Half value) => new((ushort)(value._value & 0x7FFF));

        private static ushort FloatToHalf(float value)
        {
            var fbits = BitConverter.SingleToUInt32Bits(value);
            var sign = (fbits >> 16) & 0x8000;
            var exponent = (int)((fbits >> 23) & 0xFF) - 127;
            var mantissa = fbits & 0x7FFFFF;

            if (exponent == 128)
            {
                // NaN or Infinity
                if (mantissa != 0)
                {
                    // NaN
                    return (ushort)(sign | 0x7E00);
                }
                else
                {
                    // Infinity
                    return (ushort)(sign | 0x7C00);
                }
            }
            else if (exponent > 15)
            {
                // Overflow to infinity
                return (ushort)(sign | 0x7C00);
            }
            else if (exponent > -15)
            {
                // Normal number
                exponent += 15;
                mantissa >>= 13;
                return (ushort)(sign | ((uint)exponent << 10) | mantissa);
            }
            else if (exponent > -25)
            {
                // Subnormal number
                mantissa |= 0x800000;
                mantissa >>= (int)(14 - exponent - 15);
                return (ushort)(sign | mantissa);
            }
            else
            {
                // Underflow to zero
                return (ushort)sign;
            }
        }

        private static float HalfToFloat(ushort value)
        {
            var sign = (uint)(value & 0x8000) << 16;
            var exponent = (value >> 10) & 0x1F;
            var mantissa = (uint)(value & 0x3FF) << 13;

            if (exponent == 0x1F)
            {
                // NaN or Infinity
                if (mantissa != 0)
                {
                    // NaN
                    mantissa |= 0x400000; // Make it a quiet NaN
                }
                return BitConverter.UInt32BitsToSingle(sign | 0x7F800000 | mantissa);
            }
            else if (exponent == 0)
            {
                if (mantissa == 0)
                {
                    // Zero
                    return BitConverter.UInt32BitsToSingle(sign);
                }
                else
                {
                    // Subnormal
                    exponent = 1;
                    while ((mantissa & 0x800000) == 0)
                    {
                        mantissa <<= 1;
                        exponent--;
                    }
                    mantissa &= 0x7FFFFF;
                    exponent = exponent - 15 + 127;
                    return BitConverter.UInt32BitsToSingle(sign | ((uint)exponent << 23) | mantissa);
                }
            }
            else
            {
                // Normal number
                exponent = exponent - 15 + 127;
                return BitConverter.UInt32BitsToSingle(sign | ((uint)exponent << 23) | mantissa);
            }
        }

        #region CA2225 Operator Overloads

        /// <summary>
        /// Adds two Half values.
        /// </summary>
        public static Half Add(Half left, Half right) => left + right;

        /// <summary>
        /// Subtracts two Half values.
        /// </summary>
        public static Half Subtract(Half left, Half right) => left - right;

        /// <summary>
        /// Multiplies two Half values.
        /// </summary>
        public static Half Multiply(Half left, Half right) => left * right;

        /// <summary>
        /// Divides two Half values.
        /// </summary>
        public static Half Divide(Half left, Half right) => left / right;

        /// <summary>
        /// Negates a Half value.
        /// </summary>
        public static Half Negate(Half value) => -value;

        /// <summary>
        /// Converts a single-precision floating-point number to Half (CA2225).
        /// </summary>
        /// <param name="value">The single value to convert.</param>
        /// <returns>A Half value.</returns>
        public static Half FromSingle(float value) => (Half)value;

        #endregion
    }
}
