using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFD.Services;
using PFD.Shared.Interfaces;
using PFD.Shared.Models;

namespace PFD.Wpf.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ITaskService _taskService;
    private readonly IOllamaService _ollamaService;
    private readonly IAnalysisService _analysisService;
    private readonly IAuthService _authService;
    private readonly IGoogleCalendarService? _googleCalendarService;
    private readonly MicrosoftCalendarService? _microsoftCalendarService;
    private readonly ICalendarSyncService? _calendarSyncService;

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    [ObservableProperty]
    private DateTime _currentMonth = DateTime.Today;

    [ObservableProperty]
    private string _currentMonthYear = string.Empty;

    [ObservableProperty]
    private string _selectedDateHeader = string.Empty;

    [ObservableProperty]
    private string _newTaskText = string.Empty;

    [ObservableProperty]
    private TimeSpan? _newTaskTime = null;

    [ObservableProperty]
    private string _newTaskTimeText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CalendarDayViewModel> _calendarDays = new();

    [ObservableProperty]
    private ObservableCollection<DailyTask> _tasks = new();

    [ObservableProperty]
    private bool _isDailyView = true;

    [ObservableProperty]
    private bool _isWeeklyView = false;

    [ObservableProperty]
    private ObservableCollection<WeekDayViewModel> _weekDays = new();

    [ObservableProperty]
    private ObservableCollection<WeeklyTaskViewModel> _weeklyTasks = new();

    [ObservableProperty]
    private ObservableCollection<DailyTask> _overdueTasks = new();

    [ObservableProperty]
    private bool _hasOverdueTasks = false;

    // AI Insights
    [ObservableProperty]
    private bool _showAiPanel = false;

    [ObservableProperty]
    private bool _isAiLoading = false;

    [ObservableProperty]
    private string _aiSummary = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _aiSuggestions = new();

    [ObservableProperty]
    private ObservableCollection<PrioritizedTaskViewModel> _prioritizedTasks = new();

    [ObservableProperty]
    private SchedulingSuggestion? _schedulingSuggestion;

    [ObservableProperty]
    private bool _hasSchedulingSuggestion = false;

    // Time editor for scheduled tasks
    [ObservableProperty]
    private DailyTask? _editingTimeTask = null;

    [ObservableProperty]
    private bool _isTimeEditorOpen = false;

    [ObservableProperty]
    private string _editingTimeText = "09:00";

    [ObservableProperty]
    private int _editingDuration = 30;

    // Scheduled vs All-day task lists
    [ObservableProperty]
    private ObservableCollection<DailyTask> _scheduledTasks = new();

    [ObservableProperty]
    private ObservableCollection<DailyTask> _allDayTasks = new();

    // Authentication
    [ObservableProperty]
    private int _currentUserId = 0;

    [ObservableProperty]
    private string _currentUsername = string.Empty;

    [ObservableProperty]
    private bool _isLoggedIn = false;

    [ObservableProperty]
    private bool _isLoginMode = true;

    [ObservableProperty]
    private string _loginUsername = string.Empty;

    [ObservableProperty]
    private string _loginPassword = string.Empty;

    [ObservableProperty]
    private string _loginError = string.Empty;

    // External Calendars
    [ObservableProperty]
    private bool _isGoogleCalendarAvailable = false;

    [ObservableProperty]
    private bool _isMicrosoftCalendarAvailable = false;

    [ObservableProperty]
    private bool _isGoogleConnected = false;

    [ObservableProperty]
    private bool _isMicrosoftConnected = false;

    [ObservableProperty]
    private bool _isGoogleImporting = false;

    [ObservableProperty]
    private string _googleImportMessage = string.Empty;

    // Calendar Tabs
    [ObservableProperty]
    private CalendarSource _activeCalendarTab = CalendarSource.Integrated;

    [ObservableProperty]
    private ObservableCollection<ExternalCalendarEvent> _externalEvents = new();

    [ObservableProperty]
    private bool _isLoadingExternalEvents = false;

    [ObservableProperty]
    private int _googleEventCount = 0;

    [ObservableProperty]
    private int _microsoftEventCount = 0;

    [ObservableProperty]
    private bool _showExternalCalendarTabs = false;

    public MainViewModel(
        ITaskService taskService,
        IOllamaService ollamaService,
        IAnalysisService analysisService,
        IAuthService authService,
        IGoogleCalendarService? googleCalendarService = null,
        MicrosoftCalendarService? microsoftCalendarService = null,
        ICalendarSyncService? calendarSyncService = null)
    {
        _taskService = taskService;
        _ollamaService = ollamaService;
        _analysisService = analysisService;
        _authService = authService;
        _googleCalendarService = googleCalendarService;
        _microsoftCalendarService = microsoftCalendarService;
        _calendarSyncService = calendarSyncService;
        IsGoogleCalendarAvailable = googleCalendarService != null;
        IsMicrosoftCalendarAvailable = microsoftCalendarService != null;
    }

    public async Task InitializeAsync()
    {
        UpdateCalendar();
        UpdateSelectedDateHeader();
        await LoadTasksAsync();
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        UpdateSelectedDateHeader();
        UpdateCalendarSelection();
        _ = LoadTasksAsync();
    }

    partial void OnCurrentMonthChanged(DateTime value)
    {
        UpdateCalendar();
    }

    partial void OnIsDailyViewChanged(bool value)
    {
        if (value)
        {
            IsWeeklyView = false;
            UpdateSelectedDateHeader();
            _ = LoadTasksAsync();
        }
    }

    partial void OnIsWeeklyViewChanged(bool value)
    {
        if (value)
        {
            IsDailyView = false;
            UpdateSelectedDateHeader();
            _ = LoadTasksAsync();
        }
    }

    private void UpdateSelectedDateHeader()
    {
        if (IsWeeklyView)
        {
            // Get Monday of the current week (5-day work week)
            var dayOfWeek = (int)SelectedDate.DayOfWeek;
            var daysToMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1; // Sunday = go back 6 days
            var startOfWeek = SelectedDate.AddDays(-daysToMonday);
            var endOfWeek = startOfWeek.AddDays(4); // Friday
            SelectedDateHeader = $"WEEK: {startOfWeek:MMM d} - {endOfWeek:MMM d, yyyy}";
        }
        else
        {
            SelectedDateHeader = SelectedDate.Date == DateTime.Today
                ? $"TODAY - {SelectedDate:dddd, MMMM d, yyyy}"
                : SelectedDate.ToString("dddd, MMMM d, yyyy");
        }
    }

    private void UpdateCalendar()
    {
        CurrentMonthYear = CurrentMonth.ToString("MMMM yyyy");

        var days = new ObservableCollection<CalendarDayViewModel>();

        // Get first day of month
        var firstOfMonth = new DateTime(CurrentMonth.Year, CurrentMonth.Month, 1);
        var lastOfMonth = firstOfMonth.AddMonths(1).AddDays(-1);

        // Get starting day (Sunday = 0)
        int startDayOfWeek = (int)firstOfMonth.DayOfWeek;

        // Add days from previous month
        var prevMonth = firstOfMonth.AddDays(-startDayOfWeek);
        for (int i = 0; i < startDayOfWeek; i++)
        {
            var dayVm = new CalendarDayViewModel(prevMonth.AddDays(i), false);
            dayVm.IsSelected = dayVm.Date.Date == SelectedDate.Date;
            days.Add(dayVm);
        }

        // Add days of current month
        for (int day = 1; day <= lastOfMonth.Day; day++)
        {
            var date = new DateTime(CurrentMonth.Year, CurrentMonth.Month, day);
            var dayVm = new CalendarDayViewModel(date, true);
            dayVm.IsSelected = date.Date == SelectedDate.Date;
            days.Add(dayVm);
        }

        // Add days from next month to fill grid (6 rows x 7 days = 42)
        int remainingDays = 42 - days.Count;
        var nextMonth = lastOfMonth.AddDays(1);
        for (int i = 0; i < remainingDays; i++)
        {
            var dayVm = new CalendarDayViewModel(nextMonth.AddDays(i), false);
            dayVm.IsSelected = dayVm.Date.Date == SelectedDate.Date;
            days.Add(dayVm);
        }

        CalendarDays = days;
    }

    private void UpdateCalendarSelection()
    {
        foreach (var day in CalendarDays)
        {
            day.IsSelected = day.Date.Date == SelectedDate.Date;
        }
    }

    private async Task LoadTasksAsync()
    {
        if (CurrentUserId == 0) return;

        // Load overdue tasks (only when viewing today)
        if (SelectedDate.Date == DateTime.Today)
        {
            var overdue = await _taskService.GetOverdueTasksAsync(DateTime.Today, CurrentUserId);
            OverdueTasks = new ObservableCollection<DailyTask>(overdue);
            HasOverdueTasks = overdue.Any();
        }
        else
        {
            OverdueTasks = new ObservableCollection<DailyTask>();
            HasOverdueTasks = false;
        }

        if (IsWeeklyView)
        {
            // Get Monday of the current week (5-day work week)
            var dayOfWeek = (int)SelectedDate.DayOfWeek;
            var daysToMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
            var startOfWeek = SelectedDate.AddDays(-daysToMonday);

            var allTasks = await _taskService.GetTasksForDateRangeAsync(startOfWeek, startOfWeek.AddDays(4), CurrentUserId);

            // Build weekly task list with day indicators
            var weeklyTasks = allTasks.Select(t => new WeeklyTaskViewModel(t)).ToList();
            WeeklyTasks = new ObservableCollection<WeeklyTaskViewModel>(weeklyTasks);
            Tasks = new ObservableCollection<DailyTask>(); // Clear daily tasks
            ScheduledTasks = new ObservableCollection<DailyTask>();
            AllDayTasks = new ObservableCollection<DailyTask>();
        }
        else
        {
            var tasks = await _taskService.GetTasksForDateAsync(SelectedDate, CurrentUserId);
            Tasks = new ObservableCollection<DailyTask>(tasks);

            // Split into scheduled and all-day tasks
            ScheduledTasks = new ObservableCollection<DailyTask>(
                tasks.Where(t => !t.IsAllDay && t.ScheduledTime.HasValue));
            AllDayTasks = new ObservableCollection<DailyTask>(
                tasks.Where(t => t.IsAllDay || !t.ScheduledTime.HasValue));

            WeeklyTasks = new ObservableCollection<WeeklyTaskViewModel>(); // Clear weekly tasks
        }
    }

    [RelayCommand]
    private void SelectDate(DateTime date)
    {
        SelectedDate = date;

        // If selected date is in different month, navigate to that month
        if (date.Month != CurrentMonth.Month || date.Year != CurrentMonth.Year)
        {
            CurrentMonth = new DateTime(date.Year, date.Month, 1);
        }
    }

    [RelayCommand]
    private void PreviousMonth()
    {
        CurrentMonth = CurrentMonth.AddMonths(-1);
    }

    [RelayCommand]
    private void NextMonth()
    {
        CurrentMonth = CurrentMonth.AddMonths(1);
    }

    [RelayCommand]
    private async Task AddTask()
    {
        if (string.IsNullOrWhiteSpace(NewTaskText) || CurrentUserId == 0)
            return;

        var task = new DailyTask
        {
            Title = NewTaskText,
            TaskDate = SelectedDate,
            IsCompleted = false,
            SortOrder = Tasks.Count,
            IsAllDay = string.IsNullOrWhiteSpace(NewTaskTimeText),
            UserId = CurrentUserId
        };

        // Parse scheduled time if provided
        if (!string.IsNullOrWhiteSpace(NewTaskTimeText) && TimeSpan.TryParse(NewTaskTimeText, out var time))
        {
            task.ScheduledTime = time;
            task.IsAllDay = false;
        }

        // Try to augment with AI metadata
        var metadata = await _ollamaService.AugmentTaskAsync(NewTaskText);
        if (metadata != null)
        {
            task.MetadataJson = System.Text.Json.JsonSerializer.Serialize(metadata);
            task.StudentId = metadata.StudentId;
            task.DueBy = metadata.SuggestedDueDate;

            // Determine task type from category
            if (!string.IsNullOrEmpty(metadata.Category))
            {
                task.TaskType = metadata.Category.ToLower() switch
                {
                    "academic" => PFD.Shared.Enums.TaskType.Academic,
                    "meeting" => PFD.Shared.Enums.TaskType.Meeting,
                    "personal" => PFD.Shared.Enums.TaskType.Personal,
                    "work" => PFD.Shared.Enums.TaskType.Work,
                    _ => PFD.Shared.Enums.TaskType.General
                };
            }
        }

        await _taskService.CreateTaskAsync(task);
        NewTaskText = string.Empty;
        NewTaskTimeText = string.Empty;
        await LoadTasksAsync();
    }

    [RelayCommand]
    private async Task ToggleTask(DailyTask task)
    {
        if (task == null) return;

        await _taskService.ToggleCompletionAsync(task.Id, CurrentUserId);
        await LoadTasksAsync();
    }

    [RelayCommand]
    private async Task DeleteTask(DailyTask task)
    {
        if (task == null) return;

        await _taskService.DeleteTaskAsync(task.Id, CurrentUserId);
        await LoadTasksAsync();
    }

    [RelayCommand]
    private async Task UpdateTaskTitle(DailyTask task)
    {
        if (task == null) return;

        await _taskService.UpdateTaskAsync(task);
    }

    [RelayCommand]
    private async Task ToggleWeeklyTask(WeeklyTaskViewModel weeklyTask)
    {
        if (weeklyTask == null) return;

        await _taskService.ToggleCompletionAsync(weeklyTask.Id, CurrentUserId);
        await LoadTasksAsync();
    }

    [RelayCommand]
    private async Task DeleteWeeklyTask(WeeklyTaskViewModel weeklyTask)
    {
        if (weeklyTask == null) return;

        await _taskService.DeleteTaskAsync(weeklyTask.Id, CurrentUserId);
        await LoadTasksAsync();
    }

    [RelayCommand]
    private async Task UpdateWeeklyTaskTitle(WeeklyTaskViewModel weeklyTask)
    {
        if (weeklyTask == null) return;

        await _taskService.UpdateTaskAsync(weeklyTask.Task);
    }

    [RelayCommand]
    private async Task ToggleOverdueTask(DailyTask task)
    {
        if (task == null) return;

        await _taskService.ToggleCompletionAsync(task.Id, CurrentUserId);
        await LoadTasksAsync();
    }

    [RelayCommand]
    private async Task DeleteOverdueTask(DailyTask task)
    {
        if (task == null) return;

        await _taskService.DeleteTaskAsync(task.Id, CurrentUserId);
        await LoadTasksAsync();
    }

    [RelayCommand]
    private async Task MoveOverdueToToday(DailyTask task)
    {
        if (task == null) return;

        await _taskService.RescheduleTaskAsync(task.Id, DateTime.Today, CurrentUserId);
        await LoadTasksAsync();
    }

    [RelayCommand]
    private void ToggleAiPanel()
    {
        ShowAiPanel = !ShowAiPanel;
        if (ShowAiPanel)
        {
            _ = LoadAiInsightsAsync();
        }
    }

    [RelayCommand]
    private async Task RefreshAiInsights()
    {
        await LoadAiInsightsAsync();
    }

    private async Task LoadAiInsightsAsync()
    {
        if (!await _analysisService.IsAvailableAsync())
        {
            AiSummary = "AI service unavailable. Start Ollama to enable AI features.";
            return;
        }

        IsAiLoading = true;
        try
        {
            // Get recent tasks for analysis
            var recentTasks = await _taskService.GetRecentTasksAsync(CurrentUserId, 30);
            var overdueTasks = await _taskService.GetOverdueTasksAsync(DateTime.Today, CurrentUserId);
            var todayTasks = Tasks.ToList();

            // Get insights
            var insights = await _analysisService.GetInsightsAsync(recentTasks);
            AiSummary = insights.ProductivitySummary;
            AiSuggestions = new ObservableCollection<string>(insights.Suggestions);

            // Get prioritization for today's tasks
            if (todayTasks.Any() || overdueTasks.Any())
            {
                var prioritization = await _analysisService.GetPrioritizedTasksAsync(todayTasks, overdueTasks);

                var prioritizedList = new List<PrioritizedTaskViewModel>();
                foreach (var taskId in prioritization.PriorityOrder)
                {
                    var task = todayTasks.FirstOrDefault(t => t.Id == taskId)
                            ?? overdueTasks.FirstOrDefault(t => t.Id == taskId);
                    if (task != null)
                    {
                        prioritizedList.Add(new PrioritizedTaskViewModel
                        {
                            Task = task,
                            PriorityScore = prioritization.PriorityScores.GetValueOrDefault(taskId, 50),
                            Reasoning = prioritization.Reasoning.GetValueOrDefault(taskId, "")
                        });
                    }
                }
                PrioritizedTasks = new ObservableCollection<PrioritizedTaskViewModel>(prioritizedList.Take(5));
            }
        }
        catch (Exception ex)
        {
            AiSummary = $"Error loading insights: {ex.Message}";
        }
        finally
        {
            IsAiLoading = false;
        }
    }

    [RelayCommand]
    private async Task GetSchedulingSuggestion()
    {
        if (string.IsNullOrWhiteSpace(NewTaskText))
        {
            HasSchedulingSuggestion = false;
            return;
        }

        try
        {
            var upcomingTasks = await _taskService.GetUpcomingTasksAsync(CurrentUserId, 14);
            SchedulingSuggestion = await _analysisService.SuggestSchedulingAsync(NewTaskText, upcomingTasks);
            HasSchedulingSuggestion = true;
        }
        catch
        {
            HasSchedulingSuggestion = false;
        }
    }

    [RelayCommand]
    private void ApplySchedulingSuggestion()
    {
        if (SchedulingSuggestion != null)
        {
            SelectedDate = SchedulingSuggestion.SuggestedDate;
            HasSchedulingSuggestion = false;
        }
    }

    [RelayCommand]
    private void OpenTimeEditor(DailyTask task)
    {
        if (task == null) return;

        EditingTimeTask = task;
        EditingTimeText = task.ScheduledTime?.ToString(@"hh\:mm") ?? "09:00";
        EditingDuration = task.DurationMinutes > 0 ? task.DurationMinutes : 30;
        IsTimeEditorOpen = true;
    }

    [RelayCommand]
    private void CloseTimeEditor()
    {
        EditingTimeTask = null;
        IsTimeEditorOpen = false;
    }

    [RelayCommand]
    private async Task SaveTaskTime()
    {
        if (EditingTimeTask == null) return;

        TimeSpan? time = null;
        if (TimeSpan.TryParse(EditingTimeText, out var parsed))
        {
            time = parsed;
        }

        await _taskService.ScheduleTaskTimeAsync(EditingTimeTask.Id, time, CurrentUserId, EditingDuration);
        EditingTimeTask = null;
        IsTimeEditorOpen = false;
        await LoadTasksAsync();
    }

    [RelayCommand]
    private async Task ClearTaskTime(DailyTask task)
    {
        if (task == null) return;

        await _taskService.ScheduleTaskTimeAsync(task.Id, null, CurrentUserId, 30);
        await LoadTasksAsync();
    }

    public static string FormatTime(TimeSpan? time)
    {
        if (!time.HasValue) return "";
        var dt = DateTime.Today.Add(time.Value);
        return dt.ToString("h:mm tt");
    }

    [RelayCommand]
    private async Task Login()
    {
        LoginError = string.Empty;

        if (string.IsNullOrWhiteSpace(LoginUsername) || string.IsNullOrWhiteSpace(LoginPassword))
        {
            LoginError = "Please enter username and password";
            return;
        }

        var user = await _authService.LoginAsync(LoginUsername.Trim(), LoginPassword);
        if (user != null)
        {
            CurrentUserId = user.Id;
            CurrentUsername = user.DisplayName ?? user.Username;
            IsLoggedIn = true;
            LoginPassword = string.Empty;

            // Check external calendar connections
            if (_googleCalendarService != null)
            {
                IsGoogleConnected = await _googleCalendarService.IsConnectedAsync(CurrentUserId);
            }
            if (_microsoftCalendarService != null)
            {
                IsMicrosoftConnected = await _microsoftCalendarService.IsConnectedAsync(CurrentUserId);
            }

            UpdateExternalCalendarTabs();
            await LoadExternalEventCounts();
            await InitializeAsync();
        }
        else
        {
            LoginError = "Invalid username or password";
        }
    }

    [RelayCommand]
    private async Task Register()
    {
        LoginError = string.Empty;

        if (string.IsNullOrWhiteSpace(LoginUsername) || string.IsNullOrWhiteSpace(LoginPassword))
        {
            LoginError = "Please enter username and password";
            return;
        }

        if (LoginUsername.Trim().Length < 3)
        {
            LoginError = "Username must be at least 3 characters";
            return;
        }

        if (LoginPassword.Length < 4)
        {
            LoginError = "Password must be at least 4 characters";
            return;
        }

        if (await _authService.UsernameExistsAsync(LoginUsername.Trim()))
        {
            LoginError = "Username already taken";
            return;
        }

        var user = await _authService.RegisterAsync(LoginUsername.Trim(), LoginPassword);
        if (user != null)
        {
            CurrentUserId = user.Id;
            CurrentUsername = user.DisplayName ?? user.Username;
            IsLoggedIn = true;
            LoginPassword = string.Empty;
            await InitializeAsync();
        }
        else
        {
            LoginError = "Registration failed";
        }
    }

    [RelayCommand]
    private void Logout()
    {
        CurrentUserId = 0;
        CurrentUsername = string.Empty;
        IsLoggedIn = false;
        Tasks.Clear();
        WeeklyTasks.Clear();
        OverdueTasks.Clear();
        ScheduledTasks.Clear();
        AllDayTasks.Clear();
    }

    [RelayCommand]
    private void ToggleLoginMode()
    {
        IsLoginMode = !IsLoginMode;
        LoginError = string.Empty;
        LoginPassword = string.Empty;
    }

    // Google Calendar methods
    [RelayCommand]
    private async Task ConnectGoogleCalendar()
    {
        if (_googleCalendarService == null || CurrentUserId == 0) return;

        try
        {
            var authUrl = await _googleCalendarService.GetAuthorizationUrlAsync(CurrentUserId);
            // Open in default browser
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = authUrl,
                UseShellExecute = true
            });
            GoogleImportMessage = "Complete authorization in browser, then click 'Check Connection'";
        }
        catch (Exception ex)
        {
            GoogleImportMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CheckGoogleConnection()
    {
        if (_googleCalendarService == null || CurrentUserId == 0) return;

        IsGoogleConnected = await _googleCalendarService.IsConnectedAsync(CurrentUserId);
        if (IsGoogleConnected)
        {
            GoogleImportMessage = "Connected! Click 'Import Events' to sync.";
        }
        else
        {
            GoogleImportMessage = "Not connected. Click 'Connect' to authorize.";
        }
    }

    [RelayCommand]
    private async Task ImportGoogleEvents()
    {
        if (_googleCalendarService == null || CurrentUserId == 0) return;

        IsGoogleImporting = true;
        GoogleImportMessage = "";

        try
        {
            var startDate = DateTime.Today;
            var endDate = DateTime.Today.AddDays(30);

            var count = await _googleCalendarService.ImportEventsAsTasksAsync(CurrentUserId, startDate, endDate);
            GoogleImportMessage = count > 0
                ? $"Imported {count} event(s)!"
                : "No new events to import.";

            await LoadTasksAsync();
        }
        catch (Exception ex)
        {
            GoogleImportMessage = $"Import failed: {ex.Message}";
        }
        finally
        {
            IsGoogleImporting = false;
        }
    }

    [RelayCommand]
    private async Task DisconnectGoogleCalendar()
    {
        if (_googleCalendarService == null || CurrentUserId == 0) return;

        await _googleCalendarService.DisconnectAsync(CurrentUserId);
        IsGoogleConnected = false;
        GoogleEventCount = 0;
        UpdateExternalCalendarTabs();
        GoogleImportMessage = "Disconnected.";
    }

    // Microsoft Calendar methods
    [RelayCommand]
    private async Task ConnectMicrosoftCalendar()
    {
        if (_microsoftCalendarService == null || CurrentUserId == 0) return;

        try
        {
            var authUrl = await _microsoftCalendarService.GetAuthorizationUrlAsync(CurrentUserId);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = authUrl,
                UseShellExecute = true
            });
            GoogleImportMessage = "Complete authorization in browser, then click 'Check Connection'";
        }
        catch (Exception ex)
        {
            GoogleImportMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CheckMicrosoftConnection()
    {
        if (_microsoftCalendarService == null || CurrentUserId == 0) return;

        IsMicrosoftConnected = await _microsoftCalendarService.IsConnectedAsync(CurrentUserId);
        UpdateExternalCalendarTabs();
        await LoadExternalEventCounts();

        GoogleImportMessage = IsMicrosoftConnected
            ? "Microsoft connected! Switch to Teams tab to view events."
            : "Not connected. Click 'Connect' to authorize.";
    }

    [RelayCommand]
    private async Task DisconnectMicrosoftCalendar()
    {
        if (_microsoftCalendarService == null || CurrentUserId == 0) return;

        await _microsoftCalendarService.DisconnectAsync(CurrentUserId);
        IsMicrosoftConnected = false;
        MicrosoftEventCount = 0;
        if (ActiveCalendarTab == CalendarSource.Microsoft)
        {
            ActiveCalendarTab = CalendarSource.Integrated;
        }
        UpdateExternalCalendarTabs();
        GoogleImportMessage = "Microsoft disconnected.";
    }

    // Calendar Tab methods
    private void UpdateExternalCalendarTabs()
    {
        ShowExternalCalendarTabs = IsGoogleConnected || IsMicrosoftConnected;
    }

    [RelayCommand]
    private async Task SetCalendarTab(CalendarSource tab)
    {
        ActiveCalendarTab = tab;
        if (tab != CalendarSource.Integrated)
        {
            await LoadExternalEventsForTab(tab);
        }
    }

    private async Task LoadExternalEventCounts()
    {
        if (_calendarSyncService == null || CurrentUserId == 0) return;

        var startDate = IsDailyView ? SelectedDate : GetWeekStart(SelectedDate);
        var endDate = IsDailyView ? SelectedDate.AddDays(1) : startDate.AddDays(7);

        if (IsGoogleConnected)
        {
            try
            {
                var events = await _calendarSyncService.GetEventsBySourceAsync(CurrentUserId, CalendarSource.Google, startDate, endDate);
                GoogleEventCount = events.Count;
            }
            catch { GoogleEventCount = 0; }
        }

        if (IsMicrosoftConnected)
        {
            try
            {
                var events = await _calendarSyncService.GetEventsBySourceAsync(CurrentUserId, CalendarSource.Microsoft, startDate, endDate);
                MicrosoftEventCount = events.Count;
            }
            catch { MicrosoftEventCount = 0; }
        }
    }

    private async Task LoadExternalEventsForTab(CalendarSource source)
    {
        if (_calendarSyncService == null || CurrentUserId == 0) return;

        IsLoadingExternalEvents = true;

        try
        {
            var startDate = IsDailyView ? SelectedDate : GetWeekStart(SelectedDate);
            var endDate = IsDailyView ? SelectedDate.AddDays(1) : startDate.AddDays(7);

            var events = await _calendarSyncService.GetEventsBySourceAsync(CurrentUserId, source, startDate, endDate);
            ExternalEvents = new ObservableCollection<ExternalCalendarEvent>(events);
        }
        catch (Exception ex)
        {
            GoogleImportMessage = $"Error loading events: {ex.Message}";
            ExternalEvents.Clear();
        }
        finally
        {
            IsLoadingExternalEvents = false;
        }
    }

    [RelayCommand]
    private async Task ImportSingleEvent(ExternalCalendarEvent evt)
    {
        if (_calendarSyncService == null || CurrentUserId == 0 || evt == null) return;

        try
        {
            await _calendarSyncService.ImportEventToIntegratedAsync(CurrentUserId, evt);
            evt.IsImported = true;
            await LoadTasksAsync();
            GoogleImportMessage = $"Added '{evt.Title}' to Integrated";
        }
        catch (Exception ex)
        {
            GoogleImportMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ImportAllExternalEvents()
    {
        if (_calendarSyncService == null || CurrentUserId == 0) return;

        IsGoogleImporting = true;

        try
        {
            var toImport = ExternalEvents.Where(e => !e.IsImported).ToList();
            var count = await _calendarSyncService.ImportEventsToIntegratedAsync(CurrentUserId, toImport);
            GoogleImportMessage = $"Imported {count} event(s) to Integrated!";
            await LoadTasksAsync();
            await LoadExternalEventsForTab(ActiveCalendarTab);
        }
        catch (Exception ex)
        {
            GoogleImportMessage = $"Import failed: {ex.Message}";
        }
        finally
        {
            IsGoogleImporting = false;
        }
    }

    private DateTime GetWeekStart(DateTime date)
    {
        int diff = (7 + (date.DayOfWeek - DayOfWeek.Sunday)) % 7;
        return date.AddDays(-1 * diff).Date;
    }
}

public class PrioritizedTaskViewModel
{
    public DailyTask Task { get; set; } = null!;
    public int PriorityScore { get; set; }
    public string Reasoning { get; set; } = string.Empty;

    public string PriorityLabel => PriorityScore switch
    {
        >= 90 => "Critical",
        >= 70 => "High",
        >= 50 => "Medium",
        _ => "Low"
    };

    public string PriorityColor => PriorityScore switch
    {
        >= 90 => "#D32F2F",
        >= 70 => "#F57C00",
        >= 50 => "#1976D2",
        _ => "#757575"
    };
}
