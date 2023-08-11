using System;
using System.Collections.Generic;

namespace SqlSrcGen;

public class Tokenizer
{
    public List<Token> Tokenize(string schema)
    {
        var tokens = new List<Token>();
        var text = schema.AsSpan();
        int position = 0;
        int lineIndex = 0;
        int characterInLineIndex = 0;
        while (text.Length > 0)
        {
            text = SkipWhitespace(text, ref position, ref lineIndex, ref characterInLineIndex);
            if (text.Length == 0)
            {
                break;
            }
            text = ReadToken(text, out Token token, ref position, ref lineIndex, ref characterInLineIndex);
            if (token != null)
            {
                tokens.Add(token);
            }
        }
        return tokens;
    }

    ReadOnlySpan<char> SkipWhitespace(ReadOnlySpan<char> text, ref int position, ref int lineIndex, ref int characterInLineIndex)
    {
        for (int index = 0; index < text.Length; index++)
        {
            switch (text[index])
            {
                case '\n':
                    position++;
                    lineIndex++;
                    characterInLineIndex = 0;
                    continue;
                case ' ':
                case '\t':
                case '\r':
                    characterInLineIndex++;
                    position++;
                    continue;
                default:
                    return text.Slice(index);
            }
        }
        return Span<char>.Empty;
    }


    bool ReadOperator(ReadOnlySpan<char> text, out Token read, ref int position, ref int lineIndex, ref int characterInLineIndex)
    {
        if (text.Length == 0)
        {
            read = null;
            return false;
        }
        switch (text[0])
        {
            case '|':
                if (text.Length > 1 && text[1] == '|')
                {
                    position += 2;
                    characterInLineIndex += 2;
                    read = new Token("||", position, lineIndex, characterInLineIndex, TokenType.Operator);
                    read.BinaryOperator = true;
                    return true;
                }
                read = new Token("|", position, lineIndex, characterInLineIndex, TokenType.Operator);
                read.BinaryOperator = true;
                position++;
                characterInLineIndex++;
                return true;
            case '-':
                if (text.Length > 1 && text[1] == '>')
                {
                    if (text.Length > 2 && text[2] == '>')
                    {
                        position += 3;
                        characterInLineIndex += 3;
                        read = new Token("->>", position, lineIndex, characterInLineIndex, TokenType.Operator);
                        read.BinaryOperator = true;
                        return true;
                    }
                    position += 2;
                    characterInLineIndex += 2;
                    read = new Token("->", position, lineIndex, characterInLineIndex, TokenType.Operator);
                    read.BinaryOperator = true;
                    return true;
                }
                read = new Token("-", position, lineIndex, characterInLineIndex, TokenType.Operator);
                read.BinaryOperator = true;
                read.UnaryOperator = true;
                position++;
                characterInLineIndex++;
                return true;
            case '*':
            case '/':
            case '%':
            case '&':
                read = new Token(text[0].ToString(), position, lineIndex, characterInLineIndex, TokenType.Operator);
                read.BinaryOperator = true;
                position++;
                characterInLineIndex++;
                return true;
            case '<':
                if (text.Length > 1 && text[1] == '<')
                {
                    position += 2;
                    characterInLineIndex += 2;
                    read = new Token("<<", position, lineIndex, characterInLineIndex, TokenType.Operator);
                    read.BinaryOperator = true;
                    return true;
                }
                if (text.Length > 1 && text[1] == '=')
                {
                    position += 2;
                    characterInLineIndex += 2;
                    read = new Token("<=", position, lineIndex, characterInLineIndex, TokenType.Operator);
                    read.BinaryOperator = true;
                    return true;
                }
                if (text.Length > 1 && text[1] == '>')
                {
                    position += 2;
                    characterInLineIndex += 2;
                    read = new Token("<>", position, lineIndex, characterInLineIndex, TokenType.Operator);
                    read.BinaryOperator = true;
                    return true;
                }
                read = new Token("<", position, lineIndex, characterInLineIndex, TokenType.Operator);
                read.BinaryOperator = true;
                position++;
                characterInLineIndex++;
                return true;
            case '>':
                if (text.Length > 1 && text[1] == '>')
                {
                    position += 2;
                    characterInLineIndex += 2;
                    read = new Token(">>", position, lineIndex, characterInLineIndex, TokenType.Operator);
                    read.BinaryOperator = true;
                    return true;
                }
                if (text.Length > 1 && text[1] == '=')
                {
                    position += 2;
                    characterInLineIndex += 2;
                    read = new Token(">=", position, lineIndex, characterInLineIndex, TokenType.Operator);
                    read.BinaryOperator = true;
                    return true;
                }
                read = new Token(">", position, lineIndex, characterInLineIndex, TokenType.Operator);
                read.BinaryOperator = true;
                position++;
                characterInLineIndex++;
                return true;
            case '=':
                if (text.Length > 1 && text[1] == '=')
                {
                    position += 2;
                    characterInLineIndex += 2;
                    read = new Token("==", position, lineIndex, characterInLineIndex, TokenType.Operator);
                    read.BinaryOperator = true;
                    return true;
                }
                read = new Token("=", position, lineIndex, characterInLineIndex, TokenType.Operator);
                read.BinaryOperator = true;
                position++;
                characterInLineIndex++;
                return true;
            case '!':
                if (text.Length > 1 && text[1] == '=')
                {
                    position += 2;
                    characterInLineIndex += 2;
                    read = new Token("!=", position, lineIndex, characterInLineIndex, TokenType.Operator);
                    read.BinaryOperator = true;
                    return true;
                }
                read = null;
                return false;
            case '~':
            case '+':
                position += 1;
                characterInLineIndex += 1;
                read = new Token(text[0].ToString(), position, lineIndex, characterInLineIndex, TokenType.Operator);
                read.BinaryOperator = true;
                read.UnaryOperator = true;
                return true;
            case 'a':
            case 'A':
                if (text.Length > 4 && char.ToLowerInvariant(text[1]) == 'n' && char.ToLowerInvariant(text[2]) == 'd' && char.IsWhiteSpace(text[3]))
                {
                    position += 3;
                    characterInLineIndex += 3;
                    read = new Token(text.Slice(0, 3).ToString(), position, lineIndex, characterInLineIndex, TokenType.Operator);
                    read.BinaryOperator = true;
                    return true;
                }
                read = null;
                return false;
            case 'o':
            case 'O':
                if (text.Length > 2 && char.ToLowerInvariant(text[1]) == 'r' && char.IsWhiteSpace(text[2]))
                {
                    position += 2;
                    characterInLineIndex += 2;
                    read = new Token(text.Slice(0, 2).ToString(), position, lineIndex, characterInLineIndex, TokenType.Operator);
                    read.BinaryOperator = true;
                    return true;
                }
                read = null;
                return false;

            default:
                read = null;
                return false;
        }
    }


    ReadOnlySpan<char> ReadToken(ReadOnlySpan<char> text, out Token read, ref int position, ref int lineIndex, ref int characterInLineIndex)
    {
        if (ReadOperator(text, out read, ref position, ref lineIndex, ref characterInLineIndex))
        {
            return text.Slice(read.Value.Length);
        }
        switch (text[0])
        {
            case ',':
            case '(':
            case ')':
            case ';':
                var tokenValue = text.Slice(0, 1).ToString();
                read = new Token() { Value = tokenValue, Position = position, Line = lineIndex, CharacterInLine = characterInLineIndex };
                position += 1;
                characterInLineIndex += 1;
                return text.Slice(1);
        }

        if (text[0] == '[')
        {
            return ReadSquareBacketToken(text, out read, ref position, ref lineIndex, ref characterInLineIndex);
        }

        if (text[0] == '\'')
        {
            return ReadStringLiteral(text, out read, ref position, ref lineIndex, ref characterInLineIndex);
        }

        if (char.ToLowerInvariant(text[0]) == 'x' && text.Length > 1 && text[1] == '\'')
        {
            return ReadBlobLiteral(text, out read, ref position, ref lineIndex, ref characterInLineIndex);
        }

        if ((text[0] == '.' && 1 < text.Length && char.IsDigit(text[1])) ||
            char.IsDigit(text[0]))
        {
            return ParseNumericLiteral(text, out read, ref position, ref lineIndex, ref characterInLineIndex);
        }

        if (text[0] == '.')
        {
            var tokenValue = text.Slice(0, 1).ToString();
            read = new Token() { Value = tokenValue, Position = position, Line = lineIndex, CharacterInLine = characterInLineIndex };
            position += 1;
            characterInLineIndex += 1;
            return text.Slice(1);
        }

        for (int index = 0; index < text.Length; index++)
        {
            switch (text[index])
            {
                case ' ':
                case '.':
                case '\t':
                case '\n':
                case '\r':
                case ',':
                case '(':
                case ')':
                case ';':
                case '\'':
                    string tokenValue = text.Slice(0, index).ToString();
                    read = new Token() { Value = tokenValue, Position = position, Line = lineIndex, CharacterInLine = characterInLineIndex };
                    position += index;
                    characterInLineIndex += index;
                    if (IsNewLine(text.Slice(index)))
                    {
                        lineIndex++;
                        characterInLineIndex = 0;
                    }
                    return text.Slice(index);
                default:
                    break;
            }
        }

        read = new Token() { Value = text.ToString(), Position = position, Line = lineIndex, CharacterInLine = characterInLineIndex };

        position += text.Length;
        return Span<char>.Empty;
    }

    ReadOnlySpan<char> ReadSquareBacketToken(ReadOnlySpan<char> text, out Token read, ref int position, ref int lineIndex, ref int characterInLineIndex)
    {
        int positionStart = position;
        int characterInLineIndexStart = characterInLineIndex;
        int lineStart = lineIndex;

        position++;
        characterInLineIndex++;
        for (int index = 1; index < text.Length; index++)
        {
            position++;
            characterInLineIndex++;
            if (text[index] == ']')
            {
                index++;
                string tokenValue = text.Slice(0, index).ToString();
                read = new Token()
                {
                    Value = tokenValue,
                    Position = positionStart,
                    Line = lineStart,
                    CharacterInLine = characterInLineIndexStart,
                    TokenType = TokenType.Other
                };
                return index < text.Length ? text.Slice(index) : ReadOnlySpan<char>.Empty;
            }
            if (IsNewLine(text.Slice(index)))
            {
                lineIndex++;
                characterInLineIndex = 0;
            }
        }

        var token = new Token() { Value = "", Position = position, Line = lineIndex, CharacterInLine = characterInLineIndex, TokenType = TokenType.StringLiteral };
        throw new InvalidSqlException("Ran out of charactors looking for ']'", new Token() { });
    }

    public ReadOnlySpan<char> ParseNumericLiteral(ReadOnlySpan<char> text, out Token read, ref int position, ref int lineIndex, ref int characterInLineIndex)
    {
        int index = 0;
        bool hasDot = false;
        if (text.Length == 0)
        {
            throw new InvalidSqlException("Ran out of text trying to read numberic literal", new Token() { Position = position, Line = lineIndex, CharacterInLine = characterInLineIndex });
        }
        if (text[0] == '.')
        {
            hasDot = true;
            index++;
        }
        else if (!char.IsDigit(text[0]))
        {
            throw new InvalidSqlException("numeric literals must start with a '.' or a digit", new Token() { Position = position, Line = lineIndex, CharacterInLine = characterInLineIndex });
        }
        if (text[0] == '0' && text.Length > 1 && text[1] == 'x')
        {
            index = 2;
            if (index >= text.Length)
            {
                throw new InvalidSqlException("Missing hex value in numeric literal", new Token() { Position = position, Line = lineIndex, CharacterInLine = characterInLineIndex });
            }
            ParseHexDigits(text, ref index);
        }
        else
        {

            // parse digits
            ParseDigits(text, ref index);


            if (index < text.Length && text[index] == '.')
            {
                if (hasDot)
                {
                    throw new InvalidSqlException("numeric literals can contain only one dot", new Token() { Position = position, Line = lineIndex, CharacterInLine = characterInLineIndex });
                }
                index++;
                ParseDigits(text, ref index);
            }

            if (index < text.Length && (text[index] == 'e' || text[index] == 'E'))
            {
                index++;
                if (index >= text.Length)
                {
                    throw new InvalidSqlException("Missing exponent value in numeric literal", new Token() { Position = position, Line = lineIndex, CharacterInLine = characterInLineIndex });
                }
                switch (text[index])
                {
                    case '+':
                    case '-':
                        index++;
                        break;
                }
                if (index >= text.Length)
                {
                    throw new InvalidSqlException("Missing exponent value in numeric literal", new Token() { Position = position, Line = lineIndex, CharacterInLine = characterInLineIndex });
                }
                ParseDigits(text, ref index);

            }
        }

        read = new Token() { Value = text.Slice(0, index).ToString(), Position = position, Line = lineIndex, CharacterInLine = characterInLineIndex, TokenType = TokenType.NumericLiteral };
        position += index;
        characterInLineIndex += index;
        if (index < text.Length && IsNewLine(text.Slice(index)))
        {
            lineIndex++;
            characterInLineIndex = 0;
        }
        return text.Slice(index);
    }

    public void ParseHexDigits(ReadOnlySpan<char> text, ref int index)
    {
        for (; index < text.Length; index++)
        {
            if (IsHex(text[index]))
            {
                continue;
            }
            return;
        }
    }

    bool IsNewLine(ReadOnlySpan<char> text)
    {
        return text[0] == '\n';
    }

    ReadOnlySpan<char> ReadStringLiteral(ReadOnlySpan<char> text, out Token read, ref int position, ref int lineIndex, ref int characterInLineIndex)
    {
        int positionStart = position;
        int characterInLineIndexStart = characterInLineIndex;
        int lineStart = lineIndex;

        bool escaped = false;
        for (int index = 1; index < text.Length; index++)
        {
            position++;
            characterInLineIndex++;
            if (text[index] == '\\' && !escaped)
            {
                escaped = true;
                continue;
            }
            if (IsNewLine(text.Slice(index)))
            {
                lineIndex++;
                characterInLineIndex = 0;
            }
            if (text[index] == '\'' && !escaped)
            {
                index++;
                string tokenValue = text.Slice(0, index).ToString();
                read = new Token()
                {
                    Value = tokenValue,
                    Position = positionStart,
                    Line = lineStart,
                    CharacterInLine = characterInLineIndexStart,
                    TokenType = TokenType.StringLiteral
                };
                return index < text.Length ? text.Slice(index) : ReadOnlySpan<char>.Empty;
            }
            escaped = false;
        }
        var token = new Token() { Value = "", Position = position, Line = lineIndex, CharacterInLine = characterInLineIndex, TokenType = TokenType.StringLiteral };
        throw new InvalidSqlException("Ran out of charactors looking for end of string literal", new Token() { });
    }

    public ReadOnlySpan<char> ReadBlobLiteral(ReadOnlySpan<char> text, out Token read, ref int position, ref int lineIndex, ref int characterInLineIndex)
    {
        int index = 0;

        int startPosition = position;
        int startLine = lineIndex;
        int startCharacterInLine = characterInLineIndex;
        void ThrowInvalidSqlException(string message)
        {
            throw new InvalidSqlException(
                message,
                new Token()
                {
                    Position = startPosition + index,
                    Line = startLine,
                    CharacterInLine = startCharacterInLine + index
                });
        }
        if (text.Length < 2)
        {
            ThrowInvalidSqlException("Ran out of text parsing blob literal");
        }
        if (char.ToLowerInvariant(text[index]) != 'x')
        {
            ThrowInvalidSqlException("Blob literal must start with a 'x' or 'X'");
        }
        index++;
        if (text[index] != '\'')
        {
            ThrowInvalidSqlException("Second character in a blob literal must be a single quote");
        }
        index++;
        int beforeIndex = index;
        ParseHexDigits(text, ref index);
        int hexDigitsParsed = index - beforeIndex;
        if (hexDigitsParsed % 2 != 0)
        {
            ThrowInvalidSqlException("Blob literals must have an even number of hex digits");
        }
        if (text[index] != '\'')
        {
            ThrowInvalidSqlException("Invalid charactor in blob literal");
        }
        index++;

        read = new Token() { Value = text.Slice(0, index).ToString(), Position = position, Line = lineIndex, CharacterInLine = characterInLineIndex, TokenType = TokenType.BlobLiteral };
        position += index;
        characterInLineIndex += index;
        if (index < text.Length && IsNewLine(text.Slice(index)))
        {
            lineIndex++;
            characterInLineIndex = 0;
        }
        return text.Slice(index);
    }

    static bool IsHex(char value)
    {
        switch (char.ToLowerInvariant(value))
        {
            case '0':
            case '1':
            case '2':
            case '3':
            case '4':
            case '5':
            case '6':
            case '7':
            case '8':
            case '9':
            case 'a':
            case 'b':
            case 'c':
            case 'd':
            case 'e':
            case 'f':
                return true;
            default:
                return false;
        }
    }

    public void ParseDigits(ReadOnlySpan<char> text, ref int index)
    {
        for (; index < text.Length; index++)
        {
            if (!char.IsDigit(text[index]))
            {
                break;
            }
        }
    }
}