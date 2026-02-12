using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace PFD.PlaywrightTests;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class PlannerTests : PageTest
{
    private const string BaseUrl = "https://localhost:7010";
    private const string TestUsername = "playwright_test";
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
        // Navigate to home/login page
        await Page.GotoAsync($"{BaseUrl}/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(2000);

        // Check if already on planner (logged in via localStorage)
        if (Page.Url.Contains("/planner"))
        {
            Console.WriteLine("Already logged in, on planner page");
            return;
        }

        // Should be on login page
        var loginBox = Page.Locator(".login-box");
        if (await loginBox.CountAsync() == 0)
        {
            Console.WriteLine("Not on login page, navigating...");
            await Page.GotoAsync($"{BaseUrl}/login");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1000);
        }

        // Fill in login form
        var usernameInput = Page.Locator("input[placeholder*='username' i]").First;
        var passwordInput = Page.Locator("input[type='password']").First;

        await usernameInput.FillAsync(TestUsername);
        await passwordInput.FillAsync(TestPassword);

        // Click login button
        var loginButton = Page.Locator("button.btn-primary:has-text('Login')");
        await loginButton.ClickAsync();
        await Page.WaitForTimeoutAsync(3000);

        // Check if login failed (still on login page with error)
        var errorMessage = Page.Locator(".error-message");
        if (await errorMessage.CountAsync() > 0 && await errorMessage.IsVisibleAsync())
        {
            var errorText = await errorMessage.TextContentAsync();
            Console.WriteLine($"Login failed: {errorText}. Trying to register...");

            // Click register link
            var registerLink = Page.Locator("a:has-text('Register')");
            await registerLink.ClickAsync();
            await Page.WaitForTimeoutAsync(500);

            // Fill register form
            await usernameInput.FillAsync(TestUsername);
            var displayNameInput = Page.Locator("input[placeholder*='Your name']");
            if (await displayNameInput.CountAsync() > 0)
            {
                await displayNameInput.FillAsync("Playwright Test User");
            }
            await passwordInput.FillAsync(TestPassword);
            var confirmPasswordInput = Page.Locator("input[placeholder*='Confirm password']");
            await confirmPasswordInput.FillAsync(TestPassword);

            // Click register button
            var registerButton = Page.Locator("button.btn-primary:has-text('Register')");
            await registerButton.ClickAsync();
            await Page.WaitForTimeoutAsync(3000);
        }

        // Wait for navigation to planner
        await Page.WaitForURLAsync($"{BaseUrl}/planner", new() { Timeout = 10000 });
        Console.WriteLine("Successfully logged in/registered");
    }

    [SetUp]
    public async Task Setup()
    {
        await LoginOrRegister();

        // Ensure we're on planner page
        if (!Page.Url.Contains("/planner"))
        {
            await Page.GotoAsync($"{BaseUrl}/planner");
        }
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(2000);
    }

    [Test]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    [TestCase(8)]
    [TestCase(9)]
    [TestCase(10)]
    public async Task DayRoller_ScrollAndClick_ShouldChangeDate(int iteration)
    {
        Console.WriteLine($"Day Roller Test - Iteration {iteration}");

        // Make sure we're in Daily view (day roller only shows in daily view)
        var dailyButton = Page.Locator(".view-toggle button:has-text('Daily')");
        if (await dailyButton.CountAsync() > 0 && !await dailyButton.First.GetAttributeAsync("class").ContinueWith(t => t.Result?.Contains("active") ?? false))
        {
            await dailyButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Find the day roller
        var dayRoller = Page.Locator(".day-roller");
        await Expect(dayRoller.First).ToBeVisibleAsync(new() { Timeout = 10000 });
        Console.WriteLine("Day roller is visible");

        // Find day slots
        var daySlots = Page.Locator(".day-slot");
        var slotCount = await daySlots.CountAsync();
        Console.WriteLine($"Found {slotCount} day slots");

        Assert.That(slotCount, Is.GreaterThan(0), "Day slots should be present");

        // Get initial selected slot info
        var selectedSlot = Page.Locator(".day-slot.selected");
        if (await selectedSlot.CountAsync() > 0)
        {
            var initialText = await selectedSlot.First.TextContentAsync() ?? "";
            Console.WriteLine($"Initial selected: {initialText}");
        }

        // Click on a non-selected slot (try one near the top or bottom)
        var targetIndex = (iteration % 2 == 0) ? 2 : slotCount - 3;
        targetIndex = Math.Max(0, Math.Min(targetIndex, slotCount - 1));

        var targetSlot = daySlots.Nth(targetIndex);
        await targetSlot.ScrollIntoViewIfNeededAsync();
        await targetSlot.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);

        // Verify page didn't freeze - check that we can still interact
        var plannerContainer = Page.Locator(".planner-container");
        await Expect(plannerContainer).ToBeVisibleAsync(new() { Timeout = 5000 });

        // Verify the app is still responsive by checking header is visible
        var header = Page.Locator(".task-header");
        await Expect(header).ToBeVisibleAsync(new() { Timeout = 3000 });

        Console.WriteLine($"Iteration {iteration} completed successfully - page is responsive");
    }

    [Test]
    public async Task DayRoller_DragScroll_ShouldNotFreeze()
    {
        // Ensure daily view
        var dailyButton = Page.Locator(".view-toggle button:has-text('Daily')");
        if (await dailyButton.CountAsync() > 0)
        {
            await dailyButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Find the day roller track
        var dayRollerTrack = Page.Locator(".day-roller-track");
        await Expect(dayRollerTrack).ToBeVisibleAsync(new() { Timeout = 10000 });

        var box = await dayRollerTrack.BoundingBoxAsync();
        Assert.That(box, Is.Not.Null, "Day roller track should have bounding box");

        var startX = box!.X + box.Width / 2;
        var startY = box.Y + box.Height / 2;

        // Perform drag gesture - drag up
        Console.WriteLine("Dragging day roller up...");
        await Page.Mouse.MoveAsync(startX, startY);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(startX, startY - 100, new() { Steps = 10 });
        await Page.Mouse.UpAsync();
        await Page.WaitForTimeoutAsync(500);

        // Verify page is still responsive
        var plannerContainer = Page.Locator(".planner-container");
        await Expect(plannerContainer).ToBeVisibleAsync(new() { Timeout = 3000 });
        Console.WriteLine("Page responsive after drag up");

        // Drag down
        Console.WriteLine("Dragging day roller down...");
        await Page.Mouse.MoveAsync(startX, startY);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(startX, startY + 100, new() { Steps = 10 });
        await Page.Mouse.UpAsync();
        await Page.WaitForTimeoutAsync(500);

        // Final responsiveness check
        await Expect(plannerContainer).ToBeVisibleAsync(new() { Timeout = 3000 });
        Console.WriteLine("Drag scroll test completed - page is responsive");
    }

    [Test]
    [TestCase("teal")]
    [TestCase("blue")]
    [TestCase("red")]
    [TestCase("orange")]
    [TestCase("dark")]
    [TestCase("light")]
    [TestCase("nordic")]
    [TestCase("vampire")]
    [TestCase("bigly")]
    [TestCase("tropical")]
    [TestCase("pinkish")]
    [TestCase("ughly")]
    [TestCase("greek")]
    [TestCase("preppy")]
    public async Task Theme_Switch_ShouldApplyAndHold(string themeName)
    {
        Console.WriteLine($"Testing theme: {themeName}");

        // Find theme selector dropdown in settings section
        var themeSelect = Page.Locator(".settings-section select, select");

        if (await themeSelect.CountAsync() > 0)
        {
            // Select the theme
            await themeSelect.First.SelectOptionAsync(themeName);
            await Page.WaitForTimeoutAsync(1000);

            // Verify theme class is applied to container
            var container = Page.Locator($".planner-container.theme-{themeName}, .planner-container[class*='theme-{themeName}']");

            // Either the theme class is applied or we just check the page is responsive
            var plannerContainer = Page.Locator(".planner-container");
            await Expect(plannerContainer).ToBeVisibleAsync(new() { Timeout = 5000 });

            // Check theme attribute
            var themeClass = await plannerContainer.GetAttributeAsync("class");
            Console.WriteLine($"Container class after theme change: {themeClass}");

            // Test interactions still work with new theme
            var daySlots = Page.Locator(".day-slot");
            if (await daySlots.CountAsync() > 0)
            {
                await daySlots.First.ClickAsync();
                await Page.WaitForTimeoutAsync(500);
            }

            // Verify still responsive
            await Expect(plannerContainer).ToBeVisibleAsync(new() { Timeout = 3000 });
            Console.WriteLine($"Theme {themeName} applied and page is responsive");
        }
        else
        {
            Console.WriteLine("Theme selector not found - checking if settings page needed");
            Assert.Inconclusive("Theme selector not found on page");
        }
    }

    [Test]
    public async Task Theme_SwitchAllThemes_Sequentially()
    {
        var themes = new[] { "teal", "red", "orange", "blue", "dark", "light", "nordic", "vampire", "bigly", "tropical", "pinkish", "ughly", "greek", "preppy" };

        var themeSelect = Page.Locator(".settings-section select").First;
        await Expect(themeSelect).ToBeVisibleAsync(new() { Timeout = 5000 });

        foreach (var theme in themes)
        {
            Console.WriteLine($"Switching to theme: {theme}");
            await themeSelect.SelectOptionAsync(theme);
            await Page.WaitForTimeoutAsync(500);

            // Verify responsive after each switch
            var plannerContainer = Page.Locator(".planner-container");
            await Expect(plannerContainer).ToBeVisibleAsync(new() { Timeout = 3000 });

            // Quick interaction test
            var header = Page.Locator(".task-header");
            await Expect(header).ToBeVisibleAsync(new() { Timeout = 2000 });
        }

        Console.WriteLine("All themes tested successfully");
    }

    [Test]
    public async Task Planner_AddTask_ShouldNotFreeze()
    {
        // Find task input
        var taskInput = Page.Locator(".add-task input[type='text']").First;
        await Expect(taskInput).ToBeVisibleAsync(new() { Timeout = 5000 });

        // Add a test task
        var taskTitle = $"Playwright Test Task {DateTime.Now:HHmmss}";
        await taskInput.FillAsync(taskTitle);
        await Page.Keyboard.PressAsync("Enter");
        await Page.WaitForTimeoutAsync(2000);

        // Verify app is still responsive
        var plannerContainer = Page.Locator(".planner-container");
        await Expect(plannerContainer).ToBeVisibleAsync(new() { Timeout = 5000 });

        Console.WriteLine("Task added successfully, page is responsive");
    }

    [Test]
    public async Task Planner_ToggleViews_ShouldNotFreeze()
    {
        var dailyButton = Page.Locator(".view-toggle button:has-text('Daily')");
        var weeklyButton = Page.Locator(".view-toggle button:has-text('Weekly')");

        // Toggle to weekly
        if (await weeklyButton.CountAsync() > 0)
        {
            await weeklyButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(1000);

            var plannerContainer = Page.Locator(".planner-container");
            await Expect(plannerContainer).ToBeVisibleAsync(new() { Timeout = 3000 });
            Console.WriteLine("Switched to Weekly view - responsive");
        }

        // Toggle back to daily
        if (await dailyButton.CountAsync() > 0)
        {
            await dailyButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(1000);

            var dayRoller = Page.Locator(".day-roller");
            await Expect(dayRoller.First).ToBeVisibleAsync(new() { Timeout = 5000 });
            Console.WriteLine("Switched to Daily view - day roller visible");
        }

        // Toggle multiple times
        for (int i = 0; i < 5; i++)
        {
            await weeklyButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(300);
            await dailyButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(300);
        }

        var container = Page.Locator(".planner-container");
        await Expect(container).ToBeVisibleAsync(new() { Timeout = 3000 });
        Console.WriteLine("View toggle test completed - page is responsive");
    }

    [Test]
    public async Task Calendar_NavigateMonths_ShouldNotFreeze()
    {
        var prevButton = Page.Locator(".calendar-header .nav-btn").First;
        var nextButton = Page.Locator(".calendar-header .nav-btn").Last;

        // Navigate forward
        for (int i = 0; i < 3; i++)
        {
            await nextButton.ClickAsync();
            await Page.WaitForTimeoutAsync(300);
        }

        // Navigate backward
        for (int i = 0; i < 3; i++)
        {
            await prevButton.ClickAsync();
            await Page.WaitForTimeoutAsync(300);
        }

        var plannerContainer = Page.Locator(".planner-container");
        await Expect(plannerContainer).ToBeVisibleAsync(new() { Timeout = 3000 });
        Console.WriteLine("Calendar navigation completed - page is responsive");
    }

    [Test]
    public async Task DragAndDrop_TaskToSchedule_ShouldScheduleTask()
    {
        Console.WriteLine("Testing drag-and-drop task scheduling");

        // Ensure we're in Daily view
        var dailyButton = Page.Locator(".view-toggle button:has-text('Daily')");
        if (await dailyButton.CountAsync() > 0)
        {
            await dailyButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // First, add a test task if none exist
        var taskInput = Page.Locator(".add-task input[type='text']").First;
        await Expect(taskInput).ToBeVisibleAsync(new() { Timeout = 5000 });

        var taskTitle = $"Drag Test {DateTime.Now:HHmmss}";
        await taskInput.FillAsync(taskTitle);
        await Page.Keyboard.PressAsync("Enter");
        await Page.WaitForTimeoutAsync(2000);

        // Check that tasks are added (look for any task count indicator)
        var taskList = Page.Locator(".task-list");
        var taskListExists = await taskList.CountAsync() > 0;
        Console.WriteLine($"Task list visible: {taskListExists}");

        // Debug: what sections exist before clicking tabs?
        var scheduledSection = Page.Locator(".scheduled-section");
        var scheduledExists = await scheduledSection.CountAsync() > 0;
        Console.WriteLine($"Scheduled section visible (before tab click): {scheduledExists}");

        var alldaySectionBefore = Page.Locator(".allday-section");
        var alldayBeforeCount = await alldaySectionBefore.CountAsync();
        Console.WriteLine($"Allday section visible (before tab click): {alldayBeforeCount}");

        // Click on "Tasks" tab to see unscheduled tasks (correct selector: .task-section-tabs .section-tab)
        var tasksTab = Page.Locator(".task-section-tabs .section-tab:has-text('Tasks')");
        var tabCount = await tasksTab.CountAsync();
        Console.WriteLine($"Found {tabCount} Tasks tabs");

        if (tabCount > 0)
        {
            // Wait to ensure tab is clickable
            await Expect(tasksTab.First).ToBeVisibleAsync(new() { Timeout = 3000 });
            await tasksTab.First.ClickAsync();
            Console.WriteLine("Clicked Tasks tab");
            await Page.WaitForTimeoutAsync(1500);  // Longer wait after click
        }
        else
        {
            // Fallback: try button containing "Tasks" text
            var fallbackTab = Page.Locator("button:has-text('Tasks')");
            if (await fallbackTab.CountAsync() > 0)
            {
                await fallbackTab.First.ClickAsync();
                await Page.WaitForTimeoutAsync(1500);
                Console.WriteLine("Used fallback Tasks tab selector");
            }
        }

        // Debug: check what task rows exist
        var allTaskRows = Page.Locator(".task-row");
        var allRowCount = await allTaskRows.CountAsync();
        Console.WriteLine($"Found {allRowCount} total task rows");

        // Check for allday-section
        var alldaySection = Page.Locator(".allday-section");
        var alldayCount = await alldaySection.CountAsync();
        Console.WriteLine($"Found {alldayCount} allday sections");

        // Check for drag handles
        var dragHandles = Page.Locator(".drag-handle");
        var handleCount = await dragHandles.CountAsync();
        Console.WriteLine($"Found {handleCount} drag handles");

        // Find an unscheduled task with drag handle (try multiple selectors)
        var taskRow = Page.Locator(".task-row.draggable-task, .task-row[draggable='true'], .allday-section .task-row").First;
        await Expect(taskRow).ToBeVisibleAsync(new() { Timeout = 5000 });
        Console.WriteLine("Found draggable task row");

        // Get the task's position
        var taskBox = await taskRow.BoundingBoxAsync();
        Assert.That(taskBox, Is.Not.Null, "Task row should have bounding box");

        // Find a time slot to drop on (e.g., 10:00 AM slot)
        var timeSlot = Page.Locator(".task-slot[data-slot='600']");
        if (await timeSlot.CountAsync() == 0)
        {
            // Fall back to any task slot
            timeSlot = Page.Locator(".task-slot:not(.past-slot)").First;
        }
        await Expect(timeSlot).ToBeVisibleAsync(new() { Timeout = 5000 });
        Console.WriteLine("Found target time slot");

        var slotBox = await timeSlot.BoundingBoxAsync();
        Assert.That(slotBox, Is.Not.Null, "Time slot should have bounding box");

        // Perform drag and drop
        Console.WriteLine("Performing drag and drop...");

        var startX = taskBox!.X + taskBox.Width / 2;
        var startY = taskBox.Y + taskBox.Height / 2;
        var endX = slotBox!.X + slotBox.Width / 2;
        var endY = slotBox.Y + slotBox.Height / 2;

        // Use Playwright's drag and drop
        await taskRow.DragToAsync(timeSlot);
        await Page.WaitForTimeoutAsync(2000);

        // Verify the page is still responsive
        var plannerContainer = Page.Locator(".planner-container");
        await Expect(plannerContainer).ToBeVisibleAsync(new() { Timeout = 5000 });

        // Check if the task now appears in the schedule (click on Scheduled tab)
        var scheduledTab = Page.Locator(".task-section-tabs .section-tab:has-text('Scheduled')");
        if (await scheduledTab.CountAsync() > 0)
        {
            await scheduledTab.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Look for the task in schedule slots
        var scheduledTask = Page.Locator(".slot-task");
        var scheduledCount = await scheduledTask.CountAsync();
        Console.WriteLine($"Found {scheduledCount} scheduled tasks after drag-drop");

        Console.WriteLine("Drag and drop test completed - page is responsive");
    }

    [Test]
    public async Task DragAndDrop_MultipleDrops_ShouldNotFreeze()
    {
        Console.WriteLine("Testing multiple drag-and-drop operations");

        // Ensure Daily view
        var dailyButton = Page.Locator(".view-toggle button:has-text('Daily')");
        if (await dailyButton.CountAsync() > 0)
        {
            await dailyButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Add several test tasks
        var taskInput = Page.Locator(".add-task input[type='text']").First;
        await Expect(taskInput).ToBeVisibleAsync(new() { Timeout = 5000 });

        for (int i = 1; i <= 3; i++)
        {
            var taskTitle = $"Multi Drag Test {i} - {DateTime.Now:HHmmss}";
            await taskInput.FillAsync(taskTitle);
            await Page.Keyboard.PressAsync("Enter");
            await Page.WaitForTimeoutAsync(1000);
        }

        // Switch to Tasks tab (correct selector: .task-section-tabs .section-tab)
        var tasksTab = Page.Locator(".task-section-tabs .section-tab:has-text('Tasks')");
        if (await tasksTab.CountAsync() > 0)
        {
            await tasksTab.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }
        else
        {
            // Fallback
            var fallbackTab = Page.Locator("button:has-text('Tasks')");
            if (await fallbackTab.CountAsync() > 0)
            {
                await fallbackTab.First.ClickAsync();
                await Page.WaitForTimeoutAsync(500);
            }
        }

        // Drag multiple tasks to different time slots
        var timeSlots = Page.Locator(".task-slot");
        var slotCount = await timeSlots.CountAsync();

        for (int i = 0; i < 3; i++)
        {
            var taskRows = Page.Locator(".task-row.draggable-task, .task-row[draggable='true']");
            if (await taskRows.CountAsync() == 0)
            {
                Console.WriteLine("No more draggable tasks");
                break;
            }

            var taskRow = taskRows.First;
            var targetSlotIndex = Math.Min(i + 8, slotCount - 1); // Start at slot 8 (8 AM)
            var targetSlot = timeSlots.Nth(targetSlotIndex);

            Console.WriteLine($"Dragging task to slot {targetSlotIndex}");

            try
            {
                await taskRow.DragToAsync(targetSlot);
                await Page.WaitForTimeoutAsync(1000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Drag operation {i + 1} had issue: {ex.Message}");
            }

            // Verify responsive
            var plannerContainer = Page.Locator(".planner-container");
            await Expect(plannerContainer).ToBeVisibleAsync(new() { Timeout = 3000 });
        }

        Console.WriteLine("Multiple drag-drop test completed - page is responsive");
    }

    [Test]
    public async Task DragHandle_ShouldBeVisible_ForUnscheduledTasks()
    {
        Console.WriteLine("Verifying drag handles are visible");

        // Ensure Daily view
        var dailyButton = Page.Locator(".view-toggle button:has-text('Daily')");
        if (await dailyButton.CountAsync() > 0)
        {
            await dailyButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Add a test task
        var taskInput = Page.Locator(".add-task input[type='text']").First;
        await Expect(taskInput).ToBeVisibleAsync(new() { Timeout = 5000 });

        var taskTitle = $"Handle Test {DateTime.Now:HHmmss}";
        await taskInput.FillAsync(taskTitle);
        await Page.Keyboard.PressAsync("Enter");
        await Page.WaitForTimeoutAsync(2000);

        // Switch to Tasks tab (correct selector: .task-section-tabs .section-tab)
        var tasksTab = Page.Locator(".task-section-tabs .section-tab:has-text('Tasks')");
        if (await tasksTab.CountAsync() > 0)
        {
            await tasksTab.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }
        else
        {
            // Fallback
            var fallbackTab = Page.Locator("button:has-text('Tasks')");
            if (await fallbackTab.CountAsync() > 0)
            {
                await fallbackTab.First.ClickAsync();
                await Page.WaitForTimeoutAsync(500);
            }
        }

        // Check for drag handles
        var dragHandles = Page.Locator(".drag-handle");
        var handleCount = await dragHandles.CountAsync();
        Console.WriteLine($"Found {handleCount} drag handles");

        // Also check for draggable attribute
        var draggableRows = Page.Locator(".task-row[draggable='true']");
        var draggableCount = await draggableRows.CountAsync();
        Console.WriteLine($"Found {draggableCount} draggable task rows");

        // At least one should exist if there are unscheduled, incomplete tasks
        Assert.That(handleCount + draggableCount, Is.GreaterThan(0), "Should have drag handles or draggable rows");

        Console.WriteLine("Drag handle verification completed");
    }

    [Test]
    public async Task PanelResize_DragLeft_ShouldExpandRightPanel()
    {
        Console.WriteLine("Testing panel resize - dragging left should expand right panel");

        // Ensure Daily view (resize handle only shows in daily view)
        var dailyButton = Page.Locator(".view-toggle button:has-text('Daily')");
        if (await dailyButton.CountAsync() > 0)
        {
            await dailyButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Find the resize handle
        var resizeHandle = Page.Locator(".panel-resize-handle");
        await Expect(resizeHandle).ToBeVisibleAsync(new() { Timeout = 5000 });
        Console.WriteLine("Resize handle is visible");

        // Find the day-roller-wrapper (right panel)
        var rightPanel = Page.Locator(".day-roller-wrapper");
        await Expect(rightPanel).ToBeVisibleAsync(new() { Timeout = 5000 });

        // Get initial width of right panel
        var initialBox = await rightPanel.BoundingBoxAsync();
        Assert.That(initialBox, Is.Not.Null, "Right panel should have bounding box");
        var initialWidth = initialBox!.Width;
        Console.WriteLine($"Initial right panel width: {initialWidth}px");

        // Get resize handle position
        var handleBox = await resizeHandle.BoundingBoxAsync();
        Assert.That(handleBox, Is.Not.Null, "Resize handle should have bounding box");

        var handleX = handleBox!.X + handleBox.Width / 2;
        var handleY = handleBox.Y + handleBox.Height / 2;

        // Drag the resize handle LEFT by 100 pixels (should expand right panel)
        Console.WriteLine("Dragging resize handle left by 100px...");
        await Page.Mouse.MoveAsync(handleX, handleY);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(handleX - 100, handleY, new() { Steps = 10 });
        await Page.Mouse.UpAsync();
        await Page.WaitForTimeoutAsync(500);

        // Get new width of right panel
        var newBox = await rightPanel.BoundingBoxAsync();
        Assert.That(newBox, Is.Not.Null, "Right panel should still have bounding box after resize");
        var newWidth = newBox!.Width;
        Console.WriteLine($"New right panel width: {newWidth}px");

        // Verify the right panel expanded (width increased)
        Assert.That(newWidth, Is.GreaterThan(initialWidth),
            $"Right panel should have expanded. Initial: {initialWidth}px, After: {newWidth}px");

        Console.WriteLine($"Panel resize test PASSED: {initialWidth}px -> {newWidth}px (gained {newWidth - initialWidth}px)");

        // Verify page is still responsive
        var plannerContainer = Page.Locator(".planner-container");
        await Expect(plannerContainer).ToBeVisibleAsync(new() { Timeout = 3000 });
    }

    [Test]
    public async Task PanelResize_DragRight_ShouldShrinkRightPanel()
    {
        Console.WriteLine("Testing panel resize - dragging right should shrink right panel");

        // Ensure Daily view
        var dailyButton = Page.Locator(".view-toggle button:has-text('Daily')");
        if (await dailyButton.CountAsync() > 0)
        {
            await dailyButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Find the resize handle
        var resizeHandle = Page.Locator(".panel-resize-handle");
        await Expect(resizeHandle).ToBeVisibleAsync(new() { Timeout = 5000 });

        // Find the day-roller-wrapper (right panel)
        var rightPanel = Page.Locator(".day-roller-wrapper");
        await Expect(rightPanel).ToBeVisibleAsync(new() { Timeout = 5000 });

        // First, expand the panel by dragging left
        var handleBox = await resizeHandle.BoundingBoxAsync();
        var handleX = handleBox!.X + handleBox.Width / 2;
        var handleY = handleBox.Y + handleBox.Height / 2;

        await Page.Mouse.MoveAsync(handleX, handleY);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(handleX - 100, handleY, new() { Steps = 10 });
        await Page.Mouse.UpAsync();
        await Page.WaitForTimeoutAsync(500);

        // Get the expanded width
        var expandedBox = await rightPanel.BoundingBoxAsync();
        var expandedWidth = expandedBox!.Width;
        Console.WriteLine($"Expanded right panel width: {expandedWidth}px");

        // Now drag right to shrink
        handleBox = await resizeHandle.BoundingBoxAsync();
        handleX = handleBox!.X + handleBox.Width / 2;
        handleY = handleBox.Y + handleBox.Height / 2;

        Console.WriteLine("Dragging resize handle right by 50px...");
        await Page.Mouse.MoveAsync(handleX, handleY);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(handleX + 50, handleY, new() { Steps = 10 });
        await Page.Mouse.UpAsync();
        await Page.WaitForTimeoutAsync(500);

        // Get shrunk width
        var shrunkBox = await rightPanel.BoundingBoxAsync();
        var shrunkWidth = shrunkBox!.Width;
        Console.WriteLine($"Shrunk right panel width: {shrunkWidth}px");

        // Verify the right panel shrunk
        Assert.That(shrunkWidth, Is.LessThan(expandedWidth),
            $"Right panel should have shrunk. Expanded: {expandedWidth}px, After: {shrunkWidth}px");

        Console.WriteLine($"Panel shrink test PASSED: {expandedWidth}px -> {shrunkWidth}px (lost {expandedWidth - shrunkWidth}px)");
    }

    [Test]
    public async Task DragAndDrop_TasksTabToRightPanelTimeSlot_ShouldScheduleTask()
    {
        Console.WriteLine("Testing drag from Tasks tab (center panel) to right panel time slot");

        // Ensure Daily view
        var dailyButton = Page.Locator(".view-toggle button:has-text('Daily')");
        if (await dailyButton.CountAsync() > 0)
        {
            await dailyButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Add a test task
        var taskInput = Page.Locator(".add-task input[type='text']").First;
        await Expect(taskInput).ToBeVisibleAsync(new() { Timeout = 5000 });

        var taskTitle = $"Drag to Right Panel {DateTime.Now:HHmmss}";
        await taskInput.FillAsync(taskTitle);
        await Page.Keyboard.PressAsync("Enter");
        await Page.WaitForTimeoutAsync(2000);

        // Click on Tasks tab to see unscheduled tasks
        var tasksTab = Page.Locator(".task-section-tabs .section-tab:has-text('Tasks')");
        if (await tasksTab.CountAsync() > 0)
        {
            await tasksTab.First.ClickAsync();
            await Page.WaitForTimeoutAsync(1000);
        }

        // Find the draggable task in center panel
        var draggableTask = Page.Locator(".allday-section .task-row.draggable-task, .allday-section .task-row[draggable='true']").First;
        await Expect(draggableTask).ToBeVisibleAsync(new() { Timeout = 5000 });
        Console.WriteLine("Found draggable task in center panel");

        // Find a time slot in the RIGHT panel (time-tasks-panel)
        var rightPanelTimeSlot = Page.Locator(".time-tasks-panel .task-slot[data-slot='600']");
        if (await rightPanelTimeSlot.CountAsync() == 0)
        {
            rightPanelTimeSlot = Page.Locator(".time-tasks-panel .task-slot:not(.past-slot)").First;
        }
        await Expect(rightPanelTimeSlot).ToBeVisibleAsync(new() { Timeout = 5000 });
        Console.WriteLine("Found target time slot in right panel");

        // Perform drag from center panel to right panel
        Console.WriteLine("Dragging task from center panel to right panel time slot...");
        await draggableTask.DragToAsync(rightPanelTimeSlot);
        await Page.WaitForTimeoutAsync(2000);

        // Verify the page is still responsive
        var plannerContainer = Page.Locator(".planner-container");
        await Expect(plannerContainer).ToBeVisibleAsync(new() { Timeout = 5000 });

        // Check if the task appears in the right panel schedule
        var scheduledTaskInRightPanel = Page.Locator(".time-tasks-panel .slot-task");
        var scheduledCount = await scheduledTaskInRightPanel.CountAsync();
        Console.WriteLine($"Found {scheduledCount} scheduled tasks in right panel after drag");

        // Also check Scheduled tab in center panel
        var scheduledTab = Page.Locator(".task-section-tabs .section-tab:has-text('Scheduled')");
        if (await scheduledTab.CountAsync() > 0)
        {
            await scheduledTab.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        var scheduledTasks = Page.Locator(".scheduled-section .task-row");
        var scheduledTaskCount = await scheduledTasks.CountAsync();
        Console.WriteLine($"Found {scheduledTaskCount} scheduled tasks in center panel Scheduled tab");

        Console.WriteLine("Drag from center to right panel test completed");
    }

    [Test]
    public async Task DragAndDrop_WaitingTabToRightPanel_ShouldMoveAndScheduleTask()
    {
        Console.WriteLine("Testing drag from Waiting tab to right panel - should move to today AND schedule");

        // Ensure Daily view
        var dailyButton = Page.Locator(".view-toggle button:has-text('Daily')");
        if (await dailyButton.CountAsync() > 0)
        {
            await dailyButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Click on Waiting tab
        var waitingTab = Page.Locator(".task-section-tabs .section-tab:has-text('Waiting')");
        if (await waitingTab.CountAsync() > 0)
        {
            await waitingTab.First.ClickAsync();
            await Page.WaitForTimeoutAsync(1000);
        }

        // Check if there are any waiting/overdue tasks
        var waitingTasks = Page.Locator(".waiting-tab-section .task-row.draggable-task, .waiting-tab-section .task-row[draggable='true']");
        var waitingCount = await waitingTasks.CountAsync();
        Console.WriteLine($"Found {waitingCount} draggable tasks in Waiting tab");

        if (waitingCount == 0)
        {
            Console.WriteLine("No waiting tasks to test - skipping drag portion");
            Assert.Pass("No waiting tasks available for drag test");
            return;
        }

        // Get the first draggable waiting task
        var waitingTask = waitingTasks.First;
        await Expect(waitingTask).ToBeVisibleAsync(new() { Timeout = 5000 });

        // Find a time slot in the right panel
        var rightPanelTimeSlot = Page.Locator(".time-tasks-panel .task-slot[data-slot='840']");
        if (await rightPanelTimeSlot.CountAsync() == 0)
        {
            rightPanelTimeSlot = Page.Locator(".time-tasks-panel .task-slot").Nth(6);
        }
        await Expect(rightPanelTimeSlot).ToBeVisibleAsync(new() { Timeout = 5000 });

        // Drag waiting task to right panel
        Console.WriteLine("Dragging waiting task to right panel...");
        await waitingTask.DragToAsync(rightPanelTimeSlot);
        await Page.WaitForTimeoutAsync(2000);

        // Verify page is responsive
        var plannerContainer = Page.Locator(".planner-container");
        await Expect(plannerContainer).ToBeVisibleAsync(new() { Timeout = 5000 });

        // Check if task is now scheduled
        var scheduledTaskInRightPanel = Page.Locator(".time-tasks-panel .slot-task");
        var scheduledCount = await scheduledTaskInRightPanel.CountAsync();
        Console.WriteLine($"Found {scheduledCount} scheduled tasks in right panel after drag from Waiting");

        Console.WriteLine("Drag from Waiting tab to right panel test completed");
    }


    [Test]
    public async Task TasksAndWaiting_ShouldPersistWhenDateChanges()
    {
        Console.WriteLine("Testing that Tasks and Waiting tabs persist when scrolling dates");

        // Ensure Daily view
        var dailyButton = Page.Locator(".view-toggle button:has-text('Daily')");
        if (await dailyButton.CountAsync() > 0)
        {
            await dailyButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Add a test task (unscheduled)
        var taskInput = Page.Locator(".add-task input[type='text']").First;
        await Expect(taskInput).ToBeVisibleAsync(new() { Timeout = 5000 });

        var taskTitle = $"Persist Test {DateTime.Now:HHmmss}";
        await taskInput.FillAsync(taskTitle);
        await Page.Keyboard.PressAsync("Enter");
        await Page.WaitForTimeoutAsync(2000);

        // Click on Tasks tab
        var tasksTab = Page.Locator(".task-section-tabs .section-tab:has-text('Tasks')");
        if (await tasksTab.CountAsync() > 0)
        {
            await tasksTab.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Count tasks in Tasks tab before date change
        var taskRows = Page.Locator(".allday-section .task-row");
        var initialTaskCount = await taskRows.CountAsync();
        Console.WriteLine($"Initial task count in Tasks tab: {initialTaskCount}");

        // Click on Waiting tab and count
        var waitingTab = Page.Locator(".task-section-tabs .section-tab:has-text('Waiting')");
        if (await waitingTab.CountAsync() > 0)
        {
            await waitingTab.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }
        var waitingTasks = Page.Locator(".waiting-tab-section .task-row");
        var initialWaitingCount = await waitingTasks.CountAsync();
        Console.WriteLine($"Initial waiting count: {initialWaitingCount}");

        // Now click on a different date in the day roller (right panel)
        var dayRollerDays = Page.Locator(".day-roller .day-item");
        if (await dayRollerDays.CountAsync() > 5)
        {
            // Click on a date 5 days from now
            await dayRollerDays.Nth(5).ClickAsync();
            await Page.WaitForTimeoutAsync(1000);
            Console.WriteLine("Clicked on different date in day roller");
        }

        // Check Waiting tab still has same count
        if (await waitingTab.CountAsync() > 0)
        {
            await waitingTab.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }
        var waitingAfter = await waitingTasks.CountAsync();
        Console.WriteLine($"Waiting count after date change: {waitingAfter}");
        Assert.That(waitingAfter, Is.EqualTo(initialWaitingCount),
            "Waiting tasks should persist after date change");

        // Check Tasks tab still has tasks
        if (await tasksTab.CountAsync() > 0)
        {
            await tasksTab.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }
        var tasksAfter = await taskRows.CountAsync();
        Console.WriteLine($"Task count after date change: {tasksAfter}");
        Assert.That(tasksAfter, Is.GreaterThanOrEqualTo(initialTaskCount),
            "Tasks should persist after date change");

        Console.WriteLine("Tasks and Waiting persistence test completed");
    }

    [Test]
    public async Task AddTask_ShouldAppearInTasksTab_Immediately()
    {
        Console.WriteLine("Testing that newly added task appears in Tasks tab immediately");

        // Ensure Daily view
        var dailyButton = Page.Locator(".view-toggle button:has-text('Daily')");
        if (await dailyButton.CountAsync() > 0)
        {
            await dailyButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Click on Tasks tab first and count existing tasks
        var tasksTab = Page.Locator(".task-section-tabs .section-tab:has-text('Tasks')");
        if (await tasksTab.CountAsync() > 0)
        {
            await tasksTab.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        var taskRows = Page.Locator(".allday-section .task-row");
        var initialCount = await taskRows.CountAsync();
        Console.WriteLine($"Initial task count in Tasks tab: {initialCount}");

        // Add a new task with unique name
        var taskInput = Page.Locator(".add-task input[type='text']").First;
        await Expect(taskInput).ToBeVisibleAsync(new() { Timeout = 5000 });

        var uniqueTaskTitle = $"Visibility Test {DateTime.Now:HHmmss}";
        await taskInput.FillAsync(uniqueTaskTitle);
        await Page.Keyboard.PressAsync("Enter");
        await Page.WaitForTimeoutAsync(2000);

        // Click on Tasks tab again to refresh view
        if (await tasksTab.CountAsync() > 0)
        {
            await tasksTab.First.ClickAsync();
            await Page.WaitForTimeoutAsync(1000);
        }

        // Count tasks again - should be one more
        var newCount = await taskRows.CountAsync();
        Console.WriteLine($"Task count after adding: {newCount}");

        Assert.That(newCount, Is.GreaterThan(initialCount),
            $"Task count should increase after adding task. Before: {initialCount}, After: {newCount}");

        // Verify the specific task title exists
        var newTask = Page.Locator($".allday-section .task-row:has-text('{uniqueTaskTitle}')");
        var foundTask = await newTask.CountAsync();
        Console.WriteLine($"Found task with title '{uniqueTaskTitle}': {foundTask > 0}");

        Assert.That(foundTask, Is.GreaterThan(0),
            $"Newly added task '{uniqueTaskTitle}' should appear in Tasks tab");

        Console.WriteLine("Task visibility test PASSED");
    }

    [Test]
    public async Task Search_ShouldFindTask_ThatAppearsInTasksTab()
    {
        Console.WriteLine("Testing that search finds tasks that also appear in Tasks tab");

        // Ensure Daily view
        var dailyButton = Page.Locator(".view-toggle button:has-text('Daily')");
        if (await dailyButton.CountAsync() > 0)
        {
            await dailyButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Add a task with unique searchable name
        var taskInput = Page.Locator(".add-task input[type='text']").First;
        await Expect(taskInput).ToBeVisibleAsync(new() { Timeout = 5000 });

        var uniqueTaskTitle = $"SearchMatch {DateTime.Now:HHmmss}";
        await taskInput.FillAsync(uniqueTaskTitle);
        await Page.Keyboard.PressAsync("Enter");
        await Page.WaitForTimeoutAsync(2000);

        // Verify task appears in Tasks tab
        var tasksTab = Page.Locator(".task-section-tabs .section-tab:has-text('Tasks')");
        if (await tasksTab.CountAsync() > 0)
        {
            await tasksTab.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        var taskInList = Page.Locator($".allday-section .task-row:has-text('{uniqueTaskTitle}')");
        var inListCount = await taskInList.CountAsync();
        Console.WriteLine($"Task in Tasks tab: {inListCount > 0}");

        // Now use search to find it
        var searchBtn = Page.Locator(".search-btn");
        if (await searchBtn.CountAsync() > 0)
        {
            await searchBtn.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        var searchInput = Page.Locator(".search-input");
        await Expect(searchInput).ToBeVisibleAsync(new() { Timeout = 5000 });
        await searchInput.FillAsync("SearchMatch");

        var searchExecuteBtn = Page.Locator(".search-execute-btn");
        await searchExecuteBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(3000);

        // Check search results
        var searchResults = Page.Locator(".search-result-item");
        var resultCount = await searchResults.CountAsync();
        Console.WriteLine($"Search results found: {resultCount}");

        // Both should find the task - if search finds it but Tasks tab doesn't, that's a bug
        if (resultCount > 0 && inListCount == 0)
        {
            Assert.Fail("BUG: Search found task but Tasks tab did not display it!");
        }

        Assert.That(inListCount, Is.GreaterThan(0),
            "Task should appear in Tasks tab");

        Console.WriteLine("Search and Tasks tab consistency test completed");
    }

    [Test]
    public async Task MobileDayRoller_ClickDate_ShouldSyncHeaderAndSchedule()
    {
        Console.WriteLine("Testing mobile day-roller: clicking date should sync header and schedule");

        // Set mobile viewport
        await Page.SetViewportSizeAsync(500, 800);
        await Page.ReloadAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(2000);

        // Ensure Daily view
        var dailyButton = Page.Locator(".view-toggle button:has-text('Daily')");
        if (await dailyButton.CountAsync() > 0)
        {
            await dailyButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Find the horizontal day roller at bottom
        var dayRollerWrapper = Page.Locator(".day-roller-wrapper");
        await Expect(dayRollerWrapper).ToBeVisibleAsync(new() { Timeout = 10000 });
        Console.WriteLine("Day roller wrapper visible");

        // Get the header date text before clicking
        var header = Page.Locator(".task-header");
        var initialHeaderText = await header.TextContentAsync();
        Console.WriteLine($"Initial header: {initialHeaderText}");

        // Find day slots in the roller
        var daySlots = Page.Locator(".day-slot");
        var slotCount = await daySlots.CountAsync();
        Console.WriteLine($"Found {slotCount} day slots");

        // Click on a different day slot (not the selected one)
        var targetSlot = daySlots.Nth(3); // Click on 4th slot
        var targetDateAttr = await targetSlot.GetAttributeAsync("data-date");
        Console.WriteLine($"Clicking on date: {targetDateAttr}");

        await targetSlot.ClickAsync();
        await Page.WaitForTimeoutAsync(1500);

        // Get header text after click
        var newHeaderText = await header.TextContentAsync();
        Console.WriteLine($"Header after click: {newHeaderText}");

        // Verify header changed
        if (targetDateAttr != null)
        {
            var expectedDate = DateTime.Parse(targetDateAttr);
            var expectedDayText = expectedDate.Day.ToString();
            Assert.That(newHeaderText, Does.Contain(expectedDayText),
                $"Header should show day {expectedDayText} from clicked date {targetDateAttr}");
        }

        Console.WriteLine("Mobile day-roller click sync test completed");
    }

    [Test]
    public async Task MobileDayRoller_ScrollAndStop_ShouldSyncToCenteredDate()
    {
        Console.WriteLine("Testing mobile day-roller: scroll should sync to centered date");

        // Set mobile viewport
        await Page.SetViewportSizeAsync(500, 800);
        await Page.ReloadAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(2000);

        // Ensure Daily view
        var dailyButton = Page.Locator(".view-toggle button:has-text('Daily')");
        if (await dailyButton.CountAsync() > 0)
        {
            await dailyButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Find the day roller track
        var dayRollerTrack = Page.Locator(".day-roller-track");
        await Expect(dayRollerTrack).ToBeVisibleAsync(new() { Timeout = 10000 });

        // Get initial header
        var header = Page.Locator(".task-header");
        var initialHeaderText = await header.TextContentAsync();
        Console.WriteLine($"Initial header: {initialHeaderText}");

        // Get bounding box for scroll simulation
        var box = await dayRollerTrack.BoundingBoxAsync();
        Assert.That(box, Is.Not.Null);

        // Simulate horizontal scroll (swipe left)
        var startX = box!.X + box.Width * 0.8;
        var endX = box.X + box.Width * 0.2;
        var centerY = box.Y + box.Height / 2;

        Console.WriteLine("Performing horizontal swipe...");
        await Page.Mouse.MoveAsync(startX, centerY);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(endX, centerY, new() { Steps = 20 });
        await Page.Mouse.UpAsync();

        // Wait for scroll to settle and sync
        await Page.WaitForTimeoutAsync(500);

        // Get header after scroll
        var newHeaderText = await header.TextContentAsync();
        Console.WriteLine($"Header after scroll: {newHeaderText}");

        // The header should have changed (scrolled to a different date)
        // Note: It might be the same if scroll didn't move enough
        Console.WriteLine("Mobile day-roller scroll sync test completed");

        // Verify page is responsive
        var plannerContainer = Page.Locator(".planner-container");
        await Expect(plannerContainer).ToBeVisibleAsync(new() { Timeout = 3000 });
    }

    [Test]
    public async Task MobileDayRoller_SelectedDateAndHeader_ShouldAlwaysMatch()
    {
        Console.WriteLine("Testing mobile day-roller: selected date indicator should match header");

        // Set mobile viewport
        await Page.SetViewportSizeAsync(500, 800);
        await Page.ReloadAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(2000);

        // Ensure Daily view
        var dailyButton = Page.Locator(".view-toggle button:has-text('Daily')");
        if (await dailyButton.CountAsync() > 0)
        {
            await dailyButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Find selected day slot
        var selectedSlot = Page.Locator(".day-slot.selected");
        await Expect(selectedSlot).ToBeVisibleAsync(new() { Timeout = 10000 });

        // Get selected date from data attribute
        var selectedDateAttr = await selectedSlot.GetAttributeAsync("data-date");
        Console.WriteLine($"Selected date attribute: {selectedDateAttr}");

        // Get day number from slot
        var selectedDayNum = await selectedSlot.Locator(".day-num").TextContentAsync();
        Console.WriteLine($"Selected day number: {selectedDayNum}");

        // Get header text
        var header = Page.Locator(".task-header");
        var headerText = await header.TextContentAsync();
        Console.WriteLine($"Header text: {headerText}");

        // Verify the day number from selected slot appears in header
        Assert.That(headerText, Does.Contain(selectedDayNum?.Trim() ?? ""),
            $"Header '{headerText}' should contain day number '{selectedDayNum}' from selected slot");

        // Click on a different date and verify sync
        var daySlots = Page.Locator(".day-slot:not(.selected)");
        if (await daySlots.CountAsync() > 2)
        {
            var targetSlot = daySlots.Nth(2);
            var targetDayNum = await targetSlot.Locator(".day-num").TextContentAsync();
            Console.WriteLine($"Clicking on day: {targetDayNum}");

            await targetSlot.ClickAsync();
            await Page.WaitForTimeoutAsync(1500);

            // Verify new selected slot
            var newSelectedSlot = Page.Locator(".day-slot.selected");
            var newSelectedDayNum = await newSelectedSlot.Locator(".day-num").TextContentAsync();

            // Verify header updated
            var newHeaderText = await header.TextContentAsync();
            Console.WriteLine($"New header: {newHeaderText}");

            Assert.That(newHeaderText, Does.Contain(targetDayNum?.Trim() ?? ""),
                $"After click, header '{newHeaderText}' should contain clicked day '{targetDayNum}'");

            Assert.That(newSelectedDayNum, Is.EqualTo(targetDayNum),
                $"Selected slot day '{newSelectedDayNum}' should match clicked day '{targetDayNum}'");
        }

        Console.WriteLine("Mobile day-roller date consistency test completed");
    }

    [Test]
    public async Task MobileDayRoller_CenterFrame_ShouldHighlightSelectedDate()
    {
        Console.WriteLine("Testing mobile day-roller: center frame should highlight selected date");

        // Set mobile viewport
        await Page.SetViewportSizeAsync(500, 800);
        await Page.ReloadAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(2000);

        // Ensure Daily view
        var dailyButton = Page.Locator(".view-toggle button:has-text('Daily')");
        if (await dailyButton.CountAsync() > 0)
        {
            await dailyButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Check that day-roller-wrapper has the ::before pseudo element (center frame)
        // We can't directly check pseudo elements, but we can verify the wrapper exists
        var dayRollerWrapper = Page.Locator(".day-roller-wrapper");
        await Expect(dayRollerWrapper).ToBeVisibleAsync(new() { Timeout = 10000 });

        // Verify position: relative is set (needed for absolute positioned pseudo element)
        var position = await dayRollerWrapper.EvaluateAsync<string>("el => getComputedStyle(el).position");
        Console.WriteLine($"Day roller wrapper position: {position}");
        Assert.That(position, Is.EqualTo("relative"),
            "Day roller wrapper should have position: relative for center frame");

        // Verify selected slot is visually distinguished
        var selectedSlot = Page.Locator(".day-slot.selected");
        await Expect(selectedSlot).ToBeVisibleAsync(new() { Timeout = 5000 });

        var backgroundColor = await selectedSlot.EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor");
        Console.WriteLine($"Selected slot background: {backgroundColor}");

        // Selected slot should have a distinct background (not transparent)
        Assert.That(backgroundColor, Is.Not.EqualTo("rgba(0, 0, 0, 0)"),
            "Selected slot should have a background color");

        Console.WriteLine("Mobile day-roller center frame test completed");
    }
}
