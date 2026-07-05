using Microsoft.Data.Sqlite;
using PasswordManager.Models;

namespace PasswordManager.Services;

public class DatabaseService : IDisposable
{
    private readonly string _dbPath;
    private SqliteConnection? _conn;

    public DatabaseService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "SecureVault");
        Directory.CreateDirectory(dir);
        _dbPath = Path.Combine(dir, "vault.db");
    }

    public async Task InitializeAsync()
    {
        _conn = new SqliteConnection($"Data Source={_dbPath}");
        await _conn.OpenAsync();

        using var pragma = new SqliteCommand("PRAGMA journal_mode=WAL;", _conn);
        await pragma.ExecuteNonQueryAsync();

        await CreateTablesAsync();
        await MigrateAsync();
    }

    private async Task CreateTablesAsync()
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS UserProfile (
                Id           INTEGER PRIMARY KEY,
                FirstName    TEXT NOT NULL,
                LastName     TEXT NOT NULL,
                PasswordHash TEXT NOT NULL,
                PasswordSalt TEXT NOT NULL,
                CreatedAt    TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Folders (
                Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                Name      TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Passwords (
                Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                ServiceName         TEXT NOT NULL,
                EncryptedPassword   TEXT NOT NULL,
                EncryptedUsername   TEXT,
                EncryptedEmail      TEXT,
                EncryptedPhone      TEXT,
                FolderId            INTEGER REFERENCES Folders(Id) ON DELETE SET NULL,
                CreatedAt           TEXT NOT NULL,
                UpdatedAt           TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Servers (
                Id                INTEGER PRIMARY KEY AUTOINCREMENT,
                Name              TEXT NOT NULL,
                Host              TEXT NOT NULL,
                Port              TEXT,
                ServerType        TEXT,
                EncryptedUsername TEXT,
                EncryptedPassword TEXT,
                EncryptedNotes    TEXT,
                CreatedAt         TEXT NOT NULL,
                UpdatedAt         TEXT NOT NULL
            );
            """;

        using var cmd = new SqliteCommand(sql, _conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task MigrateAsync()
    {
        await AddColumnIfMissing("Passwords", "EncryptedUsername", "TEXT");
        await AddColumnIfMissing("Passwords", "FolderId", "INTEGER");
    }

    private async Task AddColumnIfMissing(string table, string column, string type)
    {
        try
        {
            using var cmd = new SqliteCommand(
                $"ALTER TABLE {table} ADD COLUMN {column} {type};", _conn);
            await cmd.ExecuteNonQueryAsync();
        }
        catch { /* Column already exists — safe to ignore */ }
    }

    public async Task<bool> IsInitializedAsync()
        => await GetUserProfileAsync() != null;

    // ─── UserProfile ───────────────────────────────────────────────────────────

    public async Task SaveUserProfileAsync(UserProfile p)
    {
        const string sql = """
            INSERT OR REPLACE INTO UserProfile (Id, FirstName, LastName, PasswordHash, PasswordSalt, CreatedAt)
            VALUES (1, @fn, @ln, @ph, @ps, @ca)
            """;
        using var cmd = new SqliteCommand(sql, _conn);
        cmd.Parameters.AddWithValue("@fn", p.FirstName);
        cmd.Parameters.AddWithValue("@ln", p.LastName);
        cmd.Parameters.AddWithValue("@ph", p.PasswordHash);
        cmd.Parameters.AddWithValue("@ps", p.PasswordSalt);
        cmd.Parameters.AddWithValue("@ca", p.CreatedAt.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<UserProfile?> GetUserProfileAsync()
    {
        using var cmd = new SqliteCommand(
            "SELECT Id, FirstName, LastName, PasswordHash, PasswordSalt, CreatedAt FROM UserProfile WHERE Id=1",
            _conn);
        using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return new UserProfile
        {
            Id           = r.GetInt32(0),
            FirstName    = r.GetString(1),
            LastName     = r.GetString(2),
            PasswordHash = r.GetString(3),
            PasswordSalt = r.GetString(4),
            CreatedAt    = DateTime.Parse(r.GetString(5))
        };
    }

    // ─── Folders ───────────────────────────────────────────────────────────────

    public async Task<List<Folder>> GetFoldersAsync()
    {
        const string sql = "SELECT Id, Name, CreatedAt FROM Folders ORDER BY Name COLLATE NOCASE";
        using var cmd = new SqliteCommand(sql, _conn);
        var list = new List<Folder>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new Folder
            {
                Id        = r.GetInt32(0),
                Name      = r.GetString(1),
                CreatedAt = DateTime.Parse(r.GetString(2))
            });
        }
        return list;
    }

    public async Task<int> AddFolderAsync(Folder f)
    {
        const string sql = """
            INSERT INTO Folders (Name, CreatedAt) VALUES (@n, @ca);
            SELECT last_insert_rowid();
            """;
        using var cmd = new SqliteCommand(sql, _conn);
        cmd.Parameters.AddWithValue("@n",  f.Name);
        cmd.Parameters.AddWithValue("@ca", f.CreatedAt.ToString("O"));
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task DeleteFolderAsync(int id)
    {
        using var unassign = new SqliteCommand(
            "UPDATE Passwords SET FolderId = NULL WHERE FolderId = @id", _conn);
        unassign.Parameters.AddWithValue("@id", id);
        await unassign.ExecuteNonQueryAsync();

        using var del = new SqliteCommand("DELETE FROM Folders WHERE Id = @id", _conn);
        del.Parameters.AddWithValue("@id", id);
        await del.ExecuteNonQueryAsync();
    }

    // ─── Passwords ─────────────────────────────────────────────────────────────

    public async Task<List<PasswordEntry>> GetAllAsync()
    {
        const string sql = """
            SELECT Id, ServiceName, EncryptedPassword, EncryptedUsername,
                   EncryptedEmail, EncryptedPhone, CreatedAt, UpdatedAt, FolderId
            FROM Passwords ORDER BY ServiceName COLLATE NOCASE
            """;
        return await ReadEntries(sql);
    }

    public async Task<List<PasswordEntry>> GetByFolderAsync(int folderId)
    {
        const string sql = """
            SELECT Id, ServiceName, EncryptedPassword, EncryptedUsername,
                   EncryptedEmail, EncryptedPhone, CreatedAt, UpdatedAt, FolderId
            FROM Passwords WHERE FolderId = @fid ORDER BY ServiceName COLLATE NOCASE
            """;
        using var cmd = new SqliteCommand(sql, _conn);
        cmd.Parameters.AddWithValue("@fid", folderId);
        return await ReadEntries(cmd);
    }

    public async Task<PasswordEntry?> GetByIdAsync(int id)
    {
        using var cmd = new SqliteCommand(
            """
            SELECT Id, ServiceName, EncryptedPassword, EncryptedUsername,
                   EncryptedEmail, EncryptedPhone, CreatedAt, UpdatedAt, FolderId
            FROM Passwords WHERE Id=@id
            """, _conn);
        cmd.Parameters.AddWithValue("@id", id);
        var list = await ReadEntries(cmd);
        return list.Count > 0 ? list[0] : null;
    }

    public async Task<int> AddAsync(PasswordEntry e)
    {
        const string sql = """
            INSERT INTO Passwords
                (ServiceName, EncryptedPassword, EncryptedUsername, EncryptedEmail,
                 EncryptedPhone, FolderId, CreatedAt, UpdatedAt)
            VALUES (@sn, @ep, @eu, @ee, @ephone, @fid, @ca, @ua);
            SELECT last_insert_rowid();
            """;
        using var cmd = new SqliteCommand(sql, _conn);
        BindEntry(cmd, e);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task UpdateAsync(PasswordEntry e)
    {
        const string sql = """
            UPDATE Passwords
            SET ServiceName=@sn, EncryptedPassword=@ep, EncryptedUsername=@eu,
                EncryptedEmail=@ee, EncryptedPhone=@ephone, FolderId=@fid, UpdatedAt=@ua
            WHERE Id=@id
            """;
        using var cmd = new SqliteCommand(sql, _conn);
        BindEntry(cmd, e);
        cmd.Parameters.AddWithValue("@id", e.Id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int id)
    {
        using var cmd = new SqliteCommand("DELETE FROM Passwords WHERE Id=@id", _conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    private static void BindEntry(SqliteCommand cmd, PasswordEntry e)
    {
        cmd.Parameters.AddWithValue("@sn",     e.ServiceName);
        cmd.Parameters.AddWithValue("@ep",     e.EncryptedPassword);
        cmd.Parameters.AddWithValue("@eu",     e.EncryptedUsername ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@ee",     e.EncryptedEmail    ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@ephone", e.EncryptedPhone    ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@fid",    e.FolderId.HasValue ? (object)e.FolderId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@ca",     e.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@ua",     e.UpdatedAt.ToString("O"));
    }

    private async Task<List<PasswordEntry>> ReadEntries(string sql)
    {
        using var cmd = new SqliteCommand(sql, _conn);
        return await ReadEntries(cmd);
    }

    private static async Task<List<PasswordEntry>> ReadEntries(SqliteCommand cmd)
    {
        var list = new List<PasswordEntry>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new PasswordEntry
            {
                Id                = r.GetInt32(0),
                ServiceName       = r.GetString(1),
                EncryptedPassword = r.GetString(2),
                EncryptedUsername = r.IsDBNull(3) ? null : r.GetString(3),
                EncryptedEmail    = r.IsDBNull(4) ? null : r.GetString(4),
                EncryptedPhone    = r.IsDBNull(5) ? null : r.GetString(5),
                CreatedAt         = DateTime.Parse(r.GetString(6)),
                UpdatedAt         = DateTime.Parse(r.GetString(7)),
                FolderId          = r.IsDBNull(8) ? null : r.GetInt32(8)
            });
        }
        return list;
    }

    // ─── Servers ───────────────────────────────────────────────────────────────

    public async Task<List<ServerEntry>> GetAllServersAsync()
    {
        const string sql = """
            SELECT Id, Name, Host, Port, ServerType,
                   EncryptedUsername, EncryptedPassword, EncryptedNotes,
                   CreatedAt, UpdatedAt
            FROM Servers ORDER BY Name COLLATE NOCASE
            """;
        using var cmd = new SqliteCommand(sql, _conn);
        return await ReadServers(cmd);
    }

    public async Task<List<ServerEntry>> SearchServersAsync(string query)
    {
        const string sql = """
            SELECT Id, Name, Host, Port, ServerType,
                   EncryptedUsername, EncryptedPassword, EncryptedNotes,
                   CreatedAt, UpdatedAt
            FROM Servers WHERE Name LIKE @q OR Host LIKE @q ORDER BY Name COLLATE NOCASE
            """;
        using var cmd = new SqliteCommand(sql, _conn);
        cmd.Parameters.AddWithValue("@q", $"%{query}%");
        return await ReadServers(cmd);
    }

    public async Task<ServerEntry?> GetServerByIdAsync(int id)
    {
        using var cmd = new SqliteCommand(
            """
            SELECT Id, Name, Host, Port, ServerType,
                   EncryptedUsername, EncryptedPassword, EncryptedNotes,
                   CreatedAt, UpdatedAt
            FROM Servers WHERE Id=@id
            """, _conn);
        cmd.Parameters.AddWithValue("@id", id);
        var list = await ReadServers(cmd);
        return list.Count > 0 ? list[0] : null;
    }

    public async Task<int> AddServerAsync(ServerEntry s)
    {
        const string sql = """
            INSERT INTO Servers
                (Name, Host, Port, ServerType, EncryptedUsername,
                 EncryptedPassword, EncryptedNotes, CreatedAt, UpdatedAt)
            VALUES (@n, @h, @p, @st, @eu, @ep, @en, @ca, @ua);
            SELECT last_insert_rowid();
            """;
        using var cmd = new SqliteCommand(sql, _conn);
        BindServer(cmd, s);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task UpdateServerAsync(ServerEntry s)
    {
        const string sql = """
            UPDATE Servers
            SET Name=@n, Host=@h, Port=@p, ServerType=@st,
                EncryptedUsername=@eu, EncryptedPassword=@ep,
                EncryptedNotes=@en, UpdatedAt=@ua
            WHERE Id=@id
            """;
        using var cmd = new SqliteCommand(sql, _conn);
        BindServer(cmd, s);
        cmd.Parameters.AddWithValue("@id", s.Id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteServerAsync(int id)
    {
        using var cmd = new SqliteCommand("DELETE FROM Servers WHERE Id=@id", _conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    private static void BindServer(SqliteCommand cmd, ServerEntry s)
    {
        cmd.Parameters.AddWithValue("@n",  s.Name);
        cmd.Parameters.AddWithValue("@h",  s.Host);
        cmd.Parameters.AddWithValue("@p",  s.Port         ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@st", s.ServerType   ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@eu", s.EncryptedUsername ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@ep", s.EncryptedPassword ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@en", s.EncryptedNotes    ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@ca", s.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@ua", s.UpdatedAt.ToString("O"));
    }

    private static async Task<List<ServerEntry>> ReadServers(SqliteCommand cmd)
    {
        var list = new List<ServerEntry>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new ServerEntry
            {
                Id                = r.GetInt32(0),
                Name              = r.GetString(1),
                Host              = r.GetString(2),
                Port              = r.IsDBNull(3) ? null : r.GetString(3),
                ServerType        = r.IsDBNull(4) ? null : r.GetString(4),
                EncryptedUsername = r.IsDBNull(5) ? null : r.GetString(5),
                EncryptedPassword = r.IsDBNull(6) ? null : r.GetString(6),
                EncryptedNotes    = r.IsDBNull(7) ? null : r.GetString(7),
                CreatedAt         = DateTime.Parse(r.GetString(8)),
                UpdatedAt         = DateTime.Parse(r.GetString(9))
            });
        }
        return list;
    }

    public void Dispose() => _conn?.Dispose();
}
