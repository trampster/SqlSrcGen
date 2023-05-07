namespace Benchmarks;

using BenchmarkDotNet.Attributes;
using SqlSrcGen;
using SQLite;

public class QueryBenchmark
{
    readonly Database _database;
    readonly List<Contact> _list = new();
    readonly SQLiteConnection _sqliteNetConnection;

    public QueryBenchmark()
    {
        string databaseName = "database1.sql";

        if (File.Exists(databaseName))
        {
            File.Delete(databaseName);
        }

        _database = new Database(databaseName);

        _database.CreateContactTable();

        _database.InsertContact(new Contact() { Name = "Luke", Email = "luke@rebels.com" });

        _sqliteNetConnection = new SQLiteConnection(databaseName);

    }

    [Benchmark]
    public void SqlSrcGen()
    {
        _database.AllContacts(_list);
    }

    [Benchmark]
    public void SqliteNet()
    {
        _sqliteNetConnection.Query<Contact>("SELECT * FROM contact;");
    }
}