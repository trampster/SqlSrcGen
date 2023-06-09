using System.Text;
using Moq;
using SqlSrcGen;

namespace Tests;

public class GeneratedConstraintTests
{
    [TestCase("generated always as (\"name\")")]
    [TestCase("generated always as (\"name\") stored")]
    [TestCase("generated always as (\"name\") virtual")]
    [TestCase("as (\"name\")")]
    [TestCase("as (\"name\") stored")]
    [TestCase("as (\"name\") virtual")]
    public void ProcessSqlSchema_GeneratedConstraint_CreatesTableInfo(string extraClauses)
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema($"CREATE TABLE child (name Text {extraClauses});", databaseInfo, Mock.Of<IDiagnosticsReporter>());

        // assert
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