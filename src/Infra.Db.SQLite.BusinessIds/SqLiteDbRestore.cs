using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
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

        private readonly ILogger<SqLiteDbRestore> _logger;
        private readonly string _unzippedSqlFilesFolder;
        private readonly string _zippedSqlFile;

        public SqLiteDbRestore(ILogger<SqLiteDbRestore> logger)
        {
            _logger = logger;
            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _zippedSqlFile = $"{path}\\{ZippedSqlFile}";
            _unzippedSqlFilesFolder = $"{path}\\{UnzippedSqlFilesFolder}";
        }

        public async Task<bool> RestoreAsync(string connectionString, string dbFilePath, bool backup = false)
        {
            try
            {
                if (backup) BackupDatabaseFile(dbFilePath);

                await LoadSourceSqlFiles();

                ZipFile.ExtractToDirectory(_zippedSqlFile, _unzippedSqlFilesFolder, true);
                CreateDb(connectionString);
                _logger.LogInformation("db has been created created");
                Directory.Delete(_unzippedSqlFilesFolder, true);
                _logger.LogInformation("Extracted files have been deleted");
                _logger.LogInformation("BusinessId db is ready");
                File.Delete(_zippedSqlFile);
                _logger.LogInformation($"Delete {_zippedSqlFile} file");
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "error during business id db creation");
            }

            return false;
        }

        private async Task LoadSourceSqlFiles()
        {
            var file = File.Exists(_zippedSqlFile);
            if (!file)
            {
                _logger.LogInformation($"Get sql zip file from Github url: {GithubUrl}");
                await GetSqlFileFormGithub(_zippedSqlFile);
                _logger.LogInformation($"sql zip file  is downloaded from Github url: {GithubUrl}");
            }
        }

        private async Task GetSqlFileFormGithub(string zippedSqlFiles)
        {
            _logger.LogInformation($"Get sql zip file from Github url: {GithubUrl}");
            var fileInfo = new FileInfo(zippedSqlFiles);
            var response = await new HttpClient().GetAsync(GithubUrl);
            response.EnsureSuccessStatusCode();
            await using var ms = await response.Content.ReadAsStreamAsync();
            await using var fs = File.Create(fileInfo.FullName);
            ms.Seek(0, SeekOrigin.Begin);
            ms.CopyTo(fs);
        }

        private void CreateDb(string connectionString)
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            DeleteTable(connection);
            CreateTable(connection);
            LoadData(connection);
            CreateIndex(connection);
        }

        private void BackupDatabaseFile(string databaseFilePath)
        {
            if (!File.Exists(databaseFilePath)) return;
            _logger.LogInformation($"{databaseFilePath} file exists");

            _logger.LogInformation($"{databaseFilePath} file exists moving");
            // todo: async
            File.Move(databaseFilePath, $"{Guid.NewGuid()}-{databaseFilePath}");
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

        private void LoadData(SqliteConnection connection) => new DirectoryInfo(_unzippedSqlFilesFolder).GetFiles("*.sql").ToList().ForEach(f => ReadFile(connection, f));

        private void ReadFile(SqliteConnection connection, FileInfo f)
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
            _logger.LogInformation($"{f.FullName} has been saved in the sqlite database");
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