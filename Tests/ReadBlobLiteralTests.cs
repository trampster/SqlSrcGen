using SqlSrcGen.Generator;

namespace Tests;

public class ReadBlobLiteralTests
{
    [TestCase("x'1234'")]
    [TestCase("X'1234'")]
    [TestCase("X'ABCDEF1234567890'")]
    public void ReadBlobLiteral_Valid_ParsedCorrectly(string literal)
    {
        // arrange
        var generator = new Tokenizer();
        int position = 0;
        int lineIndex = 1;
        int characterInLineIndex = 0;

        // act
        var after = generator.ReadBlobLiteral(literal + " ", out Token token, ref position, ref lineIndex, ref characterInLineIndex);

        // assert
        Assert.That(after.ToString(), Is.EqualTo(" "));
        Assert.That(token.Value, Is.EqualTo(literal));
        Assert.That(token.Line, Is.EqualTo(1));
        Assert.That(token.CharacterInLine, Is.EqualTo(0));
        Assert.That(token.Position, Is.EqualTo(0));
        Assert.That(token.TokenType, Is.EqualTo(TokenType.BlobLiteral));

        Assert.That(position, Is.EqualTo(literal.Length));
        Assert.That(lineIndex, Is.EqualTo(1));
        Assert.That(characterInLineIndex, Is.EqualTo(literal.Length));
    }

    [TestCase("", "Ran out of text parsing blob literal", 0)]
    [TestCase("'", "Blob literal must start with a 'x' or 'X'", 0)]
    [TestCase("x1234", "Second character in a blob literal must be a single quote", 1)]
    [TestCase("x'AB3'", "Blob literals must have an even number of hex digits", 5)]
    [TestCase("x'G'", "Invalid charactor in blob literal", 2)]
    [TestCase("x'ABCD", "Invalid charactor in blob literal", 6)]
    public void ReadBlobLiteral_Invalid_InvalidSqlException(string literal, string exceptionMessage, int charactorInLine)
    {
        // arrange
        var generator = new Tokenizer();
        int position = 0;
        int lineIndex = 1;
        int characterInLineIndex = 0;

        // act
        try
        {
            var after = generator.ReadBlobLiteral(literal + " ", out Token token, ref position, ref lineIndex, ref characterInLineIndex);
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            Assert.That(exception.Message, Is.EqualTo(exceptionMessage));
            Assert.That(exception.Token.CharacterInLine, Is.EqualTo(charactorInLine));
            Assert.That(exception.Token.Position, Is.EqualTo(charactorInLine));
        }
    }
}