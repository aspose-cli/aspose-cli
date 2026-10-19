using Aspose.Cli.Sdk.IO;
using Aspose.Slides;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>
/// Carries out an <see cref="OutputProtection"/> on a presentation about to be saved. Aspose.Slides
/// keeps the encryption a presentation opened with until it is removed, so keeping the input's
/// password takes nothing, and an output without one must remove it.
/// </summary>
internal static class SlidesOutputProtection
{
    internal static void Protect(Presentation presentation, OutputProtection protection)
    {
        switch (protection)
        {
            case OutputProtection.Replace replace:
                presentation.ProtectionManager.Encrypt(replace.NewPassword.Reveal());
                break;
            case OutputProtection.Keep:
                break;
            default:
                if (presentation.ProtectionManager.IsEncrypted)
                {
                    presentation.ProtectionManager.RemoveEncryption();
                }

                break;
        }
    }
}
