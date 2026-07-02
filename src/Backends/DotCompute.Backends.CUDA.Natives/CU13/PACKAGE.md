# DotCompute CUDA 13 Native Runtime Libraries

NVIDIA CUDA 13 redistributable runtime libraries for the [DotCompute](https://github.com/mivertowski/DotCompute) CUDA backend: **cudart 13.0.96** and **NVRTC 13.0.88** for `win-x64` and `linux-x64`.

## Why

DotCompute compiles your `[Kernel]` methods to CUDA-C and builds them at run time with NVRTC. Those libraries normally require a full CUDA Toolkit install. With this package, the DotCompute CUDA backend works on any machine with just an **NVIDIA display driver** — the same zero-install experience as driver-only frameworks.

```
dotnet add package DotCompute.Backends.CUDA.V2
dotnet add package DotCompute.Backends.CUDA.Natives.CU13.V2
```

No further configuration: DotCompute's native-library resolver probes the package's `runtimes/<rid>/native` assets automatically.

## Requirements

- NVIDIA display driver **r580 or newer** (driver supporting CUDA 13). For older drivers (r525–r579, e.g. many GTX-era machines), use `DotCompute.Backends.CUDA.Natives.CU12.V2` instead.
- x64 Windows or Linux.

## Contents & licensing

Unmodified binaries from NVIDIA's official [redistributable archive](https://developer.download.nvidia.com/compute/cuda/redist/) (redist release 13.0.2), SHA-256-verified: `cudart64_13.dll`, `nvrtc64_130_0.dll`, `nvrtc-builtins64_130.dll`, `libcudart.so.13`, `libnvrtc.so.13`, `libnvrtc-builtins.so.13.0`. The NVIDIA binaries are licensed under the [NVIDIA CUDA Toolkit EULA](https://docs.nvidia.com/cuda/eula/index.html) (redistribution permitted per Attachment A); see `LICENSE-NVIDIA-CUDA.txt`.
