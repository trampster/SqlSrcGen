using System.Text;
using System.Collections.Generic;
using System;
using System.Linq;

namespace SqlSrcGen.Generator;

public class DatabaseAccessGenerator
{
    public void GenerateUsings(SourceBuilder builder)
    {
        builder.AppendLine("using System.Runtime.InteropServices;");
        builder.AppendLine("using System.Text;");
        builder.AppendLine("using SqlSrcGen;");
    }

    public void Generate(SourceBuilder builder, DatabaseInfo databaseInfo)
    {

        builder.AppendLine("public class Database : IDisposable");
        builder.AppendLine("{");
        builder.AppendLine("    readonly IntPtr _dbHandle;");
        builder.AppendLine();
        builder.AppendLine("    public Database(string filename)");
        builder.AppendLine("    {");
        builder.AppendLine("        var result = SqliteNativeMethods.sqlite3_open(filename, out _dbHandle);");
        builder.AppendLine("        if (result != Result.Ok)");
        builder.AppendLine("        {");
        builder.AppendLine("            throw new SqliteException($\"Failed to open database {filename}\", result);");
        builder.AppendLine("        }");
        builder.AppendLine("    }");

        builder.IncreaseIndent();

        builder.AppendLine();


        builder.AppendLine();

        if (databaseInfo.Tables.Any(table => table.Columns.Any(column => column.AutoIncrement)))
        {
            GenerateLastInsertRowId(builder);
        }

        GenerateBeginTransaction(builder);
        GenerateCommitTransaction(builder);
        GenerateRollbackTransaction(builder);

        foreach (var table in databaseInfo.Tables)
        {
            GenerateCreateTable(table, builder);
            GenerateGetAll(table, builder);
            GenerateInsert(table, builder);
            var uniqueColumns = table.Columns.Where(column => column.PrimaryKey || column.Unique).ToList();
            foreach (var column in uniqueColumns)
            {
                GenerateGet(table, new List<Column> { column }, builder);
                GenerateDelete(table, new List<Column> { column }, builder);
            }
            if (table.PrimaryKey.Any())
            {
                GenerateGet(table, table.PrimaryKey, builder);
                GenerateDelete(table, table.PrimaryKey, builder);
            }
            foreach (var uniqueColumnSets in table.Unique)
            {
                GenerateGet(table, uniqueColumnSets, builder);
                GenerateDelete(table, uniqueColumnSets, builder);
            }

            GenerateDeleteAll(table, builder);
        }

        builder.DecreaseIndent();

        builder.AppendLine("    bool _disposedValue;");
        builder.AppendLine();
        builder.AppendLine("    protected virtual void Dispose(bool disposing)");
        builder.AppendLine("    {");
        builder.AppendLine("        if (!_disposedValue)");
        builder.AppendLine("        {");
        builder.AppendLine("            SqliteNativeMethods.sqlite3_close(_dbHandle);");

        builder.IncreaseIndent();
        builder.IncreaseIndent();
        builder.IncreaseIndent();
        foreach (var disposeBlock in _disposeCode)
        {
            disposeBlock(builder);
        }
        builder.DecreaseIndent();
        builder.DecreaseIndent();
        builder.DecreaseIndent();

        builder.AppendLine("            _disposedValue = true;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");

        builder.AppendLine();

        builder.AppendLine("    ~Database()");
        builder.AppendLine("    {");
        builder.AppendLine("        Dispose(disposing: false);");
        builder.AppendLine("    }");

        builder.AppendLine();

        builder.AppendLine("    public void Dispose()");
        builder.AppendLine("    {");
        builder.AppendLine("        Dispose(disposing: true);");
        builder.AppendLine("        GC.SuppressFinalize(this);");
        builder.AppendLine("    }");
        builder.AppendLine("}");
    }

    List<Action<SourceBuilder>> _disposeCode = new List<Action<SourceBuilder>>();

    void AppendQueryBytesField(SourceBuilder builder, string name, string query)
    {
        var bytes = Encoding.UTF8.GetBytes(query);
        builder.AppendLine($"// {query}");
        builder.AppendStart($"static readonly byte[] {name} = new byte[] {{");
        bool isFirst = true;
        foreach (var value in bytes)
        {
            if (!isFirst)
            {
                builder.Append(",");
            }
            builder.Append($"0x{value.ToString("X2")}");
            isFirst = false;
        }
        builder.Append("};");
        builder.AppendLine();
        builder.AppendLine();
    }

    void GenerateCreateTable(Table table, SourceBuilder builder)
    {
        string createTableSqlBytesFieldName = $"_create{table.CSharpName}Bytes";
        AppendQueryBytesField(builder, createTableSqlBytesFieldName, table.CreateTable);

        // we don't keep the statement, because we don't expect the table to be created multiple times

        builder.AppendLine($"public void Create{table.CSharpName}Table()");
        builder.AppendLine("{");

        builder.AppendLine($"   var result = SqliteNativeMethods.sqlite3_prepare_v2(_dbHandle, {createTableSqlBytesFieldName}, {createTableSqlBytesFieldName}.Length, out IntPtr statementPtr, IntPtr.Zero);");

        builder.AppendLine("    try");
        builder.AppendLine("    {");
        builder.AppendLine("        if (result != Result.Ok)");
        builder.AppendLine("        {");
        builder.AppendLine($"            throw new SqliteException(\"Failed to prepare sqlite statement {table.CreateTable}\", result);");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        result = SqliteNativeMethods.sqlite3_step(statementPtr);");
        builder.AppendLine();
        builder.AppendLine("        if (result != Result.Done)");
        builder.AppendLine("        {");
        builder.AppendLine($"            throw new SqliteException(\"Failed to execute sqlite statement {table.CreateTable}\", result);");
        builder.AppendLine();
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("    finally");
        builder.AppendLine("    {");
        builder.AppendLine("        SqliteNativeMethods.sqlite3_finalize(statementPtr);");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    void GenerateDelete(Table table, List<Column> columns, SourceBuilder builder)
    {
        var queryBuilder = new StringBuilder();
        queryBuilder.Append($"DELETE FROM {table.SqlName} WHERE ");
        bool isFirst = true;
        foreach (var column in columns)
        {
            if (!isFirst)
            {
                queryBuilder.Append("AND ");
            }
            queryBuilder.Append($"{column.SqlName} == ? ");
            isFirst = false;
        }
        queryBuilder.Append(";");

        var query = queryBuilder.ToString();
        string deleteAllSqlBytesFieldName = $"_delete{table.CSharpName}Bytes{columns.Count()}";
        AppendQueryBytesField(builder, deleteAllSqlBytesFieldName, query);

        string statementPointerFieldName = $"_delete{table.CSharpName}Statement{columns.Count()}";
        builder.AppendLine($"IntPtr {statementPointerFieldName} = IntPtr.Zero;");
        AppendDisposeStatement(statementPointerFieldName);

        builder.AppendStart($"public void Delete{table.CSharpName}(");

        isFirst = true;
        foreach (var column in columns)
        {
            if (!isFirst)
            {
                builder.Append(" ,");
            }
            builder.Append($"{column.CSharpType} {column.CSharpParameterName}");
            isFirst = false;
        }
        builder.Append(")");
        builder.AppendLine();

        builder.AppendLine($"{{");

        builder.AppendLine($"    if({statementPointerFieldName} == IntPtr.Zero)");
        builder.AppendLine($"    {{");
        builder.AppendLine($"        var result = SqliteNativeMethods.sqlite3_prepare_v2(_dbHandle, {deleteAllSqlBytesFieldName}, {deleteAllSqlBytesFieldName}.Length, out {statementPointerFieldName}, IntPtr.Zero);");
        builder.AppendLine($"        if (result != Result.Ok)");
        builder.AppendLine($"        {{");
        builder.AppendLine($"            throw new SqliteException(\"Failed to prepare sqlite statement {query}\", result);");
        builder.AppendLine($"        }}");
        builder.AppendLine($"    }}");

        builder.AppendLine();

        int bindIndex = 1;
        foreach (var column in columns)
        {
            BindValue(column, builder, statementPointerFieldName, bindIndex, column.CSharpParameterName, false);
            bindIndex++;
        }

        builder.AppendLine();

        builder.AppendLine($"    var stepResult = SqliteNativeMethods.sqlite3_step({statementPointerFieldName});");
        builder.AppendLine();
        builder.AppendLine($"    if (stepResult != Result.Done)");
        builder.AppendLine($"    {{");
        builder.AppendLine($"        throw new SqliteException(\"Failed to execute sqlite statement {table.CreateTable}\", stepResult);");
        builder.AppendLine($"    }}");

        builder.AppendLine();

        builder.AppendLine($"    // reset the statement so it's ready for next time");
        builder.AppendLine($"    var resetResult = SqliteNativeMethods.sqlite3_reset({statementPointerFieldName});");
        builder.AppendLine($"    if (resetResult != Result.Ok)");
        builder.AppendLine($"    {{");
        builder.AppendLine($"        throw new SqliteException($\"Failed to reset sqlite statement {query}\", resetResult);");
        builder.AppendLine($"    }}");

        builder.AppendLine($"}}");
        builder.AppendLine();
    }

    void GenerateDeleteAll(Table table, SourceBuilder builder)
    {
        string deleteAllSqlBytesFieldName = $"_deleteAll{table.CSharpName}sBytes";
        string query = $"delete from {table.SqlName};";
        AppendQueryBytesField(builder, deleteAllSqlBytesFieldName, query);

        string statementPointerFieldName = $"_deleteAll{table.CSharpName}sStatement";
        builder.AppendLine($"IntPtr {statementPointerFieldName} = IntPtr.Zero;");
        AppendDisposeStatement(statementPointerFieldName);

        builder.AppendLine($"public void DeleteAll{table.CSharpName}s()");
        builder.AppendLine($"{{");

        builder.AppendLine($"    if({statementPointerFieldName} == IntPtr.Zero)");
        builder.AppendLine($"    {{");
        builder.AppendLine($"        var result = SqliteNativeMethods.sqlite3_prepare_v2(_dbHandle, {deleteAllSqlBytesFieldName}, {deleteAllSqlBytesFieldName}.Length, out {statementPointerFieldName}, IntPtr.Zero);");
        builder.AppendLine($"        if (result != Result.Ok)");
        builder.AppendLine($"        {{");
        builder.AppendLine($"            throw new SqliteException(\"Failed to prepare sqlite statement {query}\", result);");
        builder.AppendLine($"        }}");
        builder.AppendLine($"    }}");

        builder.AppendLine();
        builder.AppendLine($"    var stepResult = SqliteNativeMethods.sqlite3_step({statementPointerFieldName});");
        builder.AppendLine();
        builder.AppendLine($"    if (stepResult != Result.Done)");
        builder.AppendLine($"    {{");
        builder.AppendLine($"        throw new SqliteException(\"Failed to execute sqlite statement {table.CreateTable}\", stepResult);");
        builder.AppendLine($"    }}");

        builder.AppendLine();

        builder.AppendLine($"    // reset the statement so it's ready for next time");
        builder.AppendLine($"    var resetResult = SqliteNativeMethods.sqlite3_reset({statementPointerFieldName});");
        builder.AppendLine($"    if (resetResult != Result.Ok)");
        builder.AppendLine($"    {{");
        builder.AppendLine($"        throw new SqliteException($\"Failed to reset sqlite statement {query}\", resetResult);");
        builder.AppendLine($"    }}");

        builder.AppendLine($"}}");
        builder.AppendLine();
    }

    uint _number = 0;
    uint GetUniqueNumber()
    {
        var number = _number;
        _number++;
        return number;
    }

    void GenerateLastInsertRowId(SourceBuilder builder)
    {
        var query = "SELECT last_insert_rowid();";
        string getSqlBytesFieldName = $"_getLastInsertRowIdBytes";
        AppendQueryBytesField(builder, getSqlBytesFieldName, query);

        string statementPointerFieldName = $"_getLastInsertRowIdStatement";

        builder.AppendLine($"IntPtr {statementPointerFieldName} = IntPtr.Zero;");

        AppendDisposeStatement(statementPointerFieldName);

        builder.AppendLine($"public long LastInsertRowId");
        builder.AppendLine($"{{");
        builder.AppendLine($"    get");
        builder.AppendLine($"    {{");

        builder.AppendLine($"        if ({statementPointerFieldName} == IntPtr.Zero)");
        builder.AppendLine($"        {{");
        builder.AppendLine($"            var prepareResult = SqliteNativeMethods.sqlite3_prepare_v2(_dbHandle, {getSqlBytesFieldName}, {getSqlBytesFieldName}.Length, out {statementPointerFieldName}, IntPtr.Zero);");
        builder.AppendLine($"            if (prepareResult != Result.Ok)");
        builder.AppendLine($"            {{");
        builder.AppendLine($"                throw new SqliteException($\"Failed to prepare sqlite statement {query}\", prepareResult);");
        builder.AppendLine($"            }}");
        builder.AppendLine($"        }}");
        builder.AppendLine();

        builder.AppendLine($"        var result = SqliteNativeMethods.sqlite3_step({statementPointerFieldName});");
        builder.AppendLine();

        builder.AppendLine($"        try");
        builder.AppendLine($"        {{");
        builder.IncreaseIndent();

        builder.AppendLine($"        if (result == Result.Row)");
        builder.AppendLine($"        {{");
        builder.AppendLine();

        //read last_insert_rowid();
        builder.AppendLine($"            return SqliteNativeMethods.sqlite3_column_int64({statementPointerFieldName}, 0);");

        builder.AppendLine($"        }}");
        builder.AppendLine($"        else");
        builder.AppendLine($"        {{");
        builder.AppendLine($"            throw new SqliteException($\"Failed to run query {query}\", result);");
        builder.AppendLine($"        }}");

        builder.AppendLine();

        builder.DecreaseIndent();
        builder.AppendLine($"        }}");
        builder.AppendLine($"        finally");
        builder.AppendLine($"        {{");
        builder.AppendLine($"            // reset the statement so it's ready for next time");
        builder.AppendLine($"            result = SqliteNativeMethods.sqlite3_reset({statementPointerFieldName});");
        builder.AppendLine($"            if (result != Result.Ok)");
        builder.AppendLine($"            {{");
        builder.AppendLine($"                 throw new SqliteException($\"Failed to reset sqlite statement {query}\", result);");
        builder.AppendLine($"            }}");
        builder.AppendLine($"        }}");
        builder.AppendLine($"    }}");
        builder.AppendLine($"}}");
    }

    void GenerateGet(Table table, List<Column> columns, SourceBuilder builder)
    {
        var queryBuilder = new StringBuilder();
        queryBuilder.Append($"SELECT * FROM {table.SqlName} WHERE ");
        bool isFirst = true;
        foreach (var column in columns)
        {
            if (!isFirst)
            {
                queryBuilder.Append("AND ");
            }
            queryBuilder.Append($"{column.SqlName} == ? ");
            isFirst = false;
        }
        queryBuilder.Append(";");

        var query = queryBuilder.ToString();
        string getSqlBytesFieldName = $"_get{table.CSharpName}Bytes{columns.Count()}";
        AppendQueryBytesField(builder, getSqlBytesFieldName, query);

        string statementPointerFieldName = $"_get{table.CSharpName}Statement{columns.Count()}";

        builder.AppendLine($"IntPtr {statementPointerFieldName} = IntPtr.Zero;");

        AppendDisposeStatement(statementPointerFieldName);

        builder.AppendLine();

        builder.AppendStart($"public bool Get{table.CSharpName}({table.CSharpName} row, ");

        isFirst = true;
        foreach (var column in columns)
        {
            if (!isFirst)
            {
                builder.Append(" ,");
            }
            builder.Append($"{column.CSharpType} {column.CSharpParameterName}");
            isFirst = false;
        }
        builder.Append(")");
        builder.AppendLine();

        builder.AppendLine("{");
        builder.AppendLine($"    if ({statementPointerFieldName} == IntPtr.Zero)");
        builder.AppendLine("    {");
        builder.AppendLine($"        var prepareResult = SqliteNativeMethods.sqlite3_prepare_v2(_dbHandle, {getSqlBytesFieldName}, {getSqlBytesFieldName}.Length, out {statementPointerFieldName}, IntPtr.Zero);");
        builder.AppendLine("        if (prepareResult != Result.Ok)");
        builder.AppendLine("        {");
        builder.AppendLine($"            throw new SqliteException($\"Failed to prepare sqlite statement {query}\", prepareResult);");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();

        int bindIndex = 1;
        foreach (var column in columns)
        {
            BindValue(column, builder, statementPointerFieldName, bindIndex, column.CSharpParameterName, false);
            bindIndex++;
        }

        builder.AppendLine($"    var result = SqliteNativeMethods.sqlite3_step({statementPointerFieldName});");
        builder.AppendLine();

        builder.AppendLine("    try");
        builder.AppendLine("    {");
        builder.IncreaseIndent();

        builder.AppendLine("    if (result == Result.Row)");
        builder.AppendLine("    {");
        builder.AppendLine();

        GenerateReadRow(table, builder, statementPointerFieldName);

        builder.AppendLine("        return true;");

        builder.AppendLine("    }");
        builder.AppendLine("    else if(result == Result.Done)");
        builder.AppendLine("    {");
        builder.AppendLine($"        return false;");
        builder.AppendLine("    }");
        builder.AppendLine("    else");
        builder.AppendLine("    {");
        builder.AppendLine($"        throw new SqliteException($\"Failed to run query {query}\", result);");
        builder.AppendLine("    }");

        builder.AppendLine();

        builder.DecreaseIndent();
        builder.AppendLine("    }");
        builder.AppendLine("    finally");
        builder.AppendLine("    {");
        builder.AppendLine($"        // reset the statement so it's ready for next time");
        builder.AppendLine($"        result = SqliteNativeMethods.sqlite3_reset({statementPointerFieldName});");
        builder.AppendLine($"        if (result != Result.Ok)");
        builder.AppendLine($"        {{");
        builder.AppendLine($"            throw new SqliteException($\"Failed to reset sqlite statement {query}\", result);");
        builder.AppendLine($"        }}");
        builder.AppendLine($"    }}");
        builder.AppendLine($"}}");
        builder.AppendLine();
    }

    void GenerateReadRow(Table table, SourceBuilder builder, string statementPointerFieldName)
    {
        int columnIndex = 0;
        foreach (var column in table.Columns)
        {
            if (!column.NotNull && column.TypeAffinity != TypeAffinity.BLOB && column.TypeAffinity != TypeAffinity.TEXT)
            {
                builder.AppendLine($"        if(SqliteNativeMethods.sqlite3_column_type({statementPointerFieldName}, {columnIndex}) == SqliteDataType.Null)");
                builder.AppendLine($"        {{");
                builder.AppendLine($"            row!.{column.CSharpName} = null;");
                builder.AppendLine($"        }}");
                builder.AppendLine($"        else");
                builder.AppendLine($"        {{");
                builder.IncreaseIndent();
            }
            switch (column.TypeAffinity)
            {
                case TypeAffinity.TEXT:
                    string textPtrName = $"textPtr{GetUniqueNumber()}";
                    builder.AppendLine($"        IntPtr {textPtrName} = SqliteNativeMethods.sqlite3_column_text16({statementPointerFieldName}, {columnIndex});");
                    if (!column.NotNull)
                    {
                        builder.AppendLine($"        if({textPtrName} == IntPtr.Zero)");
                        builder.AppendLine($"        {{");
                        builder.AppendLine($"            row!.{column.CSharpName} = null;");
                        builder.AppendLine($"        }}");
                        builder.AppendLine($"        else");
                        builder.AppendLine($"        {{");
                        builder.IncreaseIndent();
                    }
                    builder.AppendLine($"        row!.{column.CSharpName} = Marshal.PtrToStringUni({textPtrName})!;");
                    if (!column.NotNull)
                    {
                        builder.DecreaseIndent();
                        builder.AppendLine($"        }}");
                    }
                    break;
                case TypeAffinity.INTEGER:
                    builder.AppendLine($"        row!.{column.CSharpName} = SqliteNativeMethods.sqlite3_column_int64({statementPointerFieldName}, {columnIndex});");
                    break;
                case TypeAffinity.REAL:
                    builder.AppendLine($"        row!.{column.CSharpName} = SqliteNativeMethods.sqlite3_column_double({statementPointerFieldName}, {columnIndex});");
                    break;
                case TypeAffinity.BLOB:
                    string blobPtrName = $"textPtr{GetUniqueNumber()}";

                    builder.AppendLine($"        IntPtr {blobPtrName} = SqliteNativeMethods.sqlite3_column_blob({statementPointerFieldName}, {columnIndex});");

                    if (!column.NotNull)
                    {
                        builder.AppendLine($"        if({blobPtrName} == IntPtr.Zero)");
                        builder.AppendLine($"        {{");
                        builder.AppendLine($"            row!.{column.CSharpName} = null;");
                        builder.AppendLine($"        }}");
                        builder.AppendLine($"        else");
                        builder.AppendLine($"        {{");
                        builder.IncreaseIndent();
                    }
                    builder.AppendLine($"        int length = SqliteNativeMethods.sqlite3_column_bytes({statementPointerFieldName}, {columnIndex});");
                    var separator = column.NotNull ? "." : "?.";
                    builder.AppendLine($"        if(row!.{column.CSharpName}{separator}Length != length)");
                    builder.AppendLine($"        {{");
                    builder.AppendLine($"            row!.{column.CSharpName} = new byte[length];");
                    builder.AppendLine($"        }}");
                    builder.AppendLine($"        Marshal.Copy({blobPtrName}, row!.{column.CSharpName}, 0, length);");

                    if (!column.NotNull)
                    {
                        builder.DecreaseIndent();
                        builder.AppendLine($"        }}");
                    }
                    break;
                case TypeAffinity.NUMERIC:
                    builder.AppendLine($"        row!.{column.CSharpName} = SqliteNumericSqliteMethods.Read({statementPointerFieldName}, {columnIndex});");
                    break;
            }
            if (!column.NotNull && column.TypeAffinity != TypeAffinity.BLOB && column.TypeAffinity != TypeAffinity.TEXT)
            {
                builder.DecreaseIndent();
                builder.AppendLine($"        }}");
            }
            columnIndex++;
        }
    }

    void GenerateBeginTransaction(SourceBuilder builder)
    {
        GenerateStatement(builder, "BEGIN", "beginTransaction", "BeginTransaction", "begin transaction");
    }

    void GenerateCommitTransaction(SourceBuilder builder)
    {
        GenerateStatement(builder, "COMMIT", "commitTransaction", "CommitTransaction", "commit transaction");
    }

    void GenerateRollbackTransaction(SourceBuilder builder)
    {
        GenerateStatement(builder, "ROLLBACK", "rollbackTransaction", "RollbackTransaction", "rollback transaction");
    }

    void GenerateStatement(SourceBuilder builder, string query, string codeName, string methodName, string description)
    {
        string beginTransactionBytesFieldName = $"_{codeName}Bytes";
        AppendQueryBytesField(builder, beginTransactionBytesFieldName, query);

        string statementPointerFieldName = $"_{codeName}Statement";
        builder.AppendLine($"IntPtr {statementPointerFieldName} = IntPtr.Zero;");
        AppendDisposeStatement(statementPointerFieldName);

        builder.AppendLine();


        builder.AppendLine($"public void {methodName}()");
        builder.AppendLine("{");
        builder.AppendLine($"    if ({statementPointerFieldName} == IntPtr.Zero)");
        builder.AppendLine("    {");
        builder.AppendLine($"        var prepareResult = SqliteNativeMethods.sqlite3_prepare_v2(_dbHandle, {beginTransactionBytesFieldName}, {beginTransactionBytesFieldName}.Length, out {statementPointerFieldName}, IntPtr.Zero);");
        builder.AppendLine("        if (prepareResult != Result.Ok)");
        builder.AppendLine("        {");
        builder.AppendLine($"            throw new SqliteException($\"Failed to prepare sqlite statement {query}\", prepareResult);");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine($"    var result = SqliteNativeMethods.sqlite3_step({statementPointerFieldName});");
        builder.AppendLine($"    if(result != Result.Done)");
        builder.AppendLine($"    {{");
        builder.AppendLine($"       throw new SqliteException(\"Failed to begin {description}\", result);");
        builder.AppendLine($"    }}");
        builder.AppendLine($"}}");
    }

    void GenerateGetAll(Table table, SourceBuilder builder)
    {
        var query = $"SELECT * FROM {table.SqlName};";
        string getAllSqlBytesFieldName = $"_queryAll{table.CSharpName}sBytes";
        AppendQueryBytesField(builder, getAllSqlBytesFieldName, query);

        string statementPointerFieldName = $"_query{table.CSharpName}Statement";

        builder.AppendLine($"IntPtr {statementPointerFieldName} = IntPtr.Zero;");

        AppendDisposeStatement(statementPointerFieldName);

        builder.AppendLine();

        builder.AppendLine($"public void All{table.CSharpName}s(List<{table.CSharpName}> list)");
        builder.AppendLine("{");
        builder.AppendLine($"    if ({statementPointerFieldName} == IntPtr.Zero)");
        builder.AppendLine("    {");
        builder.AppendLine($"        var prepareResult = SqliteNativeMethods.sqlite3_prepare_v2(_dbHandle, {getAllSqlBytesFieldName}, {getAllSqlBytesFieldName}.Length, out {statementPointerFieldName}, IntPtr.Zero);");
        builder.AppendLine("        if (prepareResult != Result.Ok)");
        builder.AppendLine("        {");
        builder.AppendLine($"            throw new SqliteException($\"Failed to prepare sqlite statement {query}\", prepareResult);");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine($"    var result = SqliteNativeMethods.sqlite3_step({statementPointerFieldName});");

        builder.AppendLine("    try");
        builder.AppendLine("    {");
        builder.IncreaseIndent();

        builder.AppendLine();
        builder.AppendLine("    int index = 0;");
        builder.AppendLine("    while (result == Result.Row)");
        builder.AppendLine("    {");
        builder.AppendLine($"        {table.CSharpName}? row = null;");
        builder.AppendLine("        if (index >= list.Count)");
        builder.AppendLine("        {");
        builder.AppendLine($"            row = new {table.CSharpName}();");
        builder.AppendLine("            list.Add(row);");
        builder.AppendLine("        }");
        builder.AppendLine("        else");
        builder.AppendLine("        {");
        builder.AppendLine("            row = list[index];");
        builder.AppendLine("        }");
        builder.AppendLine();

        GenerateReadRow(table, builder, statementPointerFieldName);

        builder.AppendLine($"        result = SqliteNativeMethods.sqlite3_step({statementPointerFieldName});");
        builder.AppendLine($"        index++;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    if (result != Result.Done)");
        builder.AppendLine("    {");
        builder.AppendLine($"        throw new SqliteException($\"Failed to execute sqlite statement {query}\", result);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.DecreaseIndent();
        builder.AppendLine("    }");
        builder.AppendLine("    finally");
        builder.AppendLine("    {");
        builder.AppendLine($"        // reset the statement so it's ready for next time");
        builder.AppendLine($"        result = SqliteNativeMethods.sqlite3_reset({statementPointerFieldName});");
        builder.AppendLine($"        if (result != Result.Ok)");
        builder.AppendLine($"        {{");
        builder.AppendLine($"            throw new SqliteException($\"Failed to reset sqlite statement {query}\", result);");
        builder.AppendLine($"        }}");
        builder.AppendLine($"    }}");
        builder.AppendLine($"}}");
        builder.AppendLine();
    }

    string BuildInsertQuery(Table table)
    {
        StringBuilder queryBuilder = new();
        queryBuilder.Append($"INSERT INTO {table.SqlName}(");

        bool isFirst = true;
        foreach (var column in table.Columns)
        {
            if (!isFirst)
            {
                queryBuilder.Append(",");
            }
            else
            {
                isFirst = false;
            }
            queryBuilder.Append(column.SqlName);
        }
        var valuesQuestionMarks = string.Join(",", table.Columns.Select(c => "?"));
        queryBuilder.Append($") VALUES({valuesQuestionMarks});");
        return queryBuilder.ToString();
    }

    void AppendDisposeStatement(string statementPointerFieldName)
    {
        _disposeCode.Add(disposeBuilder =>
        {
            disposeBuilder.AppendLine($"if({statementPointerFieldName} != IntPtr.Zero)");
            disposeBuilder.AppendLine("{");
            disposeBuilder.AppendLine($"    SqliteNativeMethods.sqlite3_finalize({statementPointerFieldName});");
            disposeBuilder.AppendLine($"    {statementPointerFieldName} = IntPtr.Zero;");
            disposeBuilder.AppendLine("}");
        });
    }

    public void GenerateInsert(Table table, SourceBuilder builder)
    {
        string insertQuery = BuildInsertQuery(table);
        string insertSqlBytesFieldName = $"_insert{table.CSharpName}Bytes";
        AppendQueryBytesField(builder, insertSqlBytesFieldName, insertQuery);

        string statementPointerFieldName = $"_insert{table.CSharpName}StatementPtr";

        AppendDisposeStatement(statementPointerFieldName);

        builder.AppendLine($"IntPtr {statementPointerFieldName} = IntPtr.Zero;");
        builder.AppendLine();

        builder.AppendLine($"public void Insert{table.CSharpName}({table.CSharpName} row)");
        builder.AppendLine("{");
        builder.AppendLine($"    if ({statementPointerFieldName} == IntPtr.Zero)");
        builder.AppendLine($"    {{");
        builder.AppendLine($"        var prepareResult = SqliteNativeMethods.sqlite3_prepare_v2(_dbHandle, {insertSqlBytesFieldName}, {insertSqlBytesFieldName}.Length, out {statementPointerFieldName}, IntPtr.Zero);");

        builder.AppendLine($"        if (prepareResult != Result.Ok)");
        builder.AppendLine("        {");
        builder.AppendLine($"            throw new SqliteException($\"Failed to prepare statement to insert {table.SqlName}\", prepareResult);");
        builder.AppendLine("        }");
        builder.AppendLine("    }");

        builder.AppendLine();

        int columnParameterNumber = 1;
        foreach (var column in table.Columns)
        {
            BindValue(column, builder, statementPointerFieldName, columnParameterNumber, $"row.{column.CSharpName}", true);
            columnParameterNumber++;
        }

        builder.AppendLine($"    var result = SqliteNativeMethods.sqlite3_step({statementPointerFieldName});");

        builder.AppendLine($"    SqliteNativeMethods.sqlite3_reset({statementPointerFieldName});");

        var autoIncrementColumn = table.Columns.Where(column => column.AutoIncrement).FirstOrDefault();
        if (autoIncrementColumn != null)
        {
            builder.AppendLine($"   row.{autoIncrementColumn.CSharpName} = LastInsertRowId;");
        }

        builder.AppendLine("    if (result != Result.Done)");
        builder.AppendLine("    {");
        builder.AppendLine($"        throw new SqliteException($\"Failed to insert {table.SqlName}\", result);");
        builder.AppendLine("    }");

        builder.AppendLine();

        builder.AppendLine("}");
        builder.AppendLine();
    }

    void BindValue(Column column, SourceBuilder builder, string statementPointerFieldName, int columnParameterNumber, string valueGetter, bool isInsert)
    {
        string extraValueAccessor = "";
        if (!column.NotNull)
        {
            builder.AppendLine($"    if({valueGetter} == null)");
            builder.AppendLine("    {");
            builder.AppendLine($"        SqliteNativeMethods.sqlite3_bind_null({statementPointerFieldName}, {columnParameterNumber});");
            builder.AppendLine("    }");
            builder.AppendLine("    else");
            builder.AppendLine("    {");
            builder.IncreaseIndent();
            extraValueAccessor = ".Value";
        }
        switch (column.TypeAffinity)
        {
            case TypeAffinity.TEXT:
                builder.AppendLine($"    SqliteNativeMethods.sqlite3_bind_text16({statementPointerFieldName}, {columnParameterNumber}, {valueGetter}, -1, SqliteNativeMethods.SQLITE_TRANSIENT);");
                break;
            case TypeAffinity.INTEGER:
                if (column.AutoIncrement && isInsert)
                {
                    builder.AppendLine($"    if({valueGetter}{extraValueAccessor} != 0)");
                    builder.AppendLine($"    {{");
                    builder.AppendLine($"        SqliteNativeMethods.sqlite3_bind_null({statementPointerFieldName}, {columnParameterNumber});");
                    builder.AppendLine($"    }}");
                    builder.AppendLine($"    else");
                    builder.AppendLine($"    {{");
                    builder.IncreaseIndent();
                }
                builder.AppendLine($"    SqliteNativeMethods.sqlite3_bind_int64({statementPointerFieldName}, {columnParameterNumber}, {valueGetter}{extraValueAccessor});");
                if (column.AutoIncrement && isInsert)
                {
                    builder.DecreaseIndent();
                    builder.AppendLine($"    }};");
                }
                break;
            case TypeAffinity.REAL:
                builder.AppendLine($"    SqliteNativeMethods.sqlite3_bind_double({statementPointerFieldName}, {columnParameterNumber}, {valueGetter}{extraValueAccessor});");
                break;
            case TypeAffinity.BLOB:
                builder.AppendLine($"    SqliteNativeMethods.sqlite3_bind_blob({statementPointerFieldName}, {columnParameterNumber}, {valueGetter}, {valueGetter}.Length, SqliteNativeMethods.SQLITE_TRANSIENT);");
                break;
            case TypeAffinity.NUMERIC:
                builder.AppendLine($"    SqliteNumericSqliteMethods.Write({statementPointerFieldName}, {columnParameterNumber}, {valueGetter}{extraValueAccessor});");
                break;
        }
        if (!column.NotNull)
        {
            builder.DecreaseIndent();
            builder.AppendLine("    }");
        }
    }
}