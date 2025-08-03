namespace SvSoft.Analyzers.GenericDecoratorGeneration;

public static class NamespaceCollector
{
    public static HashSet<string> CollectNamespacesFromType(INamedTypeSymbol typeSymbol)
    {
        var namespaces = new HashSet<string>();

        void AddNamespace(ISymbol symbol)
        {
            var ns = symbol.ContainingNamespace;
            if (ns != null && !ns.IsGlobalNamespace)
                namespaces.Add(ns.ToDisplayString());
        }

        void AddTypeNamespaces(ITypeSymbol type)
        {
            if (type == null)
                return;

            if (type is INamedTypeSymbol namedType)
            {
                AddNamespace(namedType);

                // Recurse into type arguments (e.g., IEnumerable<List<string>>)
                foreach (var arg in namedType.TypeArguments)
                {
                    AddTypeNamespaces(arg);
                }

                // Recurse into containing types (for nested types)
                if (namedType.ContainingType != null)
                {
                    AddTypeNamespaces(namedType.ContainingType);
                }

                // Constraints (e.g., where T : SomeBase)
                foreach (var typeParameter in namedType.TypeParameters)
                {
                    foreach (var constraint in typeParameter.ConstraintTypes)
                    {
                        AddTypeNamespaces(constraint);
                    }
                }
            }
            else if (type is IArrayTypeSymbol arrayType)
            {
                AddTypeNamespaces(arrayType.ElementType);
            }
            else if (type is IPointerTypeSymbol pointerType)
            {
                AddTypeNamespaces(pointerType.PointedAtType);
            }
        }

        // Add the interface's own namespace
        AddNamespace(typeSymbol);

        // Process all members
        foreach (var member in typeSymbol.GetMembers())
        {
            switch (member)
            {
                case IMethodSymbol method:
                    AddTypeNamespaces(method.ReturnType);
                    foreach (var param in method.Parameters)
                        AddTypeNamespaces(param.Type);
                    break;

                case IPropertySymbol property:
                    AddTypeNamespaces(property.Type);
                    break;

                case IEventSymbol @event:
                    AddTypeNamespaces(@event.Type);
                    break;

                    // Include more cases as needed
            }
        }

        return namespaces;
    }
}
