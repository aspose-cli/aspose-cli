using System.Text;
using System.Text.RegularExpressions;

namespace Aspose.Cli.TestKit;

/// <summary>
/// One operation document a Skill shows, with the file and line it starts on. A document an
/// <c>--ops</c> argument passes, inline or as a file beside an example, also names the command
/// word after <c>aspose-cli</c> (<see cref="Command"/>, such as <c>pdf</c>), so it can be judged
/// by that product's schema; null for a fenced block or file no command passes.
/// </summary>
public sealed record SkillOperationDocument(string Source, int Line, string Json, string? Command = null)
{
    public string Location => $"{Path.GetRelativePath(RepositoryPaths.Root, Source)}:{Line}";
}

/// <summary>
/// Finds every operation document a Skill directory shows, so the product contract tests can
/// validate each against the generated schema. The rules:
/// <list type="bullet">
/// <item>A <c>*.json</c> file or a fenced <c>json</c> block in Markdown is an operation
/// document when its text holds <c>"ops"</c>, and must then parse. Other <c>json</c> blocks show
/// output fragments (such as one member of a result) and are not read.</item>
/// <item>An <c>--ops</c> argument in a fenced code block (prose and inline code show the syntax
/// with placeholders such as <c>'{...}'</c> and are not read) whose value
/// is quoted (<c>'...'</c> or <c>"..."</c>, possibly over several lines) or a PowerShell here-string
/// (<c>@'...'@</c>, <c>@"..."@</c>) and starts with <c>{</c> or <c>[</c> is an operation document,
/// unquoted with the quoting rules of the enclosing fence (PowerShell for a <c>powershell</c> or
/// <c>pwsh</c> fence, POSIX shell otherwise). So is the body that <c>--ops -</c> reads from
/// standard input: a heredoc the command's line opens, or in a PowerShell fence a here-string
/// piped into the command.</item>
/// <item>An <c>--ops</c> value naming a <c>*.json</c> file is a reference. In a Markdown file
/// under <c>examples/</c>, whose commands run next to their files, the file must exist beside
/// it; elsewhere such names stand for the reader's own file. Variables, placeholders and a
/// <c>-</c> fed from anything but a heredoc or a piped here-string are not read.</item>
/// </list>
/// </summary>
public static class SkillOperationDocuments
{
    private static readonly Regex OpsArgument = new(@"--ops[ \t]+", RegexOptions.CultureInvariant);

    /// <summary>Collects the documents under <paramref name="skills"/> and the problems found reading them.</summary>
    public static (IReadOnlyList<SkillOperationDocument> Documents, IReadOnlyList<string> Problems) Collect(string skills)
    {
        var documents = new List<SkillOperationDocument>();
        var problems = new List<string>();
        foreach (string file in Directory.EnumerateFiles(skills, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            if (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                string json = File.ReadAllText(file);
                if (json.Contains("\"ops\"", StringComparison.Ordinal))
                {
                    documents.Add(new SkillOperationDocument(file, 1, json));
                }
            }
            else if (file.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                bool example = Path.GetRelativePath(skills, file)
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Contains("examples", StringComparer.Ordinal);
                ReadMarkdown(file, File.ReadAllText(file).ReplaceLineEndings("\n"), example, documents, problems);
            }
        }

        return (documents, problems);
    }

    private static void ReadMarkdown(
        string file,
        string markdown,
        bool example,
        List<SkillOperationDocument> documents,
        List<string> problems)
    {
        string[] lines = markdown.Split('\n');
        var lineStarts = new int[lines.Length];
        var fences = new string?[lines.Length];
        string? fence = null;
        for (int index = 0, offset = 0; index < lines.Length; offset += lines[index].Length + 1, index++)
        {
            lineStarts[index] = offset;
            string trimmed = lines[index].TrimStart();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                if (fence is null)
                {
                    fence = trimmed[3..].Trim().Split(' ', 2)[0];
                    if (fence == "json")
                    {
                        AddFencedJson(file, lines, index, documents);
                    }
                }
                else
                {
                    fence = null;
                }

                continue;
            }

            fences[index] = fence;
        }

        foreach (Match match in OpsArgument.Matches(markdown))
        {
            int start = match.Index + match.Length;
            int line = Array.BinarySearch(lineStarts, match.Index);
            line = line >= 0 ? line : ~line - 1;
            if (fences[line] is null)
            {
                continue;
            }

            string location = $"{Path.GetRelativePath(RepositoryPaths.Root, file)}:{line + 1}";
            bool powerShell = fences[line] is "powershell" or "pwsh" or "ps1";
            string? value = ReadValue(markdown, start, powerShell, out bool quoted, out string? error);
            if (error is not null)
            {
                problems.Add($"{location}: --ops {error}");
                continue;
            }

            string trimmedValue = value!.Trim();
            string? command = CommandWord(markdown, match.Index);
            string beside = Path.Combine(Path.GetDirectoryName(file)!, trimmedValue);
            if (!quoted && trimmedValue == "-")
            {
                if (StandardInput(lines, fences, line, powerShell, out int bodyLine, out string? stdinError) is { } body)
                {
                    documents.Add(new SkillOperationDocument(file, bodyLine + 1, body, command));
                }
                else if (stdinError is not null)
                {
                    problems.Add($"{location}: --ops - {stdinError}");
                }
            }
            else if (quoted && (trimmedValue.StartsWith('{') || trimmedValue.StartsWith('[')))
            {
                documents.Add(new SkillOperationDocument(file, line + 1, value, command));
            }
            else if (trimmedValue.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && example && !File.Exists(beside))
            {
                problems.Add($"{location}: --ops names '{trimmedValue}', which is not beside this example.");
            }
            else if (trimmedValue.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && example)
            {
                documents.Add(new SkillOperationDocument(beside, 1, File.ReadAllText(beside), command));
            }
        }
    }

    /// <summary>
    /// The document a command on <paramref name="line"/> reads with <c>--ops -</c>: the body of
    /// the POSIX heredoc the line opens (<c>&lt;&lt;'EOF'</c>, <c>&lt;&lt;EOF</c> or
    /// <c>&lt;&lt;-EOF</c>), or, in a PowerShell fence, of the here-string piped into it
    /// (<c>@'</c> … <c>'@ | aspose-cli …</c>); null, without an error, when the line reads standard
    /// input from anything else. <paramref name="bodyLine"/> is the body's first line, 0-based.
    /// </summary>
    private static string? StandardInput(string[] lines, string?[] fences, int line, bool powerShell, out int bodyLine, out string? error)
    {
        bodyLine = line + 1;
        error = null;
        if (powerShell)
        {
            Match pipe = HereStringPipe.Match(lines[line]);
            if (!pipe.Success)
            {
                return null;
            }
            string opening = "@" + pipe.Groups[1].Value;
            for (int start = line - 1; start >= 0 && fences[start] is not null; start--)
            {
                if (lines[start].TrimEnd().EndsWith(opening, StringComparison.Ordinal))
                {
                    bodyLine = start + 1;
                    return string.Join('\n', lines[(start + 1)..line]);
                }
            }
            error = "here-string has no start";
            return null;
        }

        Match heredoc = Heredoc.Match(lines[line]);
        if (!heredoc.Success)
        {
            return null;
        }
        string terminator = heredoc.Groups[3].Value;
        for (int end = line + 1; end < lines.Length && fences[end] is not null; end++)
        {
            if (lines[end].Trim() == terminator)
            {
                return string.Join('\n', lines[(line + 1)..end]);
            }
        }
        error = "heredoc is not terminated";
        return null;
    }

    private static readonly Regex Heredoc = new(@"<<(-?)\s*(['""]?)(\w+)\2", RegexOptions.CultureInvariant);

    private static readonly Regex HereStringPipe = new(@"^\s*(['""])@\s*\|", RegexOptions.CultureInvariant);

    /// <summary>The word after the last <c>aspose-cli</c> of the command line that holds <paramref name="index"/>, or null.</summary>
    private static string? CommandWord(string text, int index)
    {
        int start = text.LastIndexOf("aspose-cli ", index, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }
        // The command may continue over lines, but no blank line or fence lies between.
        string between = text[start..index];
        if (between.Contains("\n\n", StringComparison.Ordinal) || between.Contains("```", StringComparison.Ordinal))
        {
            return null;
        }
        Match word = CommandWordPattern.Match(text, start + "aspose-cli ".Length);
        return word.Success && word.Index == start + "aspose-cli ".Length ? word.Value : null;
    }

    private static readonly Regex CommandWordPattern = new("[a-z]+", RegexOptions.CultureInvariant);

    private static void AddFencedJson(string file, string[] lines, int fenceLine, List<SkillOperationDocument> documents)
    {
        var block = new StringBuilder();
        for (int index = fenceLine + 1; index < lines.Length && !lines[index].TrimStart().StartsWith("```", StringComparison.Ordinal); index++)
        {
            block.Append(lines[index]).Append('\n');
        }

        string text = block.ToString();
        if (text.Contains("\"ops\"", StringComparison.Ordinal))
        {
            documents.Add(new SkillOperationDocument(file, fenceLine + 2, text));
        }
    }

    /// <summary>Reads one shell word starting at <paramref name="start"/> and removes its quoting.</summary>
    private static string? ReadValue(string text, int start, bool powerShell, out bool quoted, out string? error)
    {
        quoted = false;
        error = null;
        if (start >= text.Length)
        {
            return string.Empty;
        }

        char first = text[start];
        if (first == '@' && start + 2 < text.Length && text[start + 1] is '\'' or '"' && text[start + 2] == '\n')
        {
            quoted = true;
            string terminator = "\n" + text[start + 1] + "@";
            int end = text.IndexOf(terminator, start + 2, StringComparison.Ordinal);
            if (end < 0)
            {
                error = "here-string is not terminated";
                return null;
            }

            return text[(start + 3)..end];
        }

        if (first is '\'' or '"')
        {
            quoted = true;
            var value = new StringBuilder();
            for (int index = start + 1; index < text.Length; index++)
            {
                char current = text[index];
                char next = index + 1 < text.Length ? text[index + 1] : '\0';
                if (first == '\'')
                {
                    if (current != '\'')
                    {
                        value.Append(current);
                    }
                    else if (powerShell && next == '\'')
                    {
                        value.Append('\'');
                        index++;
                    }
                    else if (!powerShell && string.CompareOrdinal(text, index, "'\\''", 0, 4) == 0)
                    {
                        value.Append('\'');
                        index += 3;
                    }
                    else
                    {
                        return value.ToString();
                    }
                }
                else if (powerShell)
                {
                    if (current == '`' && next != '\0')
                    {
                        value.Append(next);
                        index++;
                    }
                    else if (current == '"' && next == '"')
                    {
                        value.Append('"');
                        index++;
                    }
                    else if (current == '"')
                    {
                        return value.ToString();
                    }
                    else
                    {
                        value.Append(current);
                    }
                }
                else
                {
                    if (current == '\\' && next is '"' or '\\' or '$' or '`')
                    {
                        value.Append(next);
                        index++;
                    }
                    else if (current == '\\' && next == '\n')
                    {
                        index++;
                    }
                    else if (current == '"')
                    {
                        return value.ToString();
                    }
                    else
                    {
                        value.Append(current);
                    }
                }
            }

            error = "value is not terminated";
            return null;
        }

        int stop = start;
        while (stop < text.Length && !char.IsWhiteSpace(text[stop]) && text[stop] is not ('`' or ')' or '|' or ';'))
        {
            stop++;
        }

        return text[start..stop];
    }
}
