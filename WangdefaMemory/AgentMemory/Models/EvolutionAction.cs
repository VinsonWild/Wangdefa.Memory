namespace Wangdefa.AgentMemory.Models;

/// <summary>
/// 标签演化操作 — 由 C 线判断，在记忆写入后执行
/// </summary>
public class EvolutionAction
{
    public string Action { get; set; } = "";
    public string Code { get; set; } = "";
    public string? TargetCode { get; set; }
    public string? Reason { get; set; }
}