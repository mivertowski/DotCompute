// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Reflection;

namespace DotCompute.Plugins.Security;

/// <summary>
/// Extension methods for security-related operations.
/// </summary>
public static class SecurityExtensions
{
    /// <summary>
    /// Converts a byte array to a hex string representation.
    /// </summary>
    /// <param name="bytes">The byte array to convert.</param>
    /// <returns>A hex string representation of the bytes.</returns>
    public static string ToHexString(this byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// Securely compares two byte arrays in constant time to prevent timing attacks.
    /// </summary>
    /// <param name="array1">The first array.</param>
    /// <param name="array2">The second array.</param>
    /// <returns>True if the arrays are equal, false otherwise.</returns>
    public static bool SecureEquals(this byte[] array1, byte[] array2)
    {
        ArgumentNullException.ThrowIfNull(array1);
        ArgumentNullException.ThrowIfNull(array2);

        if (array1.Length != array2.Length)
        {
            return false;
        }

        var result = 0;
        for (var i = 0; i < array1.Length; i++)
        {
            result |= array1[i] ^ array2[i];
        }

        return result == 0;
    }

    /// <summary>
    /// Determines if an assembly has a strong name.
    /// </summary>
    /// <param name="assembly">The assembly to check.</param>
    /// <returns>True if the assembly has a strong name, false otherwise.</returns>
    public static bool HasStrongName(this Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        try
        {
            var publicKey = assembly.GetName().GetPublicKey();
            return publicKey != null && publicKey.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the public key token of an assembly as a hex string.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns>The public key token as a hex string, or null if not available.</returns>
    public static string? GetPublicKeyTokenString(this Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        try
        {
            var token = assembly.GetName().GetPublicKeyToken();
            return token?.ToHexString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Checks if a type is considered safe for plugin execution.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is safe, false otherwise.</returns>
    public static bool IsSafeForPlugins(this Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        // Check for dangerous types
        var dangerousTypes = new[]
        {
            typeof(System.Reflection.Assembly),
            typeof(System.AppDomain),
            typeof(System.Runtime.InteropServices.Marshal),
            typeof(System.Diagnostics.Process)
        };

        if (dangerousTypes.Contains(type))
        {
            return false;
        }

        // Check for unsafe code indicators
        if (type.Name.Contains("Unsafe", StringComparison.OrdinalIgnoreCase) ||
            type.Namespace?.Contains("Unsafe", StringComparison.OrdinalIgnoreCase) == true)
        {
            return false;
        }

        // Check for security-critical attributes
        var securityAttributes = type.GetCustomAttributes()
            .Where(attr => attr.GetType().Name.Contains("Security", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (securityAttributes.Count != 0)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates that a string doesn't contain malicious patterns.
    /// </summary>
    /// <param name="input">The string to validate.</param>
    /// <returns>True if the string is safe, false otherwise.</returns>
    public static bool IsSafeString(this string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return true;
        }

        var maliciousPatterns = new[]
        {
            "javascript:", "vbscript:", "data:", "file:",
            "<script", "</script>", "eval(", "setTimeout(",
            "setInterval(", "Function(", "ActiveXObject",
            "WScript.", "Shell.", "cmd.exe", "powershell"
        };

        return !maliciousPatterns.Any(pattern =>

            input.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Sanitizes a string by removing potentially dangerous content.
    /// </summary>
    /// <param name="input">The string to sanitize.</param>
    /// <returns>A sanitized version of the string.</returns>
    public static string Sanitize(this string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        // Remove dangerous patterns
        var dangerousPatterns = new[]
        {
            @"<script[^>]*>.*?</script>",
            @"javascript:",
            @"vbscript:",
            @"data:",
            @"eval\s*\(",
            @"Function\s*\(",
            @"setTimeout\s*\(",
            @"setInterval\s*\("
        };

        var sanitized = input;
        foreach (var pattern in dangerousPatterns)
        {
            sanitized = System.Text.RegularExpressions.Regex.Replace(
                sanitized, pattern, "",

                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return sanitized;
    }

    /// <summary>
    /// Validates that a file path is safe and doesn't contain traversal attempts.
    /// </summary>
    /// <param name="path">The path to validate.</param>
    /// <returns>True if the path is safe, false otherwise.</returns>
    public static bool IsSafePath(this string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            var fileName = Path.GetFileName(fullPath);

            // Check for directory traversal
            if (path.Contains("..") || path.Contains("~") ||
                fileName.StartsWith(".") || fileName.Contains(":"))
            {
                return false;
            }

            // Check for reserved names
            var reservedNames = new[]
            {
                "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4",
                "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2",
                "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
            };

            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName).ToUpperInvariant();
            if (reservedNames.Contains(fileNameWithoutExtension))
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if a permission is considered high-risk.
    /// </summary>
    /// <param name="permission">The permission to check.</param>
    /// <returns>True if the permission is high-risk, false otherwise.</returns>
    public static bool IsHighRiskPermission(this string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            return false;
        }

        var highRiskPermissions = new[]
        {
            "LoadNativeDll",
            "ProcessControl",
            "RegistryAccess",
            "FileSystemFull",
            "NetworkUnrestricted",
            "ExecuteArbitraryCode",
            "SystemAdministration"
        };

        return highRiskPermissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Calculates a security score for a plugin based on its permissions and characteristics.
    /// </summary>
    /// <param name="context">The security context.</param>
    /// <returns>A security score from 0 (highest risk) to 100 (lowest risk).</returns>
    public static int CalculateSecurityScore(this PluginSecurityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var score = 100; // Start with maximum security

        // Reduce score based on violations
        score -= context.Violations.Count * 10;
        score -= context.GetViolationsBySeverity(ViolationSeverity.Critical).Count() * 20;
        score -= context.GetViolationsBySeverity(ViolationSeverity.High).Count() * 15;

        // Reduce score based on high-risk permissions
        var highRiskCount = context.AllowedPermissions.Count(p => p.IsHighRiskPermission());
        score -= highRiskCount * 5;

        // Adjust based on isolation level
        score += (int)context.IsolationLevel * 5;

        // Ensure score is within valid range
        return Math.Max(0, Math.Min(100, score));
    }

    /// <summary>
    /// Determines if a plugin context indicates a trusted plugin.
    /// </summary>
    /// <param name="context">The security context.</param>
    /// <returns>True if the plugin is trusted, false otherwise.</returns>
    public static bool IsTrusted(this PluginSecurityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Plugin is trusted if it has no violations and high isolation
        return !context.HasViolations &&

               context.IsolationLevel >= PluginIsolationLevel.High &&
               context.CalculateSecurityScore() >= 80;
    }
}