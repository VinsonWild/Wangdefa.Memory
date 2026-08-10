namespace Wangdefa.Contracts;

public interface IChatService
{
    Task<string> ChatAsync(string prompt);
    void SetThink(bool enabled);
    bool IsDeepSeekThinkingMode();
}