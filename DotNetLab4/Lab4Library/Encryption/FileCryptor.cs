using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Lab4Library.Encryption
{
    public class FileCryptor
    {
        public const string EncryptedExtension = ".enc";

        private static readonly byte[] Signature = Encoding.ASCII.GetBytes("LAB4ENC1");
        private const int SaltSize = 16;
        private const int IvSize = 16;
        private const int KeySize = 32;
        private const int CheckSize = 32;
        private const int Iterations = 100000;
        private const int HeaderSize = 8 + SaltSize + IvSize + CheckSize;

        public FileCryptor() : this(1024 * 1024)
        {
        }

        public FileCryptor(int bufferSize)
        {
            if (bufferSize < 1024) throw new ArgumentOutOfRangeException(nameof(bufferSize));
            BufferSize = bufferSize;
        }

        public int BufferSize { get; private set; }

        public CryptoOperationResult Encrypt(string inputPath, string outputPath, string key,
                                             IProgress<int> progress = null,
                                             CancellationToken cancellationToken = default(CancellationToken))
        {
            return Run(CryptoOperation.Encrypt, inputPath, outputPath, key, progress, cancellationToken);
        }

        public CryptoOperationResult Decrypt(string inputPath, string outputPath, string key,
                                             IProgress<int> progress = null,
                                             CancellationToken cancellationToken = default(CancellationToken))
        {
            return Run(CryptoOperation.Decrypt, inputPath, outputPath, key, progress, cancellationToken);
        }

        public static string SuggestOutputPath(string inputPath, CryptoOperation operation)
        {
            if (operation == CryptoOperation.Encrypt)
                return inputPath + EncryptedExtension;

            if (inputPath.EndsWith(EncryptedExtension, StringComparison.OrdinalIgnoreCase))
                return inputPath.Substring(0, inputPath.Length - EncryptedExtension.Length);

            string directory = Path.GetDirectoryName(inputPath) ?? string.Empty;
            return Path.Combine(directory, Path.GetFileNameWithoutExtension(inputPath) + "_decrypted" + Path.GetExtension(inputPath));
        }

        public static bool IsEncryptedFile(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var buffer = new byte[Signature.Length];
                    return ReadExactly(stream, buffer) && BytesEqual(buffer, Signature);
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private CryptoOperationResult Run(CryptoOperation operation, string inputPath, string outputPath, string key,
                                          IProgress<int> progress, CancellationToken cancellationToken)
        {
            ValidateArguments(inputPath, outputPath, key);

            DateTime startedAt = DateTime.Now;
            Stopwatch stopwatch = Stopwatch.StartNew();
            string tempPath = outputPath + ".tmp";
            long inputSize;

            try
            {
                using (var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize))
                using (var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize))
                {
                    inputSize = input.Length;
                    if (operation == CryptoOperation.Encrypt)
                        EncryptStream(input, output, key, progress, cancellationToken);
                    else
                        DecryptStream(input, output, key, progress, cancellationToken);
                }

                if (File.Exists(outputPath))
                    File.Delete(outputPath);
                File.Move(tempPath, outputPath);
            }
            catch
            {
                TryDelete(tempPath);
                throw;
            }

            stopwatch.Stop();
            if (progress != null) progress.Report(100);

            long outputSize = new FileInfo(outputPath).Length;
            return new CryptoOperationResult(operation, inputPath, outputPath, inputSize, outputSize, startedAt, stopwatch.Elapsed);
        }

        private void EncryptStream(Stream input, Stream output, string key, IProgress<int> progress, CancellationToken token)
        {
            byte[] salt = RandomBytes(SaltSize);
            byte[] iv = RandomBytes(IvSize);
            byte[] aesKey, check;
            DeriveKey(key, salt, out aesKey, out check);

            output.Write(Signature, 0, Signature.Length);
            output.Write(salt, 0, salt.Length);
            output.Write(iv, 0, iv.Length);
            output.Write(check, 0, check.Length);

            using (Aes aes = CreateAes(aesKey, iv))
            using (ICryptoTransform transform = aes.CreateEncryptor())
            using (var crypto = new CryptoStream(new NonClosingStream(output), transform, CryptoStreamMode.Write))
            {
                CopyWithProgress(input, crypto, input.Length, progress, token);
                crypto.FlushFinalBlock();
            }
        }

        private void DecryptStream(Stream input, Stream output, string key, IProgress<int> progress, CancellationToken token)
        {
            if (input.Length < HeaderSize)
                throw new InvalidFileFormatException("Файл не є зашифрованим цією програмою або пошкоджений.");

            var signature = new byte[Signature.Length];
            var salt = new byte[SaltSize];
            var iv = new byte[IvSize];
            var storedCheck = new byte[CheckSize];
            ReadExactly(input, signature);
            if (!BytesEqual(signature, Signature))
                throw new InvalidFileFormatException("Файл не є зашифрованим цією програмою.");
            ReadExactly(input, salt);
            ReadExactly(input, iv);
            ReadExactly(input, storedCheck);

            byte[] aesKey, check;
            DeriveKey(key, salt, out aesKey, out check);
            if (!BytesEqual(check, storedCheck))
                throw new WrongKeyException();

            try
            {
                using (Aes aes = CreateAes(aesKey, iv))
                using (ICryptoTransform transform = aes.CreateDecryptor())
                using (var crypto = new CryptoStream(new NonClosingStream(output), transform, CryptoStreamMode.Write))
                {
                    CopyWithProgress(input, crypto, input.Length, progress, token);
                    crypto.FlushFinalBlock();
                }
            }
            catch (CryptographicException ex)
            {
                if (token.IsCancellationRequested)
                    throw new OperationCanceledException(token);
                throw new CryptoFileException("Зашифрований файл пошкоджено, розшифрувати його неможливо.", ex);
            }
        }

        private void CopyWithProgress(Stream source, Stream destination, long totalLength,
                                      IProgress<int> progress, CancellationToken token)
        {
            var buffer = new byte[BufferSize];
            long startPosition = source.Position;
            long total = totalLength - startPosition;
            long processed = 0;
            int lastPercent = -1;
            int read;

            ReportPercent(progress, 0, ref lastPercent);
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                token.ThrowIfCancellationRequested();
                destination.Write(buffer, 0, read);
                processed += read;
                int percent = total == 0 ? 100 : (int)(processed * 100 / total);
                ReportPercent(progress, percent, ref lastPercent);
            }
        }

        private static void ReportPercent(IProgress<int> progress, int percent, ref int lastPercent)
        {
            if (progress == null || percent == lastPercent) return;
            lastPercent = percent;
            progress.Report(percent);
        }

        private static void ValidateArguments(string inputPath, string outputPath, string key)
        {
            if (string.IsNullOrWhiteSpace(inputPath))
                throw new ArgumentException("Не вибрано файл.", nameof(inputPath));
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Не вказано файл для збереження результату.", nameof(outputPath));
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Ключ шифрування не може бути порожнім.", nameof(key));
            if (!File.Exists(inputPath))
                throw new FileNotFoundException("Файл не знайдено: " + inputPath, inputPath);
            if (string.Equals(Path.GetFullPath(inputPath), Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Файл результату має відрізнятися від вихідного файлу.", nameof(outputPath));
        }

        private static void DeriveKey(string key, byte[] salt, out byte[] aesKey, out byte[] check)
        {
            using (var kdf = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(key), salt, Iterations, HashAlgorithmName.SHA256))
            {
                aesKey = kdf.GetBytes(KeySize);
                check = kdf.GetBytes(CheckSize);
            }
        }

        private static Aes CreateAes(byte[] key, byte[] iv)
        {
            Aes aes = Aes.Create();
            aes.KeySize = KeySize * 8;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key;
            aes.IV = iv;
            return aes;
        }

        private static byte[] RandomBytes(int count)
        {
            var bytes = new byte[count];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return bytes;
        }

        private static bool ReadExactly(Stream stream, byte[] buffer)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int read = stream.Read(buffer, offset, buffer.Length - offset);
                if (read == 0) return false;
                offset += read;
            }
            return true;
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private sealed class NonClosingStream : Stream
        {
            private readonly Stream inner;

            public NonClosingStream(Stream inner)
            {
                this.inner = inner;
            }

            public override bool CanRead { get { return false; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return true; } }
            public override long Length { get { throw new NotSupportedException(); } }

            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                inner.Write(buffer, offset, count);
            }

            public override void Flush()
            {
                inner.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }
        }
    }
}
