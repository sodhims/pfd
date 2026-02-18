using NUnit.Framework;
using Moq;
using Moq.Protected;
using PFD.Services;
using PFD.Shared.Interfaces;
using PFD.Shared.Models;
using PFD.Shared.Enums;
using System.Net;
using System.Text.Json;

namespace PFD.Services.Tests;

[TestFixture]
public class ClaudeServiceSortingTests
{
    private List<DailyTask> CreateTestTasks()
    {
        return new List<DailyTask>
        {
            new DailyTask
            {
                Id = 1,
                Title = "Urgent email to client",
                DaysInQueue = 3,
                CreatedAt = DateTime.Now.AddDays(-3),
                DueBy = DateTime.Today.AddDays(1)
            },
            new DailyTask
            {
                Id = 2,
                Title = "Research AI frameworks",
                DaysInQueue = 5,
                CreatedAt = DateTime.Now.AddDays(-5),
                DueBy = null
            },
            new DailyTask
            {
                Id = 3,
                Title = "Quick standup notes",
                DaysInQueue = 1,
                CreatedAt = DateTime.Now.AddDays(-1),
                DueBy = DateTime.Today.AddDays(7)
            },
            new DailyTask
            {
                Id = 4,
                Title = "Prepare meeting slides",
                DaysInQueue = 2,
                CreatedAt = DateTime.Now.AddDays(-2),
                DueBy = DateTime.Today.AddDays(2)
            }
        };
    }

    // ==================== LOCAL SORTING TESTS (No API needed) ====================

    [Test]
    public async Task SortWaitingTasks_ManualStrategy_ReturnsOriginalOrder()
    {
        // Arrange
        var service = new ClaudeService(apiKey: null); // No API key needed for manual
        var tasks = CreateTestTasks();

        // Act
        var results = await service.SortWaitingTasksAsync(tasks, WaitingSortStrategy.Manual);

        // Assert
        Assert.That(results, Has.Count.EqualTo(4));
        Assert.That(results[0].TaskId, Is.EqualTo(1));
        Assert.That(results[1].TaskId, Is.EqualTo(2));
        Assert.That(results[2].TaskId, Is.EqualTo(3));
        Assert.That(results[3].TaskId, Is.EqualTo(4));
        Assert.That(results.All(r => r.Reason == ""), Is.True, "Manual sort should have empty reasons");
    }

    [Test]
    public async Task SortWaitingTasks_OldestStrategy_SortsByDaysInQueueDescending()
    {
        // Arrange
        var service = new ClaudeService(apiKey: null);
        var tasks = CreateTestTasks();

        // Act
        var results = await service.SortWaitingTasksAsync(tasks, WaitingSortStrategy.Oldest);

        // Assert
        Assert.That(results, Has.Count.EqualTo(4));
        Assert.That(results[0].TaskId, Is.EqualTo(2), "Task with 5 days should be first");
        Assert.That(results[1].TaskId, Is.EqualTo(1), "Task with 3 days should be second");
        Assert.That(results[2].TaskId, Is.EqualTo(4), "Task with 2 days should be third");
        Assert.That(results[3].TaskId, Is.EqualTo(3), "Task with 1 day should be last");
        Assert.That(results[0].Reason, Does.Contain("5d waiting"));
    }

    [Test]
    public async Task SortWaitingTasks_NewestStrategy_SortsByDaysInQueueAscending()
    {
        // Arrange
        var service = new ClaudeService(apiKey: null);
        var tasks = CreateTestTasks();

        // Act
        var results = await service.SortWaitingTasksAsync(tasks, WaitingSortStrategy.Newest);

        // Assert
        Assert.That(results, Has.Count.EqualTo(4));
        Assert.That(results[0].TaskId, Is.EqualTo(3), "Task with 1 day should be first");
        Assert.That(results[1].TaskId, Is.EqualTo(4), "Task with 2 days should be second");
        Assert.That(results[2].TaskId, Is.EqualTo(1), "Task with 3 days should be third");
        Assert.That(results[3].TaskId, Is.EqualTo(2), "Task with 5 days should be last");
    }

    [Test]
    public async Task SortWaitingTasks_DueDateStrategy_SortsByDueDateAscending()
    {
        // Arrange
        var service = new ClaudeService(apiKey: null);
        var tasks = CreateTestTasks();

        // Act
        var results = await service.SortWaitingTasksAsync(tasks, WaitingSortStrategy.DueDate);

        // Assert
        Assert.That(results, Has.Count.EqualTo(4));
        // Task 1: Due tomorrow (1 day)
        // Task 4: Due in 2 days
        // Task 3: Due in 7 days
        // Task 2: No due date (goes last)
        Assert.That(results[0].TaskId, Is.EqualTo(1), "Task due tomorrow should be first");
        Assert.That(results[1].TaskId, Is.EqualTo(4), "Task due in 2 days should be second");
        Assert.That(results[2].TaskId, Is.EqualTo(3), "Task due in 7 days should be third");
        Assert.That(results[3].TaskId, Is.EqualTo(2), "Task with no due date should be last");
        Assert.That(results[3].Reason, Is.EqualTo("No due date"));
    }

    [Test]
    public async Task SortWaitingTasks_EmptyList_ReturnsEmptyList()
    {
        // Arrange
        var service = new ClaudeService(apiKey: null);
        var tasks = new List<DailyTask>();

        // Act
        var results = await service.SortWaitingTasksAsync(tasks, WaitingSortStrategy.Oldest);

        // Assert
        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task SortWaitingTasks_SingleTask_ReturnsSingleResult()
    {
        // Arrange
        var service = new ClaudeService(apiKey: null);
        var tasks = new List<DailyTask>
        {
            new DailyTask { Id = 42, Title = "Single task", DaysInQueue = 1 }
        };

        // Act
        var results = await service.SortWaitingTasksAsync(tasks, WaitingSortStrategy.Oldest);

        // Assert
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].TaskId, Is.EqualTo(42));
    }

    // ==================== AI SORTING FALLBACK TESTS ====================

    [Test]
    public async Task SortWaitingTasks_PriorityStrategy_NoApiKey_ReturnsFallback()
    {
        // Arrange
        var service = new ClaudeService(apiKey: null); // No API key
        var tasks = CreateTestTasks();

        // Act
        var results = await service.SortWaitingTasksAsync(tasks, WaitingSortStrategy.Priority);

        // Assert
        Assert.That(results, Has.Count.EqualTo(4));
        Assert.That(results.All(r => r.Reason == "AI unavailable"), Is.True);
    }

    [Test]
    public async Task SortWaitingTasks_QuickWinsStrategy_NoApiKey_ReturnsFallback()
    {
        // Arrange
        var service = new ClaudeService(apiKey: null);
        var tasks = CreateTestTasks();

        // Act
        var results = await service.SortWaitingTasksAsync(tasks, WaitingSortStrategy.QuickWins);

        // Assert
        Assert.That(results, Has.Count.EqualTo(4));
        Assert.That(results.All(r => r.Reason == "AI unavailable"), Is.True);
    }

    [Test]
    public async Task SortWaitingTasks_ContextStrategy_NoApiKey_ReturnsFallback()
    {
        // Arrange
        var service = new ClaudeService(apiKey: null);
        var tasks = CreateTestTasks();

        // Act
        var results = await service.SortWaitingTasksAsync(tasks, WaitingSortStrategy.Context);

        // Assert
        Assert.That(results, Has.Count.EqualTo(4));
        Assert.That(results.All(r => r.Reason == "AI unavailable"), Is.True);
    }

    [Test]
    public async Task SortWaitingTasks_StalenessStrategy_NoApiKey_ReturnsFallback()
    {
        // Arrange
        var service = new ClaudeService(apiKey: null);
        var tasks = CreateTestTasks();

        // Act
        var results = await service.SortWaitingTasksAsync(tasks, WaitingSortStrategy.Staleness);

        // Assert
        Assert.That(results, Has.Count.EqualTo(4));
        Assert.That(results.All(r => r.Reason == "AI unavailable"), Is.True);
    }

    // ==================== AI SORTING WITH MOCK HTTP ====================

    [Test]
    public async Task SortWaitingTasks_PriorityStrategy_WithApiKey_CallsClaudeAndParsesResponse()
    {
        // Arrange
        var mockResponse = new
        {
            sorted = new[]
            {
                new { id = 1, rank = 1, reason = "Urgent client" },
                new { id = 4, rank = 2, reason = "Meeting prep" },
                new { id = 3, rank = 3, reason = "Quick task" },
                new { id = 2, rank = 4, reason = "Low urgency" }
            }
        };

        var httpClient = CreateMockHttpClient(JsonSerializer.Serialize(mockResponse));
        var service = new ClaudeService(httpClient, "test-api-key");
        var tasks = CreateTestTasks();

        // Act
        var results = await service.SortWaitingTasksAsync(tasks, WaitingSortStrategy.Priority);

        // Assert
        Assert.That(results, Has.Count.EqualTo(4));
        Assert.That(results[0].TaskId, Is.EqualTo(1));
        Assert.That(results[0].Reason, Is.EqualTo("Urgent client"));
        Assert.That(results[1].TaskId, Is.EqualTo(4));
        Assert.That(results[1].Reason, Is.EqualTo("Meeting prep"));
    }

    [Test]
    public async Task SortWaitingTasks_QuickWinsStrategy_WithApiKey_ReturnsQuickTasksFirst()
    {
        // Arrange
        var mockResponse = new
        {
            sorted = new[]
            {
                new { id = 3, rank = 1, reason = "Quick notes" },
                new { id = 1, rank = 2, reason = "Simple email" },
                new { id = 4, rank = 3, reason = "Some effort" },
                new { id = 2, rank = 4, reason = "Complex task" }
            }
        };

        var httpClient = CreateMockHttpClient(JsonSerializer.Serialize(mockResponse));
        var service = new ClaudeService(httpClient, "test-api-key");
        var tasks = CreateTestTasks();

        // Act
        var results = await service.SortWaitingTasksAsync(tasks, WaitingSortStrategy.QuickWins);

        // Assert
        Assert.That(results[0].TaskId, Is.EqualTo(3), "Quick standup notes should be first");
        Assert.That(results[0].Reason, Is.EqualTo("Quick notes"));
        Assert.That(results[3].TaskId, Is.EqualTo(2), "Research task should be last");
    }

    [Test]
    public async Task SortWaitingTasks_ApiReturnsPartialResults_AddsMissingTasks()
    {
        // Arrange - API only returns 2 of 4 tasks
        var mockResponse = new
        {
            sorted = new[]
            {
                new { id = 1, rank = 1, reason = "Important" },
                new { id = 3, rank = 2, reason = "Quick" }
            }
        };

        var httpClient = CreateMockHttpClient(JsonSerializer.Serialize(mockResponse));
        var service = new ClaudeService(httpClient, "test-api-key");
        var tasks = CreateTestTasks();

        // Act
        var results = await service.SortWaitingTasksAsync(tasks, WaitingSortStrategy.Priority);

        // Assert
        Assert.That(results, Has.Count.EqualTo(4), "All tasks should be in results");
        Assert.That(results[0].TaskId, Is.EqualTo(1));
        Assert.That(results[1].TaskId, Is.EqualTo(3));
        // Missing tasks should be added at end
        var missingTaskIds = results.Skip(2).Select(r => r.TaskId).ToList();
        Assert.That(missingTaskIds, Does.Contain(2));
        Assert.That(missingTaskIds, Does.Contain(4));
    }

    [Test]
    public async Task SortWaitingTasks_ApiError_ReturnsFallbackOrder()
    {
        // Arrange - Create a mock that returns an error
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var httpClient = new HttpClient(mockHandler.Object);
        var service = new ClaudeService(httpClient, "test-api-key");
        var tasks = CreateTestTasks();

        // Act
        var results = await service.SortWaitingTasksAsync(tasks, WaitingSortStrategy.Priority);

        // Assert - Should return all tasks (fallback behavior adds missing tasks)
        Assert.That(results, Has.Count.EqualTo(4));
    }

    // ==================== HELPER METHODS ====================

    private HttpClient CreateMockHttpClient(string sortedJson)
    {
        // Wrap the sorted response in Claude API format
        var claudeResponse = new
        {
            content = new[]
            {
                new { type = "text", text = sortedJson }
            },
            stop_reason = "end_turn"
        };

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(claudeResponse))
            });

        return new HttpClient(mockHandler.Object);
    }
}
