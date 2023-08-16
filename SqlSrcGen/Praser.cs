using System;
using System.Linq;

namespace SqlSrcGen;

public interface IParser
{
    public Query Query
    {
        get;
        set;
    }

    public IDiagnosticsReporter DiagnosticsReporter
    {
        get;
        set;
    }
}

public abstract class Parser : IParser
{
    public Query Query
    {
        get;
        set;
    }

    public IDiagnosticsReporter DiagnosticsReporter
    {
        get;
        set;
    }

    protected void Increment(ref int index, int amount, Span<Token> tokens)
    {
        AssertEnoughTokens(tokens, index + amount);
        index += amount;
    }

    protected void AssertEnoughTokens(Span<Token> tokens, int index)
    {
        if (tokens.Length == 0)
        {
            throw new InvalidSqlException("Ran out of tokens to parse command.", null);
        }
        if (index > tokens.Length - 1)
        {
            throw new InvalidSqlException("Ran out of tokens to parse command.", tokens[tokens.Length - 1]);
        }
    }

    protected void Expect(int index, Span<Token> tokens, params string[] values)
    {
        var value = tokens.GetValue(index);
        if (!values.Contains(value))
        {
            throw new InvalidSqlException($"Expected {string.Join(" or ", values)}", tokens[index]);
        }
    }

    protected bool IsEnd(int index, Span<Token> tokens)
    {
        return index >= tokens.Length;
    }
}