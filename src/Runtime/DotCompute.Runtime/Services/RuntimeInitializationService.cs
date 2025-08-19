// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Runtime.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Runtime.Services
{

/// <summary>
/// Hosted service for initializing the DotCompute runtime at application startup
/// </summary>
public class RuntimeInitializationService : IHostedService
{
    private readonly AcceleratorRuntime _runtime;
    private readonly DotComputeRuntimeOptions _options;
    private readonly ILogger<RuntimeInitializationService> _logger;

    public RuntimeInitializationService(
        AcceleratorRuntime runtime,
        IOptions<DotComputeRuntimeOptions> options,
        ILogger<RuntimeInitializationService> logger)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing DotCompute Runtime...");

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.InitializationTimeoutSeconds));

            await _runtime.InitializeAsync();
            
            _logger.LogInformation("DotCompute Runtime initialized successfully");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("DotCompute Runtime initialization was cancelled");
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("DotCompute Runtime initialization timed out after {TimeoutSeconds} seconds",
                _options.InitializationTimeoutSeconds);
            throw new TimeoutException($"Runtime initialization timed out after {_options.InitializationTimeoutSeconds} seconds");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize DotCompute Runtime");
            
            if (_options.EnableGracefulDegradation)
            {
                _logger.LogWarning("Continuing with graceful degradation enabled");
                return;
            }
            
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Shutting down DotCompute Runtime...");

        try
        {
            await _runtime.DisposeAsync();
            _logger.LogInformation("DotCompute Runtime shutdown completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during DotCompute Runtime shutdown");
            throw;
        }
    }
}}
