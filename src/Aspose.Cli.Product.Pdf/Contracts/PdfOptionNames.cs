namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>
/// The names of the PDF options that the engine names in its errors, declared once for the
/// commands that define the options and the engine that reports on their values.
/// </summary>
public static class PdfOptionNames
{
    /// <summary>The images a new PDF is made of.</summary>
    public const string FromImages = "--from-images";

    /// <summary>The opt-in to the network resources of an HTML input.</summary>
    public const string AllowNetworkResources = "--allow-network-resources";

    /// <summary>The page margins of a new PDF.</summary>
    public const string Margins = "--margins";

    /// <summary>The coordinate grid of a rendered page.</summary>
    public const string Grid = "--grid";

    /// <summary>The rectangle of a visible signature.</summary>
    public const string Rect = "--rect";

    /// <summary>The signing reason.</summary>
    public const string Reason = "--reason";

    /// <summary>The signing location.</summary>
    public const string Location = "--location";

    /// <summary>The signer's contact.</summary>
    public const string Contact = "--contact";

    /// <summary>The prefix of the options that give the signing certificate's password.</summary>
    public const string CertificatePasswordPrefix = "--certificate-password";

    /// <summary>The option that names the environment variable holding the signing certificate's password.</summary>
    public const string CertificatePasswordEnvironment = CertificatePasswordPrefix + "-env";
}
