using NetEscapades.EnumGenerators;

namespace SvSoft.Analyzers.GenericPolicyDecoratorGeneration;

[Generator]
public class GenericPolicyDecoratorGenerator : IIncrementalGenerator
{
    private const string GenericPolicyDecoratorAttributeFullyQualifiedName = "SvSoft.Analyzers.GenericPolicyDecoratorGeneration.GenericPolicyDecoratorAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Add the marker attribute to the compilation
        context.RegisterPostInitializationOutput(ctx =>
        {
            ctx.AddSource(
                "GenericPolicyDecoratorAttribute.g.cs",
                SourceText.From(SourceGenerationHelper.Attribute, Encoding.UTF8));

            ctx.AddSource(
                "GenericPolicy.g.cs",
                SourceText.From(SourceGenerationHelper.GenericPolicy, Encoding.UTF8));
        });

        IncrementalValuesProvider<DecoratorDescriptor> decoratorsToGenerate = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                GenericPolicyDecoratorAttributeFullyQualifiedName,
                predicate: static (s, _) => IsSyntaxTargetForGeneration(s),
                transform: static (ctx, _) => GetDecoratorToGenerate(ctx))
            .Where(d => d.HasValue)
            .Select(static (d, _) => d!.Value);

        context.RegisterSourceOutput(decoratorsToGenerate,
            static (spc, source) => Execute(spc, source));
    }

    static bool IsSyntaxTargetForGeneration(SyntaxNode node) => node is ClassDeclarationSyntax;

    static void Execute(SourceProductionContext context, DecoratorDescriptor decoratorToGenerate)
    {
        if (decoratorToGenerate is { } value)
        {
            // generate the source code and add it to the output
            string result = SourceGenerationHelper.GenerateDecorator(decoratorToGenerate);
            // Create a separate partial class file for each enum
            context.AddSource($"{value.Namespace}.{value.ClassName}.g.cs", SourceText.From(result, Encoding.UTF8));
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
        string className = classSymbol.ToString();
        string classNamespace = classSymbol.ContainingNamespace.Name;
        HashSet<string> usedNamespaces = NamespaceCollector.CollectNamespacesFromType(decoratedInterface);

        // Get all the members in the class
        ImmutableArray<ISymbol> interfaceMembers = decoratedInterface.GetMembers();
        var members = new List<MemberDescriptor>(interfaceMembers.Length);

        // Get all the public methods from the class, and add their name to the list
        foreach (ISymbol interfaceMember in interfaceMembers)
        {
            if (interfaceMember is IMethodSymbol method)
            {
                var memberDescriptor = new MemberDescriptor(
                    name: interfaceMember.Name,
                    @return: method.ReturnType.Name,
                    parameters: method.Parameters.Select(p => new ParameterDescriptor(
                        expression: p.para)))
                members.Add(interfaceMember.Name);
            }
        }

        // Create an EnumToGenerate for use in the generation phase
        enumsToGenerate.Add(new EnumToGenerate(className, members));

        return new DecoratorToGenerate(
            @namespace: classNamespace,
            className: className,
            usings: new EquatableArray<string>(usedNamespaces.ToArray()),
            interfaceName: decoratedInterface.Name,
            members: members);
    }
}

readonly record struct DecoratorDescriptor
{
    public readonly string Namespace;
    public readonly string ClassName;
    public readonly EquatableArray<string> Usings;

    public readonly string InterfaceName;
    public readonly EquatableArray<MemberDescriptor> Members;

    public DecoratorDescriptor(string @namespace, string className, EquatableArray<string> usings, string interfaceName, EquatableArray<MemberDescriptor> members)
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
    public readonly EquatableArray<ParameterDescriptor> Parameters;

    public MemberDescriptor(string name, string @return, EquatableArray<ParameterDescriptor> parameters)
    {
        Name = name;
        Return = @return;
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