using Aspose.Cli.TestKit;
using Aspose.Pdf;
using Aspose.Pdf.Text;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// The PDF HTML importer runs the page script of an HTML or Markdown input, and the CLI does not
/// scan for script. What script could do to the user is reach the network or read a file outside
/// the input's folder; the process's network gate and the import's resource boundary stop both.
/// The importer's script has no XMLHttpRequest, so its channel is the page itself: a frame,
/// object, image or stylesheet it adds loads the target, and a frame of an HTML file shows that
/// file's text. Each case also writes whatever a read returned into the page, so a read that
/// succeeded would show in the output. A command that fails is as good as one that succeeds, as
/// long as nothing leaks.
/// <para>
/// With the gate open (no <c>NetworkGate.Admit</c>) the network cases of inline HTML and
/// Markdown script and of a script file fail, and with every local file permitted the file
/// cases of the same three sources fail. Script in a loaded SVG file was not seen to run, so
/// the SVG cases only guard against a change of that.
/// </para>
/// </summary>
public sealed class PdfScriptReachTests
{
    private const string Marker = "SENTINEL-7Q2X-OUTSIDE";

    public static TheoryData<string, string> Cases()
    {
        var data = new TheoryData<string, string>();
        foreach (string source in new[] { "html-inline", "html-svg", "html-script-file", "markdown-inline", "markdown-svg" })
        {
            foreach (string attack in new[] { "network", "file-uri", "relative" })
            {
                data.Add(source, attack);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Script_ReachesNeitherTheNetworkNorAFileOutsideTheInputFolder(string source, string attack)
    {
        using var workspace = new TempWorkspace();
        await using var server = new ResourceHttpServer();
        Directory.CreateDirectory(workspace.File("in"));
        string sentinel = workspace.File("outside/sentinel.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(sentinel)!);
        File.WriteAllText(sentinel, Marker);
        // A frame shows an HTML file's text in the page, so the HTML sentinel is the one a
        // script-made frame would leak.
        string page = workspace.File("outside/sentinel.html");
        File.WriteAllText(page, $"<html><body><p>{Marker}</p></body></html>");
        string[] targets = attack switch
        {
            "network" => [server.Url + "/script.txt"],
            "file-uri" => [new Uri(Path.GetFullPath(sentinel)).AbsoluteUri, new Uri(Path.GetFullPath(page)).AbsoluteUri],
            _ => ["../outside/sentinel.txt", "outside/sentinel.txt", "../outside/sentinel.html", "outside/sentinel.html"],
        };
        string script = Script(targets);
        string svg = $"""<svg xmlns="http://www.w3.org/2000/svg" width="40" height="40" onload="{System.Net.WebUtility.HtmlEncode(script)}"><script>{System.Net.WebUtility.HtmlEncode(script)}</script><rect width="40" height="40"/></svg>""";
        string output = workspace.File("in/output.pdf");
        string[] args;
        switch (source)
        {
            case "html-inline":
                File.WriteAllText(workspace.File("in/input.html"), $"<html><body><p>Local content</p><script>{script}</script></body></html>");
                args = ["pdf", "create", output, "--from-html", workspace.File("in/input.html")];
                break;
            case "html-svg":
                // HtmlImportResources reads both as resources, not images, so neither passes
                // through SvgReferences: both reach the importer with their script.
                File.WriteAllText(workspace.File("in/image.svg"), svg);
                File.WriteAllText(workspace.File("in/input.html"),
                    """<html><body><p>Local content</p><img src="image.svg" width="40" height="40"><object data="image.svg" type="image/svg+xml"></object></body></html>""");
                args = ["pdf", "create", output, "--from-html", workspace.File("in/input.html")];
                break;
            case "html-script-file":
                File.WriteAllText(workspace.File("in/script.js"), script);
                File.WriteAllText(workspace.File("in/input.html"), """<html><body><p>Local content</p><script src="script.js"></script></body></html>""");
                args = ["pdf", "create", output, "--from-html", workspace.File("in/input.html")];
                break;
            case "markdown-inline":
                File.WriteAllText(workspace.File("in/input.md"), $"# Report\n\nLocal content\n\n<script>{script}</script>\n");
                args = ["pdf", "create", output, "--from-markdown", workspace.File("in/input.md")];
                break;
            default:
                // The Markdown importer reads the image itself, script included.
                File.WriteAllText(workspace.File("in/image.svg"), svg);
                File.WriteAllText(workspace.File("in/input.md"), "# Report\n\nLocal content\n\n![a](in/image.svg)\n");
                args = ["pdf", "create", output, "--from-markdown", workspace.File("in/input.md")];
                break;
        }

        CliResult result = workspace.Run([.. args, "--output-format", "json"]);

        Assert.True(server.RequestCount == 0, string.Join("; ", server.Requests));
        if (result.ExitCode != 0)
        {
            Assert.False(File.Exists(output), result.StdErr);
            return;
        }

        Assert.DoesNotContain(Marker, System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(output)), StringComparison.Ordinal);
        using var document = new Document(output);
        var absorber = new TextAbsorber();
        document.Pages.Accept(absorber);
        Assert.DoesNotContain(Marker, absorber.Text, StringComparison.Ordinal);
    }

    // Reads each target by XMLHttpRequest and fetch, and writes whatever came back into the page,
    // through the DOM and document.write; and, since the importer's script engine may lack those,
    // also makes the page load the target itself, as an image, a frame and an object, through the
    // DOM and through document.write.
    private static string Script(string[] targets) =>
        "function put(t){try{var p=document.createElement('p');p.textContent='READ '+t;document.body.appendChild(p);}catch(e){}"
        + "try{document.write('<p>WROTE '+t+'</p>');}catch(e){}}"
        + "function load(u){try{var i=document.createElement('img');i.src=u;document.body.appendChild(i);}catch(e){}"
        + "try{var f=document.createElement('iframe');f.src=u;document.body.appendChild(f);}catch(e){}"
        + "try{var o=document.createElement('object');o.data=u;document.body.appendChild(o);}catch(e){}"
        // Each tag is spelled in parts, so the markup is written only when the script runs.
        + "try{var l='<';document.write(l+'iframe src=\"'+u+'\"></iframe>'+l+'object data=\"'+u+'\"></object>'+l+'img src=\"'+u+'\">');}catch(e){}}"
        + string.Concat(targets.Select(static target =>
            $"try{{var x=new XMLHttpRequest();x.open('GET','{target}',false);x.send();put(x.responseText);}}catch(e){{}}"
            + $"try{{fetch('{target}').then(function(r){{return r.text();}}).then(put);}}catch(e){{}}"
            // A frame of a text file fails the import, which would hide whether the others leak.
            + (target.EndsWith(".txt", StringComparison.Ordinal) && !target.StartsWith("http", StringComparison.Ordinal) ? "" : $"load('{target}');")));
}
