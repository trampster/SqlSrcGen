using SqlSrcGen;
using SQLite;

namespace Tests;

public class NullableTests
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
    public void Insert_NotNull()
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
        _database!.InsertNullableContact(contact);

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
    public void Insert_Null()
    {
        // arrange
        _database!.CreateNullableContactTable();

        var contact = new NullableContact()
        {
            Name = null,
            Email = null,
            Age = null,
            Height = null,
            PrivateKey = null,
            Mana = null
        };

        // act
        _database!.InsertNullableContact(contact);

        // assert
        var actualContact = _sqliteNetConnection?.Query<SqliteNetContact>("Select * from nullable_contact;").First()!;
        Assert.That(actualContact.Name, Is.Null);
        Assert.That(actualContact.Age, Is.Null);
        Assert.That(actualContact.Email, Is.Null);
        Assert.That(actualContact.Height, Is.Null);
        Assert.That(actualContact.PrivateKey, Is.Null);
        Assert.That(actualContact.Mana, Is.Null);
    }

    [Test]
    public void All()
    {
        // arrange
        _database!.CreateNullableContactTable();

        var steveContact = new NullableContact()
        {
            Name = "Steve",
            Email = "steve.rogers@avengers.com",
            Age = 35,
            Height = 170.5,
            PrivateKey = new byte[] { 1, 2, 3 },
            Mana = new Numeric(24.5)
        };
        var tonyContact = new NullableContact()
        {
            Name = null,
            Email = null,
            Age = null,
            Height = null,
            PrivateKey = null,
            Mana = null
        };
        _sqliteNetConnection?.Execute("INSERT INTO nullable_contact (name, email, age, height, privateKey, mana) VALUES (\"Steve\", \"steve.rogers@avengers.com\", 35, 170.5, X'010203', 24.5)");
        _sqliteNetConnection?.Execute("INSERT INTO nullable_contact (name, email, age, height, privateKey, mana) VALUES (null, null, null, null, null, null)");

        List<NullableContact> contacts = new();

        // act
        _database!.AllNullableContacts(contacts);

        // assert
        Assert.That(contacts.Count, Is.EqualTo(2));
        Assert.That(contacts[0].Name, Is.EqualTo(steveContact.Name));
        Assert.That(contacts[0].Age, Is.EqualTo(steveContact.Age));
        Assert.That(contacts[0].Email, Is.EqualTo(steveContact.Email));
        Assert.That(contacts[0].PrivateKey, Is.EqualTo(steveContact.PrivateKey));
        Assert.That(contacts[0].Mana, Is.EqualTo(steveContact.Mana));
        Assert.That(contacts[1].Name, Is.EqualTo(tonyContact.Name));
        Assert.That(contacts[1].Age, Is.EqualTo(tonyContact.Age));
        Assert.That(contacts[1].Email, Is.EqualTo(tonyContact.Email));
        Assert.That(contacts[1].PrivateKey, Is.EqualTo(tonyContact.PrivateKey));
        Assert.That(contacts[1].Mana, Is.EqualTo(tonyContact.Mana));
    }
}