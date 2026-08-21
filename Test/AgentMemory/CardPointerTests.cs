using Moq;
using System.Text.Json;
using Wangdefa.AgentMemory;
using Wangdefa.AgentMemory.Cognitive;
using Wangdefa.AgentMemory.FeatureEngine;
using Wangdefa.AgentMemory.Interfaces;
using Wangdefa.AgentMemory.Knowledge;
using Wangdefa.AgentMemory.Models;
using Wangdefa.AgentMemory.Thinking;
using Wangdefa.AgentMemory.Thinking.Events;
using Wangdefa.AgentMemory.Thinking.KnowledgeExtractor;
using Wangdefa.Contracts;

using PreferenceEntry = Wangdefa.AgentMemory.Models.PreferenceEntry;

namespace Wangdefa.Tests.AgentMemory;

/// <summary>
/// 验证两阶段写入后，卡片指针（SourcePath / SummaryPointer / OverviewPointer）
/// 是否正确指向磁盘上真实存在的文件。
/// </summary>
public class CardPointerTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _recordsPath;
    private readonly string _basePath;
    private readonly FeatureEngineDb _db;
    private readonly global::Wangdefa.AgentMemory.FeatureEngine.FeatureEngine _featureEngine;
    private readonly IEventStore _eventStore;
    private readonly IThinkingStore _thinkingStore;
    private readonly IKnowledgeStore _knowledgeStore;
    private readonly ILearningOrchestrator _learningOrchestrator;
    private readonly MemorySinkService _sinkService;
    private readonly CognitiveReader _cognitiveReader;
    private readonly Mock<IChatService> _mockChatService;
    private readonly Mock<ISQLiteTools> _mockSqliteTools;

    public CardPointerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"wangdefa_pointer_test_{Guid.NewGuid()}");
        _basePath = _testDir;
        Directory.CreateDirectory(_testDir);

        _recordsPath = Path.Combine(_testDir, "cognitive", "records");
        Directory.CreateDirectory(_recordsPath);

        _db = new FeatureEngineDb(_testDir);
        _featureEngine = new global::Wangdefa.AgentMemory.FeatureEngine.FeatureEngine(_db);
        _eventStore = new EventStore(_testDir);
        _thinkingStore = new ThinkingStore(_testDir, _eventStore);
        _knowledgeStore = new KnowledgeStore(_testDir);

        var mockLearning = new Mock<ILearningOrchestrator>();
        _learningOrchestrator = mockLearning.Object;

        _mockSqliteTools = new Mock<ISQLiteTools>();
        _mockChatService = new Mock<IChatService>();
        // 让 C线摘要分析返回固定 JSON
        _mockChatService
            .Setup(s => s.ChatAsync(It.IsAny<string>()))
            .ReturnsAsync("{\"summary\": \"用户偏好简洁代码风格\", \"overview\": \"这是概览内容，用于验证指针是否指向正确的文件。\"}");

        _sinkService = new MemorySinkService(
            _recordsPath,
            _testDir,
            _featureEngine,
            _thinkingStore,
            _knowledgeStore,
            _eventStore,
            _learningOrchestrator,
            _mockSqliteTools.Object,
            _mockChatService.Object
        );

        _cognitiveReader = new CognitiveReader(
            _recordsPath,
            _featureEngine,
            _thinkingStore,
            _knowledgeStore
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
    public async Task WriteFrame_Then_Complete_ShouldKeepPointersConsistent()
    {
        var topicId = "pointer_test_topic";
        var userInput = "我喜欢用简洁的风格写代码";
        var perception = new PerceptionModel { Scene = "工作" };
        var tags = new List<string> { "代码风格", "简洁" };

        // ===== 阶段一：写框架 =====
        var frameId = await _sinkService.WriteFrameAsync(
            topicId: topicId,
            userInput: userInput,
            perception: perception,
            tags: tags,
            route: "deep"
        );

        frameId.Should().NotBeNullOrEmpty();

        // 1. 认知卡片文件必须存在
        var cardPath = Path.Combine(_recordsPath, $"{frameId}.json");
        File.Exists(cardPath).Should().BeTrue("写框架后应创建认知卡片文件");

        // 2. 读卡片，检查 RecordId 和 TopicId
        var cardJson = await File.ReadAllTextAsync(cardPath);
        var card = JsonSerializer.Deserialize<CognitiveRecordModel>(cardJson);
        card.Should().NotBeNull();
        card!.Id.Should().Be(frameId);
        card.Status.Should().Be("pending");
        card.TopicId.Should().Be(topicId);

        // 3. 检查思考层索引文件是否存在（用 RecordId 去查）
        var thinkingIndex = await _thinkingStore.LoadIndex(card.RecordId, topicId);
        thinkingIndex.Should().NotBeNull("思考层应能通过 RecordId 找到索引");
        thinkingIndex!.CognitiveRecordId.Should().Be(frameId);
        thinkingIndex.TopicId.Should().Be(topicId);
        card.EventId.Should().NotBeNullOrEmpty("写框架时应保存事件ID");

        // ===== 阶段二：补全 =====
        await _sinkService.CompleteAsync(
            cardId: frameId,
            userInput: userInput,
            agentResponse: "好的，已记录你的偏好",
            status: "completed"
        );

        // 4. 重新读卡片，检查状态和 SourcePath
        var updatedJson = await File.ReadAllTextAsync(cardPath);
        var updatedCard = JsonSerializer.Deserialize<CognitiveRecordModel>(updatedJson);
        updatedCard.Should().NotBeNull();
        updatedCard!.Status.Should().Be("completed");

        // 5. SourcePath 应指向 topicId 下的概览文件
        updatedCard.SourcePath.Should().NotBeNullOrEmpty("补全后应更新 SourcePath 指向概览");
        var sourceFullPath = Path.Combine(_basePath, updatedCard.SourcePath!);
        File.Exists(sourceFullPath).Should().BeTrue($"SourcePath 指向的文件应存在: {updatedCard.SourcePath}");

        // 6. 验证概览文件内容确实是 LLM 返回的概览
        var overviewJson = await File.ReadAllTextAsync(sourceFullPath);
        var overview = JsonSerializer.Deserialize<OverviewModel>(overviewJson);
        overview.Should().NotBeNull();
        overview!.Text.Should().Contain("这是概览内容");
        overview.CognitiveRecordId.Should().Be(frameId);

        // 7. 用 CognitiveReader 按 code 检索，验证返回的指针与磁盘一致
        var code = _featureEngine.Tags.GetCode("代码风格");
        code.Should().NotBeNull();
        var match = await _cognitiveReader.MatchByCodes(new List<string> { code! }, topicId);
        match.Should().NotBeNull("补全后的卡片应可被检索");
        match!.RecordId.Should().Be(card.RecordId);
        match.Summary.Should().Contain("简洁");

        // 8. 判断 cognitive 卡片里的 RecordId 是否真的能加载到索引
        var loadedIndex = await _thinkingStore.LoadIndex(card.RecordId!, topicId);
        loadedIndex.Should().NotBeNull();

        // 9. 判断 cognitive 卡片里的 EventId 是否能加载到事件
        var loadedEvent = await _thinkingStore.LoadEvent(updatedCard.EventId!);
        loadedEvent.Should().NotBeNull("补全后应能通过 EventId 加载到事件");
        loadedEvent!.CognitiveRecordId.Should().Be(frameId);
        loadedEvent.Result.Status.Should().Be("completed");
        loadedEvent.Data.AgentResponse.Should().Be("好的，已记录你的偏好");
    }
}
