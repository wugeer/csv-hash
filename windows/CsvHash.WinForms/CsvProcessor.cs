using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualBasic.FileIO;

namespace CsvHash.WinForms
{
    internal static class CsvProcessor
    {
        public static string[] ReadHeaders(string csvPath, string delimiter)
        {
            ValidateDelimiter(delimiter);

            using (var parser = CreateParser(csvPath, delimiter))
            {
                if (parser.EndOfData)
                {
                    throw new InvalidOperationException("CSV 文件为空。");
                }

                var headers = parser.ReadFields();
                if (headers == null || headers.Length == 0)
                {
                    throw new InvalidOperationException("CSV 表头为空。");
                }

                return headers;
            }
        }

        public static string EncryptFile(string csvPath, IEnumerable<string> fields, string delimiter)
        {
            ValidateDelimiter(delimiter);

            var selectedFields = new HashSet<string>(
                fields.Where(field => !string.IsNullOrWhiteSpace(field)).Select(field => field.Trim()),
                StringComparer.Ordinal);

            if (selectedFields.Count == 0)
            {
                throw new InvalidOperationException("请选择至少一个要加密的字段。");
            }

            var outputPath = GetOutputPath(csvPath);

            using (var parser = CreateParser(csvPath, delimiter))
            using (var writer = new StreamWriter(outputPath, false, new UTF8Encoding(true)))
            {
                if (parser.EndOfData)
                {
                    throw new InvalidOperationException("CSV 文件为空。");
                }

                var headers = parser.ReadFields();
                if (headers == null || headers.Length == 0)
                {
                    throw new InvalidOperationException("CSV 表头为空。");
                }

                var encryptedIndexes = GetEncryptedIndexes(headers, selectedFields);
                WriteRow(writer, headers, delimiter[0]);

                while (!parser.EndOfData)
                {
                    var row = parser.ReadFields();
                    if (row == null)
                    {
                        continue;
                    }

                    for (var i = 0; i < row.Length; i++)
                    {
                        if (encryptedIndexes.Contains(i))
                        {
                            row[i] = Sha256Hex(row[i] ?? string.Empty);
                        }
                    }

                    WriteRow(writer, row, delimiter[0]);
                }
            }

            return outputPath;
        }

        private static TextFieldParser CreateParser(string csvPath, string delimiter)
        {
            var parser = new TextFieldParser(csvPath, Encoding.UTF8)
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = false
            };
            parser.SetDelimiters(delimiter);
            return parser;
        }

        private static HashSet<int> GetEncryptedIndexes(string[] headers, HashSet<string> selectedFields)
        {
            var encryptedIndexes = new HashSet<int>();
            var missingFields = new HashSet<string>(selectedFields, StringComparer.Ordinal);

            for (var i = 0; i < headers.Length; i++)
            {
                if (selectedFields.Contains(headers[i]))
                {
                    encryptedIndexes.Add(i);
                    missingFields.Remove(headers[i]);
                }
            }

            if (missingFields.Count > 0)
            {
                throw new InvalidOperationException("CSV 表头中不存在字段：" + string.Join(",", missingFields.OrderBy(field => field)));
            }

            return encryptedIndexes;
        }

        private static string GetOutputPath(string csvPath)
        {
            var directory = Path.GetDirectoryName(csvPath);
            var fileName = Path.GetFileName(csvPath);

            if (string.IsNullOrEmpty(fileName))
            {
                throw new InvalidOperationException("CSV 路径必须包含文件名。");
            }

            return Path.Combine(directory ?? string.Empty, "entry-" + fileName);
        }

        private static void WriteRow(TextWriter writer, IEnumerable<string> row, char delimiter)
        {
            writer.WriteLine(string.Join(delimiter.ToString(), row.Select(value => EscapeField(value ?? string.Empty, delimiter))));
        }

        private static string EscapeField(string value, char delimiter)
        {
            var mustQuote = value.IndexOfAny(new[] { delimiter, '"', '\r', '\n' }) >= 0;
            if (!mustQuote)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string Sha256Hex(string value)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
                var builder = new StringBuilder(bytes.Length * 2);

                foreach (var item in bytes)
                {
                    builder.Append(item.ToString("x2"));
                }

                return builder.ToString();
            }
        }

        private static void ValidateDelimiter(string delimiter)
        {
            if (string.IsNullOrEmpty(delimiter) || delimiter.Length != 1)
            {
                throw new InvalidOperationException("分隔符必须是 1 个字符。");
            }
        }
    }
}
