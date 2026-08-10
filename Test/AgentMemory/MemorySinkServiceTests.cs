using Moq;
using System.Text.Json;
using Wangdefa.AgentMemory;
using Wangdefa.AgentMemory.FeatureEngine;
using Wangdefa.AgentMemory.Interfaces;
using Wangdefa.AgentMemory.Knowledge;
using Wangdefa.AgentMemory.Models;
using Wangdefa.AgentMemory.Thinking;
using Wangdefa.AgentMemory.Thinking.Events;
using Wangdefa.AgentMemory.Thinking.KnowledgeExtractor;

using PreferenceEntry = Wangdefa.AgentMemory.Models.PreferenceEntry;

namespace Wangdefa.Tests.AgentMemory;

public class MemorySinkServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _recordsPath;
    private readonly string _knowledgePath;
    private readonly FeatureEngineDb _db;
    private readonly global::Wangdefa.AgentMemory.FeatureEngine.FeatureEngine _featureEngine;
    private readonly IEventStore _eventStore;
    private readonly IThinkingStore _thinkingStore;
    private readonly IKnowledgeStore _knowledgeStore;
    private readonly ILearningOrchestrator _learningOrchestrator;
    private readonly MemorySinkService _sinkService;

    public MemorySinkServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"wangdefa_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);

        _recordsPath = Path.Combine(_testDir, "cognitive", "records");
        _knowledgePath = Path.Combine(_testDir, "experience", "knowledge");
        Directory.CreateDirectory(_recordsPath);
        Directory.CreateDirectory(_knowledgePath);

        _db = new FeatureEngineDb(_testDir);
        _featureEngine = new global::Wangdefa.AgentMemory.FeatureEngine.FeatureEngine(_db);
        _eventStore = new EventStore(_testDir);
        _thinkingStore = new ThinkingStore(_testDir, _eventStore);
        _knowledgeStore = new KnowledgeStore(_testDir);

        var mockLearning = new Mock<ILearningOrchestrator>();
        _learningOrchestrator = mockLearning.Object;

        var mockSqliteTools = new Mock<ISQLiteTools>();
        _sinkService = new MemorySinkService(
            _recordsPath,
            _testDir,
            _featureEngine,
            _thinkingStore,
            _knowledgeStore,
            _eventStore,
            _learningOrchestrator,
            mockSqliteTools.Object
        );
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); }
            catch { }
        }
    }

    [Fact]
    public async Task SinkAsync_ShouldCreateCognitiveRecord()
    {
        var userInput = "帮我规划开源项目";
        var agentResponse = "好的，我来帮你规划";
        var topicId = "test_topic";
        var perception = new PerceptionModel { Scene = "工作", Emotion = "中性" };
        var summary = "用户规划开源项目";
        var overview = "用户需要完整的开源项目计划";
        var tags = new List<string> { "规划", "开源" };
        var route = "deep";

        await _sinkService.SinkAsync(
            userInput,
            agentResponse,
            topicId,
            perception,
            summary,
            overview,
            tags,
            route
        );

        var recordFiles = Directory.GetFiles(_recordsPath, "认知_*.json");
        recordFiles.Should().HaveCount(1);

        var json = await File.ReadAllTextAsync(recordFiles[0]);
        var record = JsonSerializer.Deserialize<CognitiveRecordModel>(json);
        record.Should().NotBeNull();
        record!.Perception.Scene.Should().Be("工作");
        record.Insight.Summary.Should().Be(summary);
        record.Insight.ContentTags.Should().Contain(tags);
    }

    [Fact]
    public async Task SinkAsync_ShouldCreateEventStore()
    {
        var userInput = "帮我规划开源项目";
        var agentResponse = "好的，我来帮你规划";
        var topicId = "test_topic";
        var perception = new PerceptionModel { Scene = "工作" };
        var summary = "用户规划开源项目";
        var overview = "用户需要完整的开源项目计划";
        var tags = new List<string> { "规划", "开源" };
        var route = "deep";

        await _sinkService.SinkAsync(
            userInput,
            agentResponse,
            topicId,
            perception,
            summary,
            overview,
            tags,
            route
        );

        var today = DateTime.Now.ToString("yyyy-MM-dd");
        var eventPath = Path.Combine(_testDir, "experience", "events", today);
        Directory.Exists(eventPath).Should().BeTrue();

        var eventFiles = Directory.GetFiles(eventPath, "事件_*.json");
        eventFiles.Should().HaveCount(1);

        var json = await File.ReadAllTextAsync(eventFiles[0]);
        var evt = JsonSerializer.Deserialize<EventModel>(json);
        evt.Should().NotBeNull();
        evt!.Data.UserInput.Should().Be(userInput);
        evt.Data.AgentResponse.Should().Be(agentResponse);
        evt.FeatureTags.Should().Contain(tags);
    }

    [Fact]
    public async Task SinkAsync_ShouldCreateTagCard()
    {
        var userInput = "帮我规划开源项目";
        var agentResponse = "好的，我来帮你规划";
        var topicId = "test_topic";
        var perception = new PerceptionModel { Scene = "工作" };
        var summary = "用户规划开源项目";
        var overview = "用户需要完整的开源项目计划";
        var tags = new List<string> { "开源计划", "开源准备" };
        var route = "deep";

        await _sinkService.SinkAsync(
            userInput,
            agentResponse,
            topicId,
            perception,
            summary,
            overview,
            tags,
            route
        );

        var code1 = _featureEngine.Tags.GetCode("开源计划");
        code1.Should().NotBeNull();

        var code2 = _featureEngine.Tags.GetCode("开源准备");
        code2.Should().NotBeNull();

        var cards1 = _featureEngine.Passwords.GetCards(code1!);
        cards1.Should().NotBeEmpty();

        var cards2 = _featureEngine.Passwords.GetCards(code2!);
        cards2.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SinkAsync_WithPreferences_ShouldStorePreferences()
    {
        var userInput = "帮我规划开源项目";
        var agentResponse = "好的，我来帮你规划";
        var topicId = "test_topic";
        var perception = new PerceptionModel { Scene = "工作" };
        var summary = "用户规划开源项目";
        var overview = "用户需要完整的开源项目计划";
        var tags = new List<string> { "规划", "开源" };
        var route = "deep";
        var preferences = new List<PreferenceEntry>
        {
            new PreferenceEntry { Key = "风格", Value = "简洁", Confidence = 0.8 },
            new PreferenceEntry { Key = "深度", Value = "详细", Confidence = 0.6 }
        };

        await _sinkService.SinkAsync(
            userInput,
            agentResponse,
            topicId,
            perception,
            summary,
            overview,
            tags,
            route,
            preferences: preferences
        );

        var recordFiles = Directory.GetFiles(_recordsPath, "认知_*.json");
        var json = await File.ReadAllTextAsync(recordFiles[0]);
        var record = JsonSerializer.Deserialize<CognitiveRecordModel>(json);
        record.Should().NotBeNull();
        record!.Insight.Preferences.Should().HaveCount(2);
        record.Insight.Preferences.Should().Contain(p => p.Key == "风格" && p.Value == "简洁");
        record.Insight.Preferences.Should().Contain(p => p.Key == "深度" && p.Value == "详细");
    }

    [Fact]
    public async Task SinkAsync_WithSourcePath_ShouldStoreSourcePath()
    {
        var userInput = "文件索引测试";
        var agentResponse = "文件已索引";
        var topicId = "test_topic";
        var perception = new PerceptionModel { Scene = "工作" };
        var summary = "文件索引";
        var overview = "文件内容概览";
        var tags = new List<string> { "文件", "索引" };
        var route = "shallow";
        var sourcePath = "/path/to/test/file.txt";
        var sourceType = "file";

        await _sinkService.SinkAsync(
            userInput,
            agentResponse,
            topicId,
            perception,
            summary,
            overview,
            tags,
            route,
            sourcePath: sourcePath,
            sourceType: sourceType
        );

        var recordFiles = Directory.GetFiles(_recordsPath, "认知_*.json");
        var json = await File.ReadAllTextAsync(recordFiles[0]);
        var record = JsonSerializer.Deserialize<CognitiveRecordModel>(json);
        record.Should().NotBeNull();
        record!.SourcePath.Should().Be(sourcePath);

        var thinkingPath = Path.Combine(_testDir, "thinking", "chat", topicId);
        Directory.Exists(thinkingPath).Should().BeTrue();

        var indexFiles = Directory.GetFiles(thinkingPath, "记录_*.json");
        indexFiles.Should().HaveCount(1);

        var indexJson = await File.ReadAllTextAsync(indexFiles[0]);
        var index = JsonSerializer.Deserialize<DiversionIndexModel>(indexJson);
        index.Should().NotBeNull();
        index!.FullTextPointer.Should().Be(sourcePath);
        index.FullTextType.Should().Be("file");
    }
}