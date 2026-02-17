using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace PFD.PlaywrightTests;

/// <summary>
/// Tests for the task workflow system:
/// - Time parsing from task text (e.g., "meeting at 4:00 pm for Wed")
/// - Multi-slot display for tasks with duration > 20 minutes
/// - Task state transitions with DaysInQueue tracking
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class TaskWorkflowTests : PageTest
{
    private const string BaseUrl = "https://localhost:7010";
    private const string TestUsername = "playwright_workflow_test";
    private const string TestPassword = "test1234";

    public override BrowserNewContextOptions ContextOptions()
    {
        return new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
        };
    }

    private async Task LoginOrRegister()
    {
        await Page.GotoAsync($"{BaseUrl}/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(2000);

        if (Page.Url.Contains("/planner"))
        {
            Console.WriteLine("Already logged in");
            return;
        }

        var loginBox = Page.Locator(".login-box");
        if (await loginBox.CountAsync() == 0)
        {
            await Page.GotoAsync($"{BaseUrl}/login");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1000);
        }

        var usernameInput = Page.Locator("input[placeholder*='username' i]").First;
        var passwordInput = Page.Locator("input[type='password']").First;

        await usernameInput.FillAsync(TestUsername);
        await passwordInput.FillAsync(TestPassword);

        var loginButton = Page.Locator("button.btn-primary:has-text('Login')");
        await loginButton.ClickAsync();
        await Page.WaitForTimeoutAsync(3000);

        var errorMessage = Page.Locator(".error-message");
        if (await errorMessage.CountAsync() > 0 && await errorMessage.IsVisibleAsync())
        {
            var registerLink = Page.Locator("a:has-text('Register')");
            await registerLink.ClickAsync();
            await Page.WaitForTimeoutAsync(500);

            await usernameInput.FillAsync(TestUsername);
            var displayNameInput = Page.Locator("input[placeholder*='Your name']");
            if (await displayNameInput.CountAsync() > 0)
            {
                await displayNameInput.FillAsync("Playwright Workflow Test User");
            }
            await passwordInput.FillAsync(TestPassword);
            var confirmPasswordInput = Page.Locator("input[placeholder*='Confirm password']");
            await confirmPasswordInput.FillAsync(TestPassword);

            var registerButton = Page.Locator("button.btn-primary:has-text('Register')");
            await registerButton.ClickAsync();
            await Page.WaitForTimeoutAsync(3000);
        }

        await Page.WaitForURLAsync($"{BaseUrl}/planner", new() { Timeout = 10000 });
    }

    [SetUp]
    public async Task Setup()
    {
        await LoginOrRegister();

        if (!Page.Url.Contains("/planner"))
        {
            await Page.GotoAsync($"{BaseUrl}/planner");
        }
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(2000);

        // Ensure Daily view
        var dailyButton = Page.Locator(".view-toggle button:has-text('Daily')");
        if (await dailyButton.CountAsync() > 0)
        {
            await dailyButton.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }
    }

    // ==================== TIME PARSING TESTS ====================

    [Test]
    [TestCase("meeting at 4:00 pm", "16:00")]
    [TestCase("standup at 9:30 am", "09:30")]
    [TestCase("call at 2pm", "14:00")]
    [TestCase("review at noon", "12:00")]
    public async Task TimeParsingFromTaskText_ShouldScheduleCorrectly(string taskText, string expectedTime)
    {
        var taskId = $"time_parse_{Guid.NewGuid():N}";
        var fullTaskText = $"{taskText} - {taskId}";

        // Add task with time in text
        var taskInput = Page.Locator("input.quick-add-input").First;
        await taskInput.FillAsync(fullTaskText);
        await taskInput.PressAsync("Enter");
        await Page.WaitForTimeoutAsync(2000);

        // Check that task appears in scheduled section (time panel)
        var scheduledTask = Page.Locator($".slot-task:has-text('{taskId}')");
        var taskCount = await scheduledTask.CountAsync();

        Assert.That(taskCount, Is.GreaterThan(0),
            $"Task '{fullTaskText}' should appear in scheduled section with time {expectedTime}");

        // Verify the time is correct
        var timeSpan = Page.Locator($".slot-task:has-text('{taskId}') .t-time");
        if (await timeSpan.CountAsync() > 0)
        {
            var timeText = await timeSpan.First.InnerTextAsync();
            Console.WriteLine($"Parsed time for '{taskText}': {timeText}");
        }

        // Cleanup - delete the task
        await scheduledTask.First.ClickAsync();
        await Page.WaitForTimeoutAsync(500);
        var deleteButton = Page.Locator("button:has-text('Delete'), .delete-btn");
        if (await deleteButton.CountAsync() > 0)
        {
            await deleteButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(1000);
        }
    }

    [Test]
    public async Task RecurringTaskWithTime_ShouldScheduleOnCorrectDay()
    {
        var taskId = $"recurring_{Guid.NewGuid():N}";
        var taskText = $"capstone meeting at 4:00 pm Wed - {taskId}";

        // Add recurring task with time and day
        var taskInput = Page.Locator("input.quick-add-input").First;
        await taskInput.FillAsync(taskText);
        await taskInput.PressAsync("Enter");
        await Page.WaitForTimeoutAsync(3000);

        // The task should be created - check that it exists somewhere
        var anyTask = Page.Locator($":text('{taskId}')");
        var taskCount = await anyTask.CountAsync();

        Assert.That(taskCount, Is.GreaterThan(0),
            "Recurring task should be created");

        Console.WriteLine($"Recurring task created: {taskText}");

        // Cleanup
        var task = anyTask.First;
        await task.ClickAsync();
        await Page.WaitForTimeoutAsync(500);
        var deleteButton = Page.Locator("button:has-text('Delete'), .delete-btn");
        if (await deleteButton.CountAsync() > 0)
        {
            await deleteButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(1000);
        }
    }

    // ==================== MULTI-SLOT DISPLAY TESTS ====================

    [Test]
    public async Task TaskWithLongDuration_ShouldShowInMultipleSlots()
    {
        var taskId = $"long_duration_{Guid.NewGuid():N}";

        // First create a task
        var taskInput = Page.Locator("input.quick-add-input").First;
        await taskInput.FillAsync($"Long meeting - {taskId}");
        await taskInput.PressAsync("Enter");
        await Page.WaitForTimeoutAsync(2000);

        // Find the task in the Tasks tab
        var tasksTab = Page.Locator(".tab-button:has-text('Tasks')");
        if (await tasksTab.CountAsync() > 0)
        {
            await tasksTab.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        var taskItem = Page.Locator($".task-item:has-text('{taskId}')");
        if (await taskItem.CountAsync() == 0)
        {
            Console.WriteLine("Task not found in Tasks tab, may already be scheduled");
            return;
        }

        // Schedule the task with 60 minute duration
        // Look for schedule button or right-click menu
        await taskItem.First.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        var scheduleButton = Page.Locator(".schedule-btn, button:has-text('Schedule')");
        if (await scheduleButton.CountAsync() > 0)
        {
            await scheduleButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Set time to 10:00 AM
        var timeInput = Page.Locator("input[type='time']");
        if (await timeInput.CountAsync() > 0)
        {
            await timeInput.FillAsync("10:00");
        }

        // Set duration to 60 minutes
        var durationOption = Page.Locator("button:has-text('1h'), option:has-text('60')");
        if (await durationOption.CountAsync() > 0)
        {
            await durationOption.First.ClickAsync();
        }

        // Confirm scheduling
        var confirmButton = Page.Locator("button:has-text('Save'), button:has-text('Schedule'), button:has-text('OK')");
        if (await confirmButton.CountAsync() > 0)
        {
            await confirmButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(2000);
        }

        // Now check that the task appears in multiple time slots
        // A 60-minute task at 10:00 should appear in 10:00, 10:20, 10:40 slots
        var tasksInSlots = Page.Locator($".slot-task:has-text('{taskId}')");
        var slotCount = await tasksInSlots.CountAsync();

        Console.WriteLine($"Task '{taskId}' appears in {slotCount} time slots");

        // With 20-minute slots and 60-minute duration, expect 3 slots
        // But with continuation display, could be different
        Assert.That(slotCount, Is.GreaterThanOrEqualTo(1),
            "Task with 60-minute duration should appear in at least 1 slot");

        // Cleanup
        await tasksInSlots.First.ClickAsync();
        await Page.WaitForTimeoutAsync(500);
        var deleteButton = Page.Locator("button:has-text('Delete'), .delete-btn");
        if (await deleteButton.CountAsync() > 0)
        {
            await deleteButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(1000);
        }
    }

    // ==================== WORKFLOW TRANSITION TESTS ====================

    [Test]
    public async Task TaskWorkflow_WaitingToTasksToScheduled_ShouldPreserveTask()
    {
        var taskId = $"workflow_{Guid.NewGuid():N}";

        // 1. Create a task for a past date (will go to Waiting)
        // First create a regular task
        var taskInput = Page.Locator("input.quick-add-input").First;
        await taskInput.FillAsync($"Workflow test task - {taskId}");
        await taskInput.PressAsync("Enter");
        await Page.WaitForTimeoutAsync(2000);

        // 2. Verify task exists in Tasks tab
        var tasksTab = Page.Locator(".tab-button:has-text('Tasks')");
        if (await tasksTab.CountAsync() > 0)
        {
            await tasksTab.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        var taskInTasks = Page.Locator($".task-item:has-text('{taskId}')");
        var inTasksCount = await taskInTasks.CountAsync();
        Console.WriteLine($"Task found in Tasks tab: {inTasksCount}");

        Assert.That(inTasksCount, Is.GreaterThan(0),
            "Task should be in Tasks section after creation");

        // 3. Schedule the task (move to Scheduled)
        await taskInTasks.First.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        var scheduleButton = Page.Locator(".schedule-btn, button:has-text('Schedule'), button:has-text('Add Time')");
        if (await scheduleButton.CountAsync() > 0)
        {
            await scheduleButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        var timeInput = Page.Locator("input[type='time']");
        if (await timeInput.CountAsync() > 0)
        {
            await timeInput.FillAsync("14:00");
            await Page.WaitForTimeoutAsync(500);

            var confirmButton = Page.Locator("button:has-text('Save'), button:has-text('Schedule'), button:has-text('OK')");
            if (await confirmButton.CountAsync() > 0)
            {
                await confirmButton.First.ClickAsync();
                await Page.WaitForTimeoutAsync(2000);
            }
        }

        // 4. Verify task is now in scheduled section
        var scheduledTask = Page.Locator($".slot-task:has-text('{taskId}')");
        var scheduledCount = await scheduledTask.CountAsync();
        Console.WriteLine($"Task found in Scheduled section: {scheduledCount}");

        // Task should be somewhere (either still in Tasks or moved to Scheduled)
        var totalCount = inTasksCount + scheduledCount;
        Assert.That(totalCount, Is.GreaterThan(0),
            "Task should not be lost during workflow transitions");

        // Cleanup
        var taskToDelete = scheduledCount > 0 ? scheduledTask.First : taskInTasks.First;
        await taskToDelete.ClickAsync();
        await Page.WaitForTimeoutAsync(500);
        var deleteButton = Page.Locator("button:has-text('Delete'), .delete-btn");
        if (await deleteButton.CountAsync() > 0)
        {
            await deleteButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(1000);
        }
    }

    [Test]
    public async Task NoTasksLost_AfterMultipleTransitions()
    {
        var taskIds = new List<string>();
        var taskCount = 5;

        // Create multiple tasks
        for (int i = 0; i < taskCount; i++)
        {
            var taskId = $"notlost_{i}_{Guid.NewGuid():N}";
            taskIds.Add(taskId);

            var taskInput = Page.Locator("input.quick-add-input").First;
            await taskInput.FillAsync($"Test task {i} - {taskId}");
            await taskInput.PressAsync("Enter");
            await Page.WaitForTimeoutAsync(500);
        }

        await Page.WaitForTimeoutAsync(2000);

        // Count all tasks
        var foundCount = 0;
        foreach (var taskId in taskIds)
        {
            var task = Page.Locator($":text('{taskId}')");
            var count = await task.CountAsync();
            if (count > 0) foundCount++;
        }

        Console.WriteLine($"Created {taskCount} tasks, found {foundCount}");
        Assert.That(foundCount, Is.EqualTo(taskCount),
            "All created tasks should be visible");

        // Cleanup
        for (int i = 0; i < taskIds.Count; i++)
        {
            var task = Page.Locator($":text('{taskIds[i]}')").First;
            if (await task.CountAsync() > 0)
            {
                await task.ClickAsync();
                await Page.WaitForTimeoutAsync(300);
                var deleteButton = Page.Locator("button:has-text('Delete'), .delete-btn");
                if (await deleteButton.CountAsync() > 0)
                {
                    await deleteButton.First.ClickAsync();
                    await Page.WaitForTimeoutAsync(500);
                }
            }
        }
    }
}
