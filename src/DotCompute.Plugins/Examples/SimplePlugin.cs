// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Plugins.Core;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Plugins.Examples
{
    /// <summary>
    /// Simple example plugin implementation.
    /// </summary>
    public class SimplePlugin : BackendPluginBase
    {
        public override string Id => "simple.plugin";
        public override string Name => "Simple Plugin";
        public override Version Version => new(1, 0, 0);
        public override string Description => "A simple example plugin";
        public override string Author => "DotCompute Team";
        public override PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend;

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            Logger?.LogInformation("Simple plugin initialized");
            return Task.CompletedTask;
        }

        protected override Task OnStartAsync(CancellationToken cancellationToken)
        {
            Logger?.LogInformation("Simple plugin started");
            return Task.CompletedTask;
        }

        protected override Task OnStopAsync(CancellationToken cancellationToken)
        {
            Logger?.LogInformation("Simple plugin stopped");
            return Task.CompletedTask;
        }
    }
}