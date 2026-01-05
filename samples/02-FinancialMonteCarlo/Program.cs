// Sample 2: Financial Monte Carlo Simulation
// Demonstrates: Random number generation, reduction, statistical computing
// Complexity: Intermediate

using DotCompute;
using DotCompute.Abstractions;

namespace DotCompute.Samples.FinancialMonteCarlo;

/// <summary>
/// Monte Carlo option pricing using GPU-accelerated simulation.
/// Demonstrates European call option pricing via Black-Scholes simulation.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("DotCompute Financial Monte Carlo Sample");
        Console.WriteLine("=======================================\n");

        // Option parameters
        var option = new OptionParameters
        {
            SpotPrice = 100.0f,        // Current stock price
            StrikePrice = 105.0f,      // Strike price
            RiskFreeRate = 0.05f,      // 5% risk-free rate
            Volatility = 0.2f,         // 20% volatility
            TimeToExpiry = 1.0f,       // 1 year
            NumSimulations = 10_000_000 // 10 million paths
        };

        Console.WriteLine("Option Parameters:");
        Console.WriteLine($"  Spot Price: ${option.SpotPrice}");
        Console.WriteLine($"  Strike Price: ${option.StrikePrice}");
        Console.WriteLine($"  Risk-Free Rate: {option.RiskFreeRate * 100}%");
        Console.WriteLine($"  Volatility: {option.Volatility * 100}%");
        Console.WriteLine($"  Time to Expiry: {option.TimeToExpiry} years");
        Console.WriteLine($"  Simulations: {option.NumSimulations:N0}\n");

        // Create accelerator
        await using var accelerator = await AcceleratorFactory.CreateAsync();
        Console.WriteLine($"Using: {accelerator.Name}\n");

        // Run Monte Carlo pricing
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var (callPrice, putPrice, stdError) = await PriceOptionAsync(accelerator, option);
        sw.Stop();

        // Display results
        Console.WriteLine("Results:");
        Console.WriteLine($"  Call Option Price: ${callPrice:F4}");
        Console.WriteLine($"  Put Option Price: ${putPrice:F4}");
        Console.WriteLine($"  Standard Error: ${stdError:F6}");
        Console.WriteLine($"  95% Confidence: ±${1.96 * stdError:F4}");
        Console.WriteLine($"\nExecution time: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"Throughput: {option.NumSimulations / sw.Elapsed.TotalSeconds / 1e6:F2}M paths/sec");

        // Compare with analytical Black-Scholes
        var analyticalPrice = BlackScholesCall(option);
        Console.WriteLine($"\nBlack-Scholes analytical: ${analyticalPrice:F4}");
        Console.WriteLine($"Monte Carlo error: {Math.Abs(callPrice - analyticalPrice) / analyticalPrice * 100:F2}%");
    }

    static async Task<(float CallPrice, float PutPrice, float StdError)> PriceOptionAsync(
        IAccelerator accelerator, OptionParameters option)
    {
        int n = option.NumSimulations;

        // Allocate buffers
        await using var randomSeeds = await accelerator.Memory.AllocateAsync<uint>(n);
        await using var payoffs = await accelerator.Memory.AllocateAsync<float>(n);
        await using var partialSums = await accelerator.Memory.AllocateAsync<float>(1024);
        await using var partialSquares = await accelerator.Memory.AllocateAsync<float>(1024);

        // Initialize random seeds
        var seeds = Enumerable.Range(0, n).Select(i => (uint)(i * 1099087573 + 12345)).ToArray();
        await randomSeeds.WriteAsync(seeds);

        // Generate paths and compute payoffs
        await accelerator.ExecuteAsync<MonteCarloPathKernel>(
            randomSeeds, payoffs,
            option.SpotPrice, option.StrikePrice,
            option.RiskFreeRate, option.Volatility,
            option.TimeToExpiry, n);

        // Reduce to compute mean and variance
        await accelerator.ExecuteAsync<ReduceSumKernel>(payoffs, partialSums, n);
        await accelerator.ExecuteAsync<ReduceSquaresKernel>(payoffs, partialSquares, n);

        // Read back partial results
        var sums = await partialSums.ReadAsync();
        var squares = await partialSquares.ReadAsync();

        float totalSum = sums.Sum();
        float totalSquares = squares.Sum();

        // Compute statistics
        float mean = totalSum / n;
        float variance = (totalSquares / n) - (mean * mean);
        float stdDev = MathF.Sqrt(variance);
        float stdError = stdDev / MathF.Sqrt(n);

        // Discount to present value
        float discountFactor = MathF.Exp(-option.RiskFreeRate * option.TimeToExpiry);
        float callPrice = mean * discountFactor;
        float putPrice = callPrice - option.SpotPrice + option.StrikePrice * discountFactor;

        return (callPrice, putPrice, stdError * discountFactor);
    }

    /// <summary>
    /// Monte Carlo path simulation kernel.
    /// Uses Box-Muller transform for normal random numbers.
    /// </summary>
    [Kernel]
    static void MonteCarloPathKernel(
        uint[] seeds, float[] payoffs,
        float spot, float strike, float rate, float vol, float time, int n)
    {
        int idx = Kernel.ThreadId.X + Kernel.BlockId.X * Kernel.BlockDim.X;
        if (idx >= n) return;

        // Xorshift random number generator
        uint seed = seeds[idx];
        seed ^= seed << 13;
        seed ^= seed >> 17;
        seed ^= seed << 5;

        // Generate two uniform random numbers
        float u1 = (seed & 0xFFFFFF) / 16777216.0f;
        seed ^= seed << 13;
        seed ^= seed >> 17;
        seed ^= seed << 5;
        float u2 = (seed & 0xFFFFFF) / 16777216.0f;

        // Ensure u1 is not zero for log
        u1 = MathF.Max(u1, 1e-10f);

        // Box-Muller transform
        float z = MathF.Sqrt(-2.0f * MathF.Log(u1)) * MathF.Cos(2.0f * MathF.PI * u2);

        // Simulate terminal stock price using GBM
        float drift = (rate - 0.5f * vol * vol) * time;
        float diffusion = vol * MathF.Sqrt(time) * z;
        float terminalPrice = spot * MathF.Exp(drift + diffusion);

        // Compute call option payoff
        payoffs[idx] = MathF.Max(terminalPrice - strike, 0.0f);
    }

    /// <summary>
    /// Parallel reduction kernel for sum.
    /// </summary>
    [Kernel]
    static void ReduceSumKernel(float[] input, float[] output, int n)
    {
        // Shared memory for block reduction
        var shared = Kernel.SharedMemory<float>(256);

        int tid = Kernel.ThreadId.X;
        int bid = Kernel.BlockId.X;
        int gid = tid + bid * Kernel.BlockDim.X;

        // Load and accumulate multiple elements per thread
        float sum = 0;
        int stride = Kernel.BlockDim.X * Kernel.GridDim.X;
        for (int i = gid; i < n; i += stride)
        {
            sum += input[i];
        }

        shared[tid] = sum;
        Kernel.SyncThreads();

        // Reduction within block
        for (int s = Kernel.BlockDim.X / 2; s > 0; s >>= 1)
        {
            if (tid < s)
            {
                shared[tid] += shared[tid + s];
            }
            Kernel.SyncThreads();
        }

        // Write block result
        if (tid == 0)
        {
            output[bid] = shared[0];
        }
    }

    /// <summary>
    /// Parallel reduction kernel for sum of squares.
    /// </summary>
    [Kernel]
    static void ReduceSquaresKernel(float[] input, float[] output, int n)
    {
        var shared = Kernel.SharedMemory<float>(256);

        int tid = Kernel.ThreadId.X;
        int bid = Kernel.BlockId.X;
        int gid = tid + bid * Kernel.BlockDim.X;

        float sum = 0;
        int stride = Kernel.BlockDim.X * Kernel.GridDim.X;
        for (int i = gid; i < n; i += stride)
        {
            float val = input[i];
            sum += val * val;
        }

        shared[tid] = sum;
        Kernel.SyncThreads();

        for (int s = Kernel.BlockDim.X / 2; s > 0; s >>= 1)
        {
            if (tid < s)
            {
                shared[tid] += shared[tid + s];
            }
            Kernel.SyncThreads();
        }

        if (tid == 0)
        {
            output[bid] = shared[0];
        }
    }

    /// <summary>
    /// Analytical Black-Scholes call price for comparison.
    /// </summary>
    static float BlackScholesCall(OptionParameters opt)
    {
        float d1 = (MathF.Log(opt.SpotPrice / opt.StrikePrice) +
                   (opt.RiskFreeRate + 0.5f * opt.Volatility * opt.Volatility) * opt.TimeToExpiry) /
                   (opt.Volatility * MathF.Sqrt(opt.TimeToExpiry));
        float d2 = d1 - opt.Volatility * MathF.Sqrt(opt.TimeToExpiry);

        return opt.SpotPrice * NormCDF(d1) -
               opt.StrikePrice * MathF.Exp(-opt.RiskFreeRate * opt.TimeToExpiry) * NormCDF(d2);
    }

    static float NormCDF(float x)
    {
        return 0.5f * (1.0f + Erf(x / MathF.Sqrt(2.0f)));
    }

    static float Erf(float x)
    {
        float a1 = 0.254829592f, a2 = -0.284496736f, a3 = 1.421413741f;
        float a4 = -1.453152027f, a5 = 1.061405429f, p = 0.3275911f;
        float sign = x < 0 ? -1 : 1;
        x = MathF.Abs(x);
        float t = 1.0f / (1.0f + p * x);
        float y = 1.0f - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * MathF.Exp(-x * x);
        return sign * y;
    }
}

public record struct OptionParameters
{
    public float SpotPrice { get; init; }
    public float StrikePrice { get; init; }
    public float RiskFreeRate { get; init; }
    public float Volatility { get; init; }
    public float TimeToExpiry { get; init; }
    public int NumSimulations { get; init; }
}
