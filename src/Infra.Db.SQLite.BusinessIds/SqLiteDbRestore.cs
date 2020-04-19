using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using Microsoft.Data.Sqlite;
using static NetExtensions.Constants;

namespace NetExtensions
{
    public class SqLiteDbRestore
    {
        private const string InsertInto = "INSERT INTO";
        private const string ZippedSqlFiles = "sqls.zip";

        public bool Restore(bool backup = false)
        {
            try
            {
  
                if (File.Exists(DatabaseFile))
                {
                    if (!backup)
                    {
                        return true;
                    }

                    File.Move(DatabaseFile, $"{Guid.NewGuid()} + {DatabaseFile}");
                }

                var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                ZipFile.ExtractToDirectory(ZippedSqlFiles, path, true);
                CreateDb(path);
                Directory.Delete($"{path}\\sqls", true);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
        
            }
            return false;
        }

        private static void CreateDb(string path)
        {
            using var connection = new SqliteConnection((new SqliteConnectionStringBuilder {DataSource = DatabaseFile}).ConnectionString);
            connection.Open();
            DeleteTable(connection);
            CreateTable(connection);
            LoadData(path, connection);
            CreateIndex(connection);
        }

        private static void DeleteTable(SqliteConnection connection)
        {
            var delTableCmd = connection.CreateCommand();
            delTableCmd.CommandText = "DROP TABLE IF EXISTS BusinessIds";
            delTableCmd.ExecuteNonQuery();
        }

        private static void CreateTable(SqliteConnection connection)
        {
            var createTableCmd = connection.CreateCommand();
            createTableCmd.CommandText = "CREATE TABLE [BusinessIds] ([BusinessId] bigint NOT NULL, [Used] bit DEFAULT 0 NOT NULL, [Activated] datetime NULL)";
            createTableCmd.ExecuteNonQuery();
        }

        private static void LoadData(string path, SqliteConnection connection)
        {
            foreach (var f in new DirectoryInfo($"{path}\\sqls").GetFiles("*.sql"))
            {
                using var transaction = connection.BeginTransaction();
                var insertCmd = connection.CreateCommand();
                string preLine = null;
                foreach (var line in File.ReadAllLines(f.FullName))
                {
                    if (line.StartsWith(InsertInto))
                    {
                        preLine = line;
                        continue;
                    }

                    if (preLine != null)
                    {
                        preLine = $"{preLine} {line}";
                    }
                    else
                    {
                        if (!line.StartsWith(InsertInto)) continue;
                    }

                    if (preLine == null) continue;

                    insertCmd.CommandText = preLine;
                    insertCmd.ExecuteNonQuery();
                    preLine = null;
                }

                transaction.Commit();
            }
        }

        private static void CreateIndex(SqliteConnection connection)
        {
            using var idxTransaction = connection.BeginTransaction();
            var idxCommand = connection.CreateCommand();
            idxCommand.CommandText = @"CREATE INDEX idx_Used_BusinessId ON BusinessIds (Used,BusinessId) WHERE used = 0;";
            idxCommand.ExecuteNonQuery();
            idxTransaction.Commit();
        }
    }
}
