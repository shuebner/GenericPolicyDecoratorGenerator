using Microsoft.CodeAnalysis.CSharp;

namespace SvSoft.Analyzers.GenericDecoratorGeneration;
static class SyntaxHelper
{
    public static readonly string GlobalNsPrefix = $"{SyntaxFactory.Token(SyntaxKind.GlobalKeyword)}{SyntaxFactory.Token(SyntaxKind.ColonColonToken)}";

    public static string? WithoutGlobalPrefix(string? str) => str?.Substring(GlobalNsPrefix.Length) ?? null;
}
