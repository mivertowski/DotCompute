// Sample 4: Physics Simulation
// Demonstrates: Multi-kernel workflows, P2P transfers, particle systems
// Complexity: Advanced

using DotCompute;
using DotCompute.Abstractions;

namespace DotCompute.Samples.PhysicsSimulation;

/// <summary>
/// N-body gravitational simulation using GPU-accelerated particle physics.
/// Demonstrates multi-kernel execution, P2P memory transfers, and real-time visualization data.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("DotCompute Physics Simulation Sample");
        Console.WriteLine("=====================================\n");

        // Simulation parameters
        const int numParticles = 50_000;
        const int numSteps = 1000;
        const float dt = 0.001f;
        const float gravity = 6.67430e-11f; // Gravitational constant
        const float softening = 0.01f;       // Prevent singularities

        Console.WriteLine($"Simulating {numParticles:N0} particles for {numSteps} steps");
        Console.WriteLine($"Time step: {dt}s, Softening: {softening}\n");

        // Create accelerator
        await using var accelerator = await AcceleratorFactory.CreateAsync();
        Console.WriteLine($"Using: {accelerator.Name}\n");

        // Initialize particle system
        var (positions, velocities, masses) = InitializeGalaxy(numParticles);

        // Allocate GPU buffers
        await using var positionsBuffer = await accelerator.Memory.AllocateAsync<float>(numParticles * 3);
        await using var velocitiesBuffer = await accelerator.Memory.AllocateAsync<float>(numParticles * 3);
        await using var massesBuffer = await accelerator.Memory.AllocateAsync<float>(numParticles);
        await using var accelerationsBuffer = await accelerator.Memory.AllocateAsync<float>(numParticles * 3);

        // Copy initial data to GPU
        await positionsBuffer.WriteAsync(positions);
        await velocitiesBuffer.WriteAsync(velocities);
        await massesBuffer.WriteAsync(masses);

        Console.WriteLine("Running N-body simulation...\n");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Simulation loop
        for (int step = 0; step < numSteps; step++)
        {
            // Phase 1: Compute gravitational forces
            await accelerator.ExecuteAsync<ComputeGravityKernel>(
                positionsBuffer, massesBuffer, accelerationsBuffer,
                numParticles, gravity, softening);

            // Phase 2: Integrate velocities
            await accelerator.ExecuteAsync<IntegrateVelocityKernel>(
                velocitiesBuffer, accelerationsBuffer,
                numParticles, dt);

            // Phase 3: Integrate positions
            await accelerator.ExecuteAsync<IntegratePositionKernel>(
                positionsBuffer, velocitiesBuffer,
                numParticles, dt);

            // Report progress
            if (step % 100 == 0)
            {
                // Read back sample for diagnostics
                var samplePos = await positionsBuffer.ReadAsync(0, 3);
                var sampleVel = await velocitiesBuffer.ReadAsync(0, 3);
                float speed = MathF.Sqrt(sampleVel[0] * sampleVel[0] +
                                         sampleVel[1] * sampleVel[1] +
                                         sampleVel[2] * sampleVel[2]);

                Console.WriteLine($"Step {step,5}: Particle[0] pos=({samplePos[0]:F4}, {samplePos[1]:F4}, {samplePos[2]:F4}), speed={speed:F4}");
            }
        }

        sw.Stop();

        // Read final state
        var finalPositions = await positionsBuffer.ReadAsync();
        var finalVelocities = await velocitiesBuffer.ReadAsync();

        // Compute statistics
        var stats = ComputeStatistics(finalPositions, finalVelocities, numParticles);

        Console.WriteLine($"\nSimulation completed in {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"Performance: {(double)numSteps * numParticles / sw.Elapsed.TotalSeconds / 1e6:F2}M particle-steps/sec");

        Console.WriteLine($"\nFinal Statistics:");
        Console.WriteLine($"  Center of Mass: ({stats.CenterOfMass.X:F4}, {stats.CenterOfMass.Y:F4}, {stats.CenterOfMass.Z:F4})");
        Console.WriteLine($"  Average Speed: {stats.AverageSpeed:F4}");
        Console.WriteLine($"  Max Speed: {stats.MaxSpeed:F4}");
        Console.WriteLine($"  System Radius: {stats.SystemRadius:F4}");
    }

    /// <summary>
    /// Initialize particles in a rotating disk galaxy configuration.
    /// </summary>
    static (float[] positions, float[] velocities, float[] masses) InitializeGalaxy(int n)
    {
        var positions = new float[n * 3];
        var velocities = new float[n * 3];
        var masses = new float[n];

        var random = new Random(42);
        const float galaxyRadius = 10.0f;
        const float diskHeight = 0.5f;
        const float centralMass = 1e10f;

        for (int i = 0; i < n; i++)
        {
            // Disk distribution
            float r = (float)(random.NextDouble() * galaxyRadius);
            float theta = (float)(random.NextDouble() * 2 * Math.PI);
            float z = (float)((random.NextDouble() - 0.5) * diskHeight * Math.Exp(-r / galaxyRadius));

            // Position
            positions[i * 3 + 0] = r * MathF.Cos(theta);
            positions[i * 3 + 1] = r * MathF.Sin(theta);
            positions[i * 3 + 2] = z;

            // Circular orbital velocity
            float orbitalSpeed = MathF.Sqrt(centralMass / (r + 0.1f)) * 0.001f;
            velocities[i * 3 + 0] = -orbitalSpeed * MathF.Sin(theta);
            velocities[i * 3 + 1] = orbitalSpeed * MathF.Cos(theta);
            velocities[i * 3 + 2] = 0;

            // Mass (with some variation)
            masses[i] = 1e5f * (0.5f + (float)random.NextDouble());
        }

        return (positions, velocities, masses);
    }

    /// <summary>
    /// Compute gravitational accelerations using O(N²) direct summation.
    /// Optimized with shared memory tiling.
    /// </summary>
    [Kernel]
    static void ComputeGravityKernel(
        float[] positions, float[] masses, float[] accelerations,
        int n, float G, float softening)
    {
        int i = Kernel.ThreadId.X + Kernel.BlockId.X * Kernel.BlockDim.X;
        if (i >= n) return;

        // Load this particle's position
        float px = positions[i * 3 + 0];
        float py = positions[i * 3 + 1];
        float pz = positions[i * 3 + 2];

        float ax = 0, ay = 0, az = 0;

        // Shared memory for tile-based computation
        var sharedPos = Kernel.SharedMemory<float>(256 * 4);

        for (int tile = 0; tile < (n + 255) / 256; tile++)
        {
            int j = tile * 256 + Kernel.ThreadId.X;

            // Collaborative load into shared memory
            if (j < n)
            {
                sharedPos[Kernel.ThreadId.X * 4 + 0] = positions[j * 3 + 0];
                sharedPos[Kernel.ThreadId.X * 4 + 1] = positions[j * 3 + 1];
                sharedPos[Kernel.ThreadId.X * 4 + 2] = positions[j * 3 + 2];
                sharedPos[Kernel.ThreadId.X * 4 + 3] = masses[j];
            }
            else
            {
                sharedPos[Kernel.ThreadId.X * 4 + 3] = 0; // Zero mass for out-of-bounds
            }

            Kernel.SyncThreads();

            // Compute forces from all particles in tile
            for (int k = 0; k < 256 && tile * 256 + k < n; k++)
            {
                float qx = sharedPos[k * 4 + 0];
                float qy = sharedPos[k * 4 + 1];
                float qz = sharedPos[k * 4 + 2];
                float m = sharedPos[k * 4 + 3];

                float dx = qx - px;
                float dy = qy - py;
                float dz = qz - pz;

                float distSqr = dx * dx + dy * dy + dz * dz + softening * softening;
                float invDist = 1.0f / MathF.Sqrt(distSqr);
                float invDist3 = invDist * invDist * invDist;

                float f = G * m * invDist3;
                ax += f * dx;
                ay += f * dy;
                az += f * dz;
            }

            Kernel.SyncThreads();
        }

        // Store accelerations
        accelerations[i * 3 + 0] = ax;
        accelerations[i * 3 + 1] = ay;
        accelerations[i * 3 + 2] = az;
    }

    /// <summary>
    /// Integrate velocities using computed accelerations (Leapfrog step 1).
    /// </summary>
    [Kernel]
    static void IntegrateVelocityKernel(
        float[] velocities, float[] accelerations,
        int n, float dt)
    {
        int i = Kernel.ThreadId.X + Kernel.BlockId.X * Kernel.BlockDim.X;
        if (i >= n) return;

        velocities[i * 3 + 0] += accelerations[i * 3 + 0] * dt;
        velocities[i * 3 + 1] += accelerations[i * 3 + 1] * dt;
        velocities[i * 3 + 2] += accelerations[i * 3 + 2] * dt;
    }

    /// <summary>
    /// Integrate positions using updated velocities (Leapfrog step 2).
    /// </summary>
    [Kernel]
    static void IntegratePositionKernel(
        float[] positions, float[] velocities,
        int n, float dt)
    {
        int i = Kernel.ThreadId.X + Kernel.BlockId.X * Kernel.BlockDim.X;
        if (i >= n) return;

        positions[i * 3 + 0] += velocities[i * 3 + 0] * dt;
        positions[i * 3 + 1] += velocities[i * 3 + 1] * dt;
        positions[i * 3 + 2] += velocities[i * 3 + 2] * dt;
    }

    /// <summary>
    /// Compute final system statistics.
    /// </summary>
    static SimulationStatistics ComputeStatistics(float[] positions, float[] velocities, int n)
    {
        float comX = 0, comY = 0, comZ = 0;
        float totalSpeed = 0;
        float maxSpeed = 0;
        float maxRadius = 0;

        for (int i = 0; i < n; i++)
        {
            comX += positions[i * 3 + 0];
            comY += positions[i * 3 + 1];
            comZ += positions[i * 3 + 2];

            float vx = velocities[i * 3 + 0];
            float vy = velocities[i * 3 + 1];
            float vz = velocities[i * 3 + 2];
            float speed = MathF.Sqrt(vx * vx + vy * vy + vz * vz);
            totalSpeed += speed;
            maxSpeed = MathF.Max(maxSpeed, speed);

            float px = positions[i * 3 + 0];
            float py = positions[i * 3 + 1];
            float pz = positions[i * 3 + 2];
            float radius = MathF.Sqrt(px * px + py * py + pz * pz);
            maxRadius = MathF.Max(maxRadius, radius);
        }

        return new SimulationStatistics
        {
            CenterOfMass = (comX / n, comY / n, comZ / n),
            AverageSpeed = totalSpeed / n,
            MaxSpeed = maxSpeed,
            SystemRadius = maxRadius
        };
    }
}

public record struct SimulationStatistics
{
    public (float X, float Y, float Z) CenterOfMass { get; init; }
    public float AverageSpeed { get; init; }
    public float MaxSpeed { get; init; }
    public float SystemRadius { get; init; }
}
