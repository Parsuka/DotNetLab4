using System;

namespace Lab4Library.Encryption
{
    public class CryptoFileException : Exception
    {
        public CryptoFileException(string message) : base(message)
        {
        }

        public CryptoFileException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class WrongKeyException : CryptoFileException
    {
        public WrongKeyException() : base("Невірний ключ: файл було зашифровано іншим ключем.")
        {
        }
    }

    public class InvalidFileFormatException : CryptoFileException
    {
        public InvalidFileFormatException(string message) : base(message)
        {
        }
    }
}
