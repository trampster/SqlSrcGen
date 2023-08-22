using System.Text;

namespace SqlSrcGen.Generator;

public static class CSharp
{
    public static string ToCSharpName(string sqlName)
    {
        var builder = new StringBuilder();
        if (sqlName.StartsWith("[") && sqlName.EndsWith("]"))
        {
            sqlName = sqlName.Substring(1, sqlName.Length - 2);
        }
        bool startsLower = false;
        if (sqlName.Length > 0 && char.IsLower(sqlName[0]))
        {
            startsLower = true;
        }
        bool isFirst = true;
        for (int index = 0; index < sqlName.Length; index++)
        {
            var charactor = sqlName[index];
            if (charactor == '_')
            {
                isFirst = true;
                continue;
            }
            if (charactor == ' ')
            {
                isFirst = true;
                continue;
            }
            if (charactor == '\r')
            {
                isFirst = true;
                continue;
            }
            if (charactor == '\n')
            {
                isFirst = true;
                continue;
            }
            if (isFirst)
            {
                builder.Append(charactor.ToString().ToUpperInvariant()[0]);
                isFirst = false;
                continue;
            }

            builder.Append(startsLower ? charactor.ToString() : charactor.ToString().ToLowerInvariant());
        }
        var cSharpName = builder.ToString();
        if (IsKeyword(cSharpName))
        {
            return $"@{cSharpName}";
        }
        return cSharpName;
    }

    static bool IsKeyword(string word)
    {
        switch (word)
        {
            case "abstract":
            case "as":
            case "base":
            case "bool":
            case "break":
            case "byte":
            case "case":
            case "catch":
            case "char":
            case "checked":
            case "class":
            case "const":
            case "continue":
            case "decimal":
            case "default":
            case "delegate":
            case "do":
            case "double":
            case "else":
            case "enum":
            case "event":
            case "explicit":
            case "extern":
            case "false":
            case "finally":
            case "fixed":
            case "float":
            case "for":
            case "foreach":
            case "goto":
            case "if":
            case "implicit":
            case "in":
            case "int":
            case "interface":
            case "internal":
            case "is":
            case "lock":
            case "long":
            case "namespace":
            case "new":
            case "null":
            case "object":
            case "operator":
            case "out":
            case "override":
            case "params":
            case "private":
            case "protected":
            case "public":
            case "readonly":
            case "ref":
            case "return":
            case "sbyte":
            case "sealed":
            case "short":
            case "sizeof":
            case "stackalloc":
            case "static":
            case "string":
            case "struct":
            case "switch":
            case "this":
            case "throw":
            case "true":
            case "try":
            case "typeof":
            case "uint":
            case "ulong":
            case "unchecked":
            case "unsafe":
            case "ushort":
            case "using":
            case "virtual":
            case "void":
            case "volatile":
            case "while":
                return true;
            default:
                return false;
        }
    }
}
