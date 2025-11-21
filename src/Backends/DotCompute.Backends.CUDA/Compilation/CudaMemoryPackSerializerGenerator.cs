// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Compilation;

/// <summary>
/// Generates CUDA C++ serializer/deserializer code that matches MemoryPack's exact binary format.
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
/// Generated code enables GPU-side message processing without host round-trips.
/// </para>
/// </remarks>
public sealed class CudaMemoryPackSerializerGenerator
{
    private readonly ILogger<CudaMemoryPackSerializerGenerator> _logger;

    // LoggerMessage delegates for high-performance logging
    private static readonly Action<ILogger, string, Exception?> _sLogGenerationStarted =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1, nameof(GenerateSerializer)),
            "Starting CUDA MemoryPack serializer generation for '{TypeName}'");

    private static readonly Action<ILogger, string, int, Exception?> _sLogGenerationCompleted =
        LoggerMessage.Define<string, int>(
            LogLevel.Information,
            new EventId(2, nameof(GenerateSerializer)),
            "Completed CUDA MemoryPack serializer generation for '{TypeName}' ({FieldCount} fields)");

    private static readonly Action<ILogger, string, string, Exception> _sLogGenerationError =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(3, nameof(GenerateSerializer)),
            "Failed to generate CUDA MemoryPack serializer for '{TypeName}': {ErrorMessage}");

    private static readonly Action<ILogger, string, string, int, Exception?> _sLogFieldMapping =
        LoggerMessage.Define<string, string, int>(
            LogLevel.Debug,
            new EventId(4, nameof(GenerateSerializer)),
            "Mapping field '{FieldName}' of type '{FieldType}' at offset {Offset}");

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaMemoryPackSerializerGenerator"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    public CudaMemoryPackSerializerGenerator(ILogger<CudaMemoryPackSerializerGenerator> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>
    /// Generates complete CUDA C++ serialization code for a MemoryPackable type.
    /// </summary>
    /// <param name="messageType">The message type to generate code for.</param>
    /// <returns>Generated CUDA C++ code including struct, deserializer, and serializer.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="messageType"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when code generation fails.</exception>
    public string GenerateSerializer(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);

        _sLogGenerationStarted(_logger, messageType.Name, null);

        try
        {
            var builder = new StringBuilder(4096);
            var fields = GetSerializableFields(messageType);

            // 1. Generate file header
            AppendFileHeader(builder, messageType);

            // 2. Generate CUDA struct definition
            AppendStructDefinition(builder, messageType, fields);

            // 3. Generate deserializer function
            AppendDeserializer(builder, messageType, fields);

            // 4. Generate serializer function
            AppendSerializer(builder, messageType, fields);

            // 5. Generate message handler wrapper
            AppendMessageHandler(builder, messageType);

            _sLogGenerationCompleted(_logger, messageType.Name, fields.Count, null);

            return builder.ToString();
        }
        catch (Exception ex)
        {
            _sLogGenerationError(_logger, messageType.Name, ex.Message, ex);
            throw new InvalidOperationException(
                $"Failed to generate CUDA MemoryPack serializer for '{messageType.Name}': {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Generates CUDA serialization code for multiple MemoryPackable types in a single compilation unit.
    /// </summary>
    /// <param name="messageTypes">The message types to generate code for.</param>
    /// <param name="compilationUnitName">The name of the compilation unit.</param>
    /// <returns>Generated CUDA C++ code for all types.</returns>
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
        string compilationUnitName = "MemoryPackSerializers")
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
        _ = builder.AppendLine();
        _ = builder.AppendLine("// ============================================================================");
        _ = builder.AppendLine("// MESSAGE HANDLERS");
        _ = builder.AppendLine("// ============================================================================");

        var processedHandlers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var messageType in typeList)
        {
            // Only generate handler for Request types (not Response types)
            // This prevents duplicate handler generation
            if (!messageType.Name.EndsWith("Request", StringComparison.Ordinal))
            {
                continue;
            }

            // Derive base name and check we haven't already processed this pair
            var baseName = messageType.Name[..^7]; // Remove "Request"
            if (processedHandlers.Contains(baseName))
            {
                continue;
            }

            // Verify the Response type also exists in the type list
            var responseTypeName = baseName + "Response";
            var hasResponseType = typeList.Any(t => t.Name == responseTypeName);

            if (hasResponseType)
            {
                _ = builder.AppendLine();
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// --- {baseName} Handler ---");
                AppendMessageHandler(builder, messageType);
                processedHandlers.Add(baseName);
            }
            else
            {
                _logger.LogWarning(
                    "Skipping handler generation for {RequestType}: matching {ResponseType} not found in type list",
                    messageType.Name, responseTypeName);
            }
        }

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

            // Calculate size and CUDA type
            (fieldInfo.Size, fieldInfo.CudaType, fieldInfo.IsNullable) = GetFieldSizeAndCudaType(prop.PropertyType);

            _sLogFieldMapping(_logger, prop.Name, prop.PropertyType.Name, currentOffset, null);

            fields.Add(fieldInfo);
            currentOffset += fieldInfo.Size;
        }

        return fields;
    }

    /// <summary>
    /// Gets the serialized size and CUDA type for a C# type.
    /// </summary>
    private static (int Size, string CudaType, bool IsNullable) GetFieldSizeAndCudaType(Type csType)
    {
        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(csType);
        if (underlyingType != null)
        {
            var (innerSize, innerCudaType, _) = GetFieldSizeAndCudaType(underlyingType);
            // Nullable<T>: 1 byte has_value + T bytes
            return (1 + innerSize, $"nullable_{innerCudaType}", true);
        }

        // Primitive types - use pattern matching for type size/CUDA mapping
        return csType switch
        {
            _ when csType == typeof(byte) => (1, "uint8_t", false),
            _ when csType == typeof(sbyte) => (1, "int8_t", false),
            _ when csType == typeof(bool) => (1, "uint8_t", false),
            _ when csType == typeof(short) => (2, "int16_t", false),
            _ when csType == typeof(ushort) => (2, "uint16_t", false),
            _ when csType == typeof(int) => (4, "int32_t", false),
            _ when csType == typeof(uint) => (4, "uint32_t", false),
            _ when csType == typeof(long) => (8, "int64_t", false),
            _ when csType == typeof(ulong) => (8, "uint64_t", false),
            _ when csType == typeof(float) => (4, "float", false),
            _ when csType == typeof(double) => (8, "double", false),
            _ when csType == typeof(Guid) => (16, "guid_t", false),
            _ => throw new NotSupportedException(
                $"Type '{csType.FullName}' is not supported for MemoryPack CUDA serialization. " +
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
        _ = builder.AppendLine("// Auto-Generated CUDA MemoryPack Serializer");
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

        AppendCommonTypeDefinitions(builder);
    }

    /// <summary>
    /// Appends batch compilation unit header.
    /// </summary>
    private static void AppendBatchHeader(StringBuilder builder, string unitName, int typeCount)
    {
        _ = builder.AppendLine("// ===========================================================================");
        _ = builder.AppendLine("// Auto-Generated CUDA MemoryPack Serializers (Batch)");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// Unit Name: {unitName}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// Type Count: {typeCount}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        _ = builder.AppendLine("// DO NOT EDIT - This file is automatically generated");
        _ = builder.AppendLine("// ===========================================================================");
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
        _ = builder.AppendLine("    uint8_t bytes[16];");
        _ = builder.AppendLine("};");
        _ = builder.AppendLine();
        _ = builder.AppendLine("// Nullable Guid structure (1 + 16 = 17 bytes)");
        _ = builder.AppendLine("struct nullable_guid_t {");
        _ = builder.AppendLine("    uint8_t has_value;    // 0 = null, non-zero = has value");
        _ = builder.AppendLine("    uint8_t value[16];    // Guid bytes (only valid if has_value)");
        _ = builder.AppendLine("};");
        _ = builder.AppendLine();
        _ = builder.AppendLine("// MemoryPack constants");
        _ = builder.AppendLine("#define MEMORYPACK_NULL_OBJECT 255");
        _ = builder.AppendLine("#define MEMORYPACK_HEADER_SIZE 1");
        _ = builder.AppendLine();
    }

    /// <summary>
    /// Appends the CUDA struct definition for a message type.
    /// </summary>
    private static void AppendStructDefinition(StringBuilder builder, Type messageType, List<MemoryPackFieldInfo> fields)
    {
        var structName = ToSnakeCase(messageType.Name);
        var totalSize = fields.Sum(f => f.Size);

        _ = builder.AppendLine("/**");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $" * @brief CUDA struct for {messageType.Name}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $" * @details MemoryPack format: {totalSize} bytes data + 1 byte header = {totalSize + 1} bytes total");
        _ = builder.AppendLine(" */");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"struct {structName} {{");

        foreach (var field in fields)
        {
            var cudaFieldName = ToSnakeCase(field.Name);
            var comment = $"// Offset: {field.Offset}, Size: {field.Size}";

            if (field.IsNullable)
            {
                // Nullable fields need special handling
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    struct {{");
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        uint8_t has_value; {comment}");

                // Get inner type for nullable
                var innerType = Nullable.GetUnderlyingType(field.CSharpType)!;
                var (_, innerCudaType, _) = GetFieldSizeAndCudaType(innerType);

                if (innerType == typeof(Guid))
                {
                    _ = builder.AppendLine("        uint8_t value[16];");
                }
                else
                {
                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {innerCudaType} value;");
                }
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    }} {cudaFieldName};");
            }
            else if (field.CSharpType == typeof(Guid))
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    uint8_t {cudaFieldName}[16]; {comment}");
            }
            else
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    {field.CudaType} {cudaFieldName}; {comment}");
            }
        }

        _ = builder.AppendLine("};");
        _ = builder.AppendLine();
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// Total struct size: {totalSize} bytes (excludes 1-byte MemoryPack header)");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// Total serialized size: {totalSize + 1} bytes (includes header)");
        _ = builder.AppendLine();
    }

    /// <summary>
    /// Appends the CUDA deserializer function.
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
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"__device__ bool deserialize_{structName}(");
        _ = builder.AppendLine("    const uint8_t* buffer,");
        _ = builder.AppendLine("    int buffer_size,");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    {structName}* out)");
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
        _ = builder.AppendLine("    const uint8_t* data = buffer + MEMORYPACK_HEADER_SIZE;");
        _ = builder.AppendLine();

        // Generate field deserialization
        foreach (var field in fields)
        {
            var cudaFieldName = ToSnakeCase(field.Name);
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    // {field.Name}: offset {field.Offset}, size {field.Size}");

            if (field.IsNullable)
            {
                // Nullable field deserialization
                var innerType = Nullable.GetUnderlyingType(field.CSharpType)!;
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    out->{cudaFieldName}.has_value = data[{field.Offset}] != 0;");
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    if (out->{cudaFieldName}.has_value) {{");

                if (innerType == typeof(Guid))
                {
                    _ = builder.AppendLine("        #pragma unroll");
                    _ = builder.AppendLine("        for (int i = 0; i < 16; i++) {");
                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"            out->{cudaFieldName}.value[i] = data[{field.Offset + 1} + i];");
                    _ = builder.AppendLine("        }");
                }
                else
                {
                    var (_, innerCudaType, _) = GetFieldSizeAndCudaType(innerType);
                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        out->{cudaFieldName}.value = *reinterpret_cast<const {innerCudaType}*>(&data[{field.Offset + 1}]);");
                }

                _ = builder.AppendLine("    }");
            }
            else if (field.CSharpType == typeof(Guid))
            {
                // Guid deserialization (byte-by-byte copy)
                _ = builder.AppendLine("    #pragma unroll");
                _ = builder.AppendLine("    for (int i = 0; i < 16; i++) {");
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        out->{cudaFieldName}[i] = data[{field.Offset} + i];");
                _ = builder.AppendLine("    }");
            }
            else
            {
                // Primitive type deserialization
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    out->{cudaFieldName} = *reinterpret_cast<const {field.CudaType}*>(&data[{field.Offset}]);");
            }

            _ = builder.AppendLine();
        }

        _ = builder.AppendLine("    return true;");
        _ = builder.AppendLine("}");
        _ = builder.AppendLine();
    }

    /// <summary>
    /// Appends the CUDA serializer function.
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
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"__device__ int serialize_{structName}(");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    const {structName}* input,");
        _ = builder.AppendLine("    uint8_t* buffer,");
        _ = builder.AppendLine("    int buffer_size)");
        _ = builder.AppendLine("{");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    // Required buffer size: 1 (header) + {totalSize} (data) = {totalSize + 1} bytes");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    if (buffer_size < {totalSize + 1}) return 0;");
        _ = builder.AppendLine();
        _ = builder.AppendLine("    // Write MemoryPack header (member count)");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    buffer[0] = {memberCount};");
        _ = builder.AppendLine();
        _ = builder.AppendLine("    // Data starts after header");
        _ = builder.AppendLine("    uint8_t* data = buffer + MEMORYPACK_HEADER_SIZE;");
        _ = builder.AppendLine();

        // Generate field serialization
        foreach (var field in fields)
        {
            var cudaFieldName = ToSnakeCase(field.Name);
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    // {field.Name}: offset {field.Offset}, size {field.Size}");

            if (field.IsNullable)
            {
                // Nullable field serialization
                var innerType = Nullable.GetUnderlyingType(field.CSharpType)!;
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    data[{field.Offset}] = input->{cudaFieldName}.has_value ? 1 : 0;");
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    if (input->{cudaFieldName}.has_value) {{");

                if (innerType == typeof(Guid))
                {
                    _ = builder.AppendLine("        #pragma unroll");
                    _ = builder.AppendLine("        for (int i = 0; i < 16; i++) {");
                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"            data[{field.Offset + 1} + i] = input->{cudaFieldName}.value[i];");
                    _ = builder.AppendLine("        }");
                }
                else
                {
                    var (innerSize, innerCudaType, _) = GetFieldSizeAndCudaType(innerType);
                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        *reinterpret_cast<{innerCudaType}*>(&data[{field.Offset + 1}]) = input->{cudaFieldName}.value;");
                }

                _ = builder.AppendLine("    } else {");
                _ = builder.AppendLine("        // Zero out the value bytes for null");

                if (innerType == typeof(Guid))
                {
                    _ = builder.AppendLine("        #pragma unroll");
                    _ = builder.AppendLine("        for (int i = 0; i < 16; i++) {");
                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"            data[{field.Offset + 1} + i] = 0;");
                    _ = builder.AppendLine("        }");
                }
                else
                {
                    var (innerSize, _, _) = GetFieldSizeAndCudaType(innerType);
                    _ = builder.AppendLine("        #pragma unroll");
                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        for (int i = 0; i < {innerSize}; i++) {{");
                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"            data[{field.Offset + 1} + i] = 0;");
                    _ = builder.AppendLine("        }");
                }

                _ = builder.AppendLine("    }");
            }
            else if (field.CSharpType == typeof(Guid))
            {
                // Guid serialization (byte-by-byte copy)
                _ = builder.AppendLine("    #pragma unroll");
                _ = builder.AppendLine("    for (int i = 0; i < 16; i++) {");
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        data[{field.Offset} + i] = input->{cudaFieldName}[i];");
                _ = builder.AppendLine("    }");
            }
            else
            {
                // Primitive type serialization
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    *reinterpret_cast<{field.CudaType}*>(&data[{field.Offset}]) = input->{cudaFieldName};");
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
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"__device__ bool {handlerName}(");
        _ = builder.AppendLine("    const uint8_t* input_buffer,");
        _ = builder.AppendLine("    int input_size,");
        _ = builder.AppendLine("    uint8_t* output_buffer,");
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
        _ = builder.AppendLine("    #pragma unroll");
        _ = builder.AppendLine("    for (int i = 0; i < 16; i++) {");
        _ = builder.AppendLine("        response.message_id[i] = request.message_id[i];");
        _ = builder.AppendLine("    }");
        _ = builder.AppendLine("    response.priority = request.priority;");
        _ = builder.AppendLine("    response.correlation_id.has_value = request.correlation_id.has_value;");
        _ = builder.AppendLine("    if (request.correlation_id.has_value) {");
        _ = builder.AppendLine("        #pragma unroll");
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
        for (int i = 0; i < str.Length; i++)
        {
            char c = str[i];
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
        public string CudaType { get; set; } = string.Empty;
        public int Offset { get; set; }
        public int Size { get; set; }
        public bool IsNullable { get; set; }
    }

    #endregion
}
