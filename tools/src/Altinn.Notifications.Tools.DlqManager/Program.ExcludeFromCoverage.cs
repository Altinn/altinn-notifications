using System.Diagnostics.CodeAnalysis;

// Applies ExcludeFromCodeCoverage to the synthesised Program class produced by
// top-level statements, so only this entry-point file is excluded from coverage
// rather than the entire assembly.
[ExcludeFromCodeCoverage]
partial class Program
{
    private Program() { }
}
