using Moq;
using SqlSrcGen;

namespace Tests;

public class CollateConstraintTests
{
    [Test]
    public void ProcessSqlSchema_CollateConstraint_CreatesTableInfo()
    {
        // arrange
        var generator = new SqlGenerator();
        var databaseInfo = new DatabaseInfo();

        // act
        generator.ProcessSqlSchema($"CREATE TABLE contact (name Text COLLATE NOCASE);", databaseInfo, Mock.Of<IDiagnosticsReporter>());

        // assert
        Assert.That(databaseInfo.Tables[0].SqlName, Is.EqualTo("contact"));
        Assert.That(databaseInfo.Tables[0].CSharpName, Is.EqualTo("Contact"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlName, Is.EqualTo("name"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpName, Is.EqualTo("Name"));
        Assert.That(databaseInfo.Tables[0].Columns[0].SqlType, Is.EqualTo("Text"));
        Assert.That(databaseInfo.Tables[0].Columns[0].CSharpType, Is.EqualTo("string?"));
        Assert.That(databaseInfo.Tables[0].Columns[0].TypeAffinity, Is.EqualTo(TypeAffinity.TEXT));
    }

    [Test]
    public void ProcessSqlSchema_CustomCollateName_GeneratesWarning()
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
        generator.ProcessSqlSchema($"CREATE TABLE contact (name Text COLLATE custom);", databaseInfo, diagnosticsReporterMock.Object);

        // assert
        Assert.That(capturedToken, Is.Not.Null);
        Assert.That(capturedToken!.Line, Is.EqualTo(0));
        Assert.That(capturedToken!.CharacterInLine, Is.EqualTo(40));
        Assert.That(capturedMessage, Is.EqualTo("Collation types other than nocase, binary and rtrim require custom collation creation"));
        Assert.That(capturedErrorCode, Is.EqualTo(ErrorCode.SSG0002));
    }

    [Test]
    public void ProcessSqlSchema_CollateOnNonTextColumn_GeneratesWarning()
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
        generator.ProcessSqlSchema($"CREATE TABLE contact (name INTEGER COLLATE custom);", databaseInfo, diagnosticsReporterMock.Object);

        // assert
        Assert.That(capturedToken, Is.Not.Null);
        Assert.That(capturedToken!.Line, Is.EqualTo(0));
        Assert.That(capturedToken!.CharacterInLine, Is.EqualTo(43));
        Assert.That(capturedMessage, Is.EqualTo("Collation only affects Text columns"));
        Assert.That(capturedErrorCode, Is.EqualTo(ErrorCode.SSG0003));
    }
}