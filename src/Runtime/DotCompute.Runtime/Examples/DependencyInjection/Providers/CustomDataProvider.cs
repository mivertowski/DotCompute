// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.Examples.DependencyInjection.Providers;

/// <summary>
/// Example custom data provider implementation
/// </summary>
public class CustomDataProvider : ICustomDataProvider
{
    /// <inheritdoc />
    public async Task<byte[]> GetInitializationDataAsync()
    {
        await Task.Delay(10); // Simulate async work
        return new byte[1024];
    }
}