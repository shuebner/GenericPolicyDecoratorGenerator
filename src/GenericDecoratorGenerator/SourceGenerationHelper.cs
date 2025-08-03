namespace SvSoft.Analyzers.GenericDecoratorGeneration;
internal static class SourceGenerationHelper
{
    public static readonly string Attribute;

    static SourceGenerationHelper()
    {
        var attributeStream = typeof(SourceGenerationHelper).Assembly
            .GetManifestResourceStream("SvSoft.Analyzers.GenericDecoratorGeneration.GenericDecoratorAttribute.cs");
        using StreamReader reader = new(attributeStream);

        Attribute = reader.ReadToEnd();
    }

    public static string GenerateDecorator(DecoratorDescriptor decoratorToGenerate)
    {
        var sb = new StringBuilder();

        if (decoratorToGenerate.DecoratorClass.Namespace is not null)
        {
            sb.Append($$"""
                namespace {{SyntaxHelper.WithoutGlobalPrefix(decoratorToGenerate.DecoratorClass.Namespace)}};

                """);
        }

        sb.Append($$"""

            partial class {{decoratorToGenerate.DecoratorClass.ClassName}}
            {
                private partial void Decorate(global::System.Action doInner);
                private partial T Decorate<T>(global::System.Func<T> doInner);
                private partial global::System.Threading.Tasks.Task DecorateAsync(global::System.Func<global::System.Threading.Tasks.Task> doInner);
                private partial global::System.Threading.Tasks.Task<T> DecorateAsync<T>(global::System.Func<global::System.Threading.Tasks.Task<T>> doInner);

            """);

        foreach (var interfaceDescriptor in decoratorToGenerate.DecoratedInterfaces)
        {
            var getInstanceOfDecoratedInterfaceMethodName = GetGetInstanceOfDecoratedInterfaceMethodName(interfaceDescriptor);
            sb.Append($$"""

                private partial {{interfaceDescriptor.FullyQualifiedName}} {{getInstanceOfDecoratedInterfaceMethodName}}();

            """);

            foreach (var member in interfaceDescriptor.Members)
            {
                AppendDecoratorMember(sb, interfaceDescriptor, member);
            }
        }

        sb.AppendLine();
        sb.AppendLine("}");

        return sb.ToString();
    }

    public static string GetGetInstanceOfDecoratedInterfaceMethodName(InterfaceDescriptor interfaceDescriptor) =>
        GetGetInstanceOfDecoratedInterfaceMethodName(interfaceDescriptor.MinimallyQualifiedName);

    public static string GetGetInstanceOfDecoratedInterfaceMethodName(INamedTypeSymbol decoratedInterface) =>
        GetGetInstanceOfDecoratedInterfaceMethodName(decoratedInterface.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));

    public static string GetGetInstanceOfDecoratedInterfaceMethodName(string interfaceName)
    {
        return $"GetInner{interfaceName}";
    }

    private static void AppendDecoratorMember(StringBuilder sb, InterfaceDescriptor interfaceDescriptor, MemberDescriptor member)
    {
        sb.Append($$"""

                {{member.Return}} {{interfaceDescriptor.FullyQualifiedName}}.{{member.Name}}({{string.Join(", ", Array.ConvertAll(member.Parameters.AsArray(), p => string.Concat(p.Expression, p.Expression.Length > 0 ? " " : string.Empty, p.Name)))}}) => {{(member.IsAwaitable ? "DecorateAsync" : "Decorate")}}(() => {{GetGetInstanceOfDecoratedInterfaceMethodName(interfaceDescriptor)}}().{{GetMemberCall(member)}});
            """);
    }

    private static string GetMemberCall(MemberDescriptor member) => member.Name + "(" + string.Join(", ", Array.ConvertAll(member.Parameters.AsArray(), p => p.Name)) + ")";
}
