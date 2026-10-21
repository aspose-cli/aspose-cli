using System.Net;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.IO.Input;

/// <summary>What the document engines of an invocation may reach through the process's network stack.</summary>
public enum NetworkAccess
{
    /// <summary>Nothing: every http, https, ftp and file request is blocked.</summary>
    None,

    /// <summary>http and https, for trusted input the caller opted in for; ftp and file stay blocked.</summary>
    Web,
}

/// <summary>
/// The process's one exit to the network. Document engines request the resources a document
/// names through <see cref="WebRequest"/> and <see cref="HttpClient"/>, some before or without
/// any resource hook the CLI can install (known issue PDF-HTML-EGRESS in KNOWN-ISSUES.md), so
/// no check of the content can tell which requests an engine will make. Instead the host admits
/// every invocation here before its command runs: the gate takes over the <c>http</c>,
/// <c>https</c>, <c>ftp</c> and <c>file</c> request prefixes and the default proxy of
/// <see cref="HttpClient"/> and <see cref="WebRequest"/>, so a request the invocation's access
/// does not allow fails before it leaves the process, and the gate lists its address.
/// </summary>
public static class NetworkGate
{
    private const int ListedAddresses = 20;

    // A proxy no connection can reach: Windows refuses a connection to the unspecified address
    // at once, before any packet is sent, so a blocked HttpClient request fails immediately.
    private static readonly Uri Refusing = new("http://0.0.0.0:9/");
    private static readonly object Sync = new();
    private static readonly List<string> Blocked = [];
    private static readonly HashSet<string> Permitted = new(FilePaths.Comparer);
    private static readonly List<string> Withheld = [];
    private static NetworkAccess _access;
    private static IWebProxy? _system;
    private static bool _installed;

    /// <summary>
    /// Admits an invocation: from now on the process reaches what <paramref name="access"/>
    /// allows, and the gate lists the addresses it blocks for this invocation alone.
    /// </summary>
    /// <param name="access">What the invocation's engines may reach.</param>
    public static void Admit(NetworkAccess access)
    {
        if (!Enum.IsDefined(access))
        {
            throw new ArgumentOutOfRangeException(nameof(access));
        }

        lock (Sync)
        {
            if (!_installed)
            {
                Install();
            }

            _access = access;
            Blocked.Clear();
            Permitted.Clear();
            Withheld.Clear();
        }
    }

    /// <summary>
    /// Permits the invocation's engine to read a local file that one of its resource loaders
    /// verified beneath the input directory, through a <c>file</c> request too; an earlier
    /// blocked request for the file is no longer reported as omitted.
    /// </summary>
    /// <param name="path">The verified file.</param>
    public static void Permit(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        lock (Sync)
        {
            Permitted.Add(Path.GetFullPath(path));
        }
    }

    /// <summary>
    /// Lists references the CLI removed from content before the invocation's engine read it, such
    /// as those of an SVG image (<see cref="SvgReferences"/>), beside the addresses the gate blocks.
    /// </summary>
    /// <param name="references">The removed references.</param>
    public static void Withhold(IEnumerable<string> references)
    {
        ArgumentNullException.ThrowIfNull(references);
        lock (Sync)
        {
            foreach (string reference in references)
            {
                string listed = reference.Length <= 120 ? reference : reference[..120] + "...";
                if (!Withheld.Contains(listed, StringComparer.Ordinal))
                {
                    Withheld.Add(listed);
                }
            }
        }
    }

    /// <summary>
    /// The completeness warning for the addresses the gate blocked since the invocation was
    /// admitted, apart from the files a resource loader permitted later, and for the references
    /// withheld from the engine, or null when there are none.
    /// </summary>
    public static Warning? Warning
    {
        get
        {
            lock (Sync)
            {
                string[] omitted = [.. Blocked.Where(static address => !Permitted.Contains(address)), .. Withheld];
                if (omitted.Length == 0)
                {
                    return null;
                }

                string listed = string.Join(", ", omitted.Take(ListedAddresses));
                string more = omitted.Length > ListedAddresses ? $", and {omitted.Length - ListedAddresses} more" : "";
                return new Warning(WarningCodes.RemoteResourcesBlocked,
                    $"{omitted.Length} resource(s) outside the input were blocked or removed before the document engine could read them: {listed}{more}.")
                {
                    // Every reference an SVG image names outside itself is removed, a relative path too.
                    Hint = Withheld.Count == 0
                        ? "Place required resources beside the input and reference them by relative path, then review the incomplete output."
                        : "Embed an SVG image's images as base64 PNG, JPEG or GIF data: URIs, place other resources beside the input and reference them by relative path, then review the incomplete output.",
                    AffectsCompleteness = true,
                };
            }
        }
    }

    private static void Install()
    {
        // Before any request reads them: WebRequest keeps the proxy it reads first.
        _system = HttpClient.DefaultProxy;
        var proxy = new GateProxy();
        HttpClient.DefaultProxy = proxy;
#pragma warning disable SYSLIB0014 // Engines still create requests through WebRequest; the gate must own its prefixes.
        WebRequest.DefaultWebProxy = proxy;
        foreach (string scheme in new[] { Uri.UriSchemeHttp, Uri.UriSchemeHttps, Uri.UriSchemeFtp, Uri.UriSchemeFile })
        {
            // A whole-address prefix is longer than the built-in scheme prefix, so it wins for
            // WebRequest.Create; WebRequest.CreateDefault still reaches the built-in creator,
            // whose HTTP requests go through the gate's proxy.
            if (!WebRequest.RegisterPrefix(scheme + "://", new GateCreator(scheme)))
            {
                throw new InvalidOperationException($"The network gate could not take over the {scheme} request prefix.");
            }
        }
#pragma warning restore SYSLIB0014
        _installed = true;
    }

    private static bool Allows(string scheme) =>
        _access == NetworkAccess.Web && (scheme == Uri.UriSchemeHttp || scheme == Uri.UriSchemeHttps);

    // Whether the request may pass; a blocked address is listed once.
    private static bool Pass(Uri address)
    {
        lock (Sync)
        {
            string listed = Listed(address);
            if (Allows(address.Scheme) || (address.IsFile && Permitted.Contains(listed)))
            {
                return true;
            }

            if (!Blocked.Contains(listed, StringComparer.Ordinal))
            {
                Blocked.Add(listed);
            }

            return false;
        }
    }

    // A file by its full path, as loaders permit it; any other address without its query.
    private static string Listed(Uri address)
    {
        if (!address.IsFile)
        {
            return address.GetLeftPart(UriPartial.Path);
        }

        try
        {
            return Path.GetFullPath(address.LocalPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return address.LocalPath;
        }
    }

    private static WebException Refusal(Uri address) => new(
        $"The CLI blocked the request for '{address}': documents reach no network or file address unless the caller opts in.",
        WebExceptionStatus.ConnectFailure);

    private sealed class GateCreator(string scheme) : IWebRequestCreate
    {
        public WebRequest Create(Uri uri)
        {
            ArgumentNullException.ThrowIfNull(uri);
            bool web = scheme == Uri.UriSchemeHttp || scheme == Uri.UriSchemeHttps;
            if (Pass(uri))
            {
                return Default(uri);
            }

            // Engines expect a blocked request to fail when it is sent, as an unreachable one
            // would, and cast an HTTP request to HttpWebRequest: it is sent to the refusing proxy.
            if (web)
            {
                WebRequest request = Default(uri);
                request.Proxy = new WebProxy(Refusing);
                return request;
            }

            return new BlockedRequest(uri);
        }

        // The scheme-only lookup reaches the built-in creator, not this one.
#pragma warning disable SYSLIB0014
        private static WebRequest Default(Uri uri) => WebRequest.CreateDefault(uri);
#pragma warning restore SYSLIB0014
    }

#pragma warning disable SYSLIB0014 // WebRequest's constructor; the request is never sent.
    private sealed class BlockedRequest(Uri uri) : WebRequest
#pragma warning restore SYSLIB0014
    {
        public override Uri RequestUri => uri;

        public override string Method { get; set; } = "GET";

        public override WebHeaderCollection Headers { get; set; } = [];

        public override string? ContentType { get; set; }

        public override long ContentLength { get; set; }

        public override int Timeout { get; set; } = 100_000;

        public override ICredentials? Credentials { get; set; }

        public override IWebProxy? Proxy { get; set; }

        public override bool PreAuthenticate { get; set; }

        public override bool UseDefaultCredentials { get; set; }

        public override string? ConnectionGroupName { get; set; }

        public override WebResponse GetResponse() => throw Refusal(uri);

        public override Task<WebResponse> GetResponseAsync() => Task.FromException<WebResponse>(Refusal(uri));

        public override IAsyncResult BeginGetResponse(AsyncCallback? callback, object? state) => throw Refusal(uri);

        public override WebResponse EndGetResponse(IAsyncResult asyncResult) => throw Refusal(uri);

        public override Stream GetRequestStream() => throw Refusal(uri);

        public override Task<Stream> GetRequestStreamAsync() => Task.FromException<Stream>(Refusal(uri));

        public override IAsyncResult BeginGetRequestStream(AsyncCallback? callback, object? state) => throw Refusal(uri);

        public override Stream EndGetRequestStream(IAsyncResult asyncResult) => throw Refusal(uri);

        public override void Abort()
        {
        }
    }

    private sealed class GateProxy : IWebProxy
    {
        public ICredentials? Credentials
        {
            get => _system?.Credentials;
            set
            {
                if (_system is not null)
                {
                    _system.Credentials = value;
                }
            }
        }

        public Uri? GetProxy(Uri destination)
        {
            ArgumentNullException.ThrowIfNull(destination);
            return Pass(destination) ? _system?.GetProxy(destination) : Refusing;
        }

        public bool IsBypassed(Uri host)
        {
            ArgumentNullException.ThrowIfNull(host);
            bool allowed;
            lock (Sync)
            {
                allowed = Allows(host.Scheme);
            }

            return allowed && (_system?.IsBypassed(host) ?? true);
        }
    }
}
