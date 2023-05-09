using System.Runtime.InteropServices;
using System.Text;

namespace SqlSrcGen.Runtime;

public class Sqlite : IDisposable
{
    readonly IntPtr _dbHandle;

    public Sqlite(string filename)
    {
        var result = SqliteNativeMethods.sqlite3_open(filename, out _dbHandle);
        if (result != Result.Ok)
        {
            throw new SqliteException($"Failed to open database {filename}", result);
        }
    }

    static readonly byte[] _createContactBytes = new byte[] { 0x43, 0x52, 0x45, 0x41, 0x54, 0x45, 0x20, 0x54, 0x41, 0x42, 0x4C, 0x45, 0x20, 0x63, 0x6F, 0x6E, 0x74, 0x61, 0x63, 0x74, 0x20, 0x28, 0x20, 0x6E, 0x61, 0x6D, 0x65, 0x20, 0x54, 0x65, 0x78, 0x74, 0x20, 0x2C, 0x20, 0x65, 0x6D, 0x61, 0x69, 0x6C, 0x20, 0x54, 0x65, 0x78, 0x74, 0x20, 0x29, 0x20, 0x3B };

    public void CreateContactTable()
    {
        //var queryBytes = UTF8Encoding.UTF8.GetBytes("CREATE TABLE contact ( name Text , email Text ) ;");

        // if (!queryBytes.SequenceEqual(_createContactBytes))
        // {
        //     throw new Exception("it's different");
        // }


        var result = SqliteNativeMethods.sqlite3_prepare_v2(_dbHandle, _createContactBytes, _createContactBytes.Length, out IntPtr statementPtr, IntPtr.Zero);
        try
        {
            if (result != Result.Ok)
            {
                throw new SqliteException("Failed to prepare sqlite statement CREATE TABLE contact ( name Text , email Text ) ;", result);
            }

            result = SqliteNativeMethods.sqlite3_step(statementPtr);

            if (result != Result.Done)
            {
                throw new SqliteException("Failed to execute sqlite statement CREATE TABLE contact ( name Text , email Text ) ;", result);

            }
        }
        finally
        {
            SqliteNativeMethods.sqlite3_finalize(statementPtr);
        }
    }

    public void Query(string query)
    {
        //TODO: requires allocation so we do this ahead of time in the source generator
        var queryBytes = UTF8Encoding.UTF8.GetBytes(query);

        var result = SqliteNativeMethods.sqlite3_prepare_v2(_dbHandle, queryBytes, queryBytes.Length, out IntPtr statementPtr, IntPtr.Zero);

        try
        {
            if (result != Result.Ok)
            {
                throw new SqliteException($"Failed to prepare sqlite statement {query}", result);
            }

            result = SqliteNativeMethods.sqlite3_step(statementPtr);

            if (result != Result.Done)
            {
                throw new SqliteException($"Failed to execute sqlite statement {query}", result);

            }
        }
        finally
        {
            SqliteNativeMethods.sqlite3_finalize(statementPtr);
        }
    }

    static readonly byte[] _queryAllContactsBytes = UTF8Encoding.UTF8.GetBytes("SELECT * FROM contact;");
    IntPtr _queryContactStatement = IntPtr.Zero;

    public void AllContacts(string query, List<Contact> list)
    {
        if (_queryContactStatement == IntPtr.Zero)
        {
            var prepareResult = SqliteNativeMethods.sqlite3_prepare_v2(_dbHandle, _queryAllContactsBytes, _queryAllContactsBytes.Length, out _queryContactStatement, IntPtr.Zero);
            if (prepareResult != Result.Ok)
            {
                throw new SqliteException($"Failed to prepare sqlite statement {query}", prepareResult);
            }
        }

        var result = SqliteNativeMethods.sqlite3_step(_queryContactStatement);

        int index = 0;
        while (result == Result.Row)
        {
            Contact? contact = null;
            if (index >= list.Count)
            {
                contact = new Contact();
                Console.WriteLine("Making new contact");
                list.Add(contact);
            }
            else
            {
                contact = list[index];
            }

            contact!.Name = Marshal.PtrToStringUni(SqliteNativeMethods.sqlite3_column_text16(_queryContactStatement, 0))!;
            contact!.Email = Marshal.PtrToStringUni(SqliteNativeMethods.sqlite3_column_text16(_queryContactStatement, 1))!;

            result = SqliteNativeMethods.sqlite3_step(_queryContactStatement);
        }

        if (result != Result.Done)
        {
            throw new SqliteException($"Failed to execute sqlite statement {query}", result);
        }

        // reset the statement so it's ready for next time
        result = SqliteNativeMethods.sqlite3_reset(_queryContactStatement);
        if (result != Result.Ok)
        {
            throw new SqliteException($"Failed to reset sqlite statement {query}", result);
        }
    }

    static readonly byte[] InsertContactQueryBytes = UTF8Encoding.UTF8.GetBytes("INSERT INTO contact (name, email) VALUES (?, ?);");
    IntPtr _insertContactStatementPtr = IntPtr.Zero;

    public void InsertContact(Contact contact)
    {
        if (_insertContactStatementPtr == IntPtr.Zero)
        {
            var prepareResult = SqliteNativeMethods.sqlite3_prepare_v2(_dbHandle, InsertContactQueryBytes, InsertContactQueryBytes.Length, out IntPtr statementPtr, IntPtr.Zero);

            if (prepareResult != Result.Ok)
            {
                throw new SqliteException($"Failed to prepare statement to insert contact", prepareResult);
            }
        }

        SqliteNativeMethods.sqlite3_bind_text16(_insertContactStatementPtr, 1, contact.Name, -1, SqliteNativeMethods.SQLITE_TRANSIENT);
        SqliteNativeMethods.sqlite3_bind_text16(_insertContactStatementPtr, 2, contact.Email, -1, SqliteNativeMethods.SQLITE_TRANSIENT);

        var result = SqliteNativeMethods.sqlite3_step(_insertContactStatementPtr);

        if (result != Result.Done)
        {
            throw new SqliteException($"Failed to insert contact {contact}", result);
        }

        SqliteNativeMethods.sqlite3_reset(_insertContactStatementPtr);
    }

    bool disposedValue;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            SqliteNativeMethods.sqlite3_close(_dbHandle);

            if (_insertContactStatementPtr != IntPtr.Zero)
            {
                SqliteNativeMethods.sqlite3_finalize(_insertContactStatementPtr);
                _insertContactStatementPtr = IntPtr.Zero;
            }

            disposedValue = true;
        }
    }

    ~Sqlite()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
