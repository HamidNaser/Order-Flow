using System.Threading.Tasks.Dataflow;
using Order.MessagePump.Pipelines;
using Xunit;

namespace Order.MessagePump.Tests.Pipelines;

public class PipelineTests
{
    // ──────────────────────────────────────────────
    // SendAsync + FlushAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_ItemProcessedByBlock()
    {
        // Arrange
        var processed = new List<int>();
        var actionBlock = new ActionBlock<int>(item => processed.Add(item));
        await using var pipeline = new Pipeline<int>(actionBlock);

        // Act
        await pipeline.SendAsync(42);
        await pipeline.FlushAsync();

        // Assert
        Assert.Single(processed);
        Assert.Equal(42, processed[0]);
    }

    [Fact]
    public async Task SendAsync_MultipleItems_AllProcessed()
    {
        // Arrange
        var processed = new List<string>();
        var actionBlock = new ActionBlock<string>(item => processed.Add(item));
        await using var pipeline = new Pipeline<string>(actionBlock);

        // Act
        await pipeline.SendAsync("a");
        await pipeline.SendAsync("b");
        await pipeline.SendAsync("c");
        await pipeline.FlushAsync();

        // Assert
        Assert.Equal(3, processed.Count);
        Assert.Contains("a", processed);
        Assert.Contains("b", processed);
        Assert.Contains("c", processed);
    }

    // ──────────────────────────────────────────────
    // DisposeAsync — flushes pending items
    // ──────────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_FlushesPendingItems()
    {
        // Arrange
        var processed = new List<int>();
        var actionBlock = new ActionBlock<int>(item => processed.Add(item));
        var pipeline = new Pipeline<int>(actionBlock);

        await pipeline.SendAsync(1);
        await pipeline.SendAsync(2);

        // Act
        await pipeline.DisposeAsync();

        // Assert
        Assert.Equal(2, processed.Count);
    }

    // ──────────────────────────────────────────────
    // Multi-block pipeline (start → final)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Pipeline_WithTransformAndAction_ProcessesChain()
    {
        // Arrange
        var results = new List<int>();
        var transform = new TransformBlock<int, int>(x => x * 2);
        var action = new ActionBlock<int>(x => results.Add(x));
        transform.LinkTo(action, new DataflowLinkOptions { PropagateCompletion = true });

        await using var pipeline = new Pipeline<int>(transform, action);

        // Act
        await pipeline.SendAsync(5);
        await pipeline.SendAsync(10);
        await pipeline.FlushAsync();

        // Assert
        Assert.Contains(10, results);
        Assert.Contains(20, results);
    }
}
