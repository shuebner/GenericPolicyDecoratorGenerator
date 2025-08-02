namespace SvSoft.Analyzers.GenericPolicyDecoratorGeneration;
public static class SourceGenerationHelper
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
}
