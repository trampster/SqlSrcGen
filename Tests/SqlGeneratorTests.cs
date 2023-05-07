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
    public void ProcessSqlSchema_CreatesTableInfo()
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
        Assert.That(databaseInfo.Tables[0].Columns[1].SqlName, Is.EqualTo("email"));
        Assert.That(databaseInfo.Tables[0].Columns[1].CSharpName, Is.EqualTo("Email"));
        Assert.That(databaseInfo.Tables[0].Columns[1].SqlType, Is.EqualTo("Text"));
        Assert.That(databaseInfo.Tables[0].Columns[1].CSharpType, Is.EqualTo("string"));
    }
}