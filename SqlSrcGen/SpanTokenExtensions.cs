using System;

namespace SqlSrcGen
{
    public static class SpanTokenExtensions
    {
        public static string GetValue(this Span<Token> tokens, int index)
        {
            if (index > tokens.Length - 1)
            {
                if (tokens.Length == 0)
                {
                    throw new InvalidSqlException("Ran out of tokens to parse command.", null);
                }
                throw new InvalidSqlException("Ran out of tokens to parse command.", tokens[tokens.Length - 1]);
            }
            return tokens[index].Value.ToLowerInvariant();
        }
    }
}