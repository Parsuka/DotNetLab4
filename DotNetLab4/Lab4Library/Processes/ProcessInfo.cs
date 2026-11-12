using System;
using System.Diagnostics;

namespace Lab4Library.Processes
{
    public class ProcessInfo
    {
        public ProcessInfo(int id, string name, long memoryBytes, DateTime? startTime,
                           ProcessPriorityClass? priority, int threadCount)
        {
            Id = id;
            Name = name;
            MemoryBytes = memoryBytes;
            StartTime = startTime;
            Priority = priority;
            ThreadCount = threadCount;
        }

        public int Id { get; private set; }

        public string Name { get; private set; }

        public long MemoryBytes { get; private set; }

        public double MemoryMegabytes
        {
            get { return MemoryBytes / 1024.0 / 1024.0; }
        }

        public DateTime? StartTime { get; private set; }

        public ProcessPriorityClass? Priority { get; private set; }

        public string PriorityName
        {
            get { return Priority.HasValue ? ProcessManager.GetPriorityName(Priority.Value) : "—"; }
        }

        public int ThreadCount { get; private set; }

        public override string ToString()
        {
            return string.Format("{0} (PID {1}), {2:F1} МБ, потоків: {3}", Name, Id, MemoryMegabytes, ThreadCount);
        }
    }
}
