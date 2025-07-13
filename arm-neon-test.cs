using System;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;

public class ArmNeonTest
{
    public static void TestNeonTranspose()
    {
        if (AdvSimd.IsSupported)
        {
            var row0 = Vector128.Create(1.0f, 2.0f, 3.0f, 4.0f);
            var row1 = Vector128.Create(5.0f, 6.0f, 7.0f, 8.0f);
            var row2 = Vector128.Create(9.0f, 10.0f, 11.0f, 12.0f);
            var row3 = Vector128.Create(13.0f, 14.0f, 15.0f, 16.0f);

            // Use NEON transpose with correct instruction names
            var zip1_01 = AdvSimd.Arm64.Zip1(row0, row1);   // [00, 10, 01, 11]
            var zip2_01 = AdvSimd.Arm64.Zip2(row0, row1);   // [02, 12, 03, 13]
            var zip1_23 = AdvSimd.Arm64.Zip1(row2, row3);   // [20, 30, 21, 31]
            var zip2_23 = AdvSimd.Arm64.Zip2(row2, row3);   // [22, 32, 23, 33]
            
            var col0 = AdvSimd.Arm64.Zip1(zip1_01.AsUInt64(), zip1_23.AsUInt64()).AsSingle();
            var col1 = AdvSimd.Arm64.Zip2(zip1_01.AsUInt64(), zip1_23.AsUInt64()).AsSingle();
            var col2 = AdvSimd.Arm64.Zip1(zip2_01.AsUInt64(), zip2_23.AsUInt64()).AsSingle();
            var col3 = AdvSimd.Arm64.Zip2(zip2_01.AsUInt64(), zip2_23.AsUInt64()).AsSingle();

            Console.WriteLine("ARM NEON transpose test completed successfully");
        }
    }

    public static void TestNeonCapabilities()
    {
        Console.WriteLine($"AdvSimd.IsSupported: {AdvSimd.IsSupported}");
        Console.WriteLine($"AdvSimd.Arm64.IsSupported: {AdvSimd.Arm64.IsSupported}");
    }
}