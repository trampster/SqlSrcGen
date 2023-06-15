using SqlSrcGen;
using SQLite;
using SqlSrcGen.Runtime;

namespace Tests;

public class CompositePrimaryKeyTests
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
        _database!.CreateCompositPrimaryKeyTable();

        // assert
        var tableDetails = _sqliteNetConnection?.Query<TableDetails>("SELECT name FROM sqlite_master WHERE type='table' AND name='composit_primary_key';");
        Assert.That(tableDetails?.Any() ?? false);
    }

    record SqliteNetCompositPrimaryKey
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
    }

    [Test]
    public void Get_Exists_SetsValuesReturnsTrue()
    {
        // arrange
        _database!.CreateCompositPrimaryKeyTable();

        _sqliteNetConnection?.Execute("INSERT INTO composit_primary_key (name, email) VALUES (\"Steve\", \"steve.rogers@avengers.com\")");
        _sqliteNetConnection?.Execute("INSERT INTO composit_primary_key (name, email) VALUES (\"Bruce\", \"bruce.banner@avengers.com\")");

        CompositPrimaryKey contact = new();

        // act 
        bool found = _database!.GetCompositPrimaryKey(contact, "Bruce", "bruce.banner@avengers.com");

        // assert
        Assert.That(found, Is.True);
        Assert.That(contact.Name, Is.EqualTo("Bruce"));
        Assert.That(contact.Email, Is.EqualTo("bruce.banner@avengers.com"));
    }

    [Test]
    public void Get_DoesntExist_ReturnsFalse()
    {
        // arrange
        _database!.CreateCompositPrimaryKeyTable();

        _sqliteNetConnection?.Execute("INSERT INTO composit_primary_key (name, email) VALUES (\"Steve\", \"steve.rogers@avengers.com\")");
        _sqliteNetConnection?.Execute("INSERT INTO composit_primary_key (name, email) VALUES (\"Bruce\", \"bruce.banner@avengers.com\")");

        CompositPrimaryKey contact = new();

        // act 
        bool found = _database!.GetCompositPrimaryKey(contact, "Bruce", "steve.rogers@avengers.com");

        // assert
        Assert.That(found, Is.False);
    }

    [Test]
    public void Delete_Exists_RowDeleted()
    {
        // arrange
        _database!.CreateCompositPrimaryKeyTable();

        _sqliteNetConnection?.Execute("INSERT INTO composit_primary_key (name, email) VALUES (\"Steve\", \"steve.rogers@avengers.com\")");
        _sqliteNetConnection?.Execute("INSERT INTO composit_primary_key (name, email) VALUES (\"Bruce\", \"bruce.banner@avengers.com\")");

        PrimaryKeyTable contact = new();

        // act 
        _database!.DeleteCompositPrimaryKey("Bruce", "bruce.banner@avengers.com");

        // assert
        var rows = _sqliteNetConnection?.Query<SqliteNetCompositPrimaryKey>("Select * from composit_primary_key;")!;
        Assert.That(rows.Count(), Is.EqualTo(1));
        Assert.That(rows.First()!.Name, Is.EqualTo("Steve"));
    }

    [Test]
    public void Delete_DoesntExists_NoRowDeleted()
    {
        // arrange
        _database!.CreateCompositPrimaryKeyTable();

        _sqliteNetConnection?.Execute("INSERT INTO composit_primary_key (name, email) VALUES (\"Steve\", \"steve.rogers@avengers.com\")");
        _sqliteNetConnection?.Execute("INSERT INTO composit_primary_key (name, email) VALUES (\"Bruce\", \"bruce.banner@avengers.com\")");

        PrimaryKeyTable contact = new();

        // act 
        _database!.DeleteCompositPrimaryKey("Bruce", "steve.rogers@avengers.com");

        // assert
        var rows = _sqliteNetConnection?.Query<SqliteNetCompositPrimaryKey>("Select * from composit_primary_key;")!;
        Assert.That(rows.Count(), Is.EqualTo(2));
    }
}