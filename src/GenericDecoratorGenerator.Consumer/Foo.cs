using SvSoft.Analyzers.GenericDecoratorGeneration;

namespace GenericDecoratorGenerator.Consumer;

[GenericDecorator]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Suggesting this on private partial is just wrong.")]
sealed partial class GenericFooDecorator(IFoo inner1, IFoo2 inner2) : IFoo, IFoo2
{
    private partial IFoo GetInnerIFoo() => inner1;
    private partial IFoo2 GetInnerIFoo2() => inner2;

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
    void DoStuff();
    void DoOtherStuff(int arg1);
    Task DoStuffAsync();
    string GetStuff();
    Task<string> GetStuffAsync();
}

interface IFoo2
{
    void DoStuff23();
    void DoOtherStuff2(int arg1);
    Task DoStuffAsync2();
    string GetStuff2();
    Task<string> GetStuffAsync2();
}
