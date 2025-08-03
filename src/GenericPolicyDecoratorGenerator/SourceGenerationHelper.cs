namespace SvSoft.Analyzers.GenericPolicyDecoratorGeneration;
internal static class SourceGenerationHelper
{
    public const string Attribute = """        
        namespace SvSoft.Analyzers.GenericPolicyDecoratorGeneration
        {
            [System.AttributeUsage(System.AttributeTargets.Interface)]
            public class GenericPolicyDecoratorAttribute : System.Attribute
            {
            }
        }        
        """;

    public const string GenericPolicy = """
        interface IGenericPolicy
        {
            void Apply(Action do);
            T Apply<T>(Func<T> do);
            Task ApplyAsync(Func<Task> do);
            Task<T> ApplyAsync<T>(Func<Task<T>> do);
        }
        """;

    public static string GenerateDecorator(DecoratorDescriptor decoratorToGenerate)
    {
        var sb = new StringBuilder();
        foreach (var @using in decoratorToGenerate.Usings)
        {
            sb.Append($$"""
            using {{@using}};
            """);
        }

        sb.Append($$"""
            namespace {{decoratorToGenerate.Namespace}}
            {
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
            }
            """);

        return sb.ToString();
    }

    private static void AppendDecoratorMember(StringBuilder sb, DecoratorDescriptor decoratorToGenerate, MemberDescriptor member)
    {
        sb.Append($$"""
                {{member.Return}} {{decoratorToGenerate.InterfaceName}}.{{member.Name}}({{string.Join(", ", Array.ConvertAll(member.Parameters.AsArray(), p => string.Join(p.Expression, " ", p.Name)))}}) => {{(member.Return.StartsWith(nameof(Task)) ? "DecorateAsync" : "Decorate")}}(() => GetInner().{{GetMemberCall(member)}}
                {
            """);
    }

    private static string GetMemberCall(MemberDescriptor member) => string.Join(", ", Array.ConvertAll(member.Parameters.AsArray(), p => p.Name));
}
