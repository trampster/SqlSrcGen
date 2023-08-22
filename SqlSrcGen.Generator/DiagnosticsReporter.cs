using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SqlSrcGen.Generator;

public interface IDiagnosticsReporter
{
    string Path { get; set; }

    void Warning(ErrorCode code, string message, Token token);
}

public class DiagnosticsReporter : IDiagnosticsReporter
{
    readonly GeneratorExecutionContext _context;

    public DiagnosticsReporter(GeneratorExecutionContext context)
    {
        _context = context;
    }

    public string Path
    {
        get;
        set;
    }

    public void Warning(ErrorCode errorCode, string message, Token token)
    {
        _context.ReportDiagnostic(
            Diagnostic.Create(
                new DiagnosticDescriptor(
                    errorCode.ToString(),
                    "SQL Warning",
                    message,
                    "SQL",
                    DiagnosticSeverity.Warning,
                    true),
                Location.Create(Path,
                TextSpan.FromBounds(token.Position, token.Value.Length + token.Position),
                new LinePositionSpan(
                    new LinePosition(token.Line, token.CharacterInLine), new LinePosition(token.Line, token.CharacterInLine + token.Value.Length)))));
    }
}