using Moq;
using SqlSrcGen.Generator;

namespace Tests;

public class DefaultConstraintTests
{
    [TestCase("default 'mydefault'")]
    [TestCase("default'mydefault'")]
    [TestCase("default null")]
    [TestCase("default NULL")]
    [TestCase("default FALSE")]
    [TestCase("default CURRENT_TIME")]
    [TestCase("default CURRENT_DATE")]
    [TestCase("default CURRENT_TIMESTAMP")]
    [TestCase("default 123.123e+12")]
    [TestCase("default +123.123e+12")]
    [TestCase("default -123.123e+12")]
    [TestCase("default x'A1B2C3'")]
    [TestCase("default (1+2)")]
    public void ProcessSqlSchema_DefaultConstraint_CreatesTableInfo(string constraint)
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema($"CREATE TABLE contact (name Text {constraint});", databaseInfo);

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