// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace DotCompute.Core.Tests.Memory.P2P
{
    /// <summary>
    /// Test logger implementation that outputs to xUnit test output.
    /// </summary>
    internal sealed class TestLogger<T> : ILogger<T>
    {
        private readonly ITestOutputHelper _output;
        private readonly string _categoryName;

        public TestLogger(ITestOutputHelper output)
        {
            _output = output ?? throw new ArgumentNullException(nameof(output));
            _categoryName = typeof(T).Name;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Information;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            var logEntry = $"[{logLevel}] {_categoryName}: {message}";

            if (exception != null)
            {
                logEntry += Environment.NewLine + exception;
            }

            try
            {
                _output.WriteLine(logEntry);
            }
            catch
            {
                // Ignore output errors during tests
            }
        }
    }
}