using Microsoft.CodeAnalysis;
using System.IO;

namespace SvSoft.Analyzers.GenericDecoratorGeneration;

public static class DiagnosticsExtensions
{
    public static string GetSourceTreeText(this Location location) =>
        location.SourceTree?.ToString().Substring(location.SourceSpan.Start, location.SourceSpan.Length)
            ?? location.ToString();

    public static string GetFileName(this Diagnostic diagnostic) => Path.GetFileName(diagnostic.Location.SourceTree!.FilePath);
}
