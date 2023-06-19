using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSrcGen;

public class ExpressionParser : Parser
{
    readonly LiteralValueParser _literalValueParser;
    readonly DatabaseInfo _databaseInfo;

    public ExpressionParser(DatabaseInfo databaseInfo, LiteralValueParser literalValueParser)
    {
        _databaseInfo = databaseInfo;
        _literalValueParser = literalValueParser;
    }

    public bool Parse(ref int index, Span<Token> tokens, Table table)
    {
        if (!ParseExpr(ref index, tokens, table))
        {
            return false;
        }
        if (ParseBooleanOperator(ref index, tokens))
        {
            return Parse(ref index, tokens, table);
        }
        return true;
    }

    public bool ParseBooleanOperator(ref int index, Span<Token> tokens)
    {
        if (index >= tokens.Length)
        {
            return false;
        }
        if (tokens[index].BinaryOperator)
        {
            index++;
            return true;
        }
        return false;
    }

    bool ParseBindParam(ref int index, Span<Token> tokens, Table table)
    {
        var tokenValue = tokens.GetValue(index);
        if (tokenValue == "?")
        {
            // auto numbered binding (one higher than highest so far)
            Query.AddAutoNumbered(tokens[index]);
            index++;
            return true;
        }
        else if (tokenValue.StartsWith("?"))
        {
            // numbered binding
            if (!uint.TryParse(tokenValue.AsSpan().Slice(1).ToString(), out uint number))
            {
                throw new InvalidSqlException("Numbered parameter must be a positive integer", tokens[index]);
            }
            Query.AddNumberedParameter(number, tokens[index]);
            index++;
            return true;
        }
        else if (tokenValue.StartsWith(":"))
        {
            // named parameter, these are also auto numbered
            Query.AddNamedParameter(tokens[index].Value, tokens[index]);
            index++;
            return true;
        }
        else if (tokenValue.StartsWith("@"))
        {
            // named parameter, these are also auto numbered
            Query.AddNamedParameter(tokens[index].Value, tokens[index]);
            index++;
            return true;
        }
        else if (tokenValue.StartsWith("$"))
        {
            // named parameter, these are also auto numbered
            Query.AddNamedParameter(tokens[index].Value, tokens[index]);
            index++;
            return true;
        }

        return false;
    }

    bool ParseFunction(ref int index, Span<Token> tokens, Table table)
    {
        AssertEnoughTokens(tokens, index);
        if (tokens[index].TokenType != TokenType.Other)
        {
            return false;
        }
        if (index + 1 > tokens.Length - 1)
        {
            return false;
        }
        if (tokens[index + 1].Value != "(")
        {
            return false;
        }
        //it's a funciton
        Increment(ref index, 2, tokens);

        bool endBracketFound = false;
        while (!endBracketFound)
        {
            switch (tokens.GetValue(index))
            {
                case "*":
                    Increment(ref index, 1, tokens);
                    if (tokens[index].Value != ")")
                    {
                        throw new InvalidSqlException("Expected ')'", tokens[index]);
                    }
                    index++;
                    endBracketFound = true;
                    break;
                case ")":
                    index++;
                    endBracketFound = true;
                    break;
                case "distinct":
                    while (true)
                    {
                        index++;
                        Parse(ref index, tokens, table);
                        var value = tokens.GetValue(index);
                        if (value == ")")
                        {
                            index++;
                            endBracketFound = true;
                            break;
                        }
                        else if (value == ",")
                        {
                            index++;
                            continue;
                        }
                    }
                    break;
                default:
                    while (true)
                    {
                        Parse(ref index, tokens, table);
                        var value = tokens.GetValue(index);
                        if (value == ")")
                        {
                            index++;
                            endBracketFound = true;
                            break;
                        }
                        else if (value == ",")
                        {
                            index++;
                            continue;
                        }
                    }
                    break;
            }
        }

        ParseFilterClause(ref index, tokens, table);

        return true;

    }

    bool ParseFilterClause(ref int index, Span<Token> tokens, Table table)
    {
        if (index >= tokens.Length)
        {
            return false;
        }
        if (tokens.GetValue(index) != "filter")
        {
            return false;
        }
        Increment(ref index, 1, tokens);
        if (tokens[index].Value != "(")
        {
            throw new InvalidSqlException("Expected '('", tokens[index]);
        }

        Increment(ref index, 1, tokens);
        if (tokens.GetValue(index) != "where")
        {
            throw new InvalidSqlException("Expected 'WHERE'", tokens[index]);
        }

        Increment(ref index, 1, tokens);
        if (!Parse(ref index, tokens, table))
        {
            throw new InvalidSqlException("Expected expr", tokens[index]);
        }

        if (tokens.GetValue(index) != ")")
        {
            throw new InvalidSqlException("Expected ')'", tokens[index]);
        }
        index++;
        return true;
    }

    bool ParseExpr(ref int index, Span<Token> tokens, Table table)
    {
        if (_literalValueParser.Parse(ref index, tokens))
        {
            return true;
        }

        if (ParseBindParam(ref index, tokens, table))
        {
            return true;
        }

        if (ParseFunction(ref index, tokens, table))
        {
            return true;
        }

        switch (tokens.GetValue(index))
        {
            case "cast":
                throw new NotImplementedException();
            case "exists":
                throw new NotImplementedException();
            case "(":
                // could be select statement or and expression list
                throw new NotImplementedException();
            case "case":
                throw new NotImplementedException();
            case "raise":
                throw new NotImplementedException();
            case "~":
            case "-":
            case "+":
            case "not":
                Increment(ref index, 1, tokens);
                return Parse(ref index, tokens, table);
            default:
                if (index >= tokens.Length && tokens.GetValue(index + 1) == "(")
                {
                    //function
                    throw new NotImplementedException();
                }
                //column identifier
                ParseColumnIdentifier(ref index, tokens, table);
                return true;
        }
    }

    void ParseColumnIdentifier(ref int index, Span<Token> tokens, Table table)
    {
        int start = index;
        var firstPart = tokens.GetValue(index);

        if (index == tokens.Length - 1 || tokens.GetValue(index + 1) != ".")
        {
            //column only
            var value = tokens.GetValue(index);
            if (!table.Columns.Any(column => column.SqlName.ToLowerInvariant() == firstPart))
            {
                throw new InvalidSqlException("Column doesn't exist in the current table", tokens[index]);
            }
            index++;
            return;
        }
        Increment(ref index, 1, tokens);
        if (tokens.GetValue(index) != ".")
        {
            throw new InvalidSqlException("Expected '.", tokens[index]);
        }
        Increment(ref index, 1, tokens);

        var secondPart = tokens.GetValue(index);

        if (index == tokens.Length - 1 || tokens.GetValue(index + 1) != ".")
        {
            // table.column
            var tableName = firstPart;
            var columnName = secondPart;

            var referencedTable = _databaseInfo.Tables.Where(table => table.SqlName.ToLowerInvariant() == tableName).FirstOrDefault();
            if (referencedTable == null)
            {
                throw new InvalidSqlException("Table doesn't exist", tokens[start]);
            }

            if (!referencedTable.Columns.Any(column => column.SqlName.ToLowerInvariant() == columnName))
            {
                throw new InvalidSqlException("Table doesn't contain referenced column", tokens[start + 2]);
            }
            index++;
            return;
        }

        // its a three parter which includes schema-name which we don't support
        throw new InvalidSqlException("Attached databases are not supported", tokens[start]);
    }
}