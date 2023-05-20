using System.Runtime.InteropServices;

namespace SqlSrcGen.Runtime;

public enum Result
{
	Ok = 0,
	Error = 1,
	Busy = 5,
	Misuse = 21,
	Row = 100,
	Done = 101
}

public enum SqliteDataType
{
	Integer = 1,
	Float = 2,
	Blob = 4,
	Null = 5,
	Text = 3
}

public static class SqliteNativeMethods
{
	const string SqliteLib = "/usr/lib/x86_64-linux-gnu/libsqlite3.so.0.8.6";

	[DllImport(SqliteLib, EntryPoint = "sqlite3_open", CallingConvention = CallingConvention.Cdecl)]
	public static extern Result sqlite3_open([MarshalAs(UnmanagedType.LPStr)] string filename, out IntPtr db);

	[DllImport(SqliteLib, CallingConvention = CallingConvention.Cdecl)]
	public static extern Result sqlite3_close(IntPtr db);

	[DllImport(SqliteLib, CallingConvention = CallingConvention.Cdecl)]
	public static extern Result sqlite3_prepare_v2(IntPtr db, byte[] query, int numBytes, out IntPtr statementPtr, IntPtr pzTail);

	[DllImport(SqliteLib, CallingConvention = CallingConvention.Cdecl)]
	public static extern Result sqlite3_reset(IntPtr statementPtr);

	[DllImport(SqliteLib, CallingConvention = CallingConvention.Cdecl)]
	public static extern Result sqlite3_step(IntPtr statementPtr);

	[DllImport(SqliteLib, CallingConvention = CallingConvention.Cdecl)]
	public static extern IntPtr sqlite3_column_name(IntPtr statementPtr, int index);

	[DllImport(SqliteLib, CallingConvention = CallingConvention.Cdecl)]
	public static extern IntPtr sqlite3_column_text16(IntPtr statementPtr, int index);

	[DllImport(SqliteLib, CallingConvention = CallingConvention.Cdecl)]
	public static extern long sqlite3_column_int64(IntPtr statementPtr, int index);

	[DllImport(SqliteLib, CallingConvention = CallingConvention.Cdecl)]
	public static extern double sqlite3_column_double(IntPtr statementPtr, int index);

	[DllImport(SqliteLib, CallingConvention = CallingConvention.Cdecl)]
	public static extern SqliteDataType sqlite3_column_type(IntPtr statementPtr, int index);

	[DllImport(SqliteLib, CallingConvention = CallingConvention.Cdecl)]
	public static extern IntPtr sqlite3_column_blob(IntPtr statementPtr, int index);

	[DllImport(SqliteLib, CallingConvention = CallingConvention.Cdecl)]
	public static extern int sqlite3_column_bytes(IntPtr statementPtr, int index);

	[DllImport(SqliteLib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
	public static extern int sqlite3_bind_text16(IntPtr statementPtr, int index, [MarshalAs(UnmanagedType.LPWStr)] string val, int length, IntPtr free);

	[DllImport(SqliteLib, CallingConvention = CallingConvention.Cdecl)]
	public static extern int sqlite3_bind_int64(IntPtr statementPtr, int index, long value);

	[DllImport(SqliteLib, CallingConvention = CallingConvention.Cdecl)]
	public static extern int sqlite3_bind_double(IntPtr statementPtr, int index, double value);

	[DllImport(SqliteLib, CallingConvention = CallingConvention.Cdecl)]
	public static extern int sqlite3_bind_blob(IntPtr statementPtr, int index, byte[] blob, int length, IntPtr free);

	[DllImport(SqliteLib, CallingConvention = CallingConvention.Cdecl)]
	public static extern int sqlite3_bind_null(IntPtr statementPtr, int index);

	[DllImport(SqliteLib, CallingConvention = CallingConvention.Cdecl)]
	public static extern Result sqlite3_finalize(IntPtr statementPtr);

	public static IntPtr SQLITE_TRANSIENT = new IntPtr(-1);

}