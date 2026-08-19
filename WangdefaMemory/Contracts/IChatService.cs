namespace Wangdefa.Contracts;

public interface IChatService
{
    Task<string> ChatAsync(string prompt);
}