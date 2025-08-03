using SvSoft.Analyzers.GenericDecoratorGeneration;

namespace GenericDecoratorGenerator.PackageConsumer;

[GenericDecorator]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Suggesting this on private partial is just wrong.")]
public partial class Class1(IMyInterface myInterface) : IMyInterface
{
    private partial IMyInterface GetInnerIMyInterface() => myInterface;

    private partial void Decorate(Action doInner)
    {
        doInner();
    }

    private partial T Decorate<T>(Func<T> doInner)
    {
        return doInner();
    }

    private partial Task DecorateAsync(Func<Task> doInner)
    {
        return doInner();
    }

    private partial Task<T> DecorateAsync<T>(Func<Task<T>> doInner)
    {
        return doInner();
    }
}

public interface IMyInterface
{
    public string GetStuff(int arg1);
}
