using System;
using System.Collections.Generic;
using DotCompute.Linq.Types;

namespace DotCompute.Linq.Compilation.Stages.Utilities;
/// <summary>
/// Tracks code generation metrics.
/// </summary>
internal class CodeGenerationMetrics
{
    private readonly Dictionary<BackendType, List<TimeSpan>> _generationTimes = [];
    private readonly Dictionary<BackendType, int> _errorCounts = [];
    public void RecordGeneration(BackendType backend, TimeSpan duration)
    {
        if (!_generationTimes.ContainsKey(backend))
        {
            _generationTimes[backend] = [];
        }
        _generationTimes[backend].Add(duration);
    }
    public void RecordError(BackendType backend)
        if (!_errorCounts.ContainsKey(backend))
            _errorCounts[backend] = 0;
        _errorCounts[backend]++;
    public double GetAverageGenerationTime(BackendType backend)
        if (!_generationTimes.ContainsKey(backend) || _generationTimes[backend].Count == 0)
            return 0;
        return _generationTimes[backend].Average(t => t.TotalMilliseconds);
    public int GetErrorCount(BackendType backend)
        return _errorCounts.TryGetValue(backend, out var count) ? count : 0;
}
