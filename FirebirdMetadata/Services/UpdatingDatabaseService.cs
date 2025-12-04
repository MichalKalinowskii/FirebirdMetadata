using DbMetaTool.UnitOfWork;
using FirebirdSql.Data.FirebirdClient;
using FirebirdSql.Data.Isql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbMetaTool.Services
{
    public static class UpdatingDatabaseService
    {

        public static Result UpdateDatabase(FbConnection connection, string[] scriptFiles)
        {
            var domains = new List<string>();
            var tables = new List<string>();
            var procedures = new List<string>();

            foreach (var filePath in scriptFiles)
            {
                string scriptContent = File.ReadAllText(filePath);
                string upperContent = scriptContent.ToUpperInvariant();

                if (upperContent.Contains("CREATE DOMAIN") || upperContent.Contains("CREATE OR ALTER DOMAIN"))
                {
                    domains.Add(scriptContent);
                }
                else if (upperContent.Contains("CREATE TABLE") || upperContent.Contains("RECREATE TABLE"))
                {
                    tables.Add(scriptContent);
                }
                else if (upperContent.Contains("CREATE PROCEDURE") || upperContent.Contains("CREATE OR ALTER PROCEDURE"))
                {
                    procedures.Add(scriptContent);
                }
                else
                {
                    Console.WriteLine($"Pominięto plik (nieobsługiwany typ): {Path.GetFileName(filePath)}");
                }
            }

            var transaction = connection.BeginTransaction();

            try
            {
                ExecuteScriptContent(domains, connection, transaction, "Domeny");
                ExecuteScriptContent(tables, connection, transaction, "Tabele");
                ExecuteScriptContent(procedures, connection, transaction, "Procedury");

                transaction.Commit();
                Console.WriteLine("Aktualizacja bazy danych zakończona sukcesem.");
                return Result.Success();
            }
            catch (Exception ex)
            {
                if (transaction is not null)
                {
                    transaction.Rollback();
                }

                return Result.Failure(new Error("UpdateDatabaseError", $"Błąd podczas aktualizacji bazy, zmiany wycofane, {ex.Message}"));
            }
            finally
            {
                if (transaction is not null)
                {
                    transaction.Dispose();
                }
            }
        }

        /// <summary>
        /// Parsuje treść plików na pojedyncze komendy i wykonuje je.
        /// </summary>
        private static Result ExecuteScriptContent(List<string> scriptContents, FbConnection conn, FbTransaction trans, string category)
        {
            Console.WriteLine($"--- Przetwarzanie kategorii: {category} ---");

            foreach (var sqlText in scriptContents)
            {
                FbScript scriptParser = new FbScript(sqlText);
                var parseResult = scriptParser.Parse();

                foreach (var statment in scriptParser.Results)
                {
                    if (string.IsNullOrWhiteSpace(statment.Text))
                    {
                        continue;
                    }

                    if (IsCommandAllowed(statment.Text))
                    {
                        using (var cmd = new FbCommand(statment.Text, conn, trans))
                        {
                            try
                            {
                                cmd.ExecuteNonQuery();
                            }
                            catch (Exception ex)
                            {
                                return Result.Failure(new Error("SQLExecutionError", $"Błąd SQL: {ex.Message} \n W komendzie: {statment}"));
                            }
                        }
                    }
                }
            }

            return Result.Success();
        }

        /// <summary>
        /// Sprawdza, czy pojedyncza komenda dotyczy obsługiwanego obiektu.
        /// </summary>
        private static bool IsCommandAllowed(string sqlCommand)
        {
            string upper = sqlCommand.ToUpperInvariant().Trim();

            // Regex jest bezpieczniejszy niż Contains, aby uniknąć fałszywych dopasowań wewnątrz komentarzy lub stringów,
            // ale dla uproszczenia sprawdzamy początek komendy.

            return upper.StartsWith("CREATE DOMAIN") || upper.StartsWith("ALTER DOMAIN") ||
                   upper.StartsWith("CREATE TABLE") || upper.StartsWith("RECREATE TABLE") || upper.StartsWith("ALTER TABLE") ||
                   upper.StartsWith("CREATE PROCEDURE") || upper.StartsWith("ALTER PROCEDURE") || upper.StartsWith("CREATE OR ALTER PROCEDURE");
        }
    }
}
