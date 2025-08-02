using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

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

        IncrementalValuesProvider<ClassDeclarationSyntax?> decoratorsToGenerate = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                GenericPolicyDecoratorAttributeFullyQualifiedName,
                predicate: static (s, _) => IsSyntaxTargetForGeneration(s),
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(decoratorsToGenerate,
            static (spc, source) => Execute(source, spc));
    }

    static bool IsSyntaxTargetForGeneration(SyntaxNode node) => node is ClassDeclarationSyntax m && m.AttributeLists.Count > 0;

    static ClassDeclarationSyntax? GetSemanticTargetForGeneration(GeneratorAttributeSyntaxContext context) => (ClassDeclarationSyntax)context.TargetNode;

    static void Execute(DecoratorToGenerate? decoratorToGenerate, SourceProductionContext context)
    {
        if (decoratorToGenerate is { } value)
        {
            // generate the source code and add it to the output
            string result = SourceGenerationHelper.GenerateDecorator(value);
            // Create a separate partial class file for each enum
            context.AddSource($"GenericPolicyDecorator.{value.Name}.g.cs", SourceText.From(result, Encoding.UTF8));
        }
    }

    static DecoratorToGenerate? GetDecoratorToGenerate(SemanticModel semanticModel, ClassDeclarationSyntax targetSyntax)
    {
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

        // Get all the members in the class
        ImmutableArray<ISymbol> classMembers = decoratedInterface.GetMembers();
        var members = new List<string>(classMembers.Length);

        // Get all the public methods from the class, and add their name to the list
        foreach (ISymbol member in classMembers)
        {
            if (member is IFieldSymbol field && field.ConstantValue is not null)
            {
                members.Add(member.Name);
            }
        }

        // Create an EnumToGenerate for use in the generation phase
        enumsToGenerate.Add(new EnumToGenerate(className, members));

        foreach (ISymbol member in classMembers)
        {
            if (member is IFieldSymbol field && field.ConstantValue is not null)
            {
                members.Add(member.Name);
            }
        }

        return new EnumToGenerate(className, members);
    }

    readonly record struct DecoratorToGenerate
    {
        public readonly string InterfaceName;
        public readonly NetEscapades.EnumGenerators.EquatableArray<Member> Members;
    }

    readonly record struct Member
    {

    }
}