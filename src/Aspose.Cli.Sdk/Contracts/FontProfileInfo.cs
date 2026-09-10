using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>Non-secret identity of the font search profile used for rendering.</summary>
public sealed record FontProfileInfo
{
    public required string Mode { get; init; }

    public required string Fingerprint { get; init; }

    public static FontProfileInfo From(FontSearchProfile? profile)
    {
        FontSearchProfile value = profile ?? FontSearchProfile.Ambient;
        return new FontProfileInfo
        {
            Mode = value.IsAmbient ? "ambient" : "explicit",
            Fingerprint = value.Fingerprint,
        };
    }
}
