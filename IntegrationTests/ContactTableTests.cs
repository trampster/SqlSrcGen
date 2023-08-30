using SqlSrcGen;
using SQLite;

namespace Tests;

public class ContactTableTests
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
        _database!.CreateContactTable();

        // assert
        var tableDetails = _sqliteNetConnection?.Query<TableDetails>("SELECT name FROM sqlite_master WHERE type='table' AND name='contact';");
        Assert.That(tableDetails?.Any() ?? false);
    }

    record SqliteNetContact
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public long Age { get; set; }
        public double Height { get; set; }
        public byte[] PrivateKey { get; set; } = new byte[0];
        public double Mana { get; set; }
    }

    [Test]
    public void Insert()
    {
        // arrange
        _database!.CreateContactTable();

        var contact = new Contact()
        {
            Name = "Steve",
            Email = "steve.rogers@superhero.com",
            Age = 35,
            Height = 170.5,
            PrivateKey = new byte[] { 1, 2, 3 },
            Mana = new Numeric(24.5)
        };

        // act
        _database!.InsertContact(contact);

        // assert
        var actualContact = _sqliteNetConnection?.Query<SqliteNetContact>("Select * from contact;").First()!;
        Assert.That(contact.Name, Is.EqualTo(actualContact.Name));
        Assert.That(contact.Age, Is.EqualTo(actualContact.Age));
        Assert.That(contact.Email, Is.EqualTo(actualContact.Email));
        Assert.That(contact.Height, Is.EqualTo(actualContact.Height));
        Assert.That(contact.PrivateKey, Is.EqualTo(actualContact.PrivateKey));
        Assert.That(contact.Mana.GetReal(), Is.EqualTo(actualContact.Mana));
    }

    [Test]
    public void All()
    {
        // arrange
        _database!.CreateContactTable();

        var steveContact = new Contact()
        {
            Name = "Steve",
            Email = "steve.rogers@avengers.com",
            Age = 35,
            Height = 170.5,
            PrivateKey = new byte[] { 1, 2, 3 },
            Mana = new Numeric(24.5)
        };
        var tonyContact = new Contact()
        {
            Name = "Tony",
            Email = "tony.stark@avengers.com",
            Age = 40,
            Height = 185.42,
            PrivateKey = new byte[] { 1, 2, 3 },
            Mana = new Numeric(24.5)
        };
        _sqliteNetConnection?.Execute("INSERT INTO contact (name, email, age, height, privateKey, mana) VALUES (\"Steve\", \"steve.rogers@avengers.com\", 35, 170.5, X'010203', 24.5)");
        _sqliteNetConnection?.Execute("INSERT INTO contact (name, email, age, height, privateKey, mana) VALUES (\"Tony\", \"tony.stark@avengers.com\", 40, 185.42, X'010203', \"24.5\")");

        List<Contact> contacts = new();

        // act
        _database!.AllContacts(contacts);

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

    [Test]
    public void CustomQuery_GetAllContactsTest()
    {
        // arrange
        _database!.CreateContactTable();

        var steveContact = new Contact()
        {
            Name = "Steve",
            Email = "steve.rogers@avengers.com",
            Age = 35,
            Height = 170.5,
            PrivateKey = new byte[] { 1, 2, 3 },
            Mana = new Numeric(24.5)
        };
        var tonyContact = new Contact()
        {
            Name = "Tony",
            Email = "tony.stark@avengers.com",
            Age = 40,
            Height = 185.42,
            PrivateKey = new byte[] { 1, 2, 3 },
            Mana = new Numeric(24.5)
        };
        _sqliteNetConnection?.Execute("INSERT INTO contact (name, email, age, height, privateKey, mana) VALUES (\"Steve\", \"steve.rogers@avengers.com\", 35, 170.5, X'010203', 24.5)");
        _sqliteNetConnection?.Execute("INSERT INTO contact (name, email, age, height, privateKey, mana) VALUES (\"Tony\", \"tony.stark@avengers.com\", 40, 185.42, X'010203', \"24.5\")");

        List<Contact> contacts = new();

        // act
        _database!.GetAllContacts(contacts);

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

    [Test]
    public void DeleteAll()
    {
        // arrange
        _database!.CreateContactTable();

        _sqliteNetConnection?.Execute("INSERT INTO contact (name, email, age, height, privateKey, mana) VALUES (\"Steve\", \"steve.rogers@avengers.com\", 35, 170.5, X'010203', 24.5)");
        _sqliteNetConnection?.Execute("INSERT INTO contact (name, email, age, height, privateKey, mana) VALUES (\"Tony\", \"tony.stark@avengers.com\", 40, 185.42, X'010203', \"24.5\")");

        // act
        _database!.DeleteAllContacts();

        // assert
        var contacts = _sqliteNetConnection?.Query<SqliteNetContact>("Select * from contact;");

        Assert.That(contacts!.Count, Is.EqualTo(0));
    }
}