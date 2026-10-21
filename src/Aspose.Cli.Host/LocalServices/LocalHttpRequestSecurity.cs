using System.Globalization;
using System.Net;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>
/// Who a local HTTP service answers. A request must come from loopback with
/// the exact Host of the bound address and carry the session cookie of this
/// service; a state change must also carry this service's exact Origin. The
/// only way to a session is a one-time entry ticket: the service mints one
/// for every address it returns over its control channel, which only the
/// current user can open, and the browser that presents the ticket receives
/// an HttpOnly, SameSite=Strict cookie and is sent on to the same address
/// without it. Knowing the port, a document id or a page is not enough.
/// </summary>
internal sealed class LocalHttpRequestSecurity
{
    /// <summary>The query parameter that carries an entry ticket.</summary>
    internal const string TicketParameter = "t";

    /// <summary>How long an entry ticket stays valid before its first use.</summary>
    internal static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(5);

    private const int MaximumTickets = 64;
    private const int MaximumSessions = 64;

    private readonly object _sync = new();
    private readonly List<(string Value, long ExpiresAt)> _tickets = [];
    private readonly List<string> _sessions = [];
    private readonly int _port;

    public LocalHttpRequestSecurity(int port)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65_535);
        _port = port;
        CookieName = string.Create(CultureInfo.InvariantCulture, $"aspose-cli-{port}");
    }

    /// <summary>
    /// The session cookie's name. Browsers share cookies across the ports of
    /// one host, so the name carries the port and two services never
    /// overwrite each other's session.
    /// </summary>
    internal string CookieName { get; }

    /// <summary>The origin of the bound address, such as <c>http://127.0.0.1:49152</c>.</summary>
    internal string Origin => "http://" + Authority(_port);

    public static bool IsRequestAllowed(
        HttpListenerRequest request,
        int expectedPort)
    {
        ArgumentNullException.ThrowIfNull(request);
        return IsLoopbackRemote(request.RemoteEndPoint)
            && IsExactLoopbackHost(request.UserHostName, expectedPort);
    }

    /// <summary>
    /// A state change is accepted only from a page of this origin. The
    /// session cookie is SameSite=Strict, so another site's page cannot send
    /// it, and the exact Origin refuses a request no page of this service made.
    /// </summary>
    public static bool IsSameOrigin(HttpListenerRequest request, int expectedPort)
    {
        ArgumentNullException.ThrowIfNull(request);
        return string.Equals(request.Headers["Origin"], "http://" + Authority(expectedPort), StringComparison.Ordinal);
    }

    /// <summary>
    /// Appends a new entry ticket to an address of this service. The ticket
    /// admits one browser once, within <see cref="TicketLifetime"/>.
    /// </summary>
    public string Entry(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        // Compared as a URI: an address may spell the default port 80 or leave it out.
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? address)
            || address.Scheme != Uri.UriSchemeHttp
            || address.Host != "127.0.0.1"
            || address.Port != _port
            || address.Query.Length != 0
            || address.Fragment.Length != 0)
        {
            throw new ArgumentException("An entry address is a path of this service without a query.", nameof(url));
        }
        string ticket = RandomToken();
        lock (_sync)
        {
            long now = Environment.TickCount64;
            Prune(now);
            if (_tickets.Count == MaximumTickets)
            {
                _tickets.RemoveAt(0);
            }
            _tickets.Add((ticket, now + (long)TicketLifetime.TotalMilliseconds));
        }
        return url + "?" + TicketParameter + "=" + ticket;
    }

    /// <summary>
    /// Decides a GET that presents an entry ticket, and returns
    /// <see cref="Admission.None"/> for any other request. A valid ticket is
    /// spent and yields the <c>Set-Cookie</c> value of a new session. A spent,
    /// expired or unknown ticket is refused, unless the browser already holds
    /// a session, which it keeps: a person who opens the printed address a
    /// second time in the same browser is not turned away.
    /// </summary>
    public Admission Admit(HttpListenerRequest request, out string? setCookie)
    {
        ArgumentNullException.ThrowIfNull(request);
        setCookie = null;
        string? ticket = request.QueryString[TicketParameter];
        if (ticket is null
            || !string.Equals(request.HttpMethod, "GET", StringComparison.Ordinal))
        {
            return Admission.None;
        }
        lock (_sync)
        {
            Prune(Environment.TickCount64);
            int found = -1;
            for (int index = 0; index < _tickets.Count; index++)
            {
                if (SecretText.FixedEquals(ticket, _tickets[index].Value))
                {
                    found = index;
                }
            }
            if (found < 0)
            {
                return HasSessionLocked(request) ? Admission.Granted : Admission.Refused;
            }
            _tickets.RemoveAt(found);
            string session = RandomToken();
            if (_sessions.Count == MaximumSessions)
            {
                _sessions.RemoveAt(0);
            }
            _sessions.Add(session);
            setCookie = $"{CookieName}={session}; Path=/; HttpOnly; SameSite=Strict";
            return Admission.Granted;
        }
    }

    /// <summary>Whether the request carries the cookie of a session this service granted.</summary>
    public bool HasSession(HttpListenerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_sync)
        {
            return HasSessionLocked(request);
        }
    }

    /// <summary>
    /// The address a granted entry continues to: the same path on this
    /// origin, without the ticket. It is absolute, so a path that begins
    /// with two slashes cannot name another host.
    /// </summary>
    public string Continuation(HttpListenerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Origin + (request.Url?.AbsolutePath ?? "/");
    }

    private bool HasSessionLocked(HttpListenerRequest request)
    {
        string? supplied = request.Cookies[CookieName]?.Value;
        bool matched = false;
        foreach (string session in _sessions)
        {
            matched |= SecretText.FixedEquals(supplied, session);
        }
        return matched;
    }

    private void Prune(long now) => _tickets.RemoveAll(ticket => ticket.ExpiresAt <= now);

    internal static bool IsLoopbackRemote(IPEndPoint? remoteEndPoint) =>
        remoteEndPoint is not null
        && IPAddress.IsLoopback(remoteEndPoint.Address);

    /// <summary>
    /// The one Host a local service answers: the address it is bound to, as
    /// a byte-exact string. Parsing the header as a URI would accept user
    /// information, trailing dots or default ports that name the same host.
    /// </summary>
    internal static bool IsExactLoopbackHost(
        string? hostHeader,
        int expectedPort) =>
        string.Equals(hostHeader, Authority(expectedPort), StringComparison.Ordinal);

    /// <summary>The Host and Origin authority a browser sends for the bound address.</summary>
    private static string Authority(int port) =>
        port == 80
            ? "127.0.0.1"
            : string.Create(CultureInfo.InvariantCulture, $"127.0.0.1:{port}");

    internal static string RandomToken() =>
        Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .ToLowerInvariant();

    /// <summary>What an entry ticket earned a request.</summary>
    internal enum Admission
    {
        /// <summary>The request presents no ticket; the session cookie decides it.</summary>
        None,

        /// <summary>The browser holds a session and continues to the address without the ticket.</summary>
        Granted,

        /// <summary>The ticket is spent, expired or unknown and the browser holds no session.</summary>
        Refused,
    }
}
