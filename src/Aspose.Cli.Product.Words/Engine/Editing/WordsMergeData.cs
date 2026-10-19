using System.Globalization;
using System.Text;
using System.Text.Json;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Words.Engine.Editing;

/// <summary>
/// The rows mail_merge and repeat_table_row read, by column or property name: inline items,
/// a JSON array of flat objects, or a CSV file with a header row. Every value is text or null.
/// </summary>
internal static class WordsMergeData
{
    /// <summary>The hint for merge data whose shape is wrong.</summary>
    internal const string Shape =
        "Use a JSON array of flat objects, or a CSV file with a header row.";

    /// <summary>The merge data of mail_merge or repeat_table_row cannot be read as rows.</summary>
    internal static CliException Invalid(string reason, string hint) => new(
        WordsDiagnostics.MergeDataInvalid,
        $"Merge data is invalid: {reason}.",
        hint: hint);

    /// <summary>The op's inline items, or else the rows of its data file.</summary>
    internal static IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows(
        IReadOnlyList<IReadOnlyDictionary<string, object?>>? items,
        string? path,
        InputSource inputs) =>
        items is not null ? Inline(items) : Read(path!, inputs);

    /// <summary>
    /// Reads the rows of a JSON or CSV data file; a CSV header row alone is no rows. CSV cannot
    /// tell an empty value from a missing one, so an empty cell is null, as a cell past the end
    /// of a short row is.
    /// </summary>
    internal static IReadOnlyList<IReadOnlyDictionary<string, string?>> Read(string path, InputSource inputs)
    {
        if (!File.Exists(path))
        {
            throw CliErrors.FileNotFound(path);
        }

        return WordsFormats.MergeData.NamedBy(path).Contains(WordsFormats.CsvMergeData)
            ? CsvRows(ReadCsv(inputs.ReadTextFile(path)))
            : JsonRows(inputs.ReadTextFile(path));
    }

    /// <summary>Reads inline rows as the rows of a JSON data file.</summary>
    internal static IReadOnlyList<IReadOnlyDictionary<string, string?>> Inline(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows) =>
        [.. rows.Select(static row => (IReadOnlyDictionary<string, string?>)row.ToDictionary(
            static pair => pair.Key,
            static pair => Value(pair.Value),
            StringComparer.Ordinal))];

    /// <summary>
    /// Reads RFC 4180 CSV: quoted fields may hold commas, quotes (doubled) and line breaks.
    /// Unquoted fields are trimmed, and blank lines are skipped.
    /// </summary>
    internal static IReadOnlyList<string[]> ReadCsv(string text) => new CsvReader(text).Records();

    private static IReadOnlyList<IReadOnlyDictionary<string, string?>> CsvRows(IReadOnlyList<string[]> records)
    {
        if (records.Count == 0)
        {
            throw Invalid("the CSV has no header row", Shape);
        }

        string[] header = records[0];
        if (header.GroupBy(static name => name, StringComparer.Ordinal).FirstOrDefault(static names => names.Count() > 1) is { } repeated)
        {
            throw Invalid($"the CSV header names the column '{repeated.Key}' more than once", "Give each column of the CSV header row its own name.");
        }

        return records.Skip(1).Select(record => (IReadOnlyDictionary<string, string?>)header
            .Select((name, index) => (name, value: index < record.Length && record[index].Length > 0 ? record[index] : null))
            .ToDictionary(static pair => pair.name, static pair => pair.value, StringComparer.Ordinal)).ToArray();
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string?>> JsonRows(string text)
    {
        try
        {
            using JsonDocument json = JsonDocument.Parse(text);
            if (json.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw Invalid("JSON merge data must be an array of objects", Shape);
            }

            return json.RootElement.EnumerateArray().Select(JsonRow).ToArray();
        }
        catch (JsonException exception)
        {
            throw Invalid(exception.Message, "Fix the JSON syntax at the reported position; merge data is an array of flat objects.");
        }
    }

    private static IReadOnlyDictionary<string, string?> JsonRow(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw Invalid("each merge row must be an object", Shape);
        }

        var row = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!row.TryAdd(property.Name, Value(property.Value)))
            {
                throw Invalid($"a merge row names the property '{property.Name}' more than once", "Give each property of a merge row its own name.");
            }
        }

        return row;
    }

    // A CLR scalar from a C# caller merges as its JSON text would.
    private static string? Value(object? value) => value switch
    {
        JsonElement element => Value(element),
        bool flag => flag ? "true" : "false",
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => (string?)value,
    };

    // A string merges as itself and a number or Boolean as its JSON text; null is no value.
    private static string? Value(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
        _ => throw Invalid("a merge value must be a string, number, Boolean or null", Shape),
    };

    /// <summary>
    /// One pass over CSV text. A quote opens a quoted field only where the field so far is
    /// blank; elsewhere it is a literal character. Text after a closing quote joins the field.
    /// </summary>
    private sealed class CsvReader(string text)
    {
        private readonly List<string[]> _records = [];
        private readonly List<string> _fields = [];
        private readonly StringBuilder _field = new();
        private bool _wasQuoted;
        private int _index;

        public List<string[]> Records()
        {
            while (_index < text.Length)
            {
                char current = text[_index++];
                switch (current)
                {
                    case '"' when IsBlank(_field):
                        ReadQuoted();
                        break;
                    case ',':
                        EndField();
                        break;
                    case '\r' or '\n':
                        SkipLineFeedAfter(current);
                        EndRecord();
                        break;
                    default:
                        _field.Append(current);
                        break;
                }
            }

            if (_field.Length > 0 || _fields.Count > 0)
            {
                EndRecord();
            }

            return _records;
        }

        // Reads up to the closing quote; a doubled quote is one quote.
        private void ReadQuoted()
        {
            _field.Clear();
            _wasQuoted = true;
            while (_index < text.Length)
            {
                char current = text[_index++];
                if (current != '"')
                {
                    _field.Append(current);
                }
                else if (_index < text.Length && text[_index] == '"')
                {
                    _field.Append('"');
                    _index++;
                }
                else
                {
                    return;
                }
            }

            throw Invalid("the CSV ends inside a quoted field", "Close every quoted CSV field, and double each quote inside one.");
        }

        private void SkipLineFeedAfter(char current)
        {
            if (current == '\r' && _index < text.Length && text[_index] == '\n')
            {
                _index++;
            }
        }

        private void EndField()
        {
            _fields.Add(_wasQuoted ? _field.ToString() : _field.ToString().Trim());
            _field.Clear();
            _wasQuoted = false;
        }

        // A record of one empty field is a blank line.
        private void EndRecord()
        {
            EndField();
            if (_fields.Count > 1 || _fields[0].Length > 0)
            {
                _records.Add([.. _fields]);
            }

            _fields.Clear();
        }

        private static bool IsBlank(StringBuilder field)
        {
            foreach (ReadOnlyMemory<char> chunk in field.GetChunks())
            {
                foreach (char character in chunk.Span)
                {
                    if (!char.IsWhiteSpace(character))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
