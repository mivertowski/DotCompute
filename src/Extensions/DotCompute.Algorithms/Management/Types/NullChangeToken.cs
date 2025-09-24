// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Primitives;

namespace DotCompute.Algorithms.Management.Types
{
    /// <summary>
    /// Null change token that never changes.
    /// </summary>
    internal sealed class NullChangeToken : IChangeToken
    {
        public static readonly NullChangeToken Singleton = new();

        public bool HasChanged => false;
        public bool ActiveChangeCallbacks => false;
        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => NullDisposable.Instance;
    }
}