using SqlSrcGen.Generator;

namespace Tests;

public class ReadNumericLiteralTests
{
    [TestCase("1234")]
    [TestCase("1234.6543")]
    [TestCase("1234.")]
    [TestCase(".1234")]
    [TestCase("1234e12")]
    [TestCase("1234e+12")]
    [TestCase("1234e-12")]
    [TestCase("0xABCEDF0123456789")]
    public void ParseNumericLiteral_Valid_ParsedCorrectly(string literal)
    {
        // arrange
        var generator = new Tokenizer();
        int position = 0;
        int lineIndex = 1;
        int characterInLineIndex = 0;

        // act
        var after = generator.ParseNumericLiteral(literal + " ", out Token token, ref position, ref lineIndex, ref characterInLineIndex);

        // assert
        Assert.That(after.ToString(), Is.EqualTo(" "));
        Assert.That(token.Value, Is.EqualTo(literal));
        Assert.That(token.Line, Is.EqualTo(1));
        Assert.That(token.CharacterInLine, Is.EqualTo(0));
        Assert.That(token.Position, Is.EqualTo(0));
        Assert.That(token.TokenType, Is.EqualTo(TokenType.NumericLiteral));

        Assert.That(position, Is.EqualTo(literal.Length));
        Assert.That(lineIndex, Is.EqualTo(1));
        Assert.That(characterInLineIndex, Is.EqualTo(literal.Length));
    }
}