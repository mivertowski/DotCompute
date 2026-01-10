// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Compilation;

/// <summary>
/// Provides CUDA device math intrinsics headers for NVRTC compilation.
/// These headers enable access to fast GPU math functions like __sinf, __cosf, __sqrtf, etc.
/// </summary>
internal static class CudaMathIntrinsics
{
    /// <summary>
    /// Gets the device math header content for NVRTC compilation.
    /// This header provides declarations for CUDA device math intrinsics.
    /// </summary>
    public static string MathHeader { get; } = @"
// CUDA Device Math Intrinsics Header
// Provides declarations for fast GPU math functions

#ifndef __CUDA_MATH_INTRINSICS_H__
#define __CUDA_MATH_INTRINSICS_H__

// Single-precision floating-point intrinsics
extern ""C"" __device__ float __sinf(float x);
extern ""C"" __device__ float __cosf(float x);
extern ""C"" __device__ float __tanf(float x);
extern ""C"" __device__ float __expf(float x);
extern ""C"" __device__ float __logf(float x);
extern ""C"" __device__ float __log2f(float x);
extern ""C"" __device__ float __log10f(float x);
extern ""C"" __device__ float __sqrtf(float x);
extern ""C"" __device__ float __rsqrtf(float x);
extern ""C"" __device__ float __powf(float x, float y);
extern ""C"" __device__ float __fabsf(float x);
extern ""C"" __device__ float __floorf(float x);
extern ""C"" __device__ float __ceilf(float x);
extern ""C"" __device__ float __truncf(float x);
extern ""C"" __device__ float __roundf(float x);

// Double-precision floating-point intrinsics
extern ""C"" __device__ double __sin(double x);
extern ""C"" __device__ double __cos(double x);
extern ""C"" __device__ double __tan(double x);
extern ""C"" __device__ double __exp(double x);
extern ""C"" __device__ double __log(double x);
extern ""C"" __device__ double __log2(double x);
extern ""C"" __device__ double __log10(double x);
extern ""C"" __device__ double __sqrt(double x);
extern ""C"" __device__ double __rsqrt(double x);
extern ""C"" __device__ double __pow(double x, double y);
extern ""C"" __device__ double __fabs(double x);
extern ""C"" __device__ double __floor(double x);
extern ""C"" __device__ double __ceil(double x);
extern ""C"" __device__ double __trunc(double x);
extern ""C"" __device__ double __round(double x);

// Additional single-precision intrinsics
extern ""C"" __device__ float __fminf(float x, float y);
extern ""C"" __device__ float __fmaxf(float x, float y);
extern ""C"" __device__ float __fmodf(float x, float y);
extern ""C"" __device__ float __fdividef(float x, float y);
extern ""C"" __device__ float __sincosf(float x, float* sptr, float* cptr);
extern ""C"" __device__ float __asinf(float x);
extern ""C"" __device__ float __acosf(float x);
extern ""C"" __device__ float __atanf(float x);
extern ""C"" __device__ float __atan2f(float y, float x);
extern ""C"" __device__ float __sinhf(float x);
extern ""C"" __device__ float __coshf(float x);
extern ""C"" __device__ float __tanhf(float x);

// Additional double-precision intrinsics
extern ""C"" __device__ double __fmin(double x, double y);
extern ""C"" __device__ double __fmax(double x, double y);
extern ""C"" __device__ double __fmod(double x, double y);
extern ""C"" __device__ double __fdivide(double x, double y);
extern ""C"" __device__ double __sincos(double x, double* sptr, double* cptr);
extern ""C"" __device__ double __asin(double x);
extern ""C"" __device__ double __acos(double x);
extern ""C"" __device__ double __atan(double x);
extern ""C"" __device__ double __atan2(double y, double x);
extern ""C"" __device__ double __sinh(double x);
extern ""C"" __device__ double __cosh(double x);
extern ""C"" __device__ double __tanh(double x);

// Standard math function aliases (fallback to intrinsics)
#ifndef sinf
#define sinf(x) __sinf(x)
#endif
#ifndef cosf
#define cosf(x) __cosf(x)
#endif
#ifndef tanf
#define tanf(x) __tanf(x)
#endif
#ifndef expf
#define expf(x) __expf(x)
#endif
#ifndef logf
#define logf(x) __logf(x)
#endif
#ifndef log2f
#define log2f(x) __log2f(x)
#endif
#ifndef log10f
#define log10f(x) __log10f(x)
#endif
#ifndef sqrtf
#define sqrtf(x) __sqrtf(x)
#endif
#ifndef powf
#define powf(x, y) __powf(x, y)
#endif
#ifndef fabsf
#define fabsf(x) __fabsf(x)
#endif
#ifndef floorf
#define floorf(x) __floorf(x)
#endif
#ifndef ceilf
#define ceilf(x) __ceilf(x)
#endif
#ifndef truncf
#define truncf(x) __truncf(x)
#endif
#ifndef roundf
#define roundf(x) __roundf(x)
#endif

// Double precision aliases
#ifndef sin
#define sin(x) __sin(x)
#endif
#ifndef cos
#define cos(x) __cos(x)
#endif
#ifndef tan
#define tan(x) __tan(x)
#endif
#ifndef exp
#define exp(x) __exp(x)
#endif
#ifndef log
#define log(x) __log(x)
#endif
#ifndef sqrt
#define sqrt(x) __sqrt(x)
#endif
#ifndef pow
#define pow(x, y) __pow(x, y)
#endif
#ifndef fabs
#define fabs(x) __fabs(x)
#endif
#ifndef floor
#define floor(x) __floor(x)
#endif
#ifndef ceil
#define ceil(x) __ceil(x)
#endif

#endif // __CUDA_MATH_INTRINSICS_H__
";

    /// <summary>
    /// Gets the include name for the math header when used with NVRTC.
    /// </summary>
    public const string MathHeaderName = "cuda_math_intrinsics.h";

    /// <summary>
    /// Checks if the kernel source code requires math intrinsics.
    /// </summary>
    /// <param name="source">CUDA source code to analyze.</param>
    /// <returns>True if math intrinsics are detected, false otherwise.</returns>
    public static bool RequiresMathIntrinsics(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        // Check for common math intrinsics
        var mathIntrinsics = new[]
        {
            "__sinf", "__cosf", "__tanf", "__expf", "__logf", "__sqrtf", "__powf",
            "__fabsf", "__floorf", "__ceilf", "__roundf", "__rsqrtf",
            "__sin", "__cos", "__tan", "__exp", "__log", "__sqrt", "__pow",
            "__fabs", "__floor", "__ceil", "__round", "__rsqrt",
            "sinf(", "cosf(", "tanf(", "expf(", "logf(", "sqrtf(", "powf(",
            "fabsf(", "floorf(", "ceilf(", "roundf(",
            "sin(", "cos(", "tan(", "exp(", "log(", "sqrt(", "pow(",
            "fabs(", "floor(", "ceil(", "round("
        };

        return mathIntrinsics.Any(intrinsic =>
            source.Contains(intrinsic, StringComparison.Ordinal));
    }
}
