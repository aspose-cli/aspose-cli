using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;
using Aspose.Slides;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

public sealed class SlidesMutationAndSecurityTests
{
    /// <summary>A batch whose last operation fails writes nothing and leaves its input as it was.</summary>
    [Fact]
    public void AFailedBatch_WritesNothingAndLeavesTheInputAsItWas()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("deletions.pptx", slides: 3);
        byte[] original = File.ReadAllBytes(input);
        string output = fixture.File("deletions.out.pptx");
        string slide = $"slide/{SlideId(input, 0)}";

        CliException error = Assert.Throws<CliException>(() => SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = TestBatch.Parse([new JsonObject { ["delete"] = slide }, new JsonObject { ["delete"] = slide }]),
            Output = TestOutput.At(output),
        }));

        Assert.Equal(ErrorCodes.AddressNotFound, error.Code);
        Assert.Equal(1, error.Details!["index"]!.GetValue<int>());
        Assert.False(File.Exists(output));
        Assert.Equal(original, File.ReadAllBytes(input));
    }

    [Fact]
    public void PasswordProtectedPresentation_RequiresTheCorrectPassword()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation(
            "protected.pptx",
            slides: 1,
            password: "correct");

        CliException missing = Assert.Throws<CliException>(() =>
            SlidesRead.Run(fixture.Session, new SlidesReadRequest { Input = input }));
        CliException wrong = Assert.Throws<CliException>(() =>
            SlidesRead.Run(
                fixture.Session,
                new SlidesReadRequest { Input = input, Password = new Secret("wrong") }));
        SlidesReadResult opened = SlidesRead.Run(
            fixture.Session,
            new SlidesReadRequest { Input = input, Password = new Secret("correct"), Kinds = ["slide"] });

        Assert.Equal(ErrorCodes.PasswordRequired, missing.Code);
        Assert.Equal(ErrorCodes.PasswordInvalid, wrong.Code);
        Assert.True(opened.Input.Encrypted);
        Assert.Single(opened.Objects);
    }

    [Fact]
    public void EncryptedEdit_ReopensWithTheNewPassword()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation();
        string output = fixture.File("encrypted.pptx");

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = TestBatch.Parse([new JsonObject { ["set"] = "presentation", ["props"] = new JsonObject { ["title"] = "Safe" } }]),
            Output = TestOutput.At(output),
            EncryptPassword = new Secret("correct"),
        });

        Assert.True(result.Output.Encrypted);
        Assert.Equal(ErrorCodes.PasswordRequired, Assert.Throws<CliException>(() =>
            SlidesRead.Run(fixture.Session, new SlidesReadRequest { Input = output })).Code);
        SlidesReadResult read = SlidesRead.Run(
            fixture.Session,
            new SlidesReadRequest { Input = output, Password = new Secret("correct"), At = "presentation" });
        Assert.Equal(3, read.Inventory.Read["slides"]);
        Assert.Equal("Safe", read.Objects[0].Props["title"]!.GetValue<string>());
    }

    /// <summary>
    /// An exception the engine raises inside an operation, such as Aspose.Slides refusing to insert
    /// a slide past the end of the deck, is reported as it is, with the operation's index, and
    /// nothing is written.
    /// </summary>
    [Fact]
    public void AnEngineFailureInsideAnOperation_NamesTheOperation()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("past-the-end.pptx", slides: 1);
        uint layout;
        using (var presentation = new Presentation(input))
        {
            layout = presentation.LayoutSlides[0].SlideId;
        }

        string output = fixture.File("past-the-end.out.pptx");

        CliException error = Assert.Throws<CliException>(() => SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = TestBatch.Parse(
            [
                new JsonObject { ["set"] = "presentation", ["props"] = new JsonObject { ["title"] = "First" } },
                new JsonObject { ["add"] = "slide", ["args"] = new JsonObject { ["index"] = 99, ["layout"] = layout } },
            ]),
            Output = TestOutput.At(output),
        }));

        Assert.Equal(ErrorCodes.EngineError, error.Code);
        Assert.Equal(1, error.Details!["index"]!.GetValue<int>());
        Assert.False(File.Exists(output));
    }

    private static uint SlideId(string path, int index)
    {
        using var presentation = new Presentation(path);
        return presentation.Slides[index].SlideId;
    }
}
