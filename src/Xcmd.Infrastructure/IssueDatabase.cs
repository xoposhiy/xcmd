using System.Runtime.InteropServices;
using System.Text;

namespace Xcmd.Infrastructure;

// Windows ships the standard SQLite C API. Keep its handles, SQL and errors in Infrastructure.
internal sealed class IssueDatabase : IDisposable
{
    private nint connection;
    private const string Schema = "CREATE TABLE IF NOT EXISTS issues (" +
        "id TEXT PRIMARY KEY NOT NULL CHECK(length(trim(id)) > 0), " +
        "status TEXT NOT NULL CHECK(status IN ('new','in-progress','closed')), " +
        "text TEXT NOT NULL CHECK(length(trim(text)) > 0), " +
        "target_id TEXT NOT NULL CHECK(length(trim(target_id)) > 0), " +
        "target_name TEXT NOT NULL CHECK(length(trim(target_name)) > 0), " +
        "release TEXT NOT NULL CHECK(length(trim(release)) > 0))";

    public IssueDatabase(string path)
    {
        try
        {
            Check(Native.Open(path, out connection, 0x00010006, 0));
            Check(Native.BusyTimeout(connection, 2000));
            Execute("PRAGMA synchronous = FULL");
            var version = Query("PRAGMA user_version").Single()[0];
            var application = Query("PRAGMA application_id").Single()[0];
            if (version == "0" && application == "0" || version == "1" && application == "1480805700")
            {
                Execute("BEGIN IMMEDIATE");
                // A competing first writer may have initialized the database while we waited.
                version = Query("PRAGMA user_version").Single()[0];
                application = Query("PRAGMA application_id").Single()[0];
                if (version == "0" && application == "0")
                {
                    if (Query("SELECT name FROM sqlite_master WHERE type = 'table'").Length != 0)
                        throw new InvalidDataException("This SQLite file is not an Xcmd issue store.");
                    Execute(Schema);
                    Execute("PRAGMA application_id = 1480805700");
                    application = "1480805700";
                    version = "1";
                }
                if (version == "1" && application == "1480805700")
                {
                    Execute("CREATE TABLE legacy_import (completed INTEGER PRIMARY KEY CHECK(completed = 1))");
                    Execute("PRAGMA user_version = 2");
                }
                else if (version != "2" || application != "1480805700")
                    throw new InvalidDataException("Unsupported Xcmd issue database format.");
                Execute("COMMIT");
            }
            else if (version != "2" || application != "1480805700")
                throw new InvalidDataException("Unsupported Xcmd issue database format. Keep the database for recovery.");
        }
        catch { Dispose(); throw; }
    }

    public void Execute(string sql, params string[] parameters) => Query(sql, parameters);

    public string[][] Query(string sql, params string[] parameters)
    {
        nint statement = 0;
        try
        {
            Check(Native.Prepare(connection, sql, -1, out statement, 0));
            for (var index = 0; index < parameters.Length; index++)
                Check(Native.BindText(statement, index + 1, parameters[index], Encoding.UTF8.GetByteCount(parameters[index]), -1));
            var rows = new List<string[]>();
            int status;
            while ((status = Native.Step(statement)) == 100)
            {
                var row = new string[Native.ColumnCount(statement)];
                for (var column = 0; column < row.Length; column++)
                    row[column] = Marshal.PtrToStringUTF8(Native.ColumnText(statement, column), Native.ColumnBytes(statement, column)) ?? "";
                rows.Add(row);
            }
            if (status != 101) Check(status);
            return rows.ToArray();
        }
        finally { if (statement != 0) Native.Finalize(statement); }
    }

    private void Check(int result)
    {
        if (result == 0) return;
        var message = connection == 0 ? "Could not open database" : Marshal.PtrToStringUTF8(Native.ErrorMessage(connection));
        throw new IOException($"Issue database error ({result}): {message}");
    }

    public void Dispose()
    {
        if (connection == 0) return;
        Native.Close(connection); connection = 0;
    }

    private static class Native
    {
        private const string Library = "winsqlite3.dll";
        [DllImport(Library, EntryPoint = "sqlite3_open_v2", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out nint database, int flags, nint vfs);
        [DllImport(Library, EntryPoint = "sqlite3_close_v2", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Close(nint database);
        [DllImport(Library, EntryPoint = "sqlite3_busy_timeout", CallingConvention = CallingConvention.Cdecl)]
        public static extern int BusyTimeout(nint database, int milliseconds);
        [DllImport(Library, EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Prepare(nint database, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, int bytes, out nint statement, nint tail);
        [DllImport(Library, EntryPoint = "sqlite3_bind_text", CallingConvention = CallingConvention.Cdecl)]
        public static extern int BindText(nint statement, int index, [MarshalAs(UnmanagedType.LPUTF8Str)] string value, int bytes, nint destructor);
        [DllImport(Library, EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Step(nint statement);
        [DllImport(Library, EntryPoint = "sqlite3_column_count", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ColumnCount(nint statement);
        [DllImport(Library, EntryPoint = "sqlite3_column_text", CallingConvention = CallingConvention.Cdecl)]
        public static extern nint ColumnText(nint statement, int column);
        [DllImport(Library, EntryPoint = "sqlite3_column_bytes", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ColumnBytes(nint statement, int column);
        [DllImport(Library, EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Finalize(nint statement);
        [DllImport(Library, EntryPoint = "sqlite3_errmsg", CallingConvention = CallingConvention.Cdecl)]
        public static extern nint ErrorMessage(nint database);
    }
}
