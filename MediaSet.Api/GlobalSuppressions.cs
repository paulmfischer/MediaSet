using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

// Suppress analyzer exceptions from RouteHandlerAnalyzer
[assembly: SuppressMessage("Compiler", "AD0001:Analyzer threw an exception",
    Justification = "Known issue with ASP.NET Core RouteHandlerAnalyzer and generic minimal API patterns")]

// Make internal classes visible to test project
[assembly: InternalsVisibleTo("MediaSet.Api.Tests")]
