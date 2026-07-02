# DotCompute CUDA 12 Native Runtime Libraries

NVIDIA CUDA 12 redistributable runtime libraries for the [DotCompute](https://github.com/mivertowski/DotCompute) CUDA backend: **cudart 12.9.79** and **NVRTC 12.9.86** for `win-x64` and `linux-x64`.

## Why

DotCompute compiles your `[Kernel]` methods to CUDA-C and builds them at run time with NVRTC. Those libraries normally require a full CUDA Toolkit install. With this package, the DotCompute CUDA backend works on any machine with just an **NVIDIA display driver** — the same zero-install experience as driver-only frameworks.

```
dotnet add package DotCompute.Backends.CUDA.V2
dotnet add package DotCompute.Backends.CUDA.Natives.CU12.V2
```

No further configuration: DotCompute's native-library resolver probes the package's `runtimes/<rid>/native` assets automatically, and prefers the CUDA major matching your installed driver.

## Requirements

- NVIDIA display driver **r525 or newer** (driver supporting CUDA 12). Use this package for drivers older than r580 (e.g. many GTX-era machines); on current drivers prefer `DotCompute.Backends.CUDA.Natives.CU13.V2`.
- x64 Windows or Linux.

## Contents & licensing

Unmodified binaries from NVIDIA's official [redistributable archive](https://developer.download.nvidia.com/compute/cuda/redist/) (redist release 12.9.1), SHA-256-verified: `cudart64_12.dll`, `nvrtc64_120_0.dll`, `nvrtc-builtins64_129.dll`, `libcudart.so.12`, `libnvrtc.so.12`, `libnvrtc-builtins.so.12.9`. The NVIDIA binaries are licensed under the [NVIDIA CUDA Toolkit EULA](https://docs.nvidia.com/cuda/eula/index.html) (redistribution permitted per Attachment A); see `LICENSE-NVIDIA-CUDA.txt`.
