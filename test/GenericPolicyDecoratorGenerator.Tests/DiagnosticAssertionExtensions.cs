using AwesomeAssertions;
using AwesomeAssertions.Execution;
using AwesomeAssertions.Primitives;
using Microsoft.CodeAnalysis;
using System;
using System.Linq;

namespace SvSoft.Analyzers.GenericDecoratorGeneration;
public static class DiagnosticAssertionExtensions
{
    public static DiagnosticAssertion Should(this Diagnostic diagnostic) => new(diagnostic, AssertionChain.GetOrCreate());
}

public class DiagnosticAssertion(Diagnostic subject, AssertionChain assertionChain)
    : ReferenceTypeAssertions<Diagnostic, DiagnosticAssertion>(subject, assertionChain)
{
    protected override string Identifier => "diagnostic";

    [CustomAssertion]
    public AndConstraint<DiagnosticAssertion> BeDoesNotImplement<T>(string because = "", params object[] becauseArgs)
    {
        CurrentAssertionChain
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.Id is DiagnosticIds.CS0535DoesNotImplement)
            .FailWith("Expected {context:diagnostic} to have Id {0}{reason}, but found {1}.", DiagnosticIds.CS0535DoesNotImplement, Subject.Id)
            .Then
            .Given(() => new { Type = typeof(T), SourceTreeText = Subject.Location.GetSourceTreeText() })
            .ForCondition(c => new[] { c.Type.Name, c.Type.FullName }.Contains(c.SourceTreeText))
            .FailWith("Expected {context:diagnostic} to refer to type {0}{reason}, but found {1}", c => c.Type.Name, c => c.SourceTreeText);

        return new AndConstraint<DiagnosticAssertion>(this);
    }
}
