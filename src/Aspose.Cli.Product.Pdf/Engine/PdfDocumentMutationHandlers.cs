using System.Globalization;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Product.Pdf.Engine.Mapping;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Text;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Devices;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Optimization;
using Aspose.Pdf.Text;
using static Aspose.Cli.Product.Pdf.Engine.PdfArtifactSupport;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;
using static Aspose.Cli.Product.Pdf.Engine.PdfMutationSupport;
using PdfColor = Aspose.Pdf.Color;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Owns document metadata, navigation, forms and security mutations.</summary>
internal static class PdfDocumentMutationHandlers
{
    internal static long SetMetadata(Document document, SetMetadataOp op)
    {
        if (op.Title is not null)
        {
            document.Info.Title = op.Title;
        }

        if (op.Author is not null)
        {
            document.Info.Author = op.Author;
        }

        if (op.Subject is not null)
        {
            document.Info.Subject = op.Subject;
        }

        if (op.Keywords is not null)
        {
            document.Info.Keywords = op.Keywords;
        }
        if (op.Custom is not null)
        {
            foreach ((string key, string value) in op.Custom)
            {
                document.Info[key] = value;
            }
        }

        return 1;
    }

    internal static long RemoveMetadata(Document document, RemoveMetadataOp op)
    {
        if (op.DocumentInfo)
        {
            document.Info.Clear();
        }

        if (op.Xmp)
        {
            document.Metadata.Clear();
        }

        return 1;
    }

    internal static long AddBookmark(Document document, AddBookmarkOp op)
    {
        _ = PageAt(document, op.Page);
        OutlineCollection target = document.Outlines;
        if (op.Parent is not null)
        {
            OutlineItemCollection? parent = FindOutline(document.Outlines, op.Parent.Split('/', StringSplitOptions.RemoveEmptyEntries));
            if (parent is null)
            {
                throw new InvalidOperationException($"Bookmark parent '{op.Parent}' was not found.");
            }

            var nested = new OutlineItemCollection(document.Outlines)
            {
                Title = op.Title,
                Destination = new FitExplicitDestination(document.Pages[op.Page]),
            };
            parent.Add(nested);
            return 1;
        }

        target.Add(new OutlineItemCollection(document.Outlines)
        {
            Title = op.Title,
            Destination = new FitExplicitDestination(document.Pages[op.Page]),
        });
        return 1;
    }

    internal static long DeleteBookmarks(Document document, DeleteBookmarksOp op)
    {
        if (op.All)
        {
            int count = document.Outlines.Count;
            document.Outlines.Delete();
            return count;
        }

        string[] path = op.Path!.Split('/', StringSplitOptions.RemoveEmptyEntries);
        OutlineItemCollection? item = FindOutline(document.Outlines, path);
        if (item is null)
        {
            throw new InvalidOperationException($"Bookmark '{op.Path}' was not found.");
        }

        item.Delete();
        return 1;
    }

    internal static long AddAttachment(Document document, AddAttachmentOp op)
    {
        EnsureFile(op.Path);
        string name = op.Name ?? Path.GetFileName(op.Path);
        using FileStream stream = File.OpenRead(op.Path);
        var specification = new FileSpecification(stream, name, op.Description ?? string.Empty)
        {
            Name = name,
            UnicodeName = name,
        };
        document.EmbeddedFiles.Add(name, specification);
        return 1;
    }

    internal static long RemoveAttachment(Document document, RemoveAttachmentOp op)
    {
        FileSpecification? existing = document.EmbeddedFiles.FindByName(op.Name);
        if (existing is null)
        {
            throw new InvalidOperationException($"Attachment '{op.Name}' was not found.");
        }

        document.EmbeddedFiles.Delete(op.Name);
        return 1;
    }

    internal static long SetPageLabels(Document document, SetPageLabelsOp op)
    {
        int count = 0;
        foreach (PdfPageLabelRange range in op.Ranges)
        {
            _ = PageAt(document, range.StartPage);
            document.PageLabels.UpdateLabel(range.StartPage - 1, new PageLabel
            {
                Prefix = range.Prefix,
                StartingValue = range.StartingValue,
                NumberingStyle = NumberingStyleValue(range.Style),
            });
            count++;
        }

        return count;
    }

    internal static long SetFormField(Document document, SetFormFieldOp op)
    {
        EnsureAcroForm(document);
        Field? field = document.Form.Fields.FirstOrDefault(
            value => string.Equals(value.FullName, op.Name, StringComparison.Ordinal));
        if (field is null)
        {
            throw new InvalidOperationException($"Form field '{op.Name}' was not found.");
        }

        field.Value = op.Value;
        return 1;
    }

    internal static long FlattenForms(Document document, FlattenFormsOp op)
    {
        EnsureAcroForm(document);
        if (op.All)
        {
            int count = document.Form.Count;
            document.Form.Flatten();
            return count;
        }

        int flattened = 0;
        foreach (string name in op.Fields!)
        {
            Field? field = document.Form.Fields.FirstOrDefault(
                value => string.Equals(value.FullName, name, StringComparison.Ordinal));
            if (field is null)
            {
                throw new InvalidOperationException($"Form field '{name}' was not found.");
            }

            field.Flatten();
            flattened++;
        }

        return flattened;
    }

    internal static long Encrypt(
        Document document,
        EncryptPdfOp op,
        IReadOnlyDictionary<string, string>? secrets)
    {
        string owner = Secret(secrets, "ownerPassword", required: true)!;
        string user = Secret(secrets, "userPassword", op.UserPasswordEnv is not null) ?? string.Empty;
        Permissions permissions = (Permissions)0;
        if (op.Permissions.Print)
        {
            permissions |= Permissions.PrintDocument;
        }

        if (op.Permissions.Copy)
        {
            permissions |= Permissions.ExtractContent;
        }

        if (op.Permissions.Modify)
        {
            permissions |= Permissions.ModifyContent;
        }

        if (op.Permissions.Annotate)
        {
            permissions |= Permissions.ModifyTextAnnotations;
        }

        if (op.Permissions.FillForms)
        {
            permissions |= Permissions.FillForm;
        }

        if (op.Permissions.ExtractAccessibility)
        {
            permissions |= Permissions.ExtractContentWithDisabilities;
        }

        if (op.Permissions.Assemble)
        {
            permissions |= Permissions.AssembleDocument;
        }

        if (op.Permissions.PrintHighResolution)
        {
            permissions |= Permissions.PrintingQuality;
        }
        document.Encrypt(user, owner, permissions, CryptoAlgorithm.AESx256);
        return 1;
    }

    internal static long Decrypt(Document document)
    {
        if (!document.IsEncrypted)
        {
            return 0;
        }

        document.Decrypt();
        return 1;
    }

    internal static long Optimize(Document document, OptimizePdfOp op)
    {
        var options = new OptimizationOptions
        {
            RemoveUnusedObjects = op.RemoveUnusedObjects,
            RemoveUnusedStreams = op.RemoveUnusedObjects,
            CompressAllContentStreams = op.CompressStreams,
            CompressObjects = op.CompressStreams,
            LinkDuplicateStreams = true,
            UnembedFonts = op.UnembedFonts,
        };
        if (op.DownsampleImagesDpi.HasValue || op.ImageQuality.HasValue)
        {
            options.ImageCompressionOptions.CompressImages = true;
            options.ImageCompressionOptions.ResizeImages = op.DownsampleImagesDpi.HasValue;
            options.ImageCompressionOptions.MaxResolution = op.DownsampleImagesDpi ?? 300;
            options.ImageCompressionOptions.ImageQuality = op.ImageQuality ?? 75;
        }

        document.OptimizeResources(options);
        return 1;
    }
}
