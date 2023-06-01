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

            try
            {
                ProcessSqlSchema(additionalFiles.First().GetText().ToString(), databaseInfo);
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

    public void ProcessSqlSchema(string schemaText, DatabaseInfo databaseInfo)
    {
        var tokensList = Tokenize(schemaText);
        var tokens = tokensList.ToArray().AsSpan();
        while (tokens.Length > 0)
        {
            switch (tokens[0].Value.ToLower())
            {
                case "create":
                    tokens = ProcessCreateCommand(tokens, databaseInfo);
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

    void ParseTableName(Span<Token> tokens, ref int index, Table table)
    {
        string tableName = tokens[index].Value;
        if (tableName.Contains("."))
        {
            throw new InvalidSqlException("Schema's are not supported", tokens[index]);
        }
        table.SqlName = tableName;
        table.CSharpName = ToDotnetName(tableName);
        Increment(ref index, 1, tokens);
    }

    Span<Token> ProcessCreateCommand(Span<Token> tokensToProcess, DatabaseInfo databaseInfo)
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

        switch (tokens[index].Value.ToLower())
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

        ParseTableName(tokensToProcess, ref index, table);

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

        tokens = tokens.Slice(index);

        while (true)
        {
            //TODO: this needs to skip nested brackets
            //tokens = ReadTo(tokens, ",", ")", out Span<Token> consumed, out string found);

            tokens = ParseColumnDefinition(tokens, table.Columns);
            if (tokens[0].Value == ")")
            {
                tokens = tokens.Slice(1);
                break;
            }
            if (tokens[0].Value == ",")
            {
                tokens = tokens.Slice(1);
                continue;
            }

            throw new InvalidSqlException("Ran out of tokens while looking for ')' or ',' in CREATE TABLE command", tokensToProcess[tokensToProcess.Length - 1]);
        }

        AssertEnoughTokens(tokens, 0);
        if (tokens[0].Value != ";")
        {
            throw new InvalidSqlException($"missing ';' at end of CREATE TABLE command at position {tokens[0].Position}", tokens[0]);
        }
        if (tokens.Length == 0)
        {
            return Span<Token>.Empty;
        }
        return tokens.Slice(1);
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
        bool isFirst = true;
        for (int index = 0; index < name.Length; index++)
        {
            var charactor = name[index];
            if (isFirst)
            {
                builder.Append(charactor.ToString().ToUpperInvariant()[0]);
                isFirst = false;
                continue;
            }
            if (charactor == '_')
            {
                isFirst = true;
                continue;
            }
            builder.Append(charactor);
        }
        return builder.ToString();
    }

    Span<Token> ParseType(Span<Token> typeDefinition, out string type)
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
                    return typeDefinition.Slice(index + 1);
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
        return typeDefinition.Slice(1);
    }

    Span<Token> ParseColumnDefinition(Span<Token> columnDefinition, List<Column> existingColumns)
    {
        if (columnDefinition.Length < 2)
        {
            throw new InvalidSqlException($"Invalid column definition at position {columnDefinition[columnDefinition.Length - 1].Position}", columnDefinition[columnDefinition.Length - 1]);
        }
        string name = columnDefinition[0].Value;
        if (existingColumns.Any(column => column.SqlName.ToLower() == name.ToLower()))
        {
            throw new InvalidSqlException($"Column name {name} already exists in this table", columnDefinition[0]);
        }

        AssertEnoughTokens(columnDefinition, 1);

        columnDefinition = ParseType(columnDefinition.Slice(1), out string type);

        bool notNull = false;
        bool primaryKey = false;
        int index;
        for (index = 0; index < columnDefinition.Length; index++)
        {
            var token = columnDefinition[index];
            bool end = false;
            switch (token.Value.ToLowerInvariant())
            {
                case "not":
                    if (index + 1 > columnDefinition.Length - 1)
                    {
                        throw new InvalidSqlException($"Invalid column constraint, did you mean 'not null'?", token);
                    }
                    var next = columnDefinition[index + 1];
                    if (next.Value.ToLowerInvariant() != "null")
                    {
                        throw new InvalidSqlException($"Invalid column constraint, did you mean 'not null'?", token);
                    }
                    index += 1; //we have effectively consumed the next one
                    notNull = true;
                    break;
                case "primary":
                    if (index + 1 > columnDefinition.Length - 1)
                    {
                        throw new InvalidSqlException($"Invalid column constraint, did you mean 'primary key'?", token);
                    }
                    var next1 = columnDefinition[index + 1];
                    if (next1.Value.ToLowerInvariant() != "key")
                    {
                        throw new InvalidSqlException($"Invalid column constraint, did you mean 'primary key'?", token);
                    }

                    if (existingColumns.Any(column => column.PrimaryKey))
                    {
                        throw new InvalidSqlException($"Table already has a primary key", token);
                    }
                    index += 1; //we have effectively consume the next one
                    primaryKey = true;
                    break;
                case ",":
                case ")":
                    end = true;
                    break;
                default:
                    throw new InvalidSqlException($"Unsupported constraint", token);
            }
            if (end)
            {
                break;
            }
        }
        var typeAffinity = ToTypeAffinity(type);
        existingColumns.Add(new Column()
        {
            SqlName = name,
            SqlType = type,
            CSharpName = ToDotnetName(name),
            CSharpType = ToDotnetType(typeAffinity, notNull),
            TypeAffinity = typeAffinity,
            NotNull = notNull,
            PrimaryKey = primaryKey
        });
        if (index > columnDefinition.Length - 1)
        {
            return Span<Token>.Empty;
        }
        return columnDefinition.Slice(index);
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

    ReadOnlySpan<char> ReadToken(ReadOnlySpan<char> text, out Token read, ref int position, ref int lineIndex, ref int characterInLineIndex)
    {
        switch (text[0])
        {
            case ',':
            case '(':
            case ')':
            case ';':
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
                case '\t':
                case '\n':
                case '\r':
                case ',':
                case '(':
                case ')':
                case ';':
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
