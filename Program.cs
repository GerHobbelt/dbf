using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CommandLine;
using DbfDataReader;

namespace Dbf
{
    public class Program
    {
        public static int Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            return Parser.Default.ParseArguments<Options>(args)
                .MapResult(RunAndReturnExitCode, _ => 1);
        }

        public static int RunAndReturnExitCode(Options options)
        {
            if (options.Csv)
                PrintCsv(options);
            else if (options.Schema)
                PrintSchema(options);
            else
                PrintSummaryInfo(options);

            return 0;
        }

        private static string outputFilename = null;

        private static void Write(Options options, string text)
        {
            if (String.IsNullOrWhiteSpace(options.Output) || options.Output == "-")
            {
                Console.WriteLine(text);
            }
            else
            {
                if (outputFilename == null)
                {
                    outputFilename = options.Output;

                    File.WriteAllText(outputFilename, text + "\n", Encoding.UTF8);
                }
                else
                {
                    File.AppendAllText(outputFilename, text + "\n", Encoding.UTF8);
                }
            }
        }

        private static void WriteAllLines(Options options, string[] lines)
        {
            if (String.IsNullOrWhiteSpace(options.Output) || options.Output == "-")
            {
                foreach (string l in lines)
                {
                    Console.WriteLine(l);
                }
            }
            else
            {
                if (outputFilename == null)
                {
                    outputFilename = options.Output;

                    File.WriteAllLines(outputFilename, lines, Encoding.UTF8);
                }
                else
                {
                    File.AppendAllLines(outputFilename, lines, Encoding.UTF8);
                }
            }
        }

        private static void SetOutputFileTimestampToInputFile(string inputFilepath)
        {
            if (outputFilename != null)
            {
                var ct = File.GetCreationTime(inputFilepath);
                var lwt = File.GetLastWriteTime(inputFilepath);
                var lat = File.GetLastAccessTime(inputFilepath);

                File.SetCreationTime(outputFilename, ct);
                File.SetLastWriteTime(outputFilename, lwt);
                File.SetLastAccessTime(outputFilename, lat);
            }
        }

        private static void PrintSummaryInfo(Options options)
        {
            var encoding = GetEncoding();
            using (var dbfTable = new DbfTable(options.Filename, encoding))
            {
                var header = dbfTable.Header;

                WriteAllLines(options, new[] {
                    $"Filename: {options.Filename}",
                    $"Type: {header.VersionDescription}",
                    $"Memo File: {dbfTable.Memo != null}",
                    $"Records: {header.RecordCount}",
                    "",
                    "Fields:",
                    "Name                                             Type       Length     Decimal",
                    "------------------------------------------------------------------------------"
                });

                foreach (var dbfColumn in dbfTable.Columns)
                {
                    var name = dbfColumn.Name;
                    var columnType = ((char) dbfColumn.ColumnType).ToString();
                    var length = dbfColumn.Length.ToString();
                    var decimalCount = dbfColumn.DecimalCount;
                    Write(options,
                        $"{name.PadRight(71 - 22)} {columnType.PadRight(10)} {length.PadRight(10)} {decimalCount}");
                }
            }

            SetOutputFileTimestampToInputFile(options.Filename);
        }

        private static void PrintCsv(Options options)
        {
            List<string> lines = new List<string>();
            var encoding = GetEncoding();
            using (var dbfTable = new DbfTable(options.Filename, encoding))
            {
                var columnNames = string.Join(",", dbfTable.Columns.Select(c => c.Name));
                if (!options.SkipDeleted) columnNames += ",Deleted";

                lines.Add(columnNames);

                var dbfRecord = new DbfRecord(dbfTable);

                while (dbfTable.Read(dbfRecord))
                {
                    if (options.SkipDeleted && dbfRecord.IsDeleted) continue;

                    var values = string.Join(",", dbfRecord.Values.Select(v => EscapeValue(v)));
                    if (!options.SkipDeleted) values += $",{dbfRecord.IsDeleted}";

                    lines.Add(values);
                }
            }

            WriteAllLines(options, lines.ToArray());

            SetOutputFileTimestampToInputFile(options.Filename);
        }

        private static void PrintSchema(Options options)
        {
            var encoding = GetEncoding();
            using (var dbfTable = new DbfTable(options.Filename, encoding))
            {
                var tableName = Path.GetFileNameWithoutExtension(options.Filename);
                Write(options, $"CREATE TABLE [dbo].[{tableName}]");
                Write(options, "(");

                foreach (var dbfColumn in dbfTable.Columns)
                {
                    var columnSchema = ColumnSchema(dbfColumn);
                    var line = $"  {columnSchema}";

                    if (dbfColumn.Index < dbfTable.Columns.Count ||
                        !options.SkipDeleted)
                        line += ",";
                    Write(options, line);
                }

                if (!options.SkipDeleted) Write(options, "  [deleted] [bit] NULL DEFAULT ((0))");

                Write(options, ")");
            }

            SetOutputFileTimestampToInputFile(options.Filename);
        }

        private static Encoding GetEncoding()
        {
            return Encoding.GetEncoding(1252);
        }

        private static string ColumnSchema(DbfColumn dbfColumn)
        {
            var schema = string.Empty;
            switch (dbfColumn.ColumnType)
            {
                case DbfColumnType.Boolean:
                    schema = $"[{dbfColumn.Name}] [bit] NULL DEFAULT ((0))";
                    break;
                case DbfColumnType.Character:
                    schema = $"[{dbfColumn.Name}] [nvarchar]({dbfColumn.Length})  NULL";
                    break;
                case DbfColumnType.Currency:
                    schema =
                        $"[{dbfColumn.Name}] [decimal]({dbfColumn.Length + dbfColumn.DecimalCount},{dbfColumn.DecimalCount}) NULL DEFAULT (NULL)";
                    break;
                case DbfColumnType.Date:
                    schema = $"[{dbfColumn.Name}] [date] NULL DEFAULT (NULL)";
                    break;
                case DbfColumnType.DateTime:
                    schema = $"[{dbfColumn.Name}] [datetime] NULL DEFAULT (NULL)";
                    break;
                case DbfColumnType.Double:
                    schema =
                        $"[{dbfColumn.Name}] [decimal]({dbfColumn.Length + dbfColumn.DecimalCount},{dbfColumn.DecimalCount}) NULL DEFAULT (NULL)";
                    break;
                case DbfColumnType.Float:
                    schema =
                        $"[{dbfColumn.Name}] [decimal]({dbfColumn.Length + dbfColumn.DecimalCount},{dbfColumn.DecimalCount}) NULL DEFAULT (NULL)";
                    break;
                case DbfColumnType.General:
                    schema = $"[{dbfColumn.Name}] [nvarchar]({dbfColumn.Length})  NULL";
                    break;
                case DbfColumnType.Memo:
                    schema = $"[{dbfColumn.Name}] [ntext]  NULL";
                    break;
                case DbfColumnType.Number:
                    if (dbfColumn.DecimalCount > 0)
                        schema =
                            $"[{dbfColumn.Name}] [decimal]({dbfColumn.Length + dbfColumn.DecimalCount},{dbfColumn.DecimalCount}) NULL DEFAULT (NULL)";
                    else
                        schema = $"[{dbfColumn.Name}] [int] NULL DEFAULT (NULL)";
                    break;
                case DbfColumnType.SignedLong:
                    schema = $"[{dbfColumn.Name}] [int] NULL DEFAULT (NULL)";
                    break;
            }

            return schema;
        }

        private static string EscapeValue(IDbfValue dbfValue)
        {
            var value = dbfValue.ToString();
            if (dbfValue is DbfValueString)
                if (value.Contains(","))
                    value = $"\"{value}\"";

            return value;
        }
    }
}