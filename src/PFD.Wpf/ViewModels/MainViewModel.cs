using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFD.Shared.Interfaces;
using PFD.Shared.Models;

namespace PFD.Wpf.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ITaskService _taskService;
    private readonly IOllamaService _ollamaService;
    private readonly IAnalysisService _analysisService;

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

    public MainViewModel(ITaskService taskService, IOllamaService ollamaService, IAnalysisService analysisService)
    {
        _taskService = taskService;
        _ollamaService = ollamaService;
        _analysisService = analysisService;
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
        // Load overdue tasks (only when viewing today)
        if (SelectedDate.Date == DateTime.Today)
        {
            var overdue = await _taskService.GetOverdueTasksAsync(DateTime.Today);
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

            var allTasks = await _taskService.GetTasksForDateRangeAsync(startOfWeek, startOfWeek.AddDays(4));

            // Build weekly task list with day indicators
            var weeklyTasks = allTasks.Select(t => new WeeklyTaskViewModel(t)).ToList();
            WeeklyTasks = new ObservableCollection<WeeklyTaskViewModel>(weeklyTasks);
            Tasks = new ObservableCollection<DailyTask>(); // Clear daily tasks
            ScheduledTasks = new ObservableCollection<DailyTask>();
            AllDayTasks = new ObservableCollection<DailyTask>();
        }
        else
        {
            var tasks = await _taskService.GetTasksForDateAsync(SelectedDate);
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
        if (string.IsNullOrWhiteSpace(NewTaskText))
            return;

        var task = new DailyTask
        {
            Title = NewTaskText,
            TaskDate = SelectedDate,
            IsCompleted = false,
            SortOrder = Tasks.Count,
            IsAllDay = string.IsNullOrWhiteSpace(NewTaskTimeText)
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

        await _taskService.ToggleCompletionAsync(task.Id);
        await LoadTasksAsync();
    }

    [RelayCommand]
    private async Task DeleteTask(DailyTask task)
    {
        if (task == null) return;

        await _taskService.DeleteTaskAsync(task.Id);
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

        await _taskService.ToggleCompletionAsync(weeklyTask.Id);
        await LoadTasksAsync();
    }

    [RelayCommand]
    private async Task DeleteWeeklyTask(WeeklyTaskViewModel weeklyTask)
    {
        if (weeklyTask == null) return;

        await _taskService.DeleteTaskAsync(weeklyTask.Id);
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

        await _taskService.ToggleCompletionAsync(task.Id);
        await LoadTasksAsync();
    }

    [RelayCommand]
    private async Task DeleteOverdueTask(DailyTask task)
    {
        if (task == null) return;

        await _taskService.DeleteTaskAsync(task.Id);
        await LoadTasksAsync();
    }

    [RelayCommand]
    private async Task MoveOverdueToToday(DailyTask task)
    {
        if (task == null) return;

        await _taskService.RescheduleTaskAsync(task.Id, DateTime.Today);
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
            var recentTasks = await _taskService.GetRecentTasksAsync(30);
            var overdueTasks = await _taskService.GetOverdueTasksAsync(DateTime.Today);
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
            var upcomingTasks = await _taskService.GetUpcomingTasksAsync(14);
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

        await _taskService.ScheduleTaskTimeAsync(EditingTimeTask.Id, time, EditingDuration);
        EditingTimeTask = null;
        IsTimeEditorOpen = false;
        await LoadTasksAsync();
    }

    [RelayCommand]
    private async Task ClearTaskTime(DailyTask task)
    {
        if (task == null) return;

        await _taskService.ScheduleTaskTimeAsync(task.Id, null, 30);
        await LoadTasksAsync();
    }

    public static string FormatTime(TimeSpan? time)
    {
        if (!time.HasValue) return "";
        var dt = DateTime.Today.Add(time.Value);
        return dt.ToString("h:mm tt");
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
