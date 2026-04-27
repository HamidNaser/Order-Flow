using System.Text.Json;
using DlqReplayTool.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DlqReplayTool.Services;

public class MessageStorageService
{
    private readonly DlqReplayConfig _config;
    private readonly ILogger<MessageStorageService> _logger;

    public MessageStorageService(
        IOptions<DlqReplayConfig> config,
        ILogger<MessageStorageService> logger)
    {
        _config = config.Value;
        _logger = logger;
        
        EnsureStorageDirectoryExists();
    }

    private void EnsureStorageDirectoryExists()
    {
        if (!Directory.Exists(_config.MessageStoragePath))
        {
            Directory.CreateDirectory(_config.MessageStoragePath);
            _logger.LogInformation("Created message storage directory: {Path}", _config.MessageStoragePath);
        }
    }

    public async Task<string> SaveBatchAsync(string queueType, List<SavedMessage> messages, string sourceDlq)
    {
        var batchId = $"{DateTime.UtcNow:yyyy-MM-dd_HHmmss}_batch-{Guid.NewGuid():N}".Substring(0, 40);
        var queueFolder = Path.Combine(_config.MessageStoragePath, queueType.ToLowerInvariant());
        var batchFolder = Path.Combine(queueFolder, batchId);

        Directory.CreateDirectory(batchFolder);

        // Save manifest
        var manifest = new MessageBatch
        {
            BatchId = batchId,
            QueueType = queueType,
            CreatedAt = DateTime.UtcNow,
            SourceDlq = sourceDlq,
            MessageCount = messages.Count,
            MessageIds = messages.Select(m => m.MessageId).ToList()
        };

        var manifestPath = Path.Combine(batchFolder, "manifest.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

        // Save individual messages
        for (int i = 0; i < messages.Count; i++)
        {
            var messagePath = Path.Combine(batchFolder, $"message-{i + 1:D3}.json");
            await File.WriteAllTextAsync(messagePath, JsonSerializer.Serialize(messages[i], new JsonSerializerOptions { WriteIndented = true }));
        }

        _logger.LogInformation("Saved batch {BatchId} with {Count} messages to {Folder}", batchId, messages.Count, batchFolder);
        return batchFolder;
    }

    public async Task<List<SavedMessage>> LoadBatchAsync(string batchPath)
    {
        var messages = new List<SavedMessage>();
        var messageFiles = Directory.GetFiles(batchPath, "message-*.json").OrderBy(f => f).ToList();

        foreach (var file in messageFiles)
        {
            var json = await File.ReadAllTextAsync(file);
            var message = JsonSerializer.Deserialize<SavedMessage>(json);
            if (message != null)
            {
                messages.Add(message);
            }
        }

        _logger.LogInformation("Loaded {Count} messages from batch {Path}", messages.Count, batchPath);
        return messages;
    }

    public List<(string QueueType, List<string> Batches)> ListAvailableBatches()
    {
        var result = new List<(string QueueType, List<string> Batches)>();

        if (!Directory.Exists(_config.MessageStoragePath))
        {
            return result;
        }

        var queueFolders = Directory.GetDirectories(_config.MessageStoragePath);

        foreach (var queueFolder in queueFolders)
        {
            var queueType = Path.GetFileName(queueFolder);
            var batches = Directory.GetDirectories(queueFolder)
                .Select(Path.GetFileName)
                .Where(b => b != null)
                .Cast<string>()
                .OrderByDescending(b => b)
                .ToList();

            if (batches.Any())
            {
                result.Add((queueType, batches));
            }
        }

        return result;
    }

    public async Task<MessageBatch?> LoadManifestAsync(string batchPath)
    {
        var manifestPath = Path.Combine(batchPath, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(manifestPath);
        return JsonSerializer.Deserialize<MessageBatch>(json);
    }
}
