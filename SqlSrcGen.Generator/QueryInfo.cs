using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
    public string CSharpResultType { get; set; }
    public string CSharpInputType { get; set; }
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
            foreach (var column in generator(_fromTables))
            {
                Columns.Add(column);
            }
        }

        //Fixup duplicate csharnames
        for (int index = 0; index < Columns.Count; index++)
        {
            var column = Columns[index];
            var cSharpName = column.CSharpName;
            for (int otherColumnIndex = 0; otherColumnIndex < Columns.Count; otherColumnIndex++)
            {
                if (index == otherColumnIndex)
                {
                    continue;
                }
                var otherColumn = Columns[otherColumnIndex];

                if (otherColumn.CSharpName == cSharpName &&
                    otherColumn.Table != column.Table)
                {
                    Columns[otherColumnIndex] = otherColumn with { CSharpName = $"{otherColumn.Table.CSharpName}{otherColumn.CSharpName}" };
                    Columns[index] = column with { CSharpName = $"{column.Table.CSharpName}{cSharpName}" };
                }
            }
        }
    }
}