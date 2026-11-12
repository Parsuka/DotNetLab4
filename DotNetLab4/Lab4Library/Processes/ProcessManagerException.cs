using System;

namespace Lab4Library.Processes
{
    public class ProcessManagerException : Exception
    {
        public ProcessManagerException(string message) : base(message)
        {
        }

        public ProcessManagerException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
