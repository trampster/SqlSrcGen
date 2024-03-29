using Moq;
using SqlSrcGen.Generator;

namespace Tests;

public class SqlGeneratorTests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void GeneratesCorrectRecord()
    {
        // arrange 
        var generator = new SqlGenerator();
        var stringBuilder = new SourceBuilder();
        var databaseInfo = new DatabaseInfo();
        var table = new Table()
        {
            SqlName = "contact",
            CSharpName = "Contact",
        };
        table.AddColumn(new Column() { SqlName = "name", CSharpName = "Name", SqlType = "Text", CSharpType = "string" });
        table.AddColumn(new Column() { SqlName = "email", CSharpName = "Email", SqlType = "Text", CSharpType = "string" });
        databaseInfo.Tables.Add(table);

        string expectedSource =
            "public record Contact" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    public string Name { get; set; } = \"\";" + Environment.NewLine +
            "    public string Email { get; set; } = \"\";" + Environment.NewLine +
            "}" + Environment.NewLine;

        // act
        generator.GenerateDatabaseObjects(databaseInfo, stringBuilder, Mock.Of<IDiagnosticsReporter>());

        // assert
        var source = stringBuilder.ToString();
        Assert.That(source, Is.EqualTo(expectedSource));
    }

    [Test]
    public void ProcessSqlSchema_TextColumnNull_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema("CREATE TABLE contact (name Text);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        var columns = databaseInfo.Tables[0].Columns.ToArray();
        Assert.That(columns[0].SqlName, Is.EqualTo("name"));
        Assert.That(columns[0].CSharpName, Is.EqualTo("Name"));
        Assert.That(columns[0].SqlType, Is.EqualTo("Text"));
        Assert.That(columns[0].CSharpType, Is.EqualTo("string?"));
        Assert.That(columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.TEXT));
        Assert.That(columns[0].NotNull, Is.False);
    }

    [Test]
    public void ProcessSqlSchema_IntegerColumnNull_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema("CREATE TABLE contact (age Integer);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        var columns = databaseInfo.Tables[0].Columns.ToArray();
        Assert.That(columns[0].SqlName, Is.EqualTo("age"));
        Assert.That(columns[0].CSharpName, Is.EqualTo("Age"));
        Assert.That(columns[0].SqlType, Is.EqualTo("Integer"));
        Assert.That(columns[0].CSharpType, Is.EqualTo("long?"));
        Assert.That(columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.INTEGER));
    }

    [Test]
    public void ProcessSqlSchema_RealColumnNull_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema("CREATE TABLE contact (height Real);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        var columns = databaseInfo.Tables[0].Columns.ToArray();
        Assert.That(columns[0].SqlName, Is.EqualTo("height"));
        Assert.That(columns[0].CSharpName, Is.EqualTo("Height"));
        Assert.That(columns[0].SqlType, Is.EqualTo("Real"));
        Assert.That(columns[0].CSharpType, Is.EqualTo("double?"));
        Assert.That(columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.REAL));
    }

    [Test]
    public void ProcessSqlSchema_BlobColumnNull_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema("CREATE TABLE contact (key Blob);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        var columns = databaseInfo.Tables[0].Columns.ToArray();
        Assert.That(columns[0].SqlName, Is.EqualTo("key"));
        Assert.That(columns[0].CSharpName, Is.EqualTo("Key"));
        Assert.That(columns[0].SqlType, Is.EqualTo("Blob"));
        Assert.That(columns[0].CSharpType, Is.EqualTo("byte[]?"));
        Assert.That(columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.BLOB));
    }

    [Test]
    public void ProcessSqlSchema_NoType_UsesBlob()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema("CREATE TABLE contact (key);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        var columns = databaseInfo.Tables[0].Columns.ToArray();
        Assert.That(columns[0].SqlName, Is.EqualTo("key"));
        Assert.That(columns[0].CSharpName, Is.EqualTo("Key"));
        Assert.That(columns[0].SqlType, Is.EqualTo("blob"));
        Assert.That(columns[0].CSharpType, Is.EqualTo("byte[]?"));
        Assert.That(columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.BLOB));
    }

    [Test]
    public void ProcessSqlSchema_TwoNoType_UsesBlob()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema("CREATE TABLE contact (one, two);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        var columns = databaseInfo.Tables[0].Columns.ToArray();
        Assert.That(columns[0].SqlName, Is.EqualTo("one"));
        Assert.That(columns[0].CSharpName, Is.EqualTo("One"));
        Assert.That(columns[0].SqlType, Is.EqualTo("blob"));
        Assert.That(columns[0].CSharpType, Is.EqualTo("byte[]?"));
        Assert.That(columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.BLOB));

        Assert.That(columns[1].SqlName, Is.EqualTo("two"));
        Assert.That(columns[1].CSharpName, Is.EqualTo("Two"));
        Assert.That(columns[1].SqlType, Is.EqualTo("blob"));
        Assert.That(columns[1].CSharpType, Is.EqualTo("byte[]?"));
        Assert.That(columns[1].TypeAffinity, Is.EqualTo(TypeAffinity.BLOB));
    }

    [Test]
    public void ProcessSqlSchema_NumericColumnNull_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema("CREATE TABLE contact (distance Numeric);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        var columns = databaseInfo.Tables[0].Columns.ToArray();
        Assert.That(columns[0].SqlName, Is.EqualTo("distance"));
        Assert.That(columns[0].CSharpName, Is.EqualTo("Distance"));
        Assert.That(columns[0].SqlType, Is.EqualTo("Numeric"));
        Assert.That(columns[0].CSharpType, Is.EqualTo("Numeric?"));
        Assert.That(columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.NUMERIC));
    }

    [Test]
    public void ProcessSqlSchema_PrimaryKey_ColumnHasPrimaryKeySet()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema("CREATE TABLE contact (name Text primary key, email Text);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        Assert.That(databaseInfo.Tables[0].Columns.ElementAt(0).PrimaryKey, Is.EqualTo(true));
        Assert.That(databaseInfo.Tables[0].Columns.ElementAt(1).PrimaryKey, Is.EqualTo(false));
    }

    [Test]
    public void ProcessSqlSchema_TwoPrimaryKeys_ThrowsInvalidSqlException()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        try
        {
            // act
            generator.ProcessSqlSchema("CREATE TABLE contact (name Text primary key, email Text primary key);", databaseInfo);

            // assert
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            Assert.That(exception.Message, Is.EqualTo("Table already has a primary key"));
            Assert.That(exception.Token!.Line, Is.EqualTo(0));
            Assert.That(exception.Token.CharacterInLine, Is.EqualTo(56));
        }
    }

    [Test]
    public void ProcessSqlSchema_TwoPrimaryWithoutKey_ThrowsInvalidSqlException()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        try
        {
            // act
            generator.ProcessSqlSchema("CREATE TABLE contact (name Text primary);", databaseInfo);

            // assert
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            Assert.That(exception.Message, Is.EqualTo("Invalid column constraint, did you mean 'primary key'?"));
            Assert.That(exception.Token!.Line, Is.EqualTo(0));
            Assert.That(exception.Token.CharacterInLine, Is.EqualTo(32));
        }
    }

    [Test]
    public void ProcessSqlSchema_DuplicateColumnName_ThrowsInvalidSqlException()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        try
        {
            // act
            generator.ProcessSqlSchema("CREATE TABLE contact (name Text, Name Text);", databaseInfo);

            // assert
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            Assert.That(exception.Message, Is.EqualTo("Column name Name already exists in this table"));
            Assert.That(exception.Token!.Line, Is.EqualTo(0));
            Assert.That(exception.Token.CharacterInLine, Is.EqualTo(33));
        }
    }

    [TestCase("TEMP")]
    [TestCase("temp")]
    [TestCase("TEMPORY")]
    [TestCase("tempory")]
    public void ProcessSqlSchema_Tempory_TemporySet(string tempTokenName)
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema($"CREATE {tempTokenName} TABLE contact (name Text);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables.First().Tempory, Is.True);
        Assert.That(databaseInfo.Tables.First().Columns.First().SqlName, Is.EqualTo("name"));
    }

    [Test]
    public void ProcessSqlSchema_NotTempory_TemporyNotSet()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema($"CREATE TABLE contact (name Text);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables.First().Tempory, Is.False);
    }

    [Test]
    public void ProcessSqlSchema_IfNotExists_Parsed()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema($"CREATE TABLE IF NOT EXISTS contact (name Text);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables.First().Columns.First().SqlName, Is.EqualTo("name"));
    }

    [TestCase("IF")]
    [TestCase("if")]
    [TestCase("IF NOT")]
    [TestCase("if not")]
    public void ProcessSqlSchema_InvalidIfNotExists_ThrowInvalidSqlException(string invalidIfNotExists)
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        try
        {
            // act
            generator.ProcessSqlSchema($"CREATE TABLE {invalidIfNotExists} contact (name Text);", databaseInfo);

            // assert
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            Assert.That(exception.Message, Is.EqualTo("Did you mean 'if not exists'?"));
            Assert.That(exception.Token!.Line, Is.EqualTo(0));
            Assert.That(exception.Token.CharacterInLine, Is.EqualTo(13));
        }
    }

    [Test]
    public void ProcessSqlSchema_Schema_ThrowInvalidSqlException()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        try
        {
            // act
            generator.ProcessSqlSchema($"CREATE TABLE my_schema.contact (name Text);", databaseInfo);

            // assert
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            Assert.That(exception.Message, Is.EqualTo("Attached databases are not supported"));
            Assert.That(exception.Token!.Line, Is.EqualTo(0));
            Assert.That(exception.Token.CharacterInLine, Is.EqualTo(13));
        }
    }

    [Test]
    public void ProcessSqlSchema_SqlKeywordName_ThrowInvalidSqlException()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema($"CREATE TABLE [if] (name Text);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("[if]"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("If"));
    }

    [Test]
    public void ProcessSqlSchema_As_ThrowInvalidSqlException()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        try
        {
            // act
            generator.ProcessSqlSchema($"CREATE TABLE new_table AS (SELECT * FROM old_table);", databaseInfo);

            // assert
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            Assert.That(exception.Message, Is.EqualTo("as is not currently supported"));
            Assert.That(exception.Token!.Line, Is.EqualTo(0));
            Assert.That(exception.Token!.CharacterInLine, Is.EqualTo(23));
        }
    }

    [TestCase("varchar(50)", "varchar(50)")]
    [TestCase("FLOAT(12, 7)", "FLOAT(12,7)")]
    public void ProcessSqlSchema_TypeNameIncludesBrackets_PrasesColumnTypeCorrectly(string sqlTypeName, string expectedSqlType)
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema($"CREATE TABLE new_table (name {sqlTypeName});", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].Columns.First().SqlType, Is.EqualTo(expectedSqlType));
    }

    [TestCase("CREATE TABLE new_table (name Integer primary key autoincrement);")]
    [TestCase("CREATE TABLE new_table (name Integer primary key asc autoincrement);")]
    [TestCase("CREATE TABLE new_table (name Integer primary key desc autoincrement);")]
    [TestCase("CREATE TABLE new_table (name Integer primary key desc on conflict rollback autoincrement);")]
    public void ProcessSqlSchema_AutoIncrement_SetToTrue(string query)
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema(query, databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].Columns.First().AutoIncrement, Is.True);
    }

    [TestCase("CREATE TABLE new_table (name Integer primary key);")]
    [TestCase("CREATE TABLE new_table (name Integer primary key asc);")]
    [TestCase("CREATE TABLE new_table (name Integer primary key desc);")]
    [TestCase("CREATE TABLE new_table (name Integer primary key desc on conflict rollback);")]
    public void ProcessSqlSchema_NoAutoIncrement_SetToFalse(string query)
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema(query, databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].Columns.First().AutoIncrement, Is.False);
    }

    [TestCase("CREATE TABLE new_table (name Integer primary key desc on conflict rollback);")]
    [TestCase("CREATE TABLE new_table (name Integer primary key desc on conflict ABORT);")]
    [TestCase("CREATE TABLE new_table (name Integer primary key desc on conflict FAIL);")]
    [TestCase("CREATE TABLE new_table (name Integer primary key desc on conflict IGNORE);")]
    [TestCase("CREATE TABLE new_table (name Integer primary key desc on conflict REPLACE);")]
    public void ProcessSqlSchema_OnConflictValid_NoInvalidSqlException(string query)
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        // assert
        generator.ProcessSqlSchema(query, databaseInfo);
    }

    [TestCase("CREATE TABLE new_table (name Integer primary key desc on conflict giveup);", 66, "Invalid conflict action")]
    [TestCase("CREATE TABLE new_table (name Integer primary key desc on conflict);", 65, "Invalid conflict action")]
    [TestCase("CREATE TABLE new_table (name Integer primary key desc on);", 56, "Unexpected token while parsing column definition, did you mean 'on conflict'?")]
    public void ProcessSqlSchema_OnConflictInvalidConflictAtion_InvalidSqlException(string query, int characterInLine, string expectedMessage)
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        try
        {
            // act
            generator.ProcessSqlSchema(query, databaseInfo);

            // assert
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            Assert.That(exception.Message, Is.EqualTo(expectedMessage));
            Assert.That(exception.Token!.Line, Is.EqualTo(0));
            Assert.That(exception.Token.CharacterInLine, Is.EqualTo(characterInLine));
        }
    }

    static IEnumerable<string> IncompleteQueries()
    {
        var query = "CREATE TABLE new_table (name Integer primary key desc on conflict rollback autoincrement);";
        for (int index = 0; index < query.Length; index++)
        {
            yield return query.Substring(0, index);
        }
    }

    [TestCaseSource(nameof(IncompleteQueries))]
    public void ProcessSqlSchema_Incomplete_DoesntCrash(string query)
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        try
        {
            generator.ProcessSqlSchema(query, databaseInfo);
        }
        catch (InvalidSqlException)
        {
        }
    }

    [Test]
    public void ProcessSqlSchema_Incomplete_DoesntCrash()
    {
        // arrange
        string query = "CREATE TABLE new_table (name Integer p";
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        try
        {
            generator.ProcessSqlSchema(query, databaseInfo);
        }
        catch (InvalidSqlException)
        {
        }
    }

    [Test]
    public void ProcessSqlSchema_AutoincrementNotInteger_InvalidSqlException()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        try
        {
            // act
            generator.ProcessSqlSchema("CREATE TABLE new_table (name TEXT primary key AUTOINCREMENT);", databaseInfo);

            // assert
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            Assert.That(exception.Message, Is.EqualTo("AUTOINCREMENT is only allowed on an INTEGER PRIMARY KEY"));
            Assert.That(exception.Token!.Line, Is.EqualTo(0));
            Assert.That(exception.Token.CharacterInLine, Is.EqualTo(46));
        }
    }

    [Test]
    public void ProcessSqlSchema_NotNullAfterAutoincrement_PrasedCorrectly()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema("CREATE TABLE new_table (name INTEGER primary key AUTOINCREMENT NOT NULL);", databaseInfo);

        // assert
        var columns = databaseInfo.Tables[0].Columns.ToArray();
        Assert.That(columns[0].AutoIncrement, Is.True);
        Assert.That(columns[0].NotNull, Is.True);
    }
}