using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// A read reports a custom property under the name the batch wrote it with, so the name writes
/// back as it is; only its target address escapes it. A failed operation's targets are the
/// addresses it attempted: the block or section numbers it names, which need not exist.
/// </summary>
public sealed class WordsAddressNameTests
{
    [Fact]
    public void CustomProperties_ReadBackUnderTheirOwnNames_AndOnlyTheirTargetsEscapeThem()
    {
        using var fixture = new WordsFixture();
        var custom = new Dictionary<string, string?>
        {
            ["rate%"] = "5",
            ["项目/编号"] = "A-1",
            [" lead "] = "x",
        };

        WordsEditResult written = Edit(fixture, fixture.CreateReport(), new SetPropertiesOp { Custom = custom });
        IReadOnlyDictionary<string, string?> read = WordsInspect.Run(fixture.Session, new WordsInspectRequest
        {
            Input = written.Output!.Path,
            Details = [WordsInspectDetails.Properties],
        }).Properties!;

        Assert.Equal(
            ["property/%20lead%20", "property/rate%25", "property/项目%2F编号"],
            Assert.Single(written.Applied).Targets.Order(StringComparer.Ordinal));
        Assert.Equal(custom, custom.Keys.ToDictionary(static key => key, key => read[key]));

        WordsEditResult again = Edit(fixture, written.Output.Path, new SetPropertiesOp
        {
            Custom = custom.Keys.ToDictionary(static key => key, key => read[key]),
        });

        BoundedOperationOutcome outcome = Assert.Single(again.Applied);
        Assert.Equal((OpStatuses.Ok, 0L), (outcome.Status, outcome.ItemsAffected));
        Assert.Contains(again.Warnings!, static warning => warning.Code.Name == "OP_NO_EFFECT");
    }

    [Fact]
    public void NamesThatNeedEscaping_TargetTheAddressesTheReadGivesTheirObjects()
    {
        using var fixture = new WordsFixture();
        const string name = " Q3 / plan 100% ";
        WordsEditResult result = fixture.Disclosed(session => WordsEdit.Run(session, new WordsEditRequest
        {
            Input = fixture.CreateReport(),
            Batch = new WordsOpsBatch
            {
                Ops =
                [
                    new DefineStyleOp { Name = name, Bold = true },
                    new SetPropertiesOp { Custom = new Dictionary<string, string?> { [name] = "x" } },
                ],
            },
            Output = TestOutput.At(fixture.Temp.File("escaped.docx")),
            Verify = true,
        }));
        WordsInspectResult read = WordsInspect.Run(fixture.Session, new WordsInspectRequest
        {
            Input = result.Output!.Path,
            Details = [WordsInspectDetails.Styles, WordsInspectDetails.Properties],
        });

        string style = Assert.Single(read.Styles!, static style => style.Name.Contains("plan 100%", StringComparison.Ordinal)).Name;
        string property = Assert.Single(read.Properties!.Keys, static key => key.Contains("plan 100%", StringComparison.Ordinal));
        Assert.Equal(
            [[$"style/{TargetName.Escape(style)}"], [$"property/{TargetName.Escape(property)}"]],
            result.Applied.Select(static outcome => outcome.Targets.ToArray()));
        Assert.True(result.Verification!.Ok);
    }

    private static WordsEditResult Edit(WordsFixture fixture, string input, params WordsOp[] ops) =>
        fixture.Disclosed(session => WordsEdit.Run(session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch { Ops = ops },
            Output = TestOutput.At(fixture.Temp.File($"edited-{Guid.NewGuid():N}.docx")),
        }));
}
