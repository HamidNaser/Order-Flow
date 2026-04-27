using Order.MessageOperations.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Order.MessageOperations.Api.Controllers.V1;

[ApiController]
[Route("api/v1/batches")]
public class BatchesController : ControllerBase
{
    private readonly MessageStorageService _messageStorageService;

    public BatchesController(MessageStorageService messageStorageService)
    {
        _messageStorageService = messageStorageService;
    }

    [HttpGet]
    public IActionResult ListBatches()
    {
        var batches = _messageStorageService.ListAvailableBatches()
            .Select(item => new
            {
                QueueType = item.QueueType,
                BatchIds = item.Batches
            })
            .OrderBy(item => item.QueueType)
            .ToList();

        return Ok(batches);
    }

    [HttpGet("{queueType}/{batchId}")]
    public async Task<IActionResult> GetBatchManifest(string queueType, string batchId)
    {
        var batchPath = _messageStorageService.BuildBatchPath(queueType, batchId);
        var manifest = await _messageStorageService.LoadManifestAsync(batchPath);
        if (manifest is null)
        {
            return NotFound(new { Message = "Batch manifest not found" });
        }

        return Ok(manifest);
    }

    [HttpGet("{queueType}/{batchId}/messages")]
    public async Task<IActionResult> GetBatchMessages(string queueType, string batchId)
    {
        var batchPath = _messageStorageService.BuildBatchPath(queueType, batchId);
        if (!Directory.Exists(batchPath))
        {
            return NotFound(new { Message = "Batch not found" });
        }

        var messages = await _messageStorageService.LoadBatchAsync(batchPath);
        return Ok(messages);
    }
}
