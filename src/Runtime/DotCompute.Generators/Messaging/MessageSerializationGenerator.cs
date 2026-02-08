// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DotCompute.Generators.Messaging;

/// <summary>
/// Source generator for automatic serialization/deserialization of Ring Kernel messages.
/// </summary>
/// <remarks>
/// Generates efficient span-based serialization code for types implementing IRingKernelMessage.
/// Uses BinaryPrimitives for explicit endianness (little-endian) and optimal performance.
/// </remarks>
[Generator]
public sealed class MessageSerializationGenerator : IIncrementalGenerator
{
    private const string IRingKernelMessageFullName = "DotCompute.Abstractions.Messaging.IRingKernelMessage";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all types that might implement IRingKernelMessage
        var messageTypeDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsMessageTypeCandidate(node),
                transform: static (ctx, _) => GetMessageTypeInfo(ctx))
            .Where(static info => info is not null);

        // Generate serialization code for each message type
        context.RegisterSourceOutput(messageTypeDeclarations, static (spc, messageType) =>
        {
            if (messageType is null)
            {
                return;
            }

            var source = GenerateSerializationCode(messageType.Value);
            spc.AddSource($"{messageType.Value.TypeName}_Serialization.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    private static bool IsMessageTypeCandidate(SyntaxNode node)
    {
        // Look for class or record declarations
        return node is ClassDeclarationSyntax or RecordDeclarationSyntax;
    }

    private static MessageTypeInfo? GetMessageTypeInfo(GeneratorSyntaxContext context)
    {
        var typeDeclaration = (TypeDeclarationSyntax)context.Node;
        var typeSymbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration);

        if (typeSymbol is null)
        {
            return null;
        }

        // Check if type implements IRingKernelMessage
        if (!ImplementsIRingKernelMessage(typeSymbol))
        {
            return null;
        }

        // Extract serializable properties/fields
        var members = GetSerializableMembers(typeSymbol);

        return new MessageTypeInfo(
            typeSymbol.Name,
            typeSymbol.ContainingNamespace.ToDisplayString(),
            members,
            typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword));
    }

    private static bool ImplementsIRingKernelMessage(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.AllInterfaces.Any(i =>
            i.ToDisplayString() == IRingKernelMessageFullName);
    }

    private static ImmutableArray<MessageMemberInfo> GetSerializableMembers(INamedTypeSymbol typeSymbol)
    {
        var members = ImmutableArray.CreateBuilder<MessageMemberInfo>();

        // Get all public instance properties and fields (excluding IRingKernelMessage interface members)
        var allMembers = typeSymbol.GetMembers()
            .Where(m => m.DeclaredAccessibility == Accessibility.Public &&
                       !m.IsStatic &&
                       !IsInterfaceMember(m))
            .ToList();

        foreach (var member in allMembers)
        {
            var typeInfo = member switch
            {
                IPropertySymbol prop => (prop.Name, prop.Type),
                IFieldSymbol field => (field.Name, field.Type),
                _ => ((string, ITypeSymbol)?)null
            };

            if (typeInfo.HasValue && IsSupportedType(typeInfo.Value.Item2))
            {
                members.Add(new MessageMemberInfo(
                    typeInfo.Value.Item1,
                    typeInfo.Value.Item2.ToDisplayString(),
                    GetTypeSize(typeInfo.Value.Item2)));
            }
        }

        return members.ToImmutable();
    }

    private static bool IsInterfaceMember(ISymbol member)
    {
        // Check if member is from IRingKernelMessage interface
        var interfaceMembers = new HashSet<string>
        {
            "MessageId", "MessageType", "Priority", "CorrelationId", "PayloadSize", "Serialize", "Deserialize"
        };

        return interfaceMembers.Contains(member.Name);
    }

    private static bool IsSupportedType(ITypeSymbol type)
    {
        // Support primitive types and Guid
        var supportedTypes = new HashSet<string>
        {
            "System.Int32", "System.Int64", "System.Single", "System.Double",
            "System.Byte", "System.Int16", "System.UInt32", "System.UInt64",
            "System.Boolean", "System.Guid"
        };

        return supportedTypes.Contains(type.ToDisplayString());
    }

    private static int GetTypeSize(ITypeSymbol type)
    {
        return type.ToDisplayString() switch
        {
            "System.Byte" => 1,
            "System.Boolean" => 1,
            "System.Int16" => 2,
            "System.Int32" => 4,
            "System.UInt32" => 4,
            "System.Single" => 4,
            "System.Int64" => 8,
            "System.UInt64" => 8,
            "System.Double" => 8,
            "System.Guid" => 16,
            _ => 0
        };
    }

    private static string GenerateSerializationCode(MessageTypeInfo messageType)
    {
        var sb = new StringBuilder();

        // File header
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Copyright (c) 2025 Michael Ivertowski");
        sb.AppendLine("// Licensed under the MIT License. See LICENSE file in the project root for license information.");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Buffers.Binary;");
        sb.AppendLine();

        // Namespace
        sb.AppendLine($"namespace {messageType.Namespace};");
        sb.AppendLine();

        // Partial class
        sb.AppendLine($"public partial class {messageType.TypeName}");
        sb.AppendLine("{");

        // MessageType constant
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Gets the message type identifier.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public string MessageType => \"{messageType.TypeName}\";");
        sb.AppendLine();

        // PayloadSize calculation
        var baseSize = 16 + 1 + 17; // MessageId (16) + Priority (1) + CorrelationId (16 + 1 null marker)
        var memberSize = messageType.Members.Sum(m => m.Size);
        var totalSize = baseSize + memberSize;

        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Gets the message payload size in bytes.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public int PayloadSize => {totalSize};");
        sb.AppendLine();

        // Serialize method
        GenerateSerializeMethod(sb, messageType, totalSize);
        sb.AppendLine();

        // Deserialize method
        GenerateDeserializeMethod(sb, messageType);

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateSerializeMethod(StringBuilder sb, MessageTypeInfo messageType, int totalSize)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Serializes the message to a byte span.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public System.ReadOnlySpan<byte> Serialize()");
        sb.AppendLine("    {");
        sb.AppendLine($"        var buffer = new byte[{totalSize}];");
        sb.AppendLine("        var span = buffer.AsSpan();");
        sb.AppendLine("        int offset = 0;");
        sb.AppendLine();

        // MessageId (16 bytes)
        sb.AppendLine("        // MessageId (Guid, 16 bytes)");
        sb.AppendLine("        MessageId.TryWriteBytes(span.Slice(offset, 16));");
        sb.AppendLine("        offset += 16;");
        sb.AppendLine();

        // Priority (1 byte)
        sb.AppendLine("        // Priority (byte, 1 byte)");
        sb.AppendLine("        span[offset] = Priority;");
        sb.AppendLine("        offset += 1;");
        sb.AppendLine();

        // CorrelationId (nullable Guid, 17 bytes: 1 null marker + 16 bytes)
        sb.AppendLine("        // CorrelationId (Guid?, 17 bytes)");
        sb.AppendLine("        if (CorrelationId.HasValue)");
        sb.AppendLine("        {");
        sb.AppendLine("            span[offset] = 1; // Has value marker");
        sb.AppendLine("            offset += 1;");
        sb.AppendLine("            CorrelationId.Value.TryWriteBytes(span.Slice(offset, 16));");
        sb.AppendLine("            offset += 16;");
        sb.AppendLine("        }");
        sb.AppendLine("        else");
        sb.AppendLine("        {");
        sb.AppendLine("            span[offset] = 0; // Null marker");
        sb.AppendLine("            offset += 17;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Serialize custom members
        foreach (var member in messageType.Members)
        {
            sb.AppendLine($"        // {member.Name} ({member.TypeName}, {member.Size} bytes)");

            var writeMethod = member.TypeName switch
            {
                "System.Int32" => "BinaryPrimitives.WriteInt32LittleEndian",
                "System.Int64" => "BinaryPrimitives.WriteInt64LittleEndian",
                "System.Single" => "BinaryPrimitives.WriteSingleLittleEndian",
                "System.Double" => "BinaryPrimitives.WriteDoubleLittleEndian",
                "System.Int16" => "BinaryPrimitives.WriteInt16LittleEndian",
                "System.UInt32" => "BinaryPrimitives.WriteUInt32LittleEndian",
                "System.UInt64" => "BinaryPrimitives.WriteUInt64LittleEndian",
                "System.Byte" => null,
                "System.Boolean" => null,
                "System.Guid" => null,
                _ => null
            };

            if (writeMethod is not null)
            {
                sb.AppendLine($"        {writeMethod}(span.Slice(offset, {member.Size}), {member.Name});");
                sb.AppendLine($"        offset += {member.Size};");
            }
            else if (member.TypeName == "System.Byte" || member.TypeName == "System.Boolean")
            {
                sb.AppendLine($"        span[offset] = {(member.TypeName == "System.Boolean" ? $"(byte)({member.Name} ? 1 : 0)" : member.Name)};");
                sb.AppendLine("        offset += 1;");
            }
            else if (member.TypeName == "System.Guid")
            {
                sb.AppendLine($"        {member.Name}.TryWriteBytes(span.Slice(offset, 16));");
                sb.AppendLine("        offset += 16;");
            }

            sb.AppendLine();
        }

        sb.AppendLine("        return buffer;");
        sb.AppendLine("    }");
    }

    private static void GenerateDeserializeMethod(StringBuilder sb, MessageTypeInfo messageType)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Deserializes the message from a byte span.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public void Deserialize(System.ReadOnlySpan<byte> data)");
        sb.AppendLine("    {");
        sb.AppendLine("        int offset = 0;");
        sb.AppendLine();

        // MessageId (16 bytes)
        sb.AppendLine("        // MessageId (Guid, 16 bytes)");
        sb.AppendLine("        MessageId = new Guid(data.Slice(offset, 16));");
        sb.AppendLine("        offset += 16;");
        sb.AppendLine();

        // Priority (1 byte)
        sb.AppendLine("        // Priority (byte, 1 byte)");
        sb.AppendLine("        Priority = data[offset];");
        sb.AppendLine("        offset += 1;");
        sb.AppendLine();

        // CorrelationId (nullable Guid, 17 bytes)
        sb.AppendLine("        // CorrelationId (Guid?, 17 bytes)");
        sb.AppendLine("        if (data[offset] == 1)");
        sb.AppendLine("        {");
        sb.AppendLine("            offset += 1;");
        sb.AppendLine("            CorrelationId = new Guid(data.Slice(offset, 16));");
        sb.AppendLine("            offset += 16;");
        sb.AppendLine("        }");
        sb.AppendLine("        else");
        sb.AppendLine("        {");
        sb.AppendLine("            CorrelationId = null;");
        sb.AppendLine("            offset += 17;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Deserialize custom members
        foreach (var member in messageType.Members)
        {
            sb.AppendLine($"        // {member.Name} ({member.TypeName}, {member.Size} bytes)");

            var readMethod = member.TypeName switch
            {
                "System.Int32" => "BinaryPrimitives.ReadInt32LittleEndian",
                "System.Int64" => "BinaryPrimitives.ReadInt64LittleEndian",
                "System.Single" => "BinaryPrimitives.ReadSingleLittleEndian",
                "System.Double" => "BinaryPrimitives.ReadDoubleLittleEndian",
                "System.Int16" => "BinaryPrimitives.ReadInt16LittleEndian",
                "System.UInt32" => "BinaryPrimitives.ReadUInt32LittleEndian",
                "System.UInt64" => "BinaryPrimitives.ReadUInt64LittleEndian",
                "System.Byte" => null,
                "System.Boolean" => null,
                "System.Guid" => null,
                _ => null
            };

            if (readMethod is not null)
            {
                sb.AppendLine($"        {member.Name} = {readMethod}(data.Slice(offset, {member.Size}));");
                sb.AppendLine($"        offset += {member.Size};");
            }
            else if (member.TypeName == "System.Byte")
            {
                sb.AppendLine($"        {member.Name} = data[offset];");
                sb.AppendLine("        offset += 1;");
            }
            else if (member.TypeName == "System.Boolean")
            {
                sb.AppendLine($"        {member.Name} = data[offset] != 0;");
                sb.AppendLine("        offset += 1;");
            }
            else if (member.TypeName == "System.Guid")
            {
                sb.AppendLine($"        {member.Name} = new Guid(data.Slice(offset, 16));");
                sb.AppendLine("        offset += 16;");
            }

            sb.AppendLine();
        }

        sb.AppendLine("    }");
    }

    private readonly struct MessageTypeInfo
    {
        public string TypeName { get; }
        public string Namespace { get; }
        public ImmutableArray<MessageMemberInfo> Members { get; }
        public bool IsPartial { get; }

        public MessageTypeInfo(string typeName, string @namespace, ImmutableArray<MessageMemberInfo> members, bool isPartial)
        {
            TypeName = typeName;
            Namespace = @namespace;
            Members = members;
            IsPartial = isPartial;
        }
    }

    private readonly struct MessageMemberInfo
    {
        public string Name { get; }
        public string TypeName { get; }
        public int Size { get; }

        public MessageMemberInfo(string name, string typeName, int size)
        {
            Name = name;
            TypeName = typeName;
            Size = size;
        }
    }
}
