using DbMetaTool.UnitOfWork;
using FirebirdSql.Data.FirebirdClient;
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

            // 2. Analiza i segregacja skryptów (Tylko obsługiwane elementy)
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
                ExecuteBatch(domains, connection, transaction, "Domeny");
                ExecuteBatch(tables, connection, transaction, "Tabele");
                ExecuteBatch(procedures, connection, transaction, "Procedury");

                // Zatwierdzenie zmian
                transaction.Commit();
                Console.WriteLine("Aktualizacja bazy danych zakończona sukcesem.");
                return Result.Success();
            }
            catch (Exception ex)
            {
                // Wycofanie zmian w przypadku błędu
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
        /// Metoda pomocnicza do wykonywania listy skryptów.
        /// </summary>
        private static void ExecuteBatch(List<string> scripts, FbConnection conn, FbTransaction trans, string categoryName)
        {
            if (scripts.Count == 0)
            {
                return;
            }

            Console.WriteLine($"Wykonuję skrypty dla kategorii: {categoryName}...");

            foreach (var script in scripts)
            {
                using (var command = new FbCommand(script, conn, trans))
                {
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}
