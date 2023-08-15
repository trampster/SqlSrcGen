using System;

namespace SqlSrcGen;

public class CollationParser : Parser
{
    public void ParseCollationStatement(ref int index, Span<Token> tokens)
    {
        Expect(index, tokens, "collate");
        Increment(ref index, 1, tokens);

        switch (tokens.GetValue(index))
        {
            case "nocase":
            case "binary":
            case "rtrim":
                break;
            default:
                DiagnosticsReporter.Warning(ErrorCode.SSG0002, "Collation types other than nocase, binary and rtrim require custom collation creation", tokens[index]);
                break;
        }
        index++;
    }

    public void PraseCollateConstraint(Span<Token> columnDefinition, ref int index, Column column)
    {
        int startIndex = index;
        ParseCollationStatement(ref index, columnDefinition);
        if (column.TypeAffinity != TypeAffinity.TEXT)
        {
            DiagnosticsReporter.Warning(ErrorCode.SSG0003, "Collation only affects Text columns", columnDefinition[startIndex]);
        }
    }
}