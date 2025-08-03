using Microsoft.CodeAnalysis.Diagnostics;

namespace SvSoft.Analyzers.GenericDecoratorGeneration;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GenericDecoratorAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor MustDeclareInterface = new(
        id: "GDG0001",
        title: "Type with GenericDecoratorAttribute must declare an interface.",
        messageFormat: "Add the interface declaration for the interface to be decorated.",
        category: "SvSoft.GenericDecoratorGenerator",
        DiagnosticSeverity.Warning,
        true,
        customTags: WellKnownDiagnosticTags.Compiler);

    private static readonly DiagnosticDescriptor MustImplementPartialMethod = new(
        id: "GDG8795",
        title: "Partial member {0} must have an implementation part because it has accessibility modifiers.",
        messageFormat: "Implement {0} on {1}",
        category: "SvSoft.GenericDecoratorGenerator",
        DiagnosticSeverity.Error,
        true,
        customTags: WellKnownDiagnosticTags.Compiler);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        MustDeclareInterface,
        MustImplementPartialMethod);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private readonly struct TypeSymbols
    {
        public readonly INamedTypeSymbol ActionType;
        public readonly INamedTypeSymbol TaskType;
        public readonly INamedTypeSymbol TaskTType;
        public readonly INamedTypeSymbol FuncOfTType;
        public readonly INamedTypeSymbol FuncOfTaskType;
        public readonly INamedTypeSymbol FuncOfTaskTType;

        public TypeSymbols(
            INamedTypeSymbol actionType,
            INamedTypeSymbol taskType,
            INamedTypeSymbol taskTType,
            INamedTypeSymbol funcOfTType,
            INamedTypeSymbol funcOfTaskType,
            INamedTypeSymbol funcOfTaskTType)
        {
            ActionType = actionType;
            TaskType = taskType;
            TaskTType = taskTType;
            FuncOfTType = funcOfTType;
            FuncOfTaskType = funcOfTaskType;
            FuncOfTaskTType = funcOfTaskTType;
        }
    }

    private void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var actionType = context.Compilation.GetTypeByMetadataName(typeof(Action).FullName)!;
        var funcOfTType = context.Compilation.GetTypeByMetadataName(typeof(Func<>).FullName)!;
        var taskType = context.Compilation.GetTypeByMetadataName(typeof(Task).FullName)!;
        var taskTType = context.Compilation.GetTypeByMetadataName(typeof(Task<>).FullName)!;
        var funcOfTaskType = funcOfTType.Construct(taskType);
        var funcOfTaskTType = funcOfTType.Construct(taskTType);

        var typeSymbols = new TypeSymbols(
            actionType: actionType,
            taskType: taskType,
            taskTType: taskTType,
            funcOfTType: funcOfTType,
            funcOfTaskType: funcOfTaskType,
            funcOfTaskTType: funcOfTaskTType);

        context.RegisterSymbolAction(c => AnalyzeSymbol(c, typeSymbols), SymbolKind.NamedType);
    }

    private void AnalyzeSymbol(SymbolAnalysisContext context, TypeSymbols typeSymbols)
    {
        if (context.Symbol is not INamedTypeSymbol typeSymbol)
        {
            return;
        }

        ImmutableArray<AttributeData> attributes = typeSymbol.GetAttributes();
        bool hasGenericDecoratorAttribute = false;
        foreach (var attribute in attributes)
        {
            if (attribute.AttributeClass?.ToString() is "SvSoft.Analyzers.GenericDecoratorGeneration.GenericDecoratorAttribute")
            {
                hasGenericDecoratorAttribute = true;
                break;
            }
        }

        if (!hasGenericDecoratorAttribute)
        {
            return;
        }

        string decoratorName = typeSymbol.ToString();
        Location decoratorDeclarationLocation = typeSymbol.Locations[0];

        // interface not added yet
        if (typeSymbol.Interfaces.IsEmpty)
        {
            context.ReportDiagnostic(Diagnostic.Create(MustDeclareInterface, decoratorDeclarationLocation));
            return;
        }

        var methods = typeSymbol.GetMembers().OfType<IMethodSymbol>().ToImmutableArray();

        foreach (var decoratedInterface in typeSymbol.Interfaces)
        {
            var getInnerMethodName = SourceGenerationHelper.GetGetInstanceOfDecoratedInterfaceMethodName(decoratedInterface);
            IMethodSymbol? getInner = methods.SingleOrDefault(m => IsGetInner(m, decoratedInterface, getInnerMethodName));
            if (getInner is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(MustImplementPartialMethod, decoratorDeclarationLocation, $"{decoratedInterface} {getInnerMethodName}()", decoratorName));
            }
        }

        IMethodSymbol? decorate = methods.SingleOrDefault(IsDecorate);
        if (decorate is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(MustImplementPartialMethod, decoratorDeclarationLocation, $"void Decorate(Action doInner)", decoratorName));
        }

        IMethodSymbol? decorateT = methods.SingleOrDefault(IsDecorateT);
        if (decorateT is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(MustImplementPartialMethod, decoratorDeclarationLocation, $"T Decorate<T>(Func<T> doInner)", decoratorName));
        }

        IMethodSymbol? decorateAsync = methods.SingleOrDefault(IsDecorateAsync);
        if (decorateAsync is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(MustImplementPartialMethod, decoratorDeclarationLocation, $"Task DecorateAsync(Func<Task> doInner)", decoratorName));
        }

        IMethodSymbol? decorateAsyncT = methods.SingleOrDefault(IsDecorateAsyncT);
        if (decorateAsyncT is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(MustImplementPartialMethod, decoratorDeclarationLocation, $"Task<T> DecorateAsync<T>(Func<Task<T>> doInner)", decoratorName));
        }

        bool IsGetInner(IMethodSymbol method, INamedTypeSymbol returnType, string methodName) =>
            method.Name == methodName
            && method.Parameters.IsEmpty
            && method.ReturnType.Equals(returnType, SymbolEqualityComparer.IncludeNullability)
            && method.PartialImplementationPart is not null;

        bool IsDecorate(IMethodSymbol method) =>
            method.Name == "Decorate"
            && method.ReturnsVoid
            && method.Parameters.Length is 1
            && method.Parameters[0].Type.Equals(typeSymbols.ActionType, SymbolEqualityComparer.IncludeNullability)
            && method.PartialImplementationPart is not null;

        bool IsDecorateT(IMethodSymbol method)
        {
            if (method.Name == "Decorate" && method.IsGenericMethod && method.Arity is 1 && method.Parameters.Length is 1)
            {
                var typeParameter = method.TypeParameters[0];

                var funcOfTypeParameterType = typeSymbols.FuncOfTType.Construct(typeParameter);
                if (method.Parameters[0].Type.Equals(funcOfTypeParameterType, SymbolEqualityComparer.IncludeNullability) &&
                    method.ReturnType.Equals(typeParameter, SymbolEqualityComparer.IncludeNullability))
                {
                    return method.PartialImplementationPart is not null;
                }
            }

            return false;
        }

        bool IsDecorateAsync(IMethodSymbol method) =>
            method.Name == "DecorateAsync"
            && method.ReturnType.Equals(typeSymbols.TaskType, SymbolEqualityComparer.IncludeNullability)
            && method.Parameters.Length is 1
            && method.Parameters[0].Type.Equals(typeSymbols.FuncOfTaskType, SymbolEqualityComparer.IncludeNullability)
            && method.PartialImplementationPart is not null;

        bool IsDecorateAsyncT(IMethodSymbol method)
        {
            if (method.Name == "DecorateAsync" && method.IsGenericMethod && method.Arity is 1 && method.Parameters.Length is 1)
            {
                var typeParameter = method.TypeParameters[0];

                var taskOfTypeParameterType = typeSymbols.TaskTType.Construct(typeParameter);
                var funcOfTaskOfTypeParameterType = typeSymbols.FuncOfTType.Construct(taskOfTypeParameterType);
                if (method.Parameters[0].Type.Equals(funcOfTaskOfTypeParameterType, SymbolEqualityComparer.IncludeNullability) &&
                    method.ReturnType.Equals(taskOfTypeParameterType, SymbolEqualityComparer.IncludeNullability))
                {
                    return method.PartialImplementationPart is not null;
                }
            }

            return false;
        }
    }
}
