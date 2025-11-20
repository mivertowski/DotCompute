# WSL2 CUDA Setup Guide

## Overview

DotCompute CUDA backend requires special configuration when running in Windows Subsystem for Linux 2 (WSL2). This guide covers the necessary setup steps.

## Requirements

- Windows 11 or Windows 10 version 21H2 or later
- WSL2 installed and configured
- NVIDIA GPU with driver version 510.39.01 or later on Windows host
- CUDA Toolkit (optional - for development)

## WSL2 CUDA Driver Architecture

WSL2 uses a special GPU driver architecture:
- **Windows Host**: NVIDIA driver installed on Windows provides GPU access
- **WSL2 Guest**: No Linux driver installation needed
- **Library Path**: NVIDIA libraries are located at `/usr/lib/wsl/lib/`

### Critical Configuration

The key difference from native Linux is the library path:

```bash
# WSL2 NVIDIA libraries (correct)
/usr/lib/wsl/lib/libcuda.so
/usr/lib/wsl/lib/libcuda.so.1
/usr/lib/wsl/lib/libnvidia-ml.so.1

# Native Linux location (incorrect for WSL2)
/usr/lib/x86_64-linux-gnu/libcuda.so
```

## Automatic Setup

Run the environment setup script which automatically detects WSL2 and configures the library path:

```bash
./scripts/ci/setup-environment.sh
```

The script will:
1. Detect WSL2 environment
2. Configure `LD_LIBRARY_PATH` to include `/usr/lib/wsl/lib`
3. Verify CUDA drivers are accessible
4. Save configuration to environment info file

## Manual Setup

If you need to configure manually, add to your shell profile (`~/.bashrc` or `~/.zshrc`):

```bash
# WSL2 NVIDIA Driver Path
if grep -qi microsoft /proc/version 2>/dev/null; then
    export LD_LIBRARY_PATH="/usr/lib/wsl/lib:$LD_LIBRARY_PATH"
fi
```

## Verification

### Check WSL2 Detection

```bash
grep -i microsoft /proc/version
```

Expected output:
```
Linux version 6.6.87.2-microsoft-standard-WSL2 ...
```

### Verify CUDA Drivers

```bash
ls -la /usr/lib/wsl/lib/libcuda*
```

Expected output:
```
-r-xr-xr-x 4 root root 175248 Aug 22 05:35 libcuda.so
-r-xr-xr-x 4 root root 175248 Aug 22 05:35 libcuda.so.1
-r-xr-xr-x 4 root root 175248 Aug 22 05:35 libcuda.so.1.1
```

### Test CUDA Availability

```bash
nvidia-smi
```

Expected output showing GPU information:
```
+-----------------------------------------------------------------------------+
| NVIDIA-SMI 581.15       Driver Version: 581.15       CUDA Version: 13.0     |
|-------------------------------+----------------------+----------------------+
| GPU  Name                 ...
```

### Run Tests

```bash
# Set library path
export LD_LIBRARY_PATH="/usr/lib/wsl/lib:$LD_LIBRARY_PATH"

# Run CUDA tests
dotnet test tests/Hardware/DotCompute.Hardware.Cuda.Tests/DotCompute.Hardware.Cuda.Tests.csproj --configuration Release
```

All 57 tests should pass.

## Common Issues

### Issue 1: CUDA_ERROR_NO_DEVICE (Error 100)

**Symptoms:**
```
Failed to initialize CUDA: 100
```

**Cause:** `LD_LIBRARY_PATH` does not include `/usr/lib/wsl/lib`

**Solution:**
```bash
export LD_LIBRARY_PATH="/usr/lib/wsl/lib:$LD_LIBRARY_PATH"
```

### Issue 2: Linux Drivers Installed in WSL2

**Symptoms:**
```
CUDA initialization fails
Conflicting library versions
```

**Cause:** Native Linux NVIDIA drivers installed in WSL2 (should NOT be installed)

**Solution:**
Remove Linux drivers:
```bash
sudo apt-get purge nvidia-*
sudo apt-get autoremove
```

**Important:** Only Windows host needs NVIDIA drivers. WSL2 guest should NOT have Linux drivers.

### Issue 3: Device Files Missing

**Symptoms:**
```
ls /dev/nvidia*
ls: cannot access '/dev/nvidia*': No such file or directory
```

**Cause:** This is NORMAL for WSL2. Device files don't exist in the same way as native Linux.

**Solution:** None needed. WSL2 uses a different GPU access mechanism through `/usr/lib/wsl/lib/`.

## Performance Considerations

WSL2 GPU performance is typically within 5-10% of native Windows CUDA performance for compute workloads. Some considerations:

- **Memory Transfer**: P2P transfers work normally
- **Kernel Launch**: Slight overhead compared to native Linux
- **Multi-GPU**: WSL2 officially supports only 1 GPU (may work with multiple)

## Debugging

### Enable Verbose CUDA Logging

```bash
export CUDA_VISIBLE_DEVICES=0
export CUDA_LAUNCH_BLOCKING=1
```

### Check Library Loading

```bash
ldd artifacts/bin/DotCompute.Backends.CUDA/Release/net9.0/DotCompute.Backends.CUDA.dll | grep cuda
```

Should show libraries from `/usr/lib/wsl/lib/`.

### Verify Compute Capability

```csharp
// In your code
using DotCompute.Backends.CUDA;
int capability = CudaCapabilityManager.GetTargetComputeCapability();
Console.WriteLine($"Compute Capability: {capability / 10}.{capability % 10}");
```

## References

- [NVIDIA CUDA on WSL2](https://docs.nvidia.com/cuda/wsl-user-guide/index.html)
- [WSL GPU Acceleration](https://learn.microsoft.com/en-us/windows/wsl/tutorials/gpu-compute)
- [DotCompute CUDA Backend Documentation](../backends/cuda-backend.md)

## Support

If you encounter issues:
1. Check this guide's Common Issues section
2. Verify setup with verification commands
3. Check GitHub issues: https://github.com/mivertowski/DotCompute/issues
4. Review CUDA backend logs in `artifacts/logs/`

---

**Last Updated:** November 2025
**Tested Configurations:**
- Windows 11 23H2 + WSL2 Ubuntu 22.04 + NVIDIA RTX 2000 Ada
- Driver: 581.15, CUDA: 13.0
