using System;

namespace SqlSrcGen.Generator;

public class NumberParser : Parser
{
    public bool ParseSignedNumber(ref int index, Span<Token> tokens)
    {
        var tokenValue = tokens.GetValue(index);
        if (tokenValue == "+" || tokenValue == "-")
        {
            Increment(ref index, 1, tokens);
            if (tokens[index].TokenType != TokenType.NumericLiteral)
            {
                throw new InvalidSqlException($"missing numeric literal in signed number", tokens[index]);
            }
            index++;
            return true;
        }
        if (tokens[index].TokenType == TokenType.NumericLiteral)
        {
            index++;
            return true;
        }

        return false;
    }
}