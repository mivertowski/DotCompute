// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Configuration.Style.Enums;

/// <summary>
/// Specifies the line ending style options for generated code files.
/// </summary>
/// <remarks>
/// Line ending consistency is crucial for cross-platform development and
/// version control systems. Different operating systems use different
/// conventions, and mixed line endings can cause issues with git and
/// other tools. Consistent line endings improve diff readability and
/// prevent unnecessary changes in version control.
/// </remarks>
public enum LineEndingStyle
{
    /// <summary>
    /// Automatically detect and use the appropriate line ending style based on the current platform.
    /// </summary>
    /// <remarks>
    /// Auto detection uses the operating system's default line ending convention:
    /// - Windows: CRLF (Carriage Return + Line Feed, \r\n)
    /// - Unix/Linux/macOS: LF (Line Feed only, \n)
    ///
    /// This option provides platform-appropriate defaults but may result in
    /// different line endings across development environments, which could
    /// cause version control issues in mixed-platform teams.
    /// </remarks>
    Auto,

    /// <summary>
    /// Use Windows-style line endings (CRLF - Carriage Return + Line Feed).
    /// </summary>
    /// <remarks>
    /// Windows line endings use both carriage return (\r) and line feed (\n)
    /// characters to terminate lines. This is the historical convention from
    /// DOS and Windows systems. Using this setting ensures consistent CRLF
    /// line endings regardless of the development platform.
    ///
    /// Character sequence: \r\n (0x0D 0x0A)
    /// </remarks>
    Windows,


    /// <summary>
    /// Use Unix-style line endings (LF - Line Feed only).
    /// </summary>
    /// <remarks>
    /// Unix line endings use only the line feed (\n) character to terminate lines.
    /// This is the standard for Unix, Linux, and macOS systems. It's also the
    /// preferred format for most version control systems and web applications.
    /// Using this setting ensures consistent LF line endings across all platforms.
    ///
    /// Character sequence: \n (0x0A)
    /// </remarks>
    Unix
}
