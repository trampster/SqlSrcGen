namespace SqlSrcGen.Runtime;

public class SqliteException : IOException
{
    readonly Result _result;

    public SqliteException(string message, Result result) : base(string.Format($"{message} with Result {result}"))
    {
        _result = result;
    }

    public Result Result => _result;
}