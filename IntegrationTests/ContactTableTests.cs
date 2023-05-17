using SqlSrcGen;
using SQLite;

namespace Tests;

public class ContactTableTests
{
	const string DatabaseName = "database.sql";

	Database? _database;
	SQLiteConnection? _sqliteNetConnection;

	[OneTimeSetUp]
	public void FixtureSetup()
	{
		if (File.Exists(DatabaseName))
		{
			File.Delete(DatabaseName);
		}
	}

	[OneTimeTearDown]
	public void FixtureTearDown()
	{
		_database?.Dispose();
	}

	[SetUp]
	public void Setup()
	{
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
}