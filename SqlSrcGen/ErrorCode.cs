namespace SqlSrcGen;

public enum ErrorCode
{
    None,
    // Generic sql parsing error
    SSG0001,
    // custom collation type warning
    SSG0002,
    // COLLATE on non text field
    SSG0003
}