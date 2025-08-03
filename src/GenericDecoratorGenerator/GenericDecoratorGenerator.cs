using Microsoft.CodeAnalysis.CSharp;
using NetEscapades.EnumGenerators;

namespace SvSoft.Analyzers.GenericDecoratorGeneration;

[Generator]
public sealed class GenericDecoratorGenerator : IIncrementalGenerator
{
    private const string GenericDecoratorAttributeFullyQualifiedName = "global::SvSoft.Analyzers.GenericDecoratorGeneration.GenericDecoratorAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Add the marker attribute to the compilation
        context.RegisterPostInitializationOutput(ctx =>
        {
            ctx.AddSource(
                "GenericDecoratorAttribute.g.cs",
                SourceText.From(SourceGenerationHelper.Attribute, Encoding.UTF8));
        });

        IncrementalValuesProvider<DecoratorDescriptor> decoratorsToGenerate = context.SyntaxProvider.CreateSyntaxProvider(
            static (n, _) => n is ClassDeclarationSyntax c && c.AttributeLists.Count > 0,
            static (ctx, ct) =>
            {
                var classDeclaration = (ClassDeclarationSyntax)ctx.Node;
                var classSymbol = ctx.SemanticModel.GetDeclaredSymbol(classDeclaration, ct);
                if (classSymbol is null)
                {
                    return null;
                }

                var hasGeneratorAttribute = classSymbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) is GenericDecoratorAttributeFullyQualifiedName);

                if (!hasGeneratorAttribute)
                {
                    return null;
                }

                return GetDecoratorToGenerate(classDeclaration, ctx.SemanticModel, ct);
            })
            .WithTrackingName("IdentifyDecorators")
            .Where(static d => d.HasValue)
            .Select(static (d, _) => d!.Value);

        context.RegisterSourceOutput(decoratorsToGenerate, Execute);
    }

    void Execute(SourceProductionContext context, DecoratorDescriptor decoratorToGenerate)
    {
        // generate the source code and add it to the output
        string result = SourceGenerationHelper.GenerateDecorator(decoratorToGenerate);
        // Create a separate partial class file for each enum
        string sourceHintPrefix = decoratorToGenerate.DecoratorClass.Namespace is string ns
            ? ns.StartsWith(SyntaxHelper.GlobalNsPrefix, StringComparison.Ordinal)
                ? $"{ns.Substring(SyntaxHelper.GlobalNsPrefix.Length)}."
                : $"{ns}."
            : string.Empty;

        string hintName = $"{sourceHintPrefix}{decoratorToGenerate.DecoratorClass.ClassName}.g.cs";
        context.AddSource(hintName, SourceText.From(result, Encoding.UTF8));
    }

    static DecoratorDescriptor? GetDecoratorToGenerate(ClassDeclarationSyntax targetSyntax, SemanticModel semanticModel, CancellationToken ct)
    {
        if (targetSyntax.BaseList?.Types.Count is not > 0)
        {
            return null;
        }

        // Get the semantic representation of the enum syntax
        if (semanticModel.GetDeclaredSymbol(targetSyntax, ct) is not INamedTypeSymbol classSymbol)
        {
            // something went wrong
            return null;
        }

        // Get the full type name of the class e.g. MyService, 
        // or OuterClass<T>.MyService if it was nested in a generic type (for example)
        string className = classSymbol.Name;
        string? classNamespace = GetFullNamespace(classSymbol);

        var decoratedInterfaces = new EquatableArray<InterfaceDescriptor>([.. classSymbol.Interfaces.Select(i => GetInterfaceDescriptor(semanticModel, i, ct))]);

        return new DecoratorDescriptor(
            // use the target syntax instead of the class symbol for location because we specifically want the location with the attribute on it
            new ClassDescriptor(classNamespace, className),
            decoratedInterfaces);

        static string? GetFullNamespace(INamedTypeSymbol namedTypeSymbol) =>
            namedTypeSymbol.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? ns.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            : null;

        static string GetParameterExpression(IParameterSymbol parameter)
        {
            const char Space = ' ';
            StringBuilder sb = new();
            List<SyntaxKind> syntaxes = new();

            if (parameter.IsParams)
            {
                sb.Append(SyntaxFactory.Token(SyntaxKind.ParamsKeyword));
                sb.Append(Space);
            }

            switch (parameter.RefKind)
            {
                case RefKind.Ref:
                    sb.Append(SyntaxFactory.Token(SyntaxKind.RefKeyword));
                    sb.Append(Space);
                    break;
                case RefKind.Out:
                    sb.Append(SyntaxFactory.Token(SyntaxKind.OutKeyword));
                    sb.Append(Space);
                    break;
                case RefKind.In:
                    sb.Append(SyntaxFactory.Token(SyntaxKind.InKeyword));
                    sb.Append(Space);
                    break;
                default:
                    break;
            }
            ;

            sb.Append(parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

            return sb.ToString();
        }

        static InterfaceDescriptor GetInterfaceDescriptor(SemanticModel semanticModel, INamedTypeSymbol decoratedInterface, CancellationToken ct)
        {
            string fullyQualifiedName = decoratedInterface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string simpleName = decoratedInterface.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            string? interfaceNamespace = GetFullNamespace(decoratedInterface);
            // Get all the members in the class
            ImmutableArray<ISymbol> interfaceMembers = decoratedInterface.GetMembers();
            var interfaceMemberDescriptors = new List<MemberDescriptor>(interfaceMembers.Length);

            // Get all the public methods from the class, and add their name to the list
            foreach (ISymbol interfaceMember in interfaceMembers)
            {
                ct.ThrowIfCancellationRequested();

                if (interfaceMember is IMethodSymbol method)
                {
                    var memberDescriptor = new MemberDescriptor(
                        name: method.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        @return: method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        isAwaitable: IsAwaitable(method.ReturnType, semanticModel.Compilation),
                        parameters: new(method.Parameters.Select(p => new ParameterDescriptor(
                            expression: GetParameterExpression(p),
                            name: p.Name)).ToArray()));
                    interfaceMemberDescriptors.Add(memberDescriptor);
                }
            }

            InterfaceDescriptor decoratedInterfaceDescriptor = new(
                @namespace: interfaceNamespace,
                fullyQualifiedName: fullyQualifiedName,
                minimallyQualifiedName: simpleName,
                members: new([.. interfaceMemberDescriptors]));
            return decoratedInterfaceDescriptor;
        }
    }

    static bool IsAwaitable(ITypeSymbol type, Compilation compilation)
    {
        // Handle common known awaitables
        var knownAwaitables = new[]
        {
            "System.Threading.Tasks.Task",
            "System.Threading.Tasks.Task`1",
            "System.Threading.Tasks.ValueTask",
            "System.Threading.Tasks.ValueTask`1",
            "System.Collections.Generic.IAsyncEnumerable`1"
        };

        var original = type.OriginalDefinition;

        foreach (var fullName in knownAwaitables)
        {
            var knownSymbol = compilation.GetTypeByMetadataName(fullName);
            if (knownSymbol != null && SymbolEqualityComparer.Default.Equals(original, knownSymbol))
                return true;
        }

        return false;
    }
}

readonly record struct DecoratorDescriptor
{
    public readonly ClassDescriptor DecoratorClass;
    public readonly EquatableArray<InterfaceDescriptor> DecoratedInterfaces;

    public DecoratorDescriptor(ClassDescriptor decoratorClass, EquatableArray<InterfaceDescriptor> decoratedInterfaces)
    {
        DecoratorClass = decoratorClass;
        DecoratedInterfaces = decoratedInterfaces;
    }
}

readonly record struct ClassDescriptor
{
    public readonly string? Namespace;
    public readonly string ClassName;

    public ClassDescriptor(string? @namespace, string className)
    {
        Namespace = @namespace;
        ClassName = className;
    }
}

readonly record struct InterfaceDescriptor
{
    public readonly string? Namespace;
    public readonly string FullyQualifiedName;
    public readonly string MinimallyQualifiedName;
    public readonly EquatableArray<MemberDescriptor> Members;

    public InterfaceDescriptor(string? @namespace, string fullyQualifiedName, string minimallyQualifiedName, EquatableArray<MemberDescriptor> members)
    {
        Namespace = @namespace;
        FullyQualifiedName = fullyQualifiedName;
        MinimallyQualifiedName = minimallyQualifiedName;
        Members = members;
    }
}

readonly record struct MemberDescriptor
{
    public readonly string Name;
    public readonly string Return;
    public readonly bool IsAwaitable;
    public readonly EquatableArray<ParameterDescriptor> Parameters;

    public MemberDescriptor(string name, string @return, bool isAwaitable, EquatableArray<ParameterDescriptor> parameters)
    {
        Name = name;
        Return = @return;
        IsAwaitable = isAwaitable;
        Parameters = parameters;
    }
}

readonly record struct ParameterDescriptor
{
    public readonly string Expression;
    public readonly string Name;

    public ParameterDescriptor(string expression, string name)
    {
        Expression = expression;
        Name = name;
    }
}