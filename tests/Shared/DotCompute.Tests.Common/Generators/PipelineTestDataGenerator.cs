// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace DotCompute.Tests.Common.Generators;

/// <summary>
/// Advanced test data generator for pipeline testing scenarios.
/// Provides synthetic datasets for comprehensive pipeline validation.
/// </summary>
public class PipelineTestDataGenerator
{
    private readonly Random _random;
    /// <summary>
    /// Initializes a new instance of the PipelineTestDataGenerator class.
    /// </summary>

    public PipelineTestDataGenerator()
    {
        _random = new Random(42); // Fixed seed for deterministic tests
    }

    /// <summary>
    /// Generates numeric data with configurable patterns for testing.
    /// </summary>
    /// <typeparam name="T">Numeric type to generate</typeparam>
    /// <param name="size">Number of elements</param>
    /// <param name="pattern">Data distribution pattern</param>
    /// <returns>Generated test data array</returns>
    public T[] GenerateNumericData<T>(int size, DataPattern pattern = DataPattern.Random) where T : struct
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        var data = new T[size];


        switch (pattern)
        {
            case DataPattern.Random:
                GenerateRandomData(data);
                break;
            case DataPattern.Sequential:
                GenerateSequentialData(data);
                break;
            case DataPattern.Uniform:
                GenerateUniformData(data);
                break;
            case DataPattern.Normal:
                GenerateNormalData(data);
                break;
            case DataPattern.Alternating:
                GenerateAlternatingData(data);
                break;
            case DataPattern.EdgeCases:
                GenerateEdgeCaseData(data);
                break;
        }

        return data;
    }

    /// <summary>
    /// Generates a complex LINQ query for pipeline testing.
    /// </summary>
    /// <param name="complexity">Complexity level of the generated query</param>
    /// <returns>Expression representing complex LINQ operations</returns>
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Test code that generates dynamic expressions for testing purposes.")]
    public static Expression<Func<IQueryable<float>, IQueryable<float>>> GenerateComplexLinqQuery(int complexity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(complexity);

        // Start with a simple parameter
        var parameter = Expression.Parameter(typeof(IQueryable<float>), "source");
        Expression current = parameter;

        // Add complexity layers based on the complexity parameter
        for (var i = 0; i < complexity; i++)
        {
            switch (i % 4)
            {
                case 0: // Where clause
                    var whereParam = Expression.Parameter(typeof(float), "x");
                    var whereCondition = Expression.GreaterThan(whereParam, Expression.Constant(0.5f));
                    var whereLambda = Expression.Lambda<Func<float, bool>>(whereCondition, whereParam);
                    current = Expression.Call(typeof(Queryable), "Where", [typeof(float)], current, whereLambda);
                    break;

                case 1: // Select transformation
                    var selectParam = Expression.Parameter(typeof(float), "x");
                    var selectBody = Expression.Multiply(selectParam, Expression.Constant(2.0f));
                    var selectLambda = Expression.Lambda<Func<float, float>>(selectBody, selectParam);
                    current = Expression.Call(typeof(Queryable), "Select", [typeof(float), typeof(float)], current, selectLambda);
                    break;

                case 2: // OrderBy
                    var orderParam = Expression.Parameter(typeof(float), "x");
                    var orderLambda = Expression.Lambda<Func<float, float>>(orderParam, orderParam);
                    current = Expression.Call(typeof(Queryable), "OrderBy", [typeof(float), typeof(float)], current, orderLambda);
                    break;

                case 3: // Take
                    var takeCount = Math.Min(1000, 100 * (complexity - i));
                    current = Expression.Call(typeof(Queryable), "Take", [typeof(float)], current, Expression.Constant(takeCount));
                    break;
            }
        }

        return Expression.Lambda<Func<IQueryable<float>, IQueryable<float>>>(current, parameter);
    }

    /// <summary>
    /// Generates streaming data for real-time processing tests.
    /// </summary>
    /// <param name="batchSize">Size of each batch</param>
    /// <param name="batches">Number of batches to generate</param>
    /// <returns>Async enumerable of streaming data</returns>
    public async IAsyncEnumerable<float[]> GenerateStreamingData(int batchSize, int batches)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batches);

        for (var i = 0; i < batches; i++)
        {
            var batch = GenerateNumericData<float>(batchSize, DataPattern.Random);

            // Simulate streaming delay

            await Task.Delay(10);


            yield return batch;
        }
    }

    /// <summary>
    /// Generates image processing test data (2D arrays as flattened arrays).
    /// </summary>
    /// <param name="width">Image width in pixels</param>
    /// <param name="height">Image height in pixels</param>
    /// <param name="channels">Number of color channels (1=grayscale, 3=RGB, 4=RGBA)</param>
    /// <returns>Flattened image data for processing</returns>
    public float[] GenerateImageProcessingData(int width, int height, int channels = 3)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);

        var size = width * height * channels;
        var imageData = new float[size];

        // Generate synthetic image with patterns
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                for (var c = 0; c < channels; c++)
                {
                    var index = (y * width + x) * channels + c;

                    // Create gradient and noise pattern

                    var gradient = (x + y) / (float)(width + height);
                    var noise = (_random.NextSingle() - 0.5f) * 0.1f;


                    imageData[index] = MathF.Max(0, MathF.Min(1, gradient + noise));
                }
            }
        }

        return imageData;
    }

    /// <summary>
    /// Generates financial analysis test data with realistic patterns.
    /// </summary>
    /// <param name="transactions">Number of transactions to generate</param>
    /// <returns>Financial transaction data for analysis</returns>
    public FinancialTransaction[] GenerateFinancialAnalysisData(int transactions)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(transactions);

        var data = new FinancialTransaction[transactions];
        var basePrice = 100.0f;
        var currentPrice = basePrice;

        for (var i = 0; i < transactions; i++)
        {
            // Random walk for price with volatility
            var priceChange = (_random.NextSingle() - 0.5f) * 2.0f; // ±1%
            currentPrice *= (1 + priceChange / 100);


            data[i] = new FinancialTransaction
            {
                Id = i,
                Timestamp = DateTime.UtcNow.AddSeconds(-transactions + i),
                Price = Math.Max(1, currentPrice),
                Volume = _random.Next(100, 10000),
                Symbol = $"TEST{i % 10:D2}",
                Type = (TransactionType)(i % 3)
            };
        }

        return data;
    }

    /// <summary>
    /// Generates matrix data for linear algebra operations testing.
    /// </summary>
    /// <param name="rows">Number of rows</param>
    /// <param name="cols">Number of columns</param>
    /// <param name="sparsity">Sparsity factor (0.0 = dense, 0.9 = 90% zeros)</param>
    /// <returns>Matrix data as flattened array (row-major order)</returns>
    public float[] GenerateMatrixData(int rows, int cols, float sparsity = 0.0f)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cols);
        ArgumentOutOfRangeException.ThrowIfNegative(sparsity);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sparsity, 1.0f);

        var size = rows * cols;
        var matrix = new float[size];

        for (var i = 0; i < size; i++)
        {
            if (_random.NextSingle() > sparsity)
            {
                matrix[i] = (_random.NextSingle() - 0.5f) * 2.0f; // Range [-1, 1]
            }
            // else remains 0 (sparse)
        }

        return matrix;
    }

    private void GenerateRandomData<T>(T[] data) where T : struct
    {
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = GenerateRandomValue<T>();
        }
    }

    private static void GenerateSequentialData<T>(T[] data) where T : struct
    {
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = ConvertFromInt<T>(i);
        }
    }

    private void GenerateUniformData<T>(T[] data) where T : struct
    {
        var uniformValue = GenerateRandomValue<T>();
        Array.Fill(data, uniformValue);
    }

    private void GenerateNormalData<T>(T[] data) where T : struct
    {
        for (var i = 0; i < data.Length; i++)
        {
            var u1 = _random.NextDouble();
            var u2 = _random.NextDouble();
            var normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);


            data[i] = ConvertFromDouble<T>(normal);
        }
    }

    private void GenerateAlternatingData<T>(T[] data) where T : struct
    {
        var value1 = GenerateRandomValue<T>();
        var value2 = GenerateRandomValue<T>();

        for (var i = 0; i < data.Length; i++)
        {
            data[i] = i % 2 == 0 ? value1 : value2;
        }
    }

    private static void GenerateEdgeCaseData<T>(T[] data) where T : struct
    {
        var minValue = GetMinValue<T>();
        var maxValue = GetMaxValue<T>();
        var zeroValue = ConvertFromInt<T>(0);

        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (i % 3) switch
            {
                0 => minValue,
                1 => maxValue,
                _ => zeroValue
            };
        }
    }

    private T GenerateRandomValue<T>() where T : struct
    {
        return typeof(T) switch
        {
            _ when typeof(T) == typeof(float) => (T)(object)_random.NextSingle(),
            _ when typeof(T) == typeof(double) => (T)(object)_random.NextDouble(),
            _ when typeof(T) == typeof(int) => (T)(object)_random.Next(),
            _ when typeof(T) == typeof(long) => (T)(object)_random.NextInt64(),
            _ when typeof(T) == typeof(short) => (T)(object)(short)_random.Next(short.MinValue, short.MaxValue),
            _ when typeof(T) == typeof(byte) => (T)(object)(byte)_random.Next(0, 256),
            _ => throw new NotSupportedException($"Type {typeof(T)} not supported for random generation")
        };
    }

    private static T ConvertFromInt<T>(int value) where T : struct
    {
        return typeof(T) switch
        {
            _ when typeof(T) == typeof(float) => (T)(object)(float)value,
            _ when typeof(T) == typeof(double) => (T)(object)(double)value,
            _ when typeof(T) == typeof(int) => (T)(object)value,
            _ when typeof(T) == typeof(long) => (T)(object)(long)value,
            _ when typeof(T) == typeof(short) => (T)(object)(short)Math.Min(short.MaxValue, Math.Max(short.MinValue, value)),
            _ when typeof(T) == typeof(byte) => (T)(object)(byte)Math.Min(255, Math.Max(0, value)),
            _ => throw new NotSupportedException($"Type {typeof(T)} not supported for int conversion")
        };
    }

    private static T ConvertFromDouble<T>(double value) where T : struct
    {
        return typeof(T) switch
        {
            _ when typeof(T) == typeof(float) => (T)(object)(float)value,
            _ when typeof(T) == typeof(double) => (T)(object)value,
            _ when typeof(T) == typeof(int) => (T)(object)(int)Math.Round(value),
            _ when typeof(T) == typeof(long) => (T)(object)(long)Math.Round(value),
            _ when typeof(T) == typeof(short) => (T)(object)(short)Math.Min(short.MaxValue, Math.Max(short.MinValue, (int)Math.Round(value))),
            _ when typeof(T) == typeof(byte) => (T)(object)(byte)Math.Min(255, Math.Max(0, (int)Math.Round(value))),
            _ => throw new NotSupportedException($"Type {typeof(T)} not supported for double conversion")
        };
    }

    private static T GetMinValue<T>() where T : struct
    {
        return typeof(T) switch
        {
            _ when typeof(T) == typeof(float) => (T)(object)float.MinValue,
            _ when typeof(T) == typeof(double) => (T)(object)double.MinValue,
            _ when typeof(T) == typeof(int) => (T)(object)int.MinValue,
            _ when typeof(T) == typeof(long) => (T)(object)long.MinValue,
            _ when typeof(T) == typeof(short) => (T)(object)short.MinValue,
            _ when typeof(T) == typeof(byte) => (T)(object)byte.MinValue,
            _ => throw new NotSupportedException($"Type {typeof(T)} not supported for min value")
        };
    }

    private static T GetMaxValue<T>() where T : struct
    {
        return typeof(T) switch
        {
            _ when typeof(T) == typeof(float) => (T)(object)float.MaxValue,
            _ when typeof(T) == typeof(double) => (T)(object)double.MaxValue,
            _ when typeof(T) == typeof(int) => (T)(object)int.MaxValue,
            _ when typeof(T) == typeof(long) => (T)(object)long.MaxValue,
            _ when typeof(T) == typeof(short) => (T)(object)short.MaxValue,
            _ when typeof(T) == typeof(byte) => (T)(object)byte.MaxValue,
            _ => throw new NotSupportedException($"Type {typeof(T)} not supported for max value")
        };
    }
}

/// <summary>
/// Represents a financial transaction for testing financial analysis pipelines.
/// </summary>
public struct FinancialTransaction : IEquatable<FinancialTransaction>
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    /// <value>The id.</value>
    public int Id { get; set; }
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTime Timestamp { get; set; }
    /// <summary>
    /// Gets or sets the price.
    /// </summary>
    /// <value>The price.</value>
    public float Price { get; set; }
    /// <summary>
    /// Gets or sets the volume.
    /// </summary>
    /// <value>The volume.</value>
    public int Volume { get; set; }
    /// <summary>
    /// Gets or sets the symbol.
    /// </summary>
    /// <value>The symbol.</value>
    public string Symbol { get; set; }
    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    /// <value>The type.</value>
    public TransactionType Type { get; set; }

    /// <summary>
    /// Determines whether the specified object is equal to the current transaction.
    /// </summary>
    public override readonly bool Equals(object? obj) => obj is FinancialTransaction transaction && Equals(transaction);

    /// <summary>
    /// Determines whether the specified transaction is equal to the current transaction.
    /// </summary>
    public readonly bool Equals(FinancialTransaction other)
    {
        return Id == other.Id &&
               Timestamp.Equals(other.Timestamp) &&
               Price.Equals(other.Price) &&
               Volume == other.Volume &&
               Symbol == other.Symbol &&
               Type == other.Type;
    }
    /// <summary>
    /// Returns the hash code for this transaction.
    /// </summary>
    public override readonly int GetHashCode() => HashCode.Combine(Id, Timestamp, Price, Volume, Symbol, Type);
    /// <summary>
    /// Determines whether two transactions are equal.
    /// </summary>
    public static bool operator ==(FinancialTransaction left, FinancialTransaction right) => left.Equals(right);

    /// <summary>
    /// Determines whether two transactions are not equal.
    /// </summary>
    public static bool operator !=(FinancialTransaction left, FinancialTransaction right) => !(left == right);
}
/// <summary>
/// An transaction type enumeration.
/// </summary>

/// <summary>
/// Types of financial transactions.
/// </summary>
public enum TransactionType
{
    Buy,
    Sell,
    Hold
}