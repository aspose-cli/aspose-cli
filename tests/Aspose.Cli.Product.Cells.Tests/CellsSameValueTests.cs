using Aspose.Cli.Product.Cells.Contracts;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// An operation that writes what the workbook already holds changes nothing: applied a second
/// time to its own output, it counts 0 and warns <c>OP_NO_EFFECT</c>, whatever form the engine
/// stores the value in (C-B01).
/// </summary>
public sealed class CellsSameValueTests : IClassFixture<CellsFixture>
{
    private const string Setup = """
        [
          {"op":"set_values","sheet":"Data","range":"A5","values":[["Item","Qty","Price","Region"],["a",2,3,"East"],["b",4,5,"West"]]},
          {"op":"insert_chart","sheet":"Data","kind":"column","dataRange":"A5:C7","at":"F2:L12","title":"Sales"},
          {"op":"add_comment","sheet":"Data","cell":"A1","text":"Note","author":"Ann"}
        ]
        """;

    private readonly CellsFixture _fixture;

    public CellsSameValueTests(CellsFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Every operation the table covers, each written once with what the workbook already holds
    /// and with one thing changed, with the object that change must count at: the same-value
    /// form must count 0 and every changed form more, so a comparison too loose to see a change
    /// fails as surely as one too strict to see none.
    /// </summary>
    private static readonly (string Same, (string Changed, string Target)[] Changes)[] Cases =
    [
        ("""{"op":"set_values","sheet":"Data","range":"B6","values":[[2]]}""",
            [("""{"op":"set_values","sheet":"Data","range":"B6","values":[[3]]}""", "cell/Data!B6")]),
        ("""{"op":"set_formula","sheet":"Data","range":"E6:E7","formula":"=B6*C6"}""",
            [("""{"op":"set_formula","sheet":"Data","range":"E6:E7","formula":"=B6+C6"}""", "cell/Data!E6")]),
        ("""{"op":"format_range","sheet":"Data","range":"B6:C7","style":{"numberFormat":"#,##0.00","bold":true}}""",
            [("""{"op":"format_range","sheet":"Data","range":"B6:C7","style":{"numberFormat":"0.0","bold":true}}""", "cell/Data!B6"),
             ("""{"op":"format_range","sheet":"Data","range":"B6:C7","style":{"numberFormat":"#,##0.00","bold":false}}""", "cell/Data!C7")]),
        ("""{"op":"set_borders","sheet":"Data","range":"A5:C7","edges":["outline"],"lineStyle":"medium","color":"#1F4E79"}""",
            [("""{"op":"set_borders","sheet":"Data","range":"A5:C7","edges":["outline"],"lineStyle":"medium","color":"#FF0000"}""", "cell/Data!A5"),
             ("""{"op":"set_borders","sheet":"Data","range":"A5:C7","edges":["outline"],"lineStyle":"thin","color":"#1F4E79"}""", "cell/Data!C7")]),
        ("""{"op":"set_validation","sheet":"Data","range":"D6:D7","kind":"list","listItems":["East","West"],"errorMessage":"Pick one"}""",
            [("""{"op":"set_validation","sheet":"Data","range":"D6:D7","kind":"list","listItems":["East","West"],"errorMessage":"Pick a region"}""", "validation/Data!D6:D7"),
             ("""{"op":"set_validation","sheet":"Data","range":"D6:D7","kind":"list","listItems":["East","West","North"],"errorMessage":"Pick one"}""", "validation/Data!D6:D7")]),
        ("""{"op":"set_validation","sheet":"Data","range":"B6:B7","kind":"whole-number","operator":"between","value1":"1","value2":"1000"}""",
            [("""{"op":"set_validation","sheet":"Data","range":"B6:B7","kind":"whole-number","operator":"between","value1":"1","value2":"999"}""", "validation/Data!B6:B7"),
             ("""{"op":"set_validation","sheet":"Data","range":"B6:B7","kind":"whole-number","operator":"between","value1":"1","value2":"1000","errorMessage":"1 to 1000"}""", "validation/Data!B6:B7")]),
        ("""{"op":"set_page_setup","sheet":"Data","orientation":"landscape","pageSize":{"preset":"a4"},"fitToWidth":1,"fitToHeight":0,"headerCenter":"Sales","footerCenter":"Page &P","headerMargin":20,"footerMargin":20,"margins":{"top":54,"bottom":54}}""",
            [("""{"op":"set_page_setup","sheet":"Data","orientation":"portrait","pageSize":{"preset":"a4"},"fitToWidth":1,"fitToHeight":0,"headerCenter":"Sales","footerCenter":"Page &P","headerMargin":20,"footerMargin":20,"margins":{"top":54,"bottom":54}}""", "page-setup/Data"),
             ("""{"op":"set_page_setup","sheet":"Data","orientation":"landscape","pageSize":{"preset":"a4"},"fitToWidth":1,"fitToHeight":0,"headerCenter":"Sales","footerCenter":"Page &P","headerMargin":21,"footerMargin":20,"margins":{"top":54,"bottom":54}}""", "page-setup/Data"),
             ("""{"op":"set_page_setup","sheet":"Data","orientation":"landscape","pageSize":{"preset":"a4"},"fitToWidth":1,"fitToHeight":0,"headerCenter":"Sales","footerCenter":"Page &P","headerMargin":20,"footerMargin":20,"margins":{"top":55,"bottom":54}}""", "page-setup/Data"),
             ("""{"op":"set_page_setup","sheet":"Data","orientation":"landscape","pageSize":{"preset":"a4"},"fitToWidth":1,"fitToHeight":0,"headerCenter":"Totals","footerCenter":"Page &P","headerMargin":20,"footerMargin":20,"margins":{"top":54,"bottom":54}}""", "page-setup/Data")]),
        ("""{"op":"set_page_setup","sheet":"Data","pageSize":{"width":612,"height":792}}""",
            [("""{"op":"set_page_setup","sheet":"Data","pageSize":{"preset":"a4"}}""", "page-setup/Data")]),
        ("""{"op":"set_print_area","sheet":"Data","range":"A1:H25","titleRows":"1:1"}""",
            [("""{"op":"set_print_area","sheet":"Data","range":"A1:H30","titleRows":"1:1"}""", "print-area/Data"),
             ("""{"op":"set_print_area","sheet":"Data","range":"A1:H25","titleRows":"1:2"}""", "print-area/Data")]),
        ("""{"op":"set_tab_color","sheet":"Data","color":"#1F4E79"}""",
            [("""{"op":"set_tab_color","sheet":"Data","color":"#FF0000"}""", "tab-color/Data")]),
        ("""{"op":"set_sheet_view","sheet":"Data","gridlines":false,"zoomPercent":110}""",
            [("""{"op":"set_sheet_view","sheet":"Data","gridlines":false,"zoomPercent":120}""", "sheet-view/Data"),
             ("""{"op":"set_sheet_view","sheet":"Data","gridlines":true,"zoomPercent":110}""", "sheet-view/Data")]),
        ("""{"op":"set_active_sheet","sheet":"Second"}""",
            [("""{"op":"set_active_sheet","sheet":"Data"}""", "workbook/active-sheet")]),
        ("""{"op":"set_default_font","font":"Calibri","fontSize":11}""",
            [("""{"op":"set_default_font","font":"Calibri","fontSize":12}""", "workbook/default-font"),
             ("""{"op":"set_default_font","font":"Arial","fontSize":11}""", "workbook/default-font")]),
        ("""{"op":"set_hyperlink","sheet":"Data","cell":"A9","target":"Second!A1","display":"Details"}""",
            [("""{"op":"set_hyperlink","sheet":"Data","cell":"A9","target":"Second!A1","display":"More"}""", "hyperlink/Data!A9"),
             ("""{"op":"set_hyperlink","sheet":"Data","cell":"A9","target":"Second!B2","display":"Details"}""", "hyperlink/Data!A9")]),
        ("""{"op":"set_hyperlink","sheet":"Data","cell":"A10","url":"https://example.com/","display":"Site"}""",
            [("""{"op":"set_hyperlink","sheet":"Data","cell":"A10","url":"https://example.org/","display":"Site"}""", "hyperlink/Data!A10")]),
        ("""{"op":"set_comment","sheet":"Data","cell":"A1","text":"Checked","author":"Bob"}""",
            [("""{"op":"set_comment","sheet":"Data","cell":"A1","text":"Rechecked","author":"Bob"}""", "comment/Data!A1"),
             ("""{"op":"set_comment","sheet":"Data","cell":"A1","text":"Checked","author":"Cy"}""", "comment/Data!A1")]),
        ("""{"op":"set_chart","sheet":"Data","index":1,"dataRange":"A5:C7"}""",
            [("""{"op":"set_chart","sheet":"Data","index":1,"dataRange":"A5:B7"}""", "chart/Data/1")]),
        ("""{"op":"set_chart","sheet":"Data","index":1,"title":"Totals","legend":{"position":"right"}}""",
            [("""{"op":"set_chart","sheet":"Data","index":1,"title":"Sums","legend":{"position":"right"}}""", "chart/Data/1"),
             ("""{"op":"set_chart","sheet":"Data","index":1,"title":"Totals","legend":{"position":"bottom"}}""", "chart/Data/1")]),
        ("""{"op":"freeze_panes","sheet":"Data","cell":"A2"}""",
            [("""{"op":"freeze_panes","sheet":"Data","cell":"A3"}""", "freeze-panes/Data")]),
        ("""{"op":"set_autofilter","sheet":"Data","range":"A5:C7"}""",
            [("""{"op":"set_autofilter","sheet":"Data","range":"A5:B7"}""", "autofilter/Data")]),
        ("""{"op":"set_sheet_visibility","sheet":"Second","visibility":"hidden"}""",
            [("""{"op":"set_sheet_visibility","sheet":"Second","visibility":"visible"}""", "sheet/Second")]),
        ("""{"op":"resize_columns","sheet":"Data","first":"B","last":"C","widthChars":14}""",
            [("""{"op":"resize_columns","sheet":"Data","first":"B","last":"C","widthChars":15}""", "column/Data!B")]),
        ("""{"op":"resize_rows","sheet":"Data","first":6,"height":22}""",
            [("""{"op":"resize_rows","sheet":"Data","first":6,"height":23}""", "row/Data!6")]),
        ("""{"op":"define_name","name":"Rate","refersTo":"=Data!$C$6"}""",
            [("""{"op":"define_name","name":"Rate","refersTo":"=Data!$C$7"}""", "name/Rate")]),
        ("""{"op":"merge_cells","sheet":"Data","range":"H20:I21"}""",
            [("""{"op":"merge_cells","sheet":"Data","range":"H20:J21"}""", "merge/Data!H20:J21")]),
    ];

    /// <summary>Each operation of <see cref="Cases"/> as the workbook already holds it.</summary>
    public static TheoryData<string> Operations { get; } = [.. Cases.Select(static item => item.Same)];

    /// <summary>Each operation of <see cref="Cases"/> with one thing changed, after its same-value form, and the object it must count at.</summary>
    public static TheoryData<string, string, string> Changes { get; } = Flatten();

    private static TheoryData<string, string, string> Flatten()
    {
        var rows = new TheoryData<string, string, string>();
        foreach ((string same, (string Changed, string Target)[] changes) in Cases)
        {
            foreach ((string changed, string target) in changes)
            {
                rows.Add(same, changed, target);
            }
        }

        return rows;
    }

    [Fact]
    public void EveryOperationOfTheTable_HasAChangedForm()
    {
        Assert.All(Cases, static item => Assert.NotEmpty(item.Changes));
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public void WritingWhatTheWorkbookHolds_ChangesNothingAndSaysSo(string operation)
    {
        string path = _fixture.CreateSalesWorkbook($"same-{Guid.NewGuid():N}.xlsx");
        string seeded = Edit(path, $$"""{"ops":{{Setup}}}""").Output!.Path;
        string once = Edit(seeded, $$"""{"ops":[{{operation}}]}""").Output!.Path;

        CellsEditResult again = Edit(once, $$"""{"ops":[{{operation}}]}""");

        BoundedOperationOutcome outcome = Assert.Single(again.Applied);
        Assert.True(outcome.ItemsAffected == 0, $"{operation} written again counted {outcome.ItemsAffected}: {string.Join(", ", outcome.Targets)}");
        Assert.Contains(again.Warnings ?? [], static warning => warning.Code.Name == "OP_NO_EFFECT");
    }

    [Theory]
    [MemberData(nameof(Changes))]
    public void ChangingOneThingOfWhatTheWorkbookHolds_CountsTheObject(string operation, string changed, string target)
    {
        string path = _fixture.CreateSalesWorkbook($"one-change-{Guid.NewGuid():N}.xlsx");
        string seeded = Edit(path, $$"""{"ops":{{Setup}}}""").Output!.Path;
        string once = Edit(seeded, $$"""{"ops":[{{operation}}]}""").Output!.Path;

        BoundedOperationOutcome outcome = Assert.Single(Edit(once, $$"""{"ops":[{{changed}}]}""").Applied);

        Assert.True(outcome.ItemsAffected > 0, $"{changed} after {operation} counted {outcome.ItemsAffected}");
        Assert.Contains(target, outcome.Targets);
    }

    private CellsEditResult Edit(string path, string operations) =>
        CellsEdit.Run(_fixture.Session, new CellsEditRequest
        {
            Input = path,
            Batch = CellsOp.Catalog.Parse<CellsOpsBatch>(operations, Aspose.Cli.Generated.ProductJsonContext.Definition),
            Output = TestOutput.At(_fixture.Temp.File($"same-{Guid.NewGuid():N}.xlsx")),
        });
}
