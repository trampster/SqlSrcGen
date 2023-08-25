using Moq;
using SqlSrcGen.Generator;

namespace Tests;

public class TableCheckConstraintTests
{
    [TestCase("Age>=18")]
    [TestCase("Age>=18 AND City='Sandnes'")]
    [TestCase("(Age>=18, City='Sandnes')")]
    public void CheckConstraint_ParsedWithoutError_CreatesTableInfo(string expr)
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema($"CREATE TABLE contact (name Text, CHECK ({expr}));", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        var columns = databaseInfo.Tables[0].Columns.ToArray();
        Assert.That(columns[0].SqlName, Is.EqualTo("name"));
        Assert.That(columns[0].CSharpName, Is.EqualTo("Name"));
        Assert.That(columns[0].SqlType, Is.EqualTo("Text"));
        Assert.That(columns[0].CSharpType, Is.EqualTo("string?"));
        Assert.That(columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.TEXT));
    }
}