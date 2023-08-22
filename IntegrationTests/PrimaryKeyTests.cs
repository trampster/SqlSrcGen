using SqlSrcGen;
using SQLite;

namespace Tests;

public class PrimaryKeyTests
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
        _database!.CreatePrimaryKeyTableTable();

        // assert
        var tableDetails = _sqliteNetConnection?.Query<TableDetails>("SELECT name FROM sqlite_master WHERE type='table' AND name='primary_key_table';");
        Assert.That(tableDetails?.Any() ?? false);
    }

    record SqliteNetPrimaryKeyTable
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
    }

    [Test]
    public void Get_Exists_SetsValuesReturnsTrue()
    {
        // arrange
        _database!.CreatePrimaryKeyTableTable();

        _sqliteNetConnection?.Execute("INSERT INTO primary_key_table (name, email) VALUES (\"Steve\", \"steve.rogers@avengers.com\")");
        _sqliteNetConnection?.Execute("INSERT INTO primary_key_table (name, email) VALUES (\"Bruce\", \"bruce.banner@avengers.com\")");

        PrimaryKeyTable contact = new();

        // act 
        bool found = _database!.GetPrimaryKeyTable(contact, "Bruce");

        // assert
        Assert.That(found, Is.True);
        Assert.That(contact.Name, Is.EqualTo("Bruce"));
        Assert.That(contact.Email, Is.EqualTo("bruce.banner@avengers.com"));
    }

    [Test]
    public void Get_DoesntExist_ReturnsFalse()
    {
        // arrange
        _database!.CreatePrimaryKeyTableTable();

        _sqliteNetConnection?.Execute("INSERT INTO primary_key_table (name, email) VALUES (\"Steve\", \"steve.rogers@avengers.com\")");
        _sqliteNetConnection?.Execute("INSERT INTO primary_key_table (name, email) VALUES (\"Bruce\", \"bruce.banner@avengers.com\")");

        PrimaryKeyTable contact = new();

        // act 
        bool found = _database!.GetPrimaryKeyTable(contact, "Thanos");

        // assert
        Assert.That(found, Is.False);
    }

    [Test]
    public void Delete_Exists_RowDeleted()
    {
        // arrange
        _database!.CreatePrimaryKeyTableTable();

        _sqliteNetConnection?.Execute("INSERT INTO primary_key_table (name, email) VALUES (\"Steve\", \"steve.rogers@avengers.com\")");
        _sqliteNetConnection?.Execute("INSERT INTO primary_key_table (name, email) VALUES (\"Bruce\", \"bruce.banner@avengers.com\")");

        PrimaryKeyTable contact = new();

        // act 
        _database!.DeletePrimaryKeyTable("Bruce");

        // assert
        var rows = _sqliteNetConnection?.Query<SqliteNetPrimaryKeyTable>("Select * from primary_key_table;")!;
        Assert.That(rows.Count(), Is.EqualTo(1));
        Assert.That(rows.First()!.Name, Is.EqualTo("Steve"));
    }
}