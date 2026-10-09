using Aspose.Words;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

/// <summary>
/// Maps editing restrictions to the <c>protect.mode</c> vocabulary, so reads report the value
/// the protect operation accepts and <c>none</c> for an unrestricted document.
/// </summary>
internal static class WordsProtection
{
    public const string None = WordsProtectionModes.None;

    public static string ToMode(ProtectionType type) => type switch
    {
        ProtectionType.NoProtection => None,
        ProtectionType.ReadOnly => WordsProtectionModes.ReadOnly,
        ProtectionType.AllowOnlyComments => WordsProtectionModes.Comments,
        ProtectionType.AllowOnlyRevisions => WordsProtectionModes.TrackedChanges,
        ProtectionType.AllowOnlyFormFields => WordsProtectionModes.Forms,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unmapped protection type."),
    };

    public static ProtectionType FromMode(string mode) => mode switch
    {
        WordsProtectionModes.ReadOnly => ProtectionType.ReadOnly,
        WordsProtectionModes.Comments => ProtectionType.AllowOnlyComments,
        WordsProtectionModes.TrackedChanges => ProtectionType.AllowOnlyRevisions,
        WordsProtectionModes.Forms => ProtectionType.AllowOnlyFormFields,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown protection mode."),
    };
}
