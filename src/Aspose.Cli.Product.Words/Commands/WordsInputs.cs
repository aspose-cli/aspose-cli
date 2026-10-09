namespace Aspose.Cli.Product.Words.Commands;

/// <summary>The documents Words commands read and protect.</summary>
internal static class WordsInputs
{
    /// <summary>The document every reading command opens.</summary>
    public static readonly InputDocument Document = new("Document to open.", "the document");

    /// <summary>The password a writing command can put on its document.</summary>
    public static readonly EncryptedOutput EncryptedDocument = new("the output document");
}
