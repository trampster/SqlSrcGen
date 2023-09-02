using SqlSrcGen.Generator;

namespace Tests.SelectParserTests;

public class StarColumnsTests
{
    [Test]
    public void Select_StarColumnsOneTable_CorrectColumns()
    {
        // arrange
        var databaseInfo = new DatabaseInfo();
        var table = new Table();
        table.SqlName = "contact";
        table.AddColumn(new Column() { SqlName = "name", CSharpName = "Name" });
        table.AddColumn(new Column() { SqlName = "email", CSharpName = "Email" });
        databaseInfo.Tables.Add(table);

        SelectParser parser = new SelectParser(databaseInfo);
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize("SELECT * FROM contact").ToArray().AsSpan();
        int index = 0;
        var queryInfo = new QueryInfo();

        // act
        parser.Parse(ref index, tokens, queryInfo);
        queryInfo.Process();

        // assert
        Assert.That(queryInfo.Columns[0].SqlName, Is.EqualTo("name"));
        Assert.That(queryInfo.Columns[0].CSharpName, Is.EqualTo("Name"));
        Assert.That(queryInfo.Columns[1].SqlName, Is.EqualTo("email"));
        Assert.That(queryInfo.Columns[1].CSharpName, Is.EqualTo("Email"));
    }

    [Test]
    public void Select_StarColumnsTwoTables_HasAllColumnsFromBothTables()
    {
        // arrange
        var databaseInfo = new DatabaseInfo();

        var contactsTable = new Table()
        {
            SqlName = "contact"
        };
        contactsTable.AddColumn(new Column() { SqlName = "name", CSharpName = "Name" });
        contactsTable.AddColumn(new Column() { SqlName = "email", CSharpName = "Email" });
        databaseInfo.Tables.Add(contactsTable);

        var jobTable = new Table()
        {
            SqlName = "job"
        };
        jobTable.AddColumn(new Column() { SqlName = "title", CSharpName = "Title" });
        jobTable.AddColumn(new Column() { SqlName = "salary", CSharpName = "Salary" });
        databaseInfo.Tables.Add(jobTable);

        SelectParser parser = new SelectParser(databaseInfo);
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize("SELECT * FROM contact, job").ToArray().AsSpan();
        int index = 0;
        var queryInfo = new QueryInfo();

        // act
        parser.Parse(ref index, tokens, queryInfo);
        queryInfo.Process();

        // assert
        Assert.That(queryInfo.Columns[0].SqlName, Is.EqualTo("name"));
        Assert.That(queryInfo.Columns[0].CSharpName, Is.EqualTo("Name"));
        Assert.That(queryInfo.Columns[1].SqlName, Is.EqualTo("email"));
        Assert.That(queryInfo.Columns[1].CSharpName, Is.EqualTo("Email"));
        Assert.That(queryInfo.Columns[2].SqlName, Is.EqualTo("title"));
        Assert.That(queryInfo.Columns[2].CSharpName, Is.EqualTo("Title"));
        Assert.That(queryInfo.Columns[3].SqlName, Is.EqualTo("salary"));
        Assert.That(queryInfo.Columns[3].CSharpName, Is.EqualTo("Salary"));
    }

    [Test]
    public void Select_StarColumnsTwoDulicateColumName_UsesTableNameToDifferentiate()
    {
        // arrange
        var databaseInfo = new DatabaseInfo();

        var contactsTable = new Table()
        {
            SqlName = "contact",
            CSharpName = "Contact"
        };
        contactsTable.AddColumn(new Column() { SqlName = "name", CSharpName = "Name" });
        contactsTable.AddColumn(new Column() { SqlName = "email", CSharpName = "Email" });
        databaseInfo.Tables.Add(contactsTable);

        var jobTable = new Table()
        {
            SqlName = "job",
            CSharpName = "Job"
        };
        jobTable.AddColumn(new Column() { SqlName = "name", CSharpName = "Name" });
        jobTable.AddColumn(new Column() { SqlName = "title", CSharpName = "Title" });
        jobTable.AddColumn(new Column() { SqlName = "salary", CSharpName = "Salary" });
        databaseInfo.Tables.Add(jobTable);

        SelectParser parser = new SelectParser(databaseInfo);
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize("SELECT * FROM contact, job").ToArray().AsSpan();
        int index = 0;
        var queryInfo = new QueryInfo();

        // act
        parser.Parse(ref index, tokens, queryInfo);
        queryInfo.Process();

        // assert
        Assert.That(queryInfo.Columns[0].SqlName, Is.EqualTo("name"));
        Assert.That(queryInfo.Columns[0].CSharpName, Is.EqualTo("ContactName"));
        Assert.That(queryInfo.Columns[1].SqlName, Is.EqualTo("email"));
        Assert.That(queryInfo.Columns[1].CSharpName, Is.EqualTo("Email"));
        Assert.That(queryInfo.Columns[2].SqlName, Is.EqualTo("name"));
        Assert.That(queryInfo.Columns[2].CSharpName, Is.EqualTo("JobName"));
        Assert.That(queryInfo.Columns[3].SqlName, Is.EqualTo("title"));
        Assert.That(queryInfo.Columns[3].CSharpName, Is.EqualTo("Title"));
        Assert.That(queryInfo.Columns[4].SqlName, Is.EqualTo("salary"));
        Assert.That(queryInfo.Columns[4].CSharpName, Is.EqualTo("Salary"));
    }

    [Test]
    public void Select_StarColumnsThreeDulicateColumName_UsesTableNameToDifferentiate()
    {
        // arrange
        var databaseInfo = new DatabaseInfo();

        var contactsTable = new Table()
        {
            SqlName = "contact",
            CSharpName = "Contact"
        };
        contactsTable.AddColumn(new Column() { SqlName = "name", CSharpName = "Name" });
        databaseInfo.Tables.Add(contactsTable);

        var jobTable = new Table()
        {
            SqlName = "job",
            CSharpName = "Job"
        };
        jobTable.AddColumn(new Column() { SqlName = "name", CSharpName = "Name" });
        databaseInfo.Tables.Add(jobTable);

        var locationTable = new Table()
        {
            SqlName = "location",
            CSharpName = "Location"
        };
        locationTable.AddColumn(new Column() { SqlName = "name", CSharpName = "Name" });
        databaseInfo.Tables.Add(locationTable);

        SelectParser parser = new SelectParser(databaseInfo);
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize("SELECT * FROM contact, job, location").ToArray().AsSpan();
        int index = 0;
        var queryInfo = new QueryInfo();

        // act
        parser.Parse(ref index, tokens, queryInfo);
        queryInfo.Process();

        // assert
        Assert.That(queryInfo.Columns[0].SqlName, Is.EqualTo("name"));
        Assert.That(queryInfo.Columns[0].CSharpName, Is.EqualTo("ContactName"));
        Assert.That(queryInfo.Columns[1].SqlName, Is.EqualTo("name"));
        Assert.That(queryInfo.Columns[1].CSharpName, Is.EqualTo("JobName"));
        Assert.That(queryInfo.Columns[2].SqlName, Is.EqualTo("name"));
        Assert.That(queryInfo.Columns[2].CSharpName, Is.EqualTo("LocationName"));
    }

    [Test]
    public void Select_ConflictingColumnsAfterAddingTablename_AddsNumber()
    {
        // arrange
        var databaseInfo = new DatabaseInfo();

        var contactsTable = new Table()
        {
            SqlName = "contact",
            CSharpName = "Contact"
        };
        contactsTable.AddColumn(new Column() { SqlName = "name", CSharpName = "Name" });
        contactsTable.AddColumn(new Column() { SqlName = "contactName", CSharpName = "ContactName" });
        databaseInfo.Tables.Add(contactsTable);

        var jobTable = new Table()
        {
            SqlName = "job",
            CSharpName = "Job"
        };
        jobTable.AddColumn(new Column() { SqlName = "name", CSharpName = "Name" });
        databaseInfo.Tables.Add(jobTable);

        SelectParser parser = new SelectParser(databaseInfo);
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize("SELECT * FROM contact, job").ToArray().AsSpan();
        int index = 0;
        var queryInfo = new QueryInfo();

        // act
        parser.Parse(ref index, tokens, queryInfo);
        queryInfo.Process();

        // assert
        Assert.That(queryInfo.Columns[0].CSharpName, Is.EqualTo("ContactName1"));
        Assert.That(queryInfo.Columns[1].CSharpName, Is.EqualTo("ContactName"));
        Assert.That(queryInfo.Columns[2].CSharpName, Is.EqualTo("JobName"));
    }
}