using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Tables;
using Aspose.Words;
using Aspose.Words.Saving;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// Shared real-engine setup. The test license state is applied before any test authors a
/// document, so inputs are written in the state the engine reads them in.
/// </summary>
/// <remarks>
/// Aspose.Words evaluation mode writes a paragraph at the start of every document it opens and
/// cuts long documents short, so the tests that call the engine in this process and compare with
/// it run licensed only. The CLI suites cover evaluation mode, where every command is a fresh process.
/// </remarks>
public sealed class WordsFixture : IDisposable
{
    private const string EvaluationChanges =
        "Aspose.Words evaluation mode adds a paragraph to every document it opens and cuts long ones short, which the in-process engine suite compares against.";

    public ILicenseGate Gate { get; } = TestLicense.Apply(WordsActivation.ApplyLicense);

    /// <summary>The session of one invocation, as activation builds it.</summary>
    internal WordsSession Session
    {
        get
        {
            TestLicense.Require(EvaluationChanges);
            return ProductTestBudgets.StartEngine<WordsModule, WordsSession>(
                (budgets, writer) => WordsActivation.Session(new OutputPipeline(Gate, new WordsEvaluationProfile(), writer), budgets));
        }
    }

    internal WordsFontEnvironment Fonts =>
        ProductTestBudgets.StartEngine<WordsModule, WordsFontEnvironment>((_, _) => new WordsFontEnvironment());

    public LicenseState LicenseState => Gate.EnsureApplied();

    /// <summary>Skips a test that calls the engine in this process without a license; call it before the test opens a document.</summary>
    public static void RequireLicense() => TestLicense.Require(EvaluationChanges);

    public TempDirectory Temp { get; } = new();

    public string File(string name) => Temp.File(name);

    /// <summary>
    /// The document of the common tasks: a title, three headings each with body paragraphs, a
    /// 4 by 3 milestone table whose empty cells hold a paragraph without a run, and a last
    /// heading and paragraph; no header or footer.
    /// </summary>
    public string CreateProposal(string name = "proposal.docx")
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        void Paragraph(string style, string text)
        {
            builder.ParagraphFormat.StyleName = style;
            builder.Writeln(text);
        }

        Paragraph("Title", "Contoso 项目建议书");
        Paragraph("Heading 1", "背景 Background");
        Paragraph("Normal", "Contoso 计划在 2026 年升级客户门户。");
        Paragraph("Heading 1", "范围 Scope");
        Paragraph("Normal", "本期覆盖登录、订单查询和工单三个模块。");
        Paragraph("Normal", "Contoso 现有的移动端不在本期范围内。");
        Paragraph("Heading 1", "里程碑 Milestones");
        string[][] rows = [["阶段 Phase", "日期 Date", "负责人 Owner"], ["设计 Design", "2026-03-01", ""], ["开发 Build", "", ""], ["上线 Launch", "", ""]];
        builder.StartTable();
        foreach (string[] row in rows)
        {
            foreach (string text in row)
            {
                builder.InsertCell();
                builder.Write(text);
            }

            builder.EndRow();
        }

        builder.EndTable();
        Paragraph("Heading 1", "预算 Budget");
        builder.ParagraphFormat.StyleName = "Normal";
        builder.Write("Contoso 预计投入 120 万元。");
        string path = File(name);
        document.Save(path, SaveFormat.Docx);
        return path;
    }

    /// <summary>A request of <c>words edit</c> over a parsed operation document.</summary>
    internal static WordsEditRequest Edit(string input, string output, string ops) => new()
    {
        Input = input,
        Batch = TableBatch.Parse(ops, WordsTable.Definition, new Inputs()),
        Output = new EditOutput(output),
    };

    /// <summary>The files and secrets of a batch, as the command line reads them.</summary>
    internal sealed class Inputs : ITableInputs
    {
        public string ResolvePath(string path) => Path.GetFullPath(path);

        public byte[] ReadImage(string path) => System.IO.File.ReadAllBytes(path);


        public byte[] ReadFile(string path) => System.IO.File.ReadAllBytes(path);

        public Secret? ReadSecret(string variable) => Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value ? new Secret(value) : null;
    }

    /// <summary>The text of each line the page layout of a document lays out, in order.</summary>
    internal static IReadOnlyList<string> LayoutLines(Document document)
    {
        document.UpdatePageLayout();
        var lines = new List<string>();
        var enumerator = new Aspose.Words.Layout.LayoutEnumerator(document);
        Walk();
        return lines;

        void Walk()
        {
            do
            {
                if (enumerator.Type == Aspose.Words.Layout.LayoutEntityType.Line)
                {
                    var line = new System.Text.StringBuilder();
                    if (enumerator.MoveFirstChild())
                    {
                        do
                        {
                            line.Append(enumerator.Type == Aspose.Words.Layout.LayoutEntityType.Span ? enumerator.Text : string.Empty);
                        }
                        while (enumerator.MoveNext());
                        enumerator.MoveParent();
                    }

                    lines.Add(line.ToString());
                }
                else if (enumerator.MoveFirstChild())
                {
                    Walk();
                    enumerator.MoveParent();
                }
            }
            while (enumerator.MoveNext());
        }
    }

    public void Dispose() => Temp.Dispose();
}
