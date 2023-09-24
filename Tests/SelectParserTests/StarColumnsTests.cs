using SqlSrcGen.Generator;

namespace Tests.SelectParserTests;

public class StarColumnsTests
{
    readonly DatabaseInfo _databaseInfo;
    readonly SelectParser _selectParser;

    public StarColumnsTests()
    {
        _databaseInfo = new DatabaseInfo();
        var expressionParser = new ExpressionParser(
            _databaseInfo,
            new LiteralValueParser(),
            new TypeNameParser(),
            new CollationParser());
        _selectParser = new SelectParser(_databaseInfo, expressionParser);
    }

    [Test]
    public void Select_StarColumnsOneTable_CorrectColumns()
    {
        // arrange
        var table = new Table();
        table.SqlName = "contact";
        table.AddColumn(new Column() { SqlName = "name", CSharpName = "Name" });
        table.AddColumn(new Column() { SqlName = "email", CSharpName = "Email" });
        _databaseInfo.Tables.Add(table);

        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize("SELECT * FROM contact").ToArray().AsSpan();
        int index = 0;
        var queryInfo = new QueryInfo();

        // act
        _selectParser.Parse(ref index, tokens, queryInfo);
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
        var contactsTable = new Table()
        {
            SqlName = "contact"
        };
        contactsTable.AddColumn(new Column() { SqlName = "name", CSharpName = "Name" });
        contactsTable.AddColumn(new Column() { SqlName = "email", CSharpName = "Email" });
        _databaseInfo.Tables.Add(contactsTable);

        var jobTable = new Table()
        {
            SqlName = "job"
        };
        jobTable.AddColumn(new Column() { SqlName = "title", CSharpName = "Title" });
        jobTable.AddColumn(new Column() { SqlName = "salary", CSharpName = "Salary" });
        _databaseInfo.Tables.Add(jobTable);

        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize("SELECT * FROM contact, job").ToArray().AsSpan();
        int index = 0;
        var queryInfo = new QueryInfo();

        // act
        _selectParser.Parse(ref index, tokens, queryInfo);
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
        var contactsTable = new Table()
        {
            SqlName = "contact",
            CSharpName = "Contact"
        };
        contactsTable.AddColumn(new Column() { SqlName = "name", CSharpName = "Name" });
        contactsTable.AddColumn(new Column() { SqlName = "email", CSharpName = "Email" });
        _databaseInfo.Tables.Add(contactsTable);

        var jobTable = new Table()
        {
            SqlName = "job",
            CSharpName = "Job"
        };
        jobTable.AddColumn(new Column() { SqlName = "name", CSharpName = "Name" });
        jobTable.AddColumn(new Column() { SqlName = "title", CSharpName = "Title" });
        jobTable.AddColumn(new Column() { SqlName = "salary", CSharpName = "Salary" });
        _databaseInfo.Tables.Add(jobTable);

        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize("SELECT * FROM contact, job").ToArray().AsSpan();
        int index = 0;
        var queryInfo = new QueryInfo();

        // act
        _selectParser.Parse(ref index, tokens, queryInfo);
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
        var contactsTable = new Table()
        {
            SqlName = "contact",
            CSharpName = "Contact"
        };
        contactsTable.AddColumn(new Column() { SqlName = "name", CSharpName = "Name" });
        _databaseInfo.Tables.Add(contactsTable);

        var jobTable = new Table()
        {
            SqlName = "job",
            CSharpName = "Job"
        };
        jobTable.AddColumn(new Column() { SqlName = "name", CSharpName = "Name" });
        _databaseInfo.Tables.Add(jobTable);

        var locationTable = new Table()
        {
            SqlName = "location",
            CSharpName = "Location"
        };
        locationTable.AddColumn(new Column() { SqlName = "name", CSharpName = "Name" });
        _databaseInfo.Tables.Add(locationTable);

        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize("SELECT * FROM contact, job, location").ToArray().AsSpan();
        int index = 0;
        var queryInfo = new QueryInfo();

        // act
        _selectParser.Parse(ref index, tokens, queryInfo);
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
        var contactsTable = new Table()
        {
            SqlName = "contact",
            CSharpName = "Contact"
        };
        contactsTable.AddColumn(new Column() { SqlName = "name", CSharpName = "Name" });
        contactsTable.AddColumn(new Column() { SqlName = "contactName", CSharpName = "ContactName" });
        _databaseInfo.Tables.Add(contactsTable);

        var jobTable = new Table()
        {
            SqlName = "job",
            CSharpName = "Job"
        };
        jobTable.AddColumn(new Column() { SqlName = "name", CSharpName = "Name" });
        _databaseInfo.Tables.Add(jobTable);

        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize("SELECT * FROM contact, job").ToArray().AsSpan();
        int index = 0;
        var queryInfo = new QueryInfo();

        // act
        _selectParser.Parse(ref index, tokens, queryInfo);
        queryInfo.Process();

        // assert
        Assert.That(queryInfo.Columns[0].CSharpName, Is.EqualTo("ContactName1"));
        Assert.That(queryInfo.Columns[1].CSharpName, Is.EqualTo("ContactName"));
        Assert.That(queryInfo.Columns[2].CSharpName, Is.EqualTo("JobName"));
    }

    [Test]
    public void Select_NamedColumnsOneTable_CorrectColumns()
    {
        // arrange
        var table = new Table();
        table.SqlName = "contact";
        table.AddColumn(new Column() { SqlName = "name", CSharpName = "Name" });
        table.AddColumn(new Column() { SqlName = "email", CSharpName = "Email" });
        table.AddColumn(new Column() { SqlName = "age", CSharpName = "Age" });
        _databaseInfo.Tables.Add(table);

        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize("SELECT name, age FROM contact").ToArray().AsSpan();
        int index = 0;
        var queryInfo = new QueryInfo();

        // act
        _selectParser.Parse(ref index, tokens, queryInfo);
        queryInfo.Process();

        // assert
        Assert.That(queryInfo.Columns[0].SqlName, Is.EqualTo("name"));
        Assert.That(queryInfo.Columns[0].CSharpName, Is.EqualTo("Name"));
        Assert.That(queryInfo.Columns[1].SqlName, Is.EqualTo("age"));
        Assert.That(queryInfo.Columns[1].CSharpName, Is.EqualTo("Age"));
    }

    [Test]
    public void Select_AmbiguousColumn_InvalidSqlException()
    {
        // arrange
        var contactTable = new Table { SqlName = "contact" };
        contactTable.AddColumn(new Column() { SqlName = "name", CSharpName = "Name" });
        contactTable.AddColumn(new Column() { SqlName = "email", CSharpName = "Email" });
        contactTable.AddColumn(new Column() { SqlName = "age", CSharpName = "Age" });
        _databaseInfo.Tables.Add(contactTable);

        var jobTable = new Table { SqlName = "job" };
        jobTable.AddColumn(new Column() { SqlName = "name", CSharpName = "Name" });
        jobTable.AddColumn(new Column() { SqlName = "salary", CSharpName = "Salary" });
        _databaseInfo.Tables.Add(jobTable);

        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize("SELECT name FROM contact, job").ToArray().AsSpan();
        int index = 0;
        var queryInfo = new QueryInfo();

        // act
        try
        {
            _selectParser.Parse(ref index, tokens, queryInfo);
            queryInfo.Process();

            // assert
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            Assert.That(exception.Message, Is.EqualTo("Ambiguous column name"));
            Assert.That(exception.Token!.Position, Is.EqualTo(7));
        }
    }

    [Test]
    public void Select_FullyQualifiedColumns_CorrectColumns()
    {
        // arrange
        var contactTable = new Table { SqlName = "contact", CSharpName = "Contact" };
        contactTable.AddColumn(new Column() { SqlName = "name", CSharpName = "Name" });
        contactTable.AddColumn(new Column() { SqlName = "email", CSharpName = "Email" });
        contactTable.AddColumn(new Column() { SqlName = "age", CSharpName = "Age" });
        _databaseInfo.Tables.Add(contactTable);

        var jobTable = new Table { SqlName = "job", CSharpName = "Job" };
        jobTable.AddColumn(new Column() { SqlName = "name", CSharpName = "Name" });
        jobTable.AddColumn(new Column() { SqlName = "salary", CSharpName = "Salary" });
        _databaseInfo.Tables.Add(jobTable);

        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize("SELECT contact.name, job.name, salary FROM contact, job").ToArray().AsSpan();
        int index = 0;
        var queryInfo = new QueryInfo();

        // act
        _selectParser.Parse(ref index, tokens, queryInfo);
        queryInfo.Process();

        // assert
        Assert.That(queryInfo.Columns[0].SqlName, Is.EqualTo("name"));
        Assert.That(queryInfo.Columns[0].CSharpName, Is.EqualTo("ContactName"));
        Assert.That(queryInfo.Columns[1].SqlName, Is.EqualTo("name"));
        Assert.That(queryInfo.Columns[1].CSharpName, Is.EqualTo("JobName"));
        Assert.That(queryInfo.Columns[2].SqlName, Is.EqualTo("salary"));
        Assert.That(queryInfo.Columns[2].CSharpName, Is.EqualTo("Salary"));
    }
}