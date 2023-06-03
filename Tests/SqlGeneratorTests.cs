using SqlSrcGen;

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
        table.Columns.Add(new Column() { SqlName = "name", CSharpName = "Name", SqlType = "Text", CSharpType = "string" });
        table.Columns.Add(new Column() { SqlName = "email", CSharpName = "Email", SqlType = "Text", CSharpType = "string" });
        databaseInfo.Tables.Add(table);

        string expectedSource =
            "public record Contact" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    public string Name { get; set; } = \"\";" + Environment.NewLine +
            "    public string Email { get; set; } = \"\";" + Environment.NewLine +
            "}" + Environment.NewLine;

        // act
        generator.GenerateDatabaseObjects(databaseInfo, stringBuilder);

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
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlName, Is.EqualTo("name"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpName, Is.EqualTo("Name"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlType, Is.EqualTo("Text"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpType, Is.EqualTo("string?"));
        Assert.That(databaseInfo.Tables[0].Columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.TEXT));
        Assert.That(databaseInfo.Tables[0].Columns[0].NotNull, Is.False);
    }

    [Test]
    public void ProcessSqlSchema_TextColumnNotNull_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema("CREATE TABLE contact (name Text not null);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlName, Is.EqualTo("name"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpName, Is.EqualTo("Name"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlType, Is.EqualTo("Text"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpType, Is.EqualTo("string"));
        Assert.That(databaseInfo.Tables[0].Columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.TEXT));
        Assert.That(databaseInfo.Tables[0].Columns[0].NotNull, Is.True);
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
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlName, Is.EqualTo("age"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpName, Is.EqualTo("Age"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlType, Is.EqualTo("Integer"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpType, Is.EqualTo("long?"));
        Assert.That(databaseInfo.Tables[0].Columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.INTEGER));
    }

    [Test]
    public void ProcessSqlSchema_IntegerColumnNotNull_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema("CREATE TABLE contact (age Integer NOT NULL);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlName, Is.EqualTo("age"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpName, Is.EqualTo("Age"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlType, Is.EqualTo("Integer"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpType, Is.EqualTo("long"));
        Assert.That(databaseInfo.Tables[0].Columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.INTEGER));
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
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlName, Is.EqualTo("height"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpName, Is.EqualTo("Height"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlType, Is.EqualTo("Real"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpType, Is.EqualTo("double?"));
        Assert.That(databaseInfo.Tables[0].Columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.REAL));
    }

    [Test]
    public void ProcessSqlSchema_RealColumnNotNull_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema("CREATE TABLE contact (height Real Not Null);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlName, Is.EqualTo("height"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpName, Is.EqualTo("Height"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlType, Is.EqualTo("Real"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpType, Is.EqualTo("double"));
        Assert.That(databaseInfo.Tables[0].Columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.REAL));
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
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlName, Is.EqualTo("key"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpName, Is.EqualTo("Key"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlType, Is.EqualTo("Blob"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpType, Is.EqualTo("byte[]?"));
        Assert.That(databaseInfo.Tables[0].Columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.BLOB));
    }

    [Test]
    public void ProcessSqlSchema_BlobColumnNotNull_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema("CREATE TABLE contact (key Blob not null);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlName, Is.EqualTo("key"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpName, Is.EqualTo("Key"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlType, Is.EqualTo("Blob"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpType, Is.EqualTo("byte[]"));
        Assert.That(databaseInfo.Tables[0].Columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.BLOB));
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
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlName, Is.EqualTo("distance"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpName, Is.EqualTo("Distance"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlType, Is.EqualTo("Numeric"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpType, Is.EqualTo("Numeric?"));
        Assert.That(databaseInfo.Tables[0].Columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.NUMERIC));
    }

    [Test]
    public void ProcessSqlSchema_NumericColumnNotNull_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema("CREATE TABLE contact (distance Numeric NOT NULL);", databaseInfo);

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlName, Is.EqualTo("distance"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpName, Is.EqualTo("Distance"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlType, Is.EqualTo("Numeric"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpType, Is.EqualTo("Numeric"));
        Assert.That(databaseInfo.Tables[0].Columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.NUMERIC));
    }

    [Test]
    public void ProcessSqlSchema_InvalidConstraintNot_ThrowsInvalidSqlException()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        try
        {
            // act
            generator.ProcessSqlSchema("CREATE TABLE contact (distance Numeric NOT);", databaseInfo);

            // assert
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            Assert.That(exception.Message, Is.EqualTo("Invalid column constraint, did you mean 'not null'?"));
            Assert.That(exception.Token.Line, Is.EqualTo(0));
            Assert.That(exception.Token.CharacterInLine, Is.EqualTo(39));
        }
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
        Assert.That(databaseInfo.Tables[0].Columns[0].PrimaryKey, Is.EqualTo(true));
        Assert.That(databaseInfo.Tables[0].Columns[1].PrimaryKey, Is.EqualTo(false));
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
            Assert.That(exception.Token.Line, Is.EqualTo(0));
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
            Assert.That(exception.Token.Line, Is.EqualTo(0));
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
            Assert.That(exception.Token.Line, Is.EqualTo(0));
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
        Assert.That(databaseInfo.Tables.First().Columns[0].SqlName, Is.EqualTo("name"));
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
        Assert.That(databaseInfo.Tables.First().Columns[0].SqlName, Is.EqualTo("name"));
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
            Assert.That(exception.Token.Line, Is.EqualTo(0));
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
            Assert.That(exception.Message, Is.EqualTo("Schema's are not supported"));
            Assert.That(exception.Token.Line, Is.EqualTo(0));
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
            Assert.That(exception.Token.Line, Is.EqualTo(0));
            Assert.That(exception.Token.CharacterInLine, Is.EqualTo(23));
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
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlType, Is.EqualTo(expectedSqlType));
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
        Assert.That(databaseInfo.Tables[0].Columns[0].AutoIncrement, Is.True);
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
        Assert.That(databaseInfo.Tables[0].Columns[0].AutoIncrement, Is.False);
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
            Assert.That(exception.Token.Line, Is.EqualTo(0));
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
    public void ProcessSqlSchema_AutoincrementNotInteger_InvalidSqlException()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        try
        {
            // act
            generator.ProcessSqlSchema("CREATE TABLE new_table (name TEXT primary key AUTOINCREMENT)", databaseInfo);

            // assert
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            Assert.That(exception.Message, Is.EqualTo("AUTOINCREMENT is only allowed on an INTEGER PRIMARY KEY"));
            Assert.That(exception.Token.Line, Is.EqualTo(0));
            Assert.That(exception.Token.CharacterInLine, Is.EqualTo(46));
        }
    }

}