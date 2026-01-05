// Sample 1: Image Processing with DotCompute
// Demonstrates: Basic kernels, filters, memory management
// Complexity: Beginner

using DotCompute;
using DotCompute.Abstractions;
using System.Drawing;

namespace DotCompute.Samples.ImageProcessing;

/// <summary>
/// Image processing sample demonstrating GPU-accelerated filters.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("DotCompute Image Processing Sample");
        Console.WriteLine("==================================\n");

        // Create accelerator (auto-detects best GPU)
        await using var accelerator = await AcceleratorFactory.CreateAsync();
        Console.WriteLine($"Using: {accelerator.Name}");
        Console.WriteLine($"Memory: {accelerator.Memory.TotalMemory / (1024 * 1024)} MB\n");

        // Sample image dimensions
        const int width = 1920;
        const int height = 1080;
        const int channels = 4; // RGBA

        // Create sample image data
        var imageData = CreateSampleImage(width, height, channels);
        Console.WriteLine($"Image size: {width}x{height} ({imageData.Length * sizeof(float) / 1024} KB)");

        // Allocate GPU buffers
        await using var input = await accelerator.Memory.AllocateAsync<float>(imageData.Length);
        await using var output = await accelerator.Memory.AllocateAsync<float>(imageData.Length);

        // Upload image to GPU
        await input.WriteAsync(imageData);

        // Run various filters
        await RunGrayscaleFilter(accelerator, input, output, width, height);
        await RunGaussianBlur(accelerator, input, output, width, height);
        await RunEdgeDetection(accelerator, input, output, width, height);
        await RunBrightnessAdjust(accelerator, input, output, width, height, 1.2f);

        Console.WriteLine("\nAll filters completed successfully!");
    }

    /// <summary>
    /// Grayscale conversion filter.
    /// </summary>
    [Kernel]
    static void GrayscaleKernel(float[] input, float[] output, int width, int height)
    {
        int x = Kernel.ThreadId.X;
        int y = Kernel.ThreadId.Y;

        if (x >= width || y >= height) return;

        int idx = (y * width + x) * 4;

        // Standard luminance weights
        float gray = input[idx + 0] * 0.299f +    // R
                     input[idx + 1] * 0.587f +    // G
                     input[idx + 2] * 0.114f;     // B

        output[idx + 0] = gray;  // R
        output[idx + 1] = gray;  // G
        output[idx + 2] = gray;  // B
        output[idx + 3] = input[idx + 3];  // A (preserve alpha)
    }

    /// <summary>
    /// 3x3 Gaussian blur kernel.
    /// </summary>
    [Kernel]
    static void GaussianBlurKernel(float[] input, float[] output, int width, int height)
    {
        int x = Kernel.ThreadId.X;
        int y = Kernel.ThreadId.Y;

        if (x < 1 || x >= width - 1 || y < 1 || y >= height - 1) return;

        // 3x3 Gaussian kernel (normalized)
        float[] kernel = { 1/16f, 2/16f, 1/16f,
                          2/16f, 4/16f, 2/16f,
                          1/16f, 2/16f, 1/16f };

        for (int c = 0; c < 3; c++) // RGB channels
        {
            float sum = 0;
            int k = 0;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    int nidx = (ny * width + nx) * 4 + c;
                    sum += input[nidx] * kernel[k++];
                }
            }

            output[(y * width + x) * 4 + c] = sum;
        }

        // Preserve alpha
        output[(y * width + x) * 4 + 3] = input[(y * width + x) * 4 + 3];
    }

    /// <summary>
    /// Sobel edge detection kernel.
    /// </summary>
    [Kernel]
    static void SobelEdgeKernel(float[] input, float[] output, int width, int height)
    {
        int x = Kernel.ThreadId.X;
        int y = Kernel.ThreadId.Y;

        if (x < 1 || x >= width - 1 || y < 1 || y >= height - 1) return;

        // Sobel operators
        float gx = 0, gy = 0;

        // Convert to grayscale first, then apply Sobel
        float GetGray(int px, int py)
        {
            int idx = (py * width + px) * 4;
            return input[idx] * 0.299f + input[idx + 1] * 0.587f + input[idx + 2] * 0.114f;
        }

        // Gx = [-1 0 1; -2 0 2; -1 0 1]
        gx = -GetGray(x-1, y-1) + GetGray(x+1, y-1) +
             -2*GetGray(x-1, y) + 2*GetGray(x+1, y) +
             -GetGray(x-1, y+1) + GetGray(x+1, y+1);

        // Gy = [-1 -2 -1; 0 0 0; 1 2 1]
        gy = -GetGray(x-1, y-1) - 2*GetGray(x, y-1) - GetGray(x+1, y-1) +
              GetGray(x-1, y+1) + 2*GetGray(x, y+1) + GetGray(x+1, y+1);

        float magnitude = MathF.Sqrt(gx * gx + gy * gy);
        magnitude = MathF.Min(magnitude, 1.0f);

        int outIdx = (y * width + x) * 4;
        output[outIdx + 0] = magnitude;
        output[outIdx + 1] = magnitude;
        output[outIdx + 2] = magnitude;
        output[outIdx + 3] = 1.0f;
    }

    /// <summary>
    /// Brightness adjustment kernel.
    /// </summary>
    [Kernel]
    static void BrightnessKernel(float[] input, float[] output, int width, int height, float factor)
    {
        int x = Kernel.ThreadId.X;
        int y = Kernel.ThreadId.Y;

        if (x >= width || y >= height) return;

        int idx = (y * width + x) * 4;

        output[idx + 0] = MathF.Min(input[idx + 0] * factor, 1.0f);
        output[idx + 1] = MathF.Min(input[idx + 1] * factor, 1.0f);
        output[idx + 2] = MathF.Min(input[idx + 2] * factor, 1.0f);
        output[idx + 3] = input[idx + 3]; // Preserve alpha
    }

    // Helper methods
    static async Task RunGrayscaleFilter(IAccelerator accelerator, IUnifiedMemoryBuffer<float> input,
        IUnifiedMemoryBuffer<float> output, int width, int height)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await accelerator.ExecuteAsync<GrayscaleKernel>(input, output, width, height);
        sw.Stop();
        Console.WriteLine($"Grayscale: {sw.ElapsedMilliseconds}ms");
    }

    static async Task RunGaussianBlur(IAccelerator accelerator, IUnifiedMemoryBuffer<float> input,
        IUnifiedMemoryBuffer<float> output, int width, int height)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await accelerator.ExecuteAsync<GaussianBlurKernel>(input, output, width, height);
        sw.Stop();
        Console.WriteLine($"Gaussian Blur: {sw.ElapsedMilliseconds}ms");
    }

    static async Task RunEdgeDetection(IAccelerator accelerator, IUnifiedMemoryBuffer<float> input,
        IUnifiedMemoryBuffer<float> output, int width, int height)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await accelerator.ExecuteAsync<SobelEdgeKernel>(input, output, width, height);
        sw.Stop();
        Console.WriteLine($"Edge Detection: {sw.ElapsedMilliseconds}ms");
    }

    static async Task RunBrightnessAdjust(IAccelerator accelerator, IUnifiedMemoryBuffer<float> input,
        IUnifiedMemoryBuffer<float> output, int width, int height, float factor)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await accelerator.ExecuteAsync<BrightnessKernel>(input, output, width, height, factor);
        sw.Stop();
        Console.WriteLine($"Brightness ({factor}x): {sw.ElapsedMilliseconds}ms");
    }

    static float[] CreateSampleImage(int width, int height, int channels)
    {
        var data = new float[width * height * channels];
        var random = new Random(42);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = (y * width + x) * channels;

                // Create gradient with noise
                data[idx + 0] = (float)x / width + (float)random.NextDouble() * 0.1f;
                data[idx + 1] = (float)y / height + (float)random.NextDouble() * 0.1f;
                data[idx + 2] = 0.5f + (float)random.NextDouble() * 0.2f;
                data[idx + 3] = 1.0f; // Alpha
            }
        }

        return data;
    }
}
