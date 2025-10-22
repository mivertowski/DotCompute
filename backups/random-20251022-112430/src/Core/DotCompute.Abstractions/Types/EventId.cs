// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;

namespace DotCompute.Backends.CUDA.Execution.Types
{
    /// <summary>
    /// Unique identifier for CUDA events.
    /// </summary>
    public readonly struct EventId : IEquatable<EventId>
    {
        private readonly Guid _id;

        private EventId(Guid id)
        {
            _id = id;
        }


        /// <summary>
        /// Creates a new unique event identifier.
        /// </summary>
        /// <returns>A new EventId instance.</returns>
        public static EventId New() => new(Guid.NewGuid());

        /// <summary>
        /// Determines whether this EventId equals another EventId.
        /// </summary>
        /// <param name="other">The other EventId to compare.</param>
        /// <returns>True if the EventIds are equal; otherwise, false.</returns>
        public bool Equals(EventId other) => _id.Equals(other._id);

        /// <summary>
        /// Determines whether this EventId equals another object.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>True if the object is an EventId and equals this instance; otherwise, false.</returns>
        public override bool Equals(object? obj) => obj is EventId other && Equals(other);

        /// <summary>
        /// Gets the hash code for this EventId.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode() => _id.GetHashCode();

        /// <summary>
        /// Returns a string representation of this EventId.
        /// </summary>
        /// <returns>A shortened string representation of the underlying GUID.</returns>
        public override string ToString() => _id.ToString("N", CultureInfo.InvariantCulture)[..8];

        /// <summary>
        /// Determines whether two EventId instances are equal.
        /// </summary>
        public static bool operator ==(EventId left, EventId right) => left.Equals(right);

        /// <summary>
        /// Determines whether two EventId instances are not equal.
        /// </summary>
        public static bool operator !=(EventId left, EventId right) => !left.Equals(right);
    }
}
