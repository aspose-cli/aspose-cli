using System.Data;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Text;
using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Fields;
using Aspose.Words.Lists;
using Aspose.Words.Replacing;
using Aspose.Words.Tables;
using SkiaSharp;

using static Aspose.Cli.Product.Words.Engine.Editing.WordsMutationSupport;

namespace Aspose.Cli.Product.Words.Engine.Editing;

/// <summary>Routes each resolved operation to one cohesive operation family.</summary>
internal static class WordsOpHandlers
{
    private static readonly IReadOnlyDictionary<
        Type,
        Func<Document, ResolvedWordsOp, string?, long>> Handlers =
        new Dictionary<Type, Func<Document, ResolvedWordsOp, string?, long>>
        {
            [typeof(ReplaceTextOp)] = static (d, r, _) => WordsContentOpHandlers.ReplaceText(d, (ReplaceTextOp)r.Op),
            [typeof(SetTextOp)] = static (d, r, _) => WordsContentOpHandlers.SetText(d, r.Nodes, (SetTextOp)r.Op),
            [typeof(InsertParagraphsOp)] = static (d, r, _) => WordsContentOpHandlers.InsertParagraphs(d, r.Nodes[0], (InsertParagraphsOp)r.Op),
            [typeof(DeleteBlocksOp)] = static (_, r, _) => WordsContentOpHandlers.Delete(r.Nodes),
            [typeof(InsertBreakOp)] = static (d, r, _) => WordsContentOpHandlers.InsertBreak(d, r.Nodes[0], (InsertBreakOp)r.Op),
            [typeof(InsertTableOp)] = static (d, r, _) => WordsTableOpHandlers.InsertTable(d, r.Nodes[0], (InsertTableOp)r.Op),
            [typeof(SetTableCellOp)] = static (_, r, _) => WordsTableOpHandlers.SetTableCell(r.Nodes, (SetTableCellOp)r.Op),
            [typeof(InsertTocOp)] = static (d, r, _) => WordsObjectOpHandlers.InsertToc(d, r.Nodes[0], (InsertTocOp)r.Op),
            [typeof(InsertBookmarkOp)] = static (d, r, _) => WordsObjectOpHandlers.InsertBookmark(d, r.Nodes[0], (InsertBookmarkOp)r.Op),
            [typeof(InsertHyperlinkOp)] = static (d, r, _) => WordsObjectOpHandlers.InsertHyperlink(d, r.Nodes[0], (InsertHyperlinkOp)r.Op),
            [typeof(InsertFieldOp)] = static (d, r, _) => WordsObjectOpHandlers.InsertField(d, r.Nodes[0], (InsertFieldOp)r.Op),
            [typeof(AddSectionOp)] = static (d, r, _) => WordsStructureOpHandlers.AddSection(d, (AddSectionOp)r.Op, r.Sections.FirstOrDefault()),
            [typeof(DeleteSectionOp)] = static (d, r, _) => WordsStructureOpHandlers.DeleteSection(d, r.Sections[0]),
            [typeof(SetPageSetupOp)] = static (d, r, _) => WordsStructureOpHandlers.SetPageSetup(r.Sections, (SetPageSetupOp)r.Op),
            [typeof(SetHeaderOp)] = static (d, r, _) => WordsStructureOpHandlers.SetHeader(d, r.Sections, (SetHeaderOp)r.Op),
            [typeof(SetFooterOp)] = static (d, r, _) => WordsStructureOpHandlers.SetFooter(d, r.Sections, (SetFooterOp)r.Op),
            [typeof(SetPageNumbersOp)] = static (d, r, _) => WordsStructureOpHandlers.SetPageNumbers(d, r.Sections, (SetPageNumbersOp)r.Op),
            [typeof(FormatTextOp)] = static (_, r, _) => WordsFormattingOpHandlers.FormatText(r.Nodes, (FormatTextOp)r.Op),
            [typeof(SetStyleOp)] = static (d, r, _) => WordsFormattingOpHandlers.SetStyle(d, r.Nodes, (SetStyleOp)r.Op),
            [typeof(DefineStyleOp)] = static (d, r, _) => WordsFormattingOpHandlers.DefineStyle(d, (DefineStyleOp)r.Op),
            [typeof(ApplyListOp)] = static (d, r, _) => WordsTableOpHandlers.ApplyList(d, r.Nodes, (ApplyListOp)r.Op),
            [typeof(SetDefaultFontOp)] = static (d, r, _) => WordsFormattingOpHandlers.SetDefaultFont(d, (SetDefaultFontOp)r.Op),
            [typeof(SetPropertiesOp)] = static (d, r, _) => WordsObjectOpHandlers.SetProperties(d, (SetPropertiesOp)r.Op),
            [typeof(AddWatermarkOp)] = static (d, r, _) => WordsObjectOpHandlers.AddWatermark(d, (AddWatermarkOp)r.Op),
            [typeof(RemoveWatermarkOp)] = static (d, _, _) => WordsObjectOpHandlers.RemoveWatermark(d),
            [typeof(ProtectOp)] = static (d, r, s) => WordsObjectOpHandlers.Protect(d, (ProtectOp)r.Op, s),
            [typeof(UnprotectOp)] = static (d, _, s) => WordsObjectOpHandlers.Unprotect(d, s),
            [typeof(AcceptRevisionsOp)] = static (d, r, _) => WordsObjectOpHandlers.ChangeRevisions(d, ((AcceptRevisionsOp)r.Op).Author, accept: true),
            [typeof(RejectRevisionsOp)] = static (d, r, _) => WordsObjectOpHandlers.ChangeRevisions(d, ((RejectRevisionsOp)r.Op).Author, accept: false),
            [typeof(AddCommentOp)] = static (d, r, _) => WordsObjectOpHandlers.AddComment(d, r.Nodes[0], (AddCommentOp)r.Op),
            [typeof(RemoveCommentsOp)] = static (d, r, _) => WordsObjectOpHandlers.RemoveComments(d, (RemoveCommentsOp)r.Op),
            [typeof(UpdateFieldsOp)] = static (d, r, _) => WordsObjectOpHandlers.UpdateFields(d, (UpdateFieldsOp)r.Op),
        };

    public static long Apply(Document document, ResolvedWordsOp resolved, string? secret)
    {
        if (Handlers.TryGetValue(resolved.Op.GetType(), out var handler))
        {
            return handler(document, resolved, secret);
        }
        throw Invalid(
            $"no Words handler for {resolved.Op.GetType().Name}");
    }
}
