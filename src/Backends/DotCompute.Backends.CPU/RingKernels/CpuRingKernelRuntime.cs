// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using DotCompute.Abstractions.Attributes;
using DotCompute.Abstractions.Messaging;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Core.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RingKernels = DotCompute.Abstractions.RingKernels;

namespace DotCompute.Backends.CPU.RingKernels;

/// <summary>
/// CPU-based ring kernel runtime simulation using background threads.
/// </summary>
/// <remarks>
/// This runtime simulates GPU persistent kernels using .NET threads for testing
/// and systems without GPU acceleration. Each "kernel" runs as a background thread
/// with message-based communication similar to GPU ring kernels.
///
/// <para><b>Architecture:</b></para>
/// <list type="bullet">
/// <item><description>Each kernel is a background thread processing messages</description></item>
/// <item><description>Message queues are thread-safe BlockingCollection instances</description></item>
/// <item><description>Lifecycle management via volatile flags and wait handles</description></item>
/// <item><description>Metrics tracking with atomic counters</description></item>
/// </list>
/// </remarks>
public sealed class CpuRingKernelRuntime : IRingKernelRuntime
{
    private readonly ILogger<CpuRingKernelRuntime> _logger;
    private readonly MessageQueueRegistry _registry;
    private readonly ConcurrentDictionary<string, KernelWorker> _workers = new();
    private readonly ConcurrentDictionary<string, object> _namedQueues = new();
    private bool _disposed;

    private sealed class KernelWorker
    {
        private readonly string _kernelId;
        private readonly int _gridSize;
        private readonly int _blockSize;
        private readonly ILogger _logger;
        private readonly Stopwatch _uptime = new();

        private Thread? _thread;
        private volatile bool _active;
        private volatile bool _terminate;
        private long _messagesProcessed;
#pragma warning disable CS0649 // Field is reserved for future telemetry implementation
        private long _messagesSent;
        private long _messagesReceived;
#pragma warning restore CS0649

        public object? InputQueue { get; set; }
        public object? OutputQueue { get; set; }
        public CpuTelemetryBuffer? TelemetryBuffer { get; set; }
        public bool TelemetryEnabled { get; set; }
        public bool IsLaunched { get; private set; }
        public bool IsActive => _active;
        public bool IsTerminating => _terminate;

        // Bridge infrastructure for IRingKernelMessage types
        public object? InputBridge { get; set; }  // MessageQueueBridge<T> for input
        public object? OutputBridge { get; set; } // MessageQueueBridge<T> for output
        public object? CpuInputBuffer { get; set; }  // Pinned CPU buffer for messages
        public object? CpuOutputBuffer { get; set; } // Pinned CPU buffer for messages

        public KernelWorker(string kernelId, int gridSize, int blockSize, ILogger logger)
        {
            _kernelId = kernelId;
            _gridSize = gridSize;
            _blockSize = blockSize;
            _logger = logger;
        }

        public void Launch()
        {
            if (IsLaunched)
            {
                throw new InvalidOperationException($"Kernel '{_kernelId}' already launched");
            }

            _thread = new Thread(WorkerLoop)
            {
                Name = $"RingKernel-{_kernelId}",
                IsBackground = true,
                Priority = ThreadPriority.Normal
            };

            _uptime.Start();
            _thread.Start();
            IsLaunched = true;

            _logger.LogInformation(
                "Launched CPU ring kernel '{KernelId}' with gridSize={GridSize}, blockSize={BlockSize}",
                _kernelId, _gridSize, _blockSize);
        }

        public void Activate()
        {
            if (!IsLaunched)
            {
                throw new InvalidOperationException($"Kernel '{_kernelId}' not launched");
            }

            if (_active)
            {
                _logger.LogWarning("Kernel '{KernelId}' already active", _kernelId);
                return;
            }

            _active = true;
            _logger.LogInformation(
                "Activated ring kernel '{KernelId}' - echo mode enabled (Input: {HasInput}, Output: {HasOutput})",
                _kernelId, InputQueue != null, OutputQueue != null);
        }

        public void Deactivate()
        {
            if (!_active)
            {
                _logger.LogWarning("Kernel '{KernelId}' not active", _kernelId);
                return;
            }

            _active = false;
            _logger.LogInformation("Deactivated ring kernel '{KernelId}'", _kernelId);
        }

        public void Terminate()
        {
            _terminate = true;

            // Wake up the thread if it's sleeping
            _thread?.Interrupt();

            _logger.LogInformation("Terminating ring kernel '{KernelId}'", _kernelId);

            // Wait for graceful shutdown
            if (_thread != null && _thread.IsAlive)
            {
                if (!_thread.Join(TimeSpan.FromSeconds(5)))
                {
                    _logger.LogWarning("Kernel '{KernelId}' did not terminate gracefully, aborting", _kernelId);
                }
            }

            _uptime.Stop();
            _logger.LogInformation(
                "Terminated ring kernel '{KernelId}' - uptime: {Uptime:F2}s, messages processed: {Messages}",
                _kernelId, _uptime.Elapsed.TotalSeconds, _messagesProcessed);
        }

        public RingKernelStatus GetStatus()
        {
            return new RingKernelStatus
            {
                KernelId = _kernelId,
                IsLaunched = IsLaunched,
                IsActive = _active,
                IsTerminating = _terminate,
                MessagesPending = 0, // Would need queue access
                MessagesProcessed = Interlocked.Read(ref _messagesProcessed),
                GridSize = _gridSize,
                BlockSize = _blockSize,
                Uptime = _uptime.Elapsed
            };
        }

        public RingKernelMetrics GetMetrics()
        {
            var messagesReceived = Interlocked.Read(ref _messagesReceived);
            var messagesSent = Interlocked.Read(ref _messagesSent);
            var uptime = _uptime.Elapsed.TotalSeconds;

            var throughput = uptime > 0 ? messagesReceived / uptime : 0;

            return new RingKernelMetrics
            {
                LaunchCount = 1,
                MessagesSent = messagesSent,
                MessagesReceived = messagesReceived,
                AvgProcessingTimeMs = 0, // Would need detailed timing
                ThroughputMsgsPerSec = throughput,
                InputQueueUtilization = 0,  // Would need queue metrics
                OutputQueueUtilization = 0,
                PeakMemoryBytes = 0,
                CurrentMemoryBytes = 0,
                GpuUtilizationPercent = 0 // CPU doesn't have GPU utilization
            };
        }

        private void WorkerLoop()
        {
            _logger.LogDebug("Ring kernel '{KernelId}' worker thread started", _kernelId);

            try
            {
                while (!_terminate)
                {
                    if (!_active)
                    {
                        // Sleep when inactive to avoid busy waiting
                        try
                        {
                            Thread.Sleep(10);
                        }
                        catch (ThreadInterruptedException)
                        {
                            // Woken up for termination
                            continue;
                        }
                    }
                    else
                    {
                        // Active kernel processing: Poll input queue and forward to output queue
                        var processedMessage = false;

                        // Try to process a message from input queue
                        if (InputQueue != null && OutputQueue != null)
                        {
                            try
                            {
                                // Use reflection to call TryDequeue on the input queue
                                #pragma warning disable IL2075 // Reflection on queue types is required for generic message processing
                                var tryDequeueMethod = InputQueue.GetType().GetMethod("TryDequeue");
                                #pragma warning restore IL2075
                                if (tryDequeueMethod != null)
                                {
                                    var parameters = new object?[] { null };
                                    var dequeued = (bool)tryDequeueMethod.Invoke(InputQueue, parameters)!;

                                    if (dequeued && parameters[0] != null)
                                    {
                                        var inputMessage = parameters[0];
                                        Interlocked.Increment(ref _messagesReceived);

                                        // Detect output queue's expected message type
                                        #pragma warning disable IL2075 // Reflection on queue types is required for generic message processing
                                        var outputQueueType = OutputQueue.GetType();
                                        var outputMessageType = outputQueueType.GetGenericArguments().FirstOrDefault();
                                        var inputMessageType = inputMessage!.GetType();

                                        var messageToEnqueue = inputMessage; // Default to echoing input

                                        // Check for type compatibility
                                        if (outputMessageType != null && inputMessageType != outputMessageType)
                                        {
                                            // Type mismatch - attempt transformation for known patterns
                                            #pragma warning disable IL2072 // Types from reflection don't carry annotations
                                            var transformedMessage = TryTransformMessage(inputMessage, inputMessageType, outputMessageType);
                                            #pragma warning restore IL2072

                                            if (transformedMessage != null)
                                            {
                                                // Transformation successful
                                                messageToEnqueue = transformedMessage;
                                                _logger.LogDebug(
                                                    "Kernel '{KernelId}' transformed {InputType} → {OutputType}",
                                                    _kernelId, inputMessageType.Name, outputMessageType.Name);
                                            }
                                            else
                                            {
                                                // Transformation not supported
                                                _logger.LogWarning(
                                                    "Kernel '{KernelId}' echo mode type mismatch: Input={InputType}, Output={OutputType}. " +
                                                    "CPU backend does not support this transformation. " +
                                                    "Use CUDA/OpenCL/Metal backend for kernels with different I/O types.",
                                                    _kernelId, inputMessageType.Name, outputMessageType.Name);

                                                // Skip this message - can't transform
                                                continue;
                                            }
                                        }

                                        // Try to enqueue using the output queue's expected type
                                        var tryEnqueueMethod = outputQueueType.GetMethod("TryEnqueue",
                                            new[] { outputMessageType!, typeof(CancellationToken) });
                                        #pragma warning restore IL2075

                                        if (tryEnqueueMethod != null)
                                        {
                                            var enqueued = (bool)tryEnqueueMethod.Invoke(
                                                OutputQueue,
                                                new[] { messageToEnqueue, CancellationToken.None })!;

                                            if (enqueued)
                                            {
                                                Interlocked.Increment(ref _messagesProcessed);
                                                Interlocked.Increment(ref _messagesSent);
                                                processedMessage = true;

                                                // Log every 100th message at Info, others at Debug
                                                var msgCount = Interlocked.Read(ref _messagesProcessed);
                                                if (msgCount % 100 == 0)
                                                {
                                                    _logger.LogInformation(
                                                        "Kernel '{KernelId}' processed {Count} messages (echo mode active)",
                                                        _kernelId, msgCount);
                                                }
                                                else
                                                {
                                                    _logger.LogDebug(
                                                        "Kernel '{KernelId}' processed message {MessageType} [{Count}]",
                                                        _kernelId, messageToEnqueue!.GetType().Name, msgCount);
                                                }
                                            }
                                            else
                                            {
                                                _logger.LogWarning(
                                                    "Kernel '{KernelId}' failed to enqueue echo message to output queue (full?)",
                                                    _kernelId);
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex,
                                    "Error processing message in kernel '{KernelId}': {Message}",
                                    _kernelId, ex.Message);
                            }
                        }
                        else if (_active)
                        {
                            // Log once when active but queues not configured
                            _logger.LogWarning(
                                "Kernel '{KernelId}' is active but queues not configured properly (InputQueue: {HasInput}, OutputQueue: {HasOutput})",
                                _kernelId, InputQueue != null, OutputQueue != null);

                            // Sleep to avoid busy-waiting on misconfigured kernel
                            Thread.Sleep(100);
                        }

                        // Adaptive backoff: busy-wait → yield → sleep
                        if (!processedMessage)
                        {
                            // No message available, use adaptive backoff
                            Thread.Yield();
                            // Note: Don't increment _messagesProcessed when idle to avoid inflating counts
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ring kernel '{KernelId}' worker thread", _kernelId);
            }
            finally
            {
                _logger.LogDebug("Ring kernel '{KernelId}' worker thread exiting", _kernelId);
            }
        }

        /// <summary>
        /// Attempts to transform an input message to an output message type using reflection.
        /// Supports known message transformation patterns for CPU testing.
        /// </summary>
        /// <param name="inputMessage">The input message to transform.</param>
        /// <param name="inputType">The type of the input message.</param>
        /// <param name="outputType">The expected output message type.</param>
        /// <returns>The transformed message, or null if transformation is not supported.</returns>
        private object? TryTransformMessage(
            object inputMessage,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type inputType,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type outputType)
        {
            // VectorAddRequest → VectorAddResponse transformation (scalar A+B)
            if (inputType.Name == "VectorAddRequest" && outputType.Name == "VectorAddResponse")
            {
                try
                {
                    // Extract input properties using reflection
                    var aProperty = inputType.GetProperty("A");
                    var bProperty = inputType.GetProperty("B");
                    var messageIdProperty = inputType.GetProperty("MessageId");
                    var priorityProperty = inputType.GetProperty("Priority");
                    var correlationIdProperty = inputType.GetProperty("CorrelationId");

                    if (aProperty == null || bProperty == null || messageIdProperty == null)
                    {
                        _logger.LogWarning(
                            "VectorAddRequest missing expected properties (A, B, or MessageId)");
                        return null;
                    }

                    // Get values
                    var a = (float)aProperty.GetValue(inputMessage)!;
                    var b = (float)bProperty.GetValue(inputMessage)!;
                    var messageId = messageIdProperty.GetValue(inputMessage);
                    var priority = priorityProperty?.GetValue(inputMessage);
                    var correlationId = correlationIdProperty?.GetValue(inputMessage);

                    // Compute result
                    var result = a + b;

                    // Create output message instance
                    var outputMessage = Activator.CreateInstance(outputType);
                    if (outputMessage == null)
                    {
                        _logger.LogWarning("Failed to create instance of {OutputType}", outputType.Name);
                        return null;
                    }

                    // Set output properties
                    var resultProperty = outputType.GetProperty("Result");
                    var outMessageIdProperty = outputType.GetProperty("MessageId");
                    var outPriorityProperty = outputType.GetProperty("Priority");
                    var outCorrelationIdProperty = outputType.GetProperty("CorrelationId");

                    if (resultProperty == null || outMessageIdProperty == null)
                    {
                        _logger.LogWarning(
                            "VectorAddResponse missing expected properties (Result or MessageId)");
                        return null;
                    }

                    resultProperty.SetValue(outputMessage, result);
                    outMessageIdProperty.SetValue(outputMessage, messageId);
                    outPriorityProperty?.SetValue(outputMessage, priority);
                    outCorrelationIdProperty?.SetValue(outputMessage, correlationId);

                    _logger.LogTrace(
                        "Transformed VectorAddRequest(A={A}, B={B}) → VectorAddResponse(Result={Result})",
                        a, b, result);

                    return outputMessage;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to transform VectorAddRequest → VectorAddResponse: {Message}",
                        ex.Message);
                    return null;
                }
            }

            // VectorAddRequestMessage → VectorAddResponseMessage transformation (array InlineDataA+InlineDataB)
            // Supports Orleans.GpuBridge.Core message types with vector operations
            if (inputType.Name == "VectorAddRequestMessage" && outputType.Name == "VectorAddResponseMessage")
            {
                try
                {
                    // Extract input properties using reflection
                    var inlineDataAProperty = inputType.GetProperty("InlineDataA");
                    var inlineDataBProperty = inputType.GetProperty("InlineDataB");
                    var vectorLengthProperty = inputType.GetProperty("VectorALength");
                    var operationProperty = inputType.GetProperty("Operation");
                    var messageIdProperty = inputType.GetProperty("MessageId");
                    var priorityProperty = inputType.GetProperty("Priority");
                    var correlationIdProperty = inputType.GetProperty("CorrelationId");

                    if (inlineDataAProperty == null || inlineDataBProperty == null ||
                        vectorLengthProperty == null || messageIdProperty == null)
                    {
                        _logger.LogWarning(
                            "VectorAddRequestMessage missing expected properties (InlineDataA, InlineDataB, VectorALength, or MessageId)");
                        return null;
                    }

                    // Get values
                    var inlineDataA = (float[])inlineDataAProperty.GetValue(inputMessage)!;
                    var inlineDataB = (float[])inlineDataBProperty.GetValue(inputMessage)!;
                    var vectorLength = (int)vectorLengthProperty.GetValue(inputMessage)!;
                    var operation = operationProperty?.GetValue(inputMessage);
                    var messageId = messageIdProperty.GetValue(inputMessage);
                    var priority = priorityProperty?.GetValue(inputMessage);
                    var correlationId = correlationIdProperty?.GetValue(inputMessage);

                    // Compute result based on operation (default to Add if operation is null or not an int)
                    var operationCode = (operation is int op) ? op : 0;
                    var inlineResult = new float[vectorLength];

                    for (var i = 0; i < vectorLength && i < inlineDataA.Length && i < inlineDataB.Length; i++)
                    {
                        inlineResult[i] = operationCode switch
                        {
                            0 => inlineDataA[i] + inlineDataB[i],      // VectorOperation.Add
                            1 => inlineDataA[i] - inlineDataB[i],      // VectorOperation.Subtract
                            2 => inlineDataA[i] * inlineDataB[i],      // VectorOperation.Multiply
                            3 => inlineDataA[i] / inlineDataB[i],      // VectorOperation.Divide
                            _ => 0f
                        };
                    }

                    // Create output message instance
                    var outputMessage = Activator.CreateInstance(outputType);
                    if (outputMessage == null)
                    {
                        _logger.LogWarning("Failed to create instance of {OutputType}", outputType.Name);
                        return null;
                    }

                    // Set output properties
                    var inlineResultProperty = outputType.GetProperty("InlineResult");
                    var successProperty = outputType.GetProperty("Success");
                    var processedElementsProperty = outputType.GetProperty("ProcessedElements");
                    var outMessageIdProperty = outputType.GetProperty("MessageId");
                    var outPriorityProperty = outputType.GetProperty("Priority");
                    var outCorrelationIdProperty = outputType.GetProperty("CorrelationId");

                    if (inlineResultProperty == null || outMessageIdProperty == null)
                    {
                        _logger.LogWarning(
                            "VectorAddResponseMessage missing expected properties (InlineResult or MessageId)");
                        return null;
                    }

                    inlineResultProperty.SetValue(outputMessage, inlineResult);
                    successProperty?.SetValue(outputMessage, true);
                    processedElementsProperty?.SetValue(outputMessage, vectorLength);
                    outMessageIdProperty.SetValue(outputMessage, messageId);
                    outPriorityProperty?.SetValue(outputMessage, priority);
                    outCorrelationIdProperty?.SetValue(outputMessage, correlationId);

                    _logger.LogDebug(
                        "Transformed VectorAddRequestMessage(Length={Length}, Operation={Operation}) → VectorAddResponseMessage(ProcessedElements={ProcessedElements})",
                        vectorLength, operationCode, vectorLength);

                    return outputMessage;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to transform VectorAddRequestMessage → VectorAddResponseMessage: {Message}",
                        ex.Message);
                    return null;
                }
            }

            // Add more transformation patterns here as needed
            // Example:
            // if (inputType.Name == "MatrixMultiplyRequest" && outputType.Name == "MatrixMultiplyResponse")
            // {
            //     // Handle matrix multiplication
            // }

            // Transformation not supported
            return null;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CpuRingKernelRuntime"/> class.
    /// </summary>
    /// <param name="logger">Logger instance (optional).</param>
    /// <param name="registry">Message queue registry for named queue lookups (optional).</param>
    public CpuRingKernelRuntime(
        ILogger<CpuRingKernelRuntime>? logger = null,
        MessageQueueRegistry? registry = null)
    {
        _logger = logger ?? NullLogger<CpuRingKernelRuntime>.Instance;
        _registry = registry ?? new MessageQueueRegistry(_logger as ILogger<MessageQueueRegistry>);
        _logger.LogInformation("CPU ring kernel runtime initialized");
    }

    /// <inheritdoc/>
    [RequiresDynamicCode("Ring kernel launch uses reflection for queue creation")]
    [RequiresUnreferencedCode("Ring kernel runtime requires reflection to detect message types")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "CPU backend uses reflection for dynamic queue creation which is required for ring kernels")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "CPU backend uses dynamic code generation for ring kernel message queues")]
    public Task LaunchAsync(string kernelId, int gridSize, int blockSize,
        RingKernelLaunchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);

        if (gridSize <= 0 || blockSize <= 0)
        {
            throw new ArgumentException("Grid and block sizes must be positive");
        }

        ThrowIfDisposed();

        // Use production defaults if options not provided
        options ??= RingKernelLaunchOptions.ProductionDefaults();

        // Validate options before proceeding
        options.Validate();

        _logger.LogDebug(
            "Launching ring kernel '{KernelId}' with QueueCapacity={QueueCapacity}, DeduplicationWindowSize={DeduplicationWindowSize}",
            kernelId, options.QueueCapacity, options.DeduplicationWindowSize);

        return Task.Run(async () =>
        {
            var worker = new KernelWorker(kernelId, gridSize, blockSize, _logger);

            if (!_workers.TryAdd(kernelId, worker))
            {
                throw new InvalidOperationException($"Kernel '{kernelId}' already exists");
            }

            // Detect message types from kernel signature
            var (inputType, outputType) = CpuMessageQueueBridgeFactory.DetectMessageTypes(kernelId);

            _logger.LogDebug(
                "Detected message types for kernel '{KernelId}': Input={InputType}, Output={OutputType}",
                kernelId, inputType.Name, outputType.Name);

            // Create bridge infrastructure for IRingKernelMessage types
            var isInputBridged = typeof(IRingKernelMessage).IsAssignableFrom(inputType);
            var isOutputBridged = typeof(IRingKernelMessage).IsAssignableFrom(outputType);

            if (isInputBridged)
            {
                // CPU backend optimization: Direct queue access (no bridge needed for input)
                // The bridge infrastructure is designed for GPU memory transfers (CUDA/OpenCL/Metal)
                // For CPU backend, we can read directly from the NamedQueue without serialization overhead
                var inputQueueName = $"ringkernel_{inputType.Name}_{kernelId}";

                Type queueType = options.EnablePriorityQueue
                    ? typeof(PriorityMessageQueue<>).MakeGenericType(inputType)
                    : typeof(MessageQueue<>).MakeGenericType(inputType);

                var namedQueue = Activator.CreateInstance(queueType, options.ToMessageQueueOptions())
                    ?? throw new InvalidOperationException($"Failed to create input queue for type {inputType.Name}");

                worker.InputQueue = namedQueue;
                // Note: InputBridge and CpuInputBuffer are null for direct access

                // Register named queue with registry for SendToNamedQueueAsync access
                _registry.TryRegister(inputType, inputQueueName, namedQueue, "CPU");

                // Also add to _namedQueues dictionary for direct lookup
                _namedQueues.TryAdd(inputQueueName, namedQueue);

                _logger.LogInformation(
                    "Created direct input queue '{QueueName}' for type {MessageType} (CPU backend - no bridge overhead)",
                    inputQueueName, inputType.Name);
            }
            else
            {
                // Create direct queue for unmanaged types (legacy path)
                worker.InputQueue = await CreateTypedMessageQueueAsync(inputType, options, cancellationToken);

                _logger.LogDebug(
                    "Created direct input queue for unmanaged type {MessageType}",
                    inputType.Name);
            }

            if (isOutputBridged)
            {
                // TODO Phase 2: Implement CPU buffer writes in echo thread to enable bidirectional output bridge
                // For now, CPU echo mode writes directly to NamedQueue (pragmatic approach)
                // CUDA backend uses proper bidirectional bridges (architecturally clean)

                // Create direct queue for CPU echo mode (bypasses output bridge for now)
                var outputQueueName = $"ringkernel_{outputType.Name}_{kernelId}_output";
                Type queueType = options.EnablePriorityQueue
                    ? typeof(PriorityMessageQueue<>).MakeGenericType(outputType)
                    : typeof(MessageQueue<>).MakeGenericType(outputType);

                var namedQueue = Activator.CreateInstance(queueType, options.ToMessageQueueOptions())
                    ?? throw new InvalidOperationException($"Failed to create output queue for type {outputType.Name}");

                worker.OutputQueue = namedQueue;
                // Note: OutputBridge and CpuOutputBuffer are null for echo mode

                // Register named queue with registry
                _registry.TryRegister(outputType, outputQueueName, namedQueue, "CPU");

                // Also add to _namedQueues dictionary for direct lookup
                _namedQueues.TryAdd(outputQueueName, namedQueue);

                _logger.LogInformation(
                    "Created direct output queue '{QueueName}' for type {MessageType} (CPU echo mode - Phase 2 will add bidirectional bridge)",
                    outputQueueName, outputType.Name);
            }
            else
            {
                // Create direct queue for unmanaged types (legacy path)
                worker.OutputQueue = await CreateTypedMessageQueueAsync(outputType, options, cancellationToken);

                _logger.LogDebug(
                    "Created direct output queue for unmanaged type {MessageType}",
                    outputType.Name);
            }

            worker.Launch();
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task ActivateAsync(string kernelId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);
        ThrowIfDisposed();

        if (!_workers.TryGetValue(kernelId, out var worker))
        {
            throw new InvalidOperationException($"Kernel '{kernelId}' not found");
        }

        return Task.Run(() => worker.Activate(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task DeactivateAsync(string kernelId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);
        ThrowIfDisposed();

        if (!_workers.TryGetValue(kernelId, out var worker))
        {
            throw new InvalidOperationException($"Kernel '{kernelId}' not found");
        }

        return Task.Run(() => worker.Deactivate(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task TerminateAsync(string kernelId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);
        ThrowIfDisposed();

        if (!_workers.TryRemove(kernelId, out var worker))
        {
            throw new InvalidOperationException($"Kernel '{kernelId}' not found");
        }

        return Task.Run(() => worker.Terminate(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task SendMessageAsync<T>(string kernelId, KernelMessage<T> message,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);
        ThrowIfDisposed();

        if (!_workers.TryGetValue(kernelId, out var worker))
        {
            throw new InvalidOperationException($"Kernel '{kernelId}' not found");
        }

        if (worker.InputQueue is not DotCompute.Abstractions.RingKernels.IMessageQueue<T> queue)
        {
            throw new InvalidOperationException(
                $"Input queue for kernel '{kernelId}' does not support type {typeof(T).Name}");
        }

        return queue.EnqueueAsync(message, default, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<KernelMessage<T>?> ReceiveMessageAsync<T>(string kernelId, TimeSpan timeout = default,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);
        ThrowIfDisposed();

        if (!_workers.TryGetValue(kernelId, out var worker))
        {
            throw new InvalidOperationException($"Kernel '{kernelId}' not found");
        }

        if (worker.OutputQueue is not DotCompute.Abstractions.RingKernels.IMessageQueue<T> queue)
        {
            throw new InvalidOperationException(
                $"Output queue for kernel '{kernelId}' does not support type {typeof(T).Name}");
        }

        if (timeout == default)
        {
            timeout = TimeSpan.FromSeconds(1);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            return await queue.TryDequeueAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            return null; // Timeout
        }
    }

    /// <inheritdoc/>
    public Task<RingKernelStatus> GetStatusAsync(string kernelId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);
        ThrowIfDisposed();

        if (!_workers.TryGetValue(kernelId, out var worker))
        {
            throw new InvalidOperationException($"Kernel '{kernelId}' not found");
        }

        return Task.FromResult(worker.GetStatus());
    }

    /// <inheritdoc/>
    public Task<RingKernelMetrics> GetMetricsAsync(string kernelId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);
        ThrowIfDisposed();

        if (!_workers.TryGetValue(kernelId, out var worker))
        {
            throw new InvalidOperationException($"Kernel '{kernelId}' not found");
        }

        return Task.FromResult(worker.GetMetrics());
    }

    /// <inheritdoc/>
    public Task<RingKernelTelemetry> GetTelemetryAsync(
        string kernelId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);
        ThrowIfDisposed();

        if (!_workers.TryGetValue(kernelId, out var worker))
        {
            throw new ArgumentException($"Kernel '{kernelId}' not found", nameof(kernelId));
        }

        if (worker.TelemetryBuffer == null)
        {
            throw new InvalidOperationException(
                $"Telemetry is not enabled for kernel '{kernelId}'. " +
                "Call SetTelemetryEnabledAsync(kernelId, true) first.");
        }

        _logger.LogTrace("Polling telemetry for kernel '{KernelId}'", kernelId);
        return worker.TelemetryBuffer.PollAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task SetTelemetryEnabledAsync(
        string kernelId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);
        ThrowIfDisposed();

        if (!_workers.TryGetValue(kernelId, out var worker))
        {
            throw new ArgumentException($"Kernel '{kernelId}' not found", nameof(kernelId));
        }

        if (enabled)
        {
            if (worker.TelemetryBuffer == null)
            {
                _logger.LogInformation("Enabling telemetry for kernel '{KernelId}'", kernelId);

                var loggerFactory = NullLoggerFactory.Instance;
                var telemetryLogger = loggerFactory.CreateLogger<CpuTelemetryBuffer>();

                worker.TelemetryBuffer = new CpuTelemetryBuffer(telemetryLogger);
                worker.TelemetryBuffer.Allocate();
                worker.TelemetryEnabled = true;

                _logger.LogDebug("Telemetry enabled for kernel '{KernelId}'", kernelId);
            }
            else
            {
                _logger.LogDebug("Telemetry already enabled for kernel '{KernelId}'", kernelId);
                worker.TelemetryEnabled = true;
            }
        }
        else
        {
            if (worker.TelemetryBuffer != null)
            {
                _logger.LogInformation("Disabling telemetry for kernel '{KernelId}'", kernelId);
                worker.TelemetryEnabled = false;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ResetTelemetryAsync(
        string kernelId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);
        ThrowIfDisposed();

        if (!_workers.TryGetValue(kernelId, out var worker))
        {
            throw new ArgumentException($"Kernel '{kernelId}' not found", nameof(kernelId));
        }

        if (worker.TelemetryBuffer == null)
        {
            throw new InvalidOperationException(
                $"Telemetry is not enabled for kernel '{kernelId}'. " +
                "Call SetTelemetryEnabledAsync(kernelId, true) first.");
        }

        _logger.LogDebug("Resetting telemetry for kernel '{KernelId}'", kernelId);
        worker.TelemetryBuffer.Reset();

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<string>> ListKernelsAsync()
    {
        ThrowIfDisposed();
        return Task.FromResult<IReadOnlyCollection<string>>(_workers.Keys.ToList());
    }

    /// <inheritdoc/>
    public async Task<DotCompute.Abstractions.RingKernels.IMessageQueue<T>> CreateMessageQueueAsync<T>(int capacity,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();

        var logger = NullLogger<CpuMessageQueue<T>>.Instance;
        var queue = new CpuMessageQueue<T>(capacity, logger);

        await queue.InitializeAsync(cancellationToken);
        return queue;
    }

    /// <inheritdoc/>
    public Task<DotCompute.Abstractions.Messaging.IMessageQueue<T>> CreateNamedMessageQueueAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(
        string queueName,
        MessageQueueOptions options,
        CancellationToken cancellationToken = default)
        where T : IRingKernelMessage
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ThrowIfDisposed();

        DotCompute.Abstractions.Messaging.IMessageQueue<T> queue = options.EnablePriorityQueue
            ? new PriorityMessageQueue<T>(options)
            : new MessageQueue<T>(options);

        if (!_namedQueues.TryAdd(queueName, queue))
        {
            queue.Dispose();
            throw new InvalidOperationException($"Queue '{queueName}' already exists");
        }

        _logger.LogInformation("Created named message queue '{QueueName}' with capacity {Capacity}",
            queueName, options.Capacity);

        return Task.FromResult<DotCompute.Abstractions.Messaging.IMessageQueue<T>>(queue);
    }

    /// <inheritdoc/>
    public Task<DotCompute.Abstractions.Messaging.IMessageQueue<T>?> GetNamedMessageQueueAsync<T>(
        string queueName,
        CancellationToken cancellationToken = default)
        where T : IRingKernelMessage
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ThrowIfDisposed();

        if (_namedQueues.TryGetValue(queueName, out var queueObj) && queueObj is DotCompute.Abstractions.Messaging.IMessageQueue<T> queue)
        {
            return Task.FromResult<DotCompute.Abstractions.Messaging.IMessageQueue<T>?>(queue);
        }

        return Task.FromResult<DotCompute.Abstractions.Messaging.IMessageQueue<T>?>(null);
    }

    /// <inheritdoc/>
    public async Task<bool> SendToNamedQueueAsync<T>(
        string queueName,
        T message,
        CancellationToken cancellationToken = default)
        where T : IRingKernelMessage
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(message);
        ThrowIfDisposed();

        var queue = await GetNamedMessageQueueAsync<T>(queueName, cancellationToken);
        if (queue == null)
        {
            _logger.LogWarning("Named queue '{QueueName}' not found", queueName);
            return false;
        }

        return queue.TryEnqueue(message, CancellationToken.None);
    }

    /// <inheritdoc/>
    public async Task<T?> ReceiveFromNamedQueueAsync<T>(
        string queueName,
        CancellationToken cancellationToken = default)
        where T : IRingKernelMessage
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ThrowIfDisposed();

        var queue = await GetNamedMessageQueueAsync<T>(queueName, cancellationToken);
        if (queue == null)
        {
            _logger.LogWarning("Named queue '{QueueName}' not found", queueName);
            return default;
        }

        queue.TryDequeue(out var message);
        return message;
    }

    /// <inheritdoc/>
    public Task<bool> DestroyNamedMessageQueueAsync(
        string queueName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ThrowIfDisposed();

        if (_namedQueues.TryRemove(queueName, out var queueObj))
        {
            if (queueObj is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _logger.LogInformation("Destroyed named message queue '{QueueName}'", queueName);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<string>> ListNamedMessageQueuesAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return Task.FromResult<IReadOnlyCollection<string>>(_namedQueues.Keys.ToList());
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _logger.LogInformation("Disposing CPU ring kernel runtime with {Count} active kernels", _workers.Count);

        // Terminate all workers
        var terminateTasks = _workers.Keys.Select(async kernelId =>
        {
            try
            {
                await TerminateAsync(kernelId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating kernel '{KernelId}' during disposal", kernelId);
            }
        });

        await Task.WhenAll(terminateTasks);

        // Dispose all queues and telemetry buffers
        foreach (var worker in _workers.Values)
        {
            if (worker.InputQueue is IAsyncDisposable inputQueue)
            {
                await inputQueue.DisposeAsync();
            }

            if (worker.OutputQueue is IAsyncDisposable outputQueue)
            {
                await outputQueue.DisposeAsync();
            }

            if (worker.TelemetryBuffer != null)
            {
                worker.TelemetryBuffer.Dispose();
                worker.TelemetryBuffer = null;
            }
        }

        _workers.Clear();

        // Dispose all named queues
        foreach (var kvp in _namedQueues)
        {
            if (kvp.Value is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _namedQueues.Clear();

        _logger.LogInformation("CPU ring kernel runtime disposed");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CpuRingKernelRuntime));
        }
    }

    /// <summary>
    /// Creates a message queue dynamically for a given type using reflection.
    /// Supports both unmanaged types (via CreateMessageQueueAsync) and IRingKernelMessage types (via CreateNamedMessageQueueAsync).
    /// </summary>
    /// <param name="messageType">The message type (must be unmanaged or implement IRingKernelMessage).</param>
    /// <param name="launchOptions">Ring kernel launch options containing queue configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// For unmanaged types: Returns IMessageQueue (GPU-resident queue).
    /// For IRingKernelMessage types: Returns object (named message queue) that must be cast appropriately.
    /// </returns>
    [RequiresDynamicCode("Creates message queue using reflection which requires runtime code generation")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Dynamic queue creation is required for ring kernels")]
    [UnconditionalSuppressMessage("Trimming", "IL2071", Justification = "Dynamic type creation via reflection is required for ring kernels")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Dynamic property access via reflection is required for ring kernels")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Ring kernel message queues require dynamic type creation")]
    private async Task<object> CreateTypedMessageQueueAsync(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)]
        Type messageType,
        RingKernelLaunchOptions launchOptions,
        CancellationToken cancellationToken)
    {
        // Check if the message type implements IRingKernelMessage (class-based message passing)
        if (typeof(IRingKernelMessage).IsAssignableFrom(messageType))
        {
            _logger.LogDebug("Creating named message queue for IRingKernelMessage type: {MessageType}", messageType.Name);

            // Use CreateNamedMessageQueueAsync for IRingKernelMessage types
            var createMethod = typeof(CpuRingKernelRuntime)
                .GetMethod(nameof(CreateNamedMessageQueueAsync), BindingFlags.Public | BindingFlags.Instance)!
                .MakeGenericMethod(messageType);

            var queueName = $"ringkernel_{messageType.Name}_{Guid.NewGuid():N}";

            // Convert launch options to MessageQueueOptions
            var queueOptions = launchOptions.ToMessageQueueOptions();

            var task = (Task)createMethod.Invoke(this, new object[] { queueName, queueOptions, cancellationToken })!;
            await task.ConfigureAwait(false);

            var resultProperty = task.GetType().GetProperty("Result")!;
            return resultProperty.GetValue(task)!; // Return the named queue as object
        }
        else if (IsUnmanagedType(messageType))
        {
            _logger.LogDebug("Creating unmanaged message queue for type: {MessageType}", messageType.Name);

            // Use CreateMessageQueueAsync for unmanaged types
            var createMethod = typeof(CpuRingKernelRuntime)
                .GetMethod(nameof(CreateMessageQueueAsync), BindingFlags.Public | BindingFlags.Instance)!
                .MakeGenericMethod(messageType);

            var capacity = launchOptions.QueueCapacity;
            var task = (Task)createMethod.Invoke(this, new object[] { capacity, cancellationToken })!;
            await task.ConfigureAwait(false);

            var resultProperty = task.GetType().GetProperty("Result")!;
            return resultProperty.GetValue(task)!;
        }
        else
        {
            throw new ArgumentException(
                $"Message type '{messageType.FullName}' must be either unmanaged (struct with no managed fields) or implement IRingKernelMessage interface",
                nameof(messageType));
        }
    }

    /// <summary>
    /// Checks if a type is unmanaged (value type with no managed references).
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Recursive field type checking is required to verify unmanaged constraint")]
    private static bool IsUnmanagedType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)] Type type)
    {
        if (!type.IsValueType)
        {
            return false;
        }

        if (type.IsPrimitive || type.IsPointer || type.IsEnum)
        {
            return true;
        }

        // Recursively check all fields
        return type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .All(f => IsUnmanagedType(f.FieldType));
    }

    /// <summary>
    /// Detects input and output message types from the kernel method signature.
    /// </summary>
    /// <param name="kernelId">The kernel ID to search for.</param>
    /// <returns>A tuple containing the input and output message types.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the kernel method cannot be found or types cannot be extracted.</exception>
    [RequiresUnreferencedCode("Searches all loaded assemblies for ring kernel methods")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Kernel discovery requires reflection over all loaded types")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Ring kernel attributes must be discoverable at runtime")]
    private (Type InputType, Type OutputType) DetectMessageTypes(string kernelId)
    {
        // Search all loaded assemblies for a method with RingKernelAttribute matching the kernelId
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies)
        {
            try
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                    foreach (var method in methods)
                    {
                        var ringKernelAttr = method.GetCustomAttribute<RingKernelAttribute>();
                        if (ringKernelAttr != null)
                        {
                            // Check if this is the kernel we're looking for
                            // The generated wrapper uses format: {ClassName}_{MethodName}
                            var generatedKernelId = $"{type.Name}_{method.Name}";
                            if (generatedKernelId == kernelId || ringKernelAttr.KernelId == kernelId)
                            {
                                // Found the kernel method - extract message types from Span<T> parameters
                                var parameters = method.GetParameters();

                                // Ring kernel signature pattern:
                                // param[0]: Span<long> timestamps
                                // param[1]: Span<TInput> requestQueue  <- INPUT TYPE
                                // param[2]: Span<TOutput> responseQueue <- OUTPUT TYPE
                                // param[3+]: other kernel-specific parameters

                                if (parameters.Length >= 3)
                                {
                                    var requestQueueParam = parameters[1];
                                    var responseQueueParam = parameters[2];

                                    var inputType = ExtractSpanElementType(requestQueueParam.ParameterType);
                                    var outputType = ExtractSpanElementType(responseQueueParam.ParameterType);

                                    if (inputType != null && outputType != null)
                                    {
                                        _logger.LogDebug(
                                            "Detected message types for kernel '{KernelId}': Input={InputType}, Output={OutputType}",
                                            kernelId, inputType.Name, outputType.Name);

                                        return (inputType, outputType);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                _logger.LogDebug("Skipping assembly '{Assembly}' during kernel search: {Error}",
                    assembly.FullName, ex.Message);
                continue;
            }
        }

        // Fallback to int if kernel not found (backward compatibility)
        _logger.LogWarning(
            "Could not detect message types for kernel '{KernelId}', falling back to int (may cause SendMessageAsync/ReceiveMessageAsync to fail)",
            kernelId);

        return (typeof(int), typeof(int));
    }

    /// <summary>
    /// Extracts the element type T from a Span&lt;T&gt; parameter type.
    /// </summary>
    /// <param name="parameterType">The parameter type (should be Span&lt;T&gt;).</param>
    /// <returns>The element type T, or null if not a Span&lt;T&gt;.</returns>
    private static Type? ExtractSpanElementType(Type parameterType)
    {
        // Check if it's a generic type (Span<T>)
        if (parameterType.IsGenericType)
        {
            var genericTypeDef = parameterType.GetGenericTypeDefinition();

            // Check if it's Span<T>
            if (genericTypeDef.Name == "Span`1")
            {
                return parameterType.GetGenericArguments()[0];
            }
        }

        return null;
    }
}
