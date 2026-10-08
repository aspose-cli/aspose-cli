using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// Decision D8, input loading: the SDK owns load results and their error translation. It decides
/// between a required and an invalid password, turns I/O failures into locked, not-found or
/// access errors, and recognizes the container signatures (OLE compound file, PDF header) once. A
/// product supplies only the engine call and a classifier of its engine's exceptions. These rules
/// read source syntax only, so they hold whatever the SDK names its loading types.
/// </summary>
public sealed class InputLoadingOwnershipTests
{
    /// <summary>The exceptions an operating-system file access raises.</summary>
    private static readonly HashSet<string> IoExceptions = new(StringComparer.Ordinal)
    {
        nameof(IOException), nameof(FileNotFoundException), nameof(DirectoryNotFoundException),
        nameof(PathTooLongException), nameof(DriveNotFoundException), nameof(UnauthorizedAccessException),
    };

    /// <summary>The SDK errors that only the classification of an I/O failure produces.</summary>
    private static readonly string[] IoErrors = ["FileLocked", "FileAccessDenied"];

    private static readonly byte[] OleSignature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private static readonly byte[] PdfHeader = "%PDF"u8.ToArray();
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];

    [Fact]
    public void D8_Products_NeverChooseBetweenARequiredAndAnInvalidPassword()
    {
        IEnumerable<string> violations = OwnershipSourceFiles.Products.SelectMany(file => file.Root.DescendantNodes()
            .OfType<SimpleNameSyntax>()
            .Where(static name => name.Identifier.ValueText == "PasswordRequired")
            .Select(static name => OwnershipSourceFiles.EnclosingMember(name))
            .OfType<SyntaxNode>()
            .Distinct()
            .Where(static member => member.DescendantNodes().OfType<SimpleNameSyntax>()
                .Any(static name => name.Identifier.ValueText == "PasswordInvalid"))
            .Select(member => file.At(member, MemberName(member))));

        OwnershipSourceFiles.AssertNone(violations,
            "Whether a password is required or invalid is decided once, by the SDK's loader; no product member builds both errors:");
    }

    [Fact]
    public void D8_Products_NeverTranslateAnIoFailure()
    {
        IEnumerable<string> violations = OwnershipSourceFiles.Products.SelectMany(file => file.Root.DescendantNodes()
            .Where(static node => node switch
            {
                // catch (IOException) { throw ...; } or catch (Exception e) when (e is IOException) { throw ...; }
                CatchClauseSyntax clause => CatchesIo(clause) && Translates(clause.Block),
                // exception switch { IOException => error, ... }
                SwitchExpressionArmSyntax arm => NamesIo(arm.Pattern),
                CasePatternSwitchLabelSyntax label => NamesIo(label.Pattern),
                // if (exception is IOException) throw ...; inside a catch block
                IsPatternExpressionSyntax test => NamesIo(test.Pattern) && InsideTranslatingCatchBlock(test),
                BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression } test => NamesIo(test.Right) && InsideTranslatingCatchBlock(test),
                _ => false,
            })
            .Select(node => file.At(node, FirstLine(node))));

        OwnershipSourceFiles.AssertNone(violations,
            "A product never catches or classifies an I/O exception to raise an error of its own; the SDK turns I/O failures into "
            + "FILE_NOT_FOUND, FILE_LOCKED or FILE_ACCESS_DENIED:");
    }

    [Fact]
    public void D8_Products_NeverBuildAnIoError()
    {
        IEnumerable<string> violations = OwnershipSourceFiles.Products.SelectMany(file => file.Root.DescendantNodes()
            .OfType<SimpleNameSyntax>()
            .Where(static name => IoErrors.Contains(name.Identifier.ValueText, StringComparer.Ordinal))
            .Select(name => file.At(name, (name.Parent as MemberAccessExpressionSyntax)?.ToString() ?? name.ToString())));

        OwnershipSourceFiles.AssertNone(violations,
            "FILE_LOCKED and FILE_ACCESS_DENIED come only from the SDK's classification of an I/O failure; no product builds them:");
    }

    [Fact]
    public void D8_Products_NeverReadAPasswordProblemFromAMessage()
    {
        IEnumerable<string> violations = OwnershipSourceFiles.Products.SelectMany(file => file.Root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(static invocation => OwnershipSourceFiles.InvokedName(invocation)
                is "Contains" or "IndexOf" or "StartsWith" or "EndsWith" or "Equals" or "IsMatch" or "Match")
            .Where(static invocation => invocation.ArgumentList.Arguments.Any(static argument =>
                argument.Expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } literal
                && literal.Token.ValueText.Contains("password", StringComparison.OrdinalIgnoreCase)))
            .Select(invocation => file.At(invocation, invocation.ToString())));

        OwnershipSourceFiles.AssertNone(violations,
            "A product classifies its engine's password failures by exception type, never by searching a message for \"password\":");
    }

    [Fact]
    public void D8_TheOleSignature_IsCheckedOnlyInTheSdk() =>
        AssertOneSdkOwner("The OLE compound file signature (D0 CF 11 E0 A1 B1 1A E1)", OleSignature, [0xE11AB1A1E011CFD0]);

    [Fact]
    public void D8_ThePdfHeader_IsCheckedOnlyInTheSdk() =>
        AssertOneSdkOwner("The PDF header (%PDF)", PdfHeader, [0x46445025]);

    [Fact]
    public void D8_TheZipSignature_IsCheckedOnlyInTheSdk() =>
        AssertOneSdkOwner("The ZIP local file header signature (PK 03 04)", ZipSignature, [0x04034B50]);

    // -- Support ------------------------------------------------------------------------------

    /// <summary>The files that spell a signature: as bytes, as text or as an integer read of its first bytes.</summary>
    private static void AssertOneSdkOwner(string what, byte[] signature, ulong[] integers)
    {
        string text = new([.. signature.Select(static value => (char)value)]);
        // The first four bytes identify the signature; a check need not spell all of them.
        byte[] head = signature[..4];
        string[] owners =
        [
            .. OwnershipSourceFiles.All
                .Where(file => file.Root.DescendantTokens().Any(token => SpellsText(token, text[..4]))
                    || file.Root.DescendantNodes().Any(node => SpellsBytes(node, head))
                    || file.Root.DescendantTokens().Any(token => token.Value is int or uint or long or ulong
                        && integers.Contains(Convert.ToUInt64(token.Value, System.Globalization.CultureInfo.InvariantCulture))))
                .Select(static file => (file.IsUnder(OwnershipSourceFiles.SdkRoot) ? "SDK " : "    ") + file.Relative),
        ];

        Assert.True(owners.Length == 1 && owners[0].StartsWith("SDK ", StringComparison.Ordinal),
            $"{what} is checked once, in the SDK; it is checked in:" + Environment.NewLine + string.Join(Environment.NewLine, owners));
    }

    private static bool SpellsText(SyntaxToken token, string text) =>
        (token.IsKind(SyntaxKind.StringLiteralToken)
            || token.IsKind(SyntaxKind.Utf8StringLiteralToken)
            || token.IsKind(SyntaxKind.InterpolatedStringTextToken)
            || token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken)
            || token.IsKind(SyntaxKind.Utf8SingleLineRawStringLiteralToken))
        && token.ValueText.Contains(text, StringComparison.Ordinal);

    /// <summary>A list of expressions, such as an array or collection initializer or an argument list, that holds the bytes in order.</summary>
    private static bool SpellsBytes(SyntaxNode node, byte[] bytes)
    {
        IEnumerable<ExpressionSyntax>? items = node switch
        {
            InitializerExpressionSyntax initializer => initializer.Expressions,
            CollectionExpressionSyntax collection => collection.Elements.OfType<ExpressionElementSyntax>().Select(static element => element.Expression),
            ArgumentListSyntax arguments => arguments.Arguments.Select(static argument => argument.Expression),
            _ => null,
        };
        if (items is null)
        {
            return false;
        }
        long?[] values = [.. items.Select(static item => item is LiteralExpressionSyntax { Token.Value: int or uint or long or byte } literal
            ? Convert.ToInt64(literal.Token.Value, System.Globalization.CultureInfo.InvariantCulture)
            : (long?)null)];
        for (int start = 0; start + bytes.Length <= values.Length; start++)
        {
            if (bytes.Select((value, index) => values[start + index] == value).All(static match => match))
            {
                return true;
            }
        }
        return false;
    }

    private static bool CatchesIo(CatchClauseSyntax clause) =>
        (clause.Declaration?.Type is { } type && NamesIo(type))
        || (clause.Filter is { } filter && NamesIo(filter.FilterExpression));

    /// <summary>Whether a catch block raises an error of its own; a bare <c>throw;</c> only rethrows.</summary>
    private static bool Translates(BlockSyntax block) =>
        block.DescendantNodes().Any(static node => node is ThrowStatementSyntax { Expression: not null } or ThrowExpressionSyntax);

    private static bool InsideTranslatingCatchBlock(SyntaxNode node) =>
        node.Ancestors().OfType<CatchClauseSyntax>().FirstOrDefault() is { } clause
        && clause.Block.Contains(node)
        && Translates(clause.Block);

    private static bool NamesIo(SyntaxNode node) =>
        node.DescendantNodesAndSelf().OfType<SimpleNameSyntax>().Any(static name => IoExceptions.Contains(name.Identifier.ValueText));

    private static string MemberName(SyntaxNode member) => member switch
    {
        MethodDeclarationSyntax method => method.Identifier.ValueText,
        ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
        LocalFunctionStatementSyntax local => local.Identifier.ValueText,
        PropertyDeclarationSyntax property => property.Identifier.ValueText,
        _ => FirstLine(member),
    };

    private static string FirstLine(SyntaxNode node) => node.ToString().Split('\n')[0].Trim();
}
