using DbMetaTool.UnitOfWork;
using FirebirdSql.Data.FirebirdClient;
using FirebirdSql.Data.Isql;
using System.Data.Common;
using System.Runtime.InteropServices;
using System.Text.Unicode;

namespace DbMetaTool.Services
{
    public static class DatabaseService
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
                return Result.Failure(new Error(
                    "DatabaseDropCleanupFailed", $"Cleanup failed: {cleanupEx.Message}"));
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
                return Result.Failure(new Error(
                    "DatabaseInitializationFailed", $"Cleanup failed: {cleanupEx.Message}"));
            }
        }

        public static Result ExecuteSqlScriptsFromDirectory(string connectionString, string scriptsDirectory)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(scriptsDirectory) || !Directory.Exists(scriptsDirectory))
                {
                    return Result.Failure(new Error("DirectoryNotFound", $"Scripts directory not found: {scriptsDirectory}"));
                }

                var files = Directory.EnumerateFiles(
                        scriptsDirectory,
                        "*.sql",
                        SearchOption.TopDirectoryOnly)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (files.Count == 0)
                {
                    return Result.Failure(new Error("NoScriptsFound", $"No *.sql files found in: {scriptsDirectory}"));
                }

                return ExecuteSqlScripts(connectionString, files);
            }
            catch (Exception ex)
            {
                return Result.Failure(new Error("UnexpectedError", ex.Message));
            }
        }

        private static Result ExecuteSqlScripts(string connectionString, IEnumerable<string> scriptFilePaths)
        {
            try
            {
                using var conn = new FbConnection(connectionString);
                conn.Open();

                foreach (var path in scriptFilePaths)
                {
                    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    {
                        return Result.Failure(new Error("ScriptNotFound", $"Script file not found: {path}"));
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
                }

                return Result.Success();
            }
            catch (FbException ex)
            {
                return Result.Failure(new Error("ScriptExecutionFailed", ex.Message));
            }
            catch (Exception ex)
            {
                return Result.Failure(new Error("UnexpectedError", ex.Message));
            }
        }
    }
}