using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NetEscapades.EnumGenerators;

namespace SvSoft.Analyzers.GenericDecoratorGeneration;

[Generator]
public class GenericDecoratorGenerator : IIncrementalGenerator
{
    private const string GenericDecoratorAttributeFullyQualifiedName = "SvSoft.Analyzers.GenericDecoratorGeneration.GenericDecoratorAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Add the marker attribute to the compilation
        context.RegisterPostInitializationOutput(ctx =>
        {
            ctx.AddSource(
                "GenericDecoratorAttribute.g.cs",
                SourceText.From(SourceGenerationHelper.Attribute, Encoding.UTF8));
        });

        IncrementalValuesProvider<DecoratorDescriptor> decoratorsToGenerate = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                GenericDecoratorAttributeFullyQualifiedName,
                predicate: static (s, _) => IsSyntaxTargetForGeneration(s),
                transform: static (ctx, _) => GetDecoratorToGenerate(ctx))
            .Where(d => d.HasValue)
            .Select(static (d, _) => d!.Value);

        context.RegisterSourceOutput(decoratorsToGenerate,
            static (spc, source) => Execute(spc, source));
    }

    static bool IsSyntaxTargetForGeneration(SyntaxNode node) => node is ClassDeclarationSyntax;

    static readonly string GlobalNsPrefix = $"{SyntaxFactory.Token(SyntaxKind.GlobalKeyword)}{SyntaxFactory.Token(SyntaxKind.ColonColonToken)}";

    static void Execute(SourceProductionContext context, DecoratorDescriptor decoratorToGenerate)
    {
        if (decoratorToGenerate is { } value)
        {
            // generate the source code and add it to the output
            string result = SourceGenerationHelper.GenerateDecorator(decoratorToGenerate);
            // Create a separate partial class file for each enum
            string sourceHintPrefix = decoratorToGenerate.Namespace is string ns
                ? ns.StartsWith(GlobalNsPrefix)
                    ? $"{ns.Substring(GlobalNsPrefix.Length)}."
                    : $"{ns}."
                : string.Empty;

            context.AddSource($"{sourceHintPrefix}{value.ClassName}.g.cs", SourceText.From(result, Encoding.UTF8));
        }
    }

    static DecoratorDescriptor? GetDecoratorToGenerate(GeneratorAttributeSyntaxContext context)
    {
        var semanticModel = context.SemanticModel;
        var targetSyntax = context.TargetNode;

        // Get the semantic representation of the enum syntax
        if (semanticModel.GetDeclaredSymbol(targetSyntax) is not INamedTypeSymbol classSymbol)
        {
            // something went wrong
            return null;
        }

        if (classSymbol.Interfaces.Length is not 1)
        {
            // TODO: issue diagnostic
            return null;
        }

        var decoratedInterface = classSymbol.Interfaces[0];

        // Get the full type name of the class e.g. MyService, 
        // or OuterClass<T>.MyService if it was nested in a generic type (for example)
        string className = classSymbol.Name;
        string? classNamespace = classSymbol.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? ns.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            : null;
        HashSet<string> usedNamespaces = NamespaceCollector.CollectNamespacesFromType(decoratedInterface);

        // Get all the members in the class
        ImmutableArray<ISymbol> interfaceMembers = decoratedInterface.GetMembers();
        var memberDescriptors = new List<MemberDescriptor>(interfaceMembers.Length);

        // Get all the public methods from the class, and add their name to the list
        foreach (ISymbol interfaceMember in interfaceMembers)
        {
            if (interfaceMember is IMethodSymbol method)
            {
                var memberDescriptor = new MemberDescriptor(
                    name: method.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    @return: method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    isAwaitable: IsAwaitable(method.ReturnType, semanticModel.Compilation),
                    parameters: new(method.Parameters.Select(p => new ParameterDescriptor(
                        expression: GetParameterExpression(p),
                        name: p.Name)).ToArray()));
                memberDescriptors.Add(memberDescriptor);
            }
        }

        string GetParameterExpression(IParameterSymbol parameter)
        {
            StringBuilder sb = new();
            List<SyntaxKind> syntaxes = new();
            
            if (parameter.IsParams)
            {
                sb.Append(SyntaxFactory.Token(SyntaxKind.ParamsKeyword));
                sb.Append(" ");
            }

            switch (parameter.RefKind)
            {
                case RefKind.Ref:
                    sb.Append(SyntaxFactory.Token(SyntaxKind.RefKeyword));
                    break;
                case RefKind.Out:
                    sb.Append(SyntaxFactory.Token(SyntaxKind.OutKeyword));
                    break;
                case RefKind.In:
                    sb.Append(SyntaxFactory.Token(SyntaxKind.InKeyword));
                    break;
                default:
                    break;
            };

            sb.Append(" ");
            sb.Append(parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

            return sb.ToString();
        }

        return new DecoratorDescriptor(
            @namespace: WithoutGlobalPrefix(classNamespace),
            className: className,
            usings: new(usedNamespaces.ToArray()),
            interfaceName: decoratedInterface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            members: new(memberDescriptors.ToArray()));
    }

    static string GetFullNamespaceWithoutGlobalPrefix(ISymbol symbol)
    {
        var fullyQualifiedNsWithGlobalPrefix = symbol.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var fullyQualifiedNsWithoutGlobalPrefix = fullyQualifiedNsWithGlobalPrefix.Substring(GlobalNsPrefix.Length);
        return fullyQualifiedNsWithoutGlobalPrefix;
    }

    static string? WithoutGlobalPrefix(string? str) => str?.Substring(GlobalNsPrefix.Length) ?? null;
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
    public readonly string? Namespace;
    public readonly string ClassName;
    public readonly EquatableArray<string> Usings;

    public readonly string InterfaceName;
    public readonly EquatableArray<MemberDescriptor> Members;

    public DecoratorDescriptor(string? @namespace, string className, EquatableArray<string> usings, string interfaceName, EquatableArray<MemberDescriptor> members)
    {
        Namespace = @namespace;
        ClassName = className;
        Usings = usings;
        InterfaceName = interfaceName;
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