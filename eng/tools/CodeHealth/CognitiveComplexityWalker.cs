using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aspose.Cli.CodeHealth;

/// <summary>Counts <see cref="SourceMetrics.CognitiveComplexity"/> over one body.</summary>
internal sealed class CognitiveComplexityWalker : CSharpSyntaxWalker
{
    private int _nesting;

    /// <summary>The complexity counted so far.</summary>
    public int Total { get; private set; }

    public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        // A local function is a unit of its own.
    }

    public override void VisitIfStatement(IfStatementSyntax node)
    {
        // An else-if adds 1 without a nesting charge and nests its body like the first if.
        Total += node.Parent is ElseClauseSyntax ? 1 : 1 + _nesting;
        Visit(node.Condition);
        Deeper(() => Visit(node.Statement));
        if (node.Else is { } elseClause)
        {
            if (elseClause.Statement is IfStatementSyntax elseIf)
            {
                Visit(elseIf);
            }
            else
            {
                Total += 1;
                Deeper(() => Visit(elseClause.Statement));
            }
        }
    }

    public override void VisitConditionalExpression(ConditionalExpressionSyntax node) => Structure(node);

    public override void VisitSwitchStatement(SwitchStatementSyntax node) => Structure(node);

    public override void VisitSwitchExpression(SwitchExpressionSyntax node) => Structure(node);

    public override void VisitForStatement(ForStatementSyntax node) => Structure(node);

    public override void VisitForEachStatement(ForEachStatementSyntax node) => Structure(node);

    public override void VisitForEachVariableStatement(ForEachVariableStatementSyntax node) => Structure(node);

    public override void VisitWhileStatement(WhileStatementSyntax node) => Structure(node);

    public override void VisitDoStatement(DoStatementSyntax node) => Structure(node);

    public override void VisitCatchClause(CatchClauseSyntax node) => Structure(node);

    public override void VisitGotoStatement(GotoStatementSyntax node)
    {
        Total += 1;
        base.VisitGotoStatement(node);
    }

    public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) => Deeper(() => DefaultVisit(node));

    public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node) => Deeper(() => DefaultVisit(node));

    public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node) => Deeper(() => DefaultVisit(node));

    public override void VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        if (node.Kind() is SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression)
        {
            Total += StartsRun(node) ? 1 : 0;
        }
        base.VisitBinaryExpression(node);
    }

    public override void VisitBinaryPattern(BinaryPatternSyntax node)
    {
        Total += StartsRun(node) ? 1 : 0;
        base.VisitBinaryPattern(node);
    }

    /// <summary>Adds 1 plus the nesting level and visits the whole structure one level deeper.</summary>
    private void Structure(SyntaxNode node)
    {
        Total += 1 + _nesting;
        Deeper(() => DefaultVisit(node));
    }

    private void Deeper(Action visit)
    {
        _nesting++;
        visit();
        _nesting--;
    }

    /// <summary>Whether <paramref name="node"/> is the outermost operator of a run of its kind.</summary>
    private static bool StartsRun(SyntaxNode node)
    {
        SyntaxNode? parent = node.Parent;
        while (parent is ParenthesizedExpressionSyntax or ParenthesizedPatternSyntax)
        {
            parent = parent.Parent;
        }
        return parent is null || !parent.IsKind(node.Kind());
    }
}
