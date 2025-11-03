using NUnit.Framework;
using Moq;
using EywaClient.Core;
using EywaClient.Models;

namespace EywaClient.Tests.Core;

[TestFixture]
public class LoggerTests
{
    private Mock<JsonRpcClient> _mockRpcClient = null!;
    private Logger _logger = null!;

    [SetUp]
    public void Setup()
    {
        _mockRpcClient = new Mock<JsonRpcClient>(null, null);
        _logger = new Logger(_mockRpcClient.Object);
    }

    [Test]
    public void Constructor_ThrowsOnNullClient()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Logger(null!));
    }

    [Test]
    public void Log_ThrowsOnNullRecord()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _logger.Log(null!));
    }

    [Test]
    public void Log_SendsCorrectNotification()
    {
        // Arrange
        var record = new LogRecord
        {
            Event = "INFO",
            Message = "Test message",
            Data = new { value = 42 },
            Duration = 1500
        };

        // Act
        _logger.Log(record);

        // Assert
        _mockRpcClient.Verify(
            x => x.SendNotification("task.log", It.IsAny<object>()),
            Times.Once);
    }

    [Test]
    public void Info_SendsInfoEvent()
    {
        // Act
        _logger.Info("Test info message", new { test = true });

        // Assert
        _mockRpcClient.Verify(
            x => x.SendNotification("task.log", It.IsAny<object>()),
            Times.Once);
    }

    [Test]
    public void Info_WithNullData_Works()
    {
        // Act & Assert
        Assert.DoesNotThrow(() => _logger.Info("Test message"));

        _mockRpcClient.Verify(
            x => x.SendNotification("task.log", It.IsAny<object>()),
            Times.Once);
    }

    [Test]
    public void Error_SendsErrorEvent()
    {
        // Act
        _logger.Error("Test error", new { errorCode = 500 });

        // Assert
        _mockRpcClient.Verify(
            x => x.SendNotification("task.log", It.IsAny<object>()),
            Times.Once);
    }

    [Test]
    public void Warn_SendsWarnEvent()
    {
        // Act
        _logger.Warn("Test warning", new { threshold = 90 });

        // Assert
        _mockRpcClient.Verify(
            x => x.SendNotification("task.log", It.IsAny<object>()),
            Times.Once);
    }

    [Test]
    public void Debug_SendsDebugEvent()
    {
        // Act
        _logger.Debug("Test debug", new { variable = "value" });

        // Assert
        _mockRpcClient.Verify(
            x => x.SendNotification("task.log", It.IsAny<object>()),
            Times.Once);
    }

    [Test]
    public void Trace_SendsTraceEvent()
    {
        // Act
        _logger.Trace("Test trace", new { function = "processData" });

        // Assert
        _mockRpcClient.Verify(
            x => x.SendNotification("task.log", It.IsAny<object>()),
            Times.Once);
    }

    [Test]
    public void Exception_SendsExceptionEvent()
    {
        // Act
        _logger.Exception("Test exception", new { stack = "..." });

        // Assert
        _mockRpcClient.Verify(
            x => x.SendNotification("task.log", It.IsAny<object>()),
            Times.Once);
    }

    [Test]
    public void Report_SendsReportNotification()
    {
        // Act
        _logger.Report("Analysis complete", new { accuracy = 0.95 }, null);

        // Assert
        _mockRpcClient.Verify(
            x => x.SendNotification("task.report", It.IsAny<object>()),
            Times.Once);
    }

    [Test]
    public void Report_WithImage_Works()
    {
        // Arrange
        var imageData = "base64encodedimage";

        // Act
        _logger.Report("Report with image", new { count = 100 }, imageData);

        // Assert
        _mockRpcClient.Verify(
            x => x.SendNotification("task.report", It.IsAny<object>()),
            Times.Once);
    }

    [Test]
    public void Report_WithNullDataAndImage_Works()
    {
        // Act & Assert
        Assert.DoesNotThrow(() => _logger.Report("Simple report"));

        _mockRpcClient.Verify(
            x => x.SendNotification("task.report", It.IsAny<object>()),
            Times.Once);
    }

    [Test]
    public void AllLogLevels_SendNotifications()
    {
        // Act - call all log levels
        _logger.Info("Info");
        _logger.Error("Error");
        _logger.Warn("Warn");
        _logger.Debug("Debug");
        _logger.Trace("Trace");
        _logger.Exception("Exception");

        // Assert - verify all were sent
        _mockRpcClient.Verify(
            x => x.SendNotification("task.log", It.IsAny<object>()),
            Times.Exactly(6));
    }
}
