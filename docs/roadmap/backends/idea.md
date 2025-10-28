# Must-have next

* **DirectX 12 (DX12 / DXIL / HLSL, incl. DirectCompute)**
  Windows devs (games, enterprise visualization) live here. Great drivers across NVIDIA/AMD/Intel. Big adoption unlock.

* **Vulkan compute (SPIR-V)**
  The cross-platform GPU path for Linux + Android and even Windows. Pairs well with a SPIR-V IR so you can retarget.

* **ROCm / HIP (AMD)**
  Essential for Linux workstations/HPC with AMD GPUs. HIP gives CUDA-ish semantics; helps with CUDA→AMD parity.

* **oneAPI Level Zero / SYCL (Intel GPUs + CPUs)**
  Intel’s modern stack (Arc, Xe HPC). SYCL broadens reach via a standard C++ offload model; Level Zero for low-level control.

# High-impact “broad adoption” adds

* **WebGPU (WGSL) via wgpu-native/Dawn**
  Lets you reach desktop + web (WASM), plus Android in time. Great for demos, education, and “no-driver-install” environments.

* **DirectML (on top of DX12)**
  If you expect ML-style ops, DirectML provides vendor-agnostic ML acceleration on Windows GPUs/NPUs with one path.

* **ISPC (CPU SPMD) + .NET HW intrinsics (AVX2/AVX-512/NEON/SVE)**
  A serious CPU backend: vectorized SPMD (ISPC) + a managed fallback using .NET 9 intrinsics for portability and perf.

# Nice-to-have / strategic

* **OpenGL compute**
  Legacy but still found in scientific viz and older clusters. Low maintenance if you already have GLSL/spirv-cross tooling.

* **FPGAs (Intel FPGA OpenCL, Xilinx Vitis)**
  Niche but valuable in telco/finance/embedded. SYCL helps here too (Intel FPGA flow).

* **OpenMP target offload**
  For HPC codebases that can’t or won’t adopt CUDA/HIP; gives you CPUs and some GPUs with one directive model.

* **Vendor math/NN libs as pluggable “assist” backends**
  cuBLAS/cuDNN, rocBLAS/MIOpen, oneDNN, cuTENSOR. Not kernels per se, but huge for adoption when people need GEMMs/convs fast.

# Implementation tips to keep it truly “universal”

* **Common IRs:** Normalize on **SPIR-V** (for Vulkan/WebGPU via translation) and optionally **MLIR** or **LLVM IR** for CPU/ISPC/HIP/SYCL paths; keep PTX/DXIL as emit targets, not authoring formats.
* **Kernel authoring:** Offer a single .NET kernel DSL, but also accept **HLSL** and **WGSL** input for migration paths (auto-convert to SPIR-V/DXIL where possible).
* **Memory model:** Abstract USM/SSBO/UAV differences; expose capabilities (subgroup/wave ops, matrix accelerators) via feature flags.
* **Matrix/Tensor cores:** Surface optional hooks: NVIDIA Tensor Cores (WMMA), AMD MFMA, Intel XMX/DPAS, Apple AMX/ANE (via Metal/CoreML) to future-proof ML/linear algebra.
* **Scheduler:** Pluggable runtime choosing backend by device + kernel features; let apps pin policies (e.g., prefer ROCm on Linux, DX12 on Windows).
* **Diagnostics:** Unified profiling/validation layer, mapping to PIX (DX12), RenderDoc (Vulkan/WebGPU), Nsight (CUDA), ROCm tools, VTune/Perf (CPU).

# A practical prioritization

1. **DX12 + Vulkan + ROCm + Level Zero/SYCL** → covers Windows/Linux across NVIDIA/AMD/Intel.
2. **WebGPU** → web + easier cross-platform demos.
3. **DirectML + ISPC/.NET intrinsics** → ML and strong CPU story.
4. **OpenGL compute / OpenMP offload / FPGA flows** → strategic niches.

If you’d like, I can sketch an interface matrix (features vs. backend) and a minimal IR pipeline plan tailored to your existing CUDA/Metal/OpenCL layers.
