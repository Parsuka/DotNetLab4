using System;
using System.IO;

namespace Lab4Library.Encryption
{
    public class CryptoOperationResult
    {
        public CryptoOperationResult(CryptoOperation operation, string inputPath, string outputPath,
                                     long inputSize, long outputSize, DateTime startedAt, TimeSpan elapsed)
        {
            Operation = operation;
            InputPath = inputPath;
            OutputPath = outputPath;
            InputSize = inputSize;
            OutputSize = outputSize;
            StartedAt = startedAt;
            Elapsed = elapsed;
        }

        public CryptoOperation Operation { get; private set; }

        public string InputPath { get; private set; }

        public string OutputPath { get; private set; }

        public string OutputFileName
        {
            get { return Path.GetFileName(OutputPath); }
        }

        public long InputSize { get; private set; }

        public long OutputSize { get; private set; }

        public DateTime StartedAt { get; private set; }

        public TimeSpan Elapsed { get; private set; }
    }
}
