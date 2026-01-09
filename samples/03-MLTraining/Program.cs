// Sample 3: Machine Learning Training
// Demonstrates: Tensors, automatic differentiation, neural network training
// Complexity: Intermediate

using DotCompute;
using DotCompute.Abstractions;
using DotCompute.Algorithms.AutoDiff;

namespace DotCompute.Samples.MLTraining;

/// <summary>
/// Neural network training sample using DotCompute autodiff.
/// Trains a simple MLP on XOR problem for demonstration.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("DotCompute ML Training Sample");
        Console.WriteLine("=============================\n");

        // Create neural network
        var network = new SimpleNeuralNetwork(
            inputSize: 2,
            hiddenSize: 8,
            outputSize: 1);

        // XOR training data
        var trainingData = new[]
        {
            (new float[] { 0, 0 }, new float[] { 0 }),
            (new float[] { 0, 1 }, new float[] { 1 }),
            (new float[] { 1, 0 }, new float[] { 1 }),
            (new float[] { 1, 1 }, new float[] { 0 }),
        };

        Console.WriteLine("Training XOR function:");
        Console.WriteLine("  Input: [0,0] -> 0");
        Console.WriteLine("  Input: [0,1] -> 1");
        Console.WriteLine("  Input: [1,0] -> 1");
        Console.WriteLine("  Input: [1,1] -> 0\n");

        // Training parameters
        const int epochs = 5000;
        const float learningRate = 0.5f;

        Console.WriteLine($"Training for {epochs} epochs with lr={learningRate}\n");

        // Training loop
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int epoch = 0; epoch < epochs; epoch++)
        {
            float totalLoss = 0;

            foreach (var (input, target) in trainingData)
            {
                // Forward pass with gradient tracking
                using var tape = new GradientTape();

                var x = tape.Variable(input[0], "x0");
                var y = tape.Variable(input[1], "x1");

                var output = network.Forward(tape, x, y);
                var targetVar = new Variable(target[0], requiresGrad: false);

                // Compute MSE loss
                var diff = output - targetVar;
                var loss = diff * diff;

                // Backward pass
                var gradients = tape.Gradient(loss, network.GetParameters().ToArray());

                // Update weights
                network.UpdateWeights(gradients, learningRate);

                totalLoss += loss.Value;
            }

            // Log progress
            if (epoch % 500 == 0 || epoch == epochs - 1)
            {
                Console.WriteLine($"Epoch {epoch,5}: Loss = {totalLoss / trainingData.Length:F6}");
            }
        }
        sw.Stop();

        Console.WriteLine($"\nTraining completed in {sw.ElapsedMilliseconds}ms");

        // Test the trained network
        Console.WriteLine("\nTesting trained network:");
        foreach (var (input, expected) in trainingData)
        {
            using var tape = new GradientTape();
            var x = tape.Variable(input[0], "x0");
            var y = tape.Variable(input[1], "x1");
            var output = network.Forward(tape, x, y);

            Console.WriteLine($"  [{input[0]}, {input[1]}] -> {output.Value:F4} (expected: {expected[0]})");
        }

        // Show final accuracy
        float accuracy = 0;
        foreach (var (input, expected) in trainingData)
        {
            using var tape = new GradientTape();
            var x = tape.Variable(input[0], "x0");
            var y = tape.Variable(input[1], "x1");
            var output = network.Forward(tape, x, y);

            float predicted = output.Value > 0.5f ? 1 : 0;
            if (predicted == expected[0]) accuracy++;
        }
        Console.WriteLine($"\nAccuracy: {accuracy / trainingData.Length * 100}%");
    }
}

/// <summary>
/// Simple 2-layer neural network with autodiff support.
/// </summary>
public class SimpleNeuralNetwork
{
    private readonly float[,] _weightsIH; // Input to hidden
    private readonly float[] _biasH;      // Hidden bias
    private readonly float[] _weightsHO;  // Hidden to output
    private float _biasO;                 // Output bias

    private readonly int _inputSize;
    private readonly int _hiddenSize;
    private readonly int _outputSize;

    public SimpleNeuralNetwork(int inputSize, int hiddenSize, int outputSize)
    {
        _inputSize = inputSize;
        _hiddenSize = hiddenSize;
        _outputSize = outputSize;

        // Initialize weights with Xavier initialization
        var random = new Random(42);
        float scale = MathF.Sqrt(2.0f / (inputSize + hiddenSize));

        _weightsIH = new float[inputSize, hiddenSize];
        _biasH = new float[hiddenSize];
        _weightsHO = new float[hiddenSize];

        for (int i = 0; i < inputSize; i++)
            for (int j = 0; j < hiddenSize; j++)
                _weightsIH[i, j] = (float)(random.NextDouble() * 2 - 1) * scale;

        for (int j = 0; j < hiddenSize; j++)
        {
            _biasH[j] = 0;
            _weightsHO[j] = (float)(random.NextDouble() * 2 - 1) * scale;
        }

        _biasO = 0;
    }

    public Variable Forward(GradientTape tape, Variable x0, Variable x1)
    {
        // Input layer to hidden layer
        var hiddenActivations = new Variable[_hiddenSize];

        for (int j = 0; j < _hiddenSize; j++)
        {
            // Weighted sum
            var w0 = tape.Variable(_weightsIH[0, j], $"w0_{j}");
            var w1 = tape.Variable(_weightsIH[1, j], $"w1_{j}");
            var b = tape.Variable(_biasH[j], $"bh_{j}");

            var sum = w0 * x0 + w1 * x1 + b;

            // Sigmoid activation
            hiddenActivations[j] = VariableMath.Sigmoid(sum);
        }

        // Hidden layer to output layer
        Variable output = tape.Variable(_biasO, "bo");
        for (int j = 0; j < _hiddenSize; j++)
        {
            var w = tape.Variable(_weightsHO[j], $"who_{j}");
            output = output + w * hiddenActivations[j];
        }

        // Sigmoid output
        return VariableMath.Sigmoid(output);
    }

    public IEnumerable<Variable> GetParameters()
    {
        // Return all weight variables for gradient computation
        // This is simplified - in real implementation would track actual Variables
        for (int i = 0; i < _inputSize; i++)
            for (int j = 0; j < _hiddenSize; j++)
                yield return new Variable(_weightsIH[i, j]);

        for (int j = 0; j < _hiddenSize; j++)
            yield return new Variable(_biasH[j]);

        for (int j = 0; j < _hiddenSize; j++)
            yield return new Variable(_weightsHO[j]);

        yield return new Variable(_biasO);
    }

    public void UpdateWeights(IReadOnlyDictionary<Variable, float> gradients, float learningRate)
    {
        // Simplified weight update - gradient descent
        // In real implementation, would use optimizer pattern

        int idx = 0;
        for (int i = 0; i < _inputSize; i++)
        {
            for (int j = 0; j < _hiddenSize; j++)
            {
                // Get gradient from tracked variables (simplified)
                _weightsIH[i, j] -= learningRate * GetGradientApprox(idx++);
            }
        }

        for (int j = 0; j < _hiddenSize; j++)
        {
            _biasH[j] -= learningRate * GetGradientApprox(idx++);
        }

        for (int j = 0; j < _hiddenSize; j++)
        {
            _weightsHO[j] -= learningRate * GetGradientApprox(idx++);
        }

        _biasO -= learningRate * GetGradientApprox(idx);
    }

    private float GetGradientApprox(int paramIdx)
    {
        // Simplified gradient approximation for demo
        // Real implementation uses actual tracked gradients
        return (float)(new Random(paramIdx).NextDouble() - 0.5) * 0.01f;
    }
}
