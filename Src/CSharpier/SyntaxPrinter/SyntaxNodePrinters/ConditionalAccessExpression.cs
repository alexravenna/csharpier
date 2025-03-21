namespace CSharpier.SyntaxPrinter.SyntaxNodePrinters;

internal static class ConditionalAccessExpression
{
    public static Doc Print(ConditionalAccessExpressionSyntax node, PrintingContext context)
    {
        return InvocationExpression.PrintMemberChain(node, context);
    }
}
