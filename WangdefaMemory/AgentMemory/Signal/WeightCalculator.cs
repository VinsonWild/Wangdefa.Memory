namespace Wangdefa.AgentMemory.Signal;

/// <summary>
/// 权重计算器 - 四段式记忆衰减
/// </summary>
public static class WeightCalculator
{
    /// <summary>
    /// 计算权重（0.20-1.0）
    /// </summary>
    public static double Calculate(DateTime createdAt, DateTime lastAccessAt)
    {
        var age = (DateTime.Now - createdAt).TotalDays;
        var recall = (DateTime.Now - lastAccessAt).TotalDays;

        double baseWeight;

        if (age <= 20)
        {
            // 强势记忆：1.0 → 0.85
            baseWeight = 1.0 - (age / 20) * 0.15;
        }
        else if (age <= 60)
        {
            // 有印象：0.85 → 0.50
            baseWeight = 0.85 - ((age - 20) / 40) * 0.35;
        }
        else if (age <= 150)
        {
            // 逐渐衰减：0.50 → 0.20
            baseWeight = 0.50 - ((age - 60) / 90) * 0.30;
        }
        else
        {
            // 需要被唤醒：保底0.20
            baseWeight = 0.20;
        }

        // 访问加权：最近7天内被访问过，轻微上修
        if (recall <= 7)
        {
            var boost = 1.0 + (1 - recall / 7) * 0.15;
            baseWeight = Math.Min(1.0, baseWeight * boost);
        }

        return Math.Max(0.20, baseWeight);
    }
}