// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.ML;
using Microsoft.ML.Data;

namespace DotCompute.Samples.MLNet;

/// <summary>
/// Demonstrates a GPU-accelerated custom transformer for ML.NET pipelines.
/// </summary>
/// <remarks>
/// <para>
/// This sample shows how to create a custom ML.NET transformer that uses
/// DotCompute for GPU-accelerated data transformations. The pattern can be
/// adapted for various operations:
/// </para>
/// <list type="bullet">
/// <item><description>Feature normalization</description></item>
/// <item><description>Principal Component Analysis (PCA)</description></item>
/// <item><description>Feature hashing</description></item>
/// <item><description>Custom neural network layers</description></item>
/// </list>
/// </remarks>
public sealed class GpuAcceleratedNormalizer : IEstimator<GpuNormalizerTransformer>
{
    private readonly MLContext _mlContext;
    private readonly string _inputColumnName;
    private readonly string _outputColumnName;
    private readonly NormalizationMode _mode;

    /// <summary>
    /// Initializes a new GPU-accelerated normalizer.
    /// </summary>
    /// <param name="mlContext">ML.NET context.</param>
    /// <param name="inputColumnName">Input column name.</param>
    /// <param name="outputColumnName">Output column name.</param>
    /// <param name="mode">Normalization mode.</param>
    public GpuAcceleratedNormalizer(
        MLContext mlContext,
        string inputColumnName,
        string outputColumnName,
        NormalizationMode mode = NormalizationMode.MinMax)
    {
        _mlContext = mlContext ?? throw new ArgumentNullException(nameof(mlContext));
        _inputColumnName = inputColumnName ?? throw new ArgumentNullException(nameof(inputColumnName));
        _outputColumnName = outputColumnName ?? throw new ArgumentNullException(nameof(outputColumnName));
        _mode = mode;
    }

    /// <summary>
    /// Fits the transformer to the data.
    /// </summary>
    public GpuNormalizerTransformer Fit(IDataView input)
    {
        // Extract statistics from input data
        var column = input.GetColumn<float[]>(_inputColumnName).ToArray();

        if (column.Length == 0)
        {
            throw new InvalidOperationException("Input column is empty");
        }

        var featureCount = column[0].Length;
        var sampleCount = column.Length;

        // Calculate statistics (would be GPU-accelerated in production)
        var stats = new FeatureStatistics[featureCount];

        for (var f = 0; f < featureCount; f++)
        {
            float min = float.MaxValue, max = float.MinValue;
            float sum = 0;

            for (var s = 0; s < sampleCount; s++)
            {
                var value = column[s][f];
                min = Math.Min(min, value);
                max = Math.Max(max, value);
                sum += value;
            }

            var mean = sum / sampleCount;

            float varianceSum = 0;
            for (var s = 0; s < sampleCount; s++)
            {
                var diff = column[s][f] - mean;
                varianceSum += diff * diff;
            }
            var std = MathF.Sqrt(varianceSum / sampleCount);

            stats[f] = new FeatureStatistics(min, max, mean, std);
        }

        return new GpuNormalizerTransformer(
            _mlContext, _inputColumnName, _outputColumnName, _mode, stats);
    }

    /// <summary>
    /// Gets the output schema.
    /// </summary>
    public SchemaShape GetOutputSchema(SchemaShape inputSchema)
    {
        var columns = inputSchema.ToDictionary(c => c.Name);

        if (!columns.TryGetValue(_inputColumnName, out var inputColumn))
        {
            throw new ArgumentException($"Column '{_inputColumnName}' not found in schema");
        }

        columns[_outputColumnName] = new SchemaShape.Column(
            _outputColumnName,
            inputColumn.Kind,
            inputColumn.ItemType,
            inputColumn.IsKey,
            inputColumn.Annotations);

        return new SchemaShape(columns.Values);
    }
}

/// <summary>
/// GPU-accelerated normalizer transformer.
/// </summary>
public sealed class GpuNormalizerTransformer : ITransformer
{
    private readonly MLContext _mlContext;
    private readonly string _inputColumnName;
    private readonly string _outputColumnName;
    private readonly NormalizationMode _mode;
    private readonly FeatureStatistics[] _statistics;

    internal GpuNormalizerTransformer(
        MLContext mlContext,
        string inputColumnName,
        string outputColumnName,
        NormalizationMode mode,
        FeatureStatistics[] statistics)
    {
        _mlContext = mlContext;
        _inputColumnName = inputColumnName;
        _outputColumnName = outputColumnName;
        _mode = mode;
        _statistics = statistics;
    }

    /// <summary>
    /// Gets whether the transformer is row-to-row.
    /// </summary>
    public bool IsRowToRowMapper => true;

    /// <summary>
    /// Gets the output schema.
    /// </summary>
    public DataViewSchema GetOutputSchema(DataViewSchema inputSchema)
    {
        var builder = new DataViewSchema.Builder();

        foreach (var column in inputSchema)
        {
            builder.AddColumn(column.Name, column.Type, column.Annotations);
        }

        // Add output column if different from input
        if (_inputColumnName != _outputColumnName)
        {
            var inputColumn = inputSchema[_inputColumnName];
            builder.AddColumn(_outputColumnName, inputColumn.Type);
        }

        return builder.ToSchema();
    }

    /// <summary>
    /// Transforms the data.
    /// </summary>
    public IDataView Transform(IDataView input)
    {
        // Extract data
        var inputData = input.GetColumn<float[]>(_inputColumnName).ToArray();

        // Transform using GPU (simulated here)
        var outputData = TransformBatch(inputData);

        // Create output data view
        var transformedData = inputData.Zip(outputData, (inp, outp) => new
        {
            Input = inp,
            Output = outp
        }).ToList();

        // Return transformed data
        return _mlContext.Data.LoadFromEnumerable(
            transformedData.Select(x => new NormalizedDataPoint
            {
                Features = x.Input,
                NormalizedFeatures = x.Output
            }));
    }

    /// <summary>
    /// Gets the row-to-row mapper.
    /// </summary>
    public IRowToRowMapper GetRowToRowMapper(DataViewSchema inputSchema)
    {
        throw new NotSupportedException("Row-to-row mapping not supported for GPU batch transformer");
    }

    /// <summary>
    /// Saves the transformer.
    /// </summary>
    public void Save(ModelSaveContext ctx)
    {
        throw new NotSupportedException("Model saving not implemented in sample");
    }

    private float[][] TransformBatch(float[][] input)
    {
        var output = new float[input.Length][];

        // In production, this would be a GPU kernel:
        // await normalizeKernel.ExecuteAsync(inputBuffer, outputBuffer, statisticsBuffer);

        for (var s = 0; s < input.Length; s++)
        {
            output[s] = new float[input[s].Length];

            for (var f = 0; f < input[s].Length; f++)
            {
                output[s][f] = _mode switch
                {
                    NormalizationMode.MinMax => NormalizeMinMax(input[s][f], f),
                    NormalizationMode.ZScore => NormalizeZScore(input[s][f], f),
                    NormalizationMode.LogNormal => NormalizeLogNormal(input[s][f], f),
                    _ => input[s][f]
                };
            }
        }

        return output;
    }

    private float NormalizeMinMax(float value, int featureIndex)
    {
        var stats = _statistics[featureIndex];
        var range = stats.Max - stats.Min;
        return range > 1e-6f ? (value - stats.Min) / range : 0f;
    }

    private float NormalizeZScore(float value, int featureIndex)
    {
        var stats = _statistics[featureIndex];
        return stats.Std > 1e-6f ? (value - stats.Mean) / stats.Std : 0f;
    }

    private float NormalizeLogNormal(float value, int featureIndex)
    {
        return value > 0 ? MathF.Log(value + 1) : 0f;
    }
}

/// <summary>
/// Statistics for a single feature.
/// </summary>
public readonly record struct FeatureStatistics(float Min, float Max, float Mean, float Std);

/// <summary>
/// Normalization modes.
/// </summary>
public enum NormalizationMode
{
    /// <summary>Min-max normalization to [0, 1].</summary>
    MinMax,

    /// <summary>Z-score normalization (mean=0, std=1).</summary>
    ZScore,

    /// <summary>Log normalization for skewed distributions.</summary>
    LogNormal
}

/// <summary>
/// Data point with normalized features.
/// </summary>
public class NormalizedDataPoint
{
    [VectorType]
    public float[] Features { get; set; } = [];

    [VectorType]
    public float[] NormalizedFeatures { get; set; } = [];
}

/// <summary>
/// Extension methods for GPU-accelerated ML.NET transforms.
/// </summary>
public static class GpuTransformExtensions
{
    /// <summary>
    /// Adds a GPU-accelerated normalizer to the pipeline.
    /// </summary>
    /// <param name="catalog">Transform catalog.</param>
    /// <param name="inputColumnName">Input column name.</param>
    /// <param name="outputColumnName">Output column name.</param>
    /// <param name="mode">Normalization mode.</param>
    /// <returns>The GPU-accelerated normalizer estimator.</returns>
    public static GpuAcceleratedNormalizer GpuNormalize(
        this TransformsCatalog catalog,
        string inputColumnName,
        string? outputColumnName = null,
        NormalizationMode mode = NormalizationMode.MinMax)
    {
        var mlContext = GetMLContext(catalog);
        return new GpuAcceleratedNormalizer(
            mlContext,
            inputColumnName,
            outputColumnName ?? inputColumnName,
            mode);
    }

    private static MLContext GetMLContext(TransformsCatalog catalog)
    {
        // Note: In production, you would access the MLContext through proper channels
        // This is a simplified example
        return new MLContext();
    }
}
