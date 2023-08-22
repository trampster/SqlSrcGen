using SqlSrcGen;
using SQLite;

namespace Tests;

public class UniqueConstraintTests
{
    const string DatabaseName = "database.sql";

    Database? _database;
    SQLiteConnection? _sqliteNetConnection;

    [OneTimeTearDown]
    public void FixtureTearDown()
    {
        _database?.Dispose();
    }

    [SetUp]
    public void Setup()
    {
        if (File.Exists(DatabaseName))
        {
            File.Delete(DatabaseName);
        }

        _database = new Database(DatabaseName);

        _sqliteNetConnection = new SQLiteConnection(DatabaseName);
    }

    public class TableDetails
    {
        public string? Name { get; set; }
    }

    [Test]
    public void CreateTable()
    {
        // arrange
        // act
        _database!.CreateUniqueConstraintTable();

        // assert
        var tableDetails = _sqliteNetConnection?.Query<TableDetails>("SELECT name FROM sqlite_master WHERE type='table' AND name='unique_constraint';");
        Assert.That(tableDetails?.Any() ?? false);
    }

    record SqliteUniqueConstraint
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
    }

    [Test]
    public void Get_Composite_SetsValuesReturnsTrue()
    {
        // arrange
        _database!.CreateUniqueConstraintTable();

        _sqliteNetConnection?.Execute("INSERT INTO unique_constraint (name, email) VALUES (\"Steve\", \"steve.rogers@avengers.com\")");
        _sqliteNetConnection?.Execute("INSERT INTO unique_constraint (name, email) VALUES (\"Bruce\", \"bruce.banner@avengers.com\")");

        UniqueConstraint contact = new();

        // act 
        bool found = _database!.GetUniqueConstraint(contact, "Bruce", "bruce.banner@avengers.com");

        // assert
        Assert.That(found, Is.True);
        Assert.That(contact.Name, Is.EqualTo("Bruce"));
        Assert.That(contact.Email, Is.EqualTo("bruce.banner@avengers.com"));
    }

    [Test]
    public void Get_Single_SetsValuesReturnsTrue()
    {
        // arrange
        _database!.CreateUniqueConstraintTable();

        _sqliteNetConnection?.Execute("INSERT INTO unique_constraint (name, email) VALUES (\"Steve\", \"steve.rogers@avengers.com\")");
        _sqliteNetConnection?.Execute("INSERT INTO unique_constraint (name, email) VALUES (\"Bruce\", \"bruce.banner@avengers.com\")");

        UniqueConstraint contact = new();

        // act 
        bool found = _database!.GetUniqueConstraint(contact, "Bruce");

        // assert
        Assert.That(found, Is.True);
        Assert.That(contact.Name, Is.EqualTo("Bruce"));
        Assert.That(contact.Email, Is.EqualTo("bruce.banner@avengers.com"));
    }

    [Test]
    public void Get_DoesntExist_ReturnsFalse()
    {
        // arrange
        _database!.CreateUniqueConstraintTable();

        _sqliteNetConnection?.Execute("INSERT INTO unique_constraint (name, email) VALUES (\"Steve\", \"steve.rogers@avengers.com\")");
        _sqliteNetConnection?.Execute("INSERT INTO unique_constraint (name, email) VALUES (\"Bruce\", \"bruce.banner@avengers.com\")");

        UniqueConstraint contact = new();

        // act 
        bool found = _database!.GetUniqueConstraint(contact, "Bruce", "steve.rogers@avengers.com");

        // assert
        Assert.That(found, Is.False);
    }

    [Test]
    public void Delete_Composite_RowDeleted()
    {
        // arrange
        _database!.CreateUniqueConstraintTable();

        _sqliteNetConnection?.Execute("INSERT INTO unique_constraint (name, email) VALUES (\"Steve\", \"steve.rogers@avengers.com\")");
        _sqliteNetConnection?.Execute("INSERT INTO unique_constraint (name, email) VALUES (\"Bruce\", \"bruce.banner@avengers.com\")");

        // act 
        _database!.DeleteUniqueConstraint("Bruce", "bruce.banner@avengers.com");

        // assert
        var rows = _sqliteNetConnection?.Query<SqliteUniqueConstraint>("Select * from unique_constraint;")!;
        Assert.That(rows.Count(), Is.EqualTo(1));
        Assert.That(rows.First()!.Name, Is.EqualTo("Steve"));
    }

    [Test]
    public void Delete_Single_RowDeleted()
    {
        // arrange
        _database!.CreateUniqueConstraintTable();

        _sqliteNetConnection?.Execute("INSERT INTO unique_constraint (name, email) VALUES (\"Steve\", \"steve.rogers@avengers.com\")");
        _sqliteNetConnection?.Execute("INSERT INTO unique_constraint (name, email) VALUES (\"Bruce\", \"bruce.banner@avengers.com\")");

        // act 
        _database!.DeleteUniqueConstraint("Bruce");

        // assert
        var rows = _sqliteNetConnection?.Query<SqliteUniqueConstraint>("Select * from unique_constraint;")!;
        Assert.That(rows.Count(), Is.EqualTo(1));
        Assert.That(rows.First()!.Name, Is.EqualTo("Steve"));
    }

    [Test]
    public void Delete_DoesntExists_NoRowDeleted()
    {
        // arrange
        _database!.CreateUniqueConstraintTable();

        _sqliteNetConnection?.Execute("INSERT INTO unique_constraint (name, email) VALUES (\"Steve\", \"steve.rogers@avengers.com\")");
        _sqliteNetConnection?.Execute("INSERT INTO unique_constraint (name, email) VALUES (\"Bruce\", \"bruce.banner@avengers.com\")");

        // act 
        _database!.DeleteUniqueConstraint("Bruce", "steve.rogers@avengers.com");

        // assert
        var rows = _sqliteNetConnection?.Query<SqliteUniqueConstraint>("Select * from unique_constraint;")!;
        Assert.That(rows.Count(), Is.EqualTo(2));
    }
}