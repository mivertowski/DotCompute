
namespace DotCompute.Core.Models
{
    /// <summary>
    /// Represents runtime profiling results for memory coalescing analysis.
    /// </summary>
    public class RuntimeCoalescingProfile
    {
        private readonly Lock _lockObject = new();
        private readonly List<double> _executionTimes = [];
        private double _totalMemoryTransferred;
        private int _totalMemoryTransactions;
        private int _coalescedTransactions;

        /// <summary>
        /// Gets or initializes the name of the kernel being profiled.
        /// </summary>
        public required string KernelName { get; init; }

        /// <summary>
        /// Gets or initializes the timestamp when profiling started (DateTimeOffset).
        /// </summary>
        public DateTimeOffset ProfileStartTime { get; init; }

        /// <summary>
        /// Gets the profiling start time as DateTime for compatibility.
        /// </summary>
        public DateTime ProfileStartTimeDateTime => ProfileStartTime.DateTime;

        /// <summary>
        /// Gets or sets the average execution time across all profiling runs.
        /// </summary>
        public TimeSpan AverageExecutionTime { get; set; }

        /// <summary>
        /// Gets the average execution time in milliseconds.
        /// </summary>
        public double AverageExecutionTimeMs
        {
            get
            {
                lock (_lockObject)
                {
                    return _executionTimes.Count > 0 ? _executionTimes.Average() : 0.0;
                }
            }
        }

        /// <summary>
        /// Gets or sets the minimum execution time observed.
        /// </summary>
        public TimeSpan MinExecutionTime { get; set; }

        /// <summary>
        /// Gets the minimum execution time in milliseconds.
        /// </summary>
        public double MinExecutionTimeMs
        {
            get
            {
                lock (_lockObject)
                {
                    return _executionTimes.Count > 0 ? _executionTimes.Min() : 0.0;
                }
            }
        }

        /// <summary>
        /// Gets or sets the maximum execution time observed.
        /// </summary>
        public TimeSpan MaxExecutionTime { get; set; }

        /// <summary>
        /// Gets the maximum execution time in milliseconds.
        /// </summary>
        public double MaxExecutionTimeMs
        {
            get
            {
                lock (_lockObject)
                {
                    return _executionTimes.Count > 0 ? _executionTimes.Max() : 0.0;
                }
            }
        }

        /// <summary>
        /// Gets or sets the estimated memory bandwidth in bytes per second.
        /// </summary>
        public double EstimatedBandwidth { get; set; }

        /// <summary>
        /// Gets or sets the estimated coalescing efficiency (0.0 to 1.0).
        /// </summary>
        public double EstimatedCoalescingEfficiency { get; set; }

        /// <summary>
        /// Gets the calculated memory bandwidth based on actual transfers.
        /// </summary>
        public double CalculatedBandwidth
        {
            get
            {
                lock (_lockObject)
                {
                    var avgTime = AverageExecutionTimeMs / 1000.0; // Convert to seconds
                    return avgTime > 0 ? _totalMemoryTransferred / avgTime : 0.0;
                }
            }
        }

        /// <summary>
        /// Gets the calculated coalescing efficiency based on transaction analysis.
        /// </summary>
        public double CalculatedCoalescingEfficiency
        {
            get
            {
                lock (_lockObject)
                {
                    return _totalMemoryTransactions > 0 ? (double)_coalescedTransactions / _totalMemoryTransactions : 0.0;
                }
            }
        }

        /// <summary>
        /// Initializes the profiling session with production-quality defaults.
        /// </summary>
        public void Initialize()
        {
            lock (_lockObject)
            {
                _executionTimes.Clear();
                _totalMemoryTransferred = 0;
                _totalMemoryTransactions = 0;
                _coalescedTransactions = 0;


                EstimatedBandwidth = 0.0;
                EstimatedCoalescingEfficiency = 0.0;
                AverageExecutionTime = TimeSpan.Zero;
                MinExecutionTime = TimeSpan.MaxValue;
                MaxExecutionTime = TimeSpan.Zero;
            }
        }

        /// <summary>
        /// Records a kernel execution time and updates statistics.
        /// </summary>
        public void RecordExecution(double executionTimeMs, double memoryTransferred = 0, int coalescedTransactions = 0, int totalTransactions = 0)
        {
            lock (_lockObject)
            {
                _executionTimes.Add(executionTimeMs);
                _totalMemoryTransferred += memoryTransferred;
                _coalescedTransactions += coalescedTransactions;
                _totalMemoryTransactions += totalTransactions;

                // Update TimeSpan properties

                var executionTime = TimeSpan.FromMilliseconds(executionTimeMs);


                if (_executionTimes.Count == 1)
                {
                    MinExecutionTime = executionTime;
                    MaxExecutionTime = executionTime;
                }
                else
                {
                    if (executionTime < MinExecutionTime)
                    {
                        MinExecutionTime = executionTime;
                    }


                    if (executionTime > MaxExecutionTime)
                    {
                        MaxExecutionTime = executionTime;
                    }
                }


                AverageExecutionTime = TimeSpan.FromMilliseconds(_executionTimes.Average());

                // Update calculated values

                EstimatedBandwidth = CalculatedBandwidth;
                EstimatedCoalescingEfficiency = CalculatedCoalescingEfficiency;
            }
        }

        /// <summary>
        /// Gets comprehensive profiling statistics.
        /// </summary>
        public ProfilingStatistics GetStatistics()
        {
            lock (_lockObject)
            {
                return new ProfilingStatistics
                {
                    AverageTime = AverageExecutionTimeMs,
                    MinTime = MinExecutionTimeMs,
                    MaxTime = MaxExecutionTimeMs,
                    StandardDeviation = CalculateStandardDeviation(),
                    MedianTime = CalculateMedian()
                };
            }
        }

        /// <summary>
        /// Gets coalescing-specific profiling statistics.
        /// </summary>
        public CoalescingStatistics GetCoalescingStatistics()
        {
            lock (_lockObject)
            {
                return new CoalescingStatistics
                {
                    ExecutionCount = _executionTimes.Count,
                    AverageTimeMs = AverageExecutionTimeMs,
                    MinTimeMs = MinExecutionTimeMs,
                    MaxTimeMs = MaxExecutionTimeMs,
                    TotalMemoryTransferred = _totalMemoryTransferred,
                    MemoryBandwidthGBps = CalculatedBandwidth / (1024 * 1024 * 1024),
                    CoalescingEfficiency = CalculatedCoalescingEfficiency,
                    StandardDeviation = CalculateStandardDeviation()
                };
            }
        }

        private double CalculateStandardDeviation()
        {
            if (_executionTimes.Count <= 1)
            {
                return 0.0;
            }


            var mean = _executionTimes.Average();
            var sumOfSquaredDifferences = _executionTimes.Sum(time => Math.Pow(time - mean, 2));
            return Math.Sqrt(sumOfSquaredDifferences / _executionTimes.Count);
        }

        private double CalculateMedian()
        {
            if (_executionTimes.Count == 0)
            {
                return 0.0;
            }


            var sorted = _executionTimes.OrderBy(x => x).ToArray();
            var mid = sorted.Length / 2;


            return sorted.Length % 2 == 0
                ? (sorted[mid - 1] + sorted[mid]) / 2.0
                : sorted[mid];
        }
    }

    /// <summary>
    /// Coalescing-specific profiling statistics for runtime analysis.
    /// </summary>
    public class CoalescingStatistics
    {
        /// <summary>
        /// Gets or sets the execution count.
        /// </summary>
        /// <value>The execution count.</value>
        public int ExecutionCount { get; set; }
        /// <summary>
        /// Gets or sets the average time ms.
        /// </summary>
        /// <value>The average time ms.</value>
        public double AverageTimeMs { get; set; }
        /// <summary>
        /// Gets or sets the min time ms.
        /// </summary>
        /// <value>The min time ms.</value>
        public double MinTimeMs { get; set; }
        /// <summary>
        /// Gets or sets the max time ms.
        /// </summary>
        /// <value>The max time ms.</value>
        public double MaxTimeMs { get; set; }
        /// <summary>
        /// Gets or sets the total memory transferred.
        /// </summary>
        /// <value>The total memory transferred.</value>
        public double TotalMemoryTransferred { get; set; }
        /// <summary>
        /// Gets or sets the memory bandwidth g bps.
        /// </summary>
        /// <value>The memory bandwidth g bps.</value>
        public double MemoryBandwidthGBps { get; set; }
        /// <summary>
        /// Gets or sets the coalescing efficiency.
        /// </summary>
        /// <value>The coalescing efficiency.</value>
        public double CoalescingEfficiency { get; set; }
        /// <summary>
        /// Gets or sets the standard deviation.
        /// </summary>
        /// <value>The standard deviation.</value>
        public double StandardDeviation { get; set; }
    }
}