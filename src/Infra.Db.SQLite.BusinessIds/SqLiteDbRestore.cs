using System;
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

        private readonly ILogger<SqLiteDbRestore> _logger;
        private readonly string _databaseFile;
        private readonly string _zippedSqlFile;
        private readonly string _unzippedSqlFilesFolder;

        public SqLiteDbRestore(ILogger<SqLiteDbRestore> logger)
        {
            _logger = logger;
            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _databaseFile = $"{path}\\{DatabaseFile}";
            _zippedSqlFile = $"{path}\\{ZippedSqlFile}";
            _unzippedSqlFilesFolder = $"{path}\\{UnzippedSqlFilesFolder}";
 
    }

        public async Task<bool> RestoreAsync(bool backup = false)
        {
            try
            {
                
          
                if (File.Exists(_databaseFile))
                {
                    _logger.LogInformation($"{_databaseFile} file exists");
                    if (!backup)
                    {
                        _logger.LogInformation($"{_databaseFile} file exists and won't make any backup");
                        return await Task.FromResult(true);
                    }

                    _logger.LogInformation($"{_databaseFile} file exists moving");
                    File.Move(_databaseFile, $"{Guid.NewGuid()}-{_databaseFile}");
                }

            
                var file = File.Exists(_zippedSqlFile);
                if (!file)
                {
                    _logger.LogInformation($"Get sql zip file from Github url: {GithubUrl}");
                    await GetSqlFileFormGithub(_zippedSqlFile);
                    _logger.LogInformation($"sql zip file  is downloaded from Github url: {GithubUrl}");
                }
           
                ZipFile.ExtractToDirectory(_zippedSqlFile, _unzippedSqlFilesFolder, true);
                CreateDb();
                _logger.LogInformation("db has been created created");
                Directory.Delete(_unzippedSqlFilesFolder, true);
                _logger.LogInformation("Extracted files have been deleted");
                _logger.LogInformation($"BusinessId db is ready");
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

        private void CreateDb()
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder {DataSource = _databaseFile}.ConnectionString);
            connection.Open();
            DeleteTable(connection);
            CreateTable(connection);
            LoadData(connection);
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

        private void LoadData(SqliteConnection connection)
        {
            foreach (var f in new DirectoryInfo(_unzippedSqlFilesFolder).GetFiles("*.sql"))
            {
                ReadFile(connection, f);
            }
        }

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