using System.Globalization;
using System.Text;

namespace Aspose.Cli.CodeHealth;

/// <summary>The command line of the code health report, and its entry point.</summary>
internal static class CodeHealthCommand
{
    private const string Usage = """
        Reports code-health metrics of the repository's C# source. A diagnostic: nothing fails on its numbers.

        Usage: dotnet run -c Release --project eng/tools/CodeHealth -- [options]

          --base <ref>       Compare the working tree with <ref> (read from git, the working tree is
                             untouched) and list new, worse, better and removed members and files.
          --top <n>          Keep the first n entries of each list (default 30; 0 keeps all).
          --json             Write JSON instead of Markdown.
          --history <range>  The git log revision range for hotspots (default HEAD, the whole history).
          --source <dir>     The repository directory to measure (default src).
          -h, --help         Show this help.
        """;

    /// <summary>Parsed command-line options.</summary>
    internal sealed record Options(string? Base, int Top, bool Json, string History, string Source);

    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        if (args.Any(arg => arg is "-h" or "--help" or "-?"))
        {
            Console.Out.Write(Usage + "\n");
            return 0;
        }
        try
        {
            Options options = Parse(args);
            Console.OutputEncoding = new UTF8Encoding(false);
            Console.Out.Write(Run(options, Git.FindRoot(Environment.CurrentDirectory)));
            return 0;
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine("Run with --help for usage.");
            return 2;
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    /// <summary>The report text for <paramref name="options"/> on the repository at <paramref name="root"/>.</summary>
    internal static string Run(Options options, string root)
    {
        Git git = new(root);
        string source = options.Source.Replace('\\', '/').Trim('/');
        Snapshot head = Snapshot.Measure(WorkingTree(root, source));
        if (options.Base is { } baseRevision)
        {
            Snapshot baseline = Snapshot.Measure(git.ReadTree(baseRevision, source));
            CompareReport comparison = CompareReport.Create(source, baseRevision, "working tree", Comparison.Create(baseline, head), options.Top);
            return options.Json ? ReportWriter.Json(comparison) : ReportWriter.Markdown(comparison);
        }
        MeasureReport report = MeasureReport.Create(source, "working tree", options.History, head, git.History(options.History, source), options.Top);
        return options.Json ? ReportWriter.Json(report) : ReportWriter.Markdown(report);
    }

    /// <summary>Parses <paramref name="args"/>; a mistake throws <see cref="ArgumentException"/>.</summary>
    internal static Options Parse(IReadOnlyList<string> args)
    {
        Options options = new(null, 30, false, "HEAD", "src");
        for (int index = 0; index < args.Count; index++)
        {
            string arg = args[index];
            options = arg switch
            {
                "--json" => options with { Json = true },
                "--base" => options with { Base = Value(args, ref index) },
                "--history" => options with { History = Value(args, ref index) },
                "--source" => options with { Source = Value(args, ref index) },
                "--top" => options with { Top = Count(Value(args, ref index)) },
                _ => throw new ArgumentException($"Unknown argument '{arg}'."),
            };
        }
        return options;
    }

    private static string Value(IReadOnlyList<string> args, ref int index)
    {
        string name = args[index];
        if (++index >= args.Count || args[index].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"{name} needs a value.");
        }
        return args[index];
    }

    private static int Count(string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int count)
            ? count
            : throw new ArgumentException($"--top needs a whole number, not '{value}'.");

    /// <summary>The <c>.cs</c> files under <paramref name="source"/> on disk, outside <c>bin</c> and <c>obj</c>.</summary>
    private static IEnumerable<(string Path, string Text)> WorkingTree(string root, string source)
    {
        string directory = Path.Combine(root, source);
        if (!Directory.Exists(directory))
        {
            throw new ArgumentException($"The source directory does not exist: {directory}");
        }
        return Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Where(path => !path.Split('/').Any(segment => segment is "bin" or "obj"))
            .Select(path => (path, File.ReadAllText(Path.Combine(root, path), Encoding.UTF8)));
    }
}
