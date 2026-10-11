using System.Globalization;
using System.Text;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// The prose of the C# sources: every string literal, with adjacent literals joined by <c>+</c>
/// read as one text and each interpolation hole read as <c>…</c>, and every documentation comment
/// (<c>///</c>), which the schemas publish as descriptions, with its XML tags removed. A literal
/// inside an interpolation hole is a text of its own.
/// </summary>
internal static partial class ContractRules
{
    /// <summary>One text of a source file and the line it starts on.</summary>
    private sealed record SourceText(int Line, string Text);

    private const string Hole = "…";

    private static List<SourceText> ReadSource(string source) => new SourceReader(source).Read();

    private sealed class SourceReader(string source)
    {
        private readonly List<SourceText> _texts = [];
        private readonly List<SourceText> _holeTexts = [];
        private readonly StringBuilder _comment = new();
        private int _index;
        private int _line = 1;
        private int _commentLine;
        private int _literalEnd = -1;

        public List<SourceText> Read()
        {
            while (_index < source.Length)
            {
                char current = source[_index];
                if (current == '\n')
                {
                    _line++;
                    _index++;
                }
                else if (char.IsWhiteSpace(current))
                {
                    _index++;
                }
                else if (At("///") && !At("////"))
                {
                    int end = LineEnd();
                    if (_comment.Length == 0)
                    {
                        _commentLine = _line;
                    }
                    _comment.Append(source, _index + 3, end - _index - 3).Append(' ');
                    _index = end;
                }
                else
                {
                    FlushComment();
                    if (At("//"))
                    {
                        _index = LineEnd();
                    }
                    else if (At("/*"))
                    {
                        int end = source.IndexOf("*/", _index + 2, StringComparison.Ordinal);
                        end = end < 0 ? source.Length : end + 2;
                        _line += Count(_index, end, '\n');
                        _index = end;
                    }
                    else if (current == '\'')
                    {
                        SkipCharacter();
                    }
                    else if (StringStart() is { } start)
                    {
                        int line = _line;
                        int begin = _index;
                        string text = ReadString(start);
                        AddLiteral(begin, line, text);
                    }
                    else
                    {
                        _index++;
                    }
                }
            }
            FlushComment();
            return [.. _texts, .. _holeTexts];
        }

        /// <summary>A literal right after another one and a <c>+</c> continues its text.</summary>
        private void AddLiteral(int begin, int line, string text)
        {
            if (_literalEnd >= 0 && _texts.Count > 0 && source.AsSpan(_literalEnd, begin - _literalEnd).Trim().SequenceEqual("+"))
            {
                SourceText previous = _texts[^1];
                _texts[^1] = previous with { Text = previous.Text + text };
            }
            else
            {
                _texts.Add(new SourceText(line, text));
            }
            _literalEnd = _index;
        }

        private void FlushComment()
        {
            if (_comment.Length == 0)
            {
                return;
            }
            // Code in a comment reads as quoted, like a command in quotes in a literal.
            string text = CodeTag().Replace(_comment.ToString(), "`");
            text = XmlTag().Replace(text, " ");
            text = System.Net.WebUtility.HtmlDecode(Whitespace().Replace(text, " ")).Trim();
            _texts.Add(new SourceText(_commentLine, text));
            _comment.Clear();
        }

        private bool At(string token) => string.CompareOrdinal(source, _index, token, 0, token.Length) == 0;

        private int LineEnd()
        {
            int end = source.IndexOf('\n', _index);
            return end < 0 ? source.Length : end;
        }

        private int Count(int start, int end, char character)
        {
            int count = 0;
            for (int index = start; index < end; index++)
            {
                if (source[index] == character)
                {
                    count++;
                }
            }
            return count;
        }

        private void SkipCharacter()
        {
            int index = _index + 1;
            while (index < source.Length && source[index] != '\'' && source[index] != '\n')
            {
                index += source[index] == '\\' ? 2 : 1;
            }
            _index = Math.Min(index + 1, source.Length);
        }

        /// <summary>The prefix of a string literal at the current position: its <c>$</c> count and whether it is verbatim.</summary>
        private (int Dollars, bool Verbatim)? StringStart()
        {
            int index = _index;
            int dollars = 0;
            bool verbatim = false;
            while (index < source.Length && source[index] == '$')
            {
                dollars++;
                index++;
            }
            if (index < source.Length && source[index] == '@')
            {
                verbatim = true;
                index++;
                while (index < source.Length && source[index] == '$')
                {
                    dollars++;
                    index++;
                }
            }
            if (index >= source.Length || source[index] != '"')
            {
                return null;
            }
            _index = index;
            return (dollars, verbatim);
        }

        /// <summary>Reads the literal whose opening quote is at the current position.</summary>
        private string ReadString((int Dollars, bool Verbatim) start)
        {
            int quotes = 0;
            while (_index + quotes < source.Length && source[_index + quotes] == '"')
            {
                quotes++;
            }
            if (!start.Verbatim && quotes >= 3)
            {
                return ReadRaw(quotes, start.Dollars);
            }
            if (!start.Verbatim && quotes == 2)
            {
                _index += 2;
                return string.Empty;
            }

            _index++;
            var text = new StringBuilder();
            while (_index < source.Length)
            {
                char current = source[_index];
                if (current == '"')
                {
                    if (start.Verbatim && _index + 1 < source.Length && source[_index + 1] == '"')
                    {
                        text.Append('"');
                        _index += 2;
                        continue;
                    }
                    _index++;
                    break;
                }
                if (!start.Verbatim && current == '\\')
                {
                    text.Append(Escape());
                    continue;
                }
                if (start.Dollars > 0 && current is '{' or '}')
                {
                    if (_index + 1 < source.Length && source[_index + 1] == current)
                    {
                        text.Append(current);
                        _index += 2;
                        continue;
                    }
                    if (current == '{')
                    {
                        SkipHole();
                        text.Append(Hole);
                        continue;
                    }
                }
                if (current == '\n')
                {
                    _line++;
                }
                text.Append(current);
                _index++;
            }
            return text.ToString();
        }

        private string ReadRaw(int quotes, int dollars)
        {
            string terminator = new('"', quotes);
            int begin = _index + quotes;
            int end = source.IndexOf(terminator, begin, StringComparison.Ordinal);
            end = end < 0 ? source.Length : end;
            string text = source[begin..end];
            _line += Count(begin, end, '\n');
            _index = Math.Min(end + quotes, source.Length);
            if (dollars > 0)
            {
                string open = new('{', dollars);
                string close = new('}', dollars);
                var builder = new StringBuilder();
                int index = 0;
                while (index < text.Length)
                {
                    int hole = text.IndexOf(open, index, StringComparison.Ordinal);
                    int holeEnd = hole < 0 ? -1 : text.IndexOf(close, hole + dollars, StringComparison.Ordinal);
                    if (hole < 0 || holeEnd < 0)
                    {
                        builder.Append(text, index, text.Length - index);
                        break;
                    }
                    builder.Append(text, index, hole - index).Append(Hole);
                    index = holeEnd + dollars;
                }
                text = builder.ToString();
            }
            return text;
        }

        /// <summary>Skips an interpolation hole, with the literals and braces inside its expression.</summary>
        private void SkipHole()
        {
            int depth = 0;
            while (_index < source.Length)
            {
                char current = source[_index];
                if (current == '{')
                {
                    depth++;
                    _index++;
                }
                else if (current == '}')
                {
                    _index++;
                    if (--depth == 0)
                    {
                        return;
                    }
                }
                else if (current == '\'')
                {
                    SkipCharacter();
                }
                else if (StringStart() is { } start)
                {
                    // A literal inside a hole, such as a hint a ternary picks, is a text of its own.
                    int line = _line;
                    _holeTexts.Add(new SourceText(line, ReadString(start)));
                }
                else
                {
                    if (current == '\n')
                    {
                        _line++;
                    }
                    _index++;
                }
            }
        }

        private string Escape()
        {
            char kind = _index + 1 < source.Length ? source[_index + 1] : '\\';
            _index += 2;
            switch (kind)
            {
                case 'n':
                    return "\n";
                case 'r':
                    return "\r";
                case 't':
                    return "\t";
                case '0':
                    return "\0";
                case 'u' when _index + 4 <= source.Length:
                    string hex = source.Substring(_index, 4);
                    _index += 4;
                    return ((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString();
                default:
                    return kind.ToString();
            }
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"</?c(?:ode)?>")]
    private static partial System.Text.RegularExpressions.Regex CodeTag();

    [System.Text.RegularExpressions.GeneratedRegex(@"<[^<>]*>")]
    private static partial System.Text.RegularExpressions.Regex XmlTag();

    [System.Text.RegularExpressions.GeneratedRegex(@"\s+")]
    private static partial System.Text.RegularExpressions.Regex Whitespace();
}
