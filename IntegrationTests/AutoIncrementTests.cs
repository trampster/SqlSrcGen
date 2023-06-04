using SqlSrcGen;
using SQLite;
using SqlSrcGen.Runtime;

namespace Tests;

public class AutoIncrementTests
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
        _database!.CreateAutoincrementTableTable();

        // assert
        var tableDetails = _sqliteNetConnection?.Query<TableDetails>("SELECT name FROM sqlite_master WHERE type='table' AND name='autoincrement_table';");
        Assert.That(tableDetails?.Any() ?? false);
    }

    record SqliteNetAutoincrementTable
    {
        public long? Id { get; set; }
        public string? Email { get; set; }
    }

    [Test]
    public void Get_Exists_SetsValuesReturnsTrue()
    {
        // arrange
        _database!.CreateAutoincrementTableTable();

        _sqliteNetConnection?.Execute("INSERT INTO autoincrement_table (email) VALUES (\"steve.rogers@avengers.com\")");
        _sqliteNetConnection?.Execute("INSERT INTO autoincrement_table (email) VALUES (\"bruce.banner@avengers.com\")");

        AutoincrementTable contact = new();

        // act 
        bool found = _database!.GetAutoincrementTable(contact, 2);

        // assert
        Assert.That(found, Is.True);
        Assert.That(contact.Id, Is.EqualTo(2));
        Assert.That(contact.Email, Is.EqualTo("bruce.banner@avengers.com"));
    }

    [Test]
    public void Insert_AllowsNull_IdAutoSet()
    {
        // arrange
        _database!.CreateAutoincrementTableTable();
        AutoincrementTable row = new()
        {
            Email = "luke.skywalker@jedi.com"
        };

        // act 
        _database!.InsertAutoincrementTable(row);

        // assert
        var queriedRow = _sqliteNetConnection?.Query<SqliteNetAutoincrementTable>("Select * from autoincrement_table;").First()!;
        Assert.That(queriedRow.Id, Is.EqualTo(1));
        Assert.That(row.Id, Is.EqualTo(1));
        Assert.That(row.Email, Is.EqualTo("luke.skywalker@jedi.com"));
    }

    record SqliteNetAutoincrementNotNullTable
    {
        public long? Id { get; set; }
        public string? Email { get; set; }
    }

    [Test]
    public void Insert_NotNull_IdAutoSet()
    {
        // arrange
        _database!.CreateAutoincrementNotNullTableTable();
        AutoincrementNotNullTable row = new();

        // act 
        _database!.InsertAutoincrementNotNullTable(row);

        // assert
        var queriedRow = _sqliteNetConnection?.Query<SqliteNetAutoincrementNotNullTable>("Select * from autoincrement_not_null_table;").First()!;
        Assert.That(queriedRow.Id, Is.EqualTo(1));
        Assert.That(row.Id, Is.EqualTo(1));
    }
}