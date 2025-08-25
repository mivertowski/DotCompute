// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Validation
{
    /// <summary>
    /// Represents a validation warning with contextual information.
    /// </summary>
    public class ValidationWarning
    {
        /// <summary>
        /// Gets or sets the warning code.
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the warning message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the path to the element that triggered the warning.
        /// </summary>
        public string? Path { get; set; }

        /// <summary>
        /// Gets or sets the recommendation to resolve the warning.
        /// </summary>
        public string? Recommendation { get; set; }

        /// <summary>
        /// Gets or sets the severity level of the warning.
        /// </summary>
        public WarningSeverity Severity { get; set; } = WarningSeverity.Warning;
    }

    /// <summary>
    /// Warning severity levels.
    /// </summary>
    public enum WarningSeverity
    {
        /// <summary>
        /// Informational warning.
        /// </summary>
        Info,

        /// <summary>
        /// Standard warning.
        /// </summary>
        Warning,

        /// <summary>
        /// Critical warning that may affect functionality.
        /// </summary>
        Critical
    }
}