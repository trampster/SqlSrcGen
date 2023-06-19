using System;

namespace SqlSrcGen;

public class LiteralValueParser : Parser
{
    public bool Parse(ref int index, Span<Token> tokens)
    {
        switch (tokens.GetValue(index))
        {
            case "null":
            case "true":
            case "false":
            case "current_time":
            case "current_date":
            case "current_timestamp":
                index++;
                return true;
            default:
                var token = tokens[index];
                if (token.TokenType == TokenType.StringLiteral)
                {
                    index++;
                    return true;
                }
                if (token.TokenType == TokenType.NumericLiteral)
                {
                    index++;
                    return true;
                }
                if (token.TokenType == TokenType.BlobLiteral)
                {
                    index++;
                    return true;
                }
                return false;
        }
    }
}