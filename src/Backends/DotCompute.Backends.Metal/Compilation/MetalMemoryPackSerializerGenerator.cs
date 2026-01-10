// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Compilation;

/// <summary>
/// Generates MSL (Metal Shading Language) serializer/deserializer code that matches MemoryPack's exact binary format.
/// </summary>
/// <remarks>
/// <para>
/// MemoryPack binary format:
/// - Header: 1-byte member count (0-249 = count, 255 = null)
/// - Fields: Serialized in declaration order, no padding
/// - Primitive types: Little-endian, direct copy
/// - Guid: 16 bytes using ToByteArray() ordering
/// - Nullable&lt;T&gt;: 1-byte has_value flag + T value (if present)
/// - Collections: 4-byte signed int32 length (-1 = null) + elements
/// - Strings: 4-byte signed int32 byte count (-1 = null) + UTF-8 bytes
/// </para>
/// <para>
/// Generated code enables GPU-side message processing without host round-trips on Apple Silicon.
/// MSL-specific considerations:
/// - Uses <c>device</c> address space for GPU memory pointers
/// - No explicit <c>__device__</c> annotation (implicit in MSL)
/// - Metal compiler auto-vectorizes loops
/// </para>
/// </remarks>
public sealed class MetalMemoryPackSerializerGenerator
{
    private readonly ILogger<MetalMemoryPackSerializerGenerator> _logger;

    // LoggerMessage delegates for high-performance logging
    private static readonly Action<ILogger, string, Exception?> s_logGenerationStarted =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1, nameof(GenerateSerializer)),
            "Starting MSL MemoryPack serializer generation for '{TypeName}'");

    private static readonly Action<ILogger, string, int, Exception?> s_logGenerationCompleted =
        LoggerMessage.Define<string, int>(
            LogLevel.Information,
            new EventId(2, nameof(GenerateSerializer)),
            "Completed MSL MemoryPack serializer generation for '{TypeName}' ({FieldCount} fields)");

    private static readonly Action<ILogger, string, string, Exception> s_logGenerationError =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(3, nameof(GenerateSerializer)),
            "Failed to generate MSL MemoryPack serializer for '{TypeName}': {ErrorMessage}");

    private static readonly Action<ILogger, string, string, int, Exception?> s_logFieldMapping =
        LoggerMessage.Define<string, string, int>(
            LogLevel.Debug,
            new EventId(4, nameof(GenerateSerializer)),
            "Mapping field '{FieldName}' of type '{FieldType}' at offset {Offset}");

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalMemoryPackSerializerGenerator"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    public MetalMemoryPackSerializerGenerator(ILogger<MetalMemoryPackSerializerGenerator> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>
    /// Generates complete MSL serialization code for a MemoryPackable type.
    /// </summary>
    /// <param name="messageType">The message type to generate code for.</param>
    /// <returns>Generated MSL code including struct, deserializer, and serializer.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="messageType"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when code generation fails.</exception>
    public string GenerateSerializer(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);

        s_logGenerationStarted(_logger, messageType.Name, null);

        try
        {
            var builder = new StringBuilder(4096);
            var fields = GetSerializableFields(messageType);

            // 1. Generate file header
            AppendFileHeader(builder, messageType);

            // 2. Generate MSL struct definition
            AppendStructDefinition(builder, messageType, fields);

            // 3. Generate deserializer function
            AppendDeserializer(builder, messageType, fields);

            // 4. Generate serializer function
            AppendSerializer(builder, messageType, fields);

            // 5. Generate message handler wrapper
            AppendMessageHandler(builder, messageType);

            s_logGenerationCompleted(_logger, messageType.Name, fields.Count, null);

            return builder.ToString();
        }
        catch (Exception ex)
        {
            s_logGenerationError(_logger, messageType.Name, ex.Message, ex);
            throw new InvalidOperationException(
                $"Failed to generate MSL MemoryPack serializer for '{messageType.Name}': {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Generates MSL serialization code for multiple MemoryPackable types in a single compilation unit.
    /// </summary>
    /// <param name="messageTypes">The message types to generate code for.</param>
    /// <param name="compilationUnitName">The name of the compilation unit.</param>
    /// <param name="skipHandlerGeneration">When true, skips generating message handler (manual handler will be provided).</param>
    /// <returns>Generated MSL code for all types.</returns>
    /// <remarks>
    /// <para>
    /// Callers must ensure that all types in <paramref name="messageTypes"/> have PublicProperties accessible.
    /// This is typically satisfied by types marked with [MemoryPackable] attribute.
    /// </para>
    /// <para>
    /// Code is generated in a specific order to ensure forward declarations are not needed:
    /// 1. All struct definitions (so Response struct exists when handler is generated)
    /// 2. All deserializers
    /// 3. All serializers
    /// 4. Message handlers (one per Request/Response pair, not per type)
    /// </para>
    /// </remarks>
    [UnconditionalSuppressMessage("AOT", "IL2072", Justification = "Types passed to this method are expected to be MemoryPackable types with PublicProperties preserved")]
    public string GenerateBatchSerializer(
        IEnumerable<Type> messageTypes,
        string compilationUnitName = "MemoryPackSerializers",
        bool skipHandlerGeneration = false)
    {
        ArgumentNullException.ThrowIfNull(messageTypes);
        ArgumentException.ThrowIfNullOrWhiteSpace(compilationUnitName);

        var typeList = messageTypes.ToList();
        if (typeList.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(8192);

        // Batch header
        AppendBatchHeader(builder, compilationUnitName, typeList.Count);

        // Common type definitions
        AppendCommonTypeDefinitions(builder);

        // Pre-compute field info for all types
        var typeFieldsMap = new Dictionary<Type, List<MemoryPackFieldInfo>>();
        foreach (var messageType in typeList)
        {
            typeFieldsMap[messageType] = GetSerializableFields(messageType);
        }

        // Phase 1: Generate ALL struct definitions first
        // This ensures Response struct exists when handler references it
        _ = builder.AppendLine();
        _ = builder.AppendLine("// ============================================================================");
        _ = builder.AppendLine("// STRUCT DEFINITIONS");
        _ = builder.AppendLine("// ============================================================================");

        foreach (var messageType in typeList)
        {
            _ = builder.AppendLine();
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// --- {messageType.Name} ---");
            AppendStructDefinition(builder, messageType, typeFieldsMap[messageType]);
        }

        // Phase 2: Generate ALL deserializers
        _ = builder.AppendLine();
        _ = builder.AppendLine("// ============================================================================");
        _ = builder.AppendLine("// DESERIALIZERS");
        _ = builder.AppendLine("// ============================================================================");

        foreach (var messageType in typeList)
        {
            _ = builder.AppendLine();
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// --- {messageType.Name} ---");
            AppendDeserializer(builder, messageType, typeFieldsMap[messageType]);
        }

        // Phase 3: Generate ALL serializers
        _ = builder.AppendLine();
        _ = builder.AppendLine("// ============================================================================");
        _ = builder.AppendLine("// SERIALIZERS");
        _ = builder.AppendLine("// ============================================================================");

        foreach (var messageType in typeList)
        {
            _ = builder.AppendLine();
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// --- {messageType.Name} ---");
            AppendSerializer(builder, messageType, typeFieldsMap[messageType]);
        }

        // Phase 4: Generate message handlers (only for Request types to avoid duplicates)
        // The handler uses both Request and Response types, so only generate once per pair
        // Skip this phase entirely if a manual handler will be provided
        if (skipHandlerGeneration)
        {
            _logger.LogInformation("Phase 4: Skipping handler generation (manual handler will be provided)");
            _ = builder.AppendLine();
            _ = builder.AppendLine("// ============================================================================");
            _ = builder.AppendLine("// MESSAGE HANDLERS (skipped - manual handler provided)");
            _ = builder.AppendLine("// ============================================================================");
            return builder.ToString();
        }

        _ = builder.AppendLine();
        _ = builder.AppendLine("// ============================================================================");
        _ = builder.AppendLine("// MESSAGE HANDLERS");
        _ = builder.AppendLine("// ============================================================================");

        // Log all types for debugging
        _logger.LogDebug("Phase 4: Processing {Count} types for handler generation", typeList.Count);
        foreach (var t in typeList)
        {
            _logger.LogDebug("  - Type in list: '{TypeName}'", t.Name);
        }

        var processedHandlers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var messageType in typeList)
        {
            _logger.LogDebug("Processing type '{TypeName}' for handler generation", messageType.Name);

            // Only generate handler for Request types (not Response types)
            // This prevents duplicate handler generation
            if (!messageType.Name.EndsWith("Request", StringComparison.Ordinal))
            {
                _logger.LogDebug("Skipping '{TypeName}': does not end with 'Request'", messageType.Name);
                continue;
            }

            // Derive base name and check we haven't already processed this pair
            var baseName = messageType.Name[..^7]; // Remove "Request"
            _logger.LogDebug("Request type '{TypeName}' → baseName = '{BaseName}'", messageType.Name, baseName);

            if (processedHandlers.Contains(baseName))
            {
                _logger.LogDebug("Skipping '{BaseName}': already processed", baseName);
                continue;
            }

            // Verify the Response type also exists in the type list
            var responseTypeName = baseName + "Response";
            _logger.LogDebug("Looking for Response type: '{ResponseTypeName}'", responseTypeName);

            // Use case-insensitive comparison for robustness
            var hasResponseType = typeList.Any(t =>
                string.Equals(t.Name, responseTypeName, StringComparison.OrdinalIgnoreCase));

            _logger.LogDebug("Response type '{ResponseTypeName}' found: {Found}", responseTypeName, hasResponseType);

            if (hasResponseType)
            {
                _logger.LogInformation("Generating message handler for '{BaseName}' (Request: {RequestType})",
                    baseName, messageType.Name);
                _ = builder.AppendLine();
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// --- {baseName} Handler ---");
                AppendMessageHandler(builder, messageType);
                processedHandlers.Add(baseName);
            }
            else
            {
                _logger.LogWarning(
                    "Skipping handler generation for {RequestType}: matching {ResponseType} not found in type list. " +
                    "Available types: [{AvailableTypes}]",
                    messageType.Name, responseTypeName, string.Join(", ", typeList.Select(t => t.Name)));
            }
        }

        _logger.LogDebug("Phase 4 complete: Generated {Count} handlers", processedHandlers.Count);

        return builder.ToString();
    }

    #region Field Analysis

    /// <summary>
    /// Gets the serializable fields from a type in declaration order.
    /// </summary>
    private List<MemoryPackFieldInfo> GetSerializableFields(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type messageType)
    {
        var fields = new List<MemoryPackFieldInfo>();
        var properties = messageType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite) // Skip read-only properties like MessageType
            .OrderBy(p => p.MetadataToken); // Declaration order

        var currentOffset = 0; // Offset after 1-byte header

        foreach (var prop in properties)
        {
            var fieldInfo = new MemoryPackFieldInfo
            {
                Name = prop.Name,
                CSharpType = prop.PropertyType,
                Offset = currentOffset
            };

            // Calculate size and MSL type
            (fieldInfo.Size, fieldInfo.MslType, fieldInfo.IsNullable) = GetFieldSizeAndMslType(prop.PropertyType);

            s_logFieldMapping(_logger, prop.Name, prop.PropertyType.Name, currentOffset, null);

            fields.Add(fieldInfo);
            currentOffset += fieldInfo.Size;
        }

        return fields;
    }

    /// <summary>
    /// Gets the serialized size and MSL type for a C# type.
    /// </summary>
    private static (int Size, string MslType, bool IsNullable) GetFieldSizeAndMslType(Type csType)
    {
        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(csType);
        if (underlyingType != null)
        {
            var (innerSize, innerMslType, _) = GetFieldSizeAndMslType(underlyingType);
            // Nullable<T>: 1 byte has_value + T bytes
            return (1 + innerSize, $"nullable_{innerMslType}", true);
        }

        // Primitive types - use pattern matching for type size/MSL mapping
        // MSL uses standard C++ types with Metal-specific qualifiers
        return csType switch
        {
            _ when csType == typeof(byte) => (1, "uchar", false),      // MSL: uchar (unsigned char)
            _ when csType == typeof(sbyte) => (1, "char", false),      // MSL: char (signed char)
            _ when csType == typeof(bool) => (1, "uchar", false),      // MSL: uchar for booleans
            _ when csType == typeof(short) => (2, "short", false),     // MSL: short
            _ when csType == typeof(ushort) => (2, "ushort", false),   // MSL: ushort
            _ when csType == typeof(int) => (4, "int", false),         // MSL: int
            _ when csType == typeof(uint) => (4, "uint", false),       // MSL: uint
            _ when csType == typeof(long) => (8, "long", false),       // MSL: long
            _ when csType == typeof(ulong) => (8, "ulong", false),     // MSL: ulong
            _ when csType == typeof(float) => (4, "float", false),     // MSL: float
            _ when csType == typeof(double) => (8, "double", false),   // MSL: double (if supported)
            _ when csType == typeof(Guid) => (16, "guid_t", false),    // Custom struct
            _ => throw new NotSupportedException(
                $"Type '{csType.FullName}' is not supported for MemoryPack MSL serialization. " +
                $"Supported types: primitives, Guid, and Nullable<T> of supported types.")
        };
    }

    #endregion

    #region Code Generation

    /// <summary>
    /// Appends the file header with copyright and generation metadata.
    /// </summary>
    private static void AppendFileHeader(StringBuilder builder, Type messageType)
    {
        _ = builder.AppendLine("// ===========================================================================");
        _ = builder.AppendLine("// Auto-Generated MSL (Metal Shading Language) MemoryPack Serializer");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// Message Type: {messageType.Name}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// Namespace: {messageType.Namespace}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        _ = builder.AppendLine("// DO NOT EDIT - This file is automatically generated");
        _ = builder.AppendLine("//");
        _ = builder.AppendLine("// MemoryPack Binary Format:");
        _ = builder.AppendLine("// - Header: 1-byte member count");
        _ = builder.AppendLine("// - Fields: Serialized in declaration order (little-endian)");
        _ = builder.AppendLine("// ===========================================================================");
        _ = builder.AppendLine();
        _ = builder.AppendLine("#include <metal_stdlib>");
        _ = builder.AppendLine("using namespace metal;");
        _ = builder.AppendLine();

        AppendCommonTypeDefinitions(builder);
    }

    /// <summary>
    /// Appends batch compilation unit header.
    /// </summary>
    private static void AppendBatchHeader(StringBuilder builder, string unitName, int typeCount)
    {
        _ = builder.AppendLine("// ===========================================================================");
        _ = builder.AppendLine("// Auto-Generated MSL (Metal Shading Language) MemoryPack Serializers (Batch)");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// Unit Name: {unitName}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// Type Count: {typeCount}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        _ = builder.AppendLine("// DO NOT EDIT - This file is automatically generated");
        _ = builder.AppendLine("// ===========================================================================");
        _ = builder.AppendLine();
        _ = builder.AppendLine("#include <metal_stdlib>");
        _ = builder.AppendLine("using namespace metal;");
        _ = builder.AppendLine();
    }

    /// <summary>
    /// Appends common type definitions needed by all serializers.
    /// </summary>
    private static void AppendCommonTypeDefinitions(StringBuilder builder)
    {
        _ = builder.AppendLine("// Common type definitions for MemoryPack serialization");
        _ = builder.AppendLine();
        _ = builder.AppendLine("// Guid structure (16 bytes, byte-by-byte copy from .NET Guid.ToByteArray())");
        _ = builder.AppendLine("struct guid_t {");
        _ = builder.AppendLine("    uchar bytes[16];");
        _ = builder.AppendLine("};");
        _ = builder.AppendLine();
        _ = builder.AppendLine("// Nullable Guid structure (1 + 16 = 17 bytes)");
        _ = builder.AppendLine("struct nullable_guid_t {");
        _ = builder.AppendLine("    uchar has_value;    // 0 = null, non-zero = has value");
        _ = builder.AppendLine("    uchar value[16];    // Guid bytes (only valid if has_value)");
        _ = builder.AppendLine("};");
        _ = builder.AppendLine();
        _ = builder.AppendLine("// MemoryPack constants");
        _ = builder.AppendLine("constant uchar MEMORYPACK_NULL_OBJECT = 255;");
        _ = builder.AppendLine("constant int MEMORYPACK_HEADER_SIZE = 1;");
        _ = builder.AppendLine();
        _ = builder.AppendLine("// Helper function to read little-endian values from buffer");
        _ = builder.AppendLine("template<typename T>");
        _ = builder.AppendLine("inline T read_le(device const uchar* buffer, int offset) {");
        _ = builder.AppendLine("    T result = 0;");
        _ = builder.AppendLine("    for (int i = 0; i < (int)sizeof(T); i++) {");
        _ = builder.AppendLine("        result |= T(buffer[offset + i]) << (i * 8);");
        _ = builder.AppendLine("    }");
        _ = builder.AppendLine("    return result;");
        _ = builder.AppendLine("}");
        _ = builder.AppendLine();
        _ = builder.AppendLine("// Helper function to write little-endian values to buffer");
        _ = builder.AppendLine("template<typename T>");
        _ = builder.AppendLine("inline void write_le(device uchar* buffer, int offset, T value) {");
        _ = builder.AppendLine("    for (int i = 0; i < (int)sizeof(T); i++) {");
        _ = builder.AppendLine("        buffer[offset + i] = uchar((value >> (i * 8)) & 0xFF);");
        _ = builder.AppendLine("    }");
        _ = builder.AppendLine("}");
        _ = builder.AppendLine();
        _ = builder.AppendLine("// Specialization for float (bitcast to uint for byte access)");
        _ = builder.AppendLine("inline float read_le_float(device const uchar* buffer, int offset) {");
        _ = builder.AppendLine("    uint bits = read_le<uint>(buffer, offset);");
        _ = builder.AppendLine("    return as_type<float>(bits);");
        _ = builder.AppendLine("}");
        _ = builder.AppendLine();
        _ = builder.AppendLine("inline void write_le_float(device uchar* buffer, int offset, float value) {");
        _ = builder.AppendLine("    uint bits = as_type<uint>(value);");
        _ = builder.AppendLine("    write_le<uint>(buffer, offset, bits);");
        _ = builder.AppendLine("}");
        _ = builder.AppendLine();
    }

    /// <summary>
    /// Appends the MSL struct definition for a message type.
    /// </summary>
    private static void AppendStructDefinition(StringBuilder builder, Type messageType, List<MemoryPackFieldInfo> fields)
    {
        var structName = ToSnakeCase(messageType.Name);
        var totalSize = fields.Sum(f => f.Size);

        _ = builder.AppendLine("/**");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $" * @brief MSL struct for {messageType.Name}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $" * @details MemoryPack format: {totalSize} bytes data + 1 byte header = {totalSize + 1} bytes total");
        _ = builder.AppendLine(" */");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"struct {structName} {{");

        foreach (var field in fields)
        {
            var mslFieldName = ToSnakeCase(field.Name);
            var comment = $"// Offset: {field.Offset}, Size: {field.Size}";

            if (field.IsNullable)
            {
                // Nullable fields need special handling
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    struct {{");
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        uchar has_value; {comment}");

                // Get inner type for nullable
                var innerType = Nullable.GetUnderlyingType(field.CSharpType)!;
                var (_, innerMslType, _) = GetFieldSizeAndMslType(innerType);

                if (innerType == typeof(Guid))
                {
                    _ = builder.AppendLine("        uchar value[16];");
                }
                else
                {
                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {innerMslType} value;");
                }
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    }} {mslFieldName};");
            }
            else if (field.CSharpType == typeof(Guid))
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    uchar {mslFieldName}[16]; {comment}");
            }
            else
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    {field.MslType} {mslFieldName}; {comment}");
            }
        }

        _ = builder.AppendLine("};");
        _ = builder.AppendLine();
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// Total struct size: {totalSize} bytes (excludes 1-byte MemoryPack header)");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// Total serialized size: {totalSize + 1} bytes (includes header)");
        _ = builder.AppendLine();
    }

    /// <summary>
    /// Appends the MSL deserializer function.
    /// </summary>
    private static void AppendDeserializer(StringBuilder builder, Type messageType, List<MemoryPackFieldInfo> fields)
    {
        var structName = ToSnakeCase(messageType.Name);
        var totalSize = fields.Sum(f => f.Size);
        var expectedMemberCount = fields.Count;

        _ = builder.AppendLine("/**");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $" * @brief Deserialize MemoryPack buffer to {messageType.Name}");
        _ = builder.AppendLine(" * @param buffer Input buffer (MemoryPack serialized data)");
        _ = builder.AppendLine(" * @param buffer_size Size of input buffer in bytes");
        _ = builder.AppendLine(" * @param out Output struct to populate");
        _ = builder.AppendLine(" * @return true if deserialization succeeded, false otherwise");
        _ = builder.AppendLine(" */");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"inline bool deserialize_{structName}(");
        _ = builder.AppendLine("    device const uchar* buffer,");
        _ = builder.AppendLine("    int buffer_size,");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    thread {structName}* out)");
        _ = builder.AppendLine("{");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    // Minimum buffer size: 1 (header) + {totalSize} (data) = {totalSize + 1} bytes");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    if (buffer_size < {totalSize + 1}) return false;");
        _ = builder.AppendLine();
        _ = builder.AppendLine("    // Check MemoryPack header (member count)");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    if (buffer[0] != {expectedMemberCount}) {{");
        _ = builder.AppendLine("        // Unexpected member count - format mismatch");
        _ = builder.AppendLine("        return false;");
        _ = builder.AppendLine("    }");
        _ = builder.AppendLine();
        _ = builder.AppendLine("    // Skip header byte - data starts at offset 1");
        _ = builder.AppendLine("    device const uchar* data = buffer + MEMORYPACK_HEADER_SIZE;");
        _ = builder.AppendLine();

        // Generate field deserialization
        foreach (var field in fields)
        {
            var mslFieldName = ToSnakeCase(field.Name);
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    // {field.Name}: offset {field.Offset}, size {field.Size}");

            if (field.IsNullable)
            {
                // Nullable field deserialization
                var innerType = Nullable.GetUnderlyingType(field.CSharpType)!;
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    out->{mslFieldName}.has_value = data[{field.Offset}] != 0;");
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    if (out->{mslFieldName}.has_value) {{");

                if (innerType == typeof(Guid))
                {
                    _ = builder.AppendLine("        for (int i = 0; i < 16; i++) {");
                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"            out->{mslFieldName}.value[i] = data[{field.Offset + 1} + i];");
                    _ = builder.AppendLine("        }");
                }
                else if (innerType == typeof(float))
                {
                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        out->{mslFieldName}.value = read_le_float(data, {field.Offset + 1});");
                }
                else
                {
                    var (_, innerMslType, _) = GetFieldSizeAndMslType(innerType);
                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        out->{mslFieldName}.value = read_le<{innerMslType}>(data, {field.Offset + 1});");
                }

                _ = builder.AppendLine("    }");
            }
            else if (field.CSharpType == typeof(Guid))
            {
                // Guid deserialization (byte-by-byte copy)
                _ = builder.AppendLine("    for (int i = 0; i < 16; i++) {");
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        out->{mslFieldName}[i] = data[{field.Offset} + i];");
                _ = builder.AppendLine("    }");
            }
            else if (field.CSharpType == typeof(float))
            {
                // Float needs bitcast through uint
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    out->{mslFieldName} = read_le_float(data, {field.Offset});");
            }
            else
            {
                // Primitive type deserialization using helper
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    out->{mslFieldName} = read_le<{field.MslType}>(data, {field.Offset});");
            }

            _ = builder.AppendLine();
        }

        _ = builder.AppendLine("    return true;");
        _ = builder.AppendLine("}");
        _ = builder.AppendLine();
    }

    /// <summary>
    /// Appends the MSL serializer function.
    /// </summary>
    private static void AppendSerializer(StringBuilder builder, Type messageType, List<MemoryPackFieldInfo> fields)
    {
        var structName = ToSnakeCase(messageType.Name);
        var totalSize = fields.Sum(f => f.Size);
        var memberCount = fields.Count;

        _ = builder.AppendLine("/**");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $" * @brief Serialize {messageType.Name} to MemoryPack buffer");
        _ = builder.AppendLine(" * @param input Input struct to serialize");
        _ = builder.AppendLine(" * @param buffer Output buffer");
        _ = builder.AppendLine(" * @param buffer_size Size of output buffer in bytes");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $" * @return Number of bytes written ({totalSize + 1}), or 0 if buffer too small");
        _ = builder.AppendLine(" */");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"inline int serialize_{structName}(");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    thread const {structName}* input,");
        _ = builder.AppendLine("    device uchar* buffer,");
        _ = builder.AppendLine("    int buffer_size)");
        _ = builder.AppendLine("{");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    // Required buffer size: 1 (header) + {totalSize} (data) = {totalSize + 1} bytes");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    if (buffer_size < {totalSize + 1}) return 0;");
        _ = builder.AppendLine();
        _ = builder.AppendLine("    // Write MemoryPack header (member count)");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    buffer[0] = {memberCount};");
        _ = builder.AppendLine();
        _ = builder.AppendLine("    // Data starts after header");
        _ = builder.AppendLine("    device uchar* data = buffer + MEMORYPACK_HEADER_SIZE;");
        _ = builder.AppendLine();

        // Generate field serialization
        foreach (var field in fields)
        {
            var mslFieldName = ToSnakeCase(field.Name);
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    // {field.Name}: offset {field.Offset}, size {field.Size}");

            if (field.IsNullable)
            {
                // Nullable field serialization
                var innerType = Nullable.GetUnderlyingType(field.CSharpType)!;
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    data[{field.Offset}] = input->{mslFieldName}.has_value ? 1 : 0;");
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    if (input->{mslFieldName}.has_value) {{");

                if (innerType == typeof(Guid))
                {
                    _ = builder.AppendLine("        for (int i = 0; i < 16; i++) {");
                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"            data[{field.Offset + 1} + i] = input->{mslFieldName}.value[i];");
                    _ = builder.AppendLine("        }");
                }
                else if (innerType == typeof(float))
                {
                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        write_le_float(data, {field.Offset + 1}, input->{mslFieldName}.value);");
                }
                else
                {
                    var (_, innerMslType, _) = GetFieldSizeAndMslType(innerType);
                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        write_le<{innerMslType}>(data, {field.Offset + 1}, input->{mslFieldName}.value);");
                }

                _ = builder.AppendLine("    } else {");
                _ = builder.AppendLine("        // Zero out the value bytes for null");

                if (innerType == typeof(Guid))
                {
                    _ = builder.AppendLine("        for (int i = 0; i < 16; i++) {");
                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"            data[{field.Offset + 1} + i] = 0;");
                    _ = builder.AppendLine("        }");
                }
                else
                {
                    var (innerSize, _, _) = GetFieldSizeAndMslType(innerType);
                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        for (int i = 0; i < {innerSize}; i++) {{");
                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"            data[{field.Offset + 1} + i] = 0;");
                    _ = builder.AppendLine("        }");
                }

                _ = builder.AppendLine("    }");
            }
            else if (field.CSharpType == typeof(Guid))
            {
                // Guid serialization (byte-by-byte copy)
                _ = builder.AppendLine("    for (int i = 0; i < 16; i++) {");
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        data[{field.Offset} + i] = input->{mslFieldName}[i];");
                _ = builder.AppendLine("    }");
            }
            else if (field.CSharpType == typeof(float))
            {
                // Float needs bitcast through uint
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    write_le_float(data, {field.Offset}, input->{mslFieldName});");
            }
            else
            {
                // Primitive type serialization using helper
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    write_le<{field.MslType}>(data, {field.Offset}, input->{mslFieldName});");
            }

            _ = builder.AppendLine();
        }

        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    return {totalSize + 1}; // Total bytes written");
        _ = builder.AppendLine("}");
        _ = builder.AppendLine();
    }

    /// <summary>
    /// Appends a message handler wrapper function that integrates deserialize → process → serialize.
    /// </summary>
    private static void AppendMessageHandler(StringBuilder builder, Type messageType)
    {
        // Try to identify request/response pair
        var isRequest = messageType.Name.EndsWith("Request", StringComparison.Ordinal);
        var baseName = messageType.Name;
        if (isRequest)
        {
            baseName = messageType.Name[..^7]; // Remove "Request"
        }
        else if (messageType.Name.EndsWith("Response", StringComparison.Ordinal))
        {
            baseName = messageType.Name[..^8]; // Remove "Response"
        }

        var handlerName = $"process_{ToSnakeCase(baseName)}_message";
        var responseStructName = ToSnakeCase(baseName + "Response");
        var requestStructName = ToSnakeCase(baseName + "Request");

        _ = builder.AppendLine("/**");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $" * @brief Message handler for {baseName} messages");
        _ = builder.AppendLine(" * @param input_buffer Serialized input message (MemoryPack format)");
        _ = builder.AppendLine(" * @param input_size Size of input buffer");
        _ = builder.AppendLine(" * @param output_buffer Buffer for serialized output message");
        _ = builder.AppendLine(" * @param output_size Size of output buffer");
        _ = builder.AppendLine(" * @return true if processing succeeded, false otherwise");
        _ = builder.AppendLine(" *");
        _ = builder.AppendLine(" * @note This is a template handler. Replace the processing logic with actual implementation.");
        _ = builder.AppendLine(" */");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"inline bool {handlerName}(");
        _ = builder.AppendLine("    device const uchar* input_buffer,");
        _ = builder.AppendLine("    int input_size,");
        _ = builder.AppendLine("    device uchar* output_buffer,");
        _ = builder.AppendLine("    int output_size)");
        _ = builder.AppendLine("{");
        _ = builder.AppendLine("    // Deserialize input message");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    {requestStructName} request;");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    if (!deserialize_{requestStructName}(input_buffer, input_size, &request)) {{");
        _ = builder.AppendLine("        return false;");
        _ = builder.AppendLine("    }");
        _ = builder.AppendLine();
        _ = builder.AppendLine("    // Prepare response");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    {responseStructName} response;");
        _ = builder.AppendLine();
        _ = builder.AppendLine("    // Copy message metadata from request to response");
        _ = builder.AppendLine("    for (int i = 0; i < 16; i++) {");
        _ = builder.AppendLine("        response.message_id[i] = request.message_id[i];");
        _ = builder.AppendLine("    }");
        _ = builder.AppendLine("    response.priority = request.priority;");
        _ = builder.AppendLine("    response.correlation_id.has_value = request.correlation_id.has_value;");
        _ = builder.AppendLine("    if (request.correlation_id.has_value) {");
        _ = builder.AppendLine("        for (int i = 0; i < 16; i++) {");
        _ = builder.AppendLine("            response.correlation_id.value[i] = request.correlation_id.value[i];");
        _ = builder.AppendLine("        }");
        _ = builder.AppendLine("    }");
        _ = builder.AppendLine();
        _ = builder.AppendLine("    // ====================================================================");
        _ = builder.AppendLine("    // MESSAGE PROCESSING LOGIC - CUSTOMIZE THIS SECTION");
        _ = builder.AppendLine("    // ====================================================================");
        _ = builder.AppendLine("    // Example: For VectorAdd, compute result = a + b");
        _ = builder.AppendLine("    // response.result = request.a + request.b;");
        _ = builder.AppendLine("    // ====================================================================");
        _ = builder.AppendLine();
        _ = builder.AppendLine("    // Serialize response");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    int bytes_written = serialize_{responseStructName}(&response, output_buffer, output_size);");
        _ = builder.AppendLine("    return bytes_written > 0;");
        _ = builder.AppendLine("}");
        _ = builder.AppendLine();
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Converts a PascalCase string to snake_case.
    /// </summary>
    private static string ToSnakeCase(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return str;
        }

        var result = new StringBuilder(str.Length + 10);
        for (var i = 0; i < str.Length; i++)
        {
            var c = str[i];
            if (char.IsUpper(c) && i > 0 && (i + 1 < str.Length && char.IsLower(str[i + 1]) || char.IsLower(str[i - 1])))
            {
                _ = result.Append('_');
                _ = result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                _ = result.Append(char.ToLowerInvariant(c));
            }
        }

        return result.ToString();
    }

    #endregion

    #region Helper Types

    /// <summary>
    /// Information about a serializable field.
    /// </summary>
    private sealed class MemoryPackFieldInfo
    {
        public string Name { get; set; } = string.Empty;
        public Type CSharpType { get; set; } = typeof(object);
        public string MslType { get; set; } = string.Empty;
        public int Offset { get; set; }
        public int Size { get; set; }
        public bool IsNullable { get; set; }
    }

    #endregion
}
