using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualBasic.FileIO;

namespace CsvHash.WinForms
{
    internal sealed class EncryptionOptions
    {
        public EncryptionOptions(string algorithm, string encodingName)
        {
            Algorithm = string.IsNullOrWhiteSpace(algorithm) ? "SHA256" : algorithm.Trim();
            EncodingName = string.IsNullOrWhiteSpace(encodingName) ? "UTF-8" : encodingName.Trim();
        }

        public string Algorithm { get; private set; }

        public string EncodingName { get; private set; }

        public Encoding TextEncoding
        {
            get { return CsvProcessor.GetEncoding(EncodingName); }
        }

        public void Validate()
        {
            CsvProcessor.GetEncoding(EncodingName);

            if (!CsvProcessor.IsSupportedAlgorithm(Algorithm))
            {
                throw new InvalidOperationException("不支持的加密算法：" + Algorithm);
            }
        }
    }

    internal static class CsvProcessor
    {
        public static string[] ReadHeaders(string csvPath, string delimiter, Encoding encoding)
        {
            ValidateDelimiter(delimiter);

            using (var parser = CreateParser(csvPath, delimiter, encoding))
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

        public static void EncryptFile(string csvPath, string outputPath, IEnumerable<string> fields, string delimiter, EncryptionOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            options.Validate();
            ValidateDelimiter(delimiter);
            ValidateOutputPath(outputPath);

            var selectedFields = new HashSet<string>(
                fields.Where(field => !string.IsNullOrWhiteSpace(field)).Select(field => field.Trim()),
                StringComparer.Ordinal);

            if (selectedFields.Count == 0)
            {
                throw new InvalidOperationException("请选择至少一个要加密的字段。");
            }

            var encoding = options.TextEncoding;

            using (var parser = CreateParser(csvPath, delimiter, encoding))
            using (var writer = new StreamWriter(outputPath, false, encoding))
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
                            row[i] = TransformValue(row[i] ?? string.Empty, options);
                        }
                    }

                    WriteRow(writer, row, delimiter[0]);
                }
            }
        }

        public static bool IsSupportedAlgorithm(string algorithm)
        {
            return string.Equals(algorithm, "MD5", StringComparison.OrdinalIgnoreCase)
                || string.Equals(algorithm, "SHA256", StringComparison.OrdinalIgnoreCase)
                || string.Equals(algorithm, "SHA512", StringComparison.OrdinalIgnoreCase)
                || string.Equals(algorithm, "SM3", StringComparison.OrdinalIgnoreCase);
        }

        public static Encoding GetEncoding(string encodingName)
        {
            if (string.Equals(encodingName, "UTF-8 BOM", StringComparison.OrdinalIgnoreCase))
            {
                return new UTF8Encoding(true);
            }

            if (string.Equals(encodingName, "UTF-16 LE", StringComparison.OrdinalIgnoreCase))
            {
                return Encoding.Unicode;
            }

            if (string.Equals(encodingName, "UTF-16 BE", StringComparison.OrdinalIgnoreCase))
            {
                return Encoding.BigEndianUnicode;
            }

            if (string.Equals(encodingName, "GBK", StringComparison.OrdinalIgnoreCase))
            {
                return Encoding.GetEncoding(936);
            }

            if (string.Equals(encodingName, "UTF-8", StringComparison.OrdinalIgnoreCase))
            {
                return new UTF8Encoding(false);
            }

            throw new InvalidOperationException("不支持的字符编码：" + encodingName);
        }

        private static TextFieldParser CreateParser(string csvPath, string delimiter, Encoding encoding)
        {
            var parser = new TextFieldParser(csvPath, encoding)
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

        private static string TransformValue(string value, EncryptionOptions options)
        {
            var bytes = options.TextEncoding.GetBytes(value);
            var algorithm = options.Algorithm;

            if (string.Equals(algorithm, "MD5", StringComparison.OrdinalIgnoreCase))
            {
                using (var md5 = MD5.Create())
                {
                    return ToHex(md5.ComputeHash(bytes));
                }
            }

            if (string.Equals(algorithm, "SHA256", StringComparison.OrdinalIgnoreCase))
            {
                using (var sha256 = SHA256.Create())
                {
                    return ToHex(sha256.ComputeHash(bytes));
                }
            }

            if (string.Equals(algorithm, "SHA512", StringComparison.OrdinalIgnoreCase))
            {
                using (var sha512 = SHA512.Create())
                {
                    return ToHex(sha512.ComputeHash(bytes));
                }
            }

            if (string.Equals(algorithm, "SM3", StringComparison.OrdinalIgnoreCase))
            {
                return ToHex(Sm3Digest.ComputeHash(bytes));
            }

            throw new InvalidOperationException("不支持的加密算法：" + algorithm);
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);

            foreach (var item in bytes)
            {
                builder.Append(item.ToString("x2"));
            }

            return builder.ToString();
        }

        private static void ValidateOutputPath(string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new InvalidOperationException("请选择输出文件路径。");
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

    internal static class Sm3Digest
    {
        private static readonly uint[] InitialVector =
        {
            0x7380166f,
            0x4914b2b9,
            0x172442d7,
            0xda8a0600,
            0xa96f30bc,
            0x163138aa,
            0xe38dee4d,
            0xb0fb0e4e
        };

        public static byte[] ComputeHash(byte[] input)
        {
            var padded = Pad(input);
            var state = new uint[InitialVector.Length];
            Array.Copy(InitialVector, state, InitialVector.Length);

            for (var offset = 0; offset < padded.Length; offset += 64)
            {
                Compress(state, padded, offset);
            }

            var output = new byte[32];
            for (var i = 0; i < state.Length; i++)
            {
                WriteUInt32BigEndian(state[i], output, i * 4);
            }

            return output;
        }

        private static byte[] Pad(byte[] input)
        {
            var bitLength = (ulong)input.Length * 8UL;
            var paddedLength = input.Length + 1 + 8;
            var remainder = paddedLength % 64;
            if (remainder != 0)
            {
                paddedLength += 64 - remainder;
            }

            var padded = new byte[paddedLength];
            Buffer.BlockCopy(input, 0, padded, 0, input.Length);
            padded[input.Length] = 0x80;

            for (var i = 0; i < 8; i++)
            {
                padded[padded.Length - 1 - i] = (byte)(bitLength >> (8 * i));
            }

            return padded;
        }

        private static void Compress(uint[] state, byte[] block, int offset)
        {
            var w = new uint[68];
            var wPrime = new uint[64];

            for (var i = 0; i < 16; i++)
            {
                w[i] = ReadUInt32BigEndian(block, offset + i * 4);
            }

            for (var i = 16; i < 68; i++)
            {
                w[i] = P1(w[i - 16] ^ w[i - 9] ^ RotateLeft(w[i - 3], 15)) ^ RotateLeft(w[i - 13], 7) ^ w[i - 6];
            }

            for (var i = 0; i < 64; i++)
            {
                wPrime[i] = w[i] ^ w[i + 4];
            }

            var a = state[0];
            var b = state[1];
            var c = state[2];
            var d = state[3];
            var e = state[4];
            var f = state[5];
            var g = state[6];
            var h = state[7];

            for (var i = 0; i < 64; i++)
            {
                var t = i < 16 ? 0x79cc4519U : 0x7a879d8aU;
                var ss1 = RotateLeft(unchecked(RotateLeft(a, 12) + e + RotateLeft(t, i)), 7);
                var ss2 = ss1 ^ RotateLeft(a, 12);
                var tt1 = unchecked(FF(a, b, c, i) + d + ss2 + wPrime[i]);
                var tt2 = unchecked(GG(e, f, g, i) + h + ss1 + w[i]);

                d = c;
                c = RotateLeft(b, 9);
                b = a;
                a = tt1;
                h = g;
                g = RotateLeft(f, 19);
                f = e;
                e = P0(tt2);
            }

            state[0] ^= a;
            state[1] ^= b;
            state[2] ^= c;
            state[3] ^= d;
            state[4] ^= e;
            state[5] ^= f;
            state[6] ^= g;
            state[7] ^= h;
        }

        private static uint FF(uint x, uint y, uint z, int round)
        {
            return round < 16 ? x ^ y ^ z : (x & y) | (x & z) | (y & z);
        }

        private static uint GG(uint x, uint y, uint z, int round)
        {
            return round < 16 ? x ^ y ^ z : (x & y) | (~x & z);
        }

        private static uint P0(uint x)
        {
            return x ^ RotateLeft(x, 9) ^ RotateLeft(x, 17);
        }

        private static uint P1(uint x)
        {
            return x ^ RotateLeft(x, 15) ^ RotateLeft(x, 23);
        }

        private static uint RotateLeft(uint value, int bits)
        {
            bits &= 31;
            if (bits == 0)
            {
                return value;
            }

            return (value << bits) | (value >> (32 - bits));
        }

        private static uint ReadUInt32BigEndian(byte[] bytes, int offset)
        {
            return ((uint)bytes[offset] << 24)
                | ((uint)bytes[offset + 1] << 16)
                | ((uint)bytes[offset + 2] << 8)
                | bytes[offset + 3];
        }

        private static void WriteUInt32BigEndian(uint value, byte[] bytes, int offset)
        {
            bytes[offset] = (byte)(value >> 24);
            bytes[offset + 1] = (byte)(value >> 16);
            bytes[offset + 2] = (byte)(value >> 8);
            bytes[offset + 3] = (byte)value;
        }
    }
}
