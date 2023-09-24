using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SqlSrcGen.Generator;

public class SelectParser : Parser
{
    readonly DatabaseInfo _databaseInfo;
    readonly ExpressionParser _expressionParser;

    public SelectParser(
        DatabaseInfo databaseInfo,
        ExpressionParser expressionParser)
    {
        _databaseInfo = databaseInfo;
        _expressionParser = expressionParser;
    }

    public bool Parse(ref int index, Span<Token> tokens, QueryInfo queryInfo)
    {
        if (IsEnd(index, tokens))
        {
            return false;
        }
        switch (tokens.GetValue(index))
        {
            case "with":
                throw new NotImplementedException();
            case "select":
                ParseSelectStatement(ref index, tokens, queryInfo);
                return true;
            case "values":
                throw new NotImplementedException();
        }
        return false;
    }

    void ParseSelectStatement(ref int index, Span<Token> tokens, QueryInfo queryInfo)
    {
        Expect(index, tokens, "select");
        queryInfo.QueryType = QueryType.Select;
        Increment(ref index, 1, tokens);
        switch (tokens.GetValue(index))
        {
            case "distinct":
            case "all":
                Increment(ref index, 1, tokens);
                break;
        }
        while (true)
        {
            ParseResultColumn(ref index, tokens, queryInfo);
            if (tokens.GetValue(index) == ",")
            {
                Increment(ref index, 1, tokens);
                continue;
            }
            break;
        }

        if (tokens.GetValue(index) == "from")
        {
            ParseFromStatement(ref index, tokens, queryInfo);
        }
    }

    void ParseFromStatement(ref int index, Span<Token> tokens, QueryInfo queryInfo)
    {
        Expect(index, tokens, "from");
        Increment(ref index, 1, tokens);
        while (true)
        {
            ParseTableOrSubQuery(ref index, tokens, queryInfo);
            if (IsEnd(index, tokens) || tokens.GetValue(index) != ",")
            {
                break;
            }
            Increment(ref index, 1, tokens);
        }
    }

    void ParseTableOrSubQuery(ref int index, Span<Token> tokens, QueryInfo queryInfo)
    {
        if (tokens.GetValue(index) == "(")
        {
            throw new NotImplementedException();
        }
        if (tokens[index].TokenType == TokenType.Other && tokens.NextIs(index, "."))
        {
            throw new InvalidSqlException("Attached databases are not supported", tokens[index]);
        }
        if (tokens[index].TokenType == TokenType.Other && tokens.NextIs(index, "("))
        {
            throw new NotImplementedException("functions in from statements not supported yet");
        }

        if (tokens[index].TokenType != TokenType.Other)
        {
            throw new InvalidSqlException("Expected table name", tokens[index]);
        }
        var tableName = tokens.GetValue(index);
        var table = _databaseInfo.Tables.Where(table => table.SqlName.ToLowerInvariant() == tableName.ToLowerInvariant()).FirstOrDefault();
        if (table == null)
        {
            throw new InvalidSqlException("Table doesn't exist", tokens[index]);
        }
        index++;
        if (!IsEnd(index, tokens))
        {
            if (tokens.GetValue(index) == "as")
            {
                Increment(ref index, 1, tokens);
                if (tokens[index].TokenType != TokenType.Other)
                {
                    throw new InvalidSqlException("Expected table name", tokens[index]);
                }
                tableName = tokens[index].Value;
                Increment(ref index, 1, tokens);
            }
            if (tokens[index].TokenType == TokenType.Other)
            {
                tableName = tokens[index].Value;
                Increment(ref index, 1, tokens);
            }
            switch (tokens.GetValue(index))
            {
                case "indexed":
                    Increment(ref index, 1, tokens);
                    Expect(index, tokens, "by");
                    Increment(ref index, 1, tokens);
                    if (tokens[index].TokenType != TokenType.Other)
                    {
                        throw new InvalidSqlException("Expected index name", tokens[index]);
                    }
                    index++;
                    break;
                case "not":
                    Increment(ref index, 1, tokens);
                    Expect(index, tokens, "indexed");
                    index++;
                    break;
            }
        }
        queryInfo.FromTables.Add((tableName, table));
    }

    void ParseResultColumn(ref int index, Span<Token> tokens, QueryInfo queryInfo)
    {
        var value = tokens.GetValue(index);
        if (value == "*")
        {
            queryInfo.AddColumnsGenerator(tables =>
            {
                List<Column> columns = new();
                foreach ((var name, var table) in tables)
                {
                    columns.AddRange(table.Columns);
                }
                return columns;
            });
            Increment(ref index, 1, tokens);
            return;
        }
        var token = tokens[index];
        var expression = _expressionParser.Parse(ref index, tokens, null);
        if (expression == null || expression.ExpressionType != ExpressionType.Column)
        {
            throw new InvalidSqlException("Expected column identifier", token);
        }
        queryInfo.AddColumnsGenerator(tables =>
        {
            var columns = new List<Column>();
            if (string.IsNullOrEmpty(expression.TableName))
            {
                foreach (var table in tables)
                {
                    foreach (var column in table.Item2.Columns)
                    {
                        if (column.SqlName.ToLowerInvariant() == expression.ColumnName?.ToLowerInvariant())
                        {
                            if (columns.Count() != 0)
                            {
                                throw new InvalidSqlException("Ambiguous column name", token);
                            }
                            columns.Add(column);
                        }
                    }
                }
            }
            else
            {
                foreach (var table in tables)
                {
                    if (table.Item2.SqlName.ToLowerInvariant() == expression.TableName?.ToLowerInvariant())
                    {
                        foreach (var column in table.Item2.Columns)
                        {
                            if (column.SqlName.ToLowerInvariant() == expression.ColumnName?.ToLowerInvariant())
                            {
                                columns.Add(column);
                            }
                        }
                    }
                }
            }
            return columns;
        });
    }
}