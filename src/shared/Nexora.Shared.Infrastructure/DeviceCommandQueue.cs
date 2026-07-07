using System.Collections.Concurrent;

namespace Nexora.Shared.Infrastructure
{
    public static class DeviceCommandQueue
    {
        public static readonly ConcurrentDictionary<string, string> PendingCommands = 
            new ConcurrentDictionary<string, string>();

        public static readonly ConcurrentDictionary<string, string> ValveStates = 
            new ConcurrentDictionary<string, string>();
    }
}
