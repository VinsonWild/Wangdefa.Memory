using Wangdefa.AgentMemory.Cognitive;
using Wangdefa.AgentMemory.FeatureEngine;
using Wangdefa.AgentMemory.Interfaces;
using Wangdefa.AgentMemory.Knowledge;
using Wangdefa.AgentMemory.Models;
using Wangdefa.AgentMemory.Signal;
using Wangdefa.AgentMemory.Thinking;
using Wangdefa.AgentMemory.Thinking.Events;
using Wangdefa.AgentMemory.Thinking.KnowledgeExtractor;
using Wangdefa.Contracts;
using Wangdefa.Tools;


namespace Wangdefa.AgentMemory;

public static class ServiceRegistry
{
    private static string? _basePath;
    private static WangdefaMemory? _wangdefaMemory;
    private static FeatureEngine.FeatureEngine? _featureEngine;
    private static IThinkingStore? _thinkingStore;
    private static IKnowledgeStore? _knowledgeStore;
    private static IEventStore? _eventStore;
    private static ILearningOrchestrator? _learningOrchestrator;
    private static IMemorySinkService? _sinkService;
    private static CognitiveReader? _cognitiveReader;
    private static MemoryMetadataService? _metadataService;
    private static MemoryCleaner? _cleaner;

    public static void Initialize(IChatService chatService, string basePath, AgentMemory.Models.MaintenanceSettings? maintenanceSettings = null)
    {
        _basePath = basePath;

        if (!Directory.Exists(basePath))
        {
            Directory.CreateDirectory(basePath);
            Console.WriteLine($"[ServiceRegistry] 已创建目录: {basePath}");
        }

        var featureDb = new FeatureEngineDb(basePath);
        _featureEngine = new FeatureEngine.FeatureEngine(featureDb);

        _eventStore = new EventStore(basePath);
        _thinkingStore = new ThinkingStore(basePath, _eventStore);
        _knowledgeStore = new KnowledgeStore(basePath);

        _learningOrchestrator = new LearningOrchestrator(chatService, _eventStore);

        _cognitiveReader = new CognitiveReader(
            Path.Combine(basePath, "cognitive", "records"),
            _featureEngine,
            _thinkingStore,
            _knowledgeStore
        );

        var sqliteTools = new SQLiteTools();
        SQLiteTools.SetBasePath(basePath);
        _sinkService = new MemorySinkService(
            Path.Combine(basePath, "cognitive", "records"),
            basePath,
            _featureEngine,
            _thinkingStore,
            _knowledgeStore,
            _eventStore,
            _learningOrchestrator,
            sqliteTools
        );

        _metadataService = new MemoryMetadataService(basePath);
        _cleaner = new MemoryCleaner(basePath);

        _wangdefaMemory = new WangdefaMemory(
            basePath,
            _featureEngine,
            _thinkingStore,
            _cognitiveReader,
            _sinkService,
            _metadataService,
            _cleaner,
            maintenanceSettings ?? new AgentMemory.Models.MaintenanceSettings()
        );

        Console.WriteLine("[ServiceRegistry] 所有服务已注册完成");
    }

    public static WangdefaMemory GetWangdefaMemory() => _wangdefaMemory ?? throw new InvalidOperationException("ServiceRegistry 未初始化");
    public static string GetBasePath() => _basePath ?? throw new InvalidOperationException("ServiceRegistry 未初始化");
    public static FeatureEngine.FeatureEngine GetFeatureEngine() => _featureEngine ?? throw new InvalidOperationException("ServiceRegistry 未初始化");
    public static IThinkingStore GetThinkingStore() => _thinkingStore ?? throw new InvalidOperationException("ServiceRegistry 未初始化");
    public static IKnowledgeStore GetKnowledgeStore() => _knowledgeStore ?? throw new InvalidOperationException("ServiceRegistry 未初始化");
    public static IEventStore GetEventStore() => _eventStore ?? throw new InvalidOperationException("ServiceRegistry 未初始化");
    public static ILearningOrchestrator GetLearningOrchestrator() => _learningOrchestrator ?? throw new InvalidOperationException("ServiceRegistry 未初始化");
    public static IMemorySinkService GetSinkService() => _sinkService ?? throw new InvalidOperationException("ServiceRegistry 未初始化");
    public static CognitiveReader GetCognitiveReader() => _cognitiveReader ?? throw new InvalidOperationException("ServiceRegistry 未初始化");
    public static MemoryMetadataService GetMetadataService() => _metadataService ?? throw new InvalidOperationException("ServiceRegistry 未初始化");
    public static MemoryCleaner GetCleaner() => _cleaner ?? throw new InvalidOperationException("ServiceRegistry 未初始化");
}