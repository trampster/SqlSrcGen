using System;

namespace SqlSrcGen;

public class InvalidSqlException : FormatException
{
	public InvalidSqlException(string message, Token token) : base(message)
	{
		Token = token;
	}

	public Token Token
	{
		get;
	}
}