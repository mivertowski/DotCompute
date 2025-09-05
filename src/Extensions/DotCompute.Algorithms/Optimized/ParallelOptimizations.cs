// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using global::System.Runtime.CompilerServices;
using DotCompute.Algorithms.LinearAlgebra;

namespace DotCompute.Algorithms.Optimized;

/// <summary>
/// Production-grade parallel algorithm optimizations with work-stealing,
/// parallel reduction, GPU-specific optimizations, and dynamic load balancing.
/// </summary>
public static class ParallelOptimizations
{
    // Work-stealing configuration
    private static readonly int DefaultMaxWorkers = Environment.ProcessorCount;
    private static readonly ThreadLocal<Random> ThreadLocalRandom = new(() => new Random());

    // Parallel thresholds

    private const int PARALLEL_THRESHOLD = 1000;
    private const int WORK_STEALING_THRESHOLD = 10000;
    private const int GPU_THRESHOLD = 100000;


    /// <summary>
    /// Work-stealing thread pool for dynamic load balancing.
    /// Achieves optimal CPU utilization across all cores.
    /// </summary>
    public sealed class WorkStealingPool : IDisposable
    {
        private readonly WorkStealingQueue[] _queues;
        private readonly Thread[] _workers;
        private readonly CancellationTokenSource _cancellation;
        private volatile bool _disposed;


        public WorkStealingPool(int workerCount = 0)
        {
            workerCount = workerCount <= 0 ? DefaultMaxWorkers : workerCount;


            _queues = new WorkStealingQueue[workerCount];
            _workers = new Thread[workerCount];
            _cancellation = new CancellationTokenSource();

            // Initialize worker queues and threads

            for (var i = 0; i < workerCount; i++)
            {
                _queues[i] = new WorkStealingQueue();
                var workerId = i;
                _workers[i] = new Thread(() => WorkerLoop(workerId))
                {
                    IsBackground = true,
                    Name = $"WorkStealing-{workerId}"
                };
                _workers[i].Start();
            }
        }


        public void Execute(Action task)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(WorkStealingPool));
            }


            var workerId = Environment.CurrentManagedThreadId % _queues.Length;
            _queues[workerId].Enqueue(task);
        }


        public void ExecuteParallel<T>(IEnumerable<T> items, Action<T> action)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(WorkStealingPool));
            }


            var tasks = items.Select(item => new Action(() => action(item))).ToArray();
            var completedCount = 0;

            // Distribute tasks across workers

            for (var i = 0; i < tasks.Length; i++)
            {
                var task = tasks[i];
                var wrappedTask = new Action(() =>
                {
                    task();
                    _ = Interlocked.Increment(ref completedCount);
                });


                var workerId = i % _queues.Length;
                _queues[workerId].Enqueue(wrappedTask);
            }

            // Wait for completion

            var spinWait = new SpinWait();
            while (Volatile.Read(ref completedCount) < tasks.Length)
            {
                spinWait.SpinOnce();
            }
        }


        private void WorkerLoop(int workerId)
        {
            var queue = _queues[workerId];
            var token = _cancellation.Token;
            var random = ThreadLocalRandom.Value!;


            while (!token.IsCancellationRequested)
            {

                // Try to dequeue from local queue

                if (queue.TryDequeue(out var task) && task != null)
                {
                    ExecuteTask(task);
                    continue;
                }

                // Try to steal work from other queues

                var stoleWork = false;
                for (var attempts = 0; attempts < _queues.Length; attempts++)
                {
                    var victimId = random.Next(_queues.Length);
                    if (victimId != workerId && _queues[victimId].TrySteal(out task))
                    {
                        if (task != null)
                        {
                            ExecuteTask(task);
                            stoleWork = true;
                            break;
                        }
                    }
                }


                if (!stoleWork)
                {
                    // No work available, yield or sleep briefly
                    _ = Thread.Yield();
                    if (!stoleWork)
                    {
                        Thread.Sleep(1);
                    }
                }
            }
        }


        private static void ExecuteTask(Action task)
        {
            try
            {
                task();
            }
            catch (Exception ex)
            {
                // Log exception or handle appropriately
                Console.WriteLine($"Work-stealing task exception: {ex}");
            }
        }


        public void Dispose()
        {
            if (!_disposed)
            {
                _cancellation.Cancel();


                foreach (var worker in _workers)
                {
                    _ = worker.Join(TimeSpan.FromSeconds(5));
                }


                _cancellation.Dispose();
                _disposed = true;
            }
        }
    }


    /// <summary>
    /// Lock-free work-stealing deque implementation.
    /// </summary>
    private sealed class WorkStealingQueue
    {
        private const int InitialCapacity = 32;
        private Action[] _array = new Action[InitialCapacity];
        private volatile int _head;
        private volatile int _tail;
        private readonly object _lock = new();


        public void Enqueue(Action item)
        {
            var tail = _tail;
            var head = _head;


            if (tail - head >= _array.Length - 1)
            {
                lock (_lock)
                {
                    GrowArray();
                }
            }


            _array[tail & (_array.Length - 1)] = item;
            _tail = tail + 1;
        }


        public bool TryDequeue(out Action? item)
        {
            var tail = _tail;
            if (tail == _head)
            {
                item = null;
                return false;
            }


            tail = tail - 1;
            _tail = tail;


            item = _array[tail & (_array.Length - 1)];


            if (tail == _head)
            {
                return true;
            }


            if (tail < _head)
            {
                _tail = tail + 1;
                item = null;
                return false;
            }


            return true;
        }


        public bool TrySteal(out Action? item)
        {
            var head = _head;
            var tail = _tail;


            if (head >= tail)
            {
                item = null;
                return false;
            }


            item = _array[head & (_array.Length - 1)];


            if (Interlocked.CompareExchange(ref _head, head + 1, head) == head)
            {
                return true;
            }


            item = null;
            return false;
        }


        private void GrowArray()
        {
            var newArray = new Action[_array.Length * 2];
            var head = _head;
            var tail = _tail;


            for (var i = head; i < tail; i++)
            {
                newArray[i & (newArray.Length - 1)] = _array[i & (_array.Length - 1)];
            }


            _array = newArray;
        }
    }


    /// <summary>
    /// High-performance parallel matrix multiplication with work-stealing.
    /// Automatically scales across all available CPU cores.
    /// </summary>
    /// <param name="a">Left matrix</param>
    /// <param name="b">Right matrix</param>
    /// <param name="pool">Work-stealing pool (optional)</param>
    /// <returns>Result matrix</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static Matrix ParallelMatrixMultiply(Matrix a, Matrix b, WorkStealingPool? pool = null)
    {
        if (a.Columns != b.Rows)
        {

            throw new ArgumentException("Matrix dimensions incompatible");
        }


        var result = new Matrix(a.Rows, b.Columns);
        ParallelMatrixMultiply(a, b, result, pool);
        return result;
    }


    /// <summary>
    /// In-place parallel matrix multiplication with optimal load balancing.
    /// </summary>
    public static void ParallelMatrixMultiply(Matrix a, Matrix b, Matrix result, WorkStealingPool? pool = null)
    {
        var rows = a.Rows;
        var cols = b.Columns;
        var inner = a.Columns;


        if (rows * cols * inner < PARALLEL_THRESHOLD)
        {
            // Use sequential algorithm for small matrices
            MatrixOptimizations.OptimizedMultiply(a, b, result);
            return;
        }


        var useExternalPool = pool != null;
        pool ??= new WorkStealingPool();


        try
        {
            // Divide work into cache-friendly blocks
            var blockSize = CalculateOptimalBlockSize(rows, cols, inner);
            var tasks = new List<Action>();


            for (var ii = 0; ii < rows; ii += blockSize)
            {
                for (var jj = 0; jj < cols; jj += blockSize)
                {
                    var i = ii;
                    var j = jj;


                    tasks.Add(() =>
                    {
                        var iEnd = Math.Min(i + blockSize, rows);
                        var jEnd = Math.Min(j + blockSize, cols);


                        MultiplyBlock(a, b, result, i, iEnd, j, jEnd, inner);
                    });
                }
            }

            // Execute tasks in parallel

            pool.ExecuteParallel(tasks, task => task());
        }
        finally
        {
            if (!useExternalPool)
            {
                pool.Dispose();
            }
        }
    }


    /// <summary>
    /// Parallel reduction with optimal tree structure and cache efficiency.
    /// </summary>
    /// <typeparam name="T">Element type</typeparam>
    /// <param name="source">Source collection</param>
    /// <param name="identity">Identity element</param>
    /// <param name="reducer">Reduction function</param>
    /// <param name="pool">Work-stealing pool (optional)</param>
    /// <returns>Reduced result</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static T ParallelReduce<T>(IEnumerable<T> source, T identity, Func<T, T, T> reducer,

        WorkStealingPool? pool = null)
    {
        var items = source.ToArray();
        var length = items.Length;


        if (length == 0)
        {
            return identity;
        }


        if (length == 1)
        {
            return items[0];
        }


        if (length < PARALLEL_THRESHOLD)
        {
            return items.Aggregate(identity, reducer);
        }


        var useExternalPool = pool != null;
        pool ??= new WorkStealingPool();


        try
        {
            return ParallelReduceRecursive(items, 0, length, identity, reducer, pool);
        }
        finally
        {
            if (!useExternalPool)
            {
                pool.Dispose();
            }
        }
    }


    /// <summary>
    /// Parallel scan (prefix sum) with optimal memory access patterns.
    /// </summary>
    /// <typeparam name="T">Element type</typeparam>
    /// <param name="source">Source array</param>
    /// <param name="identity">Identity element</param>
    /// <param name="scanner">Scan function</param>
    /// <param name="pool">Work-stealing pool (optional)</param>
    /// <returns>Scan result array</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static T[] ParallelScan<T>(T[] source, T identity, Func<T, T, T> scanner,

        WorkStealingPool? pool = null)
    {
        var length = source.Length;
        if (length == 0)
        {
            return [];
        }


        var result = new T[length];
        if (length < PARALLEL_THRESHOLD)
        {
            // Sequential scan for small arrays
            result[0] = scanner(identity, source[0]);
            for (var i = 1; i < length; i++)
            {
                result[i] = scanner(result[i - 1], source[i]);
            }
            return result;
        }


        var useExternalPool = pool != null;
        pool ??= new WorkStealingPool();


        try
        {
            ParallelScanImpl(source, result, identity, scanner, pool);
            return result;
        }
        finally
        {
            if (!useExternalPool)
            {
                pool.Dispose();
            }
        }
    }


    /// <summary>
    /// Parallel sort using optimized merge sort with work-stealing.
    /// Achieves O(n log n) time with excellent cache performance.
    /// </summary>
    /// <typeparam name="T">Element type</typeparam>
    /// <param name="array">Array to sort</param>
    /// <param name="comparer">Comparison function</param>
    /// <param name="pool">Work-stealing pool (optional)</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void ParallelSort<T>(T[] array, IComparer<T> comparer, WorkStealingPool? pool = null)
    {
        if (array.Length < 2)
        {
            return;
        }


        if (array.Length < PARALLEL_THRESHOLD)
        {
            Array.Sort(array, comparer);
            return;
        }


        var useExternalPool = pool != null;
        pool ??= new WorkStealingPool();


        try
        {
            var temp = new T[array.Length];
            ParallelMergeSort(array, temp, 0, array.Length, comparer, pool);
        }
        finally
        {
            if (!useExternalPool)
            {
                pool.Dispose();
            }
        }
    }


    /// <summary>
    /// GPU-optimized data transfer and computation coordination.
    /// Manages CPU-GPU data movement and parallel execution.
    /// </summary>
    public static class GPUOptimizations
    {
        /// <summary>
        /// Asynchronous GPU computation with optimal data transfer patterns.
        /// </summary>
        /// <typeparam name="TInput">Input data type</typeparam>
        /// <typeparam name="TOutput">Output data type</typeparam>
        /// <param name="inputData">Input data</param>
        /// <param name="gpuKernel">GPU kernel function</param>
        /// <param name="cpuFallback">CPU fallback function</param>
        /// <returns>Computation result</returns>
        public static async Task<TOutput[]> AsyncGpuCompute<TInput, TOutput>(
            TInput[] inputData,
            Func<TInput[], Task<TOutput[]>> gpuKernel,
            Func<TInput[], TOutput[]> cpuFallback)
        {
            if (inputData.Length < GPU_THRESHOLD)
            {
                // Use CPU for small datasets
                return await Task.Run(() => cpuFallback(inputData));
            }


            try
            {
                // Attempt GPU computation
                return await gpuKernel(inputData);
            }
            catch (Exception ex)
            {
                // Fallback to CPU on GPU errors
                Console.WriteLine($"GPU computation failed, falling back to CPU: {ex.Message}");
                return await Task.Run(() => cpuFallback(inputData));
            }
        }


        /// <summary>
        /// Hybrid CPU-GPU computation with dynamic load balancing.
        /// </summary>
        public static async Task<TOutput[]> HybridCompute<TInput, TOutput>(
            TInput[] inputData,
            Func<TInput[], Task<TOutput[]>> gpuKernel,
            Func<TInput[], TOutput[]> cpuKernel,
            float gpuRatio = 0.8f)
        {
            if (inputData.Length < PARALLEL_THRESHOLD)
            {
                return cpuKernel(inputData);
            }


            var gpuSize = (int)(inputData.Length * gpuRatio);
            var cpuSize = inputData.Length - gpuSize;


            var gpuData = inputData.Take(gpuSize).ToArray();
            var cpuData = inputData.Skip(gpuSize).Take(cpuSize).ToArray();

            // Execute GPU and CPU work in parallel

            var gpuTask = gpuKernel(gpuData);
            var cpuTask = Task.Run(() => cpuKernel(cpuData));


            _ = await Task.WhenAll(gpuTask, cpuTask);

            // Combine results

            var result = new TOutput[inputData.Length];
            var gpuResult = gpuTask.Result;
            var cpuResult = cpuTask.Result;


            Array.Copy(gpuResult, 0, result, 0, gpuResult.Length);
            Array.Copy(cpuResult, 0, result, gpuResult.Length, cpuResult.Length);


            return result;
        }
    }

    #region Private Implementation Methods


    private static int CalculateOptimalBlockSize(int rows, int cols, int inner)
    {
        // Cache-aware block size calculation
        const int l2CacheSize = 256 * 1024; // 256KB L2 cache
        const int elementSize = sizeof(float);


        var maxBlockElements = l2CacheSize / (elementSize * 3); // A, B, C blocks
        var blockSize = (int)Math.Sqrt(maxBlockElements);

        // Ensure block size is reasonable

        blockSize = Math.Max(32, Math.Min(blockSize, 512));


        return blockSize;
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void MultiplyBlock(Matrix a, Matrix b, Matrix result,

        int iStart, int iEnd, int jStart, int jEnd, int inner)
    {
        var aSpan = a.AsSpan();
        var bSpan = b.AsSpan();
        _ = result.AsSpan();


        for (var i = iStart; i < iEnd; i++)
        {
            for (var k = 0; k < inner; k++)
            {
                var aVal = aSpan[i * a.Columns + k];
                for (var j = jStart; j < jEnd; j++)
                {
                    result.AsMutableSpan()[i * result.Columns + j] += aVal * bSpan[k * b.Columns + j];
                }
            }
        }
    }


    private static T ParallelReduceRecursive<T>(T[] items, int start, int length, T identity,
        Func<T, T, T> reducer, WorkStealingPool pool)
    {
        if (length <= PARALLEL_THRESHOLD)
        {
            var result = identity;
            for (var i = start; i < start + length; i++)
            {
                result = reducer(result, items[i]);
            }
            return result;
        }


        var mid = length / 2;
        var leftResult = default(T);
        var rightResult = default(T);
        var leftComplete = false;
        var rightComplete = false;

        // Execute left half

        pool.Execute(() =>
        {
            leftResult = ParallelReduceRecursive(items, start, mid, identity, reducer, pool);
            leftComplete = true;
        });

        // Execute right half

        pool.Execute(() =>
        {
            rightResult = ParallelReduceRecursive(items, start + mid, length - mid, identity, reducer, pool);
            rightComplete = true;
        });

        // Wait for completion

        var spinWait = new SpinWait();
        while (!leftComplete || !rightComplete)
        {
            spinWait.SpinOnce();
        }


        return reducer(leftResult!, rightResult!);
    }


    private static void ParallelScanImpl<T>(T[] source, T[] result, T identity,
        Func<T, T, T> scanner, WorkStealingPool pool)
    {
        var length = source.Length;
        var numWorkers = Environment.ProcessorCount;
        var blockSize = (length + numWorkers - 1) / numWorkers;


        var blockSums = new T[numWorkers];
        var blockComplete = new bool[numWorkers];

        // Phase 1: Parallel scan within blocks

        for (var workerId = 0; workerId < numWorkers; workerId++)
        {
            var id = workerId;
            pool.Execute(() =>
            {
                var start = id * blockSize;
                var end = Math.Min(start + blockSize, length);


                if (start < end)
                {
                    result[start] = scanner(identity, source[start]);
                    for (var i = start + 1; i < end; i++)
                    {
                        result[i] = scanner(result[i - 1], source[i]);
                    }
                    blockSums[id] = result[end - 1];
                }


                blockComplete[id] = true;
            });
        }

        // Wait for phase 1 completion

        var spinWait = new SpinWait();
        while (!blockComplete.All(x => x))
        {
            spinWait.SpinOnce();
        }

        // Phase 2: Sequential scan of block sums

        var blockPrefixes = new T[numWorkers];
        blockPrefixes[0] = identity;
        for (var i = 1; i < numWorkers; i++)
        {
            blockPrefixes[i] = scanner(blockPrefixes[i - 1], blockSums[i - 1]);
        }

        // Phase 3: Add block prefixes in parallel

        Array.Fill(blockComplete, false);
        for (var workerId = 1; workerId < numWorkers; workerId++)
        {
            var id = workerId;
            pool.Execute(() =>
            {
                var start = id * blockSize;
                var end = Math.Min(start + blockSize, length);
                var prefix = blockPrefixes[id];


                for (var i = start; i < end; i++)
                {
                    result[i] = scanner(prefix, result[i]);
                }


                blockComplete[id] = true;
            });
        }

        // Wait for completion

        while (!blockComplete.Skip(1).All(x => x))
        {
            spinWait.SpinOnce();
        }
    }


    private static void ParallelMergeSort<T>(T[] array, T[] temp, int start, int length,
        IComparer<T> comparer, WorkStealingPool pool)
    {
        if (length <= PARALLEL_THRESHOLD)
        {
            Array.Sort(array, start, length, comparer);
            return;
        }


        var mid = length / 2;
        var leftComplete = false;
        var rightComplete = false;

        // Sort left half

        pool.Execute(() =>
        {
            ParallelMergeSort(array, temp, start, mid, comparer, pool);
            leftComplete = true;
        });

        // Sort right half

        pool.Execute(() =>
        {
            ParallelMergeSort(array, temp, start + mid, length - mid, comparer, pool);
            rightComplete = true;
        });

        // Wait for completion

        var spinWait = new SpinWait();
        while (!leftComplete || !rightComplete)
        {
            spinWait.SpinOnce();
        }

        // Merge sorted halves

        Merge(array, temp, start, mid, length, comparer);
    }


    private static void Merge<T>(T[] array, T[] temp, int start, int mid, int length,
        IComparer<T> comparer)
    {
        var left = start;
        var right = start + mid;
        var end = start + length;
        var index = start;

        // Copy to temp array

        Array.Copy(array, start, temp, start, length);

        // Merge back to original array

        while (left < start + mid && right < end)
        {
            if (comparer.Compare(temp[left], temp[right]) <= 0)
            {
                array[index++] = temp[left++];
            }
            else
            {
                array[index++] = temp[right++];
            }
        }

        // Copy remaining elements

        while (left < start + mid)
        {
            array[index++] = temp[left++];
        }


        while (right < end)
        {
            array[index++] = temp[right++];
        }
    }


    #endregion
}