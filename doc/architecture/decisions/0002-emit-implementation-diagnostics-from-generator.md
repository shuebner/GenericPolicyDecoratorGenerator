# 2. Emit implementation diagnostics from generator

Date: 2025-10-26

## Status

Accepted

## Context
Partial implementation diagnostics (e. g. CS8795) are emitted for the partial declaration location.
When the declaration is in generated code, the diagnostic will be emitted for that generated code location.
This is bad DevXP, because the dev cannot see the error in their own partial class, cannot navigate to the diagnostic's location, cannot use code-fixes etc..
Using #line directive is a partial solution (https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/preprocessor-directives#error-and-warning-information).
It will work when using the CLI.
VS, however ignores the #line directive by design.

## Decision

The generator package has to emit its own diagnostics on the dev-provided partial class.
Emitting diagnostics directly from a SourceGenerator is discouraged and may be deprecated in the future (see https://github.com/dotnet/roslyn/issues/71709#issuecomment-1899076598).
Thus, a companion DiagnosticAnalyzer will be added to emit the diagnostic.

## Consequences

Additional DiagnosticAnalyzer
