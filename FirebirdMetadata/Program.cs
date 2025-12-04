using DbMetaTool.Services;
using FirebirdSql.Data.FirebirdClient;
using System.Text;

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
            UpdateDatabase($@"
                User=SYSDBA;Password=zaq1@WSX;
                Database=C:\Users\admin\Desktop\firebirdDatabases\file.fdb;
                DataSource=127.0.0.1;
                Port=3050;
                Dialect=3;
                Charset=UTF8;", "C:\\Users\\admin\\Desktop\\firebirdDatabases\\updates");
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
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var reportBuilder = new StringBuilder();
            reportBuilder.AppendLine($"--- RAPORT TWORZENIA BAZY DANYCH: {DateTime.Now} ---");

            // --- Logika nazwy pliku ---

            if (!string.IsNullOrWhiteSpace(databaseDirectory))
            {
                Console.WriteLine($"[ABORT] Inncorect database path");
                return;
            }

            bool hasFdbExtension = string.Equals(Path.GetExtension(databaseDirectory), ".fdb", StringComparison.OrdinalIgnoreCase);
            if (!hasFdbExtension)
            {
                string generatedFileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid()}.fdb";
                databaseDirectory = Path.Combine(databaseDirectory, generatedFileName);
            }

            if (File.Exists(databaseDirectory))
            {
                Console.WriteLine($"[ABORT] Database file already exists: {databaseDirectory}");
                return;
            }

            string connectionString = $@"
                User=SYSDBA;Password=zaq1@WSX;
                Database={databaseDirectory};
                DataSource=127.0.0.1;
                Port=3050;
                Dialect=3;
                Charset=UTF8;";
        
            // --- KROK 1: TWORZENIE BAZY ---
            Console.WriteLine("Tworzenie pustej bazy danych...");
            var databaseCreationResult = DatabaseBuilderService.CreateDatabase(connectionString);

            if (databaseCreationResult.IsFailure)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[CRITICAL] Failed to create database: {databaseCreationResult.Error.Description}");
                Console.ResetColor();
                return;
            }
            reportBuilder.AppendLine($"[ OK ] Utworzono plik bazy: {databaseDirectory}");

            // --- KROK 2: WYKONYWANIE SKRYPTÓW (ze zbieraniem logów) ---
            Console.WriteLine("Wykonywanie skryptów SQL...");

            // Zmieniamy wywołanie - teraz odbieramy też listę logów
            var (scriptExecutionResult, logs) = DatabaseBuilderService.ExecuteSqlScriptsFromDirectory(connectionString, scriptsDirectory);

            // --- KROK 3: GENEROWANIE I WYŚWIETLANIE RAPORTU ---

            // Dodajemy szczegóły skryptów do raportu
            if (logs != null && logs.Any())
            {
                reportBuilder.AppendLine("--- SZCZEGÓŁY WYKONANIA SKRYPTÓW ---");
                foreach (var log in logs)
                {
                    string status = log.IsSuccess ? "[ OK ]" : "[FAIL]";
                    reportBuilder.AppendLine($"{status} {log.ScriptName} ({log.DurationMs}ms)");
                    if (!log.IsSuccess)
                    {
                        reportBuilder.AppendLine($"       BŁĄD: {log.Message}");
                    }
                }
            }

            // Obsługa błędów (Drop database)
            if (scriptExecutionResult.IsFailure)
            {
                reportBuilder.AppendLine("----------------------------------------");
                reportBuilder.AppendLine($"[ERROR] Proces przerwany. Błąd: {scriptExecutionResult.Error.Description}");

                Console.WriteLine("Wystąpił błąd. Usuwanie uszkodzonej bazy...");
                var dropResult = DatabaseBuilderService.DropDatabase(connectionString);

                if (dropResult.IsFailure)
                {
                    reportBuilder.AppendLine($"[FATAL] Nie udało się usunąć bazy po błędzie: {dropResult.Error.Description}");
                    reportBuilder.AppendLine($"        Usuń plik ręcznie: {databaseDirectory}");
                }
                else
                {
                    reportBuilder.AppendLine($"[INFO] Usunięto uszkodzony plik bazy danych.");
                }
            }
            else
            {
                reportBuilder.AppendLine("----------------------------------------");
                reportBuilder.AppendLine("[SUCCESS] Baza danych została utworzona poprawnie.");
            }

            stopwatch.Stop();
            reportBuilder.AppendLine($"Całkowity czas operacji: {stopwatch.Elapsed.TotalSeconds:F2} s");

            // Finalne wyświetlenie raportu w konsoli
            Console.WriteLine();
            Console.WriteLine(reportBuilder.ToString());
        }

        /// <summary>
        /// Generuje skrypty metadanych z istniejącej bazy danych Firebird 5.0.
        /// </summary>
        public static void ExportScripts(string connectionString, string outputDirectory)
        {
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            using var conn = new FbConnection(connectionString);

            try
            {
                conn.Open();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd połączenia z bazą danych: {ex.Message}");
                return;
            }

            MetadataExtractorService.ExportDomains(conn, outputDirectory);
            MetadataExtractorService.ExportTables(conn, outputDirectory);
            MetadataExtractorService.ExportProcedures(conn, outputDirectory);
            
            Console.WriteLine($"Eksport zakończony. Pliki zapisano w: {outputDirectory}");
        }

        /// <summary>
        /// Aktualizuje istniejącą bazę danych Firebird 5.0 na podstawie skryptów.
        /// </summary>
        public static void UpdateDatabase(string connectionString, string scriptsDirectory)
        {
            if (string.IsNullOrWhiteSpace(scriptsDirectory) || !Directory.Exists(scriptsDirectory))
            {
                Console.WriteLine("Nieprawidłowy katalog skryptów.");
                return;
            }

            var scriptFiles = Directory.GetFiles(scriptsDirectory, "*.sql");

            using var conn = new FbConnection(connectionString);
            try
            {
                conn.Open();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd połączenia z bazą danych: {ex.Message}");
                return;
            }

            var result = UpdatingDatabaseService.UpdateDatabase(conn, scriptFiles);

            if (result.IsFailure)
            {
                Console.WriteLine(result.Error.Description);
            }

            conn.Close();
        }
    }
}
