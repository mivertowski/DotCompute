// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Runtime.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DotCompute.Runtime.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Runtime.Services;


/// <summary>
/// Hosted service for initializing the DotCompute runtime at application startup
/// </summary>
public class RuntimeInitializationService(
    AcceleratorRuntime runtime,
    IOptions<DotComputeRuntimeOptions> options,
    ILogger<RuntimeInitializationService> logger) : IHostedService
{
    private readonly AcceleratorRuntime _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    private readonly DotComputeRuntimeOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly ILogger<RuntimeInitializationService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInfoMessage("Initializing DotCompute Runtime...");

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.InitializationTimeoutSeconds));

            await _runtime.InitializeAsync();

            _logger.LogInfoMessage("DotCompute Runtime initialized successfully");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarningMessage("DotCompute Runtime initialization was cancelled");
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
            _logger.LogErrorMessage(ex, "Failed to initialize DotCompute Runtime");

            if (_options.EnableGracefulDegradation)
            {
                _logger.LogWarningMessage("Continuing with graceful degradation enabled");
                return;
            }

            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInfoMessage("Shutting down DotCompute Runtime...");

        try
        {
            await _runtime.DisposeAsync();
            _logger.LogInfoMessage("DotCompute Runtime shutdown completed");
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Error during DotCompute Runtime shutdown");
            throw;
        }
    }
}
