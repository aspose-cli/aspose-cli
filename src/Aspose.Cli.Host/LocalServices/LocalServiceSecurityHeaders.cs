using System.Net;

namespace Aspose.Cli.Host.LocalServices;

internal enum LocalServicePageKind
{
    AppShell,
    ProductPreview,
}

/// <summary>
/// One audited security-header baseline for every loopback HTTP surface. The
/// App and the documents it shows are served from one origin, so framing is
/// allowed within that origin and nowhere else: the App shell frames only
/// its own documents and is never framed itself, and a document page may be
/// framed only by a page of the same origin.
/// </summary>
internal static class LocalServiceSecurityHeaders
{
    public static void Apply(
        HttpListenerResponse response,
        LocalServicePageKind kind,
        string? scriptNonce = null)
    {
        ArgumentNullException.ThrowIfNull(response);
        bool app = kind == LocalServicePageKind.AppShell;
        if (!app)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scriptNonce);
        }

        response.Headers["Cache-Control"] = "no-store";
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers["Referrer-Policy"] = "no-referrer";
        response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
        response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
        response.Headers["Permissions-Policy"] =
            "camera=(), microphone=(), geolocation=()";
        response.Headers["Content-Security-Policy"] = app
            ? "default-src 'self'; script-src 'self'; style-src 'self'; "
                + "img-src 'self' data:; frame-src 'self'; "
                + "connect-src 'self'; object-src 'none'; base-uri 'none'; form-action 'none'; "
                + "frame-ancestors 'none'"
            : $"default-src 'self'; script-src 'nonce-{scriptNonce}'; "
                + "style-src 'self' 'unsafe-inline'; "
                + "img-src 'self' data:; connect-src 'self'; object-src 'none'; "
                + "base-uri 'none'; form-action 'none'; "
                + "frame-ancestors 'self'";
        response.Headers["X-Frame-Options"] = app ? "DENY" : "SAMEORIGIN";
    }
}
