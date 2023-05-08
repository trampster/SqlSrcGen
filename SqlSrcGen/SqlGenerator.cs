using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

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

            builder.AppendLine("    public class Schema");
            builder.AppendLine("    {");
            builder.AppendLine($"        public string SqlSchema = \"{additionalFiles.First().GetText().ToString()}\";");
            builder.AppendLine("    }");

            builder.AppendLine();

            var databaseInfo = new DatabaseInfo();

            builder.IncreaseIndent();
            ProcessSqlSchema(additionalFiles.First().GetText().ToString(), databaseInfo);

            GenerateDatabaseObjects(databaseInfo, builder);

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
            switch (tokens[0].ToLower())
            {
                case "create":
                    tokens = ProcessCreateCommand(tokens, databaseInfo);
                    break;
                default:
                    throw new InvalidProgramException("Unsupported sql command");

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
                builder.AppendLine();
            }
            builder.AppendLine("}");
        }
    }

    Span<string> ProcessCreateCommand(Span<string> tokens, DatabaseInfo databaseInfo)
    {
        if (tokens[1].ToLower() != "table")
        {
            throw new InvalidProgramException("Unsupported sql command");
        }

        string tableName = tokens[2];

        var table = new Table();
        table.SqlName = tableName;
        table.CSharpName = ToDotnetName(tableName);
        table.CreateTable = string.Join(" ", tokens.Slice(0, tokens.IndexOf(";") + 1).ToArray());
        databaseInfo.Tables.Add(table);

        if (tokens[3] != "(")
        {
            throw new InvalidSqlException("Missing ( in CREATE TABLE command");
        }

        tokens = tokens.Slice(4);

        while (true)
        {
            tokens = ReadTo(tokens, ",", ")", out Span<string> consumed, out string found);

            (string name, string type) = ParseColumnDefinition(consumed);

            var typeAffinity = ToTypeAffinity(type);
            var column = new Column()
            {
                SqlName = name,
                SqlType = type,
                CSharpName = ToDotnetName(name),
                CSharpType = ToDotnetType(typeAffinity),
                TypeAffinity = typeAffinity
            };
            table.Columns.Add(column);

            if (found == ")")
            {
                break;
            }
            if (found == ",")
            {
                continue;
            }

            throw new InvalidSqlException("Ran out of tokens while looking for ')' or ',' in CREATE TABLE command");
        }

        if (tokens[0] != ";")
        {
            throw new InvalidSqlException("missing ';' at end of CREATE TABLE command");
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

    string ToDotnetType(TypeAffinity typeAffinity)
    {
        return typeAffinity switch
        {
            TypeAffinity.INTEGER => "long",
            TypeAffinity.TEXT => "string",
            TypeAffinity.BLOB => "byte[]",
            TypeAffinity.REAL => "dobule",
            TypeAffinity.NUMERIC => "object",
            _ => "object"
        };
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

    (string name, string type) ParseColumnDefinition(Span<string> columnDefinition)
    {
        return (columnDefinition[0], columnDefinition[1]);
    }

    /// <summary>
    /// Read to the token
    /// </summary>
    /// <param name="input"></param>
    /// <param name="consumed">input upto but excluding token</param>
    /// <returns>input starting after token</returns>
    Span<string> ReadTo(Span<string> input, string to1, string to2, out Span<string> consumed, out string found)
    {
        for (int index = 0; index < input.Length; index++)
        {
            if (input[index] == to1 || input[index] == to2)
            {
                consumed = input.Slice(0, index);
                found = input[index];
                return input.Slice(index + 1);
            }
        }
        consumed = input;
        found = "";
        return Span<string>.Empty;
    }

    List<string> Tokenize(string schema)
    {
        var tokens = new List<string>();
        var text = schema.AsSpan();
        while (text.Length > 0)
        {
            text = SkipWhitespace(text);
            text = ReadToken(text, out string token);
            if (token != "")
            {
                tokens.Add(token);
            }
        }
        return tokens;
    }

    ReadOnlySpan<char> ReadToken(ReadOnlySpan<char> text, out string read)
    {
        switch (text[0])
        {
            case ',':
            case '(':
            case ')':
            case ';':
                read = text.Slice(0, 1).ToString();
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
                    read = text.Slice(0, index).ToString();
                    return text.Slice(index);
                default:
                    break;
            }
        }
        read = "";
        return Span<char>.Empty;
    }

    ReadOnlySpan<char> SkipWhitespace(ReadOnlySpan<char> text)
    {
        for (int index = 0; index < text.Length; index++)
        {
            switch (text[index])
            {
                case ' ':
                case '\t':
                case '\n':
                case '\r':
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
