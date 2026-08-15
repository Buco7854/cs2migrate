using System.Diagnostics;

namespace CS2Migrate.Core;

public interface IProcessInspector
{
    IReadOnlyList<string> GetBlockingProcesses();
}

public sealed class ProcessInspector : IProcessInspector
{
    private static readonly string[] ProcessNames = ["steam", "cs2"];

    public IReadOnlyList<string> GetBlockingProcesses()
    {
        var processes = new List<string>();
        foreach (var processName in ProcessNames)
        {
            try
            {
                var matchingProcesses = Process.GetProcessesByName(processName);
                if (matchingProcesses.Length > 0)
                {
                    processes.Add(processName == "cs2" ? "Counter-Strike 2" : "Steam");
                }

                foreach (var process in matchingProcesses)
                {
                    process.Dispose();
                }
            }
            catch (InvalidOperationException)
            {
                // A process may exit while it is being inspected.
            }
        }

        return processes;
    }
}
