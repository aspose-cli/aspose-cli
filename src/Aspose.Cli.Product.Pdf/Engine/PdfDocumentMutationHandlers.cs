using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Optimization;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;
using static Aspose.Cli.Product.Pdf.Engine.PdfMutationSupport;

namespace Aspose.Cli.Product.Pdf.Engine;

// Document metadata, navigation, forms and security.
internal sealed partial class PdfMutationHandlers
{
    public long Apply(SetMetadataOp operation)
    {
        if (operation.Title is not null)
        {
            _document.Info.Title = operation.Title;
        }

        if (operation.Author is not null)
        {
            _document.Info.Author = operation.Author;
        }

        if (operation.Subject is not null)
        {
            _document.Info.Subject = operation.Subject;
        }

        if (operation.Keywords is not null)
        {
            _document.Info.Keywords = operation.Keywords;
        }
        if (operation.Custom is not null)
        {
            foreach ((string key, string value) in operation.Custom)
            {
                _document.Info[key] = value;
            }
        }

        return 1;
    }

    public long Apply(RemoveMetadataOp operation)
    {
        if (operation.DocumentInfo)
        {
            _document.Info.Clear();
        }

        if (operation.Xmp)
        {
            _document.Metadata.Clear();
        }

        return 1;
    }

    public long Apply(AddBookmarkOp operation)
    {
        _ = PageAt(_document, operation.Page);
        OutlineCollection target = _document.Outlines;
        if (operation.Parent is not null)
        {
            OutlineItemCollection? parent = FindOutline(_document.Outlines, operation.Parent.Split('/', StringSplitOptions.RemoveEmptyEntries));
            if (parent is null)
            {
                throw new OperationInvalidException($"Bookmark parent '{operation.Parent}' was not found.");
            }

            var nested = new OutlineItemCollection(_document.Outlines)
            {
                Title = operation.Title,
                Destination = new FitExplicitDestination(_document.Pages[operation.Page]),
            };
            parent.Add(nested);
            return 1;
        }

        target.Add(new OutlineItemCollection(_document.Outlines)
        {
            Title = operation.Title,
            Destination = new FitExplicitDestination(_document.Pages[operation.Page]),
        });
        return 1;
    }

    public long Apply(DeleteBookmarksOp operation)
    {
        if (operation.All)
        {
            int count = _document.Outlines.Count;
            _document.Outlines.Delete();
            return count;
        }

        string[] path = operation.Path!.Split('/', StringSplitOptions.RemoveEmptyEntries);
        OutlineItemCollection? item = FindOutline(_document.Outlines, path);
        if (item is null)
        {
            throw new OperationInvalidException($"Bookmark '{operation.Path}' was not found.");
        }

        item.Delete();
        return 1;
    }

    public long Apply(AddAttachmentOp operation)
    {
        EnsureFile(operation.Path);
        string name = operation.Name ?? Path.GetFileName(operation.Path);
        Stream stream = _inputs.OpenFile(operation.Path);
        var specification = new FileSpecification(stream, name, operation.Description ?? string.Empty)
        {
            Name = name,
            UnicodeName = name,
        };
        _document.EmbeddedFiles.Add(name, specification);
        return 1;
    }

    public long Apply(RemoveAttachmentOp operation)
    {
        // FindByName throws an engine exception for a missing name; match the names query reports.
        if (!_document.EmbeddedFiles.Any(file => string.Equals(file.UnicodeName ?? file.Name, operation.Name, StringComparison.Ordinal)))
        {
            throw new OperationInvalidException($"Attachment '{operation.Name}' was not found.");
        }

        _document.EmbeddedFiles.Delete(operation.Name);
        return 1;
    }

    public long Apply(SetPageLabelsOp operation)
    {
        int count = 0;
        foreach (PdfPageLabelRange range in operation.Ranges)
        {
            _ = PageAt(_document, range.StartPage);
            _document.PageLabels.UpdateLabel(range.StartPage - 1, new PageLabel
            {
                Prefix = range.Prefix,
                StartingValue = range.StartingValue,
                NumberingStyle = NumberingStyleValue(range.Style),
            });
            count++;
        }

        return count;
    }

    public long Apply(SetFormFieldOp operation)
    {
        EnsureAcroForm(_document);
        Field? field = _document.Form.Fields.FirstOrDefault(
            value => string.Equals(value.FullName, operation.Name, StringComparison.Ordinal));
        if (field is null)
        {
            throw new OperationInvalidException($"Form field '{operation.Name}' was not found.");
        }

        if (RejectedFieldValue(field, operation.Value) is { } rejected)
        {
            throw new OperationInvalidException(rejected);
        }

        field.Value = operation.Value;
        return 1;
    }

    public long Apply(FlattenFormsOp operation)
    {
        EnsureAcroForm(_document);
        if (operation.Fields is null)
        {
            int count = _document.Form.Count;
            _document.Form.Flatten();
            return count;
        }

        // Resolve every name first: a missing field must reject the operation before any change.
        Field[] fields = operation.Fields.Select(name => _document.Form.Fields.FirstOrDefault(
                value => string.Equals(value.FullName, name, StringComparison.Ordinal))
            ?? throw new OperationInvalidException($"Form field '{name}' was not found.")).ToArray();
        foreach (Field field in fields)
        {
            field.Flatten();
        }

        return fields.Length;
    }

    public long Apply(EncryptPdfOp operation)
    {
        string owner = OperationSecrets.Resolve(_secrets, operation.OwnerPasswordEnv)!;
        string user = OperationSecrets.Resolve(_secrets, operation.UserPasswordEnv) ?? string.Empty;
        Permissions permissions = (Permissions)0;
        if (operation.Permissions.Print)
        {
            permissions |= Permissions.PrintDocument;
        }

        if (operation.Permissions.Copy)
        {
            permissions |= Permissions.ExtractContent;
        }

        if (operation.Permissions.Modify)
        {
            permissions |= Permissions.ModifyContent;
        }

        if (operation.Permissions.Annotate)
        {
            permissions |= Permissions.ModifyTextAnnotations;
        }

        if (operation.Permissions.FillForms)
        {
            permissions |= Permissions.FillForm;
        }

        if (operation.Permissions.ExtractAccessibility)
        {
            permissions |= Permissions.ExtractContentWithDisabilities;
        }

        if (operation.Permissions.Assemble)
        {
            permissions |= Permissions.AssembleDocument;
        }

        if (operation.Permissions.PrintHighResolution)
        {
            permissions |= Permissions.PrintingQuality;
        }
        _document.Encrypt(user, owner, permissions, CryptoAlgorithm.AESx256);
        return 1;
    }

    public long Apply(DecryptPdfOp operation)
    {
        if (!_document.IsEncrypted)
        {
            return 0;
        }

        _document.Decrypt();
        return 1;
    }

    public long Apply(OptimizePdfOp operation)
    {
        var options = new OptimizationOptions
        {
            RemoveUnusedObjects = operation.RemoveUnusedObjects,
            RemoveUnusedStreams = operation.RemoveUnusedObjects,
            CompressAllContentStreams = operation.CompressStreams,
            CompressObjects = operation.CompressStreams,
            LinkDuplicateStreams = true,
            UnembedFonts = operation.UnembedFonts,
        };
        if (operation.DownsampleImagesDpi.HasValue || operation.ImageQuality.HasValue)
        {
            options.ImageCompressionOptions.CompressImages = true;
            options.ImageCompressionOptions.ResizeImages = operation.DownsampleImagesDpi.HasValue;
            options.ImageCompressionOptions.MaxResolution = operation.DownsampleImagesDpi ?? 300;
            options.ImageCompressionOptions.ImageQuality = operation.ImageQuality ?? 75;
        }

        _document.OptimizeResources(options);
        return 1;
    }
}
