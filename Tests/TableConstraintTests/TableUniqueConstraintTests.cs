using Moq;
using SqlSrcGen;

namespace Tests.TableConstraintTests;

public class TableUniqueConstraintTests
{
    [Test]
    public void UniqueTableConstraint_Valid_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema($"CREATE TABLE contact (name Text, unique (name));", databaseInfo, Mock.Of<IDiagnosticsReporter>());

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlName, Is.EqualTo("name"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpName, Is.EqualTo("Name"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlType, Is.EqualTo("Text"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpType, Is.EqualTo("string?"));
        Assert.That(databaseInfo.Tables[0].Columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.TEXT));
        Assert.That(databaseInfo.Tables[0].Columns[0].Unique, Is.True);
    }

    [Test]
    public void UniqueTableConstraint_TwoColumns_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema($"CREATE TABLE contact (name Text, id integer, address Text, UNIQUE (name, id));", databaseInfo, Mock.Of<IDiagnosticsReporter>());

        // assert
        Assert.That(databaseInfo.Tables[0].Unique[0].Any(column => column.SqlName == "name"), Is.True);
        Assert.That(databaseInfo.Tables[0].Unique[0].Any(column => column.SqlName == "id"), Is.True);
    }

    [Test]
    public void UniqueTableConstraint_TwoUniqueColumns_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema($"CREATE TABLE contact (name Text, id integer, address Text, UNIQUE (name, id), UNIQUE (id, address));", databaseInfo, Mock.Of<IDiagnosticsReporter>());

        // assert
        Assert.That(databaseInfo.Tables[0].Unique[0].Any(column => column.SqlName == "name"), Is.True);
        Assert.That(databaseInfo.Tables[0].Unique[0].Any(column => column.SqlName == "id"), Is.True);
        Assert.That(databaseInfo.Tables[0].Unique[1].Any(column => column.SqlName == "id"), Is.True);
        Assert.That(databaseInfo.Tables[0].Unique[1].Any(column => column.SqlName == "address"), Is.True);
    }

    [Test]
    public void UniqueTableConstraint_ColumnAlreadyPrimaryKey_ThrowsInvalidJsonException()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        try
        {
            generator.ProcessSqlSchema($"CREATE TABLE contact (name Text, id integer Primary Key, address Text, Unique (id));", databaseInfo, Mock.Of<IDiagnosticsReporter>());
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            Assert.That(exception.Message, Is.EqualTo("Column is already unqiue because it's a primary key"));
            Assert.That(exception.Token.CharacterInLine, Is.EqualTo(79));
            Assert.That(exception.Token.Position, Is.EqualTo(79));
            Assert.That(exception.Token.Line, Is.EqualTo(0));
        }
    }

    [Test]
    public void UniqueTableConstraint_ColumnsAlreadyPrimaryKey_ThrowsInvalidJsonException()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        try
        {
            generator.ProcessSqlSchema($"CREATE TABLE contact (name Text, id integer, address Text, PRIMARY KEY (id, name), UNIQUE (id, name));", databaseInfo, Mock.Of<IDiagnosticsReporter>());
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            // assert
            Assert.That(exception.Message, Is.EqualTo("Columns are already unqiue because they are a primary key"));
            Assert.That(exception.Token.CharacterInLine, Is.EqualTo(83));
            Assert.That(exception.Token.Position, Is.EqualTo(83));
            Assert.That(exception.Token.Line, Is.EqualTo(0));
        }
    }

    [Test]
    public void UniqueTableConstraint_ColumnsAlreadyUnique_ThrowsInvalidJsonException()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        try
        {
            generator.ProcessSqlSchema($"CREATE TABLE contact (name Text, id integer, address Text, UNIQUE (id, name), UNIQUE (id, name));", databaseInfo, Mock.Of<IDiagnosticsReporter>());
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            // assert
            Assert.That(exception.Message, Is.EqualTo("Columns are already unique"));
            Assert.That(exception.Token.CharacterInLine, Is.EqualTo(78));
            Assert.That(exception.Token.Position, Is.EqualTo(78));
            Assert.That(exception.Token.Line, Is.EqualTo(0));
        }
    }

    [Test]
    public void UniqueTableConstraint_HasConflictClause_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema($"CREATE TABLE contact (name Text, id integer, address Text, Unique (name, id) ON CONFLICT ROLLBACK);", databaseInfo, Mock.Of<IDiagnosticsReporter>());

        // assert
        Assert.That(databaseInfo.Tables[0].Unique[0].Any(column => column.SqlName == "name"), Is.True);
        Assert.That(databaseInfo.Tables[0].Unique[0].Any(column => column.SqlName == "id"), Is.True);
    }
}