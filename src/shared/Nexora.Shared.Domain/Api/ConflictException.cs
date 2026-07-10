namespace Nexora.Shared.Domain.Api
{
    public class ConflictException : Exception
    {
        public ConflictException(string message) : base(message) { }
    }
}
