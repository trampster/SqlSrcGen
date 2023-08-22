namespace SqlSrcGen.Generator;

public enum ErrorCode
{
    None,
    // Generic sql parsing error
    SSG0001,
    // custom collation type warning
    SSG0002,
    // COLLATE on non text field
    SSG0003,
    // MATCH on references constrain is not supported in sqlite, it's parsed but has no effect
    SSG0004,
    // Not supported
    SSG0005
}