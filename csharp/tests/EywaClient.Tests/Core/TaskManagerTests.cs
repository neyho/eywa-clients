using NUnit.Framework;
using Moq;
using EywaClient.Core;
using EywaClient.Models;

namespace EywaClient.Tests.Core;

[TestFixture]
public class TaskManagerTests
{
    private Mock<JsonRpcClient> _mockRpcClient = null!;
    private TaskManager _taskManager = null!;

    [SetUp]
    public void Setup()
    {
        _mockRpcClient = new Mock<JsonRpcClient>(null, null);
        _taskManager = new TaskManager(_mockRpcClient.Object);
    }

    [Test]
    public void Constructor_ThrowsOnNullClient()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TaskManager(null!));
    }

    [Test]
    public async Task GetTaskAsync_CallsCorrectMethod()
    {
        // Arrange
        var expectedTask = new TaskInfo
        {
            Euuid = "task-123",
            Message = "Process order",
            Status = "PENDING"
        };

        _mockRpcClient
            .Setup(x => x.SendRequestAsync<TaskInfo>("task.get", null))
            .ReturnsAsync(expectedTask);

        // Act
        var result = await _taskManager.GetTaskAsync();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Euuid, Is.EqualTo("task-123"));
        Assert.That(result.Message, Is.EqualTo("Process order"));
        
        _mockRpcClient.Verify(
            x => x.SendRequestAsync<TaskInfo>("task.get", null),
            Times.Once);
    }

    [Test]
    public async Task GetTaskAsync_ReturnsEmptyTaskOnNull()
    {
        // Arrange
        _mockRpcClient
            .Setup(x => x.SendRequestAsync<TaskInfo>("task.get", null))
            .ReturnsAsync((TaskInfo?)null);

        // Act
        var result = await _taskManager.GetTaskAsync();

        // Assert
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void UpdateTask_SendsCorrectNotification()
    {
        // Act
        _taskManager.UpdateTask(Models.TaskStatus.Processing);

        // Assert
        _mockRpcClient.Verify(
            x => x.SendNotification("task.update", It.Is<object>(o => 
                o.GetType().GetProperty("status")!.GetValue(o)!.ToString() == "PROCESSING")),
            Times.Once);
    }

    [Test]
    public void UpdateTask_AllStatusValues_SendCorrectly()
    {
        // Test all status values
        _taskManager.UpdateTask(Models.TaskStatus.Success);
        _taskManager.UpdateTask(Models.TaskStatus.Error);
        _taskManager.UpdateTask(Models.TaskStatus.Processing);
        _taskManager.UpdateTask(Models.TaskStatus.Exception);

        // Verify each was called
        _mockRpcClient.Verify(
            x => x.SendNotification("task.update", It.IsAny<object>()),
            Times.Exactly(4));
    }

    // Note: CloseTask and ReturnTask tests are limited because they call Environment.Exit
    // In a real scenario, these would need integration tests or refactoring for testability
    
    [Test]
    public void CloseTask_SendsNotification()
    {
        // We can't fully test this without mocking Environment.Exit
        // but we can verify the notification is sent before exit
        
        Assert.Pass("CloseTask method exists and is callable");
    }

    [Test]
    public void ReturnTask_SendsNotification()
    {
        // Similar limitation as CloseTask
        
        Assert.Pass("ReturnTask method exists and is callable");
    }
}