public enum TokenType
{
    StringLiteral,
    NumericLiteral,
    BlobLiteral,
    Operator,
    Other
}

public record Token
{
    public Token()
    {
    }

    public Token(string value, int position, int line, int characterInLine, TokenType tokenType)
    {
        Value = value;
        Position = position;
        Line = line;
        CharacterInLine = characterInLine;
        TokenType = tokenType;
    }

    public string Value { get; set; }
    public int Position { get; set; }
    // zero based line index
    public int Line { get; set; }
    // zero based index of character in line
    public int CharacterInLine { get; set; }
    public TokenType TokenType { get; set; } = TokenType.Other;

    public bool BinaryOperator
    {
        get;
        set;
    }

    public bool UnaryOperator
    {
        get;
        set;
    }
}
