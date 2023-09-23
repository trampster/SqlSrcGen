using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SqlSrcGen.Generator;

[Generator]
public class SqlGenerator : Parser, ISourceGenerator
{
    readonly LiteralValueParser _literalValueParser;
    readonly NumberParser _numberParser;
    readonly TypeNameParser _typeNameParser;
    readonly ExpressionParser _expressionParser;
    readonly CollationParser _collationParser;
    readonly SelectParser _selectParser;
    readonly List<IParser> _parsers;
    readonly DatabaseInfo _databaseInfo;
    readonly Tokenizer _tokenizer;

    public SqlGenerator()
    {
        // while (!System.Diagnostics.Debugger.IsAttached)
        //     System.Threading.Thread.Sleep(500);//
        _databaseInfo = new DatabaseInfo();
        _literalValueParser = new LiteralValueParser();
        _numberParser = new NumberParser();
        _typeNameParser = new TypeNameParser();
        _collationParser = new CollationParser();
        _expressionParser = new ExpressionParser(_databaseInfo, _literalValueParser, _typeNameParser, _collationParser);
        _selectParser = new SelectParser(_databaseInfo, _expressionParser);
        _parsers = new List<IParser>
        {
            _literalValueParser,
            _expressionParser,
            _numberParser,
            _typeNameParser,
            _collationParser,
            _selectParser
        };
        _tokenizer = new Tokenizer();
    }

    IDiagnosticsReporter? _diagnosticsReporter;
    public override IDiagnosticsReporter? DiagnosticsReporter
    {
        get => _diagnosticsReporter;
        set
        {
            _diagnosticsReporter = value;
            foreach (var parser in _parsers)
            {
                parser.DiagnosticsReporter = value;
            }
        }
    }

    void SetQuery(Query query)
    {
        Query = query;
        foreach (var parser in _parsers)
        {
            parser.Query = query;
        }
    }

    public void Execute(GeneratorExecutionContext context)
    {

        var databaseAccessGenerator = new DatabaseAccessGenerator();
        var additionalFiles = context.AdditionalFiles.Where(at => at.Path.EndsWith(".sql")).ToList();
        if (additionalFiles.Any())
        {
            var builder = new SourceBuilder();
            builder.AppendLine("#nullable enable");
            builder.AppendLine("using System;");
            databaseAccessGenerator.GenerateUsings(builder);
            builder.AppendLine("namespace SqlSrcGen");
            builder.AppendLine("{");

            builder.AppendLine();

            builder.IncreaseIndent();

            var reporter = new DiagnosticsReporter(context);

            List<QueryInfo> queries = new();
            AdditionalText? currentFile = null;

            try
            {
                var schemaFile = additionalFiles.Where(additionText => Path.GetFileName(additionText.Path) == "SqlSchema.sql").FirstOrDefault();
                if (schemaFile == null)
                {
                    //no schema which means we can't generate queries as we don't know the table definitions
                    reporter.Warning(ErrorCode.SSG0006, "Did not find a SqlSchema.sql file, add one and include it as additional AdditionalFiles in your project file.");
                    return;
                }
                currentFile = schemaFile;

                reporter.Path = schemaFile.Path;
                DiagnosticsReporter = reporter;
                foreach (var parser in _parsers)
                {
                    parser.DiagnosticsReporter = reporter;
                }

                ProcessSqlSchema(schemaFile.GetText()!.ToString(), _databaseInfo);
                GenerateDatabaseObjects(_databaseInfo, builder, reporter);

                foreach (var selectQueryFile in additionalFiles.Where(file => Path.GetExtension(file.Path) == ".sql" && Path.GetFileName(file.Path) != "SqlSchema.sql"))
                {
                    currentFile = selectQueryFile;
                    reporter.Path = selectQueryFile.Path;
                    string query = selectQueryFile.GetText()!.ToString();
                    var queryInfo = ProcessQuery(query);
                    queryInfo.QueryString = query;
                    // file name must follow convention Select_CSharpMethodName_CSharpType.sql
                    (string? csharpResultType, string? methodName, string? csharpInputType) = GetMethodDetailsFromPath(selectQueryFile.Path, reporter);
                    if (csharpResultType == null || methodName == null || csharpInputType == null)
                    {
                        continue;
                    }
                    queryInfo.MethodName = methodName;
                    queryInfo.CSharpResultType = csharpResultType;
                    queryInfo.CSharpInputType = csharpInputType;

                    if (queryInfo.CSharpResultType != "void")
                    {
                        GenerateCSharpType(queryInfo.CSharpResultType, queryInfo.Columns.ToArray(), reporter, builder);
                    }

                    queries.Add(queryInfo);
                }
            }
            catch (InvalidSqlException exception)
            {
                var sqlFile = currentFile;

                var token = exception.Token;

                context.ReportDiagnostic(
                    Diagnostic.Create(
                        new DiagnosticDescriptor(
                            ErrorCode.SSG0001.ToString(),
                            "Invalid SQL",
                            exception.Message,
                            "SQL",
                            DiagnosticSeverity.Error,
                            true),
                        Location.Create(sqlFile!.Path,
                        TextSpan.FromBounds(token?.Position ?? 0, token?.Value.Length ?? 0 + token?.Position ?? 0),
                        new LinePositionSpan(
                            new LinePosition(token?.Line ?? 0, token?.CharacterInLine ?? 0),
                            new LinePosition(token?.Line ?? 0, token?.CharacterInLine ?? 0 + token?.Value.Length ?? 0)))));
                return;
            }


            builder.AppendLine();
            databaseAccessGenerator.Generate(builder, _databaseInfo, queries);


            builder.DecreaseIndent();
            //end of namespace
            builder.AppendLine("}");

            var code = builder.ToString();
            context.AddSource($"SqlSchema.g.cs", code);
        }
    }

    Dictionary<string, Column[]> _cSharpTypes = new();

    bool DoColumnsMatch(Column[] existing, Column[] newColumns)
    {
        if (existing.Length != newColumns.Length)
        {

            return false;
        }
        foreach (var newColumn in newColumns)
        {
            if (!existing.Any(col => col.CSharpName == newColumn.CSharpName))
            {
                return false;
            }
        }
        return true;
    }

    bool GenerateCSharpType(string name, Column[] columns, IDiagnosticsReporter reporter, SourceBuilder builder)
    {
        if (_cSharpTypes.TryGetValue(name, out var existingTypeColumns))
        {
            if (!DoColumnsMatch(columns, existingTypeColumns))
            {
                reporter.Error(ErrorCode.SSG0008, $"Type mismatch, type {name} already exists with different properties");
                return false;
            }
            return true;
        }
        _cSharpTypes.Add(name, columns);
        GenerateClassRecord(name, columns, builder);
        return true;
    }

    (string? resultCSharpType, string? methodName, string? inputCSharpType) GetMethodDetailsFromPath(string path, IDiagnosticsReporter reporter)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        var parts = fileName.Split('_');
        if (parts.Length != 3)
        {
            reporter.Warning(ErrorCode.SSG0007, $"Expected sql file name {fileName} to be in form QueryType_MethodName_CSharpType.sql");
            return (null, null, null);
        }
        return (parts[0], parts[1], parts[2]);
    }

    public void ProcessSqlSchema(string schemaText, DatabaseInfo databaseInfo)
    {
        var tokensList = _tokenizer.Tokenize(schemaText);
        var tokens = tokensList.ToArray().AsSpan();
        while (tokens.Length > 0)
        {
            switch (tokens[0].Value.ToLower())
            {
                case "create":
                    SetQuery(new Query());
                    tokens = ProcessCreateCommand(tokens, databaseInfo);
                    break;
                default:
                    throw new InvalidSqlException("Unsupported sql command", tokens[0]);
            }
        }
    }

    public QueryInfo ProcessQuery(string selectText)
    {
        var tokensList = _tokenizer.Tokenize(selectText);
        var tokens = tokensList.ToArray().AsSpan();
        var queryInfo = new QueryInfo();
        int index = 0;
        if (!_selectParser.Parse(ref index, tokens, queryInfo))
        {
            throw new InvalidSqlException("Unsupported query", tokens[index]);
        }
        queryInfo.Process();
        return queryInfo;
    }

    public void GenerateDatabaseObjects(DatabaseInfo databaseInfo, SourceBuilder builder, IDiagnosticsReporter reporter)
    {
        foreach (var table in databaseInfo.Tables)
        {
            if (table.Tempory)
            {
                continue;
            }
            GenerateCSharpType(table.CSharpName, table.Columns.ToArray(), reporter, builder);
        }
    }

    void GenerateClassRecord(string className, IEnumerable<Column> columns, SourceBuilder builder)
    {
        builder.AppendLine($"public record {className}");
        builder.AppendLine("{");
        builder.IncreaseIndent();

        foreach (var column in columns)
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

    void ParseTableName(Span<Token> tokens, ref int index, Table table, List<Table> existingTables)
    {
        string tableName = tokens[index].Value;
        if (tokens.GetValue(index + 1) == ".")
        {
            throw new InvalidSqlException("Attached databases are not supported", tokens[index]);
        }

        table.SqlName = tableName;
        table.CSharpName = CSharp.ToCSharpName(tableName);

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
            bool readTableConstraint = ParseTableConstraint(ref index, tokens, table.Columns, databaseInfo.Tables, table);
            if (!finishedColumns && readTableConstraint)
            {
                finishedColumns = true;
            }
            if (!finishedColumns)
            {
                ParseColumnDefinition(ref index, tokens, table.Columns, databaseInfo.Tables, table);
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

        ParseTableOptions(ref index, tokens);

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

    void ParseTableOptions(ref int index, Span<Token> tokens)
    {
        if (IsEnd(index, tokens))
        {
            return;
        }
        if (!Is(index, tokens, "without", "strict"))
        {
            return;
        }

        while (!IsEnd(index, tokens))
        {
            switch (tokens.GetValue(index))
            {
                case "without":
                    Increment(ref index, 1, tokens);
                    Expect(index, tokens, "rowid");
                    break;
                case "strict":
                    break;
                default:
                    throw new InvalidSqlException("Expected WITHOUT or STRICT", tokens[index]);
            }
            if (IsEnd(index + 1, tokens))
            {
                index++;
                return;
            }
            Increment(ref index, 1, tokens);
            if (tokens.GetValue(index) != ",")
            {
                return;
            }
            Increment(ref index, 1, tokens);

        }
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

    void ParsePrimaryKeyConstraint(Span<Token> columnDefinition, ref int index, IEnumerable<Column> existingColumns, Column column)
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

    void PraseDefaultConstraint(Span<Token> columnDefinition, ref int index)
    {
        if (columnDefinition.GetValue(index) != "default")
        {
            throw new InvalidSqlException($"expected default constraint to begin with 'default'", columnDefinition[index]);
        }
        Increment(ref index, 1, columnDefinition);

        var token = columnDefinition[index];
        // signed number
        if (_numberParser.ParseSignedNumber(ref index, columnDefinition))
        {
            return;
        }

        // expression
        if (token.Value == "(")
        {
            SkipBrackets(columnDefinition, ref index);
            return;
        }

        //literal
        if (!_literalValueParser.Parse(ref index, columnDefinition))
        {
            throw new InvalidSqlException($"Unrecognised default constraint", columnDefinition[index]);
        }
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

    void CheckForeignKeyLists(List<Token> foreignColumns, List<Column> localColumns, int start, int index, Table foreignTable, bool isColumnConstraint, Span<Token> tokens)
    {
        if (isColumnConstraint)
        {
            if (foreignColumns.Count == 0)
            {
                var foreignColumn = foreignTable.Columns.Where(column => column.PrimaryKey).SingleOrDefault();
                if (foreignColumn == null)
                {
                    throw new InvalidSqlException($"Referenced table does not have a primary key column", tokens[index]);
                }
                return;
            }
            else if (foreignColumns.Count != 1)
            {
                throw new InvalidSqlException($"column constraints must reference a single column", tokens[index]);
            }
        }

        if (foreignColumns.Count == 0)
        {
            if (foreignTable.PrimaryKey.Count != localColumns.Count)
            {
                throw new InvalidSqlException($"Referenced table doesn't have a matching foreign key", tokens[start]);
            }
            return;
        }

        if (localColumns.Count() != foreignColumns.Count())
        {
            throw new InvalidSqlException("Local and foreign column counts must match", tokens[start]);
        }

        foreach (var foreignColumn in foreignColumns)
        {
            var columnName = foreignColumn.Value.ToLowerInvariant();
            var column = foreignTable.Columns.Where(column => column.SqlName == columnName).FirstOrDefault();
            if (column == null)
            {
                throw new InvalidSqlException($"Referenced column {columnName} does not exist", foreignColumn);
            }
        }

        if (!foreignTable.IsUniqueBy(foreignColumns.Select(column => column.Value).ToList()))
        {
            throw new InvalidSqlException($"Foreign table is not unique by these columns", tokens[start]);
        }
    }

    void ParseForeignKeyClause(Span<Token> tokens, ref int index, List<Table> existingTables, List<Column> localColumns, bool isColumnConstraint)
    {
        int start = index;
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
        var foreignColumnList = ReadList(tokens, ref index);


        CheckForeignKeyLists(foreignColumnList, localColumns, start, index, foreignTable, isColumnConstraint, tokens);

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
                DiagnosticsReporter!.Warning(
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

    bool ParseColumnConstraint(Span<Token> columnDefinition, ref int index, Column column, IEnumerable<Column> existingColumns, List<Table> existingTables)
    {
        if (columnDefinition.GetValue(index) == "constraint")
        {
            Increment(ref index, 1, columnDefinition);
            if (columnDefinition[index].TokenType != TokenType.Other)
            {
                throw new InvalidSqlException($"Expected column name", columnDefinition[index]);
            }
            Increment(ref index, 1, columnDefinition);
            return ParseColumnConstraintDefinition(columnDefinition, ref index, column, existingColumns, existingTables);
        }
        return ParseColumnConstraintDefinition(columnDefinition, ref index, column, existingColumns, existingTables);
    }

    bool ParseColumnConstraintDefinition(Span<Token> tokens, ref int index, Column column, IEnumerable<Column> existingColumns, List<Table> existingTables)
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
                _collationParser.PraseCollateConstraint(tokens, ref index, column);
                return true;
            case "references":
                ParseForeignKeyClause(tokens, ref index, existingTables, new List<Column> { column }, true);
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
        IEnumerable<Column> existingColumns,
        Table table)
    {
        int startIndex = index;
        Increment(ref index, 1, tokens);

        var columns = ParseColumnList(ref index, tokens, existingColumns);

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


    /// <summary>
    /// Parses a list of indexed columns and checks they exist in the current table
    /// </summary>
    List<(Token token, Column column)> ParseIndexedColumnList(
        ref int index,
        Span<Token> tokens,
        Table table)
    {
        Expect(index, tokens, "(");
        Increment(ref index, 1, tokens);
        bool finished = false;
        List<(Token token, Column column)> columns = new();
        while (!finished)
        {
            switch (tokens.GetValue(index))
            {
                case ",":
                    Increment(ref index, 1, tokens);
                    break;
                case ")":
                    index++;
                    finished = true;
                    break;
                default:
                    var column = ParseIndexedColumn(ref index, tokens, table);
                    if (column != null)
                    {
                        columns.Add(column.Value);
                    }
                    break;
            }
        }
        return columns;
    }

    bool IsExistingColumn(Token token, Table table)
    {
        if (token.TokenType == TokenType.Other)
        {
            var tokenValue = token.Value.ToLowerInvariant();
            return table.Columns.Any(column => column.SqlName.ToLowerInvariant() == tokenValue);
        }
        return false;
    }

    (Token token, Column column)? ParseIndexedColumn(ref int index, Span<Token> tokens, Table table)
    {
        AssertEnoughTokens(tokens, index);
        Token? token = null;
        Column? column = null;
        bool expression = false;
        if (IsExistingColumn(tokens[index], table))
        {
            token = tokens[index];
            var tokenValue = token.Value.ToLowerInvariant();
            column = table.Columns.First(column => column.SqlName.ToLowerInvariant() == tokenValue);
            index++;
        }
        else
        {
            var expr = _expressionParser.Parse(ref index, tokens, table);
            if (expr == null)
            {
                throw new InvalidSqlException("Expected column name or expression", tokens[index]);
            }
            expression = true;
        }
        if (!IsEnd(index, tokens))
        {

            if (tokens.GetValue(index) == "collate")
            {
                _collationParser.ParseCollationStatement(ref index, tokens);
            }

            if (!IsEnd(index, tokens))
            {
                switch (tokens.GetValue(index))
                {
                    case "asc":
                    case "desc":
                        index++;
                        break;
                }
            }
        }

        if (expression)
        {
            return null;
        }
        return (token!, column!);
    }

    /// <summary>
    /// Parses a list of columns and checks they exist in the current table
    /// </summary>
    List<(Token token, Column column)> ParseColumnList(
        ref int index,
        Span<Token> tokens,
        IEnumerable<Column> existingColumns)
    {
        List<(Token, Column)> columns = new();
        var columnTokens = ReadList(tokens, ref index);

        foreach (var columnToken in columnTokens)
        {
            var columnName = columnToken.Value.ToLowerInvariant();
            var existingColumn = existingColumns.Where(existing => existing.SqlName.ToLowerInvariant() == columnName.ToLowerInvariant()).FirstOrDefault();
            if (existingColumn == null)
            {
                throw new InvalidSqlException($"Column {columnName} doesn't exist", columnToken);
            }
            columns.Add((columnToken, existingColumn));
        }
        return columns;
    }

    void ParseTableForeignKeyConstraint(
        ref int index,
        Span<Token> tokens,
        Table table,
        List<Table> existingTables)
    {
        if (tokens.GetValue(index) != "foreign")
        {
            throw new InvalidSqlException("Expected foreign key to start with 'foreign", tokens[index]);
        }
        Increment(ref index, 1, tokens);
        if (tokens.GetValue(index) != "key")
        {
            throw new InvalidSqlException("Expected 'key'", tokens[index]);
        }
        Increment(ref index, 1, tokens);

        var localColumns = ParseColumnList(ref index, tokens, table.Columns);

        ParseForeignKeyClause(tokens, ref index, existingTables, localColumns.Select(col => col.column).ToList(), false);
    }

    void ParseTablePrimaryKeyConstraint(
        ref int index,
        Span<Token> tokens,
        IEnumerable<Column> existingColumns,
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

        var columns = ParseIndexedColumnList(ref index, tokens, table);

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
        IEnumerable<Column> existingColumns,
        List<Table> existingTables,
        Table table)
    {
        switch (tokens.GetValue(index))
        {
            case "constraint":
                Increment(ref index, 1, tokens);
                if (tokens[index].TokenType != TokenType.Other)
                {
                    throw new InvalidSqlException("Expected constraint name", tokens[index]);
                }
                Increment(ref index, 1, tokens);
                ParseTableConstraint(ref index, tokens, existingColumns, existingTables, table);
                return true;
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
                ParseTableForeignKeyConstraint(ref index, tokens, table, existingTables);
                return true;
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

    void ParseColumnDefinition(ref int index, Span<Token> tokens, IEnumerable<Column> existingColumns, List<Table> existingTables, Table table)
    {
        AssertEnoughTokens(tokens, index);
        string name = tokens[index].Value;
        if (existingColumns.Any(column => column.SqlName.ToLower() == name.ToLower()))
        {
            throw new InvalidSqlException($"Column name {name} already exists in this table", tokens[index]);
        }
        string cSharpName = CSharp.ToCSharpName(name);
        if (existingColumns.Any(existing => existing.CSharpName == cSharpName))
        {
            throw new InvalidSqlException("Column maps to same csharp name as an existing column", tokens[index]);
        }

        Increment(ref index, 1, tokens);

        var column = new Column();
        column.SqlName = name;
        column.CSharpName = cSharpName;
        column.SqlType = "blob";

        if (!ParseColumnConstraint(tokens, ref index, column, existingColumns, existingTables))
        {
            var token = tokens[index];
            if (token.Value != "," && token.Value != ")")
            {
                _typeNameParser.ParseTypeName(ref index, tokens, out string type);
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
            if (!ParseColumnConstraint(tokens, ref index, column, existingColumns, existingTables))
            {
                throw new InvalidSqlException("Unexpected token", token);
            }
        }

        column.CSharpType = ToDotnetType(typeAffinity, column.NotNull);
        table.AddColumn(column);
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

    public void Initialize(GeneratorInitializationContext context)
    {
    }
}