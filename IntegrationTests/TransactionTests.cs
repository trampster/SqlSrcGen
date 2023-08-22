using SqlSrcGen;
using SQLite;

namespace Tests;

public class TransactionTests
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
        _database!.CreateNullableContactTable();

        // assert
        var tableDetails = _sqliteNetConnection?.Query<TableDetails>("SELECT name FROM sqlite_master WHERE type='table' AND name='nullable_contact';");
        Assert.That(tableDetails?.Any() ?? false);
    }

    record SqliteNetContact
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public long? Age { get; set; }
        public double? Height { get; set; }
        public byte[]? PrivateKey { get; set; }
        public double? Mana { get; set; }
    }

    [Test]
    public void InsertInTransaction_Works()
    {
        // arrange
        _database!.CreateNullableContactTable();

        var contact = new NullableContact()
        {
            Name = "Steve",
            Email = "steve.rogers@superhero.com",
            Age = 35,
            Height = 170.5,
            PrivateKey = new byte[] { 1, 2, 3 },
            Mana = new Numeric(24.5)
        };

        // act
        _database!.BeginTransaction();
        _database!.InsertNullableContact(contact);
        _database!.CommitTransaction();

        // assert
        var actualContact = _sqliteNetConnection?.Query<SqliteNetContact>("Select * from nullable_contact;").First()!;
        Assert.That(contact.Name, Is.EqualTo(actualContact.Name));
        Assert.That(contact.Age, Is.EqualTo(actualContact.Age));
        Assert.That(contact.Email, Is.EqualTo(actualContact.Email));
        Assert.That(contact.Height, Is.EqualTo(actualContact.Height));
        Assert.That(contact.PrivateKey, Is.EqualTo(actualContact.PrivateKey));
        Assert.That(contact.Mana.Value.GetReal(), Is.EqualTo(actualContact.Mana));
    }

    [Test]
    public void RollbackTransaction_Works()
    {
        // arrange
        _database!.CreateNullableContactTable();

        var contact = new NullableContact()
        {
            Name = "Steve",
            Email = "steve.rogers@superhero.com",
            Age = 35,
            Height = 170.5,
            PrivateKey = new byte[] { 1, 2, 3 },
            Mana = new Numeric(24.5)
        };

        // act
        _database!.BeginTransaction();
        _database!.InsertNullableContact(contact);
        _database!.RollbackTransaction();

        // assert
        var count = _sqliteNetConnection?.Query<SqliteNetContact>("Select * from nullable_contact;").Count();
        Assert.That(count, Is.EqualTo(0));
    }
}