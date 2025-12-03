using DbMetaTool.Services;
using DbMetaTool.UnitOfWork;
using System;
using System.IO;

namespace DbMetaTool
{
    public static class Program
    {
        // Przykładowe wywołania:
        // DbMetaTool build-db --db-dir "C:\db\fb5" --scripts-dir "C:\scripts"
        // DbMetaTool export-scripts --connection-string "..." --output-dir "C:\out"
        // DbMetaTool update-db --connection-string "..." --scripts-dir "C:\scripts"
        public static int Main(string[] args)
        {
            BuildDatabase("C:\\Users\\admin\\Desktop\\firebirdDatabases", "C:\\Users\\admin\\Desktop\\firebirdDatabases\\test");
            if (args.Length == 0)
            {
                Console.WriteLine("Użycie:");
                Console.WriteLine("  build-db --db-dir* <ścieżka> --scripts-dir <ścieżka>");
                Console.WriteLine("  export-scripts --connection-string <connStr> --output-dir <ścieżka>");
                Console.WriteLine("  update-db --connection-string <connStr> --scripts-dir <ścieżka>");
                return 1;
            }

            try
            {
                var command = args[0].ToLowerInvariant();

                switch (command)
                {
                    case "build-db":
                        {
                            string dbDir = GetArgValue(args, "--db-dir");
                            string scriptsDir = GetArgValue(args, "--scripts-dir");

                            BuildDatabase(dbDir, scriptsDir);
                            Console.WriteLine("Baza danych została zbudowana pomyślnie.");
                            return 0;
                        }

                    case "export-scripts":
                        {
                            string connStr = GetArgValue(args, "--connection-string");
                            string outputDir = GetArgValue(args, "--output-dir");

                            ExportScripts(connStr, outputDir);
                            Console.WriteLine("Skrypty zostały wyeksportowane pomyślnie.");
                            return 0;
                        }

                    case "update-db":
                        {
                            string connStr = GetArgValue(args, "--connection-string");
                            string scriptsDir = GetArgValue(args, "--scripts-dir");

                            UpdateDatabase(connStr, scriptsDir);
                            Console.WriteLine("Baza danych została zaktualizowana pomyślnie.");
                            return 0;
                        }

                    default:
                        Console.WriteLine($"Nieznane polecenie: {command}");
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Błąd: " + ex.Message);
                return -1;
            }
        }

        private static string GetArgValue(string[] args, string name)
        {
            int idx = Array.IndexOf(args, name);
            if (idx == -1 || idx + 1 >= args.Length)
                throw new ArgumentException($"Brak wymaganego parametru {name}");
            return args[idx + 1];
        }

        /// <summary>
        /// Buduje nową bazę danych Firebird 5.0 na podstawie skryptów.
        /// </summary>
        public static void BuildDatabase(string databaseDirectory, string scriptsDirectory)
        {
            // TODO:
            // 1) Utwórz pustą bazę danych FB 5.0 w katalogu databaseDirectory.
            // 2) Wczytaj i wykonaj kolejno skrypty z katalogu scriptsDirectory
            //    (tylko domeny, tabele, procedury).
            // 3) Obsłuż błędy i wyświetl raport.
            //throw new NotImplementedException();

            bool hasFdbExtension = string.Equals(Path.GetExtension(databaseDirectory), ".fdb", StringComparison.OrdinalIgnoreCase);

            if (!hasFdbExtension)
            {
                string generatedFileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid()}.fdb";
                databaseDirectory = Path.Combine(databaseDirectory, generatedFileName);
            }

            if (!string.IsNullOrWhiteSpace(databaseDirectory) && File.Exists(databaseDirectory))
            {
                Console.WriteLine($"Database file already exists: {databaseDirectory}");
                return;
            }

            string connectionString = $@"
                    User=SYSDBA;Password=zaq1@WSX;
                    Database={databaseDirectory};
                    DataSource=127.0.0.1;
                    Port=3050;
                    Dialect=3;
                    Charset=UTF8;";

            var databaseCreationResult = DatabaseService.CreateDatabase(connectionString);

            if (databaseCreationResult.IsFailure)
            {
                Console.WriteLine($"Failed to create database - {databaseDirectory}. " + databaseCreationResult.Error.Description);
                return;
            }

            var scriptExecutionResult = DatabaseService.ExecuteSqlScriptsFromDirectory(connectionString, scriptsDirectory);

            if (scriptExecutionResult.IsFailure)
            {
                var dropResult = DatabaseService.DropDatabase(connectionString);

                if (dropResult.IsFailure)
                {
                    Console.WriteLine($"FATAL ERROR: Failed to drop database after script execution failure. Remove database file by hand - {databaseDirectory}. " + dropResult.Error.Description);
                    return;
                }

                Console.WriteLine($"Failed to execute scripts from directory - {scriptsDirectory}. " + scriptExecutionResult.Error.Description);
                return;
            }
        }

        /// <summary>
        /// Generuje skrypty metadanych z istniejącej bazy danych Firebird 5.0.
        /// </summary>
        public static void ExportScripts(string connectionString, string outputDirectory)
        {
            // TODO:
            // 1) Połącz się z bazą danych przy użyciu connectionString.
            // 2) Pobierz metadane domen, tabel (z kolumnami) i procedur.
            // 3) Wygeneruj pliki .sql / .json / .txt w outputDirectory.
            throw new NotImplementedException();
        }

        /// <summary>
        /// Aktualizuje istniejącą bazę danych Firebird 5.0 na podstawie skryptów.
        /// </summary>
        public static void UpdateDatabase(string connectionString, string scriptsDirectory)
        {
            // TODO:
            // 1) Połącz się z bazą danych przy użyciu connectionString.
            // 2) Wykonaj skrypty z katalogu scriptsDirectory (tylko obsługiwane elementy).
            // 3) Zadbaj o poprawną kolejność i bezpieczeństwo zmian.
            throw new NotImplementedException();
        }
    }
}
