// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.Compilation;

/// <summary>
/// Provides Metal Shading Language (MSL) math intrinsics support for Metal kernel compilation.
/// Enables access to GPU-accelerated math functions using the metal:: namespace.
/// </summary>
/// <remarks>
/// Metal uses the metal:: namespace for math functions and relies on function overloading
/// rather than type suffixes (e.g., metal::sin works for both float and half).
/// </remarks>
public static class MetalMathIntrinsics
{
    /// <summary>
    /// Gets the MSL math intrinsics header content for Metal compilation.
    /// This header provides convenience macros and ensures metal math functions are available.
    /// </summary>
    public static string MathHeader { get; } = @"
// Metal Shading Language Math Intrinsics Header
// Provides math function declarations and convenience macros for GPU computation
// Compatible with Metal 3.0+ and Apple Silicon / AMD GPUs

#ifndef METAL_MATH_INTRINSICS_H
#define METAL_MATH_INTRINSICS_H

#include <metal_stdlib>
#include <metal_math>

using namespace metal;

// ============================================================================
// Single-precision (float) math function aliases
// Metal uses overloaded functions, so these are convenience macros
// ============================================================================

// Trigonometric functions
#ifndef sinf
#define sinf(x) metal::sin((float)(x))
#endif
#ifndef cosf
#define cosf(x) metal::cos((float)(x))
#endif
#ifndef tanf
#define tanf(x) metal::tan((float)(x))
#endif
#ifndef asinf
#define asinf(x) metal::asin((float)(x))
#endif
#ifndef acosf
#define acosf(x) metal::acos((float)(x))
#endif
#ifndef atanf
#define atanf(x) metal::atan((float)(x))
#endif
#ifndef atan2f
#define atan2f(y, x) metal::atan2((float)(y), (float)(x))
#endif

// Hyperbolic functions
#ifndef sinhf
#define sinhf(x) metal::sinh((float)(x))
#endif
#ifndef coshf
#define coshf(x) metal::cosh((float)(x))
#endif
#ifndef tanhf
#define tanhf(x) metal::tanh((float)(x))
#endif
#ifndef asinhf
#define asinhf(x) metal::asinh((float)(x))
#endif
#ifndef acoshf
#define acoshf(x) metal::acosh((float)(x))
#endif
#ifndef atanhf
#define atanhf(x) metal::atanh((float)(x))
#endif

// Exponential and logarithmic functions
#ifndef expf
#define expf(x) metal::exp((float)(x))
#endif
#ifndef exp2f
#define exp2f(x) metal::exp2((float)(x))
#endif
#ifndef expm1f
#define expm1f(x) metal::expm1((float)(x))
#endif
#ifndef logf
#define logf(x) metal::log((float)(x))
#endif
#ifndef log2f
#define log2f(x) metal::log2((float)(x))
#endif
#ifndef log10f
#define log10f(x) metal::log10((float)(x))
#endif
#ifndef log1pf
#define log1pf(x) metal::log1p((float)(x))
#endif

// Power and root functions
#ifndef powf
#define powf(x, y) metal::pow((float)(x), (float)(y))
#endif
#ifndef sqrtf
#define sqrtf(x) metal::sqrt((float)(x))
#endif
#ifndef rsqrtf
#define rsqrtf(x) metal::rsqrt((float)(x))
#endif
#ifndef cbrtf
#define cbrtf(x) metal::cbrt((float)(x))
#endif

// Rounding functions
#ifndef floorf
#define floorf(x) metal::floor((float)(x))
#endif
#ifndef ceilf
#define ceilf(x) metal::ceil((float)(x))
#endif
#ifndef roundf
#define roundf(x) metal::round((float)(x))
#endif
#ifndef truncf
#define truncf(x) metal::trunc((float)(x))
#endif
#ifndef rintf
#define rintf(x) metal::rint((float)(x))
#endif

// Absolute and sign functions
#ifndef fabsf
#define fabsf(x) metal::fabs((float)(x))
#endif
#ifndef copysignf
#define copysignf(x, y) metal::copysign((float)(x), (float)(y))
#endif

// Min/Max/Clamp functions
#ifndef fminf
#define fminf(x, y) metal::fmin((float)(x), (float)(y))
#endif
#ifndef fmaxf
#define fmaxf(x, y) metal::fmax((float)(x), (float)(y))
#endif
#ifndef clampf
#define clampf(x, lo, hi) metal::clamp((float)(x), (float)(lo), (float)(hi))
#endif

// Modulo and remainder functions
#ifndef fmodf
#define fmodf(x, y) metal::fmod((float)(x), (float)(y))
#endif
#ifndef remainderf
#define remainderf(x, y) metal::remainder((float)(x), (float)(y))
#endif

// Special functions
#ifndef ldexpf
#define ldexpf(x, n) metal::ldexp((float)(x), (int)(n))
#endif
#ifndef frexpf
#define frexpf(x, exp) metal::frexp((float)(x), (int&)(exp))
#endif

// ============================================================================
// Double-precision math function aliases
// Note: Double precision support depends on GPU capability
// ============================================================================

#if defined(__HAVE_NATIVE_DOUBLE__) || defined(METAL_DOUBLE_PRECISION)

#ifndef sin_d
#define sin_d(x) metal::sin((double)(x))
#endif
#ifndef cos_d
#define cos_d(x) metal::cos((double)(x))
#endif
#ifndef tan_d
#define tan_d(x) metal::tan((double)(x))
#endif
#ifndef exp_d
#define exp_d(x) metal::exp((double)(x))
#endif
#ifndef log_d
#define log_d(x) metal::log((double)(x))
#endif
#ifndef sqrt_d
#define sqrt_d(x) metal::sqrt((double)(x))
#endif
#ifndef pow_d
#define pow_d(x, y) metal::pow((double)(x), (double)(y))
#endif
#ifndef fabs_d
#define fabs_d(x) metal::fabs((double)(x))
#endif
#ifndef floor_d
#define floor_d(x) metal::floor((double)(x))
#endif
#ifndef ceil_d
#define ceil_d(x) metal::ceil((double)(x))
#endif

#endif // __HAVE_NATIVE_DOUBLE__

// ============================================================================
// Half-precision (float16) math function aliases
// Optimized for Apple Neural Engine and GPU texture sampling
// ============================================================================

#ifndef sinh
#define sinh(x) metal::sin((half)(x))
#endif
#ifndef cosh
#define cosh(x) metal::cos((half)(x))
#endif
#ifndef sqrth
#define sqrth(x) metal::sqrt((half)(x))
#endif
#ifndef rsqrth
#define rsqrth(x) metal::rsqrt((half)(x))
#endif
#ifndef exph
#define exph(x) metal::exp((half)(x))
#endif
#ifndef logh
#define logh(x) metal::log((half)(x))
#endif

// ============================================================================
// Fast math approximations
// These provide faster but less accurate results
// ============================================================================

#ifndef fast_sinf
#define fast_sinf(x) metal::fast::sin((float)(x))
#endif
#ifndef fast_cosf
#define fast_cosf(x) metal::fast::cos((float)(x))
#endif
#ifndef fast_tanf
#define fast_tanf(x) metal::fast::tan((float)(x))
#endif
#ifndef fast_expf
#define fast_expf(x) metal::fast::exp((float)(x))
#endif
#ifndef fast_exp2f
#define fast_exp2f(x) metal::fast::exp2((float)(x))
#endif
#ifndef fast_logf
#define fast_logf(x) metal::fast::log((float)(x))
#endif
#ifndef fast_log2f
#define fast_log2f(x) metal::fast::log2((float)(x))
#endif
#ifndef fast_powf
#define fast_powf(x, y) metal::fast::pow((float)(x), (float)(y))
#endif
#ifndef fast_rsqrtf
#define fast_rsqrtf(x) metal::fast::rsqrt((float)(x))
#endif

// ============================================================================
// Precise math (slower but IEEE-compliant)
// ============================================================================

#ifndef precise_sinf
#define precise_sinf(x) metal::precise::sin((float)(x))
#endif
#ifndef precise_cosf
#define precise_cosf(x) metal::precise::cos((float)(x))
#endif
#ifndef precise_tanf
#define precise_tanf(x) metal::precise::tan((float)(x))
#endif
#ifndef precise_expf
#define precise_expf(x) metal::precise::exp((float)(x))
#endif
#ifndef precise_logf
#define precise_logf(x) metal::precise::log((float)(x))
#endif
#ifndef precise_powf
#define precise_powf(x, y) metal::precise::pow((float)(x), (float)(y))
#endif
#ifndef precise_sqrtf
#define precise_sqrtf(x) metal::precise::sqrt((float)(x))
#endif
#ifndef precise_rsqrtf
#define precise_rsqrtf(x) metal::precise::rsqrt((float)(x))
#endif

// ============================================================================
// SIMD vector math helpers
// Support for float2, float3, float4 vector types
// ============================================================================

#ifndef vec_sin
#define vec_sin(v) metal::sin(v)
#endif
#ifndef vec_cos
#define vec_cos(v) metal::cos(v)
#endif
#ifndef vec_normalize
#define vec_normalize(v) metal::normalize(v)
#endif
#ifndef vec_length
#define vec_length(v) metal::length(v)
#endif
#ifndef vec_dot
#define vec_dot(a, b) metal::dot(a, b)
#endif
#ifndef vec_cross
#define vec_cross(a, b) metal::cross(a, b)
#endif

// ============================================================================
// Utility functions
// ============================================================================

// Mix/Lerp function
#ifndef mixf
#define mixf(a, b, t) metal::mix((float)(a), (float)(b), (float)(t))
#endif

// Step function
#ifndef stepf
#define stepf(edge, x) metal::step((float)(edge), (float)(x))
#endif

// Smooth step function
#ifndef smoothstepf
#define smoothstepf(edge0, edge1, x) metal::smoothstep((float)(edge0), (float)(edge1), (float)(x))
#endif

// Fused multiply-add (potentially faster on some GPUs)
#ifndef fmaf
#define fmaf(a, b, c) metal::fma((float)(a), (float)(b), (float)(c))
#endif

// Sign function
#ifndef signf
#define signf(x) metal::sign((float)(x))
#endif

// Saturate (clamp to [0, 1])
#ifndef saturatef
#define saturatef(x) metal::saturate((float)(x))
#endif

#endif // METAL_MATH_INTRINSICS_H
";

    /// <summary>
    /// Gets the include name for the math header when used with Metal compilation.
    /// </summary>
    public const string MathHeaderName = "metal_math_intrinsics.h";

    /// <summary>
    /// Checks if the kernel source code requires math intrinsics.
    /// </summary>
    /// <param name="source">MSL source code to analyze.</param>
    /// <returns>True if math intrinsics are detected, false otherwise.</returns>
    public static bool RequiresMathIntrinsics(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        // Check for common math function patterns
        var mathPatterns = new[]
        {
            // Metal namespace functions
            "metal::sin", "metal::cos", "metal::tan", "metal::exp", "metal::log",
            "metal::sqrt", "metal::pow", "metal::fabs", "metal::floor", "metal::ceil",
            "metal::round", "metal::rsqrt", "metal::atan", "metal::asin", "metal::acos",
            "metal::sinh", "metal::cosh", "metal::tanh", "metal::log2", "metal::log10",
            "metal::exp2", "metal::fmin", "metal::fmax", "metal::clamp", "metal::fmod",

            // Fast math variants
            "metal::fast::sin", "metal::fast::cos", "metal::fast::exp", "metal::fast::log",
            "metal::fast::pow", "metal::fast::rsqrt",

            // Precise math variants
            "metal::precise::sin", "metal::precise::cos", "metal::precise::exp",
            "metal::precise::log", "metal::precise::pow", "metal::precise::sqrt",

            // Common C-style function calls (may need translation)
            "sinf(", "cosf(", "tanf(", "expf(", "logf(", "sqrtf(", "powf(",
            "fabsf(", "floorf(", "ceilf(", "roundf(",
            "sin(", "cos(", "tan(", "exp(", "log(", "sqrt(", "pow(",
            "fabs(", "floor(", "ceil(", "round(",

            // Vector operations
            "normalize(", "length(", "dot(", "cross(",

            // Special functions
            "fma(", "mix(", "smoothstep(", "saturate("
        };

        return mathPatterns.Any(pattern =>
            source.Contains(pattern, StringComparison.Ordinal));
    }

    /// <summary>
    /// Gets the C# to MSL math function translation mappings.
    /// </summary>
    /// <returns>Dictionary mapping C# math functions to MSL equivalents.</returns>
    public static IReadOnlyDictionary<string, string> GetTranslationMappings()
    {
        return new Dictionary<string, string>
        {
            // System.Math (double precision)
            { "Math.Sqrt", "metal::sqrt" },
            { "Math.Abs", "metal::fabs" },
            { "Math.Sin", "metal::sin" },
            { "Math.Cos", "metal::cos" },
            { "Math.Tan", "metal::tan" },
            { "Math.Asin", "metal::asin" },
            { "Math.Acos", "metal::acos" },
            { "Math.Atan", "metal::atan" },
            { "Math.Atan2", "metal::atan2" },
            { "Math.Sinh", "metal::sinh" },
            { "Math.Cosh", "metal::cosh" },
            { "Math.Tanh", "metal::tanh" },
            { "Math.Exp", "metal::exp" },
            { "Math.Log", "metal::log" },
            { "Math.Log2", "metal::log2" },
            { "Math.Log10", "metal::log10" },
            { "Math.Pow", "metal::pow" },
            { "Math.Floor", "metal::floor" },
            { "Math.Ceiling", "metal::ceil" },
            { "Math.Round", "metal::round" },
            { "Math.Truncate", "metal::trunc" },
            { "Math.Min", "metal::min" },
            { "Math.Max", "metal::max" },
            { "Math.Clamp", "metal::clamp" },
            { "Math.Sign", "metal::sign" },

            // System.MathF (single precision)
            { "MathF.Sqrt", "metal::sqrt" },
            { "MathF.Abs", "metal::fabs" },
            { "MathF.Sin", "metal::sin" },
            { "MathF.Cos", "metal::cos" },
            { "MathF.Tan", "metal::tan" },
            { "MathF.Asin", "metal::asin" },
            { "MathF.Acos", "metal::acos" },
            { "MathF.Atan", "metal::atan" },
            { "MathF.Atan2", "metal::atan2" },
            { "MathF.Sinh", "metal::sinh" },
            { "MathF.Cosh", "metal::cosh" },
            { "MathF.Tanh", "metal::tanh" },
            { "MathF.Exp", "metal::exp" },
            { "MathF.Log", "metal::log" },
            { "MathF.Log2", "metal::log2" },
            { "MathF.Log10", "metal::log10" },
            { "MathF.Pow", "metal::pow" },
            { "MathF.Floor", "metal::floor" },
            { "MathF.Ceiling", "metal::ceil" },
            { "MathF.Round", "metal::round" },
            { "MathF.Truncate", "metal::trunc" },
            { "MathF.Min", "metal::min" },
            { "MathF.Max", "metal::max" },
            { "MathF.FusedMultiplyAdd", "metal::fma" },
            { "MathF.CopySign", "metal::copysign" },
            { "MathF.Cbrt", "metal::cbrt" },

            // Additional Metal-specific
            { "Math.FusedMultiplyAdd", "metal::fma" },
            { "Math.ReciprocalSqrtEstimate", "metal::rsqrt" }
        };
    }

    /// <summary>
    /// Gets the list of supported fast math function names.
    /// </summary>
    public static IReadOnlyList<string> FastMathFunctions => new[]
    {
        "fast::sin", "fast::cos", "fast::tan",
        "fast::exp", "fast::exp2", "fast::exp10",
        "fast::log", "fast::log2", "fast::log10",
        "fast::pow", "fast::rsqrt"
    };

    /// <summary>
    /// Gets the list of supported precise math function names.
    /// </summary>
    public static IReadOnlyList<string> PreciseMathFunctions => new[]
    {
        "precise::sin", "precise::cos", "precise::tan",
        "precise::exp", "precise::log", "precise::pow",
        "precise::sqrt", "precise::rsqrt"
    };
}
