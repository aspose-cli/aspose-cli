using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Pdf.Engine.Mapping;

/// <summary>
/// Enforces the input-directory boundary for the Aspose.PDF Markdown importer, which reads
/// files with no resource hook (known issue PDF-HTML-EGRESS in KNOWN-ISSUES.md). Before the
/// import, every file the importer could read must be an ordinary file beneath the Markdown
/// file's directory: Markdown
/// images, the resources raw HTML and CSS name, and the resources named inside the
/// stylesheets, SVG and HTML files those load. As the importer does, the Markdown's own
/// references resolve against the working directory and a loaded file's references against
/// that file. Every decoding the importer might apply is checked, so a reference that cannot
/// be proven to stay inside refuses the import. Accepted files are charged to the input budget
/// and held open, which keeps them and their directories from being replaced, until disposal.
/// </summary>
internal sealed class MarkdownImportResources : IDisposable
{
    /// <summary>The resource count an HTML import may load, applied to the files a Markdown import may read.</summary>
    private const int MaximumFiles = 256;

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);

    // A reference definition, which only applies when an image uses its label.
    private static readonly Regex Definition = new(
        @"^[ \t]*\[(?<label>(?:[^\[\]\\\n]|\\[^\n])+)\]:[ \t]*\n?[ \t]*(?:<(?<destination>[^<>\n]*)>|(?<destination>[^\s<]\S*))",
        RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, MatchTimeout);

    private static readonly Regex Tag = new(
        """<(?<name>[a-z][a-z0-9_:.\-]*)(?<attributes>(?:[^<>"']|"[^"]*"|'[^']*')*)>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, MatchTimeout);

    private static readonly Regex Attribute = new(
        """(?<name>[^\s"'<>/=]+)\s*=\s*(?:"(?<value>[^"]*)"|'(?<value>[^']*)'|(?<value>[^\s"'<>]+))""",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, MatchTimeout);

    private static readonly Regex CssImport = new(
        """@import\s*(?:url\(\s*)?(?:"(?<value>[^"]*)"|'(?<value>[^']*)'|(?<value>[^\s"'();]+))""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, MatchTimeout);

    private static readonly Regex CssUrl = new(
        """url\(\s*(?:"(?<value>[^"]*)"|'(?<value>[^']*)'|(?<value>[^\s"'()]*))""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, MatchTimeout);

    private static readonly Regex CssEscape = new(
        @"\\(?:(?<code>[0-9a-f]{1,6})\s?|(?<character>[^\n]))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, MatchTimeout);

    private static readonly Regex MarkdownEscape = new(
        @"\\(?<character>[!-/:-@\[-`{-~])", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, MatchTimeout);

    private static readonly Regex Scheme = new(
        "^[a-z][a-z0-9+.-]*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, MatchTimeout);

    private static readonly Regex RasterData = new(
        "^data:image/(?:png|jpe?g|gif|bmp|webp|tiff|x-icon|vnd.microsoft.icon)[;,]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, MatchTimeout);

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, MatchTimeout);

    private readonly string _root;
    private readonly string _rootPrefix;
    private readonly VerifiedFileBoundary _boundary;
    private readonly ResourceBudgetLedger _budgets;
    private readonly Dictionary<string, (VerifiedReadLease Lease, bool Scanned)> _files =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Refuses a Markdown file that names a network address or script, or references a file
    /// the importer could read outside the Markdown file's directory.
    /// </summary>
    /// <param name="markdownPath">The Markdown input.</param>
    /// <param name="budgets">The invocation budgets charged for every file read.</param>
    /// <param name="workingDirectory">The directory the importer resolves the Markdown's relative references against.</param>
    internal MarkdownImportResources(string markdownPath, ResourceBudgetLedger budgets, string workingDirectory)
    {
        _budgets = budgets ?? throw new ArgumentNullException(nameof(budgets));
        _root = Path.GetDirectoryName(Path.GetFullPath(markdownPath))!;
        _rootPrefix = Path.EndsInDirectorySeparator(_root) ? _root : _root + Path.DirectorySeparatorChar;
        _boundary = new VerifiedFileBoundary(_root);
        try
        {
            byte[] markdown = budgets.Inputs.ReadAllBytes(markdownPath);
            NetworkReferenceGuard.EnsureNone(markdown, "Markdown input", markdownPath);
            var pending = new Queue<(Reference Reference, string Base, string Source)>();
            foreach (Reference reference in MarkdownReferences(Text(markdown)))
            {
                pending.Enqueue((reference, Path.GetFullPath(workingDirectory), markdownPath));
            }

            while (pending.TryDequeue(out (Reference Reference, string Base, string Source) item))
            {
                foreach (string file in Resolve(item.Reference.Value, item.Base, item.Source))
                {
                    if (Admit(file, item.Reference.Document, item.Reference.Value, item.Source) is { } content)
                    {
                        foreach (Reference nested in MarkupReferences(Text(content)))
                        {
                            pending.Enqueue((nested, Path.GetDirectoryName(file)!, file));
                        }
                    }
                }
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        foreach ((VerifiedReadLease lease, _) in _files.Values)
        {
            lease.Dispose();
        }

        _files.Clear();
    }

    /// <summary>
    /// Verifies and holds one file. Returns its content when the importer parses it for further
    /// references: a file loaded as a stylesheet or document, or an image that is not raster.
    /// </summary>
    private byte[]? Admit(string file, bool document, string reference, string source)
    {
        if (_files.TryGetValue(file, out (VerifiedReadLease Lease, bool Scanned) held))
        {
            // An unscanned file is a raster image or font, loaded as an image until now.
            if (held.Scanned || !document)
            {
                return null;
            }

            _files[file] = (held.Lease, true);
            return Read(file, held.Lease);
        }

        if (_files.Count == MaximumFiles)
        {
            throw Refusal(reference, source, $"is one more file than the {MaximumFiles} a Markdown import may read");
        }

        VerifiedReadLease lease = _boundary.TryOpenRead(file)
            ?? throw Refusal(reference, source,
                $"resolves to '{file}', which is not an ordinary file: it is a link, a junction, a directory, a device or a file with several names");
        _files.Add(file, (lease, false));
        _budgets.Consume(ResourceBudgetKinds.InputBytes, lease.Length, "bytes", "markdown-resource");
        if (!document && IsSelfContained(lease))
        {
            return null;
        }

        _files[file] = (lease, true);
        return Read(file, lease);
    }

    private static bool IsSelfContained(VerifiedReadLease lease)
    {
        Span<byte> head = stackalloc byte[16];
        lease.Position = 0;
        int length = lease.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
        return NetworkReferenceGuard.IsSelfContained(head[..length]);
    }

    private byte[] Read(string file, VerifiedReadLease lease)
    {
        long length = lease.Length;
        if (length > Array.MaxLength)
        {
            throw CliErrors.InputBudgetExceeded(ResourceBudgetKinds.MemoryBufferBytes, length,
                _budgets.Limit(ResourceBudgetKinds.MemoryBufferBytes), "bytes", "markdown-resource");
        }

        _budgets.Consume(ResourceBudgetKinds.MemoryBufferBytes, length, "bytes", "markdown-resource");
        byte[] content = new byte[length];
        lease.Position = 0;
        lease.ReadExactly(content);
        NetworkReferenceGuard.EnsureNone(content, "resource", file);
        return content;
    }

    /// <summary>
    /// Resolves one reference under every decoding the importer might apply and returns the
    /// existing files it names. Every interpretation must stay beneath the directory.
    /// </summary>
    private List<string> Resolve(string reference, string baseDirectory, string source)
    {
        var files = new List<string>();
        bool local = false;
        foreach (string interpretation in Interpretations(reference))
        {
            if (interpretation.Length == 0 || interpretation[0] == '#')
            {
                continue;
            }

            if (interpretation.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                if (!RasterData.IsMatch(interpretation))
                {
                    throw Refusal(reference, source, "is a data URI that is not a raster image and can name further resources");
                }

                continue;
            }

            if (Scheme.IsMatch(interpretation) || interpretation[0] is '/' or '\\')
            {
                throw Refusal(reference, source, "is an absolute path, a drive, a share or a URL");
            }

            string full;
            try
            {
                full = Path.GetFullPath(Path.Combine(baseDirectory, interpretation.Replace('/', '\\')));
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                throw Refusal(reference, source, "is not a valid file path");
            }

            if (!full.StartsWith(_rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw Refusal(reference, source, $"resolves against '{baseDirectory}' to '{full}', outside the Markdown file's directory");
            }

            local = true;
            if (Exists(full) && !files.Contains(full, StringComparer.OrdinalIgnoreCase))
            {
                files.Add(full);
            }
        }

        if (local && files.Count == 0)
        {
            throw Refusal(reference, source, $"does not name an existing file when resolved against '{baseDirectory}'");
        }

        return files;
    }

    private CliException Refusal(string reference, string source, string reason) => new(
        ErrorCodes.FeatureUnsupported,
        $"The reference '{Shorten(reference)}' in {source} {reason}. The PDF Markdown importer has no resource policy, so a Markdown import may read only ordinary files beneath the Markdown file's directory, {_root}.",
        hint: "Place images and stylesheets beneath the Markdown file's directory, reference them by relative path, and run the command from that directory: the importer resolves the Markdown's relative paths against the working directory.");

    private static string Shorten(string reference) =>
        reference.Length <= 120 ? reference : reference[..120] + "...";

    private static bool Exists(string path)
    {
        try
        {
            _ = File.GetAttributes(path);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            // Present but unreadable: the boundary check refuses it.
            return true;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException)
        {
            // Missing, or a name no file can have, such as one with a query.
            return false;
        }
    }

    /// <summary>
    /// The forms a reference can take once the importer decodes it: Markdown escapes, character
    /// references and percent-encoding, with and without a query or fragment.
    /// </summary>
    private static IEnumerable<string> Interpretations(string reference)
    {
        var forms = new HashSet<string>(StringComparer.Ordinal) { reference, WebUtility.HtmlDecode(reference) };
        foreach (string form in forms.ToArray())
        {
            forms.Add(MarkdownEscape.Replace(form, "${character}"));
            forms.Add(UnescapeCss(form));
        }

        foreach (string form in forms.ToArray())
        {
            string decoded = form;
            for (int round = 0; round < 4; round++)
            {
                string next = Uri.UnescapeDataString(decoded);
                forms.Add(next);
                if (next == decoded)
                {
                    break;
                }

                decoded = next;
            }
        }

        foreach (string form in forms.ToArray())
        {
            int end = form.IndexOfAny(['?', '#']);
            if (end >= 0)
            {
                forms.Add(form[..end]);
            }
        }

        return forms.Select(static form => form.Trim(' ', '\t', '\n', '\r', '\f')).Distinct(StringComparer.Ordinal);
    }

    /// <summary>
    /// Image references in Markdown syntax, inline or through a reference definition, followed
    /// by the references in raw HTML and CSS. Code is scanned too: it cannot hide a reference.
    /// </summary>
    private static IEnumerable<Reference> MarkdownReferences(string text)
    {
        var references = new List<Reference>();
        var labels = new HashSet<string>(StringComparer.Ordinal);
        for (int start = text.IndexOf("![", StringComparison.Ordinal); start >= 0;
            start = text.IndexOf("![", start + 2, StringComparison.Ordinal))
        {
            int close = ClosingBracket(text, start + 1);
            if (close < 0)
            {
                continue;
            }

            // An inline form that does not parse falls back to a reference by its text.
            labels.Add(Label(text[(start + 2)..close]));
            int next = close + 1;
            if (next < text.Length && text[next] == '(' && Destination(text, next + 1) is { } destination)
            {
                references.Add(new Reference(destination, Document: false));
            }
            else if (next < text.Length && text[next] == '[')
            {
                int labelEnd = ClosingBracket(text, next);
                if (labelEnd > next)
                {
                    labels.Add(Label(text[(next + 1)..labelEnd]));
                }
            }
        }

        foreach (Match definition in Definition.Matches(text))
        {
            if (labels.Contains(Label(definition.Groups["label"].Value)))
            {
                references.Add(new Reference(definition.Groups["destination"].Value, Document: false));
            }
        }

        return references.Concat(MarkupReferences(text));
    }

    /// <summary>
    /// The resources that HTML, SVG and CSS markup name. <see cref="Reference.Document"/> marks a
    /// resource the importer parses as a stylesheet or document whatever its content.
    /// </summary>
    private static IEnumerable<Reference> MarkupReferences(string text)
    {
        foreach (Match tag in Tag.Matches(text))
        {
            string element = tag.Groups["name"].Value.ToLowerInvariant();
            element = element[(element.LastIndexOf(':') + 1)..];
            foreach (Match attribute in Attribute.Matches(tag.Groups["attributes"].Value))
            {
                string name = attribute.Groups["name"].Value.ToLowerInvariant();
                string value = attribute.Groups["value"].Value;
                if (name is "srcset" or "imagesrcset")
                {
                    foreach (string candidate in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        yield return new Reference(candidate.Split(' ', '\t', '\n')[0], Document: false);
                    }
                }
                else if (IsDocument(element, name) is { } document)
                {
                    yield return new Reference(value, document);
                }
            }
        }

        string decoded = WebUtility.HtmlDecode(text);
        foreach (string css in new[] { text, decoded, UnescapeCss(text), UnescapeCss(decoded) })
        {
            foreach (Match import in CssImport.Matches(css))
            {
                yield return new Reference(import.Groups["value"].Value, Document: true);
            }

            foreach (Match url in CssUrl.Matches(css))
            {
                yield return new Reference(url.Groups["value"].Value, Document: false);
            }
        }
    }

    /// <summary>
    /// Whether an attribute names a resource loaded as a document (true), as an image (false),
    /// or no resource at all (null). Hyperlinks are never loaded.
    /// </summary>
    private static bool? IsDocument(string element, string attribute) => attribute switch
    {
        "src" => element is not ("img" or "image" or "input" or "source" or "video" or "audio" or "track"),
        "href" or "xlink:href" => element switch
        {
            "a" => null,
            "image" => false,
            _ => true,
        },
        "data" => true,
        "background" or "poster" or "lowsrc" or "dynsrc" => false,
        _ when attribute.EndsWith(":href", StringComparison.Ordinal) => element == "a" ? null : true,
        _ => null,
    };

    // The closing bracket that matches the one at open, within one paragraph.
    private static int ClosingBracket(string text, int open)
    {
        int depth = 0;
        for (int index = open; index < text.Length; index++)
        {
            switch (text[index])
            {
                case '\\':
                    index++;
                    break;
                case '\n' when IsBlankLine(text, index + 1):
                    return -1;
                case '[':
                    depth++;
                    break;
                case ']':
                    if (--depth == 0)
                    {
                        return index;
                    }

                    break;
            }
        }

        return -1;
    }

    private static bool IsBlankLine(string text, int start)
    {
        for (int index = start; index < text.Length && text[index] != '\n'; index++)
        {
            if (!char.IsWhiteSpace(text[index]))
            {
                return false;
            }
        }

        return true;
    }

    // An inline destination: text in angle brackets, or a run without spaces and with balanced
    // parentheses. The caller decodes escapes and character references.
    private static string? Destination(string text, int start)
    {
        int index = start;
        while (index < text.Length && text[index] is ' ' or '\t' or '\n')
        {
            index++;
        }

        if (index < text.Length && text[index] == '<')
        {
            int end = text.IndexOfAny(['>', '\n'], index + 1);
            return end > index && text[end] == '>' ? text[(index + 1)..end] : null;
        }

        int first = index;
        int depth = 0;
        for (; index < text.Length; index++)
        {
            char character = text[index];
            if (character == '\\' && index + 1 < text.Length)
            {
                index++;
            }
            else if (char.IsWhiteSpace(character) || char.IsControl(character))
            {
                break;
            }
            else if (character == '(')
            {
                depth++;
            }
            else if (character == ')' && depth-- == 0)
            {
                break;
            }
        }

        return index > first ? text[first..index] : null;
    }

    // Reference labels match case-insensitively with runs of whitespace collapsed; folding
    // compatibility forms and 'ß' makes the match at least as broad as Unicode case folding.
    private static string Label(string label) =>
        Whitespace.Replace(label.Normalize(NormalizationForm.FormKC).ToUpperInvariant().ToLowerInvariant()
            .Replace("ß", "ss", StringComparison.Ordinal), " ").Trim();

    private static string UnescapeCss(string text) =>
        CssEscape.Replace(text, static match => match.Groups["code"].Success
            ? int.TryParse(match.Groups["code"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code)
              && code is > 0 and <= 0x10FFFF and not (>= 0xD800 and <= 0xDFFF)
                ? char.ConvertFromUtf32(code)
                : "\uFFFD"
            : match.Groups["character"].Value);

    // UTF-8 unless a byte order mark says otherwise, with line breaks normalized.
    private static string Text(byte[] content)
    {
        using var reader = new StreamReader(new MemoryStream(content, writable: false), Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }

    private readonly record struct Reference(string Value, bool Document);
}
