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

            var matchingIndexes = GetColumnsWithSameCSharpName(cSharpName, index);
            if (matchingIndexes.Any())
            {
                var otherTablesIndexes = matchingIndexes
                    .Where(matchingIndex => Columns[matchingIndex].Table != column.Table);
                if (otherTablesIndexes.Any())
                {
                    //need to add tableName to columns
                    foreach (int matchingIndex in matchingIndexes)
                    {
                        var matchingColumn = Columns[matchingIndex];
                        if (matchingColumn.CSharpName != cSharpName)
                        {
                            //already renamed as RenameColumn renames all duplicate columns from the same table
                            continue;
                        }
                        RenameColumn(matchingIndex, $"{matchingColumn.Table.CSharpName}{matchingColumn.CSharpName}");
                    }
                    RenameColumn(index, $"{column.Table.CSharpName}{column.CSharpName}");
                }
            }
        }
    }

    IEnumerable<int> GetColumnsWithSameCSharpName(string csharpName, int excludeIndex)
    {
        for (int index = 0; index < Columns.Count; index++)
        {
            if (index == excludeIndex)
            {
                continue;
            }
            var column = Columns[index];
            if (column.CSharpName == csharpName)
            {
                yield return index;
            }
        }
    }

    void RenameColumn(int index, string newName)
    {
        string nameToTry = newName;
        int attempt = 1;
        var column = Columns[index];

        while (GetColumnsWithSameCSharpName(nameToTry, index).Any())
        {
            nameToTry = $"{newName}{attempt}";
        }
        // rename all columns in with the same table and sql name, (you can select the same column multiple times)
        for (int columnIndex = 0; columnIndex < Columns.Count; columnIndex++)
        {
            if (Columns[columnIndex].Table == column.Table && Columns[columnIndex].SqlName == column.SqlName)
            {
                Columns[columnIndex] = column with { CSharpName = nameToTry };
            }
        }
    }
}