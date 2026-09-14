using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Host.Preview;

/// <summary>Creates the authenticated marker and control lifetime for a background preview.</summary>
internal static class PreviewBackgroundService
{
    public static void ValidateIdentity(string? id, string? token)
    {
        if ((id is null) != (token is null)
            || (id is not null && !PreviewSessionStore.IsValidId(id)))
        {
            throw CliErrors.OptionInvalid(
                "preview service",
                "the internal background-session identity is incomplete or invalid",
                "Start background previews with 'aspose-cli preview <file>'.");
        }
    }

    public static PreviewServiceLifetime? Create(
        RunningPreview runtime,
        ProductDefinition product,
        string file,
        ProductPreviewPayload? selector,
        string? id,
        string? token,
        ResultEnvelopeMetadata metadata,
        string licenseIdentity)
    {
        ValidateIdentity(id, token);
        if (id is null || token is null)
        {
            return null;
        }

        string nonce = ServiceStartSecretChannel.Current?.ServiceNonce
            ?? throw CliErrors.OptionInvalid(
                "preview service",
                "the internal service nonce is missing",
                "Start background previews with 'aspose-cli preview <file>'.");
        LocalServiceProcessIdentity identity =
            LocalServiceProcessIdentity.Current(nonce);
        var marker = new PreviewSessionMarker(
            id,
            token,
            identity.Pid,
            identity.StartTicksUtc,
            runtime.Port,
            $"http://127.0.0.1:{runtime.Port}/",
            file,
            runtime.View,
            product.Manifest.Id,
            metadata,
            Nonce: nonce,
            Version: 1,
            Selector: selector,
            FontProfileFingerprint:
                ServiceStartSecretChannel.Current?.FontProfile?.Fingerprint,
            LicenseIdentity: licenseIdentity);
        return new PreviewServiceLifetime(
            new PreviewSessionStore(),
            marker,
            runtime.Session.Dispose,
            () => runtime.Session.InteractiveState);
    }
}
