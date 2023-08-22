using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace SqlSrcGen.Generator;

public class ExpressionParser : Parser
{
    readonly LiteralValueParser _literalValueParser;
    readonly TypeNameParser _typeNameParser;
    readonly CollationParser _collationParser;
    readonly DatabaseInfo _databaseInfo;

    public ExpressionParser(
        DatabaseInfo databaseInfo,
        LiteralValueParser literalValueParser,
        TypeNameParser typeNameParser,
        CollationParser collationParser)
    {
        _databaseInfo = databaseInfo;
        _literalValueParser = literalValueParser;
        _typeNameParser = typeNameParser;
        _collationParser = collationParser;
    }

    public bool Parse(ref int index, Span<Token> tokens, Table table, bool includeBinaryOperators = true)
    {
        if (!ParseExpr(ref index, tokens, table))
        {
            return false;
        }
        if (includeBinaryOperators && ParseBooleanOperator(ref index, tokens))
        {
            return Parse(ref index, tokens, table);
        }
        if (index >= tokens.Length)
        {
            return true;
        }
        var tokenValue = tokens.GetValue(index);
        switch (tokenValue)
        {
            case "collate":
                _collationParser.ParseCollationStatement(ref index, tokens);
                return true;
            case "not":
                ParseNotStatement(ref index, tokens, table);
                return true;
            case "like":
                ParseLikeStatement(ref index, tokens, table);
                return true;
            case "glob":
            case "regexp":
            case "match":
                Increment(ref index, 1, tokens);
                Parse(ref index, tokens, table);
                return true;
            case "isnull":
            case "notnull":
                index++;
                return true;
            case "is":
                ParseIsStatement(ref index, tokens, table);
                return true;
            case "between":
                ParseBetweenStatement(ref index, tokens, table);
                return true;
            case "in":
                ParseInStatement(ref index, tokens, table);
                return true;
            default:
                return true;
        }
    }

    void ParseInStatement(ref int index, Span<Token> tokens, Table table)
    {
        Expect(index, tokens, "in");
        Increment(ref index, 1, tokens);
        if (tokens.GetValue(index) == "(")
        {
            Increment(ref index, 1, tokens);
            switch (tokens.GetValue(index))
            {
                case "with":
                case "select":
                    ParseSelectStatement(ref index, tokens, table);
                    Expect(index, tokens, ")");
                    index++;
                    return;
                case ")":
                    index++;
                    return;
                default:
                    index--;// expr list assumes sarting from the open brackets
                    ParseExprList(ref index, tokens, table);
                    return;
            }
        }
        if (tokens[index].TokenType == TokenType.Other && tokens.HasIndex(index + 1) && tokens.GetValue(index + 1) == ".")
        {
            //this is a schema
            new InvalidSqlException("Attached databases are not supported", tokens[index]);
        }
        if (tokens[index].TokenType == TokenType.Other && tokens.HasIndex(index + 1) && tokens.GetValue(index) != "(")
        {
            //table function
            Increment(ref index, 1, tokens);
            ParseExprList(ref index, tokens, table);
            return;
        }
        if (tokens[index].TokenType == TokenType.Other)
        {
            //table name
            var tableName = tokens.GetValue(index);
            if (!_databaseInfo.Tables.Any(table => table.SqlName.ToLower() == tableName))
            {
                throw new InvalidSqlException($"Table {tableName} doesn't exist", tokens[index]);
            }
            index++;
            return;
        }
    }

    void ParseBetweenStatement(ref int index, Span<Token> tokens, Table table)
    {
        Expect(index, tokens, "between");
        Increment(ref index, 1, tokens);
        if (!Parse(ref index, tokens, table, false))
        {
            throw new InvalidSqlException("Expected expression", tokens[index]);
        }
        Expect(index, tokens, "and");
        Increment(ref index, 1, tokens);
        if (!Parse(ref index, tokens, table))
        {
            throw new InvalidSqlException("Expected expression", tokens[index]);
        }
    }

    void ParseIsStatement(ref int index, Span<Token> tokens, Table table)
    {
        Expect(index, tokens, "is");
        Increment(ref index, 1, tokens);
        if (tokens.GetValue(index) == "not")
        {
            Increment(ref index, 1, tokens);
        }
        if (tokens.GetValue(index) == "distinct")
        {
            Increment(ref index, 1, tokens);
            Expect(index, tokens, "from");
            Increment(ref index, 1, tokens);
        }

        Parse(ref index, tokens, table);
    }

    void ParseNotStatement(ref int index, Span<Token> tokens, Table table)
    {
        Expect(index, tokens, "not");
        Increment(ref index, 1, tokens);
        switch (tokens.GetValue(index))
        {
            case "like":
                ParseLikeStatement(ref index, tokens, table);
                return;
            case "glob":
            case "regexp":
            case "match":
                Increment(ref index, 1, tokens);
                Parse(ref index, tokens, table);
                return;
            case "null":
                index++;
                return;
            case "between":
                ParseBetweenStatement(ref index, tokens, table);
                return;
            case "in":
                ParseInStatement(ref index, tokens, table);
                return;
        }
    }

    void ParseLikeStatement(ref int index, Span<Token> tokens, Table table)
    {
        Expect(index, tokens, "like");
        Increment(ref index, 1, tokens);
        if (!Parse(ref index, tokens, table))
        {
            throw new InvalidSqlException("Expected expression", tokens[index]);
        }
        if (IsEnd(index, tokens))
        {
            return;
        }
        if (tokens.GetValue(index) == "escape")
        {
            Increment(ref index, 1, tokens);
            if (!Parse(ref index, tokens, table))
            {
                throw new InvalidSqlException("Expected expression", tokens[index]);
            }
            return;
        }
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

        ParseOverClause(ref index, tokens, table);

        return true;

    }

    bool ParseOverClause(ref int index, Span<Token> tokens, Table table)
    {
        if (index >= tokens.Length)
        {
            return false;
        }
        if (tokens.GetValue(index) != "over")
        {
            return false;
        }
        Increment(ref index, 1, tokens);

        if (tokens.GetValue(index) != "(")
        {
            if (tokens[index].TokenType != TokenType.Other)
            {
                throw new InvalidSqlException("Expected window-name", tokens[index]);
            }
            return true;
        }

        Increment(ref index, 1, tokens);

        bool baseWindowNameDefined = false;
        bool partitionByUsed = false;
        bool orderByUsed = false;
        bool finished = false;
        while (!finished)
        {
            switch (tokens.GetValue(index))
            {
                case "partition":
                    if (partitionByUsed)
                    {
                        throw new InvalidSqlException("Partition by clause already defined", tokens[index]);
                    }
                    PrasePartitionBy(ref index, tokens, table);
                    partitionByUsed = true;
                    break;
                case "order":
                    if (orderByUsed)
                    {
                        throw new InvalidSqlException("Order by clause already defined", tokens[index]);
                    }
                    ParseOrderBy(ref index, tokens, table);
                    orderByUsed = true;
                    break;
                case "range":
                case "rows":
                case "groups":
                    ParseFrameSpec(ref index, tokens, table);
                    break;
                case ")":
                    index++;
                    return true;
                default:
                    if (baseWindowNameDefined)
                    {
                        //can't define one again
                        throw new InvalidSqlException("Unexpected token", tokens[index]);
                    }
                    if (tokens[index].TokenType != TokenType.Other)
                    {
                        throw new InvalidSqlException("Expected base-window-name", tokens[index]);
                    }
                    baseWindowNameDefined = true;
                    break;
            }
        }
        return true;
    }

    bool PrasePartitionBy(ref int index, Span<Token> tokens, Table table)
    {
        if (tokens.GetValue(index) != "partition")
        {
            throw new InvalidSqlException("Partition clause must start with partition", tokens[index]);
        }
        Increment(ref index, 1, tokens);
        if (tokens.GetValue(index) != "by")
        {
            throw new InvalidSqlException("expected 'by'", tokens[index]);
        }
        Increment(ref index, 1, tokens);
        while (true)
        {
            if (!Parse(ref index, tokens, table))
            {
                throw new InvalidSqlException("Expected expr", tokens[index]);
            }
            if (tokens.GetValue(index) != ",")
            {
                break;
            }
        }
        return true;
    }

    void ParseBetween(ref int index, Span<Token> tokens, Table table)
    {
        if (tokens.GetValue(index) != "between")
        {
            throw new InvalidSqlException("Expected BETWEEN", tokens[index]);
        }
        Increment(ref index, 1, tokens);
        switch (tokens.GetValue(index))
        {
            case "unbounded":
                Increment(ref index, 1, tokens);
                Expect(index, tokens, "preceding");
                Increment(ref index, 1, tokens);
                break;
            case "current":
                Increment(ref index, 1, tokens);
                Expect(index, tokens, "row");
                Increment(ref index, 1, tokens);
                break;
            default:
                if (!Parse(ref index, tokens, table))
                {
                    throw new InvalidSqlException("Expected expr", tokens[index]);
                }
                Increment(ref index, 1, tokens);
                Expect(index, tokens, "preceding", "following");
                Increment(ref index, 1, tokens);
                break;
        }
        Expect(index, tokens, "and");
        Increment(ref index, 1, tokens);
        switch (tokens.GetValue(index))
        {
            case "unbounded":
                Increment(ref index, 1, tokens);
                Expect(index, tokens, "following");
                Increment(ref index, 1, tokens);
                break;
            case "current":
                Increment(ref index, 1, tokens);
                Expect(index, tokens, "row");
                Increment(ref index, 1, tokens);
                break;
            default:
                if (!Parse(ref index, tokens, table))
                {
                    throw new InvalidSqlException("Expected expr", tokens[index]);
                }
                Increment(ref index, 1, tokens);
                Expect(index, tokens, "preceding", "following");
                Increment(ref index, 1, tokens);
                break;
        }
    }

    bool IsOneOf(int index, Span<Token> tokens, params string[] values)
    {
        var value = tokens.GetValue(index);
        return values.Contains(value);
    }

    bool ParseFrameSpec(ref int index, Span<Token> tokens, Table table)
    {
        if (index >= tokens.Length)
        {
            return false;
        }
        var value = tokens.GetValue(index);
        if (value != "range" && value != "rows" && value != "groups")
        {
            return false;
        }

        Increment(ref index, 1, tokens);

        switch (tokens.GetValue(index))
        {
            case "between":
                ParseBetween(ref index, tokens, table);
                break;
            case "unbounded":
                Increment(ref index, 1, tokens);
                if (tokens.GetValue(index) != "preceding")
                {
                    throw new InvalidSqlException("Expected PRECEDING", tokens[index]);
                }
                Increment(ref index, 1, tokens);
                break;
            case "current":
                Increment(ref index, 1, tokens);
                if (tokens.GetValue(index) != "row")
                {
                    throw new InvalidSqlException("Expected ROW", tokens[index]);
                }
                Increment(ref index, 1, tokens);
                break;
            default:
                if (!Parse(ref index, tokens, table))
                {
                    throw new InvalidSqlException("Expected expr", tokens[index]);
                }
                Increment(ref index, 1, tokens);
                if (tokens.GetValue(index) != "preceding")
                {
                    throw new InvalidSqlException("Expected PRECEDING", tokens[index]);
                }
                Increment(ref index, 1, tokens);
                break;
        }

        if (tokens.GetValue(index) == "exclude")
        {
            Increment(ref index, 1, tokens);
            switch (tokens.GetValue(index))
            {
                case "no":
                    Increment(ref index, 1, tokens);
                    if (tokens.GetValue(index) != "others")
                    {
                        throw new InvalidSqlException("Expected OTHERS", tokens[index]);
                    }
                    Increment(ref index, 1, tokens);
                    break;
                case "current":
                    Increment(ref index, 1, tokens);
                    if (tokens.GetValue(index) != "row")
                    {
                        throw new InvalidSqlException("Expected ROW", tokens[index]);
                    }
                    Increment(ref index, 1, tokens);
                    break;
                case "group":
                    Increment(ref index, 1, tokens);
                    break;
                case "ties":
                    Increment(ref index, 1, tokens);
                    break;
                default:
                    throw new InvalidSqlException($"Unexpected token", tokens[index]);
            }
        }
        return true;
    }

    bool ParseOrderBy(ref int index, Span<Token> tokens, Table table)
    {
        if (tokens.GetValue(index) != "order")
        {
            throw new InvalidSqlException("Order by clause must start with 'order'", tokens[index]);
        }
        Increment(ref index, 1, tokens);
        if (tokens.GetValue(index) != "by")
        {
            throw new InvalidSqlException("expected 'by'", tokens[index]);
        }
        Increment(ref index, 1, tokens);
        while (true)
        {
            if (!ParseOrderingTerm(ref index, tokens, table))
            {
                throw new InvalidSqlException("Expected ordering term", tokens[index]);
            }
            if (tokens.GetValue(index) != ",")
            {
                break;
            }
        }
        return true;
    }

    bool ParseOrderingTerm(ref int index, Span<Token> tokens, Table table)
    {
        if (!Parse(ref index, tokens, table))
        {
            return false;
        }
        if (tokens.GetValue(index) == "colate")
        {
            Increment(ref index, 2, tokens);
        }
        var value = tokens.GetValue(index);
        if (value == "asc" || value == "desc")
        {
            Increment(ref index, 1, tokens);
        }
        value = tokens.GetValue(index);
        if (value == "nulls")
        {
            Increment(ref index, 1, tokens);
            value = tokens.GetValue(index);
            if (value != "first" && value != "last")
            {
                throw new InvalidSqlException("expected FIRST or LAST", tokens[index]);
            }
            Increment(ref index, 1, tokens);
        }
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
                ParseCastStatement(ref index, tokens, table);
                return true;
            case "exists":
                PraseExistsStatement(ref index, tokens, table);
                return true;
            case "(":
                // could be select statement or and expression list
                Increment(ref index, 1, tokens);
                if (IsOneOf(index, tokens, "with", "select", "values"))
                {
                    ParseSelectStatement(ref index, tokens, table);
                    Expect(index, tokens, ")");
                    index++;
                    return true;
                }
                index--;
                ParseExprList(ref index, tokens, table);
                return true;
            case "case":
                ParseCaseStatement(ref index, tokens, table);
                return true;
            case "raise":
                ParseRaiseStatement(ref index, tokens, table);
                return true;
            case "~":
            case "-":
            case "+":
            case "not":
                Increment(ref index, 1, tokens);
                return Parse(ref index, tokens, table);
            default:
                //column identifier
                ParseColumnIdentifier(ref index, tokens, table);
                return true;
        }
    }

    void ParseRaiseStatement(ref int index, Span<Token> tokens, Table table)
    {
        Expect(index, tokens, "raise");
        Increment(ref index, 1, tokens);
        Expect(index, tokens, "(");
        Increment(ref index, 1, tokens);
        switch (tokens.GetValue(index))
        {
            case "ignore":
                Increment(ref index, 1, tokens);
                Expect(index, tokens, ")");
                index++;
                return;
            case "rollback":
            case "abort":
            case "fail":
                Increment(ref index, 1, tokens);
                Expect(index, tokens, ",");
                Increment(ref index, 1, tokens);
                if (tokens[index].TokenType != TokenType.StringLiteral)
                {
                    throw new InvalidSqlException("Expected error message", tokens[index]);
                }
                Increment(ref index, 1, tokens);
                Expect(index, tokens, ")");
                index++;
                return;
            default:
                throw new InvalidSqlException("Unexpected token", tokens[index]);
        }
    }

    void ParseCaseStatement(ref int index, Span<Token> tokens, Table table)
    {
        Expect(index, tokens, "case");
        Increment(ref index, 1, tokens);
        if (tokens.GetValue(index) != "when")
        {
            Parse(ref index, tokens, table);
        }
        while (true)
        {
            Expect(index, tokens, "when");
            Increment(ref index, 1, tokens);
            if (!Parse(ref index, tokens, table))
            {
                throw new InvalidSqlException("Expected expression", tokens[index]);
            }
            Expect(index, tokens, "then");
            Increment(ref index, 1, tokens);
            if (!Parse(ref index, tokens, table))
            {
                throw new InvalidSqlException("Expected expression", tokens[index]);
            }
            if (IsOneOf(index, tokens, "else", "end"))
            {
                break;
            }
        }

        if (tokens.GetValue(index) == "else")
        {
            Increment(ref index, 1, tokens);
            if (!Parse(ref index, tokens, table))
            {
                throw new InvalidSqlException("Expected expression", tokens[index]);
            }
        }

        Expect(index, tokens, "end");
        index++;
    }

    void PraseExistsStatement(ref int index, Span<Token> tokens, Table table)
    {
        Expect(index, tokens, "exists");
        Increment(ref index, 1, tokens);
        ParseBracketsSelectStatement(ref index, tokens, table);
    }

    void ParseBracketsSelectStatement(ref int index, Span<Token> tokens, Table table)
    {
        Expect(index, tokens, "(");
        Increment(ref index, 1, tokens);
        ParseSelectStatement(ref index, tokens, table);
        Expect(index, tokens, ")");
        index++;
    }

    void ParseCastStatement(ref int index, Span<Token> tokens, Table table)
    {
        Expect(index, tokens, "cast");
        Increment(ref index, 1, tokens);
        Expect(index, tokens, "(");
        Increment(ref index, 1, tokens);
        Parse(ref index, tokens, table);
        Expect(index, tokens, "as");
        Increment(ref index, 1, tokens);
        _typeNameParser.ParseTypeName(ref index, tokens, out string _);
        Expect(index, tokens, ")");
        index++;
        return;
    }

    void ParseExprList(ref int index, Span<Token> tokens, Table table)
    {
        Expect(index, tokens, "(");
        Increment(ref index, 1, tokens);
        while (true)
        {
            Parse(ref index, tokens, table);
            if (tokens.GetValue(index) == ",")
            {
                Increment(ref index, 1, tokens);
                continue;
            }
            Expect(index, tokens, ")");
            index++;
            return;
        }
    }

    void ParseSelectStatement(ref int index, Span<Token> tokens, Table table)
    {
        throw new NotImplementedException("Select will be implemented after create table");
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
                throw new InvalidSqlException($"Column '{firstPart}' doesn't exist in the current table", tokens[index]);
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