using System.Net;
using System.Text;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Host.Preview;

/// <summary>Writes cache-safe preview HTTP responses and security headers.</summary>
internal sealed class PreviewResponseWriter
{
    public void ApplySecurityHeaders(
        HttpListenerResponse response,
        string scriptNonce,
        bool sameOriginMount = false) =>
        LocalServiceSecurityHeaders.Apply(
            response,
            LocalServicePageKind.ProductPreview,
            scriptNonce,
            sameOriginMount);

    public void Redirect(
        HttpListenerResponse response,
        string location)
    {
        response.StatusCode = 302;
        response.RedirectLocation = location;
        response.Headers["Cache-Control"] = "no-store";
        response.Close();
    }

    public void Text(
        HttpListenerResponse response,
        int status,
        string contentType,
        string body) =>
        Bytes(
            response,
            status,
            contentType,
            Encoding.UTF8.GetBytes(body));

    public void Stream(
        HttpListenerResponse response,
        int status,
        string contentType,
        FileStream body)
    {
        response.StatusCode = status;
        response.ContentType = contentType;
        response.Headers["Cache-Control"] = "no-store";
        response.ContentLength64 = body.Length;
        byte[] buffer = new byte[64 * 1024];
        while (true)
        {
            int read = body.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                break;
            }

            response.OutputStream.Write(buffer, 0, read);
        }

        response.Close();
    }

    public static void Fail(HttpListenerResponse response)
    {
        try
        {
            response.StatusCode = 500;
            response.Close();
        }
        catch (Exception)
        {
        }
    }

    private static void Bytes(
        HttpListenerResponse response,
        int status,
        string contentType,
        byte[] body)
    {
        response.StatusCode = status;
        response.ContentType = contentType;
        response.Headers["Cache-Control"] = "no-store";
        response.ContentLength64 = body.Length;
        response.OutputStream.Write(body);
        response.Close();
    }
}
