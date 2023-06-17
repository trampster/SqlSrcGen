using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SqlSrcGen;

[Generator]
public class SqlGenerator : ISourceGenerator
{
    public SqlGenerator()
    {
        // while (!System.Diagnostics.Debugger.IsAttached)
        //     System.Threading.Thread.Sleep(500);
    }
    public void Execute(GeneratorExecutionContext context)
    {

        var databaseAccessGenerator = new DatabaseAccessGenerator();
        var additionalFiles = context.AdditionalFiles.Where(at => at.Path.EndsWith(".sql"));
        if (additionalFiles.Any())
        {
            var builder = new SourceBuilder();
            builder.AppendLine("#nullable enable");
            builder.AppendLine("using System;");
            databaseAccessGenerator.GenerateUsings(builder);
            builder.AppendLine("namespace SqlSrcGen");
            builder.AppendLine("{");

            builder.AppendLine();

            var databaseInfo = new DatabaseInfo();

            builder.IncreaseIndent();

            var reporter = new DiagnosticsReporter(context);
            reporter.Path = additionalFiles.First().Path;
            try
            {
                ProcessSqlSchema(additionalFiles.First().GetText().ToString(), databaseInfo, reporter);
                GenerateDatabaseObjects(databaseInfo, builder);
            }
            catch (InvalidSqlException exception)
            {
                var sqlFile = additionalFiles.First();

                var token = exception.Token;

                context.ReportDiagnostic(
                    Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "SSG0001",
                            "Invalid SQL",
                            exception.Message,
                            "SQL",
                            DiagnosticSeverity.Error,
                            true),
                        Location.Create(sqlFile.Path,
                        TextSpan.FromBounds(token.Position, token.Value.Length + token.Position),
                        new LinePositionSpan(
                            new LinePosition(token.Line, token.CharacterInLine), new LinePosition(token.Line, token.CharacterInLine + token.Value.Length)))));
                return;
            }


            builder.AppendLine();
            databaseAccessGenerator.Generate(builder, databaseInfo);


            builder.DecreaseIndent();
            //end of namespace
            builder.AppendLine("}");

            var code = builder.ToString();
            context.AddSource($"SqlSchema.g.cs", code);
        }
    }

    public void ProcessSqlSchema(string schemaText, DatabaseInfo databaseInfo, IDiagnosticsReporter reporter)
    {
        var tokensList = Tokenize(schemaText);
        var tokens = tokensList.ToArray().AsSpan();
        while (tokens.Length > 0)
        {
            switch (tokens[0].Value.ToLower())
            {
                case "create":
                    tokens = ProcessCreateCommand(tokens, databaseInfo, reporter);
                    break;
                default:
                    throw new InvalidSqlException("Unsupported sql command", tokens[0]);
            }
        }
    }

    public void GenerateDatabaseObjects(DatabaseInfo databaseInfo, SourceBuilder builder)
    {
        foreach (var table in databaseInfo.Tables)
        {
            if (table.Tempory)
            {
                continue;
            }
            builder.AppendLine($"public record {table.CSharpName}");
            builder.AppendLine("{");
            builder.IncreaseIndent();

            foreach (var column in table.Columns)
            {
                builder.AppendStart($"public {column.CSharpType} {column.CSharpName} {{ get; set; }}");
                if (column.CSharpType == "string")
                {
                    builder.Append(" = \"\";");
                }
                if (column.CSharpType == "byte[]")
                {
                    builder.Append(" = new byte[0];");
                }
                builder.AppendLine();
            }
            builder.DecreaseIndent();
            builder.AppendLine("}");
        }
    }

    int IndexOf(Span<Token> tokens, string value)
    {
        for (int index = 0; index < tokens.Length; index++)
        {
            if (tokens[index].Value == value)
            {
                return index;
            }
        }
        return -1;
    }

    void AssertEnoughTokens(Span<Token> tokens, int index)
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

    void Increment(ref int index, int amount, Span<Token> tokens)
    {
        AssertEnoughTokens(tokens, index + amount);
        index += amount;
    }

    void ParseTableName(Span<Token> tokens, ref int index, Table table, List<Table> existingTables)
    {
        string tableName = tokens[index].Value;
        if (tokens.GetValue(index + 1) == ".")
        {
            throw new InvalidSqlException("Attached databases are not supported", tokens[index]);
        }

        table.SqlName = tableName;
        table.CSharpName = ToDotnetName(tableName);

        var tableMatchingCSharpName = existingTables.Where(existing => existing.CSharpName == table.CSharpName).FirstOrDefault();
        if (tableMatchingCSharpName != null)
        {
            throw new InvalidSqlException(
                "Table maps to same csharp class name as an existing table",
                tokens[index]);
        }

        var tableMatchingSqlName = existingTables.Where(existing => existing.SqlName == table.SqlName).FirstOrDefault();
        if (tableMatchingSqlName != null)
        {
            throw new InvalidSqlException(
                "Table already exists",
                tokens[index]);
        }

        Increment(ref index, 1, tokens);
    }

    Span<Token> ProcessCreateCommand(Span<Token> tokensToProcess, DatabaseInfo databaseInfo, IDiagnosticsReporter diagnosticsReporter)
    {
        var tokens = tokensToProcess;
        bool isTemp = false;

        AssertEnoughTokens(tokensToProcess, 1);
        int index = 1;
        switch (tokens[1].Value.ToLower())
        {
            case "table":
                index += 1;
                break;
            case "temp":
            case "tempory":
                AssertEnoughTokens(tokensToProcess, 2);
                if (tokens[2].Value.ToLower() != "table")
                {
                    throw new InvalidSqlException("Expected 'table'", tokens[2]);
                }
                Increment(ref index, 2, tokensToProcess);
                isTemp = true;
                break;
            default:
                throw new InvalidSqlException("Invalid token expected table, temp or tempory", tokens[1]);
        }

        var table = new Table();

        switch (tokens.GetValue(index))
        {
            case "if":
                AssertEnoughTokens(tokens, index + 2);
                if (tokens[index + 1].Value.ToLower() == "not" && tokens[index + 2].Value.ToLower() == "exists")
                {
                    Increment(ref index, 3, tokensToProcess);
                }
                else
                {
                    throw new InvalidSqlException($"Did you mean 'if not exists'?", tokens[index]);
                }
                break;
            default:
                break;
        }

        ParseTableName(tokensToProcess, ref index, table, databaseInfo.Tables);

        table.CreateTable = string.Join(" ", tokens.Slice(0, IndexOf(tokens, ";") + 1).ToArray().Select(u => u.Value).ToArray());
        table.Tempory = isTemp;
        databaseInfo.Tables.Add(table);

        switch (tokens[index].Value.ToLowerInvariant())
        {
            case "(":
                break;
            case "as":
                // as isn't supported as it allows you to create tables from select queries
                // which would mean having to implement select.
                // This can be revisted once select has been implemented.
                throw new InvalidSqlException($"as is not currently supported", tokens[index]);
            default:
                throw new InvalidSqlException($"Missing ( in CREATE TABLE command", tokens[index]);
        }

        if (tokens[index].Value != "(")
        {
            throw new InvalidSqlException($"Missing ( in CREATE TABLE command at position {tokens[3].Position}", tokens[3]);
        }
        Increment(ref index, 1, tokensToProcess);

        bool finishedColumns = false;
        while (true)
        {
            bool readTableConstraint = ParseTableConstraint(ref index, tokens, table.Columns, diagnosticsReporter, databaseInfo.Tables, table);
            if (!finishedColumns && readTableConstraint)
            {
                finishedColumns = true;
            }
            if (!finishedColumns)
            {
                ParseColumnDefinition(ref index, tokens, table.Columns, diagnosticsReporter, databaseInfo.Tables);
            }
            if (tokens[index].Value == ")")
            {
                Increment(ref index, 1, tokens);
                break;
            }
            if (tokens[index].Value == ",")
            {
                Increment(ref index, 1, tokens);
                continue;
            }

            throw new InvalidSqlException("Ran out of tokens while looking for ')' or ',' in CREATE TABLE command", tokensToProcess[tokensToProcess.Length - 1]);
        }

        AssertEnoughTokens(tokens, index);
        if (tokens[index].Value != ";")
        {
            throw new InvalidSqlException($"missing ';' at end of CREATE TABLE command at position {tokens[0].Position}", tokens[0]);
        }
        if (tokens.Length == 0)
        {
            return Span<Token>.Empty;
        }
        return tokens.Slice(index + 1);
    }

    TypeAffinity ToTypeAffinity(string sqlType)
    {
        sqlType = sqlType.ToUpperInvariant();
        if (sqlType.Contains("INT"))
        {
            return TypeAffinity.INTEGER;
        }

        if (sqlType.Contains("CHAR") || sqlType.Contains("CLOB") || sqlType.Contains("TEXT"))
        {
            return TypeAffinity.TEXT;
        }

        if (sqlType.Contains("BLOB"))
        {
            return TypeAffinity.BLOB;
        }

        if (sqlType.Contains("REAL") || sqlType.Contains("FLOA") || sqlType.Contains("DOUB"))
        {
            return TypeAffinity.REAL;
        }

        return TypeAffinity.NUMERIC;
    }

    string ToDotnetType(TypeAffinity typeAffinity, bool notNull)
    {
        var type = typeAffinity switch
        {
            TypeAffinity.INTEGER => "long",
            TypeAffinity.TEXT => "string",
            TypeAffinity.BLOB => "byte[]",
            TypeAffinity.REAL => "double",
            TypeAffinity.NUMERIC => "Numeric",
            _ => "Numeric"
        };
        if (!notNull)
        {
            type += "?";
        }
        return type;
    }

    string ToDotnetName(string name)
    {
        var builder = new StringBuilder();
        if (name.StartsWith("[") && name.EndsWith("]"))
        {
            name = name.Substring(1, name.Length - 2);
        }
        bool startsLower = false;
        if (name.Length > 0 && char.IsLower(name[0]))
        {
            startsLower = true;
        }
        bool isFirst = true;
        for (int index = 0; index < name.Length; index++)
        {
            var charactor = name[index];
            if (charactor == '_')
            {
                isFirst = true;
                continue;
            }
            if (charactor == ' ')
            {
                isFirst = true;
                continue;
            }
            if (charactor == '\r')
            {
                isFirst = true;
                continue;
            }
            if (charactor == '\n')
            {
                isFirst = true;
                continue;
            }
            if (isFirst)
            {
                builder.Append(charactor.ToString().ToUpperInvariant()[0]);
                isFirst = false;
                continue;
            }

            builder.Append(startsLower ? charactor.ToString() : charactor.ToString().ToLowerInvariant());
        }
        return builder.ToString();
    }

    int ParseType(Span<Token> typeDefinition, out string type)
    {
        AssertEnoughTokens(typeDefinition, 1);
        StringBuilder typeBuilder = new StringBuilder();
        typeBuilder.Append(typeDefinition[0].Value);
        int numericLiteralCount = 0;
        bool lastTokenNumericLiteral = false;
        if (typeDefinition[1].Value == "(")
        {
            typeBuilder.Append("(");
            AssertEnoughTokens(typeDefinition, 2);
            for (int index = 2; index < typeDefinition.Length; index++)
            {
                var token = typeDefinition[index];
                if (token.Value == ")")
                {
                    typeBuilder.Append(token.Value);

                    type = typeBuilder.ToString();
                    return index + 1;
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
            throw new InvalidSqlException("Ran out of tokens trying to parse type", typeDefinition[0]);
        }
        type = typeBuilder.ToString();
        return 1;
    }

    void ParseConflictClause(Span<Token> columnDefinition, ref int index)
    {
        AssertEnoughTokens(columnDefinition, index);
        if (columnDefinition[index].Value.ToLowerInvariant() != "on")
        {
            return;
        }
        Increment(ref index, 1, columnDefinition);
        if (columnDefinition[index].Value.ToLowerInvariant() != "conflict")
        {
            throw new InvalidSqlException("Unexpected token while parsing column definition, did you mean 'on conflict'?", columnDefinition[index]);
        }
        Increment(ref index, 1, columnDefinition);
        switch (columnDefinition[index].Value.ToLowerInvariant())
        {
            case "rollback":
            case "abort":
            case "fail":
            case "ignore":
            case "replace":
                Increment(ref index, 1, columnDefinition);
                return;
            default:
                throw new InvalidSqlException("Invalid conflict action", columnDefinition[index]);
        }
    }

    void ParsePrimaryKeyConstraint(Span<Token> columnDefinition, ref int index, List<Column> existingColumns, Column column)
    {
        // already know it starts with primary
        var token = columnDefinition[index];

        index++;

        if (index > columnDefinition.Length - 1)
        {
            throw new InvalidSqlException($"Invalid column constraint, did you mean 'primary key'?", token);
        }
        var next1 = columnDefinition[index];
        if (next1.Value.ToLowerInvariant() != "key")
        {
            throw new InvalidSqlException($"Invalid column constraint, did you mean 'primary key'?", token);
        }

        if (existingColumns.Any(column => column.PrimaryKey))
        {
            throw new InvalidSqlException($"Table already has a primary key", token);
        }
        index++;

        // parse asc or desc
        switch (columnDefinition.GetValue(index))
        {
            case "asc":
            case "desc":
                Increment(ref index, 1, columnDefinition);
                break;
            default:
                break;
        }

        ParseConflictClause(columnDefinition, ref index);

        //parse autoincrement
        if (columnDefinition.GetValue(index) == "autoincrement")
        {
            if (column.SqlType.ToLowerInvariant() != "integer")
            {
                throw new InvalidSqlException("AUTOINCREMENT is only allowed on an INTEGER PRIMARY KEY", columnDefinition[index]);
            }
            column.AutoIncrement = true;
            Increment(ref index, 1, columnDefinition);
        }
    }

    void SkipBrackets(Span<Token> columnDefinition, ref int index)
    {
        int unclosedBrackets = 1;
        bool insideQuote = false;
        bool escaped = false;

        if (columnDefinition.GetValue(index) != "(")
        {
            throw new InvalidSqlException($"Missing opening bracket", columnDefinition[index]);
        }
        Increment(ref index, 1, columnDefinition);
        for (; index < columnDefinition.Length; index++)
        {
            var value = columnDefinition.GetValue(index);
            switch (value)
            {
                case ")":
                    if (insideQuote)
                    {
                        break;
                    }
                    unclosedBrackets--;
                    if (unclosedBrackets == 0)
                    {
                        index++;
                        return;
                    }
                    break;
                case "(":
                    if (insideQuote)
                    {
                        break;
                    }
                    unclosedBrackets++;
                    if (unclosedBrackets == 0)
                    {
                        index++;
                        return;
                    }
                    break;
                case "'":
                    if (!escaped)
                    {
                        insideQuote = !insideQuote;
                    }
                    break;

            }
            if (value == "\\" && !escaped)
            {
                escaped = true;
            }
        }
    }

    void ParseLiteralValue(Span<Token> columnDefinition, ref int index)
    {
        switch (columnDefinition.GetValue(index))
        {
            case "null":
            case "true":
            case "false":
            case "current_time":
            case "current_date":
            case "current_timestamp":
                index++;
                return;
            default:
                var token = columnDefinition[index];
                if (token.TokenType == TokenType.StringLiteral)
                {
                    index++;
                    return;
                }
                if (token.TokenType == TokenType.NumericLiteral)
                {
                    index++;
                    return;
                }
                if (token.TokenType == TokenType.BlobLiteral)
                {
                    index++;
                    return;
                }
                throw new InvalidSqlException($"Unrecognised default constraint", columnDefinition[index]);
        }
    }

    void PraseCollateConstraint(Span<Token> columnDefinition, ref int index, IDiagnosticsReporter reporter, Column column)
    {
        if (columnDefinition.GetValue(index) != "collate")
        {
            throw new InvalidSqlException($"expected collate constraint to begin with 'collate'", columnDefinition[index]);
        }
        Increment(ref index, 1, columnDefinition);

        var collation = columnDefinition.GetValue(index);
        switch (collation)
        {
            case "nocase":
            case "binary":
            case "rtrim":
                break;
            default:
                reporter.Warning(ErrorCode.SSG0002, "Collation types other than nocase, binary and rtrim require custom collation creation", columnDefinition[index]);
                break;
        }
        if (column.TypeAffinity != TypeAffinity.TEXT)
        {
            reporter.Warning(ErrorCode.SSG0003, "Collation only affects Text columns", columnDefinition[index]);
        }
        Increment(ref index, 1, columnDefinition);
    }

    void PraseDefaultConstraint(Span<Token> columnDefinition, ref int index)
    {
        if (columnDefinition.GetValue(index) != "default")
        {
            throw new InvalidSqlException($"expected default constraint to begin with 'default'", columnDefinition[index]);
        }
        Increment(ref index, 1, columnDefinition);

        var token = columnDefinition[index];
        // signed number
        if (token.Value == "+" || token.Value == "-")
        {
            Increment(ref index, 1, columnDefinition);
            if (columnDefinition[index].TokenType != TokenType.NumericLiteral)
            {
                throw new InvalidSqlException($"missing numeric literal in signed number", columnDefinition[index]);
            }
            index++;
            return;
        }

        // expression
        if (token.Value == "(")
        {
            SkipBrackets(columnDefinition, ref index);
            return;
        }

        //literal
        ParseLiteralValue(columnDefinition, ref index);
    }

    void PraseReferencesDeferrableClause(Span<Token> tokens, ref int index)
    {
        if (tokens.GetValue(index) != "deferrable")
        {
            throw new InvalidSqlException($"expected references deferrable clause to begin with 'deferrable'", tokens[index]);
        }
        Increment(ref index, 1, tokens);
        if (tokens.GetValue(index) != "initially")
        {
            return;
        }
        Increment(ref index, 1, tokens);
        switch (tokens.GetValue(index))
        {
            case "deferred":
            case "immediate":
                break;
            default:
                throw new InvalidSqlException($"expected 'deferred' or 'immediate", tokens[index]);
        }
        Increment(ref index, 1, tokens);
    }

    void ParseReferencesOnClause(Span<Token> tokens, ref int index)
    {
        if (tokens.GetValue(index) != "on")
        {
            throw new InvalidSqlException($"expected references on clause to begin with 'on'", tokens[index]);
        }
        Increment(ref index, 1, tokens);
        switch (tokens.GetValue(index))
        {
            case "delete":
                break;
            case "update":
                break;
            default:
                throw new InvalidSqlException($"Expected 'delete' or 'update'", tokens[index]);
        }

        Increment(ref index, 1, tokens);

        switch (tokens.GetValue(index))
        {
            case "set":
                Increment(ref index, 1, tokens);
                switch (tokens.GetValue(index))
                {
                    case "null":
                    case "default":
                        break;
                    default:
                        throw new InvalidSqlException($"Expected 'null' or 'default'", tokens[index]);
                }
                break;
            case "cascade":
            case "restrict":
                break;
            case "no":
                Increment(ref index, 1, tokens);
                if (tokens.GetValue(index) != "action")
                {
                    throw new InvalidSqlException($"Expected 'action'", tokens[index]);
                }
                break;
            default:
                throw new InvalidSqlException($"Expected 'delete' or 'update'", tokens[index]);
        }
        Increment(ref index, 1, tokens);
    }

    void ParseReferencesConstraint(Span<Token> tokens, ref int index, List<Table> existingTables, bool isColumnConstraint, IDiagnosticsReporter diagnoticsReporter)
    {
        if (tokens.GetValue(index) != "references")
        {
            throw new InvalidSqlException($"expected default constraint to begin with 'references'", tokens[index]);
        }
        Increment(ref index, 1, tokens);

        var foreignTableName = tokens.GetValue(index);
        var foreignTable = existingTables.Where(table => table.SqlName.ToLowerInvariant() == foreignTableName).FirstOrDefault();
        if (foreignTable == null)
        {
            throw new InvalidSqlException($"referenced table {tokens[index].Value} does not exist", tokens[index]);
        }
        Increment(ref index, 1, tokens);
        var columnList = ReadList(tokens, ref index);
        if (isColumnConstraint)
        {
            if (columnList.Count > 1)
            {
                throw new InvalidSqlException($"columns constraints cannot reference multiple columns", tokens[index]);
            }
            if (columnList.Count == 1)
            {
                var columnName = columnList[0].Value.ToLowerInvariant();
                var column = foreignTable.Columns.Where(column => column.SqlName == columnName).FirstOrDefault();
                if (column == null)
                {
                    throw new InvalidSqlException($"referenced column {columnName} does not exist", tokens[index]);
                }

                if (!column.PrimaryKey && !column.Unique)
                {
                    throw new InvalidSqlException($"referenced column {columnName} must be primary key or unique", tokens[index]);
                }
            }
        }

        while (true)
        {
            var tokenValue = tokens.GetValue(index);
            if (tokenValue == "on")
            {
                ParseReferencesOnClause(tokens, ref index);
                continue;
            }
            if (tokenValue == "match")
            {
                diagnoticsReporter.Warning(
                    ErrorCode.SSG0004,
                    "SQLite parses MATCH clauses, but does not enforce them. All foreign key constraints in SQLite are handled as if MATCH SIMPLE were specified.",
                    tokens[index]);
                // read match clause
                Increment(ref index, 1, tokens);
                Increment(ref index, 1, tokens);
                continue;
            }
            if (tokenValue == "not")
            {
                // read not deferable clause
                Increment(ref index, 1, tokens);
                PraseReferencesDeferrableClause(tokens, ref index);
                break;
            }
            if (tokenValue == "deferrable")
            {
                PraseReferencesDeferrableClause(tokens, ref index);
                break;
            }
            break;
        }
    }

    void ParseColumnAsConstraint(Span<Token> tokens, ref int index)
    {
        if (tokens.GetValue(index) != "as")
        {
            throw new InvalidSqlException($"expected as constraint to begin with 'as'", tokens[index]);
        }
        Increment(ref index, 1, tokens);
        if (tokens.GetValue(index) != "(")
        {
            throw new InvalidSqlException($"expected '('", tokens[index]);
        }
        SkipBrackets(tokens, ref index);

        switch (tokens.GetValue(index))
        {
            case "stored":
            case "virtual":
                Increment(ref index, 1, tokens);
                return;
            default:
                return;
        }
    }

    void ParseGeneratedConstraint(Span<Token> tokens, ref int index)
    {
        if (tokens.GetValue(index) != "generated")
        {
            throw new InvalidSqlException($"expected generated constraint to begin with 'generated'", tokens[index]);
        }

        Increment(ref index, 1, tokens);
        if (tokens.GetValue(index) != "always")
        {
            throw new InvalidSqlException($"expected 'always'", tokens[index]);
        }

        Increment(ref index, 1, tokens);
        ParseColumnAsConstraint(tokens, ref index);
    }

    List<Token> ReadList(Span<Token> tokens, ref int index)
    {
        List<Token> list = new();

        if (tokens.GetValue(index) != "(")
        {
            return list;
        }
        Increment(ref index, 1, tokens);
        while (true)
        {
            list.Add(tokens[index]);

            Increment(ref index, 1, tokens);
            if (tokens[index].Value == ",")
            {
                Increment(ref index, 1, tokens);
                continue;
            }

            if (tokens[index].Value == ")")
            {
                Increment(ref index, 1, tokens);
                return list;
            }
            throw new InvalidSqlException($"Unexpected token in list", tokens[index]);
        }
    }

    bool ParseColumnConstraint(Span<Token> columnDefinition, ref int index, Column column, List<Column> existingColumns, IDiagnosticsReporter diagnoticsReporter, List<Table> existingTables)
    {
        if (columnDefinition.GetValue(index) == "constraint")
        {
            Increment(ref index, 1, columnDefinition);
            if (columnDefinition[index].TokenType != TokenType.Other)
            {
                throw new InvalidSqlException($"Expected column name", columnDefinition[index]);
            }
            Increment(ref index, 1, columnDefinition);
            return ParseColumnConstraintDefinition(columnDefinition, ref index, column, existingColumns, diagnoticsReporter, existingTables);
        }
        return ParseColumnConstraintDefinition(columnDefinition, ref index, column, existingColumns, diagnoticsReporter, existingTables);
    }

    bool ParseColumnConstraintDefinition(Span<Token> tokens, ref int index, Column column, List<Column> existingColumns, IDiagnosticsReporter diagnoticsReporter, List<Table> existingTables)
    {
        var token = tokens[index];
        switch (token.Value.ToLowerInvariant())
        {
            case "not":
                if (index + 1 > tokens.Length - 1)
                {
                    throw new InvalidSqlException($"Invalid column constraint, did you mean 'not null'?", token);
                }
                Increment(ref index, 1, tokens);
                if (tokens.GetValue(index) != "null")
                {
                    throw new InvalidSqlException($"Invalid column constraint, did you mean 'not null'?", token);
                }
                //we have effectively consumed the next one
                Increment(ref index, 1, tokens);
                ParseConflictClause(tokens, ref index);
                column.NotNull = true;
                return true;
            case "primary":
                ParsePrimaryKeyConstraint(tokens, ref index, existingColumns, column);
                column.PrimaryKey = true;
                return true;
            case "unique":
                Increment(ref index, 1, tokens);
                column.Unique = true;
                ParseConflictClause(tokens, ref index);
                return true;
            case "check":
                ParseCheckConstraint(ref index, tokens);
                return true;
            case "default":
                PraseDefaultConstraint(tokens, ref index);
                return true;
            case "collate":
                PraseCollateConstraint(tokens, ref index, diagnoticsReporter, column);
                return true;
            case "references":
                ParseReferencesConstraint(tokens, ref index, existingTables, true, diagnoticsReporter);
                return true;
            case "generated":
                ParseGeneratedConstraint(tokens, ref index);
                return true;
            case "as":
                ParseColumnAsConstraint(tokens, ref index);
                return true;
            default:
                return false;
        }
    }

    void ParseTableUniqueConstraint(
        ref int index,
        Span<Token> tokens,
        List<Column> existingColumns,
        Table table)
    {
        int startIndex = index;
        Increment(ref index, 1, tokens);

        var columns = ParseIndexColumns(ref index, tokens, existingColumns);

        if (columns.Count == 1)
        {
            if (columns[0].column.Unique)
            {
                throw new InvalidSqlException("Column is already unique", columns[0].token);
            }
            if (columns[0].column.PrimaryKey)
            {
                throw new InvalidSqlException("Column is already unqiue because it's a primary key", columns[0].token);
            }
            columns[0].column.Unique = true;
        }
        else
        {
            var uniqueColumns = columns.Select(pair => pair.column).ToList();

            if (table.PrimaryKey.SequenceEqual(uniqueColumns))
            {
                throw new InvalidSqlException("Columns are already unqiue because they are a primary key", tokens[startIndex]);
            }
            foreach (var existingUniqueColumns in table.Unique)
            {
                if (existingUniqueColumns.SequenceEqual(uniqueColumns))
                {
                    throw new InvalidSqlException("Columns are already unique", tokens[startIndex]);
                }
            }
            table.Unique.Add(uniqueColumns);
        }

        ParseConflictClause(tokens, ref index);
    }

    List<(Token token, Column column)> ParseIndexColumns(
        ref int index,
        Span<Token> tokens,
        List<Column> existingColumns)
    {
        List<(Token, Column)> columns = new();
        var columnTokens = ReadList(tokens, ref index);
        if (columnTokens.Count == 1)
        {
            var columnName = columnTokens[0].Value.ToLowerInvariant();
            var existingColumn = existingColumns.Where(existing => existing.SqlName.ToLowerInvariant() == columnName.ToLowerInvariant()).FirstOrDefault();
            if (existingColumn == null)
            {
                throw new InvalidSqlException($"Column {columnName} doesn't exist", columnTokens[0]);
            }
            columns.Add((columnTokens[0], existingColumn));
            return columns;
        }
        if (columnTokens.Count > 1)
        {
            foreach (var columnToken in columnTokens)
            {
                var columnName = columnToken.Value.ToLowerInvariant();
                var existingColumn = existingColumns.Where(existing => existing.SqlName.ToLowerInvariant() == columnName.ToLowerInvariant()).FirstOrDefault();
                if (existingColumn == null)
                {
                    throw new InvalidSqlException($"Column {columnName} doesn't exist", columnTokens[0]);
                }
                columns.Add((columnToken, existingColumn));
            }
        }
        return columns;
    }

    void ParseTablePrimaryKeyConstraint(
        ref int index,
        Span<Token> tokens,
        List<Column> existingColumns,
        Table table)
    {
        int startIndex = index;
        if (existingColumns.Any(existing => existing.PrimaryKey) || table.PrimaryKey.Any())
        {
            throw new InvalidSqlException("Table already has a primary key", tokens[index]);
        }
        Increment(ref index, 1, tokens);

        if (tokens.GetValue(index) != "key")
        {
            throw new InvalidSqlException("Expected 'key'", tokens[index]);
        }
        Increment(ref index, 1, tokens);

        var columns = ParseIndexColumns(ref index, tokens, existingColumns);

        if (columns.Count == 1)
        {
            if (columns[0].column.Unique)
            {
                throw new InvalidSqlException("Column is already unique", columns[0].token);
            }
            columns[0].column.PrimaryKey = true;
        }
        else
        {
            var primaryKeyColumns = columns.Select(pair => pair.column).ToList();

            foreach (var existingUniqueColumns in table.Unique)
            {
                if (existingUniqueColumns.SequenceEqual(primaryKeyColumns))
                {
                    throw new InvalidSqlException("Columns are already unique", tokens[startIndex]);
                }
            }
            table.PrimaryKey.AddRange(columns.Select(column => column.column));
        }

        ParseConflictClause(tokens, ref index);
    }

    bool ParseTableConstraint(
        ref int index,
        Span<Token> tokens,
        List<Column> existingColumns,
        IDiagnosticsReporter diagnoticsReporter,
        List<Table> existingTables,
        Table table)
    {
        switch (tokens.GetValue(index))
        {
            case "constraint":
                throw new NotImplementedException();
            case "primary":
                ParseTablePrimaryKeyConstraint(ref index, tokens, existingColumns, table);
                return true;
            case "unique":
                ParseTableUniqueConstraint(ref index, tokens, existingColumns, table);
                return true;
            case "check":
                ParseCheckConstraint(ref index, tokens);
                return true;
            case "foreign":
                throw new NotImplementedException();
        }

        return false;
    }

    void ParseCheckConstraint(ref int index, Span<Token> tokens)
    {
        if (tokens.GetValue(index) != "check")
        {
            throw new InvalidSqlException("Check constraint must start with 'check'", tokens[index]);
        }
        Increment(ref index, 1, tokens);
        SkipBrackets(tokens, ref index);
    }

    void ParseColumnDefinition(ref int index, Span<Token> tokens, List<Column> existingColumns, IDiagnosticsReporter diagnoticsReporter, List<Table> existingTables)
    {
        AssertEnoughTokens(tokens, index);
        string name = tokens[index].Value;
        if (existingColumns.Any(column => column.SqlName.ToLower() == name.ToLower()))
        {
            throw new InvalidSqlException($"Column name {name} already exists in this table", tokens[index]);
        }
        string cSharpName = ToDotnetName(name);
        if (existingColumns.Any(existing => existing.CSharpName == cSharpName))
        {
            throw new InvalidSqlException("Column maps to same csharp name as an existing column", tokens[index]);
        }

        Increment(ref index, 1, tokens);

        var column = new Column();
        column.SqlName = name;
        column.CSharpName = cSharpName;
        column.SqlType = "blob";

        if (!ParseColumnConstraint(tokens, ref index, column, existingColumns, diagnoticsReporter, existingTables))
        {
            var token = tokens[index];
            if (token.Value != "," && token.Value != ")")
            {
                index += ParseType(tokens.Slice(index), out string type);
                column.SqlType = type;
            }
        }

        var typeAffinity = ToTypeAffinity(column.SqlType);

        column.TypeAffinity = typeAffinity;

        while (true)
        {
            var token = tokens[index];
            if (token.Value == "," || token.Value == ")")
            {
                break;
            }
            ParseColumnConstraint(tokens, ref index, column, existingColumns, diagnoticsReporter, existingTables);
        }

        column.CSharpType = ToDotnetType(typeAffinity, column.NotNull);
        existingColumns.Add(column);
    }

    /// <summary>
    /// Read to the token
    /// </summary>
    /// <param name="input"></param>
    /// <param name="consumed">input upto but excluding token</param>
    /// <returns>input starting after token</returns>
    Span<Token> ReadTo(Span<Token> input, string to1, string to2, out Span<Token> consumed, out string found)
    {
        for (int index = 0; index < input.Length; index++)
        {
            if (input[index].Value == to1 || input[index].Value == to2)
            {
                consumed = input.Slice(0, index);
                found = input[index].Value;
                return input.Slice(index + 1);
            }
        }
        consumed = input;
        found = "";
        return Span<Token>.Empty;
    }

    List<Token> Tokenize(string schema)
    {
        var tokens = new List<Token>();
        var text = schema.AsSpan();
        int position = 0;
        int lineIndex = 0;
        int characterInLineIndex = 0;
        while (text.Length > 0)
        {
            text = SkipWhitespace(text, ref position, ref lineIndex, ref characterInLineIndex);
            if (text.Length == 0)
            {
                break;
            }
            text = ReadToken(text, out Token token, ref position, ref lineIndex, ref characterInLineIndex);
            if (token != null)
            {
                tokens.Add(token);
            }
        }
        return tokens;
    }

    ReadOnlySpan<char> ReadSquareBacketToken(ReadOnlySpan<char> text, out Token read, ref int position, ref int lineIndex, ref int characterInLineIndex)
    {
        int positionStart = position;
        int characterInLineIndexStart = characterInLineIndex;
        int lineStart = lineIndex;

        position++;
        characterInLineIndex++;
        for (int index = 1; index < text.Length; index++)
        {
            position++;
            characterInLineIndex++;
            if (text[index] == ']')
            {
                index++;
                string tokenValue = text.Slice(0, index).ToString();
                read = new Token()
                {
                    Value = tokenValue,
                    Position = positionStart,
                    Line = lineStart,
                    CharacterInLine = characterInLineIndexStart,
                    TokenType = TokenType.Other
                };
                return index < text.Length ? text.Slice(index) : ReadOnlySpan<char>.Empty;
            }
            if (IsNewLine(text.Slice(index)))
            {
                lineIndex++;
                characterInLineIndex = 0;
            }
        }

        var token = new Token() { Value = "", Position = position, Line = lineIndex, CharacterInLine = characterInLineIndex, TokenType = TokenType.StringLiteral };
        throw new InvalidSqlException("Ran out of charactors looking for ']'", new Token() { });
    }


    ReadOnlySpan<char> ReadStringLiteral(ReadOnlySpan<char> text, out Token read, ref int position, ref int lineIndex, ref int characterInLineIndex)
    {
        int positionStart = position;
        int characterInLineIndexStart = characterInLineIndex;
        int lineStart = lineIndex;

        bool escaped = false;
        for (int index = 1; index < text.Length; index++)
        {
            position++;
            characterInLineIndex++;
            if (text[index] == '\\' && !escaped)
            {
                escaped = true;
                continue;
            }
            if (IsNewLine(text.Slice(index)))
            {
                lineIndex++;
                characterInLineIndex = 0;
            }
            if (text[index] == '\'' && !escaped)
            {
                index++;
                string tokenValue = text.Slice(0, index).ToString();
                read = new Token()
                {
                    Value = tokenValue,
                    Position = positionStart,
                    Line = lineStart,
                    CharacterInLine = characterInLineIndexStart,
                    TokenType = TokenType.StringLiteral
                };
                return index < text.Length ? text.Slice(index) : ReadOnlySpan<char>.Empty;
            }
            escaped = false;
        }
        var token = new Token() { Value = "", Position = position, Line = lineIndex, CharacterInLine = characterInLineIndex, TokenType = TokenType.StringLiteral };
        throw new InvalidSqlException("Ran out of charactors looking for end of string literal", new Token() { });
    }

    public ReadOnlySpan<char> ReadBlobLiteral(ReadOnlySpan<char> text, out Token read, ref int position, ref int lineIndex, ref int characterInLineIndex)
    {
        int index = 0;

        int startPosition = position;
        int startLine = lineIndex;
        int startCharacterInLine = characterInLineIndex;
        void ThrowInvalidSqlException(string message)
        {
            throw new InvalidSqlException(
                message,
                new Token()
                {
                    Position = startPosition + index,
                    Line = startLine,
                    CharacterInLine = startCharacterInLine + index
                });
        }
        if (text.Length < 2)
        {
            ThrowInvalidSqlException("Ran out of text parsing blob literal");
        }
        if (char.ToLowerInvariant(text[index]) != 'x')
        {
            ThrowInvalidSqlException("Blob literal must start with a 'x' or 'X'");
        }
        index++;
        if (text[index] != '\'')
        {
            ThrowInvalidSqlException("Second character in a blob literal must be a single quote");
        }
        index++;
        int beforeIndex = index;
        ParseHexDigits(text, ref index);
        int hexDigitsParsed = index - beforeIndex;
        if (hexDigitsParsed % 2 != 0)
        {
            ThrowInvalidSqlException("Blob literals must have an even number of hex digits");
        }
        if (text[index] != '\'')
        {
            ThrowInvalidSqlException("Invalid charactor in blob literal");
        }
        index++;

        read = new Token() { Value = text.Slice(0, index).ToString(), Position = position, Line = lineIndex, CharacterInLine = characterInLineIndex, TokenType = TokenType.BlobLiteral };
        position += index;
        characterInLineIndex += index;
        if (IsNewLine(text.Slice(index)))
        {
            lineIndex++;
            characterInLineIndex = 0;
        }
        return text.Slice(index);
    }

    static bool IsHex(char value)
    {
        switch (char.ToLowerInvariant(value))
        {
            case '0':
            case '1':
            case '2':
            case '3':
            case '4':
            case '5':
            case '6':
            case '7':
            case '8':
            case '9':
            case 'a':
            case 'b':
            case 'c':
            case 'd':
            case 'e':
            case 'f':
                return true;
            default:
                return false;
        }
    }

    public void ParseDigits(ReadOnlySpan<char> text, ref int index)
    {
        for (; index < text.Length; index++)
        {
            if (!char.IsDigit(text[index]))
            {
                break;
            }
        }
    }

    public void ParseHexDigits(ReadOnlySpan<char> text, ref int index)
    {
        for (; index < text.Length; index++)
        {
            if (IsHex(text[index]))
            {
                continue;
            }
            return;
        }
    }

    public ReadOnlySpan<char> ParseNumericLiteral(ReadOnlySpan<char> text, out Token read, ref int position, ref int lineIndex, ref int characterInLineIndex)
    {
        int index = 0;
        bool hasDot = false;
        if (text.Length == 0)
        {
            throw new InvalidSqlException("Ran out of text trying to read numberic literal", new Token() { Position = position, Line = lineIndex, CharacterInLine = characterInLineIndex });
        }
        if (text[0] == '.')
        {
            hasDot = true;
            index++;
        }
        else if (!char.IsDigit(text[0]))
        {
            throw new InvalidSqlException("numeric literals must start with a '.' or a digit", new Token() { Position = position, Line = lineIndex, CharacterInLine = characterInLineIndex });
        }
        if (text[0] == '0' && text.Length > 1 && text[1] == 'x')
        {
            index = 2;
            if (index >= text.Length)
            {
                throw new InvalidSqlException("Missing hex value in numeric literal", new Token() { Position = position, Line = lineIndex, CharacterInLine = characterInLineIndex });
            }
            ParseHexDigits(text, ref index);
        }
        else
        {

            // parse digits
            ParseDigits(text, ref index);


            if (index < text.Length && text[index] == '.')
            {
                if (hasDot)
                {
                    throw new InvalidSqlException("numeric literals can contain only one dot", new Token() { Position = position, Line = lineIndex, CharacterInLine = characterInLineIndex });
                }
                index++;
                ParseDigits(text, ref index);
            }

            if (index < text.Length && text[index] == 'e' || text[index] == 'E')
            {
                index++;
                if (index >= text.Length)
                {
                    throw new InvalidSqlException("Missing exponent value in numeric literal", new Token() { Position = position, Line = lineIndex, CharacterInLine = characterInLineIndex });
                }
                switch (text[index])
                {
                    case '+':
                    case '-':
                        index++;
                        break;
                }
                if (index >= text.Length)
                {
                    throw new InvalidSqlException("Missing exponent value in numeric literal", new Token() { Position = position, Line = lineIndex, CharacterInLine = characterInLineIndex });
                }
                ParseDigits(text, ref index);

            }
        }

        read = new Token() { Value = text.Slice(0, index).ToString(), Position = position, Line = lineIndex, CharacterInLine = characterInLineIndex, TokenType = TokenType.NumericLiteral };
        position += index;
        characterInLineIndex += index;
        if (IsNewLine(text.Slice(index)))
        {
            lineIndex++;
            characterInLineIndex = 0;
        }
        return text.Slice(index);
    }

    ReadOnlySpan<char> ReadToken(ReadOnlySpan<char> text, out Token read, ref int position, ref int lineIndex, ref int characterInLineIndex)
    {
        switch (text[0])
        {
            case ',':
            case '(':
            case ')':
            case ';':
            case '+':
            case '-':
                var tokenValue = text.Slice(0, 1).ToString();
                read = new Token() { Value = tokenValue, Position = position, Line = lineIndex, CharacterInLine = characterInLineIndex };
                position += 1;
                characterInLineIndex += 1;
                return text.Slice(1);
        }

        if (text[0] == '[')
        {
            return ReadSquareBacketToken(text, out read, ref position, ref lineIndex, ref characterInLineIndex);
        }

        if (text[0] == '\'')
        {
            return ReadStringLiteral(text, out read, ref position, ref lineIndex, ref characterInLineIndex);
        }

        if (char.ToLowerInvariant(text[0]) == 'x' && text.Length > 1 && text[1] == '\'')
        {
            return ReadBlobLiteral(text, out read, ref position, ref lineIndex, ref characterInLineIndex);
        }

        if ((text[0] == '.' && 1 < text.Length && char.IsDigit(text[1])) ||
            char.IsDigit(text[0]))
        {
            return ParseNumericLiteral(text, out read, ref position, ref lineIndex, ref characterInLineIndex);
        }

        if (text[0] == '.')
        {
            var tokenValue = text.Slice(0, 1).ToString();
            read = new Token() { Value = tokenValue, Position = position, Line = lineIndex, CharacterInLine = characterInLineIndex };
            position += 1;
            characterInLineIndex += 1;
            return text.Slice(1);
        }

        for (int index = 0; index < text.Length; index++)
        {
            switch (text[index])
            {
                case ' ':
                case '.':
                case '\t':
                case '\n':
                case '\r':
                case ',':
                case '(':
                case ')':
                case ';':
                case '\'':
                    string tokenValue = text.Slice(0, index).ToString();
                    read = new Token() { Value = tokenValue, Position = position, Line = lineIndex, CharacterInLine = characterInLineIndex };
                    position += index;
                    characterInLineIndex += index;
                    if (IsNewLine(text.Slice(index)))
                    {
                        lineIndex++;
                        characterInLineIndex = 0;
                    }
                    return text.Slice(index);
                default:
                    break;
            }
        }
        read = null;
        position += text.Length;
        return Span<char>.Empty;
    }

    bool IsNewLine(ReadOnlySpan<char> text)
    {
        return text[0] == '\n';
    }

    ReadOnlySpan<char> SkipWhitespace(ReadOnlySpan<char> text, ref int position, ref int lineIndex, ref int characterInLineIndex)
    {
        for (int index = 0; index < text.Length; index++)
        {
            switch (text[index])
            {
                case '\n':
                    position++;
                    lineIndex++;
                    characterInLineIndex = 0;
                    continue;
                case ' ':
                case '\t':
                case '\r':
                    characterInLineIndex++;
                    position++;
                    continue;
                default:
                    return text.Slice(index);
            }
        }
        return Span<char>.Empty;
    }

    public void Initialize(GeneratorInitializationContext context)
    {
    }
}