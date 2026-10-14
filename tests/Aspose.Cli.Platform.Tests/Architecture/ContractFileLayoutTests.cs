using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// The layout of each product's <c>Contracts</c> folder, so adding a command touches only its own
/// files: one flat request file and one result file per command, named after their types; a
/// type one command uses lives in that command's file, a type several use lives in a file of its
/// own; and every contract type carries the product's name. Products and commands are found from
/// the source (every <c>Aspose.Cli.Product.*</c> project and every
/// <c>CommandDefinition&lt;TRequest, TResult&gt;</c> it writes), so a new product or command is
/// checked without editing this test. The rules read syntax only.
/// </summary>
/// <remarks>
/// <para>A <em>contract file</em> is a <c>.cs</c> file directly in the product's
/// <c>Contracts</c> folder, except the ops files (<c>&lt;Product&gt;Ops.cs</c> and
/// <c>&lt;Product&gt;Ops.*.cs</c>), whose layout follows the operation vocabulary rather than the
/// commands. Sub-folders (<c>Contracts/Addressing</c>, <c>Contracts/Reading</c>) hold parsing and
/// planning helpers in their own namespaces, not command contracts.</para>
/// <para>A <em>contract type</em> is a type a contract file declares that is part of the contract
/// surface: a request, a result, their nested records, an input record or a value-constant class.
/// Left out, each for a stated reason: private nested types (implementation detail of the type
/// holding them); attribute classes (schema annotations on fields, never a JSON shape); the
/// source-generated <c>JsonSerializerContext</c> (its name is fixed by the SDK's JSON
/// registration and is the same in every product); static classes that declare methods
/// (behavior helpers, such as an error factory or a value normalizer, not a shape or a value
/// list); and operation records (<c>…Op</c>, including the product's ops base), which belong to
/// the operation vocabulary and keep the operation's name.</para>
/// </remarks>
public sealed class ContractFileLayoutTests
{
    private const string ProductProjectPrefix = "Aspose.Cli.Product.";

    [Fact]
    public void Products_AreDiscovered_WithCommandsAndContracts()
    {
        Assert.NotEmpty(Products.Value);
        Assert.All(Products.Value, static product =>
        {
            Assert.NotEmpty(product.Commands);
            Assert.NotEmpty(product.ContractFiles);
        });
    }

    /// <summary>
    /// Rule 1: each command's request and result is declared, all of its parts, in the contract
    /// file named after it, and no file declares the request or result of two commands. A
    /// contract file is named after a contract type it declares, so no shared
    /// <c>…Requests.cs</c> or <c>…Results.cs</c> file holds the types of several commands.
    /// </summary>
    [Fact]
    public void X1_EachRequestAndResult_IsInTheFileNamedAfterIt()
    {
        IEnumerable<string> violations = Products.Value.SelectMany(static product =>
        {
            HashSet<string> commandTypes = product.CommandTypeNames;
            IEnumerable<string> misplaced = commandTypes.SelectMany(name =>
            {
                ContractDeclaration[] declarations = [.. product.ContractFiles
                    .SelectMany(static file => file.Declarations)
                    .Where(declaration => declaration.Name == name)];
                if (declarations.Length == 0)
                {
                    return [$"{product.Id}: {name} is not declared in a file directly under Contracts"];
                }

                return declarations
                    .Where(declaration => declaration.IsNested || declaration.File.Name != name)
                    .Select(declaration => declaration.File.Source.At(
                        declaration.Node,
                        $"{name} is declared here; declare it at the top level of Contracts/{name}.cs"));
            });
            IEnumerable<string> shared = product.ContractFiles
                .Select(file => (file, names: file.Declarations
                    .Select(static declaration => declaration.Name)
                    .Where(commandTypes.Contains)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray()))
                .Where(static entry => entry.names.Length > 1)
                .Select(static entry => $"{entry.file.Source.Relative}: declares the requests or results of several commands: "
                    + string.Join(", ", entry.names));
            IEnumerable<string> unnamed = product.ContractFiles
                .Where(static file => file.Declarations.Any(static declaration => declaration.IsContractType)
                    && !file.Declarations.Any(declaration => !declaration.IsNested
                        && declaration.IsContractType
                        && declaration.Name == file.Name))
                .Select(static file => $"{file.Source.Relative}: declares contract types but none named {file.Name}: "
                    + string.Join(", ", file.Declarations
                        .Where(static declaration => declaration.IsContractType)
                        .Select(static declaration => declaration.Name)));
            return misplaced.Concat(shared).Concat(unnamed);
        });

        OwnershipSourceFiles.AssertNone(violations,
            "X1: a command's request and result each live in their own flat file Contracts/<Type>.cs, and a contract file "
            + "is named after a type it declares:");
    }

    /// <summary>
    /// Rule 2: every contract type starts with its product's id (<c>Cells</c>, <c>Pdf</c>,
    /// <c>Slides</c>, <c>Words</c>), not a document noun (<c>Workbook</c>, <c>Presentation</c>,
    /// <c>Document</c>) or nothing.
    /// </summary>
    [Fact]
    public void X1_EachContractType_StartsWithItsProductName()
    {
        IEnumerable<string> violations = Products.Value.SelectMany(static product => product.ContractFiles
            .SelectMany(static file => file.Declarations)
            .Where(static declaration => declaration.IsContractType)
            .Where(declaration => !declaration.Name.StartsWith(product.Id, StringComparison.Ordinal))
            .Select(declaration => declaration.File.Source.At(
                declaration.Node,
                $"{declaration.Name} does not start with {product.Id}")));

        OwnershipSourceFiles.AssertNone(violations,
            "X1: contract types are named <Product>…; operation records, attributes, the JSON context and static helpers "
            + "are the only types without the prefix:");
    }

    /// <summary>
    /// Rule 3: a contract type that the files of two or more commands use (its request, result
    /// and command definition files), or one command and the operations, is declared at the top
    /// level of a file of its own, named after it, rather than inside one command's file.
    /// </summary>
    [Fact]
    public void X1_ATypeSeveralCommandsUse_HasAFileOfItsOwn()
    {
        IEnumerable<string> violations = Products.Value.SelectMany(static product => product.ContractFiles
            .SelectMany(static file => file.Declarations)
            .Where(declaration => declaration.IsContractType && !product.CommandTypeNames.Contains(declaration.Name))
            .Select(declaration => (declaration, users: product.UsersOf(declaration.Name)))
            .Where(static entry => entry.users.Count > 1
                && (entry.declaration.IsNested || entry.declaration.File.Name != entry.declaration.Name))
            .Select(static entry => entry.declaration.File.Source.At(
                entry.declaration.Node,
                $"{entry.declaration.Name} is used by {string.Join(", ", entry.users)}; declare it at the top level of "
                + $"Contracts/{entry.declaration.Name}.cs")));

        OwnershipSourceFiles.AssertNone(violations,
            "X1: a contract type used by more than one command (or by a command and the operations) lives in its own file:");
    }

    /// <summary>
    /// Rule 4: each command has one request and one result, <c>&lt;Product&gt;&lt;Verb&gt;Request</c>
    /// and <c>&lt;Product&gt;&lt;Verb&gt;Result</c>, where <c>&lt;Verb&gt;Command.cs</c> defines it;
    /// no two commands share a request or a result.
    /// </summary>
    [Fact]
    public void X1_EachCommand_HasOneRequestAndOneResultOfItsOwn()
    {
        IEnumerable<string> violations = Products.Value.SelectMany(static product =>
        {
            IEnumerable<string> shapes = product.Commands
                .GroupBy(static command => command.File.Path, StringComparer.OrdinalIgnoreCase)
                .SelectMany(group =>
                {
                    CommandDeclaration[] pairs = [.. group.DistinctBy(static command => (command.Request, command.Result))];
                    if (pairs.Length > 1)
                    {
                        return [$"{pairs[0].File.Relative}: defines several commands or request/result pairs: "
                            + string.Join(", ", pairs.Select(static pair => $"<{pair.Request}, {pair.Result}>"))];
                    }

                    CommandDeclaration command = pairs[0];
                    string fileName = Path.GetFileNameWithoutExtension(command.File.Path);
                    string verb = fileName.EndsWith("Command", StringComparison.Ordinal) ? fileName[..^"Command".Length] : fileName;
                    string request = product.Id + verb + "Request";
                    string result = product.Id + verb + "Result";
                    return command.Request == request && command.Result == result
                        ? []
                        : (IEnumerable<string>)[$"{command.File.Relative}: defines <{command.Request}, {command.Result}>; "
                            + $"the {verb} command's request and result are <{request}, {result}>"];
                });
            IEnumerable<string> shared = product.Commands
                .DistinctBy(static command => command.File.Path, StringComparer.OrdinalIgnoreCase)
                .SelectMany(static command => new[] { (type: command.Request, command), (type: command.Result, command) })
                .GroupBy(static entry => entry.type, StringComparer.Ordinal)
                .Where(static group => group.Select(static entry => entry.command.File.Path)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                .Select(static group => $"{group.Key} is shared by "
                    + string.Join(", ", group.Select(static entry => entry.command.File.Relative).Distinct()));
            return shapes.Concat(shared);
        });

        OwnershipSourceFiles.AssertNone(violations,
            "X1: one command, one request, one result, named after the command's verb:");
    }

    private static readonly Lazy<ProductContracts[]> Products = new(LoadProducts);

    private static ProductContracts[] LoadProducts() =>
        [.. OwnershipSourceFiles.Products
            .GroupBy(static file => file.Product!, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => ProductContracts.Create(group.Key, [.. group]))];

    private sealed class ProductContracts
    {
        private ProductContracts(
            string id,
            IReadOnlyList<CommandDeclaration> commands,
            IReadOnlyList<ContractFile> contractFiles,
            IReadOnlyList<OwnedSourceFile> opsFiles)
        {
            Id = id;
            Commands = commands;
            ContractFiles = contractFiles;
            OpsFiles = opsFiles;
            CommandTypeNames = new HashSet<string>(
                commands.SelectMany(static command => new[] { command.Request, command.Result }),
                StringComparer.Ordinal);
        }

        public string Id { get; }

        public IReadOnlyList<CommandDeclaration> Commands { get; }

        public IReadOnlyList<ContractFile> ContractFiles { get; }

        public IReadOnlyList<OwnedSourceFile> OpsFiles { get; }

        public HashSet<string> CommandTypeNames { get; }

        public static ProductContracts Create(string id, IReadOnlyList<OwnedSourceFile> files)
        {
            string contracts = Path.Combine(OwnershipSourceFiles.SourceRoot, ProductProjectPrefix + id, "Contracts");
            OwnedSourceFile[] flat = [.. files.Where(file => string.Equals(
                Path.GetDirectoryName(file.Path), contracts, StringComparison.OrdinalIgnoreCase))];
            bool IsOps(OwnedSourceFile file)
            {
                string name = Path.GetFileName(file.Path);
                return name.Equals(id + "Ops.cs", StringComparison.Ordinal)
                    || name.StartsWith(id + "Ops.", StringComparison.Ordinal);
            }

            CommandDeclaration[] commands = [.. files.SelectMany(static file => file.Root.DescendantNodes()
                .OfType<GenericNameSyntax>()
                .Where(static name => name.Identifier.ValueText == "CommandDefinition" && name.TypeArgumentList.Arguments.Count == 2)
                .Select(name => new CommandDeclaration(
                    file,
                    SimpleName(name.TypeArgumentList.Arguments[0]),
                    SimpleName(name.TypeArgumentList.Arguments[1]))))];
            return new ProductContracts(
                id,
                commands,
                [.. flat.Where(file => !IsOps(file)).Select(static file => new ContractFile(file))],
                [.. flat.Where(IsOps)]);
        }

        /// <summary>
        /// The commands (by verb) whose request, result or definition file names the type, and
        /// <c>ops</c> when an ops file names it.
        /// </summary>
        public IReadOnlyList<string> UsersOf(string typeName)
        {
            var users = new SortedSet<string>(StringComparer.Ordinal);
            foreach (CommandDeclaration command in Commands)
            {
                IEnumerable<SyntaxNode> roots = ContractFiles
                    .Where(file => file.Name == command.Request || file.Name == command.Result)
                    .Select(static file => file.Source.Root)
                    .Append(command.File.Root);
                if (roots.Any(root => Names(root, typeName)))
                {
                    users.Add(command.Request.EndsWith("Request", StringComparison.Ordinal)
                        ? command.Request[..^"Request".Length]
                        : command.Request);
                }
            }

            if (OpsFiles.Any(file => Names(file.Root, typeName)))
            {
                users.Add("ops");
            }

            return [.. users];
        }

        private static bool Names(SyntaxNode root, string typeName) => root.DescendantNodes()
            .OfType<SimpleNameSyntax>()
            .Any(name => name.Identifier.ValueText == typeName);

        private static string SimpleName(TypeSyntax type) => type switch
        {
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            NullableTypeSyntax nullable => SimpleName(nullable.ElementType),
            _ => type.ToString(),
        };
    }

    private sealed record CommandDeclaration(OwnedSourceFile File, string Request, string Result);

    private sealed class ContractFile
    {
        public ContractFile(OwnedSourceFile source)
        {
            Source = source;
            Name = Path.GetFileNameWithoutExtension(source.Path);
            Declarations = [.. source.Root.DescendantNodes()
                .OfType<BaseTypeDeclarationSyntax>()
                .Select(declaration => new ContractDeclaration(this, declaration))];
        }

        public OwnedSourceFile Source { get; }

        public string Name { get; }

        public IReadOnlyList<ContractDeclaration> Declarations { get; }
    }

    private sealed class ContractDeclaration(ContractFile file, BaseTypeDeclarationSyntax node)
    {
        public ContractFile File { get; } = file;

        public BaseTypeDeclarationSyntax Node { get; } = node;

        public string Name => Node.Identifier.ValueText;

        public bool IsNested => Node.Parent is BaseTypeDeclarationSyntax;

        /// <summary>Whether the type is part of the contract surface; see the class remarks for each exclusion.</summary>
        public bool IsContractType =>
            !IsPrivate
            && !IsAttribute
            && !IsJsonContext
            && !IsStaticHelper
            && !Name.EndsWith("Op", StringComparison.Ordinal);

        private bool IsPrivate => Node.AncestorsAndSelf()
            .OfType<BaseTypeDeclarationSyntax>()
            .Any(static type => type.Modifiers.Any(SyntaxKind.PrivateKeyword));

        private bool IsAttribute => BaseNames.Any(static name => name.EndsWith("Attribute", StringComparison.Ordinal));

        private bool IsJsonContext => BaseNames.Contains("JsonSerializerContext", StringComparer.Ordinal);

        private bool IsStaticHelper => Node is ClassDeclarationSyntax type
            && type.Modifiers.Any(SyntaxKind.StaticKeyword)
            && type.Members.OfType<MethodDeclarationSyntax>().Any();

        private IEnumerable<string> BaseNames => Node.BaseList?.Types
            .Select(static type => type.Type switch
            {
                QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
                SimpleNameSyntax simple => simple.Identifier.ValueText,
                _ => type.Type.ToString(),
            }) ?? [];
    }
}
