using Xunit;
using System;
using Xunit;
using DotCompute.Core;

namespace DotCompute.Core.Tests
{
    public class BasicTests
    {
        [Fact]
        public void Core_Assembly_Loads()
        {
            var assembly = typeof(ComputeContext).Assembly;
            Assert.NotNull(assembly);
            Assert.Contains("DotCompute.Core", assembly.FullName);
        }

        [Theory]
        [InlineData(1, 2, 3)]
        [InlineData(10, 20, 30)]
        [InlineData(-5, 5, 0)]
        public void Basic_Math_Works(int a, int b, int expected)
        {
            var result = a + b;
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Platform_Detection_Works()
        {
            var platform = Environment.OSVersion.Platform;
            Assert.True(
                platform == PlatformID.Unix ||
                platform == PlatformID.Win32NT ||
                platform == PlatformID.MacOSX ||
                platform == PlatformID.Other
            );
        }
    }
}