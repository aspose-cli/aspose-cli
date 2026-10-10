using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.ViewerService;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Aspose.Cli.Host.Tests;

/// <summary>
/// A document password the Host carries from the CLI to the viewer service and on to its render
/// worker is redacted wherever a request is printed, yet still reaches the other process over
/// the local frames. The requests are built from their wire JSON, the form the peer receives.
/// </summary>
public sealed class HostSecretTests : IDisposable
{
    private const string Sentinel = "S3cr3t-sentinel";
    private const string PasswordVariable = "ASPOSE_CLI_TEST_PREVIEW_PASSWORD";
    private static readonly string HostRoot = Path.Combine(RepositoryPaths.Root, "src", "Aspose.Cli.Host");
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void Requests_DoNotPrintThePassword()
    {
        Assert.DoesNotContain(Sentinel, OpenRequest(Sentinel).ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Sentinel, RenderRequest(Sentinel).ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Sentinel, Options(Sentinel).ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ViewerOpenRequest_CarriesThePasswordToTheService()
    {
        string payload = JsonSerializer.Serialize(OpenRequest(Sentinel), ViewerServiceJsonContext.Default.ViewerOpenRequest);
        ViewerOpenRequest received = JsonSerializer.Deserialize(payload, ViewerServiceJsonContext.Default.ViewerOpenRequest)!;

        Assert.Equal(Sentinel, received.Password?.ToSecret().Reveal());
    }

    [Fact]
    public async Task RenderWorkerRequest_CarriesThePasswordToTheWorker()
    {
        using var frames = new MemoryStream();
        await ProcessPipeMessages.WriteAsync(frames, RenderRequest(Sentinel), CancellationToken.None);
        frames.Position = 0;
        RenderWorkerRequest received = await ProcessPipeMessages.ReadAsync<RenderWorkerRequest>(frames, CancellationToken.None);

        Assert.Equal(Sentinel, received.Password?.ToSecret().Reveal());
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void RenderWorker_OpensAnEncryptedDocumentWithThePassword()
    {
        File.WriteAllText(_workspace.File("content.txt"), "Encrypted preview fixture");
        CliResult created = _workspace.RunWithEnv(
            new Dictionary<string, string?> { [PasswordVariable] = Sentinel },
            "words", "create", "secret.docx", "--text", "content.txt", "--encrypt-env", PasswordVariable, "--output", "json");
        Assert.True(created.ExitCode == 0, created.StdErr);
        using var supervisor = new RenderWorkerSupervisor(() => _workspace.StartInfo(), TimeSpan.FromMinutes(2));

        RenderWorkerResponse locked = supervisor.Render(RenderRequest(null, _workspace.File("secret.docx"), Output()));
        RenderWorkerResponse opened = supervisor.Render(RenderRequest(Sentinel, _workspace.File("secret.docx"), Output()));

        Assert.False(locked.Ok);
        Assert.Equal(ErrorCodes.PasswordRequired.Name, locked.Code);
        Assert.True(opened.Ok, $"{opened.Code} {opened.Message}");
    }

    [Fact]
    public void LiveDocument_ReusesOnlyTheSamePassword()
    {
        File.WriteAllText(_workspace.File("watched.txt"), "fixture");
        using var supervisor = new RenderWorkerSupervisor(() => _workspace.StartInfo(), TimeSpan.FromMinutes(2));
        using var document = new LiveDocument(
            "doc", _workspace.File("watched.txt"), _workspace.File("root"), supervisor, Options(Sentinel),
            LocalServiceResourceLimits.Resolve());

        Assert.True(document.Matches(Options(Sentinel)));
        Assert.False(document.Matches(Options("other-password")));
        Assert.False(document.Matches(Options(null)));
    }

    [Fact]
    public void LocalServiceRecords_DoNotPrintTheToken()
    {
        var request = new LocalServiceControlRequest(1, "request", "viewer", "instance", "nonce", Token(Sentinel), "ping");
        var start = new ServiceStartSecrets { WorkDirectory = "work", ServiceToken = Token(Sentinel), ServiceNonce = "nonce" };
        var marker = new ViewerServiceMarker("id", Token(Sentinel), 1, 2, 3, "http://127.0.0.1:3/", "nonce");
        var secrets = new ViewerServiceSecrets(Token(Sentinel));

        Assert.All(new object[] { request, start, marker, secrets },
            record => Assert.DoesNotContain(Sentinel, record.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task LocalServiceFrames_KeepTheTokenAsAJsonString()
    {
        var request = new LocalServiceControlRequest(1, "request", "viewer", "instance", "nonce", Token(Sentinel), "ping");
        byte[] payload = LocalServiceControlCodec.Serialize(request);
        Assert.Equal(Sentinel, JsonNode.Parse(payload)!["token"]!.GetValue<string>());
        LocalServiceControlRequest received = JsonSerializer.Deserialize(payload, LocalServiceJsonContext.Default.LocalServiceControlRequest)!;
        Assert.Equal(Sentinel, Reveal(received.Token));

        using var frames = new MemoryStream();
        await ProcessPipeMessages.WriteAsync(
            frames, new ServiceStartSecrets { WorkDirectory = "work", ServiceToken = Token(Sentinel), ServiceNonce = "nonce" },
            CancellationToken.None);
        Assert.Equal(Sentinel, JsonNode.Parse(frames.ToArray().AsSpan(sizeof(int)))!["serviceToken"]!.GetValue<string>());
        frames.Position = 0;
        ServiceStartSecrets start = await ProcessPipeMessages.ReadAsync<ServiceStartSecrets>(frames, CancellationToken.None);
        Assert.Equal(Sentinel, Reveal(start.ServiceToken));
    }

    [Fact]
    public void ViewerServiceStore_KeepsTheTokenOnlyInTheSecretFile()
    {
        string directory = _workspace.File("viewer");
        var store = new ViewerServiceStore(directory);

        store.Write(new ViewerServiceMarker("id", Token(Sentinel), 1, 2, 3, "http://127.0.0.1:3/", "nonce"));

        Assert.DoesNotContain(Sentinel, File.ReadAllText(Path.Combine(directory, "service.json")), StringComparison.Ordinal);
        Assert.Equal(
            new JsonObject { ["token"] = Sentinel }.ToJsonString(),
            JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "service.secret")))!.ToJsonString());
        Assert.Equal(Sentinel, Reveal(store.ReadCandidate()?.Token));
        store.DeleteIfOwned("other-token");
        Assert.NotNull(store.ReadCandidate());
        store.DeleteIfOwned(Sentinel);
        Assert.Null(store.ReadCandidate());
    }

    [Fact]
    public void HostSource_HoldsNoPasswordAsAString()
    {
        string[] violations = [.. HostMembers()
            .Where(static member => IsString(member.Type) && member.Name.EndsWith("Password", StringComparison.OrdinalIgnoreCase))
            .Select(static member => member.Location)];

        Assert.True(violations.Length == 0,
            "The Host carries a password as a redacting secret type, never as a string:" + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void HostRecords_HoldNoTokenAsAString()
    {
        string[] violations = [.. HostMembers()
            .Where(static member => member.InRecord && IsString(member.Type) && member.Name.EndsWith("Token", StringComparison.Ordinal))
            .Select(static member => member.Location)];

        Assert.True(violations.Length == 0,
            "A Host record carries a token as a redacting secret type, never as a string, so printing the record never shows it:"
            + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    /// <summary>Every property, field, local and parameter the Host declares with a type.</summary>
    private static IEnumerable<(TypeSyntax Type, string Name, bool InRecord, string Location)> HostMembers() =>
        Directory.GetFiles(HostRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .SelectMany(static path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path).GetRoot().DescendantNodes()
                .SelectMany(static node => node switch
                {
                    PropertyDeclarationSyntax property => [(node, property.Type, property.Identifier.ValueText)],
                    FieldDeclarationSyntax field => field.Declaration.Variables
                        .Select(variable => ((SyntaxNode)variable, field.Declaration.Type, variable.Identifier.ValueText)),
                    LocalDeclarationStatementSyntax local => local.Declaration.Variables
                        .Select(variable => ((SyntaxNode)variable, local.Declaration.Type, variable.Identifier.ValueText)),
                    ParameterSyntax { Type: { } type } parameter => [(node, type, parameter.Identifier.ValueText)],
                    _ => Array.Empty<(SyntaxNode Node, TypeSyntax Type, string Name)>(),
                })
                .Select(member => (
                    member.Item2,
                    member.Item3,
                    member.Item1 switch
                    {
                        PropertyDeclarationSyntax property => property.Parent is RecordDeclarationSyntax,
                        ParameterSyntax parameter => parameter.Parent?.Parent is RecordDeclarationSyntax,
                        _ => false,
                    },
                    $"{Path.GetRelativePath(HostRoot, path)}:{member.Item1.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: string {member.Item3}")));

    private static WireSecret Token(string value) => new(value);

    private static string? Reveal(WireSecret? token) => token?.Reveal();

    private static bool IsString(TypeSyntax type) => type switch
    {
        PredefinedTypeSyntax predefined => predefined.Keyword.IsKind(SyntaxKind.StringKeyword),
        NullableTypeSyntax nullable => IsString(nullable.ElementType),
        _ => false,
    };

    private string Output() =>
        Directory.CreateDirectory(_workspace.File(Path.Combine("out", Guid.NewGuid().ToString("N")))).FullName;

    private static ViewerOpenRequest OpenRequest(string? password) =>
        Wire<ViewerOpenRequest>(new JsonObject { ["file"] = "secret.docx", ["password"] = password });

    private static RenderWorkerRequest RenderRequest(string? password, string source = "secret.docx", string output = "out") =>
        Wire<RenderWorkerRequest>(new JsonObject
        {
            ["id"] = 1,
            ["source"] = source,
            ["output"] = output,
            ["maxPartCount"] = 8,
            ["timeoutMs"] = 120_000,
            ["password"] = password,
        });

    private static LiveDocumentOptions Options(string? password) =>
        Wire<LiveDocumentOptions>(new JsonObject { ["password"] = password });

    /// <summary>Reads a request from the JSON the peer process receives.</summary>
    private static T Wire<T>(JsonObject json) => json.Deserialize<T>(JsonSerializerOptions.Web)!;
}
