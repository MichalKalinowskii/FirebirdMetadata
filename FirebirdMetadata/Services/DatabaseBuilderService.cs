using DbMetaTool.Models;
using DbMetaTool.UnitOfWork;
using FirebirdSql.Data.FirebirdClient;
using FirebirdSql.Data.Isql;
using System.Data.Common;
using System.Runtime.InteropServices;
using System.Text.Unicode;

namespace DbMetaTool.Services
{
    public static class DatabaseBuilderService
    {
        public static Result DropDatabase(string connectionString)
        {
            try
            {
                FbConnection.ClearPool(connectionString);
                FbConnection.DropDatabase(connectionString);
                return Result.Success();
            }
            catch (FbException ex)
            {
                return Result.Failure(new Error("DatabaseDropFailed", ex.Message));
            }
            catch (Exception cleanupEx)
            {
                return Result.Failure(new Error("DatabaseDropCleanupFailed", $"Cleanup failed: {cleanupEx.Message}"));
            }
        }

        public static Result CreateDatabase(string conectionString)
        {
            try
            {
                FbConnection.CreateDatabase(conectionString);
                return Result.Success();
            }
            catch (FbException ex)
            {
                return Result.Failure(new Error("DatabaseCreationFailed", ex.Message));
            }
            catch (Exception cleanupEx)
            {
                return Result.Failure(new Error("DatabaseInitializationFailed", $"Cleanup failed: {cleanupEx.Message}"));
            }
        }

        // Zmieniamy zwracany typ na (Result, List<ScriptLogEntry>)
        public static (Result, List<ScriptLogEntry>) ExecuteSqlScriptsFromDirectory(string connectionString, string scriptsDirectory)
        {
            var logs = new List<ScriptLogEntry>();
            try
            {
                if (string.IsNullOrWhiteSpace(scriptsDirectory))
                {
                    return (Result.Failure(new Error("EmptyScriptPath", $"Scripts path not found: {scriptsDirectory}")), logs);
                }

                var singleSqlFilePath = string.Equals(Path.GetExtension(scriptsDirectory), ".sql", StringComparison.OrdinalIgnoreCase);
                var files = new List<string>();

                if (singleSqlFilePath)
                {
                    files.Add(scriptsDirectory);
                }
                else if (!Directory.Exists(scriptsDirectory))
                {
                    return (Result.Failure(new Error("DirectoryNotFound", $"Scripts directory not found: {scriptsDirectory}")), logs);
                }
                else
                {
                    files.AddRange(
                        Directory.EnumerateFiles(scriptsDirectory, "*.sql", SearchOption.TopDirectoryOnly)
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                        .ToList()
                    );
                }

                if (files.Count == 0)
                {
                    return (Result.Failure(new Error("NoScriptsFound", $"No *.sql files found in: {scriptsDirectory}")), logs);
                }

                // Przekazujemy listę logs do metody wykonawczej, żeby ją uzupełniła
                return ExecuteSqlScripts(connectionString, files);
            }
            catch (Exception ex)
            {
                return (Result.Failure(new Error("UnexpectedError", ex.Message)), logs);
            }
        }

        private static (Result, List<ScriptLogEntry>) ExecuteSqlScripts(string connectionString, IEnumerable<string> scriptFilePaths)
        {
            var logs = new List<ScriptLogEntry>();

            try
            {
                using var conn = new FbConnection(connectionString);
                conn.Open();

                foreach (var path in scriptFilePaths)
                {
                    var fileName = Path.GetFileName(path);
                    var scriptStopwatch = System.Diagnostics.Stopwatch.StartNew();

                    try
                    {
                        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                        {
                            logs.Add(new ScriptLogEntry { ScriptName = fileName, IsSuccess = false, Message = "File not found" });
                            return (Result.Failure(new Error("ScriptNotFound", $"Script file not found: {path}")), logs);
                        }

                        var sqlText = File.ReadAllText(path);
                        var fbScript = new FbScript(sqlText);
                        fbScript.Parse();

                        var batch = new FbBatchExecution(conn);
                        foreach (var stmt in fbScript.Results)
                        {
                            batch.Statements.Add(stmt);
                        }
                        batch.Execute(true);

                        scriptStopwatch.Stop();
                        // LOG SUCCESS
                        logs.Add(new ScriptLogEntry
                        {
                            ScriptName = fileName,
                            IsSuccess = true,
                            Message = "OK",
                            DurationMs = scriptStopwatch.ElapsedMilliseconds
                        });
                    }
                    catch (Exception ex)
                    {
                        scriptStopwatch.Stop();
                        // LOG FAILURE
                        logs.Add(new ScriptLogEntry
                        {
                            ScriptName = fileName,
                            IsSuccess = false,
                            Message = ex.Message,
                            DurationMs = scriptStopwatch.ElapsedMilliseconds
                        });

                        // Zwracamy błąd, ale też listę logów (żeby wiedzieć co przeszło, a co nie)
                        return (Result.Failure(new Error("ScriptExecutionFailed", $"Error in {fileName}: {ex.Message}")), logs);
                    }
                }

                return (Result.Success(), logs);
            }
            catch (Exception ex)
            {
                // Błąd ogólny połączenia itp.
                return (Result.Failure(new Error("UnexpectedError", ex.Message)), logs);
            }
        }
    }
}