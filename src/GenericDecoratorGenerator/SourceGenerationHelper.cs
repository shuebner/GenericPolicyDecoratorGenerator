namespace SvSoft.Analyzers.GenericDecoratorGeneration;
internal static class SourceGenerationHelper
{
    public const string Attribute = """        
        namespace SvSoft.Analyzers.GenericDecoratorGeneration
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public class GenericDecoratorAttribute : System.Attribute
            {
            }
        }        
        """;

    public static string GenerateDecorator(DecoratorDescriptor decoratorToGenerate)
    {
        var sb = new StringBuilder();

        if (decoratorToGenerate.Namespace is not null)
        {
            sb.Append($$"""
                namespace {{decoratorToGenerate.Namespace}};

                """);
        }

        sb.Append($$"""
            partial class {{decoratorToGenerate.ClassName}}
            {
                private partial {{decoratorToGenerate.InterfaceName}} GetInner();

            """);

        foreach (var member in decoratorToGenerate.Members)
        {
            AppendDecoratorMember(sb, decoratorToGenerate, member);
        }

        sb.Append("""

                private partial void Decorate(Action doInner);
                private partial T Decorate<T>(Func<T> doInner);
                private partial Task DecorateAsync(Func<Task> doInner);
                private partial Task<T> DecorateAsync<T>(Func<Task<T>> doInner);
            }
            """);

        return sb.ToString();
    }

    private static void AppendDecoratorMember(StringBuilder sb, DecoratorDescriptor decoratorToGenerate, MemberDescriptor member)
    {
        sb.Append($$"""
                {{member.Return}} {{decoratorToGenerate.InterfaceName}}.{{member.Name}}({{string.Join(", ", Array.ConvertAll(member.Parameters.AsArray(), p => string.Concat(p.Expression, p.Expression.Length > 0 ? " " : string.Empty, p.Name)))}}) => {{(member.IsAwaitable ? "DecorateAsync" : "Decorate")}}(() => GetInner().{{GetMemberCall(member)}});

            """);
    }

    private static string GetMemberCall(MemberDescriptor member) => member.Name + "(" + string.Join(", ", Array.ConvertAll(member.Parameters.AsArray(), p => p.Name)) + ")";
}
