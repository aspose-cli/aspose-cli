using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// One SDK type escapes a name for a target address (<c>TargetName.Escape</c>), so every
/// product writes the same address for the same name. A product never percent-encodes text
/// itself: it calls no URI or URL encoder, and no member of its formats a byte as hex
/// (<c>X2</c>) next to a percent sign.
/// </summary>
public sealed class TargetNameOwnershipTests
{
    private static readonly string[] Encoders = ["EscapeDataString", "EscapeUriString", "UrlEncode", "UrlPathEncode"];

    [Fact]
    public void Products_CallNoUriOrUrlEncoder()
    {
        IEnumerable<string> violations = OwnershipSourceFiles.Products.SelectMany(file => file.Root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(static invocation => Encoders.Contains(OwnershipSourceFiles.InvokedName(invocation), StringComparer.Ordinal))
            .Select(invocation => file.At(invocation, invocation.ToString())));

        OwnershipSourceFiles.AssertNone(violations,
            "A product escapes a name for an address with the SDK's TargetName.Escape, never with a URI or URL encoder:");
    }

    [Fact]
    public void Products_WriteNoPercentEscapeThemselves()
    {
        IEnumerable<string> violations = OwnershipSourceFiles.Products.SelectMany(file => file.Root.DescendantNodes()
            .Where(IsHexByteFormat)
            .Select(static node => (Node: node, Member: OwnershipSourceFiles.EnclosingMember(node)))
            .Where(static found => found.Member is not null && found.Member.DescendantTokens().Any(HoldsPercent))
            .Select(found => file.At(found.Node, found.Member!.ToString().Split('\n')[0].Trim())));

        OwnershipSourceFiles.AssertNone(violations,
            "A product escapes a name for an address with the SDK's TargetName.Escape, never writes %XX itself:");
    }

    /// <summary>A hex byte format: <c>{value:X2}</c> in an interpolation, or a <c>"X2"</c> format argument.</summary>
    private static bool IsHexByteFormat(SyntaxNode node) => node switch
    {
        InterpolationFormatClauseSyntax format => IsHexByte(format.FormatStringToken.ValueText),
        LiteralExpressionSyntax { Parent: ArgumentSyntax } literal when literal.IsKind(SyntaxKind.StringLiteralExpression) =>
            IsHexByte(literal.Token.ValueText),
        _ => false,
    };

    private static bool IsHexByte(string format) => string.Equals(format, "X2", StringComparison.OrdinalIgnoreCase);

    private static bool HoldsPercent(SyntaxToken token) =>
        (token.IsKind(SyntaxKind.CharacterLiteralToken)
            || token.IsKind(SyntaxKind.StringLiteralToken)
            || token.IsKind(SyntaxKind.InterpolatedStringTextToken))
        && token.ValueText.Contains('%', StringComparison.Ordinal);
}
