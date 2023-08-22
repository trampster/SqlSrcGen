using System.Text;
using Moq;
using SqlSrcGen.Generator;

namespace Tests;

public class TableForeignKeyConstraintTests
{
    [Test]
    public void ForeignKeyConstraint_ReferencesTwoColumnsUnique_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();
        var queryBuilder = new StringBuilder();
        queryBuilder.Append("CREATE TABLE addresses (street_name Text, street_number Integer, UNIQUE (street_name, street_number));\n");
        queryBuilder.Append("CREATE TABLE contact (name Text, street Text, number Integer, FOREIGN KEY (street, number) references addresses (street_name, street_number));");

        // act
        generator.ProcessSqlSchema(queryBuilder.ToString(), databaseInfo, Mock.Of<IDiagnosticsReporter>());

        // assert
        Assert.That(databaseInfo.Tables[1].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[1].CSharpName, Is.EqualTo("Contact"));
        Assert.That(databaseInfo.Tables[1].Columns[0].SqlName, Is.EqualTo("name"));
        Assert.That(databaseInfo.Tables[1].Columns[1].SqlName, Is.EqualTo("street"));
        Assert.That(databaseInfo.Tables[1].Columns[2].SqlName, Is.EqualTo("number"));
    }

    [Test]
    public void ForeignKeyConstraint_ReferencesTwoColumnsPrimaryKey_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();
        var queryBuilder = new StringBuilder();
        queryBuilder.Append("CREATE TABLE addresses (street_name Text, street_number Integer, PRIMARY KEY (street_name, street_number));\n");
        queryBuilder.Append("CREATE TABLE contact (name Text, street Text, number Integer, FOREIGN KEY (street, number) references addresses (street_name, street_number));");

        // act
        generator.ProcessSqlSchema(queryBuilder.ToString(), databaseInfo, Mock.Of<IDiagnosticsReporter>());

        // assert
        Assert.That(databaseInfo.Tables[1].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[1].CSharpName, Is.EqualTo("Contact"));
        Assert.That(databaseInfo.Tables[1].Columns[0].SqlName, Is.EqualTo("name"));
        Assert.That(databaseInfo.Tables[1].Columns[1].SqlName, Is.EqualTo("street"));
        Assert.That(databaseInfo.Tables[1].Columns[2].SqlName, Is.EqualTo("number"));
    }

    [Test]
    public void ForeignKeyConstraint_TableOnlyReferenceHasPrimaryKey_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();
        var queryBuilder = new StringBuilder();
        queryBuilder.Append("CREATE TABLE addresses (street_name Text, street_number Integer, PRIMARY KEY (street_name, street_number));\n");
        queryBuilder.Append("CREATE TABLE contact (name Text, street Text, number Integer, FOREIGN KEY (street, number) references addresses);");

        // act
        generator.ProcessSqlSchema(queryBuilder.ToString(), databaseInfo, Mock.Of<IDiagnosticsReporter>());

        // assert
        Assert.That(databaseInfo.Tables[1].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[1].CSharpName, Is.EqualTo("Contact"));
        Assert.That(databaseInfo.Tables[1].Columns[0].SqlName, Is.EqualTo("name"));
        Assert.That(databaseInfo.Tables[1].Columns[1].SqlName, Is.EqualTo("street"));
        Assert.That(databaseInfo.Tables[1].Columns[2].SqlName, Is.EqualTo("number"));
    }

    [Test]
    public void ForeignKeyConstraint_TableOnlyReferenceColumnsUnique_InvalidSqlException()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();
        var queryBuilder = new StringBuilder();
        queryBuilder.Append("CREATE TABLE addresses (street_name Text, street_number Integer, UNIQUE (street_name, street_number));\n");
        queryBuilder.Append("CREATE TABLE contact (name Text, street Text, number Integer, FOREIGN KEY (street, number) references addresses);");

        try
        {
            // act
            generator.ProcessSqlSchema(queryBuilder.ToString(), databaseInfo, Mock.Of<IDiagnosticsReporter>());
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            Assert.That(exception.Message, Is.EqualTo("Referenced table doesn't have a matching foreign key"));
            Assert.That(exception.Token.CharacterInLine, Is.EqualTo(91));
            Assert.That(exception.Token.Position, Is.EqualTo(194));
            Assert.That(exception.Token.Line, Is.EqualTo(1));
        }
    }

    [Test]
    public void ForeignKeyConstraint_MismatchedColumnCounts_InvalidSqlException()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();
        var queryBuilder = new StringBuilder();
        queryBuilder.Append("CREATE TABLE addresses (street_name Text, street_number Integer, PRIMARY KEY (street_name, street_number));\n");
        queryBuilder.Append("CREATE TABLE contact (name Text, street Text, number Integer, FOREIGN KEY (street) references addresses (street_name, street_number));");

        try
        {
            // act
            generator.ProcessSqlSchema(queryBuilder.ToString(), databaseInfo, Mock.Of<IDiagnosticsReporter>());
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            Assert.That(exception.Message, Is.EqualTo("Local and foreign column counts must match"));
            Assert.That(exception.Token.CharacterInLine, Is.EqualTo(83));
            Assert.That(exception.Token.Position, Is.EqualTo(191));
            Assert.That(exception.Token.Line, Is.EqualTo(1));
        }
    }

    [Test]
    public void ForeignKeyConstraint_ReferencedColumnDoesntExist_InvalidSqlException()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();
        var queryBuilder = new StringBuilder();
        queryBuilder.Append("CREATE TABLE addresses (street_name Text, street_number Integer, PRIMARY KEY (street_name, street_number));\n");
        queryBuilder.Append("CREATE TABLE contact (name Text, street Text, number Integer, FOREIGN KEY (street, number) references addresses (street_name, street_number1));");

        try
        {
            // act
            generator.ProcessSqlSchema(queryBuilder.ToString(), databaseInfo, Mock.Of<IDiagnosticsReporter>());
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            Assert.That(exception.Message, Is.EqualTo("Referenced column street_number1 does not exist"));
            Assert.That(exception.Token.CharacterInLine, Is.EqualTo(126));
            Assert.That(exception.Token.Position, Is.EqualTo(234));
            Assert.That(exception.Token.Line, Is.EqualTo(1));
        }
    }

    [Test]
    public void ForeignKeyConstraint_ForeignTableNotUniqueByColumns_InvalidSqlException()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();
        var queryBuilder = new StringBuilder();
        queryBuilder.Append("CREATE TABLE addresses (street_name Text, street_number Integer, PRIMARY KEY (street_name, street_number));\n");
        queryBuilder.Append("CREATE TABLE contact (name Text, street Text, number Integer, FOREIGN KEY (street) references addresses (street_name));");

        try
        {
            // act
            generator.ProcessSqlSchema(queryBuilder.ToString(), databaseInfo, Mock.Of<IDiagnosticsReporter>());
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            Assert.That(exception.Message, Is.EqualTo("Foreign table is not unique by these columns"));
            Assert.That(exception.Token.CharacterInLine, Is.EqualTo(83));
            Assert.That(exception.Token.Position, Is.EqualTo(191));
            Assert.That(exception.Token.Line, Is.EqualTo(1));
        }
    }
}