using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// One way a product's reads report an item: places matching <see cref="Place"/> belong to the
/// items <see cref="Items"/> names, as <c>&lt;unit&gt;/&lt;address&gt;</c> the way a target names
/// them; no item when the place is derived from others, such as a count or a preview.
/// </summary>
/// <param name="Place">The place, <c>&lt;command&gt;: &lt;path&gt;</c>, with groups the items use.</param>
/// <param name="Items">The items of a match, by its groups (the whole place first) and a lookup of a path in the same read result.</param>
/// <param name="When">Whether the rule applies to a match; null when it applies to every one.</param>
internal sealed record ReadItem(Regex Place, Func<string[], Func<string, string?>, IEnumerable<string>> Items, Func<string[], Func<string, string?>, bool>? When = null);

// C-R39: the items whose reads changed, named as the targets of an edit name them (AGENTS.md
// "Targets"). One table per product, in one place; a changed place no rule names is reported.
internal static partial class ContractRules
{
    /// <summary>The items of each product's reads, first match wins.</summary>
    internal static IReadOnlyDictionary<string, ReadItem[]> ReadItems { get; } = new Dictionary<string, ReadItem[]>(StringComparer.Ordinal)
    {
        ["cells"] =
        [
            Derived(@"^cells inspect: workbook\.(name|sheetCount|definedNameCount|hasMacros|fonts|formulaErrors)\b"),
            Item(@"^cells inspect: workbook\.(structureProtected|structurePasswordProtected)\b", static (_, _) => ["workbook/protection"]),
            Derived(@"^cells inspect: workbook\.sheets\[\d+\]\.(position|usedRange|rowCount|columnCount|chartCount|pivotTableCount|preview|protected|passwordProtected)\b"),
            // The warning sheet an evaluation save adds is the evaluation's, not the operation's.
            Item(@"^cells inspect: workbook\.(sheets|layouts)\[(\d+)\]", static (_, _) => [],
                static (m, at) => EvaluationWarningSheet().IsMatch(at(m[1] == "sheets" ? $"workbook.sheets[{m[2]}].name" : $"workbook.layouts[{m[2]}].sheet") ?? string.Empty)),
            Item(@"^cells inspect: workbook\.sheets\[(\d+)\]", (m, at) => [$"sheet/{at($"workbook.sheets[{m[1]}].name")}"]),
            Item(@"^cells inspect: workbook\.layouts\[(\d+)\](\.sheet)?$", (m, at) => [$"sheet/{at($"workbook.layouts[{m[1]}].sheet")}"]),
            Item(@"^cells inspect: workbook\.layouts\[(\d+)\]\.(sheetView|tabColor|freezePanes|protection|printArea|pageSetup|autofilter)\b",
                (m, at) => [$"{SheetUnit(m[2])}/{at($"workbook.layouts[{m[1]}].sheet")}"]),
            Item(@"^cells inspect: workbook\.layouts\[(\d+)\]\.(rowHeights|rowGroups)\[(\d+)\]", (m, at) =>
                LineRange(at($"workbook.layouts[{m[1]}].{m[2]}[{m[3]}].first"), at($"workbook.layouts[{m[1]}].{m[2]}[{m[3]}].last"), rows: true)
                    .Select(line => $"row/{at($"workbook.layouts[{m[1]}].sheet")}!{line}")),
            Item(@"^cells inspect: workbook\.layouts\[(\d+)\]\.(columnWidths|columnGroups)\[(\d+)\]", (m, at) =>
                LineRange(at($"workbook.layouts[{m[1]}].{m[2]}[{m[3]}].first"), at($"workbook.layouts[{m[1]}].{m[2]}[{m[3]}].last"), rows: false)
                    .Select(line => $"column/{at($"workbook.layouts[{m[1]}].sheet")}!{line}")),
            // The names a print area, print titles or a filter keep themselves in.
            Item(@"^cells inspect: workbook\.definedNames\[(\d+)\]", (m, at) => BuiltInName().IsMatch(at($"workbook.definedNames[{m[1]}].name") ?? string.Empty)
                ? []
                : [$"name/{at($"workbook.definedNames[{m[1]}].name")}"]),
            Item(@"^cells inspect: workbook\.tables\[(\d+)\]", (m, at) => [$"table/{at($"workbook.tables[{m[1]}].name")}"]),
            Item(@"^cells inspect: workbook\.(charts|images)\[(\d+)\]", (m, at) =>
                [$"{(m[1] == "charts" ? "chart" : "image")}/{at($"workbook.{m[1]}[{m[2]}].sheet")}/{at($"workbook.{m[1]}[{m[2]}].index")}"]),
            Item(@"^cells inspect: workbook\.pivots\[(\d+)\]", (m, at) => [$"pivot/{at($"workbook.pivots[{m[1]}].sheet")}/{at($"workbook.pivots[{m[1]}].name")}"]),
            Item(@"^cells inspect: workbook\.validations\[(\d+)\]", (m, at) => [$"validation/{at($"workbook.validations[{m[1]}].sheet")}!{at($"workbook.validations[{m[1]}].range")}"]),
            Item(@"^cells inspect: workbook\.(conditionalFormats|sparklines)\[(\d+)\]", (m, at) =>
                [$"{(m[1] == "sparklines" ? "sparkline-group" : "conditional-format")}/{at($"workbook.{m[1]}[{m[2]}].sheet")}/{SheetOrdinal(m[1], int.Parse(m[2], CultureInfo.InvariantCulture), at)}"]),
            Item(@"^cells inspect: workbook\.comments\[(\d+)\]", (m, at) => [$"comment/{at($"workbook.comments[{m[1]}].sheet")}!{at($"workbook.comments[{m[1]}].cell")}"]),
            Item(@"^cells inspect: workbook\.hyperlinks\[(\d+)\]", (m, at) => [$"hyperlink/{at($"workbook.hyperlinks[{m[1]}].sheet")}!{at($"workbook.hyperlinks[{m[1]}].range")}"]),
            Item(@"^cells inspect: workbook\.defaultFont\b", static (_, _) => ["workbook/default-font"]),
            // A read without --sheet reads the active sheet: reading another one, at another
            // position, is a change of it; the same one under another name is a renamed sheet.
            Item(@"^cells read: sheet\.position$", static (_, _) => ["workbook/active-sheet"]),
            Item(@"^cells read: sheet\.name$", static (_, at) => [$"sheet/{at("sheet.name")}"]),
            Derived(@"^cells read: (content|window|sheet\.(usedRange|range))\b"),
            Item(@"^cells read: sheet\.cells\[(\d+)\]\[(\d+)\]", (m, at) => [$"cell/{at("sheet.name")}!{Offset(at("sheet.range"), m[1], m[2])}"]),
        ],
        ["pdf"] =
        [
            Derived(@"^pdf (inspect|read): (pdf|fonts|signatures|layers|forms\.(kind|fieldCount|readOnly)|window|pageCount|content)\b"),
            Derived(@"^pdf inspect: (properties\.(creationDate|modificationDate|creator|producer))\b"),
            Item(@"^pdf (inspect|read): pages\[(\d+)\]", (m, at) => [$"page/{at($"pages[{m[2]}].page") ?? Next(m[2])}"]),
            Item(@"^pdf inspect: pageLabels\[(\d+)\]", (m, _) => [$"page-label/{Next(m[1])}"]),
            Item(@"^pdf inspect: outline\[(\d+)\]", (m, at) => [$"bookmark/{at($"outline[{m[1]}].bookmark")}"]),
            Item(@"^pdf inspect: links\[(\d+)\]", (m, at) => [$"page/{at($"links[{m[1]}].page")}"]),
            Item(@"^pdf inspect: forms\.fields\[(\d+)\]", (m, at) => [$"field/{at($"forms.fields[{m[1]}].name")}"]),
            Item(@"^pdf inspect: attachments\[(\d+)\]", (m, at) => [$"attachment/{at($"attachments[{m[1]}].name")}"]),
            Item(@"^pdf inspect: permissions\b", static (_, _) => ["security/encryption"]),
            Item(@"^pdf inspect: properties\.([^.\[]+)", static (m, _) => [$"property/{m[1]}"]),
        ],
        ["slides"] =
        [
            Derived(@"^slides (inspect|read): (content|window|slideCount|layouts|masters)\b"),
            Derived(@"^slides inspect: presentation\.(slideCount|masterCount|layoutCount|sectionCount|commentCount|mediaCount|hasMacros|orientation)\b"),
            Derived(@"^slides (inspect|read): slides\[\d+\]\.(slideId|shapeCount|hasNotes|commentCount|contentTruncated|title)\b"),
            Item(@"^slides inspect: presentation\.slideSize\b", static (_, _) => ["presentation/slide-size"]),
            Item(@"^slides inspect: properties\.([^.\[]+)", static (m, _) => [$"property/{m[1]}"]),
            Item(@"^slides inspect: sections\[(\d+)\]", (m, at) => [$"section/{at($"sections[{m[1]}].name")}"]),
            Item(@"^slides read: slides\[(\d+)\]\.notes\b", (m, at) => [$"slide/{at($"slides[{m[1]}].slide")}/notes"]),
            // The text box an evaluation save adds to every slide is the evaluation's, not the operation's.
            Item(@"^slides read: slides\[(\d+)\]\.shapes\[(\d+)\]", static (_, _) => [],
                static (m, at) => at($"slides[{m[1]}].shapes[{m[2]}].text")?.StartsWith("Evalu", StringComparison.Ordinal) == true),
            // The footer, date and number placeholders are the slide's footer, which the slide reports.
            Item(@"^slides read: slides\[(\d+)\]\.shapes\[(\d+)\]", (m, at) =>
                FooterPlaceholder().IsMatch(at($"slides[{m[1]}].shapes[{m[2]}].placeholder") ?? string.Empty)
                    ? [$"slide/{at($"slides[{m[1]}].slide")}"]
                    : [$"slide/{at($"slides[{m[1]}].slide")}/shape/{at($"slides[{m[1]}].shapes[{m[2]}].shapeId")}"]),
            Item(@"^slides (inspect|read): slides\[(\d+)\]", (m, at) => [$"slide/{at($"slides[{m[2]}].slide")}"]),
        ],
        ["words"] =
        [
            Derived(@"^words (inspect|read): (content|window|blockCount|outline|fields|bookmarks|fonts|images|revisions)\b"),
            Derived(@"^words inspect: document\.(sectionCount|blockCount|paragraphCount|tableCount|pageCount|wordCount|hasRevisions|revisionCount|revisionAuthors|commentCount|signed|hasMacros)\b"),
            Item(@"^words inspect: document\.watermark\b", static (_, _) => ["watermark/1"]),
            Item(@"^words inspect: document\.protection\b", static (_, _) => ["protection/document"]),
            Item(@"^words inspect: properties\.([^.\[]+)", static (m, _) => [$"property/{m[1]}"]),
            Item(@"^words inspect: sections\[(\d+)\]", (m, at) => [$"section/{at($"sections[{m[1]}].section")}"]),
            Item(@"^words inspect: styles\[(\d+)\]", (m, at) => [$"style/{at($"styles[{m[1]}].name")}"]),
            Item(@"^words inspect: (comments|tables)\[(\d+)\]", (m, at) => [$"block/{at($"{m[1]}[{m[2]}].block")}"]),
            Item(@"^words read: blocks\[(\d+)\]", (m, at) => [$"block/{at($"blocks[{m[1]}].block")}"]),
        ],
    };

    /// <summary>The unit of a sheet's part in a target, by its name in the layout a read reports.</summary>
    private static string SheetUnit(string part) => part switch
    {
        "sheetView" => "sheet-view",
        "tabColor" => "tab-color",
        "freezePanes" => "freeze-panes",
        "printArea" => "print-area",
        "pageSetup" => "page-setup",
        _ => part,
    };

    private static ReadItem Item([StringSyntax(StringSyntaxAttribute.Regex)] string place, Func<string[], Func<string, string?>, IEnumerable<string>> items) =>
        new(new Regex(place, RegexOptions.CultureInvariant), items);

    /// <summary>A rule that applies only where <paramref name="when"/> holds; another rule names the other places.</summary>
    private static ReadItem Item(
        [StringSyntax(StringSyntaxAttribute.Regex)] string place, Func<string[], Func<string, string?>, IEnumerable<string>> items, Func<string[], Func<string, string?>, bool> when) =>
        new(new Regex(place, RegexOptions.CultureInvariant), items, when);

    private static ReadItem Derived([StringSyntax(StringSyntaxAttribute.Regex)] string place) =>
        new(new Regex(place, RegexOptions.CultureInvariant), static (_, _) => []);

    /// <summary>
    /// The items a changed place of a read belongs to, by the product's <see cref="ReadItems"/>,
    /// looking its names up in the read after the change, or before it for an item the change
    /// removed; none for a derived place, and null for a place no rule names.
    /// </summary>
    internal static IEnumerable<string>? RoundTripAddress(string product, string change, JsonArray read, JsonArray staged)
    {
        int colon = change.IndexOf(": ", StringComparison.Ordinal);
        string command = change[..colon];
        string? Lookup(string path)
        {
            foreach (JsonArray state in new[] { read, staged })
            {
                JsonNode? result = state.FirstOrDefault(item => item!["command"]!.GetValue<string>() == command)?["result"];
                if (result is not null && ScenarioRunner.TryResolve(result, path, out JsonNode? value) && value is JsonValue scalar)
                {
                    return scalar.ToJsonString().Trim('"');
                }
            }
            return null;
        }
        foreach (ReadItem rule in ReadItems.GetValueOrDefault(product) ?? [])
        {
            if (rule.Place.Match(change) is { Success: true } match
                && match.Groups.Cast<Group>().Select(static group => group.Value).ToArray() is var groups
                && rule.When?.Invoke(groups, Lookup) != false)
            {
                string[] items = [.. rule.Items(groups, Lookup)];
                // A name the lookup could not find leaves the address incomplete: no rule names it.
                return items.Any(static item => item.Contains("//", StringComparison.Ordinal) || item.EndsWith('/') || item.EndsWith('!')) ? null : items;
            }
        }
        return null;
    }

    /// <summary>
    /// A read with its style pool resolved into the cells that use it, so that a change of the
    /// pool's numbering is no change and a changed style is a change of each cell that uses it.
    /// </summary>
    internal static JsonArray ResolveStyles(JsonArray state) =>
        [.. state.Select(static item =>
        {
            var copy = (JsonObject)item!.DeepClone();
            if (copy["result"] is JsonObject result && result["styles"] is JsonObject pool)
            {
                result.Remove("styles");
                Inline(result, pool);
            }
            return (JsonNode)copy;
        })];

    private static void Inline(JsonNode? node, JsonObject pool)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj["styleId"]?.GetValue<string>() is { } id)
                {
                    obj["styleId"] = pool[id]?.DeepClone();
                }
                foreach ((_, JsonNode? value) in obj.ToArray())
                {
                    Inline(value, pool);
                }
                break;
            case JsonArray array:
                foreach (JsonNode? item in array)
                {
                    Inline(item, pool);
                }
                break;
        }
    }

    private static string Next(string index) => (int.Parse(index, CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture);

    /// <summary>The rows or column letters from first to last.</summary>
    private static IEnumerable<string> LineRange(string? first, string? last, bool rows)
    {
        if (first is null || last is null)
        {
            return ["/"];
        }
        if (rows)
        {
            int from = int.Parse(first, CultureInfo.InvariantCulture);
            return Enumerable.Range(from, int.Parse(last, CultureInfo.InvariantCulture) - from + 1).Select(static row => row.ToString(CultureInfo.InvariantCulture));
        }
        int start = ColumnNumber(first);
        return Enumerable.Range(start, ColumnNumber(last) - start + 1).Select(ColumnName);
    }

    /// <summary>The number, from 1, of a sheet's conditional format or sparkline group among those of its sheet.</summary>
    private static int SheetOrdinal(string list, int index, Func<string, string?> at)
    {
        string? sheet = at($"workbook.{list}[{index}].sheet");
        return Enumerable.Range(0, index).Count(earlier => at($"workbook.{list}[{earlier}].sheet") == sheet) + 1;
    }

    /// <summary>The cell a read window's row and column offsets name, from the window's first cell.</summary>
    private static string Offset(string? range, string row, string column)
    {
        Match origin = CellName().Match(range ?? string.Empty);
        if (!origin.Success)
        {
            return string.Empty;
        }
        int first = int.Parse(origin.Groups["row"].Value, CultureInfo.InvariantCulture);
        return ColumnName(ColumnNumber(origin.Groups["column"].Value) + int.Parse(column, CultureInfo.InvariantCulture))
            + (first + int.Parse(row, CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture);
    }

    private static int ColumnNumber(string letters) => letters.Aggregate(0, static (number, letter) => number * 26 + (char.ToUpperInvariant(letter) - 'A' + 1));

    private static string ColumnName(int number)
    {
        string name = string.Empty;
        for (; number > 0; number = (number - 1) / 26)
        {
            name = (char)('A' + ((number - 1) % 26)) + name;
        }
        return name;
    }

    [GeneratedRegex(@"^(?<column>[A-Z]+)(?<row>\d+)")]
    private static partial Regex CellName();

    [GeneratedRegex(@"(Print_Area|Print_Titles|_FilterDatabase|^_xlnm)", RegexOptions.CultureInvariant)]
    private static partial Regex BuiltInName();

    [GeneratedRegex(@"^(footer|date|date-time|datetime|slide-number|slidenumber|number)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex FooterPlaceholder();
}
