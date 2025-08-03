using SvSoft.Analyzers.GenericDecoratorGeneration;

namespace GenericDecoratorGenerator.Consumer;

[GenericDecorator]
partial class GenericFooDecorator(IFoo inner) : IFoo
{
    private partial IFoo GetInner() => inner;

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

interface IFoo
{
    void DoStuff2();
    void DoOtherStuff(int arg1);
    Task DoStuffAsync();
    string GetStuff();
    Task<string> GetStuffAsync();
}
