# CUDA Without a CUDA Toolkit Install

DotCompute's CUDA backend compiles your `[Kernel]` methods to CUDA-C and builds them at run time with **NVRTC**, using the **cudart** runtime API. Both libraries normally ship with the CUDA Toolkit — a multi-GB developer install that end-user machines rarely have. The NVIDIA **display driver** alone (what every machine with a working NVIDIA GPU has) provides only the driver API (`nvcuda.dll` / `libcuda.so.1`), which is why driver-only frameworks work out of the box while a toolkit-based stack reports "CUDA runtime library not found" (see issue [#182](https://github.com/mivertowski/DotCompute/issues/182)).

The **DotCompute.Backends.CUDA.Natives** packages close that gap: they ship NVIDIA's official redistributable cudart + NVRTC binaries as NuGet native assets, so the CUDA backend works with **only a display driver installed**.

## Usage

```bash
dotnet add package DotCompute.Backends.CUDA.V2
# pick ONE, matching the machine's driver generation:
dotnet add package DotCompute.Backends.CUDA.Natives.CU13.V2   # driver r580+ (CUDA 13)
dotnet add package DotCompute.Backends.CUDA.Natives.CU12.V2   # driver r525+ (CUDA 12, e.g. GTX-era)
```

No configuration needed. DotCompute's native-library resolver probes, in order:

1. the .NET host search path — which includes the package's `runtimes/<rid>/native` assets (from `deps.json`) and the application directory,
2. the OS loader (PATH / `ldconfig` — i.e. a system CUDA Toolkit still wins if present and newer),
3. explicit `runtimes/<rid>/native` next to the app (for hosts that skip `deps.json`),
4. on Windows, CUDA Toolkit install locations (`%CUDA_PATH%`, `%CUDA_PATH_V*%`, `Program Files`).

If **both** natives packages are referenced, the resolver picks the newest CUDA major the installed driver actually supports (via `cuDriverGetVersion`) — CU13 binaries are skipped on a CUDA-12-era driver rather than failing with `InsufficientDriver`.

## Choosing CU12 vs CU13

| Driver | nvidia-smi shows | Package |
|---|---|---|
| r580 or newer | CUDA Version 13.x | `...Natives.CU13.V2` |
| r525 – r579 | CUDA Version 12.x | `...Natives.CU12.V2` |

`nvidia-smi`'s "CUDA Version" is the **driver's** supported version — it does not mean a toolkit is installed.

## Building the packages from source

The NVIDIA binaries are not checked into git. To pack locally:

```bash
./scripts/download-cuda-natives.sh   # downloads + SHA-256-verifies from NVIDIA's redist archive
dotnet pack src/Backends/DotCompute.Backends.CUDA.Natives/CU13/DotCompute.Backends.CUDA.Natives.CU13.csproj -c Release -o artifacts/packages
dotnet pack src/Backends/DotCompute.Backends.CUDA.Natives/CU12/DotCompute.Backends.CUDA.Natives.CU12.csproj -c Release -o artifacts/packages
```

The `CUDA Natives Validation` GitHub workflow proves the packages in a true clean room: GitHub-hosted runners have no toolkit and no GPU, yet the packaged NVRTC must compile a kernel to PTX (NVRTC is a pure compiler and needs no GPU).

## Licensing

The packaging is MIT; the contained NVIDIA binaries are governed by the [NVIDIA CUDA Toolkit EULA](https://docs.nvidia.com/cuda/eula/index.html), which expressly permits redistributing cudart and NVRTC (Attachment A). Each package carries `LICENSE-NVIDIA-CUDA.txt`.

## Limitations

- A GPU + display driver are still required to *execute* kernels (NVRTC compiles without either, but launches need the driver API).
- x64 only (win-x64, linux-x64) for now.
- macOS has no CUDA; use the Metal backend.
