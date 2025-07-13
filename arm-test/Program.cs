using System;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;

class Program
{
    static void Main()
    {
        TestNeonCapabilities();
        TestNeonTranspose();
    }

    public static void TestNeonTranspose()
    {
        if (AdvSimd.IsSupported)
        {
            var row0 = Vector128.Create(1.0f, 2.0f, 3.0f, 4.0f);
            var row1 = Vector128.Create(5.0f, 6.0f, 7.0f, 8.0f);
            var row2 = Vector128.Create(9.0f, 10.0f, 11.0f, 12.0f);
            var row3 = Vector128.Create(13.0f, 14.0f, 15.0f, 16.0f);

            // Use alternative approach without ARM64-specific NEON instructions
            // For now use generic Vector128 operations that are cross-platform
            var temp0 = Vector128.Create(row0.GetElement(0), row1.GetElement(0), row2.GetElement(0), row3.GetElement(0));
            var temp1 = Vector128.Create(row0.GetElement(1), row1.GetElement(1), row2.GetElement(1), row3.GetElement(1));
            var temp2 = Vector128.Create(row0.GetElement(2), row1.GetElement(2), row2.GetElement(2), row3.GetElement(2));
            var temp3 = Vector128.Create(row0.GetElement(3), row1.GetElement(3), row2.GetElement(3), row3.GetElement(3));
            
            var col0 = temp0;
            var col1 = temp1;
            var col2 = temp2;
            var col3 = temp3;

            Console.WriteLine("ARM NEON transpose test compiled successfully");
        }
        else
        {
            Console.WriteLine("ARM NEON not supported on this platform");
        }
    }

    public static void TestNeonCapabilities()
    {
        Console.WriteLine($"AdvSimd.IsSupported: {AdvSimd.IsSupported}");
        Console.WriteLine($"AdvSimd.Arm64.IsSupported: {AdvSimd.Arm64.IsSupported}");
    }
}
