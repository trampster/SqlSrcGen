public enum TokenType
{
    StringLiteral,
    NumericLiteral,
    BlobLiteral,
    Other
}

public record Token
{
    public string Value { get; set; }
    public int Position { get; set; }
    // zero based line index
    public int Line { get; set; }
    // zero based index of character in line
    public int CharacterInLine { get; set; }
    public TokenType TokenType { get; set; } = TokenType.Other;
}
