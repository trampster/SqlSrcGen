using System.Text;
using SqlSrcGen.Generator;

namespace Tests;

public class SquareBacketNameTests
{
    [TestCase("[table]", "Table")]
    [TestCase("[two part]", "TwoPart")]
    [TestCase("[two  part]", "TwoPart")]
    public void ProcessSqlSchema_SquareBracketInTableName_CreatesTableInfo(string tableName, string cSharpName)
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema($"CREATE TABLE {tableName} (name Text);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo(tableName));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo(cSharpName));
        var columns = databaseInfo.Tables[0].Columns.ToArray();
        Assert.That(columns[0].SqlName, Is.EqualTo("name"));
        Assert.That(columns[0].CSharpName, Is.EqualTo("Name"));
        Assert.That(columns[0].SqlType, Is.EqualTo("Text"));
        Assert.That(columns[0].CSharpType, Is.EqualTo("string?"));
        Assert.That(columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.TEXT));
    }

    [Test]
    public void ProcessSqlSchema_TwoTablesMapToSameCSharpName_InvalidSqlException()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();
        var builder = new StringBuilder();
        builder.Append($"CREATE TABLE [one two] (name Text);");
        builder.Append("\n");
        builder.Append($"CREATE TABLE [one  two] (name Text);");

        // act
        try
        {
            generator.ProcessSqlSchema(builder.ToString(), databaseInfo);
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            Assert.That(exception.Message, Is.EqualTo("Table maps to same csharp class name as an existing table"));
            Assert.That(exception.Token.Line, Is.EqualTo(1));
            Assert.That(exception.Token.CharacterInLine, Is.EqualTo(13));
            Assert.That(exception.Token.Position, Is.EqualTo(49));
        }
    }

    [TestCase("[NUMBER]", "Number")]
    [TestCase("[two part]", "TwoPart")]
    [TestCase("[two  part]", "TwoPart")]
    public void ProcessSqlSchema_SquareBracketInColumnName_CreatesTableInfo(string columnName, string cSharpName)
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema($"CREATE TABLE test ({columnName} Text);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("test"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Test"));
        var columns = databaseInfo.Tables[0].Columns.ToArray();
        Assert.That(columns[0].SqlName, Is.EqualTo(columnName));
        Assert.That(columns[0].CSharpName, Is.EqualTo(cSharpName));
        Assert.That(columns[0].SqlType, Is.EqualTo("Text"));
        Assert.That(columns[0].CSharpType, Is.EqualTo("string?"));
        Assert.That(columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.TEXT));
    }

    [Test]
    public void ProcessSqlSchema_SquareBracketInColumnName_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        try
        {
            // act
            generator.ProcessSqlSchema($"CREATE TABLE test ([one two] Text, [one  two] Text);", databaseInfo);
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            Assert.That(exception.Message, Is.EqualTo("Column maps to same csharp name as an existing column"));
            Assert.That(exception.Token.Line, Is.EqualTo(0));
            Assert.That(exception.Token.CharacterInLine, Is.EqualTo(35));
            Assert.That(exception.Token.Position, Is.EqualTo(35));
        }
    }
}