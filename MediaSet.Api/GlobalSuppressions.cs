using System.Diagnostics.CodeAnalysis;

// Suppress analyzer exceptions from RouteHandlerAnalyzer
[assembly: SuppressMessage("Compiler", "AD0001:Analyzer threw an exception", 
    Justification = "Known issue with ASP.NET Core RouteHandlerAnalyzer and generic minimal API patterns")]