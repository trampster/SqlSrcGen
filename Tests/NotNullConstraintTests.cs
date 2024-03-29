using Moq;
using SqlSrcGen.Generator;

namespace Tests;

public class NotNullConstraintTests
{
    [Test]
    public void ProcessSqlSchema_TextColumnNotNull_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema("CREATE TABLE contact (name Text not null);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        var columns = databaseInfo.Tables[0].Columns.ToArray();
        Assert.That(columns[0].SqlName, Is.EqualTo("name"));
        Assert.That(columns[0].CSharpName, Is.EqualTo("Name"));
        Assert.That(columns[0].SqlType, Is.EqualTo("Text"));
        Assert.That(columns[0].CSharpType, Is.EqualTo("string"));
        Assert.That(columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.TEXT));
        Assert.That(columns[0].NotNull, Is.True);
    }


    [Test]
    public void ProcessSqlSchema_IntegerColumnNotNull_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema("CREATE TABLE contact (age Integer NOT NULL);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        var columns = databaseInfo.Tables[0].Columns.ToArray();
        Assert.That(columns[0].SqlName, Is.EqualTo("age"));
        Assert.That(columns[0].CSharpName, Is.EqualTo("Age"));
        Assert.That(columns[0].SqlType, Is.EqualTo("Integer"));
        Assert.That(columns[0].CSharpType, Is.EqualTo("long"));
        Assert.That(columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.INTEGER));
    }

    [Test]
    public void ProcessSqlSchema_RealColumnNotNull_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema("CREATE TABLE contact (height Real Not Null);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        var columns = databaseInfo.Tables[0].Columns.ToArray();
        Assert.That(columns[0].SqlName, Is.EqualTo("height"));
        Assert.That(columns[0].CSharpName, Is.EqualTo("Height"));
        Assert.That(columns[0].SqlType, Is.EqualTo("Real"));
        Assert.That(columns[0].CSharpType, Is.EqualTo("double"));
        Assert.That(columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.REAL));
    }

    [Test]
    public void ProcessSqlSchema_BlobColumnNotNull_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema("CREATE TABLE contact (key Blob not null);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        var columns = databaseInfo.Tables[0].Columns.ToArray();
        Assert.That(columns[0].SqlName, Is.EqualTo("key"));
        Assert.That(columns[0].CSharpName, Is.EqualTo("Key"));
        Assert.That(columns[0].SqlType, Is.EqualTo("Blob"));
        Assert.That(columns[0].CSharpType, Is.EqualTo("byte[]"));
        Assert.That(columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.BLOB));
    }

    [Test]
    public void ProcessSqlSchema_NumericColumnNotNull_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema("CREATE TABLE contact (distance Numeric NOT NULL);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        var columns = databaseInfo.Tables[0].Columns.ToArray();
        Assert.That(columns[0].SqlName, Is.EqualTo("distance"));
        Assert.That(columns[0].CSharpName, Is.EqualTo("Distance"));
        Assert.That(columns[0].SqlType, Is.EqualTo("Numeric"));
        Assert.That(columns[0].CSharpType, Is.EqualTo("Numeric"));
        Assert.That(columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.NUMERIC));
    }

    [Test]
    public void ProcessSqlSchema_InvalidConstraintNot_ThrowsInvalidSqlException()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        try
        {
            // act
            generator.ProcessSqlSchema("CREATE TABLE contact (distance Numeric NOT);", databaseInfo);

            // assert
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            Assert.That(exception.Message, Is.EqualTo("Invalid column constraint, did you mean 'not null'?"));
            Assert.That(exception.Token!.Line, Is.EqualTo(0));
            Assert.That(exception.Token.CharacterInLine, Is.EqualTo(39));
        }
    }

    [Test]
    public void ProcessSqlSchema_NotNulWithOnConflict_Parsed()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema("CREATE TABLE contact (distance Numeric NOT NULL ON CONFLICT ROLLBACK);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].Columns.First().NotNull, Is.True);
    }

}