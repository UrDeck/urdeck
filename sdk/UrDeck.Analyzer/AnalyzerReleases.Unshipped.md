; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
URDECK001 | Usage | Error | Widget class must have [Widget]
URDECK002 | Usage | Error | Widget class must have [WidgetSize]
URDECK003 | Usage | Error | Widget must have a refresh strategy attribute
URDECK004 | Usage | Error | Widget must have only one refresh strategy attribute
URDECK005 | Usage | Error | Invalid [WidgetSize] dimensions
URDECK006 | Usage | Warning | A settings property is not declared as a setting, or a declared setting has no kind
