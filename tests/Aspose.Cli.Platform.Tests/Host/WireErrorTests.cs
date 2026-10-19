using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Serialization;
using Xunit;

namespace Aspose.Cli.Host.Tests;

/// <summary>An error crosses every Host process boundary whole, suggestions included.</summary>
public sealed class WireErrorTests
{
    [Fact]
    public void ChildStandardError_RoundTripsTheWholeError()
    {
        CliException sent = Sample();
        string stderr = JsonSerializer.Serialize(
            new ErrorEnvelope
            {
                Error = new ErrorPayload
                {
                    Code = sent.Code.Name, Message = sent.Message, Hint = sent.Hint, Details = sent.Details, Docs = sent.Docs,
                },
            },
            SdkJsonContext.Default.ErrorEnvelope);

        AssertSame(sent, LocalServiceChildError.TryRead(stderr, (int)sent.ExitCode));
    }

    internal static CliException Sample() => CliErrors.FromRemote(
        "SHEET_NOT_FOUND",
        ExitCode.InputError,
        "No sheet 'Salez'.",
        "Use one of the sheets in details.suggestions.",
        new JsonObject { ["requested"] = "Salez", ["suggestions"] = new JsonArray("Sales", "Saleé") },
        "cells/sheets");

    internal static void AssertSame(CliException sent, CliException? received)
    {
        Assert.NotNull(received);
        Assert.Equal(
            (sent.Code.Name, sent.ExitCode, sent.Message, sent.Hint, sent.Docs),
            (received.Code.Name, received.ExitCode, received.Message, received.Hint, received.Docs));
        Assert.True(JsonNode.DeepEquals(sent.Details, received.Details), received.Details?.ToJsonString());
        Assert.Equal(["Sales", "Saleé"],
            received.Details?["suggestions"]?.AsArray().Select(item => item!.GetValue<string>()));
    }
}
