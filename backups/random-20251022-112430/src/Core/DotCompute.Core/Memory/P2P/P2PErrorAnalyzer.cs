// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Memory.P2P
{
    /// <summary>
    /// Analyzes and tracks P2P validation errors and statistics.
    /// Provides comprehensive error analysis, statistics tracking, and benchmarking result management.
    /// </summary>
    internal static class P2PErrorAnalyzer
    {
        /// <summary>
        /// Updates validation statistics based on a validation result.
        /// </summary>
        public static void UpdateValidationStatistics(P2PValidationStatistics statistics, P2PValidationResult validationResult)
        {
            ArgumentNullException.ThrowIfNull(statistics);
            ArgumentNullException.ThrowIfNull(validationResult);

            lock (statistics)
            {
                statistics.TotalValidations++;

                if (validationResult.IsValid)
                {
                    statistics.SuccessfulValidations++;
                }
                else
                {
                    statistics.FailedValidations++;
                }

                foreach (var detail in validationResult.ValidationDetails)
                {
                    switch (detail.ValidationType)
                    {
                        case "FullDataIntegrity":
                        case "SampledDataIntegrity":
                        case "ChecksumIntegrity":
                            statistics.IntegrityChecks++;
                            break;
                        case "TransferStrategy":
                        case "DeviceCapabilities":
                            statistics.PerformanceValidations++;
                            break;
                    }
                }

                statistics.TotalValidationTime += DateTimeOffset.UtcNow - validationResult.ValidationTime;
            }
        }

        /// <summary>
        /// Generates a comprehensive validation statistics snapshot.
        /// </summary>
        public static P2PValidationStatistics CreateStatisticsSnapshot(P2PValidationStatistics source)
        {
            ArgumentNullException.ThrowIfNull(source);

            lock (source)
            {
                return new P2PValidationStatistics
                {
                    TotalValidations = source.TotalValidations,
                    SuccessfulValidations = source.SuccessfulValidations,
                    FailedValidations = source.FailedValidations,
                    IntegrityChecks = source.IntegrityChecks,
                    PerformanceValidations = source.PerformanceValidations,
                    BenchmarksExecuted = source.BenchmarksExecuted,
                    TotalValidationTime = source.TotalValidationTime,
                    AverageValidationTime = source.TotalValidations > 0
                        ? source.TotalValidationTime / source.TotalValidations
                        : TimeSpan.Zero,
                    CachedBenchmarkHits = source.CachedBenchmarkHits,
                    ValidationSuccessRate = source.TotalValidations > 0
                        ? (double)source.SuccessfulValidations / source.TotalValidations
                        : 0.0
                };
            }
        }

        /// <summary>
        /// Generates a cache key for benchmark results.
        /// </summary>
        public static string GetBenchmarkCacheKey(string sourceDeviceId, string targetDeviceId, P2PBenchmarkOptions options)
        {
            ArgumentNullException.ThrowIfNull(sourceDeviceId);
            ArgumentNullException.ThrowIfNull(targetDeviceId);
            ArgumentNullException.ThrowIfNull(options);

            return $"{sourceDeviceId}->{targetDeviceId}_{options.MinTransferSizeMB}-{options.MaxTransferSizeMB}";
        }

        /// <summary>
        /// Analyzes validation failure patterns and provides insights.
        /// </summary>
        public static ValidationFailureAnalysis AnalyzeValidationFailures(IEnumerable<P2PValidationResult> failedValidations)
        {
            ArgumentNullException.ThrowIfNull(failedValidations);

            var failures = failedValidations.ToList();
            var analysis = new ValidationFailureAnalysis();

            if (failures.Count == 0)
            {
                analysis.NoFailures = true;
                return analysis;
            }

            // Analyze common failure patterns
            var failuresByType = failures
                .SelectMany(v => v.ValidationDetails)
                .Where(d => !d.IsValid)
                .GroupBy(d => d.ValidationType)
                .ToDictionary(g => g.Key, g => g.Count());

            analysis.FailuresByType.Clear();
            foreach (var kvp in failuresByType)
            {
                analysis.FailuresByType[kvp.Key] = kvp.Value;
            }
            analysis.MostCommonFailureType = failuresByType.OrderByDescending(kvp => kvp.Value).First().Key;
            analysis.TotalFailures = failures.Count;
            analysis.DistinctFailureTypes = failuresByType.Keys.Count;

            // Analyze temporal patterns
            var recentFailures = failures.Where(f => f.ValidationTime > DateTimeOffset.UtcNow.AddHours(-1)).Count();
            analysis.RecentFailureRate = (double)recentFailures / failures.Count;

            return analysis;
        }
    }

    /// <summary>
    /// Analysis results for validation failures.
    /// </summary>
    public sealed class ValidationFailureAnalysis
    {
        /// <summary>
        /// Gets or sets the no failures.
        /// </summary>
        /// <value>The no failures.</value>
        public bool NoFailures { get; set; }
        /// <summary>
        /// Gets or sets the total failures.
        /// </summary>
        /// <value>The total failures.</value>
        public int TotalFailures { get; set; }
        /// <summary>
        /// Gets or sets the distinct failure types.
        /// </summary>
        /// <value>The distinct failure types.</value>
        public int DistinctFailureTypes { get; set; }
        /// <summary>
        /// Gets or sets the most common failure type.
        /// </summary>
        /// <value>The most common failure type.</value>
        public string? MostCommonFailureType { get; set; }
        /// <summary>
        /// Gets or sets the recent failure rate.
        /// </summary>
        /// <value>The recent failure rate.</value>
        public double RecentFailureRate { get; set; }
        /// <summary>
        /// Gets or sets the failures by type.
        /// </summary>
        /// <value>The failures by type.</value>
        public Dictionary<string, int> FailuresByType { get; } = [];
    }
}