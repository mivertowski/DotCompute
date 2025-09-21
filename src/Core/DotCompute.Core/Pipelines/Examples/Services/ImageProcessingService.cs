// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Pipelines.Examples.Services;

/// <summary>
/// Service for image processing using kernel chains.
/// Demonstrates production-ready image processing workflows.
/// </summary>
public class ImageProcessingService
{
    /// <summary>
    /// Processes an image using the kernel chain pipeline.
    /// </summary>
    /// <param name="imageData">Raw image data to process</param>
    /// <returns>Processed image data</returns>
    public async Task<byte[]> ProcessImageAsync(byte[] imageData)
    {
        return await KernelChainExamples.ImageProcessingChainExample(imageData);
    }
}
