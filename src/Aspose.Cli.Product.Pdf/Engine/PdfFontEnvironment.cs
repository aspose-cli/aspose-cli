using Aspose.Cli.Sdk.Rendering;
using Aspose.Pdf.Text;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Aspose.PDF font diagnostics behind the product-neutral <see cref="IFontEnvironment"/>.</summary>
internal sealed class PdfFontEnvironment : IFontEnvironment
{
    /// <inheritdoc />
    public EngineFonts ListFonts() => new()
    {
        Sources = FontRepository.Sources.Select(ToContract).ToArray(),
    };

    /// <inheritdoc />
    /// <remarks>Aspose.PDF resolves fonts only through its global repository.</remarks>
    public IDisposable UseFonts(FontSearchProfile profile) =>
        FontScope.Enter(profile, static directories =>
        {
            Aspose.Pdf.Text.FontSource[] ambient = FontRepository.Sources.ToArray();
            foreach (string directory in directories)
            {
                FontRepository.Sources.Add(new FolderFontSource(directory));
            }
            return () =>
            {
                FontRepository.Sources.Clear();
                foreach (Aspose.Pdf.Text.FontSource source in ambient)
                {
                    FontRepository.Sources.Add(source);
                }
            };
        });

    private static Aspose.Cli.Sdk.Contracts.FontSource ToContract(Aspose.Pdf.Text.FontSource source) => source switch
    {
        FolderFontSource folder => new Aspose.Cli.Sdk.Contracts.FontSource { Kind = "folder", Location = folder.FolderPath },
        SystemFontSource => new Aspose.Cli.Sdk.Contracts.FontSource { Kind = "system" },
        _ => new Aspose.Cli.Sdk.Contracts.FontSource { Kind = "other" },
    };
}
