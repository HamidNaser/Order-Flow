using Order.MessageOperations.Api.Configuration;
using Order.MessageOperations.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Order.MessageOperations.Api.Tests.Services;

public class MessageStorageServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MessageStorageService _sut;

    public MessageStorageServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "msg-storage-test-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);

        var options = Options.Create(new MessageOperationsOptions
        {
            MessageStoragePath = _tempDir
        });

        _sut = new MessageStorageService(options, NullLogger<MessageStorageService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void BuildBatchPath_ValidInputs_CombinesCorrectly()
    {
        // Act
        var result = _sut.BuildBatchPath("InboundOrders", "batch123");

        // Assert
        var expected = Path.Combine(_tempDir, "inboundorders", "batch123");
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("../../../etc", "batch1")]
    [InlineData("queue", "../../secret")]
    [InlineData("..\\windows\\system32", "batch1")]
    [InlineData("queue", "..\\..\\secret")]
    public void BuildBatchPath_PathTraversal_StripsDirectoryComponents(string queueType, string batchId)
    {
        // Act — should not throw; the dangerous segments are stripped
        var result = _sut.BuildBatchPath(queueType, batchId);

        // Assert — result must stay inside the storage root
        Assert.StartsWith(_tempDir, result);
        Assert.DoesNotContain("..", result);
    }

    [Theory]
    [InlineData("", "batch1")]
    [InlineData("queue", "")]
    [InlineData("  ", "batch1")]
    [InlineData("/", "batch1")]
    public void BuildBatchPath_InvalidInputs_ThrowsArgumentException(string queueType, string batchId)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _sut.BuildBatchPath(queueType, batchId));
    }
}
