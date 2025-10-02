// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Execution.Optimization
{
    /// <summary>
    /// Provides load balancing hints for work stealing execution strategies.
    /// </summary>
    /// <remarks>
    /// This class contains optimization hints that guide the distribution of work items
    /// across available devices in a work stealing execution model. It helps optimize
    /// performance by providing device preferences, work item priorities, and affinity groupings.
    /// </remarks>
    public class LoadBalancingHints
    {
        /// <summary>
        /// Gets or sets the preferred devices for specific work items.
        /// </summary>
        /// <value>
        /// A dictionary mapping work item indices to preferred device identifiers.
        /// When a work item has a preferred device, the scheduler will attempt to
        /// assign it to that device first.
        /// </value>
        /// <remarks>
        /// This is useful for work items that have specific hardware requirements
        /// or benefit from device-specific optimizations.
        /// </remarks>
        public Dictionary<int, string> PreferredDevices { get; } = [];

        /// <summary>
        /// Gets or sets the work item priorities.
        /// </summary>
        /// <value>
        /// A dictionary mapping work item indices to priority values.
        /// Higher priority values indicate work items that should be executed sooner.
        /// Default priority is assumed to be 0 for items not in this dictionary.
        /// </value>
        /// <remarks>
        /// Priority-based scheduling ensures that critical work items are processed
        /// before lower-priority ones, improving overall system responsiveness.
        /// </remarks>
        public Dictionary<int, int> Priorities { get; } = [];

        /// <summary>
        /// Gets or sets the affinity groups for work items.
        /// </summary>
        /// <value>
        /// A dictionary mapping work item indices to affinity group identifiers.
        /// Work items in the same affinity group benefit from being executed
        /// on the same device or similar devices.
        /// </value>
        /// <remarks>
        /// Affinity grouping is useful for work items that share data or have
        /// similar computational characteristics, allowing for better cache utilization
        /// and reduced data transfer overhead.
        /// </remarks>
        public Dictionary<int, int> AffinityGroups { get; } = [];

        /// <summary>
        /// Gets a value indicating whether any hints have been specified.
        /// </summary>
        /// <value>
        /// <c>true</c> if any optimization hints have been provided; otherwise, <c>false</c>.
        /// </value>
        public bool HasHints => PreferredDevices.Count > 0 || Priorities.Count > 0 || AffinityGroups.Count > 0;

        /// <summary>
        /// Gets the number of unique devices referenced in the preferred devices hints.
        /// </summary>
        /// <value>The count of distinct device identifiers in the preferred devices mapping.</value>
        public int UniqueDeviceCount => PreferredDevices.Values.Distinct().Count();

        /// <summary>
        /// Gets the number of unique affinity groups defined.
        /// </summary>
        /// <value>The count of distinct affinity group identifiers.</value>
        public int UniqueAffinityGroupCount => AffinityGroups.Values.Distinct().Count();

        /// <summary>
        /// Gets the priority for a specific work item.
        /// </summary>
        /// <param name="workItemIndex">The index of the work item.</param>
        /// <returns>
        /// The priority value for the work item, or 0 if no priority is specified.
        /// </returns>
        public int GetPriority(int workItemIndex) => Priorities.TryGetValue(workItemIndex, out var priority) ? priority : 0;

        /// <summary>
        /// Gets the preferred device for a specific work item.
        /// </summary>
        /// <param name="workItemIndex">The index of the work item.</param>
        /// <returns>
        /// The preferred device identifier, or null if no preference is specified.
        /// </returns>
        public string? GetPreferredDevice(int workItemIndex) => PreferredDevices.TryGetValue(workItemIndex, out var device) ? device : null;

        /// <summary>
        /// Gets the affinity group for a specific work item.
        /// </summary>
        /// <param name="workItemIndex">The index of the work item.</param>
        /// <returns>
        /// The affinity group identifier, or null if no affinity group is specified.
        /// </returns>
        public int? GetAffinityGroup(int workItemIndex) => AffinityGroups.TryGetValue(workItemIndex, out var group) ? group : null;

        /// <summary>
        /// Sets a device preference for a work item.
        /// </summary>
        /// <param name="workItemIndex">The index of the work item.</param>
        /// <param name="deviceId">The preferred device identifier.</param>
        public void SetPreferredDevice(int workItemIndex, string deviceId)
        {
            if (!string.IsNullOrEmpty(deviceId))
            {
                PreferredDevices[workItemIndex] = deviceId;
            }
        }

        /// <summary>
        /// Sets a priority for a work item.
        /// </summary>
        /// <param name="workItemIndex">The index of the work item.</param>
        /// <param name="priority">The priority value (higher values indicate higher priority).</param>
        public void SetPriority(int workItemIndex, int priority) => Priorities[workItemIndex] = priority;

        /// <summary>
        /// Sets an affinity group for a work item.
        /// </summary>
        /// <param name="workItemIndex">The index of the work item.</param>
        /// <param name="affinityGroup">The affinity group identifier.</param>
        public void SetAffinityGroup(int workItemIndex, int affinityGroup) => AffinityGroups[workItemIndex] = affinityGroup;

        /// <summary>
        /// Gets all work item indices that belong to a specific affinity group.
        /// </summary>
        /// <param name="affinityGroup">The affinity group identifier.</param>
        /// <returns>An enumerable of work item indices in the specified affinity group.</returns>
        public IEnumerable<int> GetWorkItemsInAffinityGroup(int affinityGroup) => AffinityGroups.Where(kvp => kvp.Value == affinityGroup).Select(kvp => kvp.Key);

        /// <summary>
        /// Gets all work item indices that prefer a specific device.
        /// </summary>
        /// <param name="deviceId">The device identifier.</param>
        /// <returns>An enumerable of work item indices that prefer the specified device.</returns>
        public IEnumerable<int> GetWorkItemsPreferringDevice(string deviceId) => PreferredDevices.Where(kvp => kvp.Value == deviceId).Select(kvp => kvp.Key);

        /// <summary>
        /// Gets work items sorted by priority in descending order.
        /// </summary>
        /// <returns>
        /// An enumerable of work item indices ordered by priority (highest priority first).
        /// Work items without explicit priorities are treated as having priority 0.
        /// </returns>
        public IEnumerable<int> GetWorkItemsByPriority() => Priorities.OrderByDescending(kvp => kvp.Value).Select(kvp => kvp.Key);

        /// <summary>
        /// Clears all optimization hints.
        /// </summary>
        public void Clear()
        {
            PreferredDevices.Clear();
            Priorities.Clear();
            AffinityGroups.Clear();
        }

        /// <summary>
        /// Creates a copy of the current hints.
        /// </summary>
        /// <returns>A new <see cref="LoadBalancingHints"/> instance with the same hint data.</returns>
        public LoadBalancingHints Clone()
        {
            return new LoadBalancingHints
            {
                PreferredDevices = new Dictionary<int, string>(PreferredDevices),
                Priorities = new Dictionary<int, int>(Priorities),
                AffinityGroups = new Dictionary<int, int>(AffinityGroups)
            };
        }

        /// <summary>
        /// Returns a string representation of the load balancing hints.
        /// </summary>
        /// <returns>A summary string describing the hints configuration.</returns>
        public override string ToString()
        {
            return $"LoadBalancingHints: {PreferredDevices.Count} device preferences, " +
                   $"{Priorities.Count} priorities, {AffinityGroups.Count} affinity mappings, " +
                   $"{UniqueDeviceCount} unique devices, {UniqueAffinityGroupCount} affinity groups";
        }
    }
}