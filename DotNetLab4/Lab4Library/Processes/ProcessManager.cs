using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

namespace Lab4Library.Processes
{
    public class ProcessManager
    {
        private static readonly ProgramDefinition[] defaultPrograms =
        {
            new ProgramDefinition("Калькулятор", "calc.exe"),
            new ProgramDefinition("Microsoft Word", "winword.exe"),
            new ProgramDefinition("Блокнот", "notepad.exe"),
            new ProgramDefinition("Paint", "mspaint.exe"),
            new ProgramDefinition("Командний рядок", "cmd.exe")
        };

        private static readonly ProcessPriorityClass[] priorities =
        {
            ProcessPriorityClass.Idle,
            ProcessPriorityClass.BelowNormal,
            ProcessPriorityClass.Normal,
            ProcessPriorityClass.AboveNormal,
            ProcessPriorityClass.High,
            ProcessPriorityClass.RealTime
        };

        public static IReadOnlyList<ProgramDefinition> DefaultPrograms
        {
            get { return defaultPrograms; }
        }

        public static IReadOnlyList<ProcessPriorityClass> Priorities
        {
            get { return priorities; }
        }

        public static string GetPriorityName(ProcessPriorityClass priority)
        {
            switch (priority)
            {
                case ProcessPriorityClass.Idle: return "Низький";
                case ProcessPriorityClass.BelowNormal: return "Нижче звичайного";
                case ProcessPriorityClass.Normal: return "Звичайний";
                case ProcessPriorityClass.AboveNormal: return "Вище звичайного";
                case ProcessPriorityClass.High: return "Високий";
                case ProcessPriorityClass.RealTime: return "Реального часу";
                default: return priority.ToString();
            }
        }

        public int Start(ProgramDefinition program)
        {
            if (program == null) throw new ArgumentNullException(nameof(program));

            try
            {
                var startInfo = new ProcessStartInfo(program.FileName, program.Arguments)
                {
                    UseShellExecute = true
                };
                using (Process process = Process.Start(startInfo))
                {
                    return process != null ? process.Id : 0;
                }
            }
            catch (Win32Exception ex)
            {
                throw new ProcessManagerException(
                    "Не вдалося запустити \"" + program.DisplayName + "\". Можливо, програму не встановлено.", ex);
            }
            catch (InvalidOperationException ex)
            {
                throw new ProcessManagerException("Не вдалося запустити \"" + program.DisplayName + "\".", ex);
            }
        }

        public List<ProcessInfo> GetProcesses()
        {
            var result = new List<ProcessInfo>();
            foreach (Process process in Process.GetProcesses())
            {
                using (process)
                {
                    ProcessInfo info = TryCreateInfo(process);
                    if (info != null)
                        result.Add(info);
                }
            }
            result.Sort((a, b) =>
            {
                int byName = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                return byName != 0 ? byName : a.Id.CompareTo(b.Id);
            });
            return result;
        }

        public ProcessInfo GetProcess(int id)
        {
            using (Process process = FindProcess(id))
            {
                ProcessInfo info = TryCreateInfo(process);
                if (info == null)
                    throw new ProcessManagerException("Процес з PID " + id + " уже завершився.");
                return info;
            }
        }

        public void Kill(int id)
        {
            using (Process process = FindProcess(id))
            {
                string name = SafeName(process);
                try
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
                catch (Win32Exception ex)
                {
                    throw new ProcessManagerException(
                        "Недостатньо прав, щоб завершити процес \"" + name + "\" (PID " + id + ").", ex);
                }
                catch (InvalidOperationException ex)
                {
                    throw new ProcessManagerException("Процес \"" + name + "\" (PID " + id + ") уже завершився.", ex);
                }
                catch (NotSupportedException ex)
                {
                    throw new ProcessManagerException("Процес \"" + name + "\" не можна завершити.", ex);
                }
            }
        }

        public void SetPriority(int id, ProcessPriorityClass priority)
        {
            using (Process process = FindProcess(id))
            {
                string name = SafeName(process);
                try
                {
                    process.PriorityClass = priority;
                }
                catch (Win32Exception ex)
                {
                    throw new ProcessManagerException(
                        "Недостатньо прав, щоб змінити пріоритет процесу \"" + name + "\" (PID " + id + ").", ex);
                }
                catch (InvalidOperationException ex)
                {
                    throw new ProcessManagerException("Процес \"" + name + "\" (PID " + id + ") уже завершився.", ex);
                }
            }
        }

        private static Process FindProcess(int id)
        {
            try
            {
                return Process.GetProcessById(id);
            }
            catch (ArgumentException ex)
            {
                throw new ProcessManagerException("Процес з PID " + id + " не знайдено. Можливо, він уже завершився.", ex);
            }
        }

        private static ProcessInfo TryCreateInfo(Process process)
        {
            try
            {
                if (process.HasExited)
                    return null;
            }
            catch (Win32Exception)
            {
            }
            catch (InvalidOperationException)
            {
                return null;
            }

            try
            {
                return new ProcessInfo(
                    process.Id,
                    process.ProcessName,
                    SafeGet(() => process.WorkingSet64, 0L),
                    SafeGet<DateTime?>(() => process.StartTime, null),
                    SafeGet<ProcessPriorityClass?>(() => process.PriorityClass, null),
                    SafeGet(() => process.Threads.Count, 0));
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static string SafeName(Process process)
        {
            return SafeGet(() => process.ProcessName, "?");
        }

        private static T SafeGet<T>(Func<T> getter, T fallback)
        {
            try
            {
                return getter();
            }
            catch (Win32Exception)
            {
                return fallback;
            }
            catch (InvalidOperationException)
            {
                return fallback;
            }
            catch (NotSupportedException)
            {
                return fallback;
            }
        }
    }
}
