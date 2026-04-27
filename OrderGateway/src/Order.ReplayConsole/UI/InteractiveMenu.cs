using DlqReplayTool.Models;
using DlqReplayTool.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DlqReplayTool.UI;

public class InteractiveMenu
{
    private const string MainKeySuffix = "-main";
    private const string DlqKeySuffix = "-dlq";
    private const string DeadletterSuffix = "-deadletter";
    private readonly DlqReplayConfig _config;
    private readonly DlqReplayService _replayService;
    private readonly MessageStorageService _storageService;
    private readonly S3SyncService _s3SyncService;
    private readonly ILogger<InteractiveMenu> _logger;

    public InteractiveMenu(
        IOptions<DlqReplayConfig> config,
        DlqReplayService replayService,
        MessageStorageService storageService,
        S3SyncService s3SyncService,
        ILogger<InteractiveMenu> logger)
    {
        _config = config.Value;
        _replayService = replayService;
        _storageService = storageService;
        _s3SyncService = s3SyncService;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        Console.Clear();
        ShowBanner();

        while (!cancellationToken.IsCancellationRequested)
        {
            ShowMainMenu();
            var choice = ReadInput("\nSelect option");

            try
            {
                switch (choice)
                {
                    case "1":
                        await ReplayFromFilesAsync(cancellationToken);
                        break;
                    case "2":
                        await ListSavedBatchesAsync();
                        break;
                    case "3":
                        await CheckLocalStackQueuesAsync(cancellationToken);
                        break;
                    case "0":
                        Console.WriteLine("\n👋 Goodbye!");
                        return;
                    default:
                        Console.WriteLine("\n❌ Invalid option. Please try again.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error: {ex.Message}");
                _logger.LogError(ex, "Error during operation");
            }

            if (!cancellationToken.IsCancellationRequested && choice != "0")
            {
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey(true);
                Console.Clear();
                ShowBanner();
            }
        }
    }

    private void ShowBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔════════════════════════════════════════════════════╗");
        Console.WriteLine("║              Order Replay Console                  ║");
        Console.WriteLine("╚════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine($"AWS Region: {_config.AwsRegion} | Environment: {_config.Environment}");
        Console.WriteLine($"LocalStack: {_config.LocalStackEndpoint}");
        Console.WriteLine();
    }

    private void ShowMainMenu()
    {
        Console.WriteLine("Main Menu:");
        Console.WriteLine("  1. Replay saved alert messages");
        Console.WriteLine("  2. List Saved Batches");
        Console.WriteLine("  3. Check LocalStack Queues");
        Console.WriteLine("  0. Exit");
    }

    private async Task DownloadFromAwsAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("Download from AWS Queue");
        Console.WriteLine(new string('=', 50));

        // Select queue
        var queueSelection = SelectQueue();
        if (queueSelection == null) return;

        // Select download mode
        Console.WriteLine("\nDownload Mode:");
        Console.WriteLine("  1. Count (download N messages)");
        Console.WriteLine("  2. Specific Message ID");
        Console.WriteLine("  3. All messages (until empty)");
        Console.WriteLine("  0. Cancel");

        var mode = ReadInput("\nSelect mode");

        int? maxMessages = null;
        string? messageId = null;

        switch (mode)
        {
            case "1":
                var countStr = ReadInput("Enter message count");
                if (int.TryParse(countStr, out var count) && count > 0)
                {
                    maxMessages = count;
                }
                else
                {
                    Console.WriteLine("❌ Invalid count");
                    return;
                }
                break;
            case "2":
                messageId = ReadInput("Enter Message ID");
                if (string.IsNullOrWhiteSpace(messageId))
                {
                    Console.WriteLine("❌ Invalid Message ID");
                    return;
                }
                break;
            case "3":
                maxMessages = null; // Download all
                break;
            case "0":
                return;
            default:
                Console.WriteLine("❌ Invalid mode");
                return;
        }

        // Execute download and replay
        Console.WriteLine($"\n⏳ Downloading from {queueSelection.DisplayName}...");

        var (downloaded, batchPath) = await _replayService.DownloadFromAwsQueueAsyncByName(
            queueSelection.QueueType,
            queueSelection.AwsSourceQueueName,
            maxMessages,
            messageId,
            cancellationToken);

        if (downloaded == 0)
        {
            Console.WriteLine("\n⚠️  No messages downloaded.");
            return;
        }

        var messages = await _storageService.LoadBatchAsync(batchPath);

        Console.WriteLine("⏳ Syncing S3 objects to LocalStack...");
        var syncedCount = await _s3SyncService.SyncS3ObjectsForMessagesAsync(messages, true, cancellationToken);
        if (syncedCount > 0)
        {
            Console.WriteLine($"✅ Synced {syncedCount} S3 object(s) to LocalStack");
        }

        Console.WriteLine($"⏳ Replaying {messages.Count} messages to LocalStack...");
        var successCount = await _replayService.ReplayToLocalStackAsyncByName(
            queueSelection.LocalStackQueueName,
            messages,
            cancellationToken);

        Console.WriteLine($"\n✅ Completed! Replayed {successCount}/{messages.Count} messages to LocalStack.");
    }

    private async Task ReplayFromFilesAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("Replay from Saved Files");
        Console.WriteLine(new string('=', 50));

        var batches = _storageService.ListAvailableBatches();
        if (batches.Count == 0)
        {
            Console.WriteLine("\n⚠️  No saved batches found.");
            return;
        }

        // Display available batches
        Console.WriteLine("\nAvailable Batches:");
        var batchList = new List<(string QueueType, string BatchId, string FullPath)>();
        var index = 1;

        foreach (var (queueType, batchIds) in batches)
        {
            Console.WriteLine($"\n{queueType.ToUpper()}:");
            foreach (var batchId in batchIds)
            {
                var fullPath = Path.Combine(_config.MessageStoragePath, queueType, batchId);
                var manifest = await _storageService.LoadManifestAsync(fullPath);
                
                Console.Write($"  {index}. {batchId}");
                if (manifest != null)
                {
                    Console.Write($" ({manifest.MessageCount} messages, {manifest.CreatedAt:yyyy-MM-dd HH:mm})");
                }
                Console.WriteLine();

                batchList.Add((queueType, batchId, fullPath));
                index++;
            }
        }

        var selection = ReadInput("\nSelect batch number (0 to cancel)");
        if (!int.TryParse(selection, out var selectedIndex) || selectedIndex < 1 || selectedIndex > batchList.Count)
        {
            if (selectedIndex != 0)
            {
                Console.WriteLine("❌ Invalid selection");
            }
            return;
        }

        var selectedBatch = batchList[selectedIndex - 1];
        var queueSelection = ResolveQueueSelectionFromQueueType(selectedBatch.QueueType);
        if (queueSelection == null)
        {
            Console.WriteLine($"❌ Could not find queue mapping for type: {selectedBatch.QueueType}");
            return;
        }

        Console.WriteLine($"\n⏳ Loading messages from batch...");
        var messages = await _storageService.LoadBatchAsync(selectedBatch.FullPath);

        // Sync cached S3 objects to LocalStack before replaying messages
        Console.WriteLine("⏳ Syncing cached S3 objects to LocalStack...");
        var syncedCount = await _s3SyncService.SyncS3ObjectsForMessagesAsync(messages, false, cancellationToken);
        if (syncedCount > 0)
        {
            Console.WriteLine($"✅ Synced {syncedCount} cached S3 object(s) to LocalStack");
        }

        Console.WriteLine($"⏳ Replaying {messages.Count} messages to LocalStack...");
        var successCount = await _replayService.ReplayToLocalStackAsyncByName(
            queueSelection.LocalStackQueueName,
            messages,
            cancellationToken);

        Console.WriteLine($"\n✅ Completed! Replayed {successCount}/{messages.Count} messages.");
    }

    private async Task ListSavedBatchesAsync()
    {
        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("Saved Message Batches");
        Console.WriteLine(new string('=', 50));

        var batches = _storageService.ListAvailableBatches();
        if (batches.Count == 0)
        {
            Console.WriteLine("\n⚠️  No saved batches found.");
            return;
        }

        foreach (var (queueType, batchIds) in batches)
        {
            Console.WriteLine($"\n📁 {queueType.ToUpper()} ({batchIds.Count} batches)");
            foreach (var batchId in batchIds.Take(10))
            {
                var fullPath = Path.Combine(_config.MessageStoragePath, queueType, batchId);
                var manifestPath = Path.Combine(fullPath, "manifest.json");
                
                if (File.Exists(manifestPath))
                {
                    var manifest = await _storageService.LoadManifestAsync(fullPath);
                    if (manifest != null)
                    {
                        Console.WriteLine($"  • {batchId}");
                        Console.WriteLine($"    {manifest.MessageCount} messages | {manifest.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                    }
                }
            }

            if (batchIds.Count > 10)
            {
                Console.WriteLine($"  ... and {batchIds.Count - 10} more");
            }
        }

        Console.WriteLine($"\nStorage path: {Path.GetFullPath(_config.MessageStoragePath)}");
    }

    private async Task CheckLocalStackQueuesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine("\n" + new string('=', 50));
            Console.WriteLine("LocalStack Queue Inspector");
            Console.WriteLine(new string('=', 50));
            Console.WriteLine();
            Console.WriteLine("1. List all LocalStack queues");
            Console.WriteLine("2. Check specific queue status");
            Console.WriteLine("3. Peek at messages in queue");
            Console.WriteLine("0. Back to main menu");

            var selection = ReadInput("\nSelect option");
            switch (selection)
            {
                case "1":
                    await ListAllLocalStackQueuesAsync(cancellationToken);
                    break;
                case "2":
                    await ShowQueueStatusAsync(cancellationToken);
                    break;
                case "3":
                    await PeekQueueMessagesAsync(cancellationToken);
                    break;
                case "0":
                    return;
                default:
                    Console.WriteLine("\n❌ Invalid option. Please try again.");
                    break;
            }
        }
    }

    private async Task ListAllLocalStackQueuesAsync(CancellationToken cancellationToken)
    {
        var queueUrls = await _replayService.ListLocalStackQueuesAsync(cancellationToken);
        if (queueUrls.Count == 0)
        {
            Console.WriteLine("\n⚠️  No queues found in LocalStack.");
            return;
        }

        Console.WriteLine($"\nFound {queueUrls.Count} queue(s):");
        foreach (var queueUrl in queueUrls.OrderBy(url => url))
        {
            Console.WriteLine($"  • {queueUrl}");
        }
    }

    private async Task ShowQueueStatusAsync(CancellationToken cancellationToken)
    {
        var queueSelection = SelectQueue();
        if (queueSelection == null)
        {
            return;
        }

        var queueName = queueSelection.LocalStackQueueName;

        try
        {
            var attributes = await _replayService.GetLocalStackQueueAttributesAsync(queueName, cancellationToken);
            if (attributes.Count == 0)
            {
                Console.WriteLine("\n⚠️  No attributes found for queue.");
                return;
            }

            Console.WriteLine($"\nQueue: {queueName}");
            PrintAttributeIfExists(attributes, "ApproximateNumberOfMessages");
            PrintAttributeIfExists(attributes, "ApproximateNumberOfMessagesNotVisible");
            PrintAttributeIfExists(attributes, "ApproximateNumberOfMessagesDelayed");
        }
        catch (Amazon.SQS.Model.QueueDoesNotExistException)
        {
            Console.WriteLine("\n❌ Queue does not exist in LocalStack.");
        }
    }

    private async Task PeekQueueMessagesAsync(CancellationToken cancellationToken)
    {
        var queueSelection = SelectQueue();
        if (queueSelection == null)
        {
            return;
        }

        var queueName = queueSelection.LocalStackQueueName;

        var countInput = ReadInput("Message count (1-10)");
        var maxMessages = 5;
        if (int.TryParse(countInput, out var parsedCount))
        {
            maxMessages = Math.Clamp(parsedCount, 1, 10);
        }

        try
        {
            var messages = await _replayService.PeekLocalStackMessagesAsync(queueName, maxMessages, cancellationToken);
            if (messages.Count == 0)
            {
                Console.WriteLine("\n⚠️  No messages found in queue.");
                return;
            }

            Console.WriteLine($"\nFound {messages.Count} message(s):");
            foreach (var message in messages)
            {
                var bodyPreview = message.Body?.Length > 200
                    ? message.Body.Substring(0, 200) + "..."
                    : message.Body ?? string.Empty;
                Console.WriteLine($"  • {message.MessageId} ({message.Body?.Length ?? 0} bytes)");
                if (!string.IsNullOrWhiteSpace(bodyPreview))
                {
                    Console.WriteLine($"    {bodyPreview}");
                }
            }
        }
        catch (Amazon.SQS.Model.QueueDoesNotExistException)
        {
            Console.WriteLine("\n❌ Queue does not exist in LocalStack.");
        }
    }

    private void PrintAttributeIfExists(IReadOnlyDictionary<string, string> attributes, string key)
    {
        if (attributes.TryGetValue(key, out var value))
        {
            Console.WriteLine($"  {key}: {value}");
        }
    }

    private QueueSelection? SelectQueue()
    {
        Console.WriteLine("\nAvailable Queues:");
        var selections = BuildQueueSelections();

        if (selections.Count == 0)
        {
            Console.WriteLine("❌ No enabled queues found in configuration");
            return null;
        }

        var index = 1;
        foreach (var selection in selections)
        {
            Console.WriteLine($"  {index}. {selection.DisplayName} ({selection.LocalStackQueueName})");
            index++;
        }
        Console.WriteLine("  0. Cancel");

        var selectionInput = ReadInput("\nSelect queue");
        if (!int.TryParse(selectionInput, out var selectedIndex) || selectedIndex < 1 || selectedIndex > selections.Count)
        {
            if (selectedIndex != 0)
            {
                Console.WriteLine("❌ Invalid selection");
            }
            return null;
        }

        return selections[selectedIndex - 1];
    }

    private QueueSelection? ResolveQueueSelectionFromQueueType(string queueType)
    {
        if (queueType.EndsWith(MainKeySuffix, StringComparison.OrdinalIgnoreCase))
        {
            var baseKey = queueType[..^MainKeySuffix.Length];
            if (_config.Queues.TryGetValue(baseKey, out var mainMapping))
            {
                return new QueueSelection(
                    queueType,
                    $"{mainMapping.DisplayName} (Main)",
                    mainMapping.LocalStackQueueName,
                    mainMapping.LocalStackQueueName,
                    mainMapping.AwsDlqName);
            }
        }

        if (_config.Queues.TryGetValue(queueType, out var mapping))
        {
            return new QueueSelection(
                queueType,
                $"{mapping.DisplayName} (Main)",
                mapping.LocalStackQueueName,
                mapping.LocalStackQueueName,
                mapping.AwsDlqName);
        }

        if (queueType.EndsWith(DlqKeySuffix, StringComparison.OrdinalIgnoreCase))
        {
            var baseKey = queueType[..^DlqKeySuffix.Length];
            if (_config.Queues.TryGetValue(baseKey, out var baseMapping))
            {
                return new QueueSelection(
                    queueType,
                    $"{baseMapping.DisplayName} (DLQ)",
                    baseMapping.LocalStackQueueName + DeadletterSuffix,
                    baseMapping.AwsDlqName,
                    baseMapping.AwsDlqName);
            }
        }

        var match = _config.Queues.FirstOrDefault(q =>
            q.Key.Equals(queueType, StringComparison.OrdinalIgnoreCase));

        return match.Key != null
            ? new QueueSelection(
                match.Key,
                $"{match.Value.DisplayName} (Main)",
                match.Value.LocalStackQueueName,
                match.Value.LocalStackQueueName,
                match.Value.AwsDlqName)
            : null;
    }

    private List<QueueSelection> BuildQueueSelections()
    {
        var selections = new List<QueueSelection>();
        foreach (var (key, mapping) in _config.Queues.Where(q => q.Value.Enabled))
        {
            selections.Add(new QueueSelection(
                key + MainKeySuffix,
                $"{mapping.DisplayName} (Main)",
                mapping.LocalStackQueueName,
                mapping.LocalStackQueueName,
                mapping.AwsDlqName));

            selections.Add(new QueueSelection(
                key + DlqKeySuffix,
                $"{mapping.DisplayName} (DLQ)",
                mapping.LocalStackQueueName + DeadletterSuffix,
                mapping.AwsDlqName,
                mapping.AwsDlqName));
        }

        return selections;
    }

    private string ReadInput(string prompt)
    {
        Console.Write($"{prompt}: ");
        return Console.ReadLine()?.Trim() ?? string.Empty;
    }

    private sealed record QueueSelection(
        string QueueType,
        string DisplayName,
        string LocalStackQueueName,
        string AwsSourceQueueName,
        string AwsDlqName);
}
