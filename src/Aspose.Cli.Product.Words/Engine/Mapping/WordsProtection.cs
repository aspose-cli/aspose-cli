using Aspose.Words;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

/// <summary>
/// Maps editing restrictions to the <c>protect.mode</c> vocabulary, so reads report the value
/// the protect operation accepts and <c>none</c> for an unrestricted document.
/// </summary>
internal static class WordsProtection
{
    public const string None = "none";

    public static string ToMode(ProtectionType type) => type switch
    {
        ProtectionType.NoProtection => None,
        ProtectionType.ReadOnly => "readOnly",
        ProtectionType.AllowOnlyComments => "comments",
        ProtectionType.AllowOnlyRevisions => "trackedChanges",
        ProtectionType.AllowOnlyFormFields => "forms",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unmapped protection type."),
    };

    public static ProtectionType FromMode(string mode) => mode switch
    {
        "readOnly" => ProtectionType.ReadOnly,
        "comments" => ProtectionType.AllowOnlyComments,
        "trackedChanges" => ProtectionType.AllowOnlyRevisions,
        "forms" => ProtectionType.AllowOnlyFormFields,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown protection mode."),
    };
}
