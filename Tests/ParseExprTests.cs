using Moq;
using SqlSrcGen;
using SqlSrcGen.Generator;

namespace Tests;

public class ParseExprTests
{
    readonly LiteralValueParser _literalValueParser;
    readonly DatabaseInfo _databaseInfo;
    readonly ExpressionParser _expressionParser;
    readonly TypeNameParser _typeNameParser;
    readonly CollationParser _collationParser;
    readonly Query _query;

    public ParseExprTests()
    {
        _databaseInfo = new DatabaseInfo();
        _query = new Query();
        _literalValueParser = new LiteralValueParser();
        _typeNameParser = new TypeNameParser();
        _collationParser = new CollationParser();
        _collationParser.DiagnosticsReporter = Mock.Of<IDiagnosticsReporter>();
        _expressionParser = new ExpressionParser(_databaseInfo, _literalValueParser, _typeNameParser, _collationParser);
        _expressionParser.Query = _query;
    }

    [TestCase("'mystring'")]
    [TestCase("null")]
    [TestCase("NULL")]
    [TestCase("FALSE")]
    [TestCase("CURRENT_TIME")]
    [TestCase("CURRENT_DATE")]
    [TestCase("CURRENT_TIMESTAMP")]
    [TestCase("123.123e+12")]
    [TestCase("+123.123e+12")]
    [TestCase("-123.123e+12")]
    [TestCase("x'A1B2C3'")]
    public void Parse_Litterals_Parsed(string literalValue)
    {
        // arrange
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize(literalValue).ToArray().AsSpan();

        int index = 0;

        // act
        var result = _expressionParser.Parse(ref index, tokens, null);

        // assert
        Assert.That(result, Is.True);
        Assert.That(index, Is.EqualTo(tokens.Length));
    }

    [TestCase("+'name'")]
    [TestCase("- 1234")]
    [TestCase("-1234")]
    [TestCase("~1234")]
    [TestCase("NOT 1234")]
    public void Parse_UnaryOperator_Parsed(string literalValue)
    {
        // arrange
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize(literalValue).ToArray().AsSpan();

        int index = 0;

        // act
        var result = _expressionParser.Parse(ref index, tokens, null);

        // assert
        Assert.That(result, Is.True);
        Assert.That(index, Is.EqualTo(tokens.Length));
    }

    [TestCase("?")]
    [TestCase("?1234")]
    [TestCase(":name")]
    [TestCase("@name")]
    [TestCase("$name")]
    public void Parse_BindingParameter_Parsed(string literalValue)
    {
        // arrange
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize(literalValue).ToArray().AsSpan();

        int index = 0;

        // act
        var result = _expressionParser.Parse(ref index, tokens, null);

        // assert
        Assert.That(result, Is.True);
        Assert.That(index, Is.EqualTo(tokens.Length));
    }

    [TestCase("?name")]
    [TestCase("?-123")]
    public void Parse_BindingParameterNotANumber_Parsed(string literalValue)
    {
        // arrange
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize(literalValue).ToArray().AsSpan();

        int index = 0;

        // act
        try
        {
            var result = _expressionParser.Parse(ref index, tokens, null);
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            Assert.That(exception.Message, Is.EqualTo("Numbered parameter must be a positive integer"));
            Assert.That(exception.Token.Line, Is.EqualTo(0));
            Assert.That(exception.Token.CharacterInLine, Is.EqualTo(0));
        }
    }

    [TestCase(":name")]
    [TestCase("@name")]
    [TestCase("$name")]
    public void Parse_BindingParameterNameAlreadyExists_Parsed(string literalValue)
    {
        // arrange
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize(literalValue).ToArray().AsSpan();
        string prefix = literalValue[0] switch
        {
            ':' => "@",
            '@' => "$",
            '$' => ":",
            _ => throw new ArgumentException("literalValue must start with :, @ or $")
        };
        _query.AddNamedParameter($"{prefix}name", new Token());

        int index = 0;

        // act
        try
        {
            var result = _expressionParser.Parse(ref index, tokens, null);
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            Assert.That(exception.Message, Is.EqualTo("Parameter produces the same c# name as an existing parameter"));
            Assert.That(exception.Token.Line, Is.EqualTo(0));
            Assert.That(exception.Token.CharacterInLine, Is.EqualTo(0));
        }
    }

    [TestCase("columnName")]
    [TestCase("tableName.myColumn")]
    public void Parse_ColumnIdentifier_Parsed(string literalValue)
    {
        // arrange
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize(literalValue).ToArray().AsSpan();

        var table = new Table()
        {
            SqlName = "tableName"
        };
        table.AddColumn(new Column()
        {
            SqlName = "myColumn"
        });

        _databaseInfo.Tables.Add(table);

        var currentTable = new Table();
        currentTable.AddColumn(new Column
        {
            SqlName = "columnName"
        });

        int index = 0;

        // act
        var result = _expressionParser.Parse(ref index, tokens, currentTable);

        // assert
        Assert.That(result, Is.True);
        Assert.That(index, Is.EqualTo(tokens.Length));
    }

    [Test]
    public void Parse_ThreePartColumnIdentifier_NotSupported()
    {
        // arrange
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize("one.two.three").ToArray().AsSpan();

        int index = 0;

        // act
        try
        {
            var result = _expressionParser.Parse(ref index, tokens, null);
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            Assert.That(exception.Message, Is.EqualTo("Attached databases are not supported"));
            Assert.That(exception.Token.CharacterInLine, Is.EqualTo(0));
            Assert.That(exception.Token.Position, Is.EqualTo(0));
            Assert.That(exception.Token.Line, Is.EqualTo(0));
        }
    }

    [TestCase("1 || 2")]
    [TestCase("1||2")]
    [TestCase("1 -> 2")]
    [TestCase("1->2")]
    [TestCase("1 ->> 2")]
    [TestCase("1->>2")]
    [TestCase("1 * 2")]
    [TestCase("1*2")]
    [TestCase("1 / 2")]
    [TestCase("1/2")]
    [TestCase("1 % 2")]
    [TestCase("1%2")]
    [TestCase("1 + 2")]
    [TestCase("1+2")]
    [TestCase("1 - 2")]
    [TestCase("1-2")]
    [TestCase("1 & 2")]
    [TestCase("1&2")]
    [TestCase("1 | 2")]
    [TestCase("1|2")]
    [TestCase("1 << 2")]
    [TestCase("1<<2")]
    [TestCase("1 >> 2")]
    [TestCase("1>>2")]
    [TestCase("1 < 2")]
    [TestCase("1<2")]
    [TestCase("1 > 2")]
    [TestCase("1>2")]
    [TestCase("1 <= 2")]
    [TestCase("1<=2")]
    [TestCase("1 >= 2")]
    [TestCase("1>=2")]
    [TestCase("1 = 2")]
    [TestCase("1=2")]
    [TestCase("1 == 2")]
    [TestCase("1==2")]
    [TestCase("1 <> 2")]
    [TestCase("1<>2")]
    [TestCase("1 != 2")]
    [TestCase("1!=2")]
    [TestCase("1 AND 2")]
    [TestCase("1 OR 2")]
    public void Parse_BooleanOperator_Parsed(string literalValue)
    {
        // arrange
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize(literalValue).ToArray().AsSpan();

        int index = 0;

        // act
        var result = _expressionParser.Parse(ref index, tokens, null);

        // assert
        Assert.That(result, Is.True);
        Assert.That(index, Is.EqualTo(tokens.Length));
    }

    [TestCase("abs(12.5)")]
    [TestCase("count(*)")]
    [TestCase("max(salary)")]
    [TestCase("instr('firstname','name')")]
    [TestCase("COUNT(DISTINCT salary)")]
    [TestCase("COUNT(*) FILTER (WHERE salary > 50000)")]
    [TestCase("min(salary) over(partition by type)")]
    [TestCase("min(salary) over(partition by type order by salary)")]
    [TestCase("min(salary) over(range unbounded preceding exclude no others)")]
    [TestCase("min(salary) over(range unbounded preceding exclude current row)")]
    [TestCase("min(salary) over(range unbounded preceding exclude group)")]
    [TestCase("min(salary) over(range unbounded preceding exclude ties)")]
    [TestCase("min(salary) over(range unbounded preceding)")]
    [TestCase("min(salary) over(range current row)")]
    [TestCase("min(salary) over(range current row)")]
    [TestCase("min(salary) over(rows current row)")]
    [TestCase("min(salary) over(groups current row)")]
    [TestCase("min(salary) over(order by salary asc nulls first)")]
    public void Parse_Function_Parsed(string literalValue)
    {
        // arrange
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize(literalValue).ToArray().AsSpan();
        var table = new Table();
        table.AddColumn(new Column() { SqlName = "salary" });
        table.AddColumn(new Column() { SqlName = "type" });

        int index = 0;

        // act
        var result = _expressionParser.Parse(ref index, tokens, table);

        // assert
        Assert.That(result, Is.True);
        Assert.That(index, Is.EqualTo(tokens.Length));
    }

    [TestCase("(min(salary), abs(12.5), COUNT(*))")]
    public void Parse_ExprList_Parsed(string literalValue)
    {
        // arrange
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize(literalValue).ToArray().AsSpan();
        var table = new Table();
        table.AddColumn(new Column() { SqlName = "salary" });

        int index = 0;

        // act
        var result = _expressionParser.Parse(ref index, tokens, table);

        // assert
        Assert.That(result, Is.True);
        Assert.That(index, Is.EqualTo(tokens.Length));
    }

    [TestCase("CAST (123 as FLOAT(12, 7))")]
    public void Parse_Cast_Parsed(string literalValue)
    {
        // arrange
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize(literalValue).ToArray().AsSpan();
        var table = new Table();
        table.AddColumn(new Column() { SqlName = "salary" });

        int index = 0;

        // act
        var result = _expressionParser.Parse(ref index, tokens, table);

        // assert
        Assert.That(result, Is.True);
        Assert.That(index, Is.EqualTo(tokens.Length));
    }

    [TestCase("1234 COLLATE nocase")] //TODO: collate would be part of a select normally, update test once we support select
    public void Parse_Collate_Parsed(string literalValue)
    {
        // arrange
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize(literalValue).ToArray().AsSpan();
        var table = new Table();

        int index = 0;

        // act
        var result = _expressionParser.Parse(ref index, tokens, table);

        // assert
        Assert.That(result, Is.True);
        Assert.That(index, Is.EqualTo(tokens.Length));
    }

    [TestCase("salary like 'Wild%'")]
    [TestCase("salary like 'Wild%' escape '\'")]
    [TestCase("salary not like 'Wild%'")]
    public void Parse_Like_Parsed(string literalValue)
    {
        // arrange
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize(literalValue).ToArray().AsSpan();
        var table = new Table();
        table.AddColumn(new Column() { SqlName = "salary" });

        int index = 0;

        // act
        var result = _expressionParser.Parse(ref index, tokens, table);

        // assert
        Assert.That(result, Is.True);
        Assert.That(index, Is.EqualTo(tokens.Length));
    }

    [TestCase("salary GLOB 'XXXX*'")]
    [TestCase("salary NOT GLOB 'XXXX*'")]
    [TestCase("salary REGEXP '\b3\b'")]
    [TestCase("salary NOT REGEXP '\b3\b'")]
    [TestCase("salary MATCH 'andrew OR bill'")]
    [TestCase("salary NOT MATCH 'andrew OR bill'")]
    public void Parse_Glob_Regexp_Match_Parsed(string literalValue)
    {
        // arrange
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize(literalValue).ToArray().AsSpan();
        var table = new Table();
        table.AddColumn(new Column() { SqlName = "salary" });

        int index = 0;

        // act
        var result = _expressionParser.Parse(ref index, tokens, table);

        // assert
        Assert.That(result, Is.True);
        Assert.That(index, Is.EqualTo(tokens.Length));
    }

    [TestCase("salary ISNULL")]
    [TestCase("salary NOTNULL")]
    [TestCase("salary NOT NULL")]
    public void Parse_NULL_Parsed(string literalValue)
    {
        // arrange
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize(literalValue).ToArray().AsSpan();
        var table = new Table();
        table.AddColumn(new Column() { SqlName = "salary" });

        int index = 0;

        // act
        var result = _expressionParser.Parse(ref index, tokens, table);

        // assert
        Assert.That(result, Is.True);
        Assert.That(index, Is.EqualTo(tokens.Length));
    }

    [TestCase("salary is distinct from @master_id")]
    [TestCase("salary is not distinct from @master_id")]
    [TestCase("salary is not @master_id")]
    public void Parse_Is_Parsed(string literalValue)
    {
        // arrange
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize(literalValue).ToArray().AsSpan();
        var table = new Table();
        table.AddColumn(new Column() { SqlName = "salary" });

        int index = 0;

        // act
        var result = _expressionParser.Parse(ref index, tokens, table);

        // assert
        Assert.That(result, Is.True);
        Assert.That(index, Is.EqualTo(tokens.Length));
    }

    [TestCase("salary between 20000 and 50000")]
    [TestCase("salary not between 20000 and 50000")]
    public void Parse_Between_Parsed(string literalValue)
    {
        // arrange
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize(literalValue).ToArray().AsSpan();
        var table = new Table();
        table.AddColumn(new Column() { SqlName = "salary" });

        int index = 0;

        // act
        var result = _expressionParser.Parse(ref index, tokens, table);

        // assert
        Assert.That(result, Is.True);
        Assert.That(index, Is.EqualTo(tokens.Length));
    }

    [TestCase("salary in (1,2,3)")]
    [TestCase("salary not in (1,2,3)")]
    [TestCase("2 in Job")]
    [TestCase("2 not in Job")]
    [TestCase("2 in someFunction(1,2,3)")]
    [TestCase("2 not in someFunction(1,2,3)")]
    public void Parse_In_Parsed(string literalValue)
    {
        // arrange
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize(literalValue).ToArray().AsSpan();
        var table = new Table();
        table.AddColumn(new Column() { SqlName = "salary" });

        _databaseInfo.Tables.Add(new Table() { SqlName = "Job" });

        int index = 0;

        // act
        var result = _expressionParser.Parse(ref index, tokens, table);

        // assert
        Assert.That(result, Is.True);
        Assert.That(index, Is.EqualTo(tokens.Length));
    }

    [TestCase("case salary when 1000 then 'poor' when 100000 then 'rich' end")]
    [TestCase("case salary when 1000 then 'poor' else 'unknown' end")]
    [TestCase("case when 1000 then 'poor' else 'unknown' end")]
    public void Parse_Case_Parsed(string literalValue)
    {
        // arrange
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize(literalValue).ToArray().AsSpan();
        var table = new Table();
        table.AddColumn(new Column() { SqlName = "salary" });

        int index = 0;

        // act
        var result = _expressionParser.Parse(ref index, tokens, table);

        // assert
        Assert.That(result, Is.True);
        Assert.That(index, Is.EqualTo(tokens.Length));
    }

    [TestCase("raise ( ignore )")]
    [TestCase("raise ( rollback, 'my error message' )")]
    [TestCase("raise ( abort, 'my error message' )")]
    [TestCase("raise ( fail, 'my error message' )")]
    public void Parse_Raise_Parsed(string literalValue)
    {
        // arrange
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize(literalValue).ToArray().AsSpan();
        var table = new Table();
        table.AddColumn(new Column() { SqlName = "salary" });

        int index = 0;

        // act
        var result = _expressionParser.Parse(ref index, tokens, table);

        // assert
        Assert.That(result, Is.True);
        Assert.That(index, Is.EqualTo(tokens.Length));
    }
}