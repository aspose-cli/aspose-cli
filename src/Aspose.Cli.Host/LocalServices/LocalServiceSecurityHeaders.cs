using System.Net;

namespace Aspose.Cli.Host.LocalServices;

internal enum LocalServicePageKind
{
    AppShell,
    ProductPreview,
}

/// <summary>
/// One audited security-header baseline for every loopback HTTP surface.
/// A mounted product preview is frameable only by its same-origin App. The
/// standalone preview remains frameable by a loopback App origin, while the
/// App shell is never frameable.
/// </summary>
internal static class LocalServiceSecurityHeaders
{
    public static void Apply(
        HttpListenerResponse response,
        LocalServicePageKind kind,
        string? scriptNonce = null,
        bool sameOriginMount = false)
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
        response.Headers["Cross-Origin-Opener-Policy"] =
            "same-origin";
        response.Headers["Cross-Origin-Resource-Policy"] =
            app || sameOriginMount ? "same-origin" : "same-site";
        response.Headers["Permissions-Policy"] =
            "camera=(), microphone=(), geolocation=()";
        response.Headers["Content-Security-Policy"] = app
            ? "default-src 'self'; script-src 'self'; style-src 'self'; "
                + "img-src 'self' data:; frame-src http://127.0.0.1:*; "
                + "connect-src 'self'; base-uri 'none'; form-action 'none'; "
                + "frame-ancestors 'none'"
            : $"default-src 'self'; script-src 'nonce-{scriptNonce}'; "
                + "style-src 'self' 'unsafe-inline'; "
                + "img-src 'self' data:; connect-src 'self'; "
                + "base-uri 'none'; form-action 'none'; "
                + (sameOriginMount
                    ? "frame-ancestors 'self'"
                    : "frame-ancestors http://127.0.0.1:*");
        if (app)
        {
            response.Headers["X-Frame-Options"] = "DENY";
        }
    }
}
