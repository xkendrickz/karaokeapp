using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using KaraokeApp.Models;

namespace KaraokeApp.Data
{
    public class DatabaseService
    {
        private static DatabaseService _instance;
        public static DatabaseService Instance =>
            _instance ?? (_instance = new DatabaseService());

        private readonly string _dbPath;
        private readonly string _connectionString;

        private DatabaseService()
        {
            string appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "KaraokeApp");
            Directory.CreateDirectory(appData);
            _dbPath           = Path.Combine(appData, "karaoke.db");
            _connectionString = "Data Source=" + _dbPath + ";Version=3;";
            InitializeDatabase();
        }

        // ── SCHEMA ──────────────────────────────────────────────────

        private void InitializeDatabase()
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();

                // Execute each DDL statement separately to avoid multi-statement parser issues
                Exec(conn, @"
                    CREATE TABLE IF NOT EXISTS Categories (
                        Id       INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name     TEXT NOT NULL UNIQUE,
                        Keywords TEXT DEFAULT ''
                    )");

                Exec(conn, @"
                    CREATE TABLE IF NOT EXISTS Songs (
                        Id             INTEGER PRIMARY KEY AUTOINCREMENT,
                        Title          TEXT NOT NULL,
                        Artist         TEXT DEFAULT '',
                        FilePath       TEXT DEFAULT '',
                        VocalFilePath  TEXT DEFAULT '',
                        CategoryId     INTEGER,
                        SourceType     TEXT DEFAULT 'local',
                        YoutubeId      TEXT DEFAULT '',
                        YoutubeUrl     TEXT DEFAULT '',
                        ThumbnailUrl   TEXT DEFAULT '',
                        DateAdded      TEXT DEFAULT (datetime('now'))
                    )");

                Exec(conn, @"
                    CREATE TABLE IF NOT EXISTS Settings (
                        Key   TEXT PRIMARY KEY,
                        Value TEXT DEFAULT ''
                    )");

                Exec(conn, "CREATE INDEX IF NOT EXISTS idx_songs_title    ON Songs(Title)");
                Exec(conn, "CREATE INDEX IF NOT EXISTS idx_songs_artist   ON Songs(Artist)");
                Exec(conn, "CREATE INDEX IF NOT EXISTS idx_songs_category ON Songs(CategoryId)");

                // Safe migrations for databases created by older versions
                // Old column was named "References" – rename to Keywords if present
                MigrateRenameReferencesToKeywords(conn);
                // Add Keywords if still missing (fresh schema)
                MigrateAddColumn(conn, "Categories", "Keywords", "TEXT DEFAULT ''");
                // Add DateAdded if missing — must use a literal default (no functions in ALTER TABLE)
                MigrateAddColumn(conn, "Songs", "DateAdded", "TEXT DEFAULT ''");
                // Backfill any rows that got the empty-string default
                Exec(conn, "UPDATE Songs SET DateAdded = datetime('now') WHERE DateAdded = '' OR DateAdded IS NULL");

                // Seed default categories if table is empty
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM Categories";
                    long count = (long)cmd.ExecuteScalar();
                    if (count == 0)
                    {
                        var seeds = new[]
                        {
                            new[] { "Indonesia", "indonesia,indo,indon" },
                            new[] { "English",   "english,inggris,barat,western,west" },
                            new[] { "Mandarin",  "mandarin,chinese,china,cina,tiongkok" },
                            new[] { "Jepang",    "jepang,japan,japanese,nihon" },
                            new[] { "Korea",     "korea,korean,kpop,k-pop" }
                        };
                        foreach (var s in seeds)
                        {
                            cmd.CommandText = "INSERT INTO Categories (Name, Keywords) VALUES (@n, @k)";
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@n", s[0]);
                            cmd.Parameters.AddWithValue("@k", s[1]);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        // ─── MIGRATION HELPERS ───────────────────────────────────────

        /// <summary>Execute a single SQL statement (no semicolons).</summary>
        private static void Exec(SQLiteConnection conn, string sql)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql.Trim().TrimEnd(';');
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// If the Categories table has a column called "References" (old reserved-word mistake),
        /// copy its data into a new "Keywords" column and drop the old one via table rebuild.
        /// </summary>
        private static void MigrateRenameReferencesToKeywords(SQLiteConnection conn)
        {
            bool hasOldCol = false;
            bool hasNewCol = false;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info(Categories)";
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        string colName = r.GetString(1);
                        if (string.Equals(colName, "References", StringComparison.OrdinalIgnoreCase))
                            hasOldCol = true;
                        if (string.Equals(colName, "Keywords", StringComparison.OrdinalIgnoreCase))
                            hasNewCol = true;
                    }
            }

            if (!hasOldCol) return; // nothing to do

            if (!hasNewCol)
            {
                // Add Keywords column first
                Exec(conn, "ALTER TABLE Categories ADD COLUMN Keywords TEXT DEFAULT ''");
            }

            // Copy data from old column to new column
            // We must quote "References" with double-quotes since it's a reserved word
            Exec(conn, "UPDATE Categories SET Keywords = \"References\" WHERE Keywords = '' OR Keywords IS NULL");

            // SQLite has no DROP COLUMN before 3.35 – rebuild the table to remove the old column
            // Check if DROP COLUMN is supported
            bool canDropColumn = false;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT sqlite_version()";
                string ver = cmd.ExecuteScalar()?.ToString() ?? "0";
                var parts = ver.Split('.');
                if (parts.Length >= 2 &&
                    int.TryParse(parts[0], out int major) &&
                    int.TryParse(parts[1], out int minor))
                    canDropColumn = major > 3 || (major == 3 && minor >= 35);
            }

            if (canDropColumn)
            {
                try { Exec(conn, "ALTER TABLE Categories DROP COLUMN \"References\""); }
                catch { /* ignore if it fails – Keywords already has the data */ }
            }
            // If DROP COLUMN isn't supported, old column stays but is ignored by all queries
        }

        /// <summary>Add a column if it doesn't already exist.</summary>
        private static void MigrateAddColumn(SQLiteConnection conn,
            string table, string column, string definition)
        {
            bool exists = false;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info(" + table + ")";
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        if (string.Equals(r.GetString(1), column,
                            StringComparison.OrdinalIgnoreCase))
                        { exists = true; break; }
            }
            if (!exists)
                Exec(conn, "ALTER TABLE " + table + " ADD COLUMN " + column + " " + definition);
        }

        // ── CATEGORIES ───────────────────────────────────────────────

        public List<Category> GetAllCategories()
        {
            var list = new List<Category>();
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        "SELECT Id, Name, COALESCE(Keywords,'') FROM Categories ORDER BY Name";
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            list.Add(new Category
                            {
                                Id       = r.GetInt32(0),
                                Name     = r.GetString(1),
                                Keywords = r.GetString(2)
                            });
                }
            }
            return list;
        }

        /// <summary>
        /// Find a category whose Name or Keywords contains the given token (case-insensitive).
        /// </summary>
        public Category FindCategoryByReference(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return null;
            string kw = keyword.Trim().ToLower();
            foreach (var cat in GetAllCategories())
            {
                if (cat.Name.ToLower() == kw) return cat;
                if (!string.IsNullOrEmpty(cat.Keywords))
                    foreach (var r in cat.Keywords.Split(','))
                        if (r.Trim().ToLower() == kw) return cat;
            }
            return null;
        }

        public void AddCategory(string name, string keywords = "")
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        "INSERT INTO Categories (Name, Keywords) VALUES (@n, @k)";
                    cmd.Parameters.AddWithValue("@n", name);
                    cmd.Parameters.AddWithValue("@k", keywords ?? "");
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateCategory(int id, string name, string keywords = "")
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        "UPDATE Categories SET Name=@n, Keywords=@k WHERE Id=@id";
                    cmd.Parameters.AddWithValue("@n",  name);
                    cmd.Parameters.AddWithValue("@k",  keywords ?? "");
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void DeleteCategory(int id)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE Songs SET CategoryId=NULL WHERE CategoryId=@id";
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                    cmd.Parameters.Clear();
                    cmd.CommandText = "DELETE FROM Categories WHERE Id=@id";
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ── SONGS ────────────────────────────────────────────────────

        public List<Song> GetAllSongs(int categoryId = 0, string searchText = "",
                                       bool searchByTitle = true, string sourceType = "local")
        {
            var list = new List<Song>();
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    string filter = "WHERE s.SourceType=@sourceType";
                    if (categoryId > 0)
                        filter += " AND s.CategoryId=@catId";
                    if (!string.IsNullOrWhiteSpace(searchText))
                        filter += searchByTitle
                            ? " AND LOWER(s.Title) LIKE @search"
                            : " AND LOWER(s.Artist) LIKE @search";

                    cmd.CommandText =
                        "SELECT s.Id, s.Title, s.Artist, s.FilePath, s.VocalFilePath," +
                        "       s.CategoryId, c.Name, s.SourceType," +
                        "       s.YoutubeId, s.YoutubeUrl, s.ThumbnailUrl," +
                        "       COALESCE(s.DateAdded,'')" +
                        " FROM Songs s" +
                        " LEFT JOIN Categories c ON s.CategoryId = c.Id " +
                        filter +
                        " ORDER BY s.Title";

                    cmd.Parameters.AddWithValue("@sourceType", sourceType);
                    if (categoryId > 0)
                        cmd.Parameters.AddWithValue("@catId", categoryId);
                    if (!string.IsNullOrWhiteSpace(searchText))
                        cmd.Parameters.AddWithValue("@search",
                            "%" + searchText.ToLower() + "%");

                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) list.Add(MapSong(r));
                }
            }
            return list;
        }

        public List<Song> GetYoutubeSongs(int categoryId = 0, string searchText = "",
                                           bool searchByTitle = true)
            => GetAllSongs(categoryId, searchText, searchByTitle, "youtube");

        public void AddSong(Song song)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO Songs
                            (Title, Artist, FilePath, VocalFilePath, CategoryId,
                             SourceType, YoutubeId, YoutubeUrl, ThumbnailUrl, DateAdded)
                        VALUES
                            (@title, @artist, @filePath, @vocalFilePath, @categoryId,
                             @sourceType, @youtubeId, @youtubeUrl, @thumbnailUrl, @dateAdded)";
                    SetSongParams(cmd, song);
                    cmd.ExecuteNonQuery();
                    song.Id = (int)conn.LastInsertRowId;
                }
            }
        }

        public void UpdateSong(Song song)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        UPDATE Songs SET
                            Title=@title, Artist=@artist, FilePath=@filePath,
                            VocalFilePath=@vocalFilePath, CategoryId=@categoryId,
                            SourceType=@sourceType, YoutubeId=@youtubeId,
                            YoutubeUrl=@youtubeUrl, ThumbnailUrl=@thumbnailUrl,
                            DateAdded=@dateAdded
                        WHERE Id=@id";
                    SetSongParams(cmd, song);
                    cmd.Parameters.AddWithValue("@id", song.Id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void DeleteSong(int id)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM Songs WHERE Id=@id";
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ── SETTINGS ─────────────────────────────────────────────────

        public AppSettings LoadSettings()
        {
            var s = new AppSettings();
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT Key, Value FROM Settings";
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                        {
                            string key = r.GetString(0);
                            string val = r.IsDBNull(1) ? "" : r.GetString(1);
                            switch (key)
                            {
                                case "YoutubeApiKey":     s.YoutubeApiKey     = val; break;
                                case "Volume":            s.Volume            = int.TryParse(val, out int v) ? v : 100; break;
                                case "PitchSemitones":    s.PitchSemitones    = int.TryParse(val, out int p) ? p : 0;   break;
                                case "VocalOn":           s.VocalOn           = val == "1"; break;
                                case "Repeat":            s.Repeat            = val == "1"; break;
                                case "LastCategoryId":    s.LastCategoryId    = int.TryParse(val, out int c) ? c : 0;   break;
                                case "LastTab":           s.LastTab           = int.TryParse(val, out int t) ? t : 0;   break;
                                case "FilenameSeparator": s.FilenameSeparator = string.IsNullOrEmpty(val) ? "#" : val;  break;
                            }
                        }
                }
            }
            return s;
        }

        public void SaveSetting(string key, string value)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        "INSERT OR REPLACE INTO Settings (Key, Value) VALUES (@k, @v)";
                    cmd.Parameters.AddWithValue("@k", key);
                    cmd.Parameters.AddWithValue("@v", value ?? "");
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void SaveSettings(AppSettings s)
        {
            SaveSetting("YoutubeApiKey",     s.YoutubeApiKey     ?? "");
            SaveSetting("Volume",            s.Volume.ToString());
            SaveSetting("PitchSemitones",    s.PitchSemitones.ToString());
            SaveSetting("VocalOn",           s.VocalOn           ? "1" : "0");
            SaveSetting("Repeat",            s.Repeat            ? "1" : "0");
            SaveSetting("LastCategoryId",    s.LastCategoryId.ToString());
            SaveSetting("LastTab",           s.LastTab.ToString());
            SaveSetting("FilenameSeparator", s.FilenameSeparator ?? "#");
        }

        // ── PRIVATE HELPERS ───────────────────────────────────────────

        private static Song MapSong(IDataReader r)
        {
            DateTime dateAdded = DateTime.Now;
            if (!r.IsDBNull(11))
                DateTime.TryParse(r.GetString(11), out dateAdded);
            return new Song
            {
                Id            = r.GetInt32(0),
                Title         = r.IsDBNull(1)  ? "" : r.GetString(1),
                Artist        = r.IsDBNull(2)  ? "" : r.GetString(2),
                FilePath      = r.IsDBNull(3)  ? "" : r.GetString(3),
                VocalFilePath = r.IsDBNull(4)  ? "" : r.GetString(4),
                CategoryId    = r.IsDBNull(5)  ? 0  : r.GetInt32(5),
                CategoryName  = r.IsDBNull(6)  ? "" : r.GetString(6),
                SourceType    = r.IsDBNull(7)  ? "local" : r.GetString(7),
                YoutubeId     = r.IsDBNull(8)  ? "" : r.GetString(8),
                YoutubeUrl    = r.IsDBNull(9)  ? "" : r.GetString(9),
                ThumbnailUrl  = r.IsDBNull(10) ? "" : r.GetString(10),
                DateAdded     = dateAdded
            };
        }

        private static void SetSongParams(SQLiteCommand cmd, Song song)
        {
            cmd.Parameters.AddWithValue("@title",         song.Title         ?? "");
            cmd.Parameters.AddWithValue("@artist",        song.Artist        ?? "");
            cmd.Parameters.AddWithValue("@filePath",      song.FilePath      ?? "");
            cmd.Parameters.AddWithValue("@vocalFilePath", song.VocalFilePath ?? "");
            cmd.Parameters.AddWithValue("@categoryId",
                song.CategoryId > 0 ? (object)song.CategoryId : DBNull.Value);
            cmd.Parameters.AddWithValue("@sourceType",    song.SourceType    ?? "local");
            cmd.Parameters.AddWithValue("@youtubeId",     song.YoutubeId     ?? "");
            cmd.Parameters.AddWithValue("@youtubeUrl",    song.YoutubeUrl    ?? "");
            cmd.Parameters.AddWithValue("@thumbnailUrl",  song.ThumbnailUrl  ?? "");
            cmd.Parameters.AddWithValue("@dateAdded",
                song.DateAdded.ToString("yyyy-MM-dd HH:mm:ss"));
        }
    }
}
