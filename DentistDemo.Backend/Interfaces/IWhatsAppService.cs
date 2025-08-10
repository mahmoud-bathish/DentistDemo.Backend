namespace DentistDemo.Backend.Interfaces
{
    public interface IWhatsAppService
    {
        Task<bool> SendMessageAsync(string to, string message);
        Task<bool> SendMessageAsync(string to, string message, string messageId);
    }
}
