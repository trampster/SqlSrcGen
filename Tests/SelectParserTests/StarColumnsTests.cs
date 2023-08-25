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
        table.SqlName = "contacts";
        table.AddColumn(new Column() { SqlName = "name", CSharpName = "Name" });
        table.AddColumn(new Column() { SqlName = "email", CSharpName = "Email" });
        databaseInfo.Tables.Add(table);

        SelectParser parser = new SelectParser(databaseInfo);
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize("SELECT * FROM contacts").ToArray().AsSpan();
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
}