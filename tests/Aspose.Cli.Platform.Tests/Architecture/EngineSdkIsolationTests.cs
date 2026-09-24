using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// Reads the compiled product assemblies: a document-SDK type may appear only in the owning
/// product's Engine namespace, in signatures and in method bodies alike. Compiler-generated
/// closures and state machines count as part of the type that declares them.
/// </summary>
public sealed class EngineSdkIsolationTests
{
    private static readonly IReadOnlyDictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(static field => (OpCode)field.GetValue(null)!)
        .ToDictionary(static code => code.Value);

    [Fact]
    public void DocumentSdkTypesAppearOnlyInTheOwningProductEngine()
    {
        string directory = Path.GetDirectoryName(CliRunner.ExecutablePath)!;
        string[] products = Directory.GetFiles(directory, "Aspose.Cli.Product.*.dll");
        Assert.NotEmpty(products);

        string[] violations = products.SelectMany(Violations).ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    private static IEnumerable<string> Violations(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        MetadataReader metadata = pe.GetMetadataReader();
        string root = metadata.GetString(metadata.GetAssemblyDefinition().Name);
        var scanner = new SdkReferenceScanner(metadata);
        var found = new List<string>();
        foreach (TypeDefinitionHandle handle in metadata.TypeDefinitions)
        {
            TypeDefinition type = metadata.GetTypeDefinition(handle);
            string owner = OwnerNamespace(metadata, type);
            if (owner == root + ".Engine" || owner.StartsWith(root + ".Engine.", StringComparison.Ordinal))
            {
                continue;
            }

            string name = $"{Path.GetFileName(path)}: {owner}.{metadata.GetString(type.Name)}";
            foreach (string sdkType in scanner.TypeUses(type)
                         .Concat(type.GetFields().SelectMany(field => scanner.FieldUses(metadata.GetFieldDefinition(field))))
                         .Concat(type.GetMethods().SelectMany(method => scanner.MethodUses(pe, metadata.GetMethodDefinition(method))))
                         .Distinct(StringComparer.Ordinal))
            {
                found.Add($"{name} uses {sdkType}");
            }
        }
        return found;
    }

    /// <summary>Nested types, including compiler-generated ones, belong to their outermost declaring type.</summary>
    private static string OwnerNamespace(MetadataReader metadata, TypeDefinition type)
    {
        while (type.GetDeclaringType() is { IsNil: false } declaring)
        {
            type = metadata.GetTypeDefinition(declaring);
        }
        return metadata.GetString(type.Namespace);
    }

    private sealed class SdkReferenceScanner(MetadataReader metadata) : ISignatureTypeProvider<string?, object?>
    {
        public IEnumerable<string> TypeUses(TypeDefinition type)
        {
            if (!type.BaseType.IsNil && Resolve(type.BaseType) is { } baseType)
            {
                yield return baseType;
            }
            foreach (InterfaceImplementationHandle handle in type.GetInterfaceImplementations())
            {
                if (Resolve(metadata.GetInterfaceImplementation(handle).Interface) is { } implemented)
                {
                    yield return implemented;
                }
            }
        }

        public IEnumerable<string> FieldUses(FieldDefinition field)
        {
            if (field.DecodeSignature(this, null) is { } fieldType)
            {
                yield return fieldType;
            }
        }

        public IEnumerable<string> MethodUses(PEReader pe, MethodDefinition method)
        {
            MethodSignature<string?> signature = method.DecodeSignature(this, null);
            foreach (string? type in signature.ParameterTypes.Prepend(signature.ReturnType))
            {
                if (type is not null)
                {
                    yield return type;
                }
            }
            if (method.RelativeVirtualAddress == 0)
            {
                yield break;
            }

            MethodBodyBlock body = pe.GetMethodBody(method.RelativeVirtualAddress);
            if (!body.LocalSignature.IsNil)
            {
                StandaloneSignature locals = metadata.GetStandaloneSignature(body.LocalSignature);
                foreach (string? local in locals.DecodeLocalSignature(this, null))
                {
                    if (local is not null)
                    {
                        yield return local;
                    }
                }
            }
            foreach (int token in Tokens(body.GetILReader()))
            {
                if (Resolve(MetadataTokens.EntityHandle(token)) is { } used)
                {
                    yield return used;
                }
            }
        }

        private static IEnumerable<int> Tokens(BlobReader il)
        {
            var tokens = new List<int>();
            while (il.RemainingBytes > 0)
            {
                short value = il.ReadByte();
                if (value == 0xFE)
                {
                    value = unchecked((short)(0xFE00 | il.ReadByte()));
                }
                OpCode code = OpCodesByValue[value];
                switch (code.OperandType)
                {
                    case OperandType.InlineField or OperandType.InlineMethod or OperandType.InlineTok or OperandType.InlineType:
                        tokens.Add(il.ReadInt32());
                        break;
                    case OperandType.InlineSwitch:
                        int targets = il.ReadInt32();
                        il.Offset += targets * 4;
                        break;
                    case OperandType.InlineI8 or OperandType.InlineR:
                        il.Offset += 8;
                        break;
                    case OperandType.InlineBrTarget or OperandType.InlineI or OperandType.InlineSig
                        or OperandType.InlineString or OperandType.ShortInlineR:
                        il.Offset += 4;
                        break;
                    case OperandType.InlineVar:
                        il.Offset += 2;
                        break;
                    case OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar:
                        il.Offset += 1;
                        break;
                }
            }
            return tokens;
        }

        private string? Resolve(EntityHandle handle) => handle.Kind switch
        {
            HandleKind.TypeReference => SdkTypeName((TypeReferenceHandle)handle),
            HandleKind.TypeSpecification => metadata.GetTypeSpecification((TypeSpecificationHandle)handle).DecodeSignature(this, null),
            HandleKind.MemberReference => MemberUse(metadata.GetMemberReference((MemberReferenceHandle)handle)),
            HandleKind.MethodSpecification => MethodSpecificationUse(metadata.GetMethodSpecification((MethodSpecificationHandle)handle)),
            _ => null,
        };

        private string? MemberUse(MemberReference member)
        {
            if (Resolve(member.Parent) is { } parent)
            {
                return parent;
            }
            return member.GetKind() == MemberReferenceKind.Field
                ? member.DecodeFieldSignature(this, null)
                : member.DecodeMethodSignature(this, null) is var signature
                    ? signature.ParameterTypes.Prepend(signature.ReturnType).FirstOrDefault(static type => type is not null)
                    : null;
        }

        private string? MethodSpecificationUse(MethodSpecification specification) =>
            Resolve(specification.Method)
            ?? specification.DecodeSignature(this, null).FirstOrDefault(static type => type is not null);

        private string? SdkTypeName(TypeReferenceHandle handle)
        {
            TypeReference reference = metadata.GetTypeReference(handle);
            EntityHandle scope = reference.ResolutionScope;
            while (scope.Kind == HandleKind.TypeReference)
            {
                scope = metadata.GetTypeReference((TypeReferenceHandle)scope).ResolutionScope;
            }
            if (scope.Kind != HandleKind.AssemblyReference)
            {
                return null;
            }
            string assembly = metadata.GetString(metadata.GetAssemblyReference((AssemblyReferenceHandle)scope).Name);
            return assembly.StartsWith("Aspose.", StringComparison.Ordinal)
                && !assembly.StartsWith("Aspose.Cli.", StringComparison.Ordinal)
                    ? $"{metadata.GetString(reference.Namespace)}.{metadata.GetString(reference.Name)} ({assembly})"
                    : null;
        }

        public string? GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) =>
            SdkTypeName(handle);

        public string? GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => null;

        public string? GetTypeFromSpecification(MetadataReader reader, object? context, TypeSpecificationHandle handle, byte rawTypeKind) =>
            reader.GetTypeSpecification(handle).DecodeSignature(this, context);

        public string? GetGenericInstantiation(string? genericType, ImmutableArray<string?> typeArguments) =>
            genericType ?? typeArguments.FirstOrDefault(static type => type is not null);

        public string? GetArrayType(string? elementType, ArrayShape shape) => elementType;
        public string? GetSZArrayType(string? elementType) => elementType;
        public string? GetByReferenceType(string? elementType) => elementType;
        public string? GetPointerType(string? elementType) => elementType;
        public string? GetPinnedType(string? elementType) => elementType;
        public string? GetModifiedType(string? modifier, string? unmodifiedType, bool isRequired) => unmodifiedType ?? modifier;
        public string? GetFunctionPointerType(MethodSignature<string?> signature) =>
            signature.ParameterTypes.Prepend(signature.ReturnType).FirstOrDefault(static type => type is not null);
        public string? GetGenericMethodParameter(object? context, int index) => null;
        public string? GetGenericTypeParameter(object? context, int index) => null;
        public string? GetPrimitiveType(PrimitiveTypeCode typeCode) => null;
    }
}
