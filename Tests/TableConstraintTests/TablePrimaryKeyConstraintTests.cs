using Moq;
using SqlSrcGen.Generator;

namespace Tests.TableConstraintTests;

public class TablePrimaryKeyConstraintTests
{
    [Test]
    public void PrimaryKeyTableConstraint_Valid_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema($"CREATE TABLE contact (name Text, PRIMARY KEY (name));", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        var columns = databaseInfo.Tables[0].Columns.ToArray();
        Assert.That(columns[0].SqlName, Is.EqualTo("name"));
        Assert.That(columns[0].CSharpName, Is.EqualTo("Name"));
        Assert.That(columns[0].SqlType, Is.EqualTo("Text"));
        Assert.That(columns[0].CSharpType, Is.EqualTo("string?"));
        Assert.That(columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.TEXT));
        Assert.That(columns[0].PrimaryKey, Is.True);
    }

    [Test]
    public void PrimaryKeyTableConstraint_TwoColumns_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema($"CREATE TABLE contact (name Text, id integer, address Text, PRIMARY KEY (name, id));", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].PrimaryKey.Any(column => column.SqlName == "name"), Is.True);
        Assert.That(databaseInfo.Tables[0].PrimaryKey.Any(column => column.SqlName == "id"), Is.True);
    }

    [Test]
    public void TablePrimaryKeyConstraint_TableAlreadyHasPrimaryKey_ThrowsInvalidJsonException()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        try
        {
            generator.ProcessSqlSchema($"CREATE TABLE contact (name Text Primary Key, id integer, address Text, PRIMARY KEY (id));", databaseInfo);
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            Assert.That(exception.Message, Is.EqualTo("Table already has a primary key"));
            Assert.That(exception.Token!.CharacterInLine, Is.EqualTo(71));
            Assert.That(exception.Token.Position, Is.EqualTo(71));
            Assert.That(exception.Token.Line, Is.EqualTo(0));
        }
    }

    [Test]
    public void TablePrimaryKeyConstraint_ColumnAlreadyHasPrimaryKey_ThrowsInvalidJsonException()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        try
        {
            generator.ProcessSqlSchema($"CREATE TABLE contact (name Text, id integer Primary Key, address Text, PRIMARY KEY (id));", databaseInfo);
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            Assert.That(exception.Message, Is.EqualTo("Table already has a primary key"));
            Assert.That(exception.Token!.CharacterInLine, Is.EqualTo(71));
            Assert.That(exception.Token.Position, Is.EqualTo(71));
            Assert.That(exception.Token.Line, Is.EqualTo(0));
        }
    }

    [Test]
    public void TablePrimaryKeyConstraint_ColumnAlreadyUnique_ThrowsInvalidJsonException()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        try
        {
            generator.ProcessSqlSchema($"CREATE TABLE contact (name Text, id integer UNIQUE, address Text, PRIMARY KEY (id));", databaseInfo);
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            Assert.That(exception.Message, Is.EqualTo("Column is already unique"));
            Assert.That(exception.Token!.CharacterInLine, Is.EqualTo(79));
            Assert.That(exception.Token.Position, Is.EqualTo(79));
            Assert.That(exception.Token.Line, Is.EqualTo(0));
        }
    }

    [Test]
    public void PrimaryKeyTableConstraint_TableAlreadyPrimaryKey_ThrowsInvalidJsonException()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        try
        {
            generator.ProcessSqlSchema($"CREATE TABLE contact (name Text, id integer, address Text, PRIMARY KEY (id, name), PRIMARY KEY (id, address));", databaseInfo);
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            // assert
            Assert.That(exception.Message, Is.EqualTo("Table already has a primary key"));
            Assert.That(exception.Token!.CharacterInLine, Is.EqualTo(83));
            Assert.That(exception.Token.Position, Is.EqualTo(83));
            Assert.That(exception.Token.Line, Is.EqualTo(0));
        }
    }

    [Test]
    public void PrimaryKeyTableConstraint_ColumnsAlreadyUnique_ThrowsInvalidJsonException()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        try
        {
            generator.ProcessSqlSchema($"CREATE TABLE contact (name Text, id integer, address Text, UNIQUE (id, name), PRIMARY KEY (id, name));", databaseInfo);
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            // assert
            Assert.That(exception.Message, Is.EqualTo("Columns are already unique"));
            Assert.That(exception.Token!.CharacterInLine, Is.EqualTo(78));
            Assert.That(exception.Token.Position, Is.EqualTo(78));
            Assert.That(exception.Token.Line, Is.EqualTo(0));
        }
    }

    [Test]
    public void PrimaryKeyTableConstraint_HasConflictClause_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema($"CREATE TABLE contact (name Text, id integer, address Text, PRIMARY KEY (name, id) ON CONFLICT ROLLBACK);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].PrimaryKey.Any(column => column.SqlName == "name"), Is.True);
        Assert.That(databaseInfo.Tables[0].PrimaryKey.Any(column => column.SqlName == "id"), Is.True);
    }


    [TestCase("(name, id)")]
    [TestCase("(name collate nocase, id)")]
    [TestCase("(name collate nocase asc, id)")]
    [TestCase("(name collate nocase desc, id)")]
    public void PrimaryKeyTableConstraint_IndexColumns_CreatesTableInfo(string indexedColumns)
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema($"CREATE TABLE contact (name Text, id integer, address Text, PRIMARY KEY {indexedColumns} ON CONFLICT ROLLBACK);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].PrimaryKey.Any(column => column.SqlName == "name"), Is.True);
        Assert.That(databaseInfo.Tables[0].PrimaryKey.Any(column => column.SqlName == "id"), Is.True);
    }
}