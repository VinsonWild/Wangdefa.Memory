// Copyright © 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0.

using FluentAssertions;
using Microsoft.Data.Sqlite;
using Wangdefa.AgentMemory.FeatureEngine;
using Wangdefa.AgentMemory.FeatureEngine.Models;
using Wangdefa.AgentMemory.FeatureEngine.TagServices;

namespace Wangdefa.Tests.AgentMemory;

/// <summary>
/// 验证标签状态生命周期与缓存的正确性。
///
/// 覆盖 4 个已修复缺陷：
///   缺陷 1：弃用标签在 A 线仍被命中（读侧缓存穿透）
///   缺陷 2：判重漏掉弃用标签 → 重复建 code 撞 UNIQUE 约束
///   缺陷 3：弃用理由被写进 Definition（污染语义字段）
///   缺陷 4：Activate 可复活弃用标签（写侧缓存穿透）
///
/// 缺陷 1 与 4 同根因：Deprecate 曾把 deprecated 对象留在内存缓存中。
/// 修复方式：TagCache 增加 IsCacheable 门，Add/Update 均拦截 deprecated。
/// </summary>
public class TagStatusLifecycleTests : IDisposable
{
    private readonly string _testDir;
    private readonly FeatureEngineDb _db;
    private readonly global::Wangdefa.AgentMemory.FeatureEngine.FeatureEngine _engine;
    private readonly TagDictionary _tags;
    private readonly TagEvolutionService _evolution;

    public TagStatusLifecycleTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"wangdefa_status_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);

        _db = new FeatureEngineDb(_testDir);
        _engine = new global::Wangdefa.AgentMemory.FeatureEngine.FeatureEngine(_db);
        _tags = _engine.Tags;

        // 复用 TagDictionary 内部的 TagEvolutionService 实例 ——
        // 产品中两者共用同一个 TagCache（见 TagDictionary 构造函数）。
        // 若自行 new 一个，缓存不同步会造成「库已写、读回旧值」的假象。
        _evolution = (TagEvolutionService)typeof(TagDictionary)
            .GetField("_evolution", System.Reflection.BindingFlags.NonPublic
                                  | System.Reflection.BindingFlags.Instance)!
            .GetValue(_tags)!;
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { /* 忽略清理失败 */ }
    }

    // ────────────────────────────────────────────────────────────
    // 辅助
    // ────────────────────────────────────────────────────────────

    /// <summary>直接读库，绕过缓存 —— 用于验证「落库值」与「缓存值」是否一致。</summary>
    private string ReadDbStatus(string code)
    {
        using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(_testDir, "feature_pool.db"),
            Mode = SqliteOpenMode.ReadOnly
        }.ToString());
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT status FROM tag_dictionary WHERE code = @c;";
        cmd.Parameters.AddWithValue("@c", code);
        return cmd.ExecuteScalar()?.ToString() ?? "";
    }

    private string ReadDbDefinition(string code)
    {
        using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(_testDir, "feature_pool.db"),
            Mode = SqliteOpenMode.ReadOnly
        }.ToString());
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT definition FROM tag_dictionary WHERE code = @c;";
        cmd.Parameters.AddWithValue("@c", code);
        return cmd.ExecuteScalar()?.ToString() ?? "";
    }

    private long CountRowsByTag(string tag)
    {
        using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(_testDir, "feature_pool.db"),
            Mode = SqliteOpenMode.ReadOnly
        }.ToString());
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM tag_dictionary WHERE tag = @t;";
        cmd.Parameters.AddWithValue("@t", tag);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    // ────────────────────────────────────────────────────────────
    // 缺陷 1：弃用标签在 A 线仍被命中（读侧缓存穿透）
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Deprecate_同进程内_GetCode应返回null()
    {
        // 修复前：Deprecate 把 deprecated 对象留在缓存，GetCode 先查缓存即命中
        var entry = _tags.AddWithSynonyms("缺陷1标签", "content", "定义文本", "内容", "auto");

        _evolution.Deprecate(entry.Code);

        ReadDbStatus(entry.Code).Should().Be("deprecated", "库中状态应已更新");
        _tags.GetCode("缺陷1标签").Should().BeNull("弃用标签不应被召回");
    }

    [Fact]
    public void Deprecate_同进程内_GetEntry应返回null()
    {
        var entry = _tags.AddWithSynonyms("缺陷1标签B", "content", "定义文本", "内容", "auto");

        _evolution.Deprecate(entry.Code);

        _tags.GetEntry("缺陷1标签B").Should().BeNull();
        _tags.GetEntryByCode(entry.Code).Should().BeNull();
    }

    [Fact]
    public void Deprecate_冷启动_仍应不可召回()
    {
        // 冷启动路径本就依赖 SQL 过滤（status != 'deprecated'），此测试防回归
        var entry = _tags.AddWithSynonyms("缺陷1标签C", "content", "定义文本", "内容", "auto");
        _evolution.Deprecate(entry.Code);

        var freshEngine = new global::Wangdefa.AgentMemory.FeatureEngine.FeatureEngine(
            new FeatureEngineDb(_testDir));

        freshEngine.Tags.GetCode("缺陷1标签C").Should().BeNull();
    }

    // ────────────────────────────────────────────────────────────
    // 缺陷 2：判重漏掉弃用标签 → 重复建 code 撞 UNIQUE 约束
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void AddWithSynonyms_同名弃用标签已存在_应复用原code而非新建()
    {
        // 修复前：判重走 LoadFromDb（带 status != 'deprecated' 过滤），
        // 查不到已弃用行 → 误判为不存在 → 新建 → code 撞 UNIQUE 约束
        var first = _tags.AddWithSynonyms("缺陷2标签", "content", "初次定义", "内容", "auto");
        _evolution.Deprecate(first.Code);

        var second = _tags.AddWithSynonyms("缺陷2标签", "content", "二次定义", "内容", "auto");

        second.Code.Should().Be(first.Code, "应复用已弃用标签的 code");
        CountRowsByTag("缺陷2标签").Should().Be(1, "不应重复建行");
    }

    [Fact]
    public void AddWithSynonyms_同名弃用标签已存在_不应抛出UNIQUE约束异常()
    {
        var first = _tags.AddWithSynonyms("缺陷2标签B", "content", "定义", "内容", "auto");
        _evolution.Deprecate(first.Code);

        var act = () => _tags.AddWithSynonyms("缺陷2标签B", "content", "新定义", "内容", "auto");

        act.Should().NotThrow();
    }

    [Fact]
    public void AddWithSynonyms_弃用标签被判重命中_状态不应被改回()
    {
        // 判重命中后走「已存在」分支：只合并近义词/累积释义，不改 Status
        var first = _tags.AddWithSynonyms("缺陷2标签C", "content", "定义", "内容", "auto");
        _evolution.Deprecate(first.Code);

        _tags.AddWithSynonyms("缺陷2标签C", "content", "新定义", "内容", "auto");

        ReadDbStatus(first.Code).Should().Be("deprecated", "复用 code 不应改变状态");
    }

    // ────────────────────────────────────────────────────────────
    // 缺陷 3：弃用理由被写进 Definition（污染语义字段）
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Deprecate_带reason_不应覆盖Definition()
    {
        // 修复前：Deprecate 执行 entry.Definition = reason，原释义被覆盖，
        // 且随后被 MergeDefinitions 当作释义累积，污染 Jaccard 相似度计算
        var entry = _tags.AddWithSynonyms("缺陷3标签", "content", "原始语义释义", "内容", "auto");
        var before = ReadDbDefinition(entry.Code);
        before.Should().Be("原始语义释义");

        _evolution.Deprecate(entry.Code, "泛用词，C线弃用");

        ReadDbDefinition(entry.Code).Should().Be("原始语义释义", "reason 不应写入 Definition");
        ReadDbStatus(entry.Code).Should().Be("deprecated", "状态仍应正确转换");
    }

    [Fact]
    public void Deprecate_带reason_Definition为空时也不应被填入reason()
    {
        var entry = _tags.AddWithSynonyms("缺陷3标签B", "content", "", "内容", "auto");

        _evolution.Deprecate(entry.Code, "泛用词，C线弃用");

        ReadDbDefinition(entry.Code).Should().BeEmpty("空 Definition 不应被 reason 填充");
    }

    // ────────────────────────────────────────────────────────────
    // 缺陷 4：Activate 可复活弃用标签（写侧缓存穿透）
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Activate_对已弃用标签_不应复活为active()
    {
        // 修复前：Activate 从缓存拿到那个 deprecated 对象（Deprecate 留在缓存里的），
        // 直接 entry.Status = "active" → 复活
        var entry = _tags.AddWithSynonyms("缺陷4标签", "content", "定义", "内容", "auto");
        _evolution.Deprecate(entry.Code);
        ReadDbStatus(entry.Code).Should().Be("deprecated");

        _evolution.Activate(entry.Code);

        ReadDbStatus(entry.Code).Should().Be("deprecated", "弃用标签不应被复活");
    }

    // ────────────────────────────────────────────────────────────
    // 回归：merged 重定向（不得被误伤）
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void MergeTags_同进程内_重定向应连通()
    {
        // merged 有意保留在缓存中：MergedTo 是「活路标」，靠它一跳重定向到目标
        var src = _tags.AddWithSynonyms("合并源", "content", "源释义", "内容", "auto");
        var dst = _tags.AddWithSynonyms("合并目标", "content", "目标释义", "内容", "auto");

        _evolution.MergeTags(src.Code, dst.Code);

        ReadDbStatus(src.Code).Should().Be("merged");
        _tags.GetCode("合并源").Should().Be(dst.Code, "merged 标签应被重定向到目标");
    }

    [Fact]
    public void MergeTags_冷启动_重定向应连通()
    {
        var src = _tags.AddWithSynonyms("合并源B", "content", "源释义", "内容", "auto");
        var dst = _tags.AddWithSynonyms("合并目标B", "content", "目标释义", "内容", "auto");
        _evolution.MergeTags(src.Code, dst.Code);

        var freshEngine = new global::Wangdefa.AgentMemory.FeatureEngine.FeatureEngine(
            new FeatureEngineDb(_testDir));

        freshEngine.Tags.GetCode("合并源B").Should().Be(dst.Code,
            "冷启动时 merged 重定向走 DB 查询也应连通");
    }

    [Fact]
    public void MergeTags_两跳链_重定向应连通()
    {
        var a = _tags.AddWithSynonyms("链头A", "content", "A释义", "内容", "auto");
        var b = _tags.AddWithSynonyms("链中B", "content", "B释义", "内容", "auto");
        var c = _tags.AddWithSynonyms("链尾C", "content", "C释义", "内容", "auto");

        _evolution.MergeTags(a.Code, b.Code);
        _evolution.MergeTags(b.Code, c.Code);

        _tags.GetCode("链头A").Should().Be(c.Code, "A→B→C 两跳应最终落到 C");
    }

    // ────────────────────────────────────────────────────────────
    // 回归：正常标签不受影响
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Active标签_应正常可召回()
    {
        var entry = _tags.AddWithSynonyms("在役标签", "content", "定义", "内容", "auto");
        _tags.Activate(entry.Code);

        ReadDbStatus(entry.Code).Should().Be("active");
        _tags.GetCode("在役标签").Should().Be(entry.Code);
        _tags.GetEntry("在役标签").Should().NotBeNull();
    }

    [Fact]
    public void Unexamined标签_应正常可召回()
    {
        // unexamined 是待审状态，仍需参与召回 —— IsCacheable 不应误伤
        var entry = _tags.AddWithSynonyms("待审标签", "content", "定义", "内容", "auto");

        ReadDbStatus(entry.Code).Should().Be("unexamined");
        _tags.GetCode("待审标签").Should().Be(entry.Code);
    }
}
