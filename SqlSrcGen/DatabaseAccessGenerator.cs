using System.Text;
using System.Collections.Generic;
using System;
using System.Linq;

namespace SqlSrcGen;

public class DatabaseAccessGenerator
{
    public void GenerateUsings(SourceBuilder builder)
    {
        builder.AppendLine("using System.Runtime.InteropServices;");
        builder.AppendLine("using System.Text;");
        builder.AppendLine("using SqlSrcGen.Runtime;");
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

        foreach (var table in databaseInfo.Tables)
        {
            GenerateCreateTable(table, builder);
            GenerateGetAll(table, builder);
            GenerateInsert(table, builder);
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

        int columnIndex = 0;
        foreach (var column in table.Columns)
        {
            switch (column.TypeAffinity)
            {
                case TypeAffinity.TEXT:
                    builder.AppendLine($"        row!.{column.CSharpName} = Marshal.PtrToStringUni(SqliteNativeMethods.sqlite3_column_text16({statementPointerFieldName}, {columnIndex}))!;");
                    break;
                case TypeAffinity.INTEGER:
                    builder.AppendLine($"        row!.{column.CSharpName} = SqliteNativeMethods.sqlite3_column_int64({statementPointerFieldName}, {columnIndex});");
                    break;
            }
            columnIndex++;
        }
        builder.AppendLine($"        result = SqliteNativeMethods.sqlite3_step({statementPointerFieldName});");
        builder.AppendLine($"        index++;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    if (result != Result.Done)");
        builder.AppendLine("    {");
        builder.AppendLine($"        throw new SqliteException($\"Failed to execute sqlite statement {query}\", result);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    // reset the statement so it's ready for next time");
        builder.AppendLine($"    result = SqliteNativeMethods.sqlite3_reset({statementPointerFieldName});");
        builder.AppendLine("    if (result != Result.Ok)");
        builder.AppendLine("    {");
        builder.AppendLine($"        throw new SqliteException($\"Failed to reset sqlite statement {query}\", result);");
        builder.AppendLine("    }");
        builder.AppendLine("}");
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
        builder.AppendLine("    {");
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
            switch (column.TypeAffinity)
            {
                case TypeAffinity.TEXT:
                    builder.AppendLine($"    SqliteNativeMethods.sqlite3_bind_text16({statementPointerFieldName}, {columnParameterNumber}, row.{column.CSharpName}, -1, SqliteNativeMethods.SQLITE_TRANSIENT);");
                    break;
                case TypeAffinity.INTEGER:
                    builder.AppendLine($"    SqliteNativeMethods.sqlite3_bind_int64({statementPointerFieldName}, {columnParameterNumber}, row.{column.CSharpName});");
                    break;
            }
            columnParameterNumber++;
        }

        builder.AppendLine($"    var result = SqliteNativeMethods.sqlite3_step({statementPointerFieldName});");

        builder.AppendLine($"    SqliteNativeMethods.sqlite3_reset({statementPointerFieldName});");

        builder.AppendLine("    if (result != Result.Done)");
        builder.AppendLine("    {");
        builder.AppendLine($"        throw new SqliteException($\"Failed to insert {table.SqlName}\", result);");
        builder.AppendLine("    }");

        builder.AppendLine();

        builder.AppendLine("}");
        builder.AppendLine();
    }
}