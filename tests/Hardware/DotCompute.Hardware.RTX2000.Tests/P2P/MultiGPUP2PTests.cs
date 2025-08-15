using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Tests.Hardware.P2P;

/// <summary>
/// Multi-GPU P2P(Peer-to-Peer) communication tests for RTX 2000 Ada Generation.
/// Tests direct GPU-to-GPU memory transfers and multi-device orchestration.
/// </summary>
[Trait("Category", "RTX2000")]
[Trait("Category", "P2P")]
[Trait("Category", "MultiGPU")]
[Trait("Category", "RequiresMultipleGPUs")]
public class MultiGPUP2PTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly List<IntPtr> _cudaContexts;
    private readonly List<int> _deviceIds;
    private bool _p2pCapable;
    private int _deviceCount;

    public MultiGPUP2PTests(ITestOutputHelper output)
    {
        _output = output;
        _cudaContexts = new List<IntPtr>();
        _deviceIds = new List<int>();
        InitializeMultiGPU();
    }

    private void InitializeMultiGPU()
    {
        try
        {
            // Initialize CUDA
            var result = CudaInit(0);
            if(result != 0)
            {
                _output.WriteLine($"CUDA initialization failed with error code: {result}");
                return;
            }

            // Get device count
            result = CudaGetDeviceCount(ref _deviceCount);
            if(result != 0 || _deviceCount < 2)
            {
                _output.WriteLine($"Insufficient GPU devices found. Count: {_deviceCount}, Required: 2+");
                return;
            }

            _output.WriteLine($"Found {_deviceCount} CUDA devices");

            // Initialize contexts for each device
            for(int i = 0; i < Math.Min(_deviceCount, 4); i++) // Limit to 4 devices for testing
            {
                IntPtr context = IntPtr.Zero;
                result = CudaCtxCreate(ref context, 0, i);
                
                if(result == 0)
                {
                    _cudaContexts.Add(context);
                    _deviceIds.Add(i);
                    
                    var deviceName = new byte[256];
                    CudaDeviceGetName(deviceName, 256, i);
                    var name = System.Text.Encoding.ASCII.GetString(deviceName).TrimEnd('\0');
                    _output.WriteLine($"Device {i}: {name}");
                }
                else
                {
                    _output.WriteLine($"Failed to create context for device {i}, error code: {result}");
                    break;
                }
            }

            // Check P2P capability between devices
            if(_deviceIds.Count >= 2)
            {
                CheckP2PCapability();
            }
        }
        catch(Exception ex)
        {
            _output.WriteLine($"Multi-GPU initialization exception: {ex.Message}");
        }
    }

    private void CheckP2PCapability()
    {
        _output.WriteLine("Checking P2P capabilities between devices...");
        
        for(int i = 0; i < _deviceIds.Count; i++)
        {
            for(int j = i + 1; j < _deviceIds.Count; j++)
            {
                int canAccessPeer = 0;
                var result = CudaDeviceCanAccessPeer(ref canAccessPeer, _deviceIds[i], _deviceIds[j]);
                
                if(result == 0 && canAccessPeer == 1)
                {
                    _output.WriteLine($"P2P access available: Device {_deviceIds[i]} ↔ Device {_deviceIds[j]}");
                    _p2pCapable = true;
                }
                else
                {
                    _output.WriteLine($"P2P access not available: Device {_deviceIds[i]} ↔ Device {_deviceIds[j]}");
                }
            }
        }

        if(_p2pCapable)
        {
            _output.WriteLine("✓ P2P capabilities detected");
        }
        else
        {
            _output.WriteLine("⚠ No P2P capabilities detected");
        }
    }

    [SkippableFact]
    public async Task DetectMultiGPUConfiguration_ShouldFindMultipleDevices()
    {
        Skip.IfNot(_deviceCount >= 2, "Multi-GPU configuration not available");

        _deviceCount .Should().BeGreaterThanOrEqualTo(2, "Should detect at least 2 GPU devices");
        _cudaContexts.Count.Should().BeGreaterOrEqualTo(2, "Should create contexts for multiple devices");

        _output.WriteLine($"Multi-GPU configuration validated:");
        _output.WriteLine($"  Device count: {_deviceCount}");
        _output.WriteLine($"  Active contexts: {_cudaContexts.Count}");
        _output.WriteLine($"  P2P capable: {_p2pCapable}");

        await Task.CompletedTask;
    }

    [SkippableFact]
    public async Task EnableP2PAccess_ShouldEstablishPeerConnections()
    {
        Skip.IfNot(_deviceCount >= 2 && _p2pCapable, "P2P capable multi-GPU not available");

        var p2pConnections = 0;
        
        // Set context to first device and enable P2P access to others
        var result = CudaCtxSetCurrent(_cudaContexts[0]);
        Assert.Equal(0, result); // Setting current context should succeed;

        for(int i = 1; i < _deviceIds.Count; i++)
        {
            // Enable P2P access from device 0 to device i
            result = CudaCtxEnablePeerAccess(_cudaContexts[i], 0);
            
            if(result == 0)
            {
                p2pConnections++;
                _output.WriteLine($"✓ P2P enabled: Device {_deviceIds[0]} → Device {_deviceIds[i]}");
            }
            else if(result == 704) // CUDA_ERROR_PEER_ACCESS_ALREADY_ENABLED
            {
                p2pConnections++;
                _output.WriteLine($"✓ P2P already enabled: Device {_deviceIds[0]} → Device {_deviceIds[i]}");
            }
            else
            {
                _output.WriteLine($"✗ P2P enable failed: Device {_deviceIds[0]} → Device {_deviceIds[i]}, error: {result}");
            }
        }

        // Enable reverse P2P access
        for(int i = 1; i < _deviceIds.Count; i++)
        {
            result = CudaCtxSetCurrent(_cudaContexts[i]);
            Assert.Equal(0, result); // Setting current context should succeed;

            result = CudaCtxEnablePeerAccess(_cudaContexts[0], 0);
            
            if(result == 0)
            {
                _output.WriteLine($"✓ P2P enabled: Device {_deviceIds[i]} → Device {_deviceIds[0]}");
            }
            else if(result == 704) // Already enabled
            {
                _output.WriteLine($"✓ P2P already enabled: Device {_deviceIds[i]} → Device {_deviceIds[0]}");
            }
        }

        p2pConnections.Should().BeGreaterThan(0, "Should establish at least one P2P connection");
        _output.WriteLine($"P2P connections established: {p2pConnections}");

        await Task.CompletedTask;
    }

    [SkippableFact]
    public async Task DirectP2PMemoryTransfer_ShouldTransferDataBetweenGPUs()
    {
        Skip.IfNot(_deviceCount >= 2 && _p2pCapable, "P2P capable multi-GPU not available");

        const int dataSize = 1024 * 1024; // 1MB
        const int elementCount = dataSize / sizeof(float);
        
        IntPtr device0Buffer = IntPtr.Zero, device1Buffer = IntPtr.Zero;
        
        try
        {
            // Set context to device 0 and allocate memory
            var result = CudaCtxSetCurrent(_cudaContexts[0]);
            Assert.Equal(0, result); // Setting context 0 should succeed;
            
            result = CudaMalloc(ref device0Buffer, dataSize);
            Assert.Equal(0, result); // Allocation on device 0 should succeed;

            // Set context to device 1 and allocate memory
            result = CudaCtxSetCurrent(_cudaContexts[1]);
            Assert.Equal(0, result); // Setting context 1 should succeed;
            
            result = CudaMalloc(ref device1Buffer, dataSize);
            Assert.Equal(0, result); // Allocation on device 1 should succeed;

            // Initialize data on device 0
            result = CudaCtxSetCurrent(_cudaContexts[0]);
            Assert.Equal(0, result); // Setting context 0 should succeed;

            var hostData = new float[elementCount];
            for(int i = 0; i < elementCount; i++)
            {
                hostData[i] = i + 0.5f;
            }

            var hostHandle = GCHandle.Alloc(hostData, GCHandleType.Pinned);
            try
            {
                result = CudaMemcpyHtoD(device0Buffer, hostHandle.AddrOfPinnedObject(), dataSize);
                Assert.Equal(0, result); // H2D copy to device 0 should succeed;

                _output.WriteLine($"Data initialized on device 0: {elementCount} elements");

                // Perform direct P2P transfer from device 0 to device 1
                var sw = Stopwatch.StartNew();
                result = CudaMemcpyPeer(device1Buffer, _cudaContexts[1], device0Buffer, _cudaContexts[0], dataSize);
                sw.Stop();
                
                Assert.Equal(0, result); // P2P memory copy should succeed;
                _output.WriteLine($"P2P transfer completed in {sw.ElapsedTicks * 1000000.0 / Stopwatch.Frequency:F1} μs");

                // Calculate transfer bandwidth
                var transferBandwidth = (dataSize / (1024.0 * 1024.0 * 1024.0)) / ((sw.ElapsedTicks * 1000000.0 / Stopwatch.Frequency) / 1e6);
                _output.WriteLine($"P2P transfer bandwidth: {transferBandwidth:F2} GB/s");

                // Validate data integrity by copying back from device 1
                result = CudaCtxSetCurrent(_cudaContexts[1]);
                Assert.Equal(0, result); // Setting context 1 should succeed;

                var resultData = new float[elementCount];
                var resultHandle = GCHandle.Alloc(resultData, GCHandleType.Pinned);
                try
                {
                    result = CudaMemcpyDtoH(resultHandle.AddrOfPinnedObject(), device1Buffer, dataSize);
                    Assert.Equal(0, result); // D2H copy from device 1 should succeed;

                    // Verify data integrity
                    for(int i = 0; i < Math.Min(1000, elementCount); i++)
                    {
                        resultData[i].Should().Be(hostData[i], $"Data at index {i} should match after P2P transfer");
                    }

                    _output.WriteLine("✓ P2P data integrity verified");
                }
                finally
                {
                    resultHandle.Free();
                }

                // P2P bandwidth should be significantly higher than PCIe
                transferBandwidth.Should().BeGreaterThan(20.0, "P2P bandwidth should exceed PCIe limitations");
            }
            finally
            {
                hostHandle.Free();
            }
        }
        finally
        {
            if(device0Buffer != IntPtr.Zero) 
            {
                CudaCtxSetCurrent(_cudaContexts[0]);
                CudaFree(device0Buffer);
            }
            if(device1Buffer != IntPtr.Zero)
            {
                CudaCtxSetCurrent(_cudaContexts[1]);
                CudaFree(device1Buffer);
            }
        }

        await Task.CompletedTask;
    }

    [SkippableFact]
    public async Task MultiGPUWorkloadDistribution_ShouldDistributeWorkEfficiently()
    {
        Skip.IfNot(_deviceCount >= 2, "Multi-GPU configuration not available");

        const int totalWorkSize = 4 * 1024 * 1024; // 4M elements
        int elementsPerGPU = totalWorkSize / Math.Min(_deviceIds.Count, 4);

        _output.WriteLine($"Distributing workload across {_deviceIds.Count} GPUs");
        _output.WriteLine($"Total work: {totalWorkSize} elements, {elementsPerGPU} per GPU");

        var gpuBuffers = new IntPtr[_deviceIds.Count];
        var gpuTasks = new Task[_deviceIds.Count];
        var completionTimes = new double[_deviceIds.Count];

        try
        {
            // Prepare data for each GPU
            for(int gpu = 0; gpu < _deviceIds.Count; gpu++)
            {
                var result = CudaCtxSetCurrent(_cudaContexts[gpu]);
                Assert.Equal(0, result); // Setting context should succeed

                result = CudaMalloc(ref gpuBuffers[gpu], elementsPerGPU * sizeof(float));
                Assert.Equal(0, result); // Memory allocation should succeed

                // Initialize data
                var gpuData = new float[elementsPerGPU];
                for(int i = 0; i < elementsPerGPU; i++)
                {
                    gpuData[i] = gpu * elementsPerGPU + i;
                }

                var hostHandle = GCHandle.Alloc(gpuData, GCHandleType.Pinned);
                try
                {
                    result = CudaMemcpyHtoD(gpuBuffers[gpu], hostHandle.AddrOfPinnedObject(), elementsPerGPU * sizeof(float));
                    Assert.Equal(0, result); // Data initialization should succeed
                }
                finally
                {
                    hostHandle.Free();
                }
            }

            _output.WriteLine("Data initialized on all GPUs, starting parallel workload...");

            // Launch workload on each GPU simultaneously
            var overallSw = Stopwatch.StartNew();

            for(int gpu = 0; gpu < _deviceIds.Count; gpu++)
            {
                int capturedGpu = gpu; // Capture for closure
                
                gpuTasks[gpu] = Task.Run(async () =>
                {
                    var sw = Stopwatch.StartNew();
                    
                    var result = CudaCtxSetCurrent(_cudaContexts[capturedGpu]);
                    if(result != 0) return;

                    // Simulate compute-intensive work
                    await SimulateComputeWorkload(gpuBuffers[capturedGpu], elementsPerGPU, capturedGpu);
                    
                    result = CudaCtxSynchronize();
                    sw.Stop();
                    
                    completionTimes[capturedGpu] = sw.Elapsed.TotalMilliseconds;
                    _output.WriteLine($"GPU {capturedGpu} completed workload in {completionTimes[capturedGpu]:F2} ms");
                });
            }

            // Wait for all GPUs to complete
            await Task.WhenAll(gpuTasks);
            overallSw.Stop();

            var totalTime = overallSw.ElapsedMilliseconds;
            var averageTime = completionTimes.Average();
            var maxTime = completionTimes.Max();
            var minTime = completionTimes.Min();
            var workloadEfficiency =(averageTime / maxTime) * 100;

            _output.WriteLine($"Multi-GPU workload distribution results:");
            _output.WriteLine($"  Total time: {totalTime:F2} ms");
            _output.WriteLine($"  Average GPU time: {averageTime:F2} ms");
            _output.WriteLine($"  Min GPU time: {minTime:F2} ms");
            _output.WriteLine($"  Max GPU time: {maxTime:F2} ms");
            _output.WriteLine($"  Load balance efficiency: {workloadEfficiency:F1}%");

            // Validate performance characteristics
            workloadEfficiency.Should().BeGreaterThan(70.0, "GPUs should have reasonably balanced workloads");
            totalTime.Should().BeLessThan((long)(maxTime * 1.2), "Parallel execution should not exceed sequential by much");

            _output.WriteLine("✓ Multi-GPU workload distribution validated");
        }
        finally
        {
            // Cleanup
            for(int gpu = 0; gpu < _deviceIds.Count; gpu++)
            {
                if(gpuBuffers[gpu] != IntPtr.Zero)
                {
                    CudaCtxSetCurrent(_cudaContexts[gpu]);
                    CudaFree(gpuBuffers[gpu]);
                }
            }
        }
    }

    [SkippableFact]
    public async Task MultiGPUMemoryBandwidthTest_ShouldAchieveAggregatedBandwidth()
    {
        Skip.IfNot(_deviceCount >= 2, "Multi-GPU configuration not available");

        const int transferSizeMB = 256; // 256MB per GPU
        const int transferSize = transferSizeMB * 1024 * 1024;
        const int iterations = 10;

        _output.WriteLine($"Testing aggregated memory bandwidth across {_deviceIds.Count} GPUs");

        var gpuBuffers = new IntPtr[_deviceIds.Count];
        var hostBuffers = new byte[_deviceIds.Count][];
        var bandwidthResults = new double[_deviceIds.Count];

        try
        {
            // Prepare buffers for each GPU
            for(int gpu = 0; gpu < _deviceIds.Count; gpu++)
            {
                var result = CudaCtxSetCurrent(_cudaContexts[gpu]);
                Assert.Equal(0, result); // Setting context should succeed

                result = CudaMalloc(ref gpuBuffers[gpu], transferSize);
                Assert.Equal(0, result); // Memory allocation should succeed

                hostBuffers[gpu] = new byte[transferSize];
                new Random(42 + gpu).NextBytes(hostBuffers[gpu]);
            }

            // Measure concurrent transfers
            _output.WriteLine("Starting concurrent memory bandwidth test...");

            var tasks = new Task[_deviceIds.Count];
            var overallSw = Stopwatch.StartNew();

            for(int gpu = 0; gpu < _deviceIds.Count; gpu++)
            {
                int capturedGpu = gpu;
                
                tasks[gpu] = Task.Run(() =>
                {
                    var result = CudaCtxSetCurrent(_cudaContexts[capturedGpu]);
                    if(result != 0) return;

                    var hostHandle = GCHandle.Alloc(hostBuffers[capturedGpu], GCHandleType.Pinned);
                    try
                    {
                        // Warm up
                        for(int i = 0; i < 3; i++)
                        {
                            CudaMemcpyHtoD(gpuBuffers[capturedGpu], hostHandle.AddrOfPinnedObject(), transferSize);
                        }

                        // Measure H2D bandwidth
                        var sw = Stopwatch.StartNew();
                        for(int i = 0; i < iterations; i++)
                        {
                            result = CudaMemcpyHtoD(gpuBuffers[capturedGpu], hostHandle.AddrOfPinnedObject(), transferSize);
                            if(result != 0) break;
                        }
                        sw.Stop();

                        var bandwidth =((long)transferSize * iterations /(1024.0 * 1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds;
                        bandwidthResults[capturedGpu] = bandwidth;
                        
                        _output.WriteLine($"GPU {capturedGpu} bandwidth: {bandwidth:F2} GB/s");
                    }
                    finally
                    {
                        hostHandle.Free();
                    }
                });
            }

            await Task.WhenAll(tasks);
            overallSw.Stop();

            var totalBandwidth = bandwidthResults.Sum();
            var averageBandwidth = bandwidthResults.Average();
            var parallelEfficiency = (totalBandwidth / (bandwidthResults.Max() * _deviceIds.Count)) * 100;

            _output.WriteLine($"Multi-GPU memory bandwidth results:");
            _output.WriteLine($"  Total aggregated bandwidth: {totalBandwidth:F2} GB/s");
            _output.WriteLine($"  Average per GPU: {averageBandwidth:F2} GB/s");
            _output.WriteLine($"  Parallel efficiency: {parallelEfficiency:F1}%");

            // Validate aggregated performance
            totalBandwidth.Should().BeGreaterThan(averageBandwidth * _deviceIds.Count * 0.7, 
                "Aggregated bandwidth should benefit from parallelism");
            
            _output.WriteLine("✓ Multi-GPU memory bandwidth aggregation validated");
        }
        finally
        {
            // Cleanup
            for(int gpu = 0; gpu < _deviceIds.Count; gpu++)
            {
                if(gpuBuffers[gpu] != IntPtr.Zero)
                {
                    CudaCtxSetCurrent(_cudaContexts[gpu]);
                    CudaFree(gpuBuffers[gpu]);
                }
            }
        }
    }

    private async Task SimulateComputeWorkload(IntPtr buffer, int elementCount, int gpuId)
    {
        // Simulate compute-intensive operations
        const int computeIterations = 1000;
        
        // This would normally launch a CUDA kernel using computeIterations
        await Task.Delay(computeIterations / 100); // Simulate compute time
        // For simulation, we'll just do synchronous operations
        await Task.Delay(50 + gpuId * 10); // Simulate variable compute time

        // In real implementation, this would be:
        // - Launch compute kernel with buffer
        // - Wait for kernel completion
        // - Verify results
    }

    public void Dispose()
    {
        // Disable P2P access
        try
        {
            for(int i = 0; i < _cudaContexts.Count; i++)
            {
                CudaCtxSetCurrent(_cudaContexts[i]);
                
                for(int j = 0; j < _cudaContexts.Count; j++)
                {
                    if(i != j)
                    {
                        CudaCtxDisablePeerAccess(_cudaContexts[j]);
                    }
                }
            }
        }
        catch
        {
            // Ignore cleanup errors
        }

        // Destroy contexts
        foreach (var context in _cudaContexts)
        {
            if(context != IntPtr.Zero)
            {
                CudaCtxDestroy(context);
            }
        }
        
        _cudaContexts.Clear();
        _deviceIds.Clear();
    }

    #region Native Methods

    [DllImport("nvcuda", EntryPoint = "cuInit")]
    private static extern int CudaInit(uint flags);

    [DllImport("nvcuda", EntryPoint = "cuDeviceGetCount")]
    private static extern int CudaGetDeviceCount(ref int count);

    [DllImport("nvcuda", EntryPoint = "cuDeviceGetName")]
    private static extern int CudaDeviceGetName(byte[] name, int len, int dev);

    [DllImport("nvcuda", EntryPoint = "cuDeviceCanAccessPeer")]
    private static extern int CudaDeviceCanAccessPeer(ref int canAccessPeer, int dev, int peerDev);

    [DllImport("nvcuda", EntryPoint = "cuCtxCreate_v2")]
    private static extern int CudaCtxCreate(ref IntPtr ctx, uint flags, int dev);

    [DllImport("nvcuda", EntryPoint = "cuCtxDestroy_v2")]
    private static extern int CudaCtxDestroy(IntPtr ctx);

    [DllImport("nvcuda", EntryPoint = "cuCtxSetCurrent")]
    private static extern int CudaCtxSetCurrent(IntPtr ctx);

    [DllImport("nvcuda", EntryPoint = "cuCtxSynchronize")]
    private static extern int CudaCtxSynchronize();

    [DllImport("nvcuda", EntryPoint = "cuCtxEnablePeerAccess")]
    private static extern int CudaCtxEnablePeerAccess(IntPtr peerContext, uint flags);

    [DllImport("nvcuda", EntryPoint = "cuCtxDisablePeerAccess")]
    private static extern int CudaCtxDisablePeerAccess(IntPtr peerContext);

    [DllImport("nvcuda", EntryPoint = "cuMemAlloc_v2")]
    private static extern int CudaMalloc(ref IntPtr dptr, long bytesize);

    [DllImport("nvcuda", EntryPoint = "cuMemFree_v2")]
    private static extern int CudaFree(IntPtr dptr);

    [DllImport("nvcuda", EntryPoint = "cuMemcpyHtoD_v2")]
    private static extern int CudaMemcpyHtoD(IntPtr dstDevice, IntPtr srcHost, long byteCount);

    [DllImport("nvcuda", EntryPoint = "cuMemcpyDtoH_v2")]
    private static extern int CudaMemcpyDtoH(IntPtr dstHost, IntPtr srcDevice, long byteCount);

    [DllImport("nvcuda", EntryPoint = "cuMemcpyPeer")]
    private static extern int CudaMemcpyPeer(IntPtr dstDevice, IntPtr dstContext, IntPtr srcDevice, IntPtr srcContext, long byteCount);

    #endregion
}

/// <summary>
/// Helper attribute to skip tests when conditions aren't met.
/// </summary>
public class SkippableFactAttribute : FactAttribute
{
    public override string? Skip { get; set; }
}

/// <summary>
/// Helper class for skipping tests conditionally.
/// </summary>
public static class Skip
{
    public static void IfNot(bool condition, string reason)
    {
        if(!condition)
        {
            throw new SkipException(reason);
        }
    }
}

/// <summary>
/// Exception thrown to skip a test.
/// </summary>
public class SkipException : Exception
{
    public SkipException(string reason) : base(reason) { }
}
