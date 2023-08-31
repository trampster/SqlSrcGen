using SqlSrcGen;
using SQLite;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using System.Reflection.Metadata.Ecma335;

namespace Tests;

public class JoinTests
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

    record SqliteNetContact
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public long Age { get; set; }
        public double Height { get; set; }
        public byte[] PrivateKey { get; set; } = new byte[0];
        public double Mana { get; set; }
    }

    record SqliteNetJob
    {
        public string Name { get; set; } = "";
        public long Salary { get; set; }
    }


    [Test]
    public void CustomQuery_GetAllContactsTest()
    {
        // arrange
        _database!.CreateContactTable();
        _database!.CreateJobTable();

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
        var teamLead = new Job()
        {
            Name = "Team Lead",
            Salary = 100000
        };

        var ceo = new Job()
        {
            Name = "CEO",
            Salary = 200000
        };

        _sqliteNetConnection?.Execute("INSERT INTO contact (name, email, age, height, privateKey, mana) VALUES (\"Steve\", \"steve.rogers@avengers.com\", 35, 170.5, X'010203', 24.5)");
        _sqliteNetConnection?.Execute("INSERT INTO contact (name, email, age, height, privateKey, mana) VALUES (\"Tony\", \"tony.stark@avengers.com\", 40, 185.42, X'010203', \"24.5\")");
        _sqliteNetConnection?.Execute("INSERT INTO job (name, salary) VALUES (\"Team Lead\", 100000)");
        _sqliteNetConnection?.Execute("INSERT INTO job (name, salary) VALUES (\"CEO\", 200000)");

        List<ContactJob> contactJobs = new();

        // act
        _database!.GetJobCombos(contactJobs);

        // assert
        Assert.That(contactJobs.Count, Is.EqualTo(4));
        Assert.That(contactJobs[0].ContactName, Is.EqualTo(steveContact.Name));
        Assert.That(contactJobs[0].Age, Is.EqualTo(steveContact.Age));
        Assert.That(contactJobs[0].Email, Is.EqualTo(steveContact.Email));
        Assert.That(contactJobs[0].PrivateKey, Is.EqualTo(steveContact.PrivateKey));
        Assert.That(contactJobs[0].JobName, Is.EqualTo(teamLead.Name));
        Assert.That(contactJobs[0].Salary, Is.EqualTo(teamLead.Salary));

        Assert.That(contactJobs[1].ContactName, Is.EqualTo(steveContact.Name));
        Assert.That(contactJobs[1].Age, Is.EqualTo(steveContact.Age));
        Assert.That(contactJobs[1].Email, Is.EqualTo(steveContact.Email));
        Assert.That(contactJobs[1].PrivateKey, Is.EqualTo(steveContact.PrivateKey));
        Assert.That(contactJobs[1].JobName, Is.EqualTo(ceo.Name));
        Assert.That(contactJobs[1].Salary, Is.EqualTo(ceo.Salary));

        Assert.That(contactJobs[2].ContactName, Is.EqualTo(tonyContact.Name));
        Assert.That(contactJobs[2].Age, Is.EqualTo(tonyContact.Age));
        Assert.That(contactJobs[2].Email, Is.EqualTo(tonyContact.Email));
        Assert.That(contactJobs[2].PrivateKey, Is.EqualTo(tonyContact.PrivateKey));
        Assert.That(contactJobs[2].Mana, Is.EqualTo(tonyContact.Mana));
        Assert.That(contactJobs[2].JobName, Is.EqualTo(teamLead.Name));
        Assert.That(contactJobs[2].Salary, Is.EqualTo(teamLead.Salary));

        Assert.That(contactJobs[3].ContactName, Is.EqualTo(tonyContact.Name));
        Assert.That(contactJobs[3].Age, Is.EqualTo(tonyContact.Age));
        Assert.That(contactJobs[3].Email, Is.EqualTo(tonyContact.Email));
        Assert.That(contactJobs[3].PrivateKey, Is.EqualTo(tonyContact.PrivateKey));
        Assert.That(contactJobs[3].Mana, Is.EqualTo(tonyContact.Mana));
        Assert.That(contactJobs[3].JobName, Is.EqualTo(ceo.Name));
        Assert.That(contactJobs[3].Salary, Is.EqualTo(ceo.Salary));
    }
}