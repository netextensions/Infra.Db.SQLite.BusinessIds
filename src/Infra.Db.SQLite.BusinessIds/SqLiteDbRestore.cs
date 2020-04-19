﻿using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using static NetExtensions.Constants;

namespace NetExtensions
{
    public class SqLiteDbRestore
    {
        private const string InsertInto = "INSERT INTO";
        private const string ZippedSqlFiles = "sqls.zip";
        private readonly ILogger<SqLiteDbRestore> _logger;

        public SqLiteDbRestore(ILogger<SqLiteDbRestore> logger)
        {
            _logger = logger;
        }

        public async Task<bool> RestoreAsync(bool backup = false)
        {
            try
            {
                if (File.Exists(DatabaseFile))
                {
                    _logger.LogInformation($"{DatabaseFile} file exists");
                    if (!backup)
                    {
                        _logger.LogInformation($"{DatabaseFile} file exists and won't make any backup");
                        return await Task.FromResult(true);
                    }

                    _logger.LogInformation($"{DatabaseFile} file exists moving");
                    File.Move(DatabaseFile, $"{Guid.NewGuid()}-{DatabaseFile}");
                }

                var file = File.Exists(ZippedSqlFiles);
                if (!file) await GetSqlFileFormGithub();

                var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                ZipFile.ExtractToDirectory(ZippedSqlFiles, path, true);
                CreateDb(path);
                _logger.LogInformation("db has been created created");
                Directory.Delete($"{path}\\sqls", true);
                _logger.LogInformation("Extracted files have been deleted");
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "error during business id db creation");
            }

            return false;
        }

        private async Task GetSqlFileFormGithub()
        {
            _logger.LogInformation("Get sql zip file from Github");
            var fileInfo = new FileInfo(ZippedSqlFiles);
            var response = await new HttpClient().GetAsync("https://github.com/netextensions/Infra.Db.SQLite.BusinessIds/raw/master/data/sqls.zip");
            response.EnsureSuccessStatusCode();
            await using var ms = await response.Content.ReadAsStreamAsync();
            await using var fs = File.Create(fileInfo.FullName);
            ms.Seek(0, SeekOrigin.Begin);
            ms.CopyTo(fs);
            _logger.LogInformation("sql zip file from Github is downloaded");
        }

        private void CreateDb(string path)
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder {DataSource = DatabaseFile}.ConnectionString);
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

        private void LoadData(string path, SqliteConnection connection)
        {
            foreach (var f in new DirectoryInfo($"{path}\\sqls").GetFiles("*.sql"))
            {
                _logger.LogInformation($"Save {f.FullName} to the sqlite database");
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
                _logger.LogInformation("Commit transaction");
            }
        }

        private void CreateIndex(SqliteConnection connection)
        {
            _logger.LogInformation("Create idx_Used_BusinessId index");
            using var idxTransaction = connection.BeginTransaction();
            var idxCommand = connection.CreateCommand();
            idxCommand.CommandText = @"CREATE INDEX idx_Used_BusinessId ON BusinessIds (Used,BusinessId) WHERE used = 0;";
            idxCommand.ExecuteNonQuery();
            idxTransaction.Commit();
            _logger.LogInformation("idx_Used_BusinessId is created");
        }
    }
}