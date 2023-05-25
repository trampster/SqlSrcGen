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
        // 	System.Threading.Thread.Sleep(500);
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
            builder.AppendLine($"public record {table.CSharpName}");
            builder.AppendLine("{");

            foreach (var column in table.Columns)
            {
                builder.Append($"    public {column.CSharpType} {column.CSharpName} {{ get; set; }}");
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

    Span<Token> ProcessCreateCommand(Span<Token> tokensToProcess, DatabaseInfo databaseInfo)
    {
        var tokens = tokensToProcess;
        if (tokens[1].Value.ToLower() != "table")
        {
            throw new InvalidProgramException("Unsupported sql command");
        }

        string tableName = tokens[2].Value;

        var table = new Table();
        table.SqlName = tableName;
        table.CSharpName = ToDotnetName(tableName);
        table.CreateTable = string.Join(" ", tokens.Slice(0, IndexOf(tokens, ";") + 1).ToArray().Select(u => u.Value).ToArray());
        databaseInfo.Tables.Add(table);

        if (tokens[3].Value != "(")
        {
            throw new InvalidSqlException($"Missing ( in CREATE TABLE command at position {tokens[3].Position}", tokens[3]);
        }

        tokens = tokens.Slice(4);

        while (true)
        {
            tokens = ReadTo(tokens, ",", ")", out Span<Token> consumed, out string found);

            var column = ParseColumnDefinition(consumed, table.Columns);

            table.Columns.Add(column);

            if (found == ")")
            {
                break;
            }
            if (found == ",")
            {
                continue;
            }

            throw new InvalidSqlException("Ran out of tokens while looking for ')' or ',' in CREATE TABLE command", tokensToProcess[tokensToProcess.Length - 1]);
        }

        if (tokens[0].Value != ";")
        {
            throw new InvalidSqlException($"missing ';' at end of CREATE TABLE command at position {tokens[0].Position}", tokens[0]);
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

    Column ParseColumnDefinition(Span<Token> columnDefinition, IEnumerable<Column> existingColumns)
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

        string type = columnDefinition[1].Value;
        bool notNull = false;
        bool primaryKey = false;
        for (int index = 2; index < columnDefinition.Length; index++)
        {
            var token = columnDefinition[index];
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
                        throw new InvalidSqlException($"Invalid column constraint at position, did you mean 'not null'?", token);
                    }
                    index += 1; //we have effectively consume the next one
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
                        throw new InvalidSqlException($"Invalid column constraint at position, did you mean 'primary key'?", token);
                    }

                    if (existingColumns.Any(column => column.PrimaryKey))
                    {
                        throw new InvalidSqlException($"Table already has a primary key", token);
                    }
                    index += 1; //we have effectively consume the next one
                    primaryKey = true;
                    break;

            }
        }
        var typeAffinity = ToTypeAffinity(type);
        return new Column()
        {
            SqlName = name,
            SqlType = type,
            CSharpName = ToDotnetName(name),
            CSharpType = ToDotnetType(typeAffinity, notNull),
            TypeAffinity = typeAffinity,
            NotNull = notNull,
            PrimaryKey = primaryKey
        };
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
