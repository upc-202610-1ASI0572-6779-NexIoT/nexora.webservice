namespace Nexora.Domain.Entities
{
    public class SystemStatus
    {
        public string Message { get; private set; }
        public string Environment { get; private set; }

        public SystemStatus(string message, string environment)
        {
            Message = message;
            Environment = environment;
        }
    }
}
