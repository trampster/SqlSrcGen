using System;
using System.Collections.Generic;

namespace SqlSrcGen.Generator;

public enum QueryType
{
    Select,
    Insert,
    Delete
}

public class QueryInfo
{
    public string MethodName { get; set; }
    public string QueryString { get; set; }
    public string CSharpName { get; set; }
    public QueryType QueryType { get; set; }

    List<Func<List<(string, Table)>, List<Column>>> _columnGenerators = new();
    public void AddColumnsGenerator(Func<List<(string, Table)>, List<Column>> generator)
    {
        _columnGenerators.Add(generator);
    }

    public List<Column> Columns
    {
        get;
        set;
    } = new List<Column>();

    readonly List<(string, Table)> _fromTables = new();
    public List<(string, Table)> FromTables => _fromTables;

    public void Process()
    {
        foreach (var generator in _columnGenerators)
        {
            Columns.AddRange(generator(_fromTables));
        }
    }
}