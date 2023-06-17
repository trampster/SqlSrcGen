using Moq;
using SqlSrcGen;

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
        generator.ProcessSqlSchema($"CREATE TABLE contact (name Text, CHECK ({expr}));", databaseInfo, Mock.Of<IDiagnosticsReporter>());

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlName, Is.EqualTo("name"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpName, Is.EqualTo("Name"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlType, Is.EqualTo("Text"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpType, Is.EqualTo("string?"));
        Assert.That(databaseInfo.Tables[0].Columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.TEXT));
    }
}