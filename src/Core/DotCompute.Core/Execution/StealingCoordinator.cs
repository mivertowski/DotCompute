// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Execution;
using DotCompute.Core.Logging;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Execution
{
    /// <summary>
    /// Coordinates stealing statistics and decisions.
    /// </summary>
    public class StealingCoordinator
    {
        private readonly ILogger _logger;
        private readonly int _deviceCount;
        private readonly int[][] _successfulSteals;
        private readonly int[][] _failedSteals;
        private readonly object _statsLock = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="StealingCoordinator"/> class.
        /// </summary>
        /// <param name="deviceCount">The device count.</param>
        /// <param name="logger">The logger.</param>
        /// <exception cref="ArgumentNullException">logger</exception>
        public StealingCoordinator(int deviceCount, ILogger logger)
        {
            _deviceCount = deviceCount;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize jagged arrays instead of multidimensional
            _successfulSteals = new int[deviceCount][];
            _failedSteals = new int[deviceCount][];
            for (int i = 0; i < deviceCount; i++)
            {
                _successfulSteals[i] = new int[deviceCount];
                _failedSteals[i] = new int[deviceCount];
            }
        }

        /// <summary>
        /// Records the successful steal.
        /// </summary>
        /// <param name="thiefIndex">Index of the thief.</param>
        /// <param name="victimIndex">Index of the victim.</param>
        public void RecordSuccessfulSteal(int thiefIndex, int victimIndex)
        {
            lock (_statsLock)
            {
                _successfulSteals[thiefIndex][victimIndex]++;
            }
        }

        /// <summary>
        /// Records the failed steal.
        /// </summary>
        /// <param name="thiefIndex">Index of the thief.</param>
        /// <param name="victimIndex">Index of the victim.</param>
#pragma warning disable CA1822 // Mark members as static - instance method for consistency with class design
        public void RecordFailedSteal(int thiefIndex, int victimIndex)
#pragma warning restore CA1822
        {
            lock (_statsLock)
            {
                _failedSteals[thiefIndex][victimIndex]++;
            }
        }

        /// <summary>
        /// Gets the statistics.
        /// </summary>
        /// <returns></returns>
#pragma warning disable CA1822 // Mark members as static - instance property for consistency with class design
        public StealingStatistics Statistics
#pragma warning restore CA1822
        {
            get
            {
                lock (_statsLock)
                {
                    var totalSuccessful = 0;
                    var totalFailed = 0;

                    for (var i = 0; i < _deviceCount; i++)
                    {
                        for (var j = 0; j < _deviceCount; j++)
                        {
                            totalSuccessful += _successfulSteals[i][j];
                            totalFailed += _failedSteals[i][j];
                        }
                    }

                    return new StealingStatistics
                    {
                        TotalStealAttempts = totalSuccessful + totalFailed,
                        SuccessfulSteals = totalSuccessful,
                        FailedSteals = totalFailed,
                        StealSuccessRate = totalSuccessful + totalFailed > 0
                            ? (double)totalSuccessful / (totalSuccessful + totalFailed) * 100
                            : 0
                    };
                }
            }
        }
    }
}