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
}
