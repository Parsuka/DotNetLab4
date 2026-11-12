namespace Lab4Library.Processes
{
    public class ProgramDefinition
    {
        public ProgramDefinition(string displayName, string fileName, string arguments = "")
        {
            DisplayName = displayName;
            FileName = fileName;
            Arguments = arguments ?? string.Empty;
        }

        public string DisplayName { get; private set; }

        public string FileName { get; private set; }

        public string Arguments { get; private set; }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
