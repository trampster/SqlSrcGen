using Moq;
using SqlSrcGen.Generator;

namespace Tests;

public class UniqueConstraintTests
{
    [Test]
    public void ProcessSqlSchema_UniqueConstraint_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema("CREATE TABLE contact (name Text unique);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        var columns = databaseInfo.Tables[0].Columns.ToArray();
        Assert.That(columns[0].Unique, Is.True);
        Assert.That(columns[0].SqlName, Is.EqualTo("name"));
        Assert.That(columns[0].CSharpName, Is.EqualTo("Name"));
        Assert.That(columns[0].SqlType, Is.EqualTo("Text"));
        Assert.That(columns[0].CSharpType, Is.EqualTo("string?"));
        Assert.That(columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.TEXT));
    }

    [Test]
    public void ProcessSqlSchema_UnqiueConstraintWithOnConflict_Parsed()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema("CREATE TABLE contact (distance INTEGER UNIQUE ON CONFLICT ROLLBACK);", databaseInfo);

        // assert
        var columns = databaseInfo.Tables[0].Columns.ToArray();
        Assert.That(columns[0].Unique, Is.True);
        Assert.That(columns[0].SqlType, Is.EqualTo("INTEGER"));
        Assert.That(columns[0].SqlName, Is.EqualTo("distance"));
        Assert.That(columns[0].CSharpType, Is.EqualTo("long?"));
        Assert.That(columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.INTEGER));
    }

}