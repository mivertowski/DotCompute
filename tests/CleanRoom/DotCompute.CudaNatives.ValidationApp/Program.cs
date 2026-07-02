// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// Clean-room validation for the DotCompute.Backends.CUDA.Natives.* packages (GH #187).
// Proves — on a machine with NO CUDA Toolkit and no GPU — that:
//   1. cudart resolves through DotCompute's native-library resolver (cudaRuntimeGetVersion),
//   2. NVRTC resolves the same way (nvrtcVersion),
//   3. NVRTC compiles a real kernel to PTX (exercises nvrtc-builtins too),
//   4. with NATIVES_VALIDATION_EXPECT_PACKAGED=1, the loaded libraries actually came from the
//      application's own output (the NuGet runtimes/<rid>/native assets), not a system install.
// The DotCompute P/Invokes are internal; they are invoked via reflection so this app exercises
// the exact code paths (and the exact resolver) the product uses.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

var failures = 0;

var backendAssembly = typeof(DotCompute.Backends.CUDA.CudaBackendFactory).Assembly;
var cudaRuntime = backendAssembly.GetType("DotCompute.Backends.CUDA.Native.CudaRuntime")
    ?? throw new InvalidOperationException("CudaRuntime type not found");
var nvrtc = backendAssembly.GetType("DotCompute.Backends.CUDA.Native.NvrtcInterop")
    ?? throw new InvalidOperationException("NvrtcInterop type not found");

static MethodInfo GetMethod(Type type, string name)
{
    return type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{type.Name}.{name} not found");
}

// ---- 1. cudart resolves and answers (works with no GPU and no driver) ----
try
{
    var versionArgs = new object?[] { 0 };
    var result = GetMethod(cudaRuntime, "cudaRuntimeGetVersion").Invoke(null, versionArgs)!;
    var version = (int)versionArgs[0]!;
    if (result.ToString() == "Success" && version > 0)
    {
        Console.WriteLine($"[natives-validation] PASS cudart resolved, cudaRuntimeGetVersion = {version / 1000}.{version % 1000 / 10}");
    }
    else
    {
        Console.WriteLine($"[natives-validation] FAIL cudaRuntimeGetVersion returned {result} (version {version})");
        failures++;
    }
}
catch (TargetInvocationException ex) when (ex.InnerException is DllNotFoundException dll)
{
    Console.WriteLine($"[natives-validation] FAIL cudart did not resolve: {dll.Message}");
    failures++;
}

// ---- 2. NVRTC resolves and answers ----
try
{
    var nvrtcArgs = new object?[] { 0, 0 };
    var result = GetMethod(nvrtc, "nvrtcVersion").Invoke(null, nvrtcArgs)!;
    Console.WriteLine((int)result! == 0
        ? $"[natives-validation] PASS nvrtc resolved, nvrtcVersion = {nvrtcArgs[0]}.{nvrtcArgs[1]}"
        : $"[natives-validation] FAIL nvrtcVersion returned {result}");
    failures += (int)result! == 0 ? 0 : 1;
}
catch (TargetInvocationException ex) when (ex.InnerException is DllNotFoundException dll)
{
    Console.WriteLine($"[natives-validation] FAIL nvrtc did not resolve: {dll.Message}");
    failures++;
}

// ---- 3. Compile a real kernel to PTX (GPU-less; also exercises nvrtc-builtins) ----
if (failures == 0)
{
    const string KernelSource = """
        extern "C" __global__ void vector_add(const float* a, const float* b, float* c, unsigned int n) {
            unsigned int i = blockIdx.x * blockDim.x + threadIdx.x;
            if (i < n) c[i] = a[i] + b[i];
        }
        """;

    var createArgs = new object?[] { IntPtr.Zero, KernelSource, "validation.cu", 0, IntPtr.Zero, IntPtr.Zero };
    var create = (int)GetMethod(nvrtc, "nvrtcCreateProgram").Invoke(null, createArgs)!;
    if (create != 0)
    {
        Console.WriteLine($"[natives-validation] FAIL nvrtcCreateProgram returned {create}");
        failures++;
    }
    else
    {
        var prog = (IntPtr)createArgs[0]!;
        var compile = (int)GetMethod(nvrtc, "nvrtcCompileProgram").Invoke(null, [prog, 0, IntPtr.Zero])!;
        if (compile != 0)
        {
            Console.WriteLine($"[natives-validation] FAIL nvrtcCompileProgram returned {compile}");
            failures++;
        }
        else
        {
            var sizeArgs = new object?[] { prog, IntPtr.Zero };
            _ = GetMethod(nvrtc, "nvrtcGetPTXSize").Invoke(null, sizeArgs);
            var ptxSize = (nint)(IntPtr)sizeArgs[1]!;
            var buffer = Marshal.AllocHGlobal(ptxSize);
            try
            {
                _ = GetMethod(nvrtc, "nvrtcGetPTX").Invoke(null, [prog, buffer]);
                var ptx = Marshal.PtrToStringUTF8(buffer) ?? string.Empty;
                if (ptx.Contains(".visible .entry vector_add", StringComparison.Ordinal))
                {
                    Console.WriteLine($"[natives-validation] PASS NVRTC compiled kernel to PTX ({ptxSize} bytes) with no toolkit and no GPU");
                }
                else
                {
                    Console.WriteLine("[natives-validation] FAIL PTX did not contain the expected entry point");
                    failures++;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        var destroyArgs = new object?[] { prog };
        _ = GetMethod(nvrtc, "nvrtcDestroyProgram").Invoke(null, destroyArgs);
    }
}

// ---- 4. Prove the libraries came from the package (strict clean-room assertion) ----
var loaded = Process.GetCurrentProcess().Modules
    .Cast<ProcessModule>()
    .Where(m => m.ModuleName.Contains("cudart", StringComparison.OrdinalIgnoreCase)
             || m.ModuleName.Contains("nvrtc", StringComparison.OrdinalIgnoreCase))
    .Select(m => m.FileName)
    .ToList();
foreach (var module in loaded)
{
    Console.WriteLine($"[natives-validation] loaded: {module}");
}

if (Environment.GetEnvironmentVariable("NATIVES_VALIDATION_EXPECT_PACKAGED") == "1")
{
    // Published apps load the assets from <app>/runtimes/<rid>/native (or the app root for
    // RID-specific publishes); `dotnet run` loads them straight from the NuGet package cache.
    var appRoot = Path.GetFullPath(AppContext.BaseDirectory);
    static bool IsPackaged(string file, string appRoot)
    {
        return Path.GetFullPath(file).StartsWith(appRoot, StringComparison.OrdinalIgnoreCase)
            || file.Contains("dotcompute.backends.cuda.natives", StringComparison.OrdinalIgnoreCase);
    }

    var offenders = loaded.Where(f => !IsPackaged(f, appRoot)).ToList();
    if (loaded.Count == 0 || offenders.Count > 0)
    {
        Console.WriteLine($"[natives-validation] FAIL expected packaged natives (app root or NuGet cache); offending: {string.Join(", ", offenders)}");
        failures++;
    }
    else
    {
        Console.WriteLine("[natives-validation] PASS all CUDA libraries were loaded from the packaged assets");
    }
}

Console.WriteLine(failures == 0 ? "[natives-validation] ALL PASS" : $"[natives-validation] {failures} FAILURE(S)");
return failures == 0 ? 0 : 1;
