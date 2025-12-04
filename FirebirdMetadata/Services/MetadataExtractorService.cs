using FirebirdSql.Data.FirebirdClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbMetaTool.Services
{
    public static class MetadataExtractorService
    {
        public static void ExportDomains(FbConnection conn, string outputDir)
        {
            // Zapytanie pobiera domeny użytkownika (nie systemowe)
            string sql = @"
            SELECT F.RDB$FIELD_NAME, F.RDB$FIELD_TYPE, F.RDB$FIELD_LENGTH, F.RDB$FIELD_SCALE, F.RDB$NULL_FLAG
            FROM RDB$FIELDS F
            WHERE F.RDB$SYSTEM_FLAG = 0 
            AND F.RDB$FIELD_NAME NOT LIKE 'RDB$%'";

            var sb = new StringBuilder();

            using (var cmd = new FbCommand(sql, conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string name = reader["RDB$FIELD_NAME"].ToString().Trim();
                    string typeDef = DecodeFirebirdType(
                        Convert.ToInt32(reader["RDB$FIELD_TYPE"]),
                        Convert.ToInt32(reader["RDB$FIELD_LENGTH"]),
                        Convert.ToInt32(reader["RDB$FIELD_SCALE"])
                    );

                    string notNull = reader["RDB$NULL_FLAG"] != DBNull.Value && Convert.ToInt32(reader["RDB$NULL_FLAG"]) == 1
                        ? "NOT NULL"
                        : "";

                    sb.AppendLine($"CREATE DOMAIN {name} AS {typeDef} {notNull};");
                }
            }

            if (sb.Length > 0)
            {
                File.WriteAllText(Path.Combine(outputDir, "01_Domains.sql"), sb.ToString());
            }
        }

        public static void ExportTables(FbConnection conn, string outputDir)
        {
            // Pobieranie listy tabel
            string tableSql = "SELECT RDB$RELATION_NAME FROM RDB$RELATIONS WHERE RDB$SYSTEM_FLAG = 0 AND RDB$VIEW_BLR IS NULL";

            using (var cmdTables = new FbCommand(tableSql, conn))
            using (var readerTables = cmdTables.ExecuteReader())
            {
                while (readerTables.Read())
                {
                    string tableName = readerTables["RDB$RELATION_NAME"].ToString().Trim();
                    var sb = new StringBuilder();

                    sb.AppendLine($"CREATE TABLE {tableName} (");

                    // Pobieranie kolumn dla danej tabeli
                    string colSql = @"
                    SELECT RF.RDB$FIELD_NAME, F.RDB$FIELD_TYPE, F.RDB$FIELD_LENGTH, F.RDB$FIELD_SCALE, RF.RDB$NULL_FLAG
                    FROM RDB$RELATION_FIELDS RF
                    JOIN RDB$FIELDS F ON RF.RDB$FIELD_SOURCE = F.RDB$FIELD_NAME
                    WHERE RF.RDB$RELATION_NAME = @Table
                    ORDER BY RF.RDB$FIELD_POSITION";

                    using (var cmdCols = new FbCommand(colSql, conn))
                    {
                        cmdCols.Parameters.AddWithValue("@Table", tableName);
                        using (var readerCols = cmdCols.ExecuteReader())
                        {
                            bool first = true;
                            while (readerCols.Read())
                            {
                                if (!first) sb.AppendLine(",");

                                string colName = readerCols["RDB$FIELD_NAME"].ToString().Trim();
                                string typeDef = DecodeFirebirdType(
                                    Convert.ToInt32(readerCols["RDB$FIELD_TYPE"]),
                                    Convert.ToInt32(readerCols["RDB$FIELD_LENGTH"]),
                                    Convert.ToInt32(readerCols["RDB$FIELD_SCALE"])
                                );

                                string notNull = readerCols["RDB$NULL_FLAG"] != DBNull.Value && Convert.ToInt32(readerCols["RDB$NULL_FLAG"]) == 1
                                    ? "NOT NULL"
                                    : "";

                                sb.Append($"    {colName} {typeDef} {notNull}");
                                first = false;
                            }
                        }
                    }
                    sb.AppendLine();
                    sb.AppendLine(");");

                    // Zapisz każdą tabelę do osobnego pliku lub zbiorczo (tutaj: osobny plik dla tabeli)
                    File.WriteAllText(Path.Combine(outputDir, $"Table_{tableName}.sql"), sb.ToString());
                }
            }
        }

        public static void ExportProcedures(FbConnection conn, string outputDir)
        {
            // W Firebird kod źródłowy procedury znajduje się w RDB$PROCEDURE_SOURCE
            // UWAGA: Pełna rekonstrukcja nagłówka (parametry IN/OUT) jest skomplikowana. 
            // Poniższy kod pobiera ciało procedury i generuje uproszczony nagłówek.

            string procSql = "SELECT RDB$PROCEDURE_NAME, RDB$PROCEDURE_SOURCE FROM RDB$PROCEDURES WHERE RDB$SYSTEM_FLAG = 0";

            using (var cmd = new FbCommand(procSql, conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string procName = reader["RDB$PROCEDURE_NAME"].ToString().Trim();
                    string source = reader["RDB$PROCEDURE_SOURCE"].ToString(); // To jest tylko treść od BEGIN do END

                    // W pełnym rozwiązaniu należy odpytać RDB$PROCEDURE_PARAMETERS, aby zbudować sekcję (INPUTS) RETURNS (OUTPUTS)
                    // Tutaj generujemy szkic:
                    var sb = new StringBuilder();
                    sb.AppendLine($"SET TERM ^ ;");
                    sb.AppendLine($"CREATE OR ALTER PROCEDURE {procName}");
                    sb.AppendLine($"/* Parametry należy uzupełnić na podstawie RDB$PROCEDURE_PARAMETERS */");
                    sb.AppendLine($"AS");
                    sb.AppendLine(source);
                    sb.AppendLine($"^");
                    sb.AppendLine($"SET TERM ; ^");

                    File.WriteAllText(Path.Combine(outputDir, $"Proc_{procName}.sql"), sb.ToString());
                }
            }
        }

        /// <summary>
        /// Mapuje wewnętrzne typy numeryczne Firebird na nazwy SQL.
        /// Jest to wersja uproszczona.
        /// </summary>
        private static string DecodeFirebirdType(int type, int length, int scale)
        {
            // Firebird używa specyficznych ID dla typów danych
            switch (type)
            {
                case 7: return "SMALLINT";
                case 8: return "INTEGER";
                case 10: return "FLOAT";
                case 12: return "DATE";
                case 13: return "TIME";
                case 14: return $"CHAR({length})";
                case 16: return (scale == 0) ? "BIGINT" : $"DECIMAL(18, {-scale})";
                case 27: return "DOUBLE PRECISION";
                case 35: return "TIMESTAMP";
                case 37: return $"VARCHAR({length})";
                case 261: return "BLOB SUB_TYPE TEXT";
                default: return "UNKNOWN_TYPE";
            }
        }
    }
}
