
using System.Runtime.InteropServices;

namespace SqlSrcGen.Runtime;

public enum NumericType
{
    Integer,
    Text,
    Real,
    Blob,
    Null
}

public struct Numeric
{
    double? _realValue = null;
    long? _integerValue = 0;
    string? _textValue = null;
    byte[]? _blobValue = null;
    NumericType _type = NumericType.Integer;


    public Numeric(double value)
    {
        _type = NumericType.Real;
        _realValue = value;
    }

    public Numeric(long value)
    {
        _type = NumericType.Integer;
        _integerValue = value;
    }

    public Numeric(string value)
    {
        _type = NumericType.Text;
        _textValue = value;
    }

    public Numeric(byte[] value)
    {
        _type = NumericType.Blob;
        _blobValue = value;
    }

    public double GetReal()
    {
        if (_type != NumericType.Real)
        {
            throw new InvalidOperationException($"Type access mismatch");
        }
        return _realValue!.Value;
    }

    public long GetInteger()
    {
        if (_type != NumericType.Integer)
        {
            throw new InvalidOperationException($"Type access mismatch");
        }
        return _integerValue!.Value;
    }

    public string GetText()
    {
        if (_type != NumericType.Text)
        {
            throw new InvalidOperationException($"Type access mismatch");
        }
        return _textValue!;
    }

    public byte[] GetBlob()
    {
        if (_type != NumericType.Text)
        {
            throw new InvalidOperationException($"Type access mismatch");
        }
        return _blobValue!;
    }

    public NumericType NumericType => _type;
}

public static class SqliteNumericSqliteMethods
{
    public static Numeric Read(IntPtr statement, int columnIndex)
    {
        var type = SqliteNativeMethods.sqlite3_column_type(statement, columnIndex);
        return type switch
        {
            SqliteDataType.Integer => new Numeric(SqliteNativeMethods.sqlite3_column_int64(statement, columnIndex)),
            SqliteDataType.Float => new Numeric(SqliteNativeMethods.sqlite3_column_double(statement, columnIndex)),
            SqliteDataType.Text => new Numeric(Marshal.PtrToStringUni(SqliteNativeMethods.sqlite3_column_text16(statement, columnIndex))),
            SqliteDataType.Blob => new Numeric(GetBlob(statement, columnIndex)),
            _ => throw new InvalidOperationException($"Unexpected SqliteDataType in numeric column {type}")
        };
    }

    static byte[] GetBlob(IntPtr statement, int columnIndex)
    {
        IntPtr blobPtr = SqliteNativeMethods.sqlite3_column_blob(statement, columnIndex);
        int length = SqliteNativeMethods.sqlite3_column_bytes(statement, columnIndex);
        var blob = new byte[length];
        Marshal.Copy(blobPtr, blob, 0, blob.Length);
        return blob;
    }

    public static void Write(IntPtr statement, int columnIndex, Numeric value)
    {
        switch (value.NumericType)
        {
            case NumericType.Integer:
                SqliteNativeMethods.sqlite3_bind_int64(statement, columnIndex, value.GetInteger());
                break;
            case NumericType.Real:
                SqliteNativeMethods.sqlite3_bind_double(statement, columnIndex, value.GetReal());
                break;
            case NumericType.Text:
                var textValue = value.GetText();
                SqliteNativeMethods.sqlite3_bind_text16(statement, columnIndex, textValue, -1, SqliteNativeMethods.SQLITE_TRANSIENT);
                break;
            case NumericType.Blob:
                var blobValue = value.GetBlob();
                SqliteNativeMethods.sqlite3_bind_blob(statement, columnIndex, blobValue, blobValue.Length, SqliteNativeMethods.SQLITE_TRANSIENT);
                break;
            default:
                throw new InvalidOperationException($"Unexpected SqliteDataType in numeric column {value.NumericType}");
        }
    }
}