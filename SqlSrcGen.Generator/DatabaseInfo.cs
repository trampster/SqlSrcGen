using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace SqlSrcGen.Generator
{
    public enum TypeAffinity
    {
        TEXT,
        NUMERIC,
        INTEGER,
        REAL,
        BLOB
    }

    public class Column
    {
        public string SqlName { get; set; }
        public string SqlType { get; set; }
        public string CSharpName { get; set; }
        public string CSharpType { get; set; }
        public TypeAffinity TypeAffinity { get; set; }
        public bool NotNull { get; set; }
        public bool PrimaryKey { get; set; }
        public bool AutoIncrement { get; set; }
        public bool Unique { get; set; }

        public string CSharpParameterName => char.ToLowerInvariant(CSharpName[0]) + CSharpName.Substring(1, CSharpName.Length - 1);

        public Table Table { get; set; }
    }

    public class Table
    {
        public string SqlName { get; set; }
        public string CSharpName { get; set; }

        readonly List<Column> _columns = new();
        public IEnumerable<Column> Columns => _columns;

        public void AddColumn(Column column)
        {
            column.Table = this;
            _columns.Add(column);
        }

        public string CreateTable { get; set; } = "";
        public bool Tempory { get; set; }

        public List<Column> PrimaryKey
        {
            get;
            set;
        } = new List<Column>();

        public List<List<Column>> Unique
        {
            get;
            set;
        } = new List<List<Column>>();

        public bool IsUniqueBy(List<string> columns)
        {
            if (columns.Count == 1)
            {
                return Columns.Where(column => column.SqlName.ToLowerInvariant() == columns[0] && (column.PrimaryKey || column.Unique)).Any();
            }
            foreach (var uniqueColumns in Unique.Concat(new List<List<Column>> { PrimaryKey }))
            {
                if (columns.Count != uniqueColumns.Count)
                {
                    continue;
                }
                foreach (var column in columns)
                {
                    // check that this column exists

                    var lowerName = column.ToLowerInvariant();

                    if (!uniqueColumns.Any(column => column.SqlName.ToLowerInvariant() == lowerName))
                    {
                        continue;
                    }
                }
                return true;
            }
            return false;
        }
    }

    public class DatabaseInfo
    {
        public List<Table> Tables
        {
            get;
            set;
        } = new List<Table>();
    }
}