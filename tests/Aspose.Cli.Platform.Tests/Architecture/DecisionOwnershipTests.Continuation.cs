using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit.Scenarios;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

public sealed partial class DecisionOwnershipTests
{
    // -- D14 Continuation commands -----------------------------------------------------------

    /// <summary>
    /// A runnable <c>aspose-cli</c> command with a value in it, a path or an option's value, is
    /// built only by the SDK's <c>ContinuationCommand</c>, started by
    /// <c>StandardInvocation.ContinuationAt</c> or <c>ContinuationInputExtensions.ContinuationAt</c>:
    /// it passes on the variable of the input's password, quotes every value and asks for JSON, so an
    /// agent can run it as written. No other code splices a value into one by interpolation,
    /// concatenation, <c>string.Join</c>, a list of tokens or a <c>StringBuilder</c>. Text with
    /// placeholders only, such as <c>'aspose-cli cells inspect &lt;file&gt; --detail comments'</c>,
    /// splices no value, and neither do the command names of a command's path (a product id, a
    /// command's <c>Name</c>, <c>CommandPath()</c>) or the id a <c>schema</c> or <c>docs</c>
    /// command takes, which name vocabulary rather than a document.
    /// </summary>
    [Fact]
    public void D14_RunnableCommandsWithValues_AreBuiltOnlyByContinuationCommand()
    {
        string[] owners =
        [
            Path.Combine(SdkRoot, "Extensibility", "Commanding", "ContinuationCommand.cs"),
        ];
        var reader = new CommandReader(CliCatalog.Current);
        string[] violations = [.. Sources.Value
            .Where(file => !owners.Contains(file.Path, StringComparer.OrdinalIgnoreCase))
            .SelectMany(file => reader.Splices(file.Root).Select(splice => file.At(splice.Value, $"'{splice.Value}' in {Excerpt(splice.Command)}")))
            .Distinct(StringComparer.Ordinal)];

        AssertNone(violations,
            "A runnable aspose-cli command with a path or an option value in it is built by ContinuationCommand, through StandardInvocation.ContinuationAt or ContinuationInputExtensions.ContinuationAt, which passes on the input's password variable, quotes every value and asks for JSON; no code splices a value into one:");
    }

    /// <summary>
    /// A command a hint names says what it writes only through <c>ContinuationCommand.Out</c> or
    /// <c>OutDir</c>, given a <c>ContinuationOutput</c> that says whose file it is, which alone
    /// decides <c>--overwrite</c>: no other code gives a continuation <c>--out</c>, <c>-o</c>,
    /// <c>--out-dir</c> or <c>--overwrite</c>, written out, as a <c>StandardOptionNames</c> constant
    /// or as the <c>Name</c> of the option a command declares.
    /// </summary>
    [Fact]
    public void D14_TheOutputsOfContinuations_AreNamedOnlyThroughContinuationOutput()
    {
        string owner = Path.Combine(SdkRoot, "Extensibility", "Commanding", "ContinuationCommand.cs");
        string[] violations = [.. Sources.Value
            .Where(file => !string.Equals(file.Path, owner, StringComparison.OrdinalIgnoreCase))
            .SelectMany(file => file.Root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(NamesAnOutput)
                .Select(invocation => file.At(invocation, Excerpt(invocation))))];

        AssertNone(violations,
            "A continuation names what it writes with ContinuationCommand.Out or OutDir and a ContinuationOutput, the one rule for --overwrite; no other code gives it --out, --out-dir or --overwrite:");
    }

    /// <summary>The ways of naming an output option of a continuation that D14 reads, and the calls it leaves alone.</summary>
    [Theory]
    [InlineData("""_ = command.Option("--out", path);""", true)]
    [InlineData("""_ = command.Flag("--overwrite", overwrite);""", true)]
    [InlineData("""_ = command.Option(StandardOptionNames.OutDir, folder);""", true)]
    [InlineData("""_ = command.Flag(options.Overwrite.Name);""", true)]
    [InlineData("""_ = command.Flag(options.Overwrite!.Name, overwrite);""", true)]
    [InlineData("""_ = command.Option(options.OutputFile.Option.Name, path);""", true)]
    [InlineData("""_ = command.Option(options?.OutputDirectory?.Option.Name, folder);""", true)]
    [InlineData("""_ = command.Out(ContinuationOutput.New(path));""", false)]
    [InlineData("""_ = command.Option("--to", "pdf");""", false)]
    [InlineData("""_ = command.Option(options.To.Name, "pdf");""", false)]
    public void D14_TheOutputRule_ReadsEachWayOfNamingAnOutputOption(string statements, bool names)
    {
        SyntaxNode root = CSharpSyntaxTree.ParseText($"class C {{ void M() {{ {statements} }} }}").GetRoot();

        Assert.Equal(names, root.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(NamesAnOutput));
    }

    // A call that gives a command line an option with its first argument, as Option, Flag and
    // Document do, when that argument names --out, -o, --out-dir or --overwrite.
    private static bool NamesAnOutput(InvocationExpressionSyntax invocation) =>
        invocation is { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Option" or "Flag" or "Document" } }
        && invocation.ArgumentList.Arguments is [{ Expression: var option }, ..]
        && option switch
        {
            LiteralExpressionSyntax literal => literal.Token.ValueText is "--out" or "-o" or "--out-dir" or "--overwrite",
            MemberAccessExpressionSyntax { Expression: var type, Name.Identifier.ValueText: "Out" or "OutAlias" or "OutDir" or "Overwrite" }
                when type.ToString().EndsWith("StandardOptionNames", StringComparison.Ordinal) => true,
            // The Name of an output option a command declares, as in options.Overwrite.Name.
            _ => option.ToString().EndsWith(".Name", StringComparison.Ordinal)
                && option.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
                    .Any(static name => name.Identifier.ValueText is "Overwrite" or "OutputFile" or "OutputDirectory"),
        };

    /// <summary>The ways of splicing a value into a command that D14 reads, and the texts it leaves alone.</summary>
    [Theory]
    [InlineData("""_ = $"Run 'aspose-cli cells convert {path} --to xlsx'.";""", true)]
    [InlineData("""_ = "Run 'aspose-cli cells convert " + path + " --to xlsx'.";""", true)]
    [InlineData("""_ = $"Convert it: aspose-cli cells convert \"{path}\" --to xlsx (or another format).";""", true)]
    [InlineData("""_ = $"Run 'aspose-cli cells convert <file> --to {format}'.";""", true)]
    [InlineData("""_ = $"Run '{DistributionInfo.CommandName} app open {file}'.";""", true)]
    [InlineData("""_ = $"Run 'aspose-cli {string.Join(' ', arguments)}' to see why.";""", true)]
    [InlineData("""_ = string.Join(' ', [DistributionInfo.CommandName, "cells", "convert", path]);""", true)]
    [InlineData("""string[] group = [DistributionInfo.CommandName, .. CommandPath()]; _ = string.Join(' ', [.. group, "--to", format]);""", true)]
    [InlineData("""_ = new StringBuilder("aspose-cli cells convert ").Append(path).ToString();""", true)]
    [InlineData("""var tokens = new List<string> { DistributionInfo.CommandName, "cells", "convert" }; tokens.Add(path);""", true)]
    [InlineData("""_ = "Run 'aspose-cli cells inspect <file> --detail comments'.";""", false)]
    [InlineData("""_ = $"Use the '{DistributionInfo.CommandName} {productId}' commands for it.";""", false)]
    [InlineData("""_ = $"Vocabulary: {DistributionInfo.CommandName} schema {id}.";""", false)]
    [InlineData("""_ = $"Check it with 'aspose-cli cells inspect' before relying on {path}.";""", false)]
    [InlineData("""_ = $"No product reads it: {DistributionInfo.CommandName} writes {extension} files but reads none.";""", false)]
    [InlineData("""_ = string.Join(' ', [DistributionInfo.CommandName, .. CommandPath.Select(static command => command.Name)]);""", false)]
    [InlineData("""_ = string.Join(' ', [DistributionInfo.CommandName, .. encryptor.Path]);""", false)]
    [InlineData("""_ = string.Join(' ', [DistributionInfo.CommandName, .. arguments]);""", true)]
    [InlineData("""_ = string.Join(' ', [DistributionInfo.CommandName, .. encryptor.Path, path]);""", true)]
    public void D14_TheRule_ReadsEachWayOfSplicingAValue(string statements, bool splices)
    {
        SyntaxNode root = CSharpSyntaxTree.ParseText($"class C {{ void M() {{ {statements} }} }}").GetRoot();

        Assert.Equal(splices, new CommandReader(CliCatalog.Current).Splices(root).Any());
    }

    private static string Excerpt(SyntaxNode node)
    {
        string text = Regex.Replace(node.ToString(), @"\s+", " ");
        return text.Length <= 120 ? text : text[..117] + "...";
    }

    /// <summary>
    /// Reads the <c>aspose-cli</c> command lines source code assembles and finds the values spliced
    /// into them. A command starts at the text <c>aspose-cli</c> or at
    /// <c>DistributionInfo.CommandName</c>, in an interpolated or concatenated string, a
    /// <c>StringBuilder</c>, or a list of tokens (a collection, a list that is added to, the
    /// arguments of <c>string.Join</c> or a <c>Prepend</c>). In text it runs to the quote that
    /// opened it, or, unquoted, to its sentence's end. Its words are read against the catalog: the
    /// command path, then options and arguments. A value is a hole after an option that takes one,
    /// a hole in an argument that names a file, or a sequence of tokens spliced whole, unless it is
    /// only command names; a hole in the command path is a command name, and a whole command's
    /// path spliced there ends it, so a hole after it, in a command the catalog cannot name, is a
    /// value. Text whose first word after <c>aspose-cli</c> is no command is prose, not a command.
    /// </summary>
    private sealed partial class CommandReader
    {
        private const string Executable = "aspose-cli";

        private readonly CliCommand _root;
        private readonly Dictionary<string, CliCommand> _commands;
        private readonly Dictionary<string, (int Arity, bool File)[]> _arguments;

        public CommandReader(CliCatalog catalog)
        {
            _root = catalog.Commands.Single(static command => command.Words.Length == 0);
            _commands = catalog.Commands.ToDictionary(static command => command.Words, StringComparer.Ordinal);
            // An argument names a file when the catalog says it reads one, or names it file, as a
            // create's output and a license file are.
            _arguments = catalog.Document["commands"]!.AsArray().OfType<JsonObject>().ToDictionary(
                static command => command["path"]!.GetValue<string>() is var path && path.Contains(' ', StringComparison.Ordinal) ? path[(path.IndexOf(' ', StringComparison.Ordinal) + 1)..] : string.Empty,
                static command => (command["arguments"]?.AsArray() ?? []).OfType<JsonObject>()
                    .Select(static argument => (argument["maximumArity"]!.GetValue<int>(),
                        argument["inputKind"] is not null || argument["name"]!.GetValue<string>() == "file"))
                    .ToArray(),
                StringComparer.Ordinal);
        }

        /// <summary>Each command assembled under <paramref name="root"/> with a value spliced into it, and that value.</summary>
        public IEnumerable<(SyntaxNode Command, SyntaxNode Value)> Splices(SyntaxNode root)
        {
            foreach (SyntaxNode node in root.DescendantNodes())
            {
                IEnumerable<IReadOnlyList<Word>> commands = node switch
                {
                    InterpolatedStringExpressionSyntax text when !Concatenated(text) => FromText(Parts(text)),
                    BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression) && !Concatenated(binary) && Parts(binary) is { } parts
                        && parts.Any(static part => part.Text is not null) => FromText(parts),
                    ObjectCreationExpressionSyntax creation when LastIdentifier(creation.Type) == "StringBuilder" => FromText(BuilderParts(creation)),
                    CollectionExpressionSyntax collection => FromTokens(node, [.. collection.Elements.Select(static element => element switch
                    {
                        SpreadElementSyntax spread => (spread.Expression, true),
                        ExpressionElementSyntax item => (item.Expression, false),
                        _ => ((ExpressionSyntax)SyntaxFactory.IdentifierName("_"), true),
                    })]),
                    InitializerExpressionSyntax initializer when initializer.IsKind(SyntaxKind.ArrayInitializerExpression)
                        || initializer.IsKind(SyntaxKind.CollectionInitializerExpression)
                        => FromTokens(node, [.. initializer.Expressions.Select(static expression => (expression, false))]),
                    InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Join" } join } invocation
                        when LastIdentifier(join.Expression) is "string" or "String" && invocation.ArgumentList.Arguments.Count > 2
                        => FromTokens(node, [.. invocation.ArgumentList.Arguments.Skip(1).Select(static argument => (argument.Expression, false))]),
                    InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Prepend" } prepend } invocation
                        when invocation.ArgumentList.Arguments is [{ Expression: var first }] && IsExecutable(first)
                        => FromTokens(node, [(first, false), (prepend.Expression, true)]),
                    _ => [],
                };
                foreach (IReadOnlyList<Word> words in commands)
                {
                    if (FirstValue(words, node) is { } value)
                    {
                        yield return (node, value);
                    }
                }
            }
        }

        // -- Text: interpolated strings, concatenations, StringBuilder -----------------------

        /// <summary>One piece of assembled text: literal text, or a hole an expression fills.</summary>
        private sealed record Part(string? Text, ExpressionSyntax? Hole);

        private static bool Concatenated(ExpressionSyntax expression)
        {
            SyntaxNode? parent = expression.Parent;
            while (parent is ParenthesizedExpressionSyntax)
            {
                parent = parent.Parent;
            }
            return parent is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression);
        }

        private static List<Part> Parts(ExpressionSyntax expression)
        {
            var parts = new List<Part>();
            Add(expression);
            return parts;

            void Add(ExpressionSyntax piece)
            {
                switch (piece)
                {
                    case ParenthesizedExpressionSyntax parenthesized:
                        Add(parenthesized.Expression);
                        break;
                    case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression):
                        Add(binary.Left);
                        Add(binary.Right);
                        break;
                    case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) || literal.IsKind(SyntaxKind.CharacterLiteralExpression):
                        parts.Add(new Part(literal.Token.ValueText, null));
                        break;
                    case InterpolatedStringExpressionSyntax interpolated:
                        foreach (InterpolatedStringContentSyntax content in interpolated.Contents)
                        {
                            parts.Add(content is InterpolatedStringTextSyntax text
                                ? new Part(text.TextToken.ValueText, null)
                                : new Part(null, ((InterpolationSyntax)content).Expression));
                        }
                        break;
                    default:
                        parts.Add(new Part(null, piece));
                        break;
                }
            }
        }

        /// <summary>What a <c>StringBuilder</c> is given: its initial text, then each <c>Append</c> on it, in its chain or on the variable that holds it.</summary>
        private static List<Part> BuilderParts(ObjectCreationExpressionSyntax creation)
        {
            var parts = new List<Part>();
            if (creation.ArgumentList?.Arguments is [{ Expression: var initial }] && !initial.IsKind(SyntaxKind.NumericLiteralExpression))
            {
                parts.AddRange(Parts(initial));
            }
            SyntaxNode chain = creation;
            while (chain.Parent is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Append" or "AppendLine" } access
                && access.Parent is InvocationExpressionSyntax append)
            {
                parts.AddRange(Appended(append));
                chain = append;
            }
            if (chain.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variable } && Scope(creation) is { } scope)
            {
                foreach (InvocationExpressionSyntax append in scope.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Where(invocation => invocation.SpanStart > creation.SpanStart && InvokedName(invocation) is "Append" or "AppendLine"
                        && Receiver(invocation) == variable.Identifier.ValueText))
                {
                    parts.AddRange(Appended(append));
                }
            }
            return parts;

            static IEnumerable<Part> Appended(InvocationExpressionSyntax append) =>
                append.ArgumentList.Arguments is [{ Expression: var value }] ? Parts(value) : [];

            // The variable a chain of calls starts from, such as builder of builder.Append(x).Append(y).
            static string? Receiver(InvocationExpressionSyntax invocation)
            {
                ExpressionSyntax current = invocation;
                while (current is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access })
                {
                    current = access.Expression;
                }
                return current is IdentifierNameSyntax name ? name.Identifier.ValueText : null;
            }
        }

        /// <summary>
        /// The commands in assembled text, each read from its start to the quote that opened it, or,
        /// unquoted, to a word that ends its sentence or a <c>(</c> that opens one.
        /// </summary>
        private static IEnumerable<IReadOnlyList<Word>> FromText(List<Part> parts)
        {
            // The text as characters and holes, so a command can run across both.
            var items = new List<(char Character, ExpressionSyntax? Hole)>();
            foreach (Part part in parts)
            {
                if (part.Hole is { } hole)
                {
                    items.Add(('\0', hole));
                }
                else
                {
                    items.AddRange(part.Text!.Select(static character => (character, (ExpressionSyntax?)null)));
                }
            }
            for (int index = 0; index < items.Count; index++)
            {
                int after;
                if (items[index].Hole is { } hole && IsExecutable(hole))
                {
                    after = index + 1;
                }
                else if (items[index].Hole is null && StartsExecutable(items, index))
                {
                    after = index + Executable.Length;
                }
                else
                {
                    continue;
                }
                char opener = index > 0 && items[index - 1] is { Hole: null, Character: '\'' or '`' } previous ? previous.Character : '\0';
                (List<Word> words, int end) = TextWords(items, after, opener);
                yield return words;
                index = Math.Max(index, end - 1);
            }
        }

        private static bool StartsExecutable(List<(char Character, ExpressionSyntax? Hole)> items, int index)
        {
            for (int offset = 0; offset < Executable.Length; offset++)
            {
                if (index + offset >= items.Count || items[index + offset] is not { Hole: null } item || item.Character != Executable[offset])
                {
                    return false;
                }
            }
            bool before = index == 0 || items[index - 1] is { Hole: null } previous && !char.IsLetterOrDigit(previous.Character) && previous.Character is not '-' and not '_';
            int next = index + Executable.Length;
            return before && next < items.Count && items[next] is { Hole: null } following && char.IsWhiteSpace(following.Character);
        }

        private static (List<Word> Words, int End) TextWords(List<(char Character, ExpressionSyntax? Hole)> items, int from, char opener)
        {
            var words = new List<Word>();
            Word? current = null;
            char inner = '\0';
            bool placeholder = false;
            int index = from;
            for (; index < items.Count; index++)
            {
                (char character, ExpressionSyntax? hole) = items[index];
                if (hole is not null)
                {
                    if (inner == '\0' && IsExecutable(hole))
                    {
                        break; // Another command starts.
                    }
                    (current ??= new Word()).Add(hole);
                    continue;
                }
                if (inner != '\0')
                {
                    if (character == inner)
                    {
                        inner = '\0';
                    }
                    else
                    {
                        current!.Add(character);
                    }
                    continue;
                }
                if (opener != '\0' && character == opener)
                {
                    break;
                }
                if (character == '"' || (character == '\'' && opener != '\''))
                {
                    inner = character;
                    current ??= new Word();
                    continue;
                }
                if (placeholder || character == '<')
                {
                    placeholder = character != '>';
                    (current ??= new Word()).Add(character);
                    continue;
                }
                if (char.IsWhiteSpace(character))
                {
                    if (Finish())
                    {
                        break;
                    }
                    continue;
                }
                if (opener == '\0' && current is null && character == '(')
                {
                    break;
                }
                (current ??= new Word()).Add(character);
            }
            Finish();
            return (words, index);

            // Ends the current word; true when, unquoted, it ends its sentence.
            bool Finish()
            {
                if (current is null)
                {
                    return false;
                }
                bool end = opener == '\0' && current.EndsSentence();
                words.Add(current);
                current = null;
                return end;
            }
        }

        // -- Tokens: collections, lists, string.Join, Prepend --------------------------------

        /// <summary>
        /// The command a list of tokens holds: from its executable, or from a spread of a variable
        /// whose collection starts with one, on, followed by what is added to the list it initializes.
        /// </summary>
        private IEnumerable<IReadOnlyList<Word>> FromTokens(SyntaxNode node, (ExpressionSyntax Expression, bool Spread)[] tokens)
        {
            List<Word>? words = null;
            for (int index = 0; index < tokens.Length; index++)
            {
                (ExpressionSyntax expression, bool spread) = tokens[index];
                if (!spread && IsExecutable(expression))
                {
                    words = [];
                }
                else if (words is null && spread && LocalCommand(expression) is { } local)
                {
                    words = [.. local];
                }
                else
                {
                    words?.Add(Word.Of(expression, spread));
                }
            }
            if (words is null)
            {
                yield break;
            }
            words.AddRange(Additions(node));
            yield return words;
        }

        /// <summary>The words of the command a variable's collection holds, when <paramref name="expression"/> names such a variable.</summary>
        private List<Word>? LocalCommand(ExpressionSyntax expression)
        {
            if (expression is not IdentifierNameSyntax name || Declarator(expression, name.Identifier.ValueText) is not { Initializer.Value: var value })
            {
                return null;
            }
            SyntaxNode? collection = value switch
            {
                CollectionExpressionSyntax items => items,
                BaseObjectCreationExpressionSyntax { Initializer: { } initializer } => initializer,
                ArrayCreationExpressionSyntax { Initializer: { } initializer } => initializer,
                InitializerExpressionSyntax initializer => initializer,
                _ => null,
            };
            return collection is not null && Commands(collection).FirstOrDefault() is { } words ? [.. words] : null;

            IEnumerable<IReadOnlyList<Word>> Commands(SyntaxNode list) => list switch
            {
                CollectionExpressionSyntax items => FromTokens(list, [.. items.Elements.Select(static element => element is SpreadElementSyntax spread
                    ? (spread.Expression, true)
                    : (((ExpressionElementSyntax)element).Expression, false))]),
                InitializerExpressionSyntax initializer => FromTokens(list, [.. initializer.Expressions.Select(static item => (item, false))]),
                _ => [],
            };
        }

        /// <summary>
        /// What code adds to the list a collection initializes, after it: each <c>Add</c>, any
        /// other addition as a sequence, and the list passed to a method, which may add anything.
        /// </summary>
        private static IEnumerable<Word> Additions(SyntaxNode collection)
        {
            SyntaxNode? owner = collection.Parent is BaseObjectCreationExpressionSyntax creation ? creation : collection;
            if (owner?.Parent is not EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variable } || Scope(collection) is not { } scope)
            {
                yield break;
            }
            string name = variable.Identifier.ValueText;
            foreach (IdentifierNameSyntax use in scope.DescendantNodes().OfType<IdentifierNameSyntax>()
                .Where(use => use.SpanStart > collection.Span.End && use.Identifier.ValueText == name))
            {
                if (use.Parent is MemberAccessExpressionSyntax access && access.Expression == use && access.Parent is InvocationExpressionSyntax call)
                {
                    string member = access.Name.Identifier.ValueText;
                    if (member == "Add" && call.ArgumentList.Arguments is [{ Expression: var added }])
                    {
                        yield return Word.Of(added, spread: false);
                    }
                    else if (member is "AddRange" or "Insert" or "InsertRange")
                    {
                        yield return Word.Of(call, spread: true);
                    }
                }
                else if (use.Parent is ArgumentSyntax { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax receiver } }
                    && InvokedName(receiver) is not "Join" and not "Concat")
                {
                    yield return Word.Of(receiver, spread: true);
                }
            }
        }

        // -- Reading a command against the catalog --------------------------------------------

        /// <summary>The first value spliced into a command's words, or null when it holds none or is prose.</summary>
        private SyntaxNode? FirstValue(IReadOnlyList<Word> words, SyntaxNode scope)
        {
            CliCommand? command = _root;
            bool inPath = true;
            bool expectsValue = false;
            int positional = 0;
            foreach (Word word in words)
            {
                if (word.Holes.Count == 0 || word.IsOptionName)
                {
                    string text = word.IsOptionName ? "--" : word.Text;
                    if (inPath)
                    {
                        if (command is null)
                        {
                            if (CommandWord().IsMatch(text))
                            {
                                continue;
                            }
                        }
                        else if (command.Subcommands.Contains(text, StringComparer.Ordinal))
                        {
                            command = _commands[command.Words.Length == 0 ? text : command.Words + " " + text];
                            continue;
                        }
                        else if (command.Subcommands.Count > 0 && !text.StartsWith('-'))
                        {
                            return null; // The word after the executable names no command: prose.
                        }
                        inPath = false;
                    }
                    if (text.Length > 1 && text[0] == '-')
                    {
                        expectsValue = !text.Contains('=', StringComparison.Ordinal) && TakesValue(command, text);
                    }
                    else if (expectsValue)
                    {
                        expectsValue = false;
                    }
                    else
                    {
                        positional++;
                    }
                    continue;
                }
                ExpressionSyntax hole = word.Holes[0].Expression;
                if (expectsValue || word.Text.StartsWith('-'))
                {
                    return hole; // An option's value.
                }
                if (inPath)
                {
                    if (command is null || command.Subcommands.Count > 0)
                    {
                        if (word.Holes.Where(item => item.Spread && !NamesCommands(item.Expression, scope)).Select(static item => item.Expression).FirstOrDefault() is { } spliced)
                        {
                            return spliced; // Several tokens spliced whole.
                        }
                        command = null; // A command name, such as a product id.
                        // A whole command's path, such as CommandPath() or edit.Path, ends the path:
                        // what follows it is the command's options and arguments.
                        inPath = !word.Holes.Any(static item => item.Spread && WholePath(item.Expression));
                        continue;
                    }
                    inPath = false;
                }
                if (command is null || word.Holes.Any(static item => item.Spread))
                {
                    return hole;
                }
                if (Argument(command, positional) is not { } argument)
                {
                    return null; // Beyond the command's arguments: prose.
                }
                if (argument.File)
                {
                    return hole; // A path.
                }
                positional++;
            }
            return null;
        }

        private bool TakesValue(CliCommand? command, string option)
        {
            string name = option.Split('=')[0];
            CliOption? declared = command?.Option(name) ?? _root.Options.FirstOrDefault(candidate => candidate.Recursive && candidate.Name == name);
            return name == "--" || declared is null || (declared.MaximumArity > 0 && declared.Type != "boolean");
        }

        private (int Arity, bool File)? Argument(CliCommand command, int position)
        {
            int covered = 0;
            foreach ((int arity, bool file) in _arguments.GetValueOrDefault(command.Words) ?? [])
            {
                covered += arity;
                if (position < covered)
                {
                    return (arity, file);
                }
            }
            return null;
        }

        /// <summary>
        /// Whether a sequence is a whole command's path: <c>CommandPath()</c>, or a member or
        /// variable named for one (<c>Path</c>, <c>edit.Path</c>, <c>childPath</c>).
        /// </summary>
        private static bool WholePath(ExpressionSyntax expression) => expression switch
        {
            InvocationExpressionSyntax invocation => InvokedName(invocation) == "CommandPath" && invocation.ArgumentList.Arguments.Count == 0,
            IdentifierNameSyntax or MemberAccessExpressionSyntax => LastIdentifier(expression) is { } member
                && (member == "path" || member.EndsWith("Path", StringComparison.Ordinal)),
            _ => false,
        };

        /// <summary>
        /// Whether a sequence spliced into a command's path is command names only:
        /// <c>CommandPath()</c>, a member or variable named for a command path (<c>Path</c>,
        /// <c>edit.Path</c>, <c>childPath</c>), a projection of commands to their <c>Name</c>, the
        /// tokens <c>string.Join</c> joins, or a variable that holds or is given only those. A path
        /// after the executable is a command's; a file is named after the command path, where any
        /// sequence is a value.
        /// </summary>
        private static bool NamesCommands(ExpressionSyntax expression, SyntaxNode scope)
        {
            switch (expression)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    return NamesCommands(parenthesized.Expression, scope);
                case InvocationExpressionSyntax invocation when InvokedName(invocation) == "CommandPath":
                    return true;
                case IdentifierNameSyntax or MemberAccessExpressionSyntax when WholePath(expression):
                    return true;
                case InvocationExpressionSyntax invocation when InvokedName(invocation) == "Select":
                    return invocation.ArgumentList.Arguments is [{ Expression: LambdaExpressionSyntax { ExpressionBody: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Name" } } }];
                case InvocationExpressionSyntax invocation when InvokedName(invocation) == "Join":
                    return invocation.ArgumentList.Arguments.Count == 2 && NamesCommands(invocation.ArgumentList.Arguments[1].Expression, scope);
                case IdentifierNameSyntax name when Declarator(expression, name.Identifier.ValueText) is { } declarator:
                    string variable = name.Identifier.ValueText;
                    ExpressionSyntax[] given =
                    [
                        .. Elements(declarator.Initializer?.Value),
                        .. (Scope(expression) ?? scope).DescendantNodes().OfType<InvocationExpressionSyntax>()
                            .Where(call => call.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Add" or "Push" or "Insert" } access
                                && LastIdentifier(access.Expression) == variable)
                            .SelectMany(static call => call.ArgumentList.Arguments.Select(static argument => argument.Expression)),
                    ];
                    return given.Length > 0 && given.All(item => item is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Name" } || NamesCommands(item, scope));
                default:
                    return false;
            }

            static IEnumerable<ExpressionSyntax> Elements(ExpressionSyntax? value) => value switch
            {
                CollectionExpressionSyntax items => items.Elements.Select(static element => element switch
                {
                    SpreadElementSyntax spread => spread.Expression,
                    ExpressionElementSyntax item => item.Expression,
                    _ => SyntaxFactory.IdentifierName("_"),
                }),
                BaseObjectCreationExpressionSyntax { Initializer: { } initializer } => initializer.Expressions,
                _ => [],
            };
        }

        // -- Support ---------------------------------------------------------------------------

        private static bool IsExecutable(ExpressionSyntax expression) => expression switch
        {
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: "CommandName" } access => LastIdentifier(access.Expression) == "DistributionInfo",
            LiteralExpressionSyntax literal => literal.IsKind(SyntaxKind.StringLiteralExpression) && literal.Token.ValueText == Executable,
            _ => false,
        };

        /// <summary>An option's name held by a constant, such as <c>StandardOptionNames.To</c> or <c>OpsOption</c>.</summary>
        private static bool IsOptionName(ExpressionSyntax expression) => expression switch
        {
            MemberAccessExpressionSyntax access => LastIdentifier(access.Expression)?.EndsWith("OptionNames", StringComparison.Ordinal) == true
                || access.Name.Identifier.ValueText.EndsWith("Option", StringComparison.Ordinal),
            IdentifierNameSyntax name => name.Identifier.ValueText.EndsWith("Option", StringComparison.Ordinal) && char.IsUpper(name.Identifier.ValueText[0]),
            _ => false,
        };

        /// <summary>The member or local function that holds a node.</summary>
        private static SyntaxNode? Scope(SyntaxNode node) =>
            node.Ancestors().FirstOrDefault(static ancestor => ancestor is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax
                or PropertyDeclarationSyntax or AccessorDeclarationSyntax);

        private static VariableDeclaratorSyntax? Declarator(SyntaxNode use, string name) =>
            Scope(use)?.DescendantNodes().OfType<VariableDeclaratorSyntax>().FirstOrDefault(declarator => declarator.Identifier.ValueText == name);

        [GeneratedRegex("^[a-z][a-z0-9-]*$")]
        private static partial Regex CommandWord();

        /// <summary>One word of a command: its literal characters and the holes spliced into it.</summary>
        private sealed class Word
        {
            private readonly StringBuilder _text = new();

            public List<(ExpressionSyntax Expression, bool Spread)> Holes { get; } = [];

            /// <summary>The literal text, with nothing in place of a hole.</summary>
            public string Text => _text.ToString();

            /// <summary>An option name a constant holds, as a token of a list.</summary>
            public bool IsOptionName { get; private init; }

            public static Word Of(ExpressionSyntax expression, bool spread)
            {
                if (!spread && expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    var text = new Word();
                    foreach (char character in literal.Token.ValueText)
                    {
                        text.Add(character);
                    }
                    return text;
                }
                var word = new Word { IsOptionName = !spread && IsOptionName(expression) };
                word.Holes.Add((expression, spread || IsJoin(expression)));
                return word;
            }

            public void Add(char character) => _text.Append(character);

            public void Add(ExpressionSyntax hole) => Holes.Add((hole, IsJoin(hole)));

            /// <summary>Whether the word ends its sentence, as a final <c>.</c>, <c>,</c>, <c>;</c> or <c>:</c> does.</summary>
            public bool EndsSentence()
            {
                if (_text.Length > 0 && _text[^1] is '.' or ',' or ';' or ':')
                {
                    _text.Length--;
                    return true;
                }
                return false;
            }

            private static bool IsJoin(ExpressionSyntax expression) =>
                expression is InvocationExpressionSyntax invocation && InvokedName(invocation) == "Join";
        }
    }
}
