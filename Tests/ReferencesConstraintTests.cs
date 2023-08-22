using System.Text;
using Moq;
using SqlSrcGen;
using SqlSrcGen.Generator;

namespace Tests;

public class ReferencesConstraintTests
{
    [Test]
    public void ProcessSqlSchema_ReferencesConstraintTableOnly_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        var schemaBuilder = new StringBuilder();
        schemaBuilder.AppendLine("CREATE TABLE parent (name Text PRIMARY KEY);");
        schemaBuilder.AppendLine("CREATE TABLE child (name Text REFERENCES parent);");

        // act
        generator.ProcessSqlSchema(schemaBuilder.ToString(), databaseInfo, Mock.Of<IDiagnosticsReporter>());

        // assert
        Assert.That(databaseInfo.Tables[1].SqlName, Is.EqualTo("child"));
        Assert.That(databaseInfo.Tables[1].CSharpName, Is.EqualTo("Child"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlName, Is.EqualTo("name"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpName, Is.EqualTo("Name"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlType, Is.EqualTo("Text"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpType, Is.EqualTo("string?"));
        Assert.That(databaseInfo.Tables[0].Columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.TEXT));
    }

    [Test]
    public void ProcessSqlSchema_ReferencesConstraintTableDoesntExist_InvalidSqlException()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        var schemaBuilder = new StringBuilder();
        schemaBuilder.AppendLine("CREATE TABLE parent1 (name Text PRIMARY KEY);");
        schemaBuilder.AppendLine("CREATE TABLE child (name Text REFERENCES parent);");

        // act
        try
        {
            generator.ProcessSqlSchema(schemaBuilder.ToString(), databaseInfo, Mock.Of<IDiagnosticsReporter>());
            Assert.Fail("InvalidSqlException didn't occur");
        }
        catch (InvalidSqlException exception)
        {
            // assert
            Assert.That(exception.Message, Is.EqualTo("referenced table parent does not exist"));
            Assert.That(exception.Token.Line, Is.EqualTo(1));
            Assert.That(exception.Token.CharacterInLine, Is.EqualTo(41));
            Assert.That(exception.Token.Position, Is.EqualTo(87));
        }
    }

    [Test]
    public void ProcessSqlSchema_ReferencesConstraintColumnDefined_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        var schemaBuilder = new StringBuilder();
        schemaBuilder.AppendLine("CREATE TABLE parent (name Text PRIMARY KEY);");
        schemaBuilder.AppendLine("CREATE TABLE child (name Text REFERENCES parent(name));");

        // act
        generator.ProcessSqlSchema(schemaBuilder.ToString(), databaseInfo, Mock.Of<IDiagnosticsReporter>());

        // assert
        Assert.That(databaseInfo.Tables[1].SqlName, Is.EqualTo("child"));
        Assert.That(databaseInfo.Tables[1].CSharpName, Is.EqualTo("Child"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlName, Is.EqualTo("name"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpName, Is.EqualTo("Name"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlType, Is.EqualTo("Text"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpType, Is.EqualTo("string?"));
        Assert.That(databaseInfo.Tables[0].Columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.TEXT));
    }

    [TestCase("on delete set null")]
    [TestCase("on delete set default")]
    [TestCase("on delete cascade")]
    [TestCase("on delete restrict")]
    [TestCase("on delete no action")]
    [TestCase("on update set null")]
    [TestCase("on update set default")]
    [TestCase("on update cascade")]
    [TestCase("on update restrict")]
    [TestCase("on update no action")]
    [TestCase("on update no action on delete set null")]
    public void ProcessSqlSchema_ReferencesConstraintExtraClauses_CreatesTableInfo(string extraClauses)
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        var schemaBuilder = new StringBuilder();
        schemaBuilder.AppendLine("CREATE TABLE parent (name Text PRIMARY KEY);");
        schemaBuilder.AppendLine($"CREATE TABLE child (name Text REFERENCES parent(name) {extraClauses});");

        // act
        generator.ProcessSqlSchema(schemaBuilder.ToString(), databaseInfo, Mock.Of<IDiagnosticsReporter>());

        // assert
        Assert.That(databaseInfo.Tables[1].SqlName, Is.EqualTo("child"));
        Assert.That(databaseInfo.Tables[1].CSharpName, Is.EqualTo("Child"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlName, Is.EqualTo("name"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpName, Is.EqualTo("Name"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlType, Is.EqualTo("Text"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpType, Is.EqualTo("string?"));
        Assert.That(databaseInfo.Tables[0].Columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.TEXT));
    }

    [Test]
    public void ProcessSqlSchema_ReferencesConstraintMatchClause_CreatesWarning()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();
        var diagnosticsReporterMock = new Mock<IDiagnosticsReporter>();

        ErrorCode capturedErrorCode = ErrorCode.None;
        Token? capturedToken = null;
        string capturedMessage = "";

        diagnosticsReporterMock
            .Setup(reporter => reporter.Warning(It.IsAny<ErrorCode>(), It.IsAny<string>(), It.IsAny<Token>()))
            .Callback<ErrorCode, string, Token>((errorCode, message, token) =>
            {
                capturedErrorCode = errorCode;
                capturedToken = token;
                capturedMessage = message;
            });

        // act
        var schemaBuilder = new StringBuilder();
        schemaBuilder.AppendLine("CREATE TABLE parent (name Text PRIMARY KEY);");
        schemaBuilder.AppendLine($"CREATE TABLE child (name Text REFERENCES parent(name) match simple);");
        generator.ProcessSqlSchema(schemaBuilder.ToString(), databaseInfo, diagnosticsReporterMock.Object);

        // assert
        Assert.That(capturedToken, Is.Not.Null);
        Assert.That(capturedToken!.Line, Is.EqualTo(1));
        Assert.That(capturedToken!.CharacterInLine, Is.EqualTo(54));
        Assert.That(capturedMessage, Is.EqualTo("SQLite parses MATCH clauses, but does not enforce them. All foreign key constraints in SQLite are handled as if MATCH SIMPLE were specified."));
        Assert.That(capturedErrorCode, Is.EqualTo(ErrorCode.SSG0004));
    }

    [TestCase("DEFERRABLE")]
    [TestCase("DEFERRABLE INITIALLY DEFERRED")]
    [TestCase("DEFERRABLE INITIALLY IMMEDIATE")]
    [TestCase("not DEFERRABLE")]
    [TestCase("NOT DEFERRABLE INITIALLY DEFERRED")]
    [TestCase("not DEFERRABLE INITIALLY IMMEDIATE")]
    public void ProcessSqlSchema_ReferencesConstraintDeferrable_CreatesTableInfo(string extraClauses)
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        var schemaBuilder = new StringBuilder();
        schemaBuilder.AppendLine("CREATE TABLE parent (name Text PRIMARY KEY);");
        schemaBuilder.AppendLine($"CREATE TABLE child (name Text REFERENCES parent(name) {extraClauses});");

        // act
        generator.ProcessSqlSchema(schemaBuilder.ToString(), databaseInfo, Mock.Of<IDiagnosticsReporter>());

        // assert
        Assert.That(databaseInfo.Tables[1].SqlName, Is.EqualTo("child"));
        Assert.That(databaseInfo.Tables[1].CSharpName, Is.EqualTo("Child"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlName, Is.EqualTo("name"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpName, Is.EqualTo("Name"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlType, Is.EqualTo("Text"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpType, Is.EqualTo("string?"));
        Assert.That(databaseInfo.Tables[0].Columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.TEXT));
    }
}