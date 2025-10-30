// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Logging;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Memory.P2P
{
    /// <summary>
    /// Comprehensive P2P validator that orchestrates transfer readiness validation,
    /// integrity checking, and performance benchmarking across different hardware configurations.
    /// Uses specialized components for focused responsibilities.
    /// </summary>
    public sealed class P2PValidator : IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly P2PCapabilityValidator _capabilityValidator;
        private readonly SemaphoreSlim _validationSemaphore;
        private readonly P2PValidationStatistics _statistics;
        private bool _disposed;

        // Validation configuration
        private const int MaxConcurrentValidations = 8;
        /// <summary>
        /// Initializes a new instance of the P2PValidator class.
        /// </summary>
        /// <param name="logger">The logger.</param>

        public P2PValidator(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _capabilityValidator = new P2PCapabilityValidator(logger);
            _validationSemaphore = new SemaphoreSlim(MaxConcurrentValidations, MaxConcurrentValidations);
            _statistics = new P2PValidationStatistics();

            _logger.LogDebugMessage("P2P Validator initialized with {MaxConcurrentValidations} concurrent validations");
        }

        /// <summary>
        /// Validates transfer readiness before executing a P2P transfer.
        /// </summary>
        public async Task<P2PValidationResult> ValidateTransferReadinessAsync<T>(
            IUnifiedMemoryBuffer<T> sourceBuffer,
            IUnifiedMemoryBuffer<T> destinationBuffer,
            P2PTransferPlan transferPlan,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(sourceBuffer);
            ArgumentNullException.ThrowIfNull(destinationBuffer);
            ArgumentNullException.ThrowIfNull(transferPlan);

            await _validationSemaphore.WaitAsync(cancellationToken);
            try
            {
                var validationResult = new P2PValidationResult
                {
                    ValidationId = Guid.NewGuid().ToString(),
                    ValidationTime = DateTimeOffset.UtcNow,
                    IsValid = true,
                    ValidationDetails = []
                };

                var validationTasks = new List<Task<P2PValidationDetail>>
                {
                    // Validate buffer compatibility
                    P2PValidationRules.ValidateBufferCompatibilityAsync(sourceBuffer, destinationBuffer, cancellationToken),

                    // Validate device capabilities
                    P2PValidationRules.ValidateDeviceCapabilitiesAsync(sourceBuffer.Accelerator, destinationBuffer.Accelerator, transferPlan, cancellationToken),

                    // Validate memory availability
                    P2PValidationRules.ValidateMemoryAvailabilityAsync(sourceBuffer, destinationBuffer, transferPlan, cancellationToken),

                    // Validate transfer strategy
                    P2PValidationRules.ValidateTransferStrategyAsync(transferPlan, cancellationToken)
                };

                var validationDetails = await Task.WhenAll(validationTasks);
                foreach (var detail in validationDetails)
                {
                    validationResult.ValidationDetails.Add(detail);
                }

                // Check if any validation failed
                var failedValidations = validationDetails.Where(d => !d.IsValid).ToList();
                if (failedValidations.Count != 0)
                {
                    validationResult.IsValid = false;
                    validationResult.ErrorMessage = string.Join("; ", failedValidations.Select(v => v.ErrorMessage));
                }

                // Update statistics
                P2PErrorAnalyzer.UpdateValidationStatistics(_statistics, validationResult);

                _logger.LogDebugMessage($"Transfer readiness validation completed: {validationResult.IsValid}, {validationDetails.Length} checks performed");

                return validationResult;
            }
            finally
            {
                _ = _validationSemaphore.Release();
            }
        }

        /// <summary>
        /// Validates transfer integrity after a P2P transfer completes.
        /// </summary>
        public async Task<P2PValidationResult> ValidateTransferIntegrityAsync<T>(
            IUnifiedMemoryBuffer<T> sourceBuffer,
            IUnifiedMemoryBuffer<T> destinationBuffer,
            P2PTransferPlan transferPlan,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(sourceBuffer);
            ArgumentNullException.ThrowIfNull(destinationBuffer);
            ArgumentNullException.ThrowIfNull(transferPlan);

            await _validationSemaphore.WaitAsync(cancellationToken);
            try
            {
                var validationResult = new P2PValidationResult
                {
                    ValidationId = Guid.NewGuid().ToString(),
                    ValidationTime = DateTimeOffset.UtcNow,
                    IsValid = true,
                    ValidationDetails = []
                };

                // Perform different integrity checks based on buffer size
                var transferSize = sourceBuffer.SizeInBytes;

                if (transferSize <= 16 * 1024 * 1024) // <= 16MB - full validation
                {
                    var integrityDetail = await P2PConnectivityValidator.ValidateFullDataIntegrityAsync(sourceBuffer, destinationBuffer, cancellationToken);
                    validationResult.ValidationDetails.Add(integrityDetail);
                    validationResult.IsValid = integrityDetail.IsValid;
                    if (!integrityDetail.IsValid)
                    {
                        validationResult.ErrorMessage = integrityDetail.ErrorMessage;
                    }
                }
                else // > 16MB - sampling validation
                {
                    var samplingDetail = await P2PConnectivityValidator.ValidateSampledDataIntegrityAsync(sourceBuffer, destinationBuffer, transferPlan, cancellationToken);
                    validationResult.ValidationDetails.Add(samplingDetail);
                    validationResult.IsValid = samplingDetail.IsValid;
                    if (!samplingDetail.IsValid)
                    {
                        validationResult.ErrorMessage = samplingDetail.ErrorMessage;
                    }
                }

                // Additional checksums for critical transfers
                if (transferSize > 100 * 1024 * 1024) // > 100MB
                {
                    var checksumDetail = await P2PConnectivityValidator.ValidateChecksumIntegrityAsync(sourceBuffer, destinationBuffer, cancellationToken);
                    validationResult.ValidationDetails.Add(checksumDetail);
                    if (!checksumDetail.IsValid)
                    {
                        validationResult.IsValid = false;
                        validationResult.ErrorMessage = (validationResult.ErrorMessage ?? "") + "; " + checksumDetail.ErrorMessage;
                    }
                }

                // Update statistics
                P2PErrorAnalyzer.UpdateValidationStatistics(_statistics, validationResult);

                _logger.LogDebugMessage($"Transfer integrity validation completed: {validationResult.IsValid}, Size: {transferSize} bytes");

                return validationResult;
            }
            finally
            {
                _ = _validationSemaphore.Release();
            }
        }

        /// <summary>
        /// Executes comprehensive P2P performance benchmarks.
        /// </summary>
        public async Task<P2PBenchmarkResult> ExecuteP2PBenchmarkAsync(
            IAccelerator sourceDevice,
            IAccelerator targetDevice,
            P2PBenchmarkOptions? options = null,
            CancellationToken cancellationToken = default) => await _capabilityValidator.ExecuteP2PBenchmarkAsync(sourceDevice, targetDevice, options, cancellationToken);

        /// <summary>
        /// Executes a comprehensive multi-GPU P2P benchmark suite.
        /// </summary>
        public async Task<P2PMultiGpuBenchmarkResult> ExecuteMultiGpuBenchmarkAsync(
            IAccelerator[] devices,
            P2PMultiGpuBenchmarkOptions? options = null,
            CancellationToken cancellationToken = default) => await _capabilityValidator.ExecuteMultiGpuBenchmarkAsync(devices, options, cancellationToken);

        /// <summary>
        /// Gets comprehensive validation and benchmark statistics.
        /// </summary>
        public P2PValidationStatistics GetValidationStatistics() => P2PErrorAnalyzer.CreateStatisticsSnapshot(_statistics);
        /// <summary>
        /// Gets dispose asynchronously.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _validationSemaphore.Dispose();
            await _capabilityValidator.DisposeAsync();

            _logger.LogDebugMessage("P2P Validator disposed");
        }
    }
}
