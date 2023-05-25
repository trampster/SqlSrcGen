using System.Collections.Generic;

namespace SqlSrcGen
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
    }

    public class Table
    {
        public string SqlName { get; set; }
        public string CSharpName { get; set; }
        public List<Column> Columns
        {
            get;
            set;
        } = new List<Column>();

        public string CreateTable { get; set; } = "";
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