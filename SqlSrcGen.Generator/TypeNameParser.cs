using System;
using System.Text;

namespace SqlSrcGen.Generator;

public class TypeNameParser : Parser
{
    public void ParseTypeName(ref int index, Span<Token> tokens, out string type)
    {
        AssertEnoughTokens(tokens, index);
        var typeBuilder = new StringBuilder();
        typeBuilder.Append(tokens[index].Value);
        Increment(ref index, 1, tokens);
        int numericLiteralCount = 0;
        bool lastTokenNumericLiteral = false;
        if (tokens.GetValue(index) == "(")
        {
            Increment(ref index, 1, tokens);
            typeBuilder.Append("(");
            for (; index < tokens.Length; index++)
            {
                var token = tokens[index];
                if (token.Value == ")")
                {
                    typeBuilder.Append(token.Value);

                    type = typeBuilder.ToString();
                    index++;
                    return;
                }
                if (lastTokenNumericLiteral)
                {
                    lastTokenNumericLiteral = false;
                    if (token.Value != ",")
                    {
                        throw new InvalidSqlException("expected ',''", token);
                    }
                    typeBuilder.Append(",");
                    continue;
                }
                if (!long.TryParse(token.Value, out long value))
                {
                    throw new InvalidSqlException("expected signed numeric-literal", token);
                }
                lastTokenNumericLiteral = true;
                typeBuilder.Append(token.Value);
                numericLiteralCount++;
                if (numericLiteralCount > 2)
                {
                    throw new InvalidSqlException("type-name can't have more than two numeric-literals", token);
                }
            }
            throw new InvalidSqlException("Ran out of tokens trying to parse type", tokens[0]);
        }
        type = typeBuilder.ToString();
    }
}