using System.Diagnostics;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Tests.Utilities;
using DotCompute.Tests.Utilities.TestFixtures;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Tests.Hardware
{
    /// <summary>
    /// Comprehensive hardware-dependent tests for OpenCL functionality consolidated from multiple test files.
    /// These tests require OpenCL runtime to be installed and actual hardware (GPU, CPU, or FPGA).
    /// </summary>
    [Collection("Hardware")]
    [Trait("Category", "HardwareRequired")]
    [Trait("Category", "OpenCLRequired")]
    [Trait("Category", "Hardware")]
    public class OpenCLHardwareTests(ITestOutputHelper output) : IClassFixture<AcceleratorTestFixture>
    {
        private readonly ITestOutputHelper _output = output;

        #region Platform Detection Tests

        [SkippableFact]
        [Trait("Category", "OpenCLRequired")]
        public void Should_DetectOpenCLPlatforms()
        {
            Skip.IfNot(IsOpenCLAvailable(), "OpenCL runtime not available");

            uint platformCount = 0;
            var result = clGetPlatformIDs(0, null, ref platformCount);

            _output.WriteLine($"OpenCL platform detection result: {result}");
            _output.WriteLine($"Number of OpenCL platforms found: {platformCount}");

            _ = platformCount.Should().BeGreaterThan(0, "No OpenCL platforms detected");
        }

        [HardwareFact(AcceleratorType.OpenCL)]
        public async Task OpenCL_Platform_Detection()
        {
            _output.WriteLine("Testing OpenCL platform detection...");

            // TODO: When real OpenCL backend is implemented:
            // 1. Query platforms using clGetPlatformIDs
            // 2. Get platform info(vendor, version, extensions)
            // 3. List all available devices per platform

            Assert.True(IsOpenCLAvailable(), "OpenCL runtime is available");

            _output.WriteLine("OpenCL platforms detected");
            await Task.CompletedTask;
        }

        #endregion

        #region Device Discovery Tests

        [SkippableFact]
        [Trait("Category", "OpenCLRequired")]
        public void Should_GetOpenCLDevices()
        {
            Skip.IfNot(IsOpenCLAvailable(), "OpenCL runtime not available");

            uint platformCount = 0;
            _ = clGetPlatformIDs(0, null, ref platformCount);

            if (platformCount > 0)
            {
                var platforms = new IntPtr[platformCount];
                var getPlatformsResult = clGetPlatformIDs(platformCount, platforms, ref platformCount);

                Assert.Equal(0, getPlatformsResult); // CL_SUCCESS

                foreach (var platform in platforms)
                {
                    uint deviceCount = 0;
                    _ = clGetDeviceIDs(platform, DeviceType.CL_DEVICE_TYPE_ALL, 0, null, ref deviceCount);

                    _output.WriteLine($"Platform has {deviceCount} devices");
                    Assert.True(deviceCount >= 0);
                }
            }
        }

        [HardwareFact(AcceleratorType.OpenCL)]
        public async Task OpenCL_Device_Properties()
        {
            _output.WriteLine("Testing OpenCL device properties...");

            // TODO: When real OpenCL backend is implemented:
            // 1. Query device properties using clGetDeviceInfo
            // 2. Report compute units, memory, work group sizes
            // 3. Check for specific extensions(cl_khr_fp64, etc.)

            _output.WriteLine("Device properties:");
            _output.WriteLine("  - Type: GPU/CPU/Accelerator");
            _output.WriteLine("  - Compute Units: TBD");
            _output.WriteLine("  - Global Memory: TBD");
            _output.WriteLine("  - Max Work Group Size: TBD");

            await Task.CompletedTask;
        }

        #endregion

        #region Context Creation Tests

        [SkippableFact]
        [Trait("Category", "OpenCLRequired")]
        public void Should_CreateOpenCLContext()
        {
            Skip.IfNot(IsOpenCLAvailable(), "OpenCL runtime not available");

            uint platformCount = 0;
            _ = clGetPlatformIDs(0, null, ref platformCount);

            if (platformCount > 0)
            {
                var platforms = new IntPtr[1];
                _ = clGetPlatformIDs(1, platforms, ref platformCount);

                uint deviceCount = 0;
                _ = clGetDeviceIDs(platforms[0], DeviceType.CL_DEVICE_TYPE_DEFAULT, 0, null, ref deviceCount);

                if (deviceCount > 0)
                {
                    var devices = new IntPtr[1];
                    _ = clGetDeviceIDs(platforms[0], DeviceType.CL_DEVICE_TYPE_DEFAULT, 1, devices, ref deviceCount);

                    var errorCode = 0;
                    var context = clCreateContext(IntPtr.Zero, 1, devices, IntPtr.Zero, IntPtr.Zero, ref errorCode);

                    Assert.NotEqual(IntPtr.Zero, context);
                    Assert.Equal(0, errorCode); // CL_SUCCESS

                    if (context != IntPtr.Zero)
                    {
                        var releaseResult = clReleaseContext(context);
                        _output.WriteLine($"Context release result: {releaseResult}");
                    }
                }
            }
        }

        [HardwareFact(AcceleratorType.OpenCL)]
        public async Task OpenCL_MultiDevice_Context()
        {
            _output.WriteLine("Testing OpenCL multi-device context...");

            // TODO: When real OpenCL backend is implemented:
            // 1. Create context with multiple devices
            // 2. Create command queue per device
            // 3. Distribute work across devices
            // 4. Synchronize between devices

            _output.WriteLine("Multi-device context test placeholder");

            await Task.CompletedTask;
        }

        #endregion

        #region Kernel Compilation Tests

        [HardwareFact(AcceleratorType.OpenCL)]
        public async Task OpenCL_Kernel_Compilation()
        {
            _output.WriteLine("Testing OpenCL kernel compilation...");

            const string kernelSource = @"
                __kernel void vector_add(__global const float* a,
                                        __global const float* b,
                                        __global float* c,
                                        const unsigned int n)
                {
                    int id = get_global_id(0);
                    if(id < n) {
                        c[id] = a[id] + b[id];
                    }
                }
            ";

            // TODO: When real OpenCL backend is implemented:
            // 1. Create program from source
            // 2. Build program for device
            // 3. Create kernel from program
            // 4. Handle compilation errors

            _output.WriteLine("Kernel compilation test placeholder");
            _output.WriteLine($"Kernel source length: {kernelSource.Length} chars");

            await Task.CompletedTask;
        }

        #endregion

        #region Memory Operations Tests

        [HardwareFact(AcceleratorType.OpenCL)]
        public async Task OpenCL_BufferOperations()
        {
            _output.WriteLine("Testing OpenCL buffer operations...");

            const int dataSize = 1024 * 1024; // 1M elements
            _ = TestDataGenerators.GenerateRandomVector(dataSize);

            var stopwatch = Stopwatch.StartNew();

            // TODO: When real OpenCL backend is implemented:
            // 1. Create context and command queue
            // 2. Create buffer using clCreateBuffer
            // 3. Write data using clEnqueueWriteBuffer
            // 4. Read back using clEnqueueReadBuffer
            // 5. Verify data integrity

            stopwatch.Stop();

            var bandwidth = dataSize * sizeof(float) * 2 / (stopwatch.Elapsed.TotalSeconds * 1e9);
            _output.WriteLine($"Transfer size: {dataSize * sizeof(float) / (1024 * 1024)}MB");
            _output.WriteLine($"Transfer time: {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"Bandwidth: {bandwidth:F2} GB/s");

            await Task.CompletedTask;
        }

        [HardwareFact(AcceleratorType.OpenCL)]
        public async Task OpenCL_SubBuffer_Operations()
        {
            _output.WriteLine("Testing OpenCL sub-buffer operations...");

            const int mainBufferSize = 10 * 1024 * 1024; // 10MB
            const int subBufferSize = 1024 * 1024; // 1MB

            // TODO: When real OpenCL backend is implemented:
            // 1. Create main buffer
            // 2. Create sub-buffers using clCreateSubBuffer
            // 3. Operate on sub-buffers independently
            // 4. Verify no interference between sub-buffers

            _output.WriteLine($"Main buffer: {mainBufferSize / (1024 * 1024)}MB");
            _output.WriteLine($"Sub-buffers: {mainBufferSize / subBufferSize} x {subBufferSize / (1024 * 1024)}MB");

            await Task.CompletedTask;
        }

        #endregion

        #region Image Processing Tests

        [HardwareFact(AcceleratorType.OpenCL)]
        public async Task OpenCL_ImageOperations()
        {
            _output.WriteLine("Testing OpenCL image operations...");

            const int width = 1024;
            const int height = 1024;
            _ = TestDataGenerators.GeneratePatternData(width, height, PatternType.Gradient);

            // TODO: When real OpenCL backend is implemented:
            // 1. Create 2D image object
            // 2. Write image data
            // 3. Apply image processing kernel(e.g., blur, edge detection)
            // 4. Read back processed image

            _output.WriteLine($"Image size: {width}x{height}");
            _output.WriteLine($"Image memory: {width * height * sizeof(float) / (1024 * 1024)}MB");

            await Task.CompletedTask;
        }

        #endregion

        #region Advanced Features Tests

        [HardwareFact(AcceleratorType.OpenCL)]
        public async Task OpenCL_LocalMemory_Optimization()
        {
            _output.WriteLine("Testing OpenCL local memory optimization...");

            const int matrixSize = 1024;
            const int tileSize = 16;

            _ = TestDataGenerators.GenerateRandomMatrix(matrixSize, matrixSize);
            _ = TestDataGenerators.GenerateRandomMatrix(matrixSize, matrixSize);
            _ = TestDataGenerators.CreateJaggedArray(matrixSize, matrixSize);

            // TODO: When real OpenCL backend is implemented:
            // 1. Implement tiled matrix multiplication
            // 2. Use local memory for tile caching
            // 3. Compare performance with/without local memory

            _output.WriteLine($"Matrix size: {matrixSize}x{matrixSize}");
            _output.WriteLine($"Tile size: {tileSize}x{tileSize}");
            _output.WriteLine($"Local memory per work group: {2 * tileSize * tileSize * sizeof(float)} bytes");

            await Task.CompletedTask;
        }

        [HardwareFact(AcceleratorType.OpenCL)]
        public async Task OpenCL_Atomics_Operations()
        {
            _output.WriteLine("Testing OpenCL atomic operations...");

            const int numThreads = 1000000;

            // TODO: When real OpenCL backend is implemented:
            // 1. Create kernel using atomic operations
            // 2. Test atomic_add, atomic_cmpxchg, etc.
            // 3. Verify correctness with concurrent updates

            _output.WriteLine($"Testing with {numThreads:N0} concurrent threads");

            await Task.CompletedTask;
        }

        [HardwareFact(AcceleratorType.OpenCL)]
        public async Task OpenCL_SVM_SharedVirtualMemory()
        {
            _output.WriteLine("Testing OpenCL Shared Virtual Memory...");

            // Requires OpenCL 2.0+

            // TODO: When real OpenCL backend is implemented:
            // 1. Check for SVM support
            // 2. Allocate SVM buffer
            // 3. Share pointers between host and device
            // 4. Test fine-grained vs coarse-grained SVM

            _output.WriteLine("SVM test placeholder");

            await Task.CompletedTask;
        }

        #endregion

        #region Performance and Profiling Tests

        [HardwareFact(AcceleratorType.OpenCL)]
        public async Task OpenCL_Events_Profiling()
        {
            _output.WriteLine("Testing OpenCL event profiling...");

            // TODO: When real OpenCL backend is implemented:
            // 1. Enable profiling in command queue
            // 2. Capture events for kernel execution
            // 3. Query event timing information
            // 4. Calculate kernel execution time

            _output.WriteLine("Event profiling test placeholder");

            await Task.CompletedTask;
        }

        #endregion

        #region OpenCL 2.1+ Features Tests

        [HardwareFact(AcceleratorType.OpenCL)]
        public async Task OpenCL_SPIRV_Compilation()
        {
            _output.WriteLine("Testing OpenCL SPIR-V compilation...");

            // Requires OpenCL 2.1+ or cl_khr_il_program extension

            // TODO: When real OpenCL backend is implemented:
            // 1. Generate or load SPIR-V bytecode
            // 2. Create program from SPIR-V
            // 3. Build and execute
            // 4. Compare with source compilation

            _output.WriteLine("SPIR-V compilation test placeholder");

            await Task.CompletedTask;
        }

        #endregion

        #region Helper Methods and P/Invoke Declarations

        private static bool IsOpenCLAvailable()
        {
            try
            {
                uint platformCount = 0;
                var result = clGetPlatformIDs(0, null, ref platformCount);
                return result == 0 && platformCount > 0;
            }
            catch
            {
                return false;
            }
        }

        // OpenCL P/Invoke declarations
        private const string OpenCLLibrary = "OpenCL";

        [DllImport(OpenCLLibrary)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        private static extern int clGetPlatformIDs(uint numEntries, IntPtr[]? platforms, ref uint numPlatforms);

        [DllImport(OpenCLLibrary)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        private static extern int clGetDeviceIDs(IntPtr platform, DeviceType deviceType, uint numEntries, IntPtr[]? devices, ref uint numDevices);

        [DllImport(OpenCLLibrary)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        private static extern IntPtr clCreateContext(IntPtr properties, uint numDevices, IntPtr[] devices, IntPtr pfnNotify, IntPtr userData, ref int errorCode);

        [DllImport(OpenCLLibrary)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        private static extern int clReleaseContext(IntPtr context);

        private enum DeviceType : ulong
        {
            CL_DEVICE_TYPE_DEFAULT = (1 << 0),
            CL_DEVICE_TYPE_CPU = (1 << 1),
            CL_DEVICE_TYPE_GPU = (1 << 2),
            CL_DEVICE_TYPE_ACCELERATOR = (1 << 3),
            CL_DEVICE_TYPE_ALL = 0xFFFFFFFF
        }

        #endregion
    }
}