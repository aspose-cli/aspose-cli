using System.Text.Json.Nodes;
using Xunit;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// The C-R39 judgments on results and reads made up for the purpose, so that a judgment that lets
/// a wrong outcome through fails here and not only when a product happens to produce one.
/// </summary>
public sealed class RoundTripJudgmentTests
{
    private static readonly Dictionary<string, RoundTripExemption> NoFields = [];

    private static JsonNode Result(int affected, string[] targets, params string[] warnings) => new JsonObject
    {
        ["applied"] = new JsonArray(new JsonObject
        {
            ["op"] = "set_tab_color",
            ["itemsAffected"] = affected,
            ["targets"] = new JsonArray([.. targets.Select(static target => (JsonNode)JsonValue.Create(target))]),
        }),
        ["warnings"] = new JsonArray([.. warnings.Select(static code => (JsonNode)new JsonObject { ["code"] = code, ["message"] = code })]),
    };

    private static JsonArray Read(string json) => [new JsonObject { ["command"] = "cells inspect", ["result"] = JsonNode.Parse(json) }];

    [Fact]
    public void SameValue_CountedAsAnEffect_IsReported()
    {
        string[] problems = [.. ContractRules.SameValueProblems(0, Result(1, ["sheet/Sheet1"]), string.Empty)];

        Assert.Equal(["written back unchanged, itemsAffected is above 0 and there is no OP_NO_EFFECT warning"], problems);
    }

    [Fact]
    public void SameValue_WithZeroAndNoWarning_IsReported()
    {
        string[] problems = [.. ContractRules.SameValueProblems(0, Result(0, []), string.Empty)];

        Assert.Equal(["written back unchanged, itemsAffected is 0 without an OP_NO_EFFECT warning"], problems);
    }

    [Fact]
    public void SameValue_WithZeroAndTheWarning_Holds() =>
        Assert.Empty(ContractRules.SameValueProblems(0, Result(0, [], "EVALUATION_MODE", "OP_NO_EFFECT"), string.Empty));

    [Fact]
    public void SameValue_ThatFails_IsReported() =>
        Assert.Equal(["written back unchanged, it fails: exit 2 OPS_INVALID"], ContractRules.SameValueProblems(2, null, "exit 2 OPS_INVALID"));

    [Fact]
    public void OneChange_ReportedAsNoEffect_IsReported()
    {
        string[] problems = [.. ContractRules.OneChangeProblems(0, Result(0, [], "OP_NO_EFFECT"), string.Empty, "color", ["sheet/Sheet1"])];

        Assert.Equal(
            [
                "changing color, it warns OP_NO_EFFECT",
                "changing color, targets do not name sheet/Sheet1, whose read changed",
                "changing color, itemsAffected is 0 where the reads changed 1 items",
            ],
            problems);
    }

    [Fact]
    public void OneChange_ThatNamesAnotherItem_IsReported()
    {
        string[] problems = [.. ContractRules.OneChangeProblems(0, Result(1, ["sheet/Sheet2"]), string.Empty, "color", ["sheet/Sheet1"])];

        Assert.Equal(
            [
                "changing color, targets do not name sheet/Sheet1, whose read changed",
                "changing color, targets name sheet/Sheet2, whose read did not change",
            ],
            problems);
    }

    [Fact]
    public void OneChange_ThatNamesOneItemTooMany_IsReported()
    {
        string[] problems = [.. ContractRules.OneChangeProblems(0, Result(2, ["sheet/Sheet1", "sheet/Sheet2"]), string.Empty, "color", ["sheet/Sheet1"])];

        Assert.Equal(
            [
                "changing color, targets name sheet/Sheet2, whose read did not change",
                "changing color, itemsAffected is 2 where the reads changed 1 items",
            ],
            problems);
    }

    [Fact]
    public void OneChange_ThatNoReadSees_IsReported()
    {
        string[] problems = [.. ContractRules.OneChangeProblems(0, Result(0, [], "OP_NO_EFFECT"), string.Empty, "font", [])];

        Assert.Equal(["changing font, it warns OP_NO_EFFECT", "changing font, no read changed"], problems);
    }

    [Fact]
    public void OneChange_OfExactlyTheChangedItems_Holds() =>
        Assert.Empty(ContractRules.OneChangeProblems(0, Result(1, ["property/title"], "EVALUATION_MODE"), string.Empty, "title", ["property/title"]));

    [Fact]
    public void Changes_AreTheLeafPlacesThatDiffer() =>
        Assert.Equal(
            ["cells inspect: workbook.sheets[0].tabColor", "cells inspect: workbook.sheets[1]"],
            ContractRules.Changes(
                Read("""{"workbook":{"sheets":[{"name":"Sheet1","tabColor":"#FF0000"}]}}"""),
                Read("""{"workbook":{"sheets":[{"name":"Sheet1","tabColor":"#00B050"},{"name":"Extra"}]}}""")));

    [Fact]
    public void Changes_OfAReadOfAnotherActiveSheet_AreItsPosition()
    {
        static JsonArray Sheet(string name, int position, string value) => [new JsonObject
        {
            ["command"] = "cells read",
            ["result"] = new JsonObject { ["sheet"] = new JsonObject { ["name"] = name, ["position"] = position, ["cells"] = new JsonArray(new JsonArray(JsonValue.Create(value))) } },
        }];

        string[] other = [.. ContractRules.Changes(Sheet("Sheet1", 1, "a"), Sheet("Extra", 2, "b"))];
        string[] renamed = [.. ContractRules.Changes(Sheet("Sheet1", 1, "a"), Sheet("Data", 1, "a"))];

        Assert.Equal(["cells read: sheet.position"], other);
        Assert.Equal(["workbook/active-sheet"], ContractRules.RoundTripAddress("cells", other[0], Sheet("Extra", 2, "b"), Sheet("Sheet1", 1, "a")));
        Assert.Equal(["cells read: sheet.name"], renamed);
        Assert.Equal(["sheet/Data"], ContractRules.RoundTripAddress("cells", renamed[0], Sheet("Data", 1, "a"), Sheet("Sheet1", 1, "a")));
    }

    [Fact]
    public void ReadsBackAt_ItsPlace_AMovedPlace_OrFalseAsAbsence()
    {
        JsonArray staged = Read("""{"styles":{"s0":{"bold":true,"fontSize":14}}}""");

        Assert.True(ContractRules.ReadsBackAt(staged, Read("""{"styles":{"s0":{"bold":true,"fontSize":11}}}"""), "cells inspect: styles.s0.fontSize", "fontSize", JsonNode.Parse("11")));
        Assert.True(ContractRules.ReadsBackAt(staged, Read("""{"styles":{"s0":{"bold":true,"fontSize":14},"s1":{"fontSize":11}}}"""), "cells inspect: styles.s0.fontSize", "fontSize", JsonNode.Parse("11")));
        Assert.True(ContractRules.ReadsBackAt(staged, Read("""{"styles":{"s0":{"fontSize":14}}}"""), "cells inspect: styles.s0.bold", "bold", JsonValue.Create(false)));
        Assert.False(ContractRules.ReadsBackAt(staged, Read("""{"styles":{"s0":{"bold":true,"fontSize":14}}}"""), "cells inspect: styles.s0.fontSize", "fontSize", JsonNode.Parse("11")));
        Assert.False(ContractRules.ReadsBackAt(staged, Read("""{"styles":{}}"""), "cells inspect: styles.s0.bold", "bold", JsonValue.Create(false)));
    }

    [Fact]
    public void Addresses_NameTheItemOfAChangedPlace_AsTargetsDo()
    {
        JsonArray inspect = Read("""{"workbook":{"sheetCount":1,"layouts":[{"sheet":"Sales","tabColor":{"color":"#00B050"},"rowGroups":[{"first":2,"last":3}]}]}}""");

        Assert.Equal(["tab-color/Sales"], ContractRules.RoundTripAddress("cells", "cells inspect: workbook.layouts[0].tabColor.color", inspect, inspect));
        Assert.Equal(["row/Sales!2", "row/Sales!3"], ContractRules.RoundTripAddress("cells", "cells inspect: workbook.layouts[0].rowGroups[0].collapsed", inspect, inspect));
        Assert.Empty(ContractRules.RoundTripAddress("cells", "cells inspect: workbook.sheetCount", inspect, inspect)!);
        Assert.Null(ContractRules.RoundTripAddress("cells", "cells inspect: workbook.somethingNew", inspect, inspect));

        JsonArray evaluation = Read("""{"workbook":{"sheets":[{"name":"Sales"},{"name":"Evaluation Warning (2)"}],"layouts":[{"sheet":"Sales"},{"sheet":"Evaluation Warning (2)"}]}}""");
        Assert.Empty(ContractRules.RoundTripAddress("cells", "cells inspect: workbook.layouts[1]", evaluation, evaluation)!);
        Assert.Equal(["sheet/Sales"], ContractRules.RoundTripAddress("cells", "cells inspect: workbook.sheets[0].hidden", evaluation, evaluation));
    }

    [Fact]
    public void Addresses_OfAStyle_AreTheCellsThatUseIt()
    {
        static JsonArray Cells(string bold) => ContractRules.ResolveStyles([new JsonObject
        {
            ["command"] = "cells read",
            ["result"] = JsonNode.Parse("""{"sheet":{"name":"Sheet1","range":"B2:C2","cells":[[{"v":1,"styleId":"s0"},{"v":2,"styleId":"s1"}]]},"styles":{"s0":{"bold":true},"s1":{"bold":""" + bold + "}}}"),
        }]);
        JsonArray before = Cells("true");
        JsonArray after = Cells("false");

        string[] changes = [.. ContractRules.Changes(before, after)];

        Assert.Equal(["cells read: sheet.cells[0][1].styleId.bold"], changes);
        Assert.Equal(["cell/Sheet1!C2"], ContractRules.RoundTripAddress("cells", changes[0], after, before));
    }

    [Fact]
    public void Exemption_HidesOnlyItsPart_AndFailsWhenThePartHolds()
    {
        var exemption = new RoundTripExemption(RoundTripPart.SameValue, "adds an object");
        RoundTripProblem[] failing = [new(RoundTripPart.SameValue, null, null), new(RoundTripPart.SameValue, null, "same"), new(RoundTripPart.OneChange, "color", null), new(RoundTripPart.OneChange, "color", "change")];
        RoundTripProblem[] holding = [new(RoundTripPart.SameValue, null, null), new(RoundTripPart.OneChange, "color", null)];

        Assert.Equal(["change"], ContractRules.Excused(failing, exemption, NoFields));
        Assert.Equal(["the exemption of SameValue no longer applies: it holds"], ContractRules.Excused(holding, exemption, NoFields));
        Assert.Empty(ContractRules.Excused([], exemption, NoFields));
        Assert.Equal(["same", "change"], ContractRules.Excused(failing, null, NoFields));
    }

    [Fact]
    public void FieldExemption_HidesThatFieldsChangeAlone_AndFailsWhenItHolds()
    {
        var fields = new Dictionary<string, RoundTripExemption> { ["name"] = new(RoundTripPart.OneChange, "key") };
        RoundTripProblem[] failing = [new(RoundTripPart.OneChange, "name", null), new(RoundTripPart.OneChange, "name", "renamed"), new(RoundTripPart.OneChange, "refersTo", "moved")];
        RoundTripProblem[] holding = [new(RoundTripPart.OneChange, "name", null)];
        RoundTripProblem[] unwritten = [new(RoundTripPart.OneChange, "name", "name is written by no sample, so it does not round-trip")];

        Assert.Equal(["moved"], ContractRules.Excused(failing, null, fields));
        Assert.Equal(["the exemption of name no longer applies: changing it holds"], ContractRules.Excused(holding, null, fields));
        Assert.Empty(ContractRules.Excused(unwritten, null, fields));
    }

    [Fact]
    public void ReadBackExemption_HidesOnlyTheReadBack()
    {
        var fields = new Dictionary<string, RoundTripExemption> { ["locked"] = new(RoundTripPart.ReadBack, "read leaves it out") };
        RoundTripProblem[] failing = [new(RoundTripPart.OneChange, "locked", null), new(RoundTripPart.ReadBack, "locked", "reads nothing"), new(RoundTripPart.OneChange, "locked", "targets wrong")];
        RoundTripProblem[] holding = [new(RoundTripPart.OneChange, "locked", null)];
        RoundTripProblem[] excused = [new(RoundTripPart.OneChange, "locked", null), new(RoundTripPart.ReadBack, "locked", "reads nothing")];

        Assert.Equal(["targets wrong"], ContractRules.Excused(failing, null, fields));
        Assert.Equal(["the exemption of locked no longer applies: changing it holds"], ContractRules.Excused(holding, null, fields));
        Assert.Empty(ContractRules.Excused(excused, null, fields));
    }
}
