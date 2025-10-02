// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Services
{
    /// <summary>
    /// Minimal service provider for plugin dependency injection.
    /// </summary>
    internal sealed class MinimalServiceProvider : IServiceProvider
    {
        /// <summary>
        /// Gets the service.
        /// </summary>
        /// <param name="serviceType">The service type.</param>
        /// <returns>The service.</returns>
        public object? GetService(Type serviceType)
        {
            if (serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(ILogger<>))
            {
                var loggerType = typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>).MakeGenericType(serviceType.GetGenericArguments()[0]);
                return Activator.CreateInstance(loggerType);
            }

            if (serviceType == typeof(IConfiguration))
            {
                return new MinimalConfiguration();
            }

            return null;
        }
    }
}