using System.Text.Json;
using Order.MessageOperations.Api.Configuration;
using Order.MessageOperations.Api.Models;
using Microsoft.Extensions.Options;

namespace Order.MessageOperations.Api.Services;

public class MessageStorageService : IMessageStorageService
{
    private readonly MessageOperationsOptions _config;
    private readonly ILogger<MessageStorageService> _logger;

    public MessageStorageService(
        IOptions<MessageOperationsOptions> config,
        ILogger<MessageStorageService> logger)
    {
        _config = config.Value;
        _logger = logger;

        EnsureStorageDirectoryExists();
    }

    public string BuildBatchPath(string queueType, string batchId)
    {
        // Sanitize inputs to prevent path traversal attacks.
        // Path.GetFileName strips directory separators, ".." segments, etc.
        var safeQueueType = Path.GetFileName(queueType.ToLowerInvariant());
        var safeBatchId = Path.GetFileName(batchId);

        if (string.IsNullOrWhiteSpace(safeQueueType) || string.IsNullOrWhiteSpace(safeBatchId))
        {
            throw new ArgumentException("Invalid queueType or batchId.");
        }

        return Path.Combine(_config.MessageStoragePath, safeQueueType, safeBatchId);
    }

    public async Task<string> SaveBatchAsync(string queueType, List<SavedMessage> messages, string sourceDlq)
    {
        var batchId = $"{DateTime.UtcNow:yyyy-MM-dd_HHmmss}_batch-{Guid.NewGuid():N}".Substring(0, 40);
        var queueFolder = Path.Combine(_config.MessageStoragePath, queueType.ToLowerInvariant());
        var batchFolder = Path.Combine(queueFolder, batchId);

        Directory.CreateDirectory(batchFolder);

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
        await File.WriteAllTextAsync(
            manifestPath,
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

        for (int index = 0; index < messages.Count; index++)
        {
            var messagePath = Path.Combine(batchFolder, $"message-{index + 1:D3}.json");
            await File.WriteAllTextAsync(
                messagePath,
                JsonSerializer.Serialize(messages[index], new JsonSerializerOptions { WriteIndented = true }));
        }

        _logger.LogInformation(
            "Saved batch {BatchId} with {Count} messages to {Folder}",
            batchId,
            messages.Count,
            batchFolder);

        return batchFolder;
    }

    public async Task<List<SavedMessage>> LoadBatchAsync(string batchPath)
    {
        var messages = new List<SavedMessage>();
        if (!Directory.Exists(batchPath))
        {
            return messages;
        }

        var messageFiles = Directory.GetFiles(batchPath, "message-*.json")
            .OrderBy(path => path)
            .ToList();

        foreach (var file in messageFiles)
        {
            var json = await File.ReadAllTextAsync(file);
            var message = JsonSerializer.Deserialize<SavedMessage>(json);
            if (message is not null)
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
                .Where(batch => !string.IsNullOrWhiteSpace(batch))
                .Cast<string>()
                .OrderByDescending(batch => batch)
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

    private void EnsureStorageDirectoryExists()
    {
        if (!Directory.Exists(_config.MessageStoragePath))
        {
            Directory.CreateDirectory(_config.MessageStoragePath);
            _logger.LogInformation("Created message storage directory: {Path}", _config.MessageStoragePath);
        }
    }
}
