using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SvSoft.Analyzers.GenericDecoratorGeneration.Samples;
using SvSoft.Analyzers.TestUtil;
using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SvSoft.Analyzers.GenericDecoratorGeneration;

public class IncrementalGenerationTests
{
    [Fact]
    public void Given_nothing_Then_generator_generates_its_attribute()
    {
        var runner = new GeneratorScenarioRunner(new GenericDecoratorGenerator());
        var initialResult = runner.Run();
        initialResult.GeneratedSources.Should().ContainSingle().Which.HintName.Should().Be("GenericDecoratorAttribute.g.cs");
        initialResult.TrackedSources.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_class_and_attribute_Then_report_diagnostic_about_missing_interface_declaration()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var runner = new GeneratorScenarioRunner(new GenericDecoratorGenerator());
        const string InitialSource = """
            namespace MyNamespace;

            interface IMyInterface
            {
                System.Threading.Tasks.Task<string> GetStuffAsync(int arg1, string arg2);
            }

            [SvSoft.Analyzers.GenericDecoratorGeneration.GenericDecorator]
            class GenericFooDecorator { }
            """;
        var initialResult = runner.Run(("GenericFooDecorator.cs", InitialSource));
        initialResult.TrackedSources.Should().BeEmpty();

        var diagnostics = await initialResult.Compilation.WithAnalyzers([new GenericDecoratorAnalyzer()]).GetAllDiagnosticsAsync();

        var missingInterfaceDiagnostic = diagnostics.Should().ContainSingle().Subject;
        missingInterfaceDiagnostic.Id.Should().Be("GDG0001");
        missingInterfaceDiagnostic.GetMessage(CultureInfo.InvariantCulture).Should().ContainAll("interface");
        missingInterfaceDiagnostic.GetFileName().Should().Be("GenericFooDecorator.cs");
    }

    [Fact]
    // note that the diagnostics are emitted independent of an existing interface declaration
    public async Task Given_class_with_interface_and_attribute_Then_report_diagnostics_about_missing_partial_method_implementations()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var runner = new GeneratorScenarioRunner(new GenericDecoratorGenerator());
        const string InitialSource = """
            namespace MyNamespace;

            interface IMyInterface
            {
                System.Threading.Tasks.Task<string> GetStuffAsync(int arg1, string arg2);
            }

            [SvSoft.Analyzers.GenericDecoratorGeneration.GenericDecorator]
            class GenericFooDecorator : IMyInterface { }
            """;
        var initialResult = runner.Run(("GenericFooDecorator.cs", InitialSource));

        var diagnostics = await initialResult.Compilation.WithAnalyzers([new GenericDecoratorAnalyzer()]).GetAllDiagnosticsAsync(ct);

        var missingPartialDiagnostics = diagnostics.Where(d => d.Id == "GDG8795").Should().HaveCount(5).And.Subject;
        missingPartialDiagnostics.Should().AllSatisfy(d => d.GetFileName().Should().Be("GenericFooDecorator.cs"));
        missingPartialDiagnostics.Should().ContainSingle(d => d.GetMessage(CultureInfo.InvariantCulture).Contains("GetInnerIMyInterface("));
        missingPartialDiagnostics.Should().ContainSingle(d => d.GetMessage(CultureInfo.InvariantCulture).Contains("Decorate("));
        missingPartialDiagnostics.Should().ContainSingle(d => d.GetMessage(CultureInfo.InvariantCulture).Contains("Decorate<T>("));
        missingPartialDiagnostics.Should().ContainSingle(d => d.GetMessage(CultureInfo.InvariantCulture).Contains("DecorateAsync("));
        missingPartialDiagnostics.Should().ContainSingle(d => d.GetMessage(CultureInfo.InvariantCulture).Contains("DecorateAsync<T>("));
    }

    [Fact]
    // note that the diagnostics are emitted independent of an existing interface declaration
    public async Task Given_class_and_attribute_and_all_partial_methods_Then_report_no_diagnostics_about_missing_partial_method_implementations()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var runner = new GeneratorScenarioRunner(new GenericDecoratorGenerator());
        const string InitialSource = """
            namespace MyNamespace;

            interface IMyInterface
            {
                System.Threading.Tasks.Task<string> GetStuffAsync(int arg1, string arg2);
            }

            [SvSoft.Analyzers.GenericDecoratorGeneration.GenericDecorator]
            class GenericFooDecorator(IMyInterface inner) : MyNamespace.IMyInterface
            {
                private partial MyNamespace.IMyInterface GetInnerIMyInterface() => inner;
                private partial void Decorate(System.Action doInner) => doInner();
                private partial T Decorate<T>(System.Func<T> doInner) => doInner();
                private partial System.Threading.Tasks.Task DecorateAsync(System.Func<System.Threading.Tasks.Task> doInner) => doInner();
                private partial System.Threading.Tasks.Task<T> DecorateAsync<T>(System.Func<System.Threading.Tasks.Task<T>> doInner) => doInner();
            }
            """;
        var initialResult = runner.Run(("GenericFooDecorator.cs", InitialSource));

        var diagnostics = await initialResult.Compilation.WithAnalyzers([new GenericDecoratorAnalyzer()]).GetAllDiagnosticsAsync();

        var missingPartialDiagnostics = diagnostics.Where(d => d.Id == "GDG8795").Should().BeEmpty();
    }

    [Fact]
    public void Given_interface_and_class_When_attribute_is_added_Then_generator_should_generate_decorator()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var runner = new GeneratorScenarioRunner(new GenericDecoratorGenerator());
        const string InitialSource = """
            namespace MyNamespace;

            interface IMyInterface
            {
                System.Threading.Tasks.Task<string> GetStuffAsync(int arg1, string arg2);
            }

            class GenericFooDecorator : IMyInterface { }
            """;
        var initialResult = runner.Run(("GenericFooDecorator.cs", InitialSource));
        initialResult.TrackedSources.Should().BeEmpty();
        initialResult.Diagnostics.Should().ContainSingle(d => d.GetFileName() == "GenericFooDecorator.cs").Which.Should().BeDoesNotImplement<IMyInterface>();

        string sourceWithAttribute = InitialSource.Replace(
            "class GenericFooDecorator",
            """
            [SvSoft.Analyzers.GenericDecoratorGeneration.GenericDecorator]
            class GenericFooDecorator
            """);
        var withAttributeResult = runner.Run(("GenericFooDecorator.cs", sourceWithAttribute));
        var generatorSource = withAttributeResult.TrackedSources.Should().ContainSingle(s => s.HintName == "MyNamespace.GenericFooDecorator.g.cs").Subject;
        generatorSource.SourceText.Should().Be(File.ReadAllText("Samples/AddAttribute/02_expectation.cs_"));
        generatorSource.Reason.Should().Be(IncrementalStepRunReason.New);
        withAttributeResult.Diagnostics.Should().NotContain(d => d.Id == DiagnosticIds.CS0535DoesNotImplement);
        withAttributeResult.Diagnostics.Where(d => d.Severity is DiagnosticSeverity.Error).Should()
            .OnlyContain(d => new[] { DiagnosticIds.CS8795PartialMethodMustHaveAnImplementationPart, DiagnosticIds.CS0260MissingPartialModifier }.Contains(d.Id));

        string sourceWithPartial = sourceWithAttribute.Replace(
            "class GenericFooDecorator : IMyInterface { }",
            """
            partial class GenericFooDecorator(IMyInterface inner) : IMyInterface
            {
                private partial IMyInterface GetInnerIMyInterface() => inner;

                private partial void Decorate(System.Action doInner)
                {
                    doInner();
                }

                private partial T Decorate<T>(System.Func<T> doInner)
                {
                    return doInner();
                }

                private partial System.Threading.Tasks.Task DecorateAsync(System.Func<System.Threading.Tasks.Task> doInner)
                {
                    return doInner();
                }

                private partial System.Threading.Tasks.Task<T> DecorateAsync<T>(System.Func<System.Threading.Tasks.Task<T>> doInner)
                {
                    return doInner();
                }
            }
            """);

        var partialResult = runner.Run(("GenericFooDecorator.cs", sourceWithPartial));
        partialResult.Diagnostics.Should().BeEmpty();
        partialResult.TrackedSources.Should().ContainSingle(s => s.HintName == "MyNamespace.GenericFooDecorator.g.cs")
            .Which.Reason.Should().Be(IncrementalStepRunReason.Cached);
    }

    [Fact]
    public void Given_class_and_attribute_When_interface_is_added_Then_generate_decorator()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var runner = new GeneratorScenarioRunner(new GenericDecoratorGenerator());
        const string InitialSource = """
        namespace MyNamespace;

        interface IMyInterface
        {
            System.Threading.Tasks.Task<string> GetStuffAsync(int arg1, string arg2);
        }

        [SvSoft.Analyzers.GenericDecoratorGeneration.GenericDecorator]
        class GenericFooDecorator { }
        """;
        var initialResult = runner.Run(("GenericFooDecorator.cs", InitialSource));
        initialResult.TrackedSources.Should().BeEmpty();
        initialResult.Diagnostics.Should().BeEmpty();

        string sourceWithInterface = InitialSource.Replace(
            "class GenericFooDecorator { }",
            "class GenericFooDecorator : IMyInterface { }");
        var withInterfaceResult = runner.Run(("GenericFooDecorator.cs", sourceWithInterface));
        var generatorSource = withInterfaceResult.TrackedSources.Should().ContainSingle(s => s.HintName == "MyNamespace.GenericFooDecorator.g.cs").Subject;
        generatorSource.Reason.Should().Be(IncrementalStepRunReason.New);
        withInterfaceResult.Diagnostics.Should().NotContain(d => d.Id == DiagnosticIds.CS0535DoesNotImplement);
        withInterfaceResult.Diagnostics.Where(d => d.Severity is DiagnosticSeverity.Error).Should()
            .OnlyContain(d => new[] { DiagnosticIds.CS8795PartialMethodMustHaveAnImplementationPart, DiagnosticIds.CS0260MissingPartialModifier }.Contains(d.Id));
    }

    [Fact]
    public void Given_class_and_attribute_When_multiple_interfaces_are_added_Then_generate_decorator()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var runner = new GeneratorScenarioRunner(new GenericDecoratorGenerator());
        // the two interfaces have the same method signatures on purpose
        const string InitialSource = """
        namespace MyNamespace;

        interface IMyInterface1
        {
            System.Threading.Tasks.Task<string> GetStuffAsync(int arg1, string arg2);
        }

        interface IMyInterface2
        {
            System.Threading.Tasks.Task<string> GetStuffAsync(int arg1, string arg2);
        }

        [SvSoft.Analyzers.GenericDecoratorGeneration.GenericDecorator]
        class GenericFooDecorator { }
        """;
        var initialResult = runner.Run(("GenericFooDecorator.cs", InitialSource));
        initialResult.TrackedSources.Should().BeEmpty();
        initialResult.Diagnostics.Should().BeEmpty();

        string sourceWithInterface = InitialSource.Replace(
            "class GenericFooDecorator { }",
            "class GenericFooDecorator : IMyInterface1, IMyInterface2 { }");
        var withInterfaceResult = runner.Run(("GenericFooDecorator.cs", sourceWithInterface));
        var generatorSource = withInterfaceResult.TrackedSources.Should().ContainSingle(s => s.HintName == "MyNamespace.GenericFooDecorator.g.cs").Subject;
        generatorSource.Reason.Should().Be(IncrementalStepRunReason.New);
        withInterfaceResult.Diagnostics.Should().NotContain(d => d.Id == DiagnosticIds.CS0535DoesNotImplement);
        withInterfaceResult.Diagnostics.Where(d => d.Severity is DiagnosticSeverity.Error).Should()
            .OnlyContain(d => new[] { DiagnosticIds.CS8795PartialMethodMustHaveAnImplementationPart, DiagnosticIds.CS0260MissingPartialModifier }.Contains(d.Id));
    }

    [Fact]
    public void Given_decorator_exists_When_decorator_name_changes_Then_decorator_is_regenerated_with_updated_name_into_updated_file()
    {
        var runner = new GeneratorScenarioRunner(new GenericDecoratorGenerator());
        const string InitialSource = """
            namespace MyNamespace;

            interface IMyInterface
            {
                System.Threading.Tasks.Task<string> GetStuffAsync(int arg1, string arg2);
            }

            [SvSoft.Analyzers.GenericDecoratorGeneration.GenericDecorator]
            class GenericFooDecorator : IMyInterface { }
            """;

        var initialResult = runner.Run(("GenericFooDecorator.cs", InitialSource));
        var initialDecoratorSource = initialResult.TrackedSources.Should().ContainSingle(s => s.HintName == "MyNamespace.GenericFooDecorator.g.cs").Subject;
        initialDecoratorSource.Reason.Should().Be(IncrementalStepRunReason.New);

        string RenamedSource = InitialSource.Replace("GenericFooDecorator", "GenericBarDecorator");
        // note that the file name did not change
        var renamedResult = runner.Run(("GenericFooDecorator.cs", RenamedSource));
        var renamedDecoratorSource = renamedResult.TrackedSources.Should().ContainSingle(s => s.HintName == "MyNamespace.GenericBarDecorator.g.cs").Subject;
        renamedDecoratorSource.Reason.Should().Be(IncrementalStepRunReason.Modified);
    }

    [Fact]
    public void Given_decorator_exists_When_interface_method_gains_parameter_Then_decorator_is_regenerated_with_updated_interface_method_implementation()
    {
        var runner = new GeneratorScenarioRunner(new GenericDecoratorGenerator());
        const string InitialSource = """
            namespace MyNamespace;

            interface IMyInterface
            {
                System.Threading.Tasks.Task<string> GetStuffAsync(int arg1, string arg2);
            }

            [SvSoft.Analyzers.GenericDecoratorGeneration.GenericDecorator]
            class GenericFooDecorator : IMyInterface { }
            """;

        var initialResult = runner.Run(("GenericFooDecorator.cs", InitialSource));
        var initialDecoratorSource = initialResult.TrackedSources.Should().ContainSingle(s => s.HintName == "MyNamespace.GenericFooDecorator.g.cs").Subject;
        initialDecoratorSource.Reason.Should().Be(IncrementalStepRunReason.New);
        initialDecoratorSource.SourceText.Should().Be(File.ReadAllText("Samples/ChangeInerface/01_initial.cs_"));

        string addedParameterSource = InitialSource.Replace("GetStuffAsync(int arg1, string arg2)", "GetStuffAsync(int arg1, string arg2, int arg3)");
        var addedParameterResult = runner.Run(("GenericFooDecorator.cs", addedParameterSource));
        var addedParameterDecoratorSource = addedParameterResult.TrackedSources.Should().ContainSingle(s => s.HintName == "MyNamespace.GenericFooDecorator.g.cs").Subject;
        addedParameterDecoratorSource.Reason.Should().Be(IncrementalStepRunReason.Modified);
        addedParameterDecoratorSource.SourceText.Should().Be(File.ReadAllText("Samples/ChangeInerface/02_added_method_parameter.cs_"));
    }

    [Fact]
    public void Given_two_decorators_for_two_different_interfaces_exist_When_interface_1_changes_Then_decorator_2_is_not_regenerated()
    {
        var runner = new GeneratorScenarioRunner(new GenericDecoratorGenerator());
        const string InitialSource = """
            namespace MyNamespace;

            interface IMyInterface1
            {
                System.Threading.Tasks.Task<string> GetStuff1Async(int arg1, string arg2);
            }
            
            interface IMyInterface2
            {
                System.Threading.Tasks.Task<string> GetStuff2Async(int arg1, string arg2);
            }

            [SvSoft.Analyzers.GenericDecoratorGeneration.GenericDecorator]
            class GenericFooDecorator1 : IMyInterface1 { }
            
            [SvSoft.Analyzers.GenericDecoratorGeneration.GenericDecorator]
            class GenericFooDecorator2 : IMyInterface2 { }
            """;

        var initialResult = runner.Run(("GenericFooDecorator.cs", InitialSource));
        initialResult.TrackedSources.Should().HaveCount(2);
        initialResult.TrackedSources.Should().AllSatisfy(s => s.Reason.Should().Be(IncrementalStepRunReason.New));

        string interface1ChangedSource = InitialSource.Replace("GetStuff2Async", "GetOtherStuff2Async");
        var interface1ChangedResult = runner.Run(("GenericFooDecorator.cs", interface1ChangedSource));
        var interface1DecoratorSource = interface1ChangedResult.TrackedSources.Should().ContainSingle(s => s.HintName == "MyNamespace.GenericFooDecorator1.g.cs").Subject;
        interface1DecoratorSource.Reason.Should().Be(IncrementalStepRunReason.Cached);
        var interface2DecoratorSource = interface1ChangedResult.TrackedSources.Should().ContainSingle(s => s.HintName == "MyNamespace.GenericFooDecorator2.g.cs").Subject;
        interface2DecoratorSource.Reason.Should().Be(IncrementalStepRunReason.Modified);
    }

    [Fact]
    public void Given_interface_and_class_with_attribute_When_attribute_is_removed_Then_decorator_is_not_generated_anymore()
    {
        var runner = new GeneratorScenarioRunner(new GenericDecoratorGenerator());
        const string InitialSource = """
            namespace MyNamespace;

            interface IMyInterface
            {
                System.Threading.Tasks.Task<string> GetStuffAsync(int arg1, string arg2);
            }

            [SvSoft.Analyzers.GenericDecoratorGeneration.GenericDecorator]
            class GenericFooDecorator : IMyInterface { }
            """;

        var initialResult = runner.Run(("GenericFooDecorator.cs", InitialSource));
        var initialDecoratorSource = initialResult.TrackedSources.Should().ContainSingle(s => s.HintName == "MyNamespace.GenericFooDecorator.g.cs").Subject;
        initialDecoratorSource.Reason.Should().Be(IncrementalStepRunReason.New);

        string attributeRemovedSource = InitialSource.Replace("[SvSoft.Analyzers.GenericDecoratorGeneration.GenericDecorator]", string.Empty);
        var attributeRemovedResult = runner.Run(("GenericFooDecorator.cs", attributeRemovedSource));
        var attributeRemovedDecoratorSource = attributeRemovedResult.TrackedSources.Should().ContainSingle(s => s.HintName == "MyNamespace.GenericFooDecorator.g.cs").Subject;
        attributeRemovedDecoratorSource.Reason.Should().Be(IncrementalStepRunReason.Removed);
    }
}