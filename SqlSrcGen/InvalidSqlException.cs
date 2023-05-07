using System;

namespace SqlSrcGen;

public class InvalidSqlException : FormatException
{
    public InvalidSqlException(string message) : base(message)
    {
    }
}