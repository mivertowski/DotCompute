// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Tests
{

/// <summary>
/// Simple test to detect RTX GPU and measure its capabilities.
/// </summary>
public class RTXGpuDetectionTest
{
    private readonly ITestOutputHelper _output;

    public RTXGpuDetectionTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void DetectCudaCapability()
    {
        _output.WriteLine("=== CUDA GPU Detection Test ===");
        
        try
        {
            // Check if CUDA runtime is available
            var cudaVersion = GetCudaRuntimeVersion();
            if (cudaVersion > 0)
            {
                _output.WriteLine($"CUDA Runtime Version: {cudaVersion / 1000}.{(cudaVersion % 1000) / 10}");
                
                // Get device count
                var deviceCount = GetCudaDeviceCount();
                _output.WriteLine($"CUDA Devices Found: {deviceCount}");
                
                if (deviceCount > 0)
                {
                    for (int i = 0; i < deviceCount; i++)
                    {
                        var props = GetDeviceProperties(i);
                        _output.WriteLine($"\nDevice {i}:");
                        _output.WriteLine($"  Name: {props.name}");
                        _output.WriteLine($"  Compute Capability: {props.major}.{props.minor}");
                        _output.WriteLine($"  Global Memory: {props.totalGlobalMem / (1024 * 1024 * 1024):F1} GB");
                        _output.WriteLine($"  Multiprocessors: {props.multiProcessorCount}");
                        _output.WriteLine($"  Max Threads per Block: {props.maxThreadsPerBlock}");
                        _output.WriteLine($"  Warp Size: {props.warpSize}");
                        
                        // Check if it's an RTX card
                        bool isRTX = props.name.Contains("RTX") || props.name.Contains("GeForce");
                        _output.WriteLine($"  Is RTX/GeForce: {isRTX}");
                        
                        if (isRTX)
                        {
                            _output.WriteLine($"  RTX GPU Detected! 🎉");
                            
                            // Test memory bandwidth
                            TestMemoryBandwidth(i);
                        }
                    }
                }
                else
                {
                    _output.WriteLine("No CUDA devices found");
                }
            }
            else
            {
                _output.WriteLine("CUDA Runtime not available");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Error during CUDA detection: {ex.Message}");
            _output.WriteLine($"This is expected if CUDA is not installed");
        }
    }

    private void TestMemoryBandwidth(int deviceId)
    {
        try
        {
            _output.WriteLine($"\n--- Memory Bandwidth Test for Device {deviceId} ---");
            
            const int dataSize = 64 * 1024 * 1024; // 64MB
            var hostData = new float[dataSize / sizeof(float)];
            
            // Initialize test data
            for (int i = 0; i < hostData.Length; i++)
            {
                hostData[i] = (float)i;
            }
            
            // Allocate device memory
            IntPtr devicePtr = IntPtr.Zero;
            var result = cudaMalloc(ref devicePtr, (ulong)dataSize);
            if (result != 0)
            {
                _output.WriteLine($"  Failed to allocate device memory: {result}");
                return;
            }
            
            try
            {
                // Measure Host to Device transfer
                var h2dStopwatch = Stopwatch.StartNew();
                var pinnedHandle = GCHandle.Alloc(hostData, GCHandleType.Pinned);
                try
                {
                    result = cudaMemcpy(devicePtr, pinnedHandle.AddrOfPinnedObject(), (ulong)dataSize, 1);
                    if (result != 0)
                    {
                        _output.WriteLine($"  Failed H2D copy: {result}");
                        return;
                    }
                }
                finally
                {
                    pinnedHandle.Free();
                }
                h2dStopwatch.Stop();
                
                // Measure Device to Host transfer
                var d2hStopwatch = Stopwatch.StartNew();
                var resultData = new float[hostData.Length];
                var resultHandle = GCHandle.Alloc(resultData, GCHandleType.Pinned);
                try
                {
                    result = cudaMemcpy(resultHandle.AddrOfPinnedObject(), devicePtr, (ulong)dataSize, 2);
                    if (result != 0)
                    {
                        _output.WriteLine($"  Failed D2H copy: {result}");
                        return;
                    }
                }
                finally
                {
                    resultHandle.Free();
                }
                d2hStopwatch.Stop();
                
                // Calculate bandwidth
                var h2dBandwidth = dataSize / h2dStopwatch.Elapsed.TotalSeconds / (1024 * 1024 * 1024);
                var d2hBandwidth = dataSize / d2hStopwatch.Elapsed.TotalSeconds / (1024 * 1024 * 1024);
                
                _output.WriteLine($"  Host to Device: {h2dBandwidth:F2} GB/s");
                _output.WriteLine($"  Device to Host: {d2hBandwidth:F2} GB/s");
                _output.WriteLine($"  Transfer size: {dataSize / (1024 * 1024)} MB");
                
                // Verify data integrity
                bool dataValid = true;
                for (int i = 0; i < Math.Min(100, hostData.Length); i++)
                {
                    if (Math.Abs(hostData[i] - resultData[i]) > 0.001f)
                    {
                        dataValid = false;
                        break;
                    }
                }
                _output.WriteLine($"  Data integrity: {(dataValid ? "PASS" : "FAIL")}");
            }
            finally
            {
                cudaFree(devicePtr);
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"  Memory bandwidth test failed: {ex.Message}");
        }
    }

    #region CUDA P/Invoke

    [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern int cudaRuntimeGetVersion(out int version);

    [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern int cudaGetDeviceCount(out int count);

    [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern int cudaGetDeviceProperties(out CudaDeviceProperties props, int device);

    [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern int cudaMalloc(ref IntPtr devPtr, ulong size);

    [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern int cudaFree(IntPtr devPtr);

    [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern int cudaMemcpy(IntPtr dst, IntPtr src, ulong count, int kind);

    [StructLayout(LayoutKind.Sequential)]
    private struct CudaDeviceProperties
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string name;
        public ulong totalGlobalMem;
        public ulong sharedMemPerBlock;
        public int regsPerBlock;
        public int warpSize;
        public ulong memPitch;
        public int maxThreadsPerBlock;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public int[] maxThreadsDim;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public int[] maxGridSize;
        public int clockRate;
        public ulong totalConstMem;
        public int major;
        public int minor;
        public ulong textureAlignment;
        public ulong texturePitchAlignment;
        public int deviceOverlap;
        public int multiProcessorCount;
        public int kernelExecTimeoutEnabled;
        public int integrated;
        public int canMapHostMemory;
        public int computeMode;
        public int maxTexture1D;
        public int maxTexture1DMipmap;
        public int maxTexture1DLinear;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public int[] maxTexture2D;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public int[] maxTexture2DMipmap;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public int[] maxTexture2DLinear;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public int[] maxTexture2DGather;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public int[] maxTexture3D;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public int[] maxTexture3DAlt;
        public int maxTextureCubemap;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public int[] maxTexture1DLayered;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public int[] maxTexture2DLayered;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public int[] maxTextureCubemapLayered;
        public int maxSurface1D;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public int[] maxSurface2D;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public int[] maxSurface3D;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public int[] maxSurface1DLayered;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public int[] maxSurface2DLayered;
        public int maxSurfaceCubemap;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public int[] maxSurfaceCubemapLayered;
        public ulong surfaceAlignment;
        public int concurrentKernels;
        public int ECCEnabled;
        public int pciBusID;
        public int pciDeviceID;
        public int pciDomainID;
        public int tccDriver;
        public int asyncEngineCount;
        public int unifiedAddressing;
        public int memoryClockRate;
        public int memoryBusWidth;
        public int l2CacheSize;
        public int maxThreadsPerMultiProcessor;
        public int streamPrioritiesSupported;
        public int globalL1CacheSupported;
        public int localL1CacheSupported;
        public ulong sharedMemPerMultiprocessor;
        public int regsPerMultiprocessor;
        public int managedMemory;
        public int isMultiGpuBoard;
        public int multiGpuBoardGroupID;
        public int hostNativeAtomicSupported;
        public int singleToDoublePrecisionPerfRatio;
        public int pageableMemoryAccess;
        public int concurrentManagedAccess;
        public int computePreemptionSupported;
        public int canUseHostPointerForRegisteredMem;
        public int cooperativeLaunch;
        public int cooperativeMultiDeviceLaunch;
        public ulong sharedMemPerBlockOptin;
        public int pageableMemoryAccessUsesHostPageTables;
        public int directManagedMemAccessFromHost;
    }

    #endregion

    private int GetCudaRuntimeVersion()
    {
        try
        {
            var result = cudaRuntimeGetVersion(out var version);
            return result == 0 ? version : 0;
        }
        catch
        {
            return 0;
        }
    }

    private int GetCudaDeviceCount()
    {
        try
        {
            var result = cudaGetDeviceCount(out var count);
            return result == 0 ? count : 0;
        }
        catch
        {
            return 0;
        }
    }

    private CudaDeviceProperties GetDeviceProperties(int deviceId)
    {
        var result = cudaGetDeviceProperties(out var props, deviceId);
        if (result != 0)
        {
            throw new InvalidOperationException($"Failed to get device properties: {result}");
        }
        return props;
    }
}}
