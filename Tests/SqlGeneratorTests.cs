using SqlSrcGen;

namespace Tests;

public class SqlGeneratorTests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void GeneratesCorrectRecord()
    {
        // arrange 
        var generator = new SqlGenerator();
        var stringBuilder = new SourceBuilder();
        var databaseInfo = new DatabaseInfo();
        var table = new Table()
        {
            SqlName = "contact",
            CSharpName = "Contact",
        };
        table.Columns.Add(new Column() { SqlName = "name", CSharpName = "Name", SqlType = "Text", CSharpType = "string" });
        table.Columns.Add(new Column() { SqlName = "email", CSharpName = "Email", SqlType = "Text", CSharpType = "string" });
        databaseInfo.Tables.Add(table);

        string expectedSource =
            "public record Contact" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    public string Name { get; set; } = \"\";" + Environment.NewLine +
            "    public string Email { get; set; } = \"\";" + Environment.NewLine +
            "}" + Environment.NewLine;

        // act
        generator.GenerateDatabaseObjects(databaseInfo, stringBuilder);

        // assert
        var source = stringBuilder.ToString();
        Assert.That(source, Is.EqualTo(expectedSource));
    }

    [Test]
    public void ProcessSqlSchema_TextColumn_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema("CREATE TABLE contact (name Text, email Text);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlName, Is.EqualTo("name"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpName, Is.EqualTo("Name"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlType, Is.EqualTo("Text"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpType, Is.EqualTo("string"));
        Assert.That(databaseInfo.Tables[0].Columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.TEXT));
        Assert.That(databaseInfo.Tables[0].Columns[1].SqlName, Is.EqualTo("email"));
        Assert.That(databaseInfo.Tables[0].Columns[1].CSharpName, Is.EqualTo("Email"));
        Assert.That(databaseInfo.Tables[0].Columns[1].SqlType, Is.EqualTo("Text"));
        Assert.That(databaseInfo.Tables[0].Columns[1].CSharpType, Is.EqualTo("string"));
        Assert.That(databaseInfo.Tables[0].Columns[1].TypeAffinity, Is.EqualTo(TypeAffinity.TEXT));
    }

    [Test]
    public void ProcessSqlSchema_IntegerColumn_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema("CREATE TABLE contact (age Integer);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlName, Is.EqualTo("age"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpName, Is.EqualTo("Age"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlType, Is.EqualTo("Integer"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpType, Is.EqualTo("long"));
        Assert.That(databaseInfo.Tables[0].Columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.INTEGER));
    }

    [Test]
    public void ProcessSqlSchema_RealColumn_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema("CREATE TABLE contact (height Real);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlName, Is.EqualTo("height"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpName, Is.EqualTo("Height"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlType, Is.EqualTo("Real"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpType, Is.EqualTo("double"));
        Assert.That(databaseInfo.Tables[0].Columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.REAL));
    }

    [Test]
    public void ProcessSqlSchema_BlobColumn_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema("CREATE TABLE contact (key Blob);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlName, Is.EqualTo("key"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpName, Is.EqualTo("Key"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlType, Is.EqualTo("Blob"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpType, Is.EqualTo("byte[]"));
        Assert.That(databaseInfo.Tables[0].Columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.BLOB));
    }

    [Test]
    public void ProcessSqlSchema_NumericColumn_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema("CREATE TABLE contact (distance Numeric);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlName, Is.EqualTo("distance"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpName, Is.EqualTo("Distance"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlType, Is.EqualTo("Numeric"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpType, Is.EqualTo("Numeric"));
        Assert.That(databaseInfo.Tables[0].Columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.NUMERIC));
    }
}