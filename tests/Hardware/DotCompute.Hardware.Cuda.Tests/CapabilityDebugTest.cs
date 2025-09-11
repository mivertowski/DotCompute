using DotCompute.Backends.CUDA.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Cuda.Tests
{
    public class CapabilityDebugTest
    {
        private readonly ITestOutputHelper _output;

        public CapabilityDebugTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Debug_CheckComputeCapability()
        {
            // Check what the CudaCapabilityManager returns
            var (major, minor) = CudaCapabilityManager.GetTargetComputeCapability();
            _output.WriteLine($"CudaCapabilityManager.GetTargetComputeCapability returned: {major}.{minor}");


            var archString = CudaCapabilityManager.GetArchitectureString((major, minor));
            _output.WriteLine($"Architecture string: {archString}");


            var smString = CudaCapabilityManager.GetSmString((major, minor));
            _output.WriteLine($"SM string: {smString}");


            var ptxVersion = CudaCapabilityManager.GetCompatiblePtxVersion((major, minor));
            _output.WriteLine($"PTX version: {ptxVersion}");

            // Check CUDA installation

            _output.WriteLine($"/usr/local/cuda exists: {System.IO.Directory.Exists("/usr/local/cuda")}");
            _output.WriteLine($"/usr/local/cuda-13.0 exists: {System.IO.Directory.Exists("/usr/local/cuda-13.0")}");

            // This should be 8.9 for RTX 2000 Ada with CUDA 13

            Assert.Equal((8, 9), (major, minor));
        }
    }
}