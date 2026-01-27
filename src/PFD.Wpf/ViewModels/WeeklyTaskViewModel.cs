using CommunityToolkit.Mvvm.ComponentModel;
using PFD.Shared.Models;

namespace PFD.Wpf.ViewModels;

public partial class WeeklyTaskViewModel : ObservableObject
{
    private readonly DailyTask _task;

    public int Id => _task.Id;

    public string Title
    {
        get => _task.Title;
        set
        {
            if (_task.Title != value)
            {
                _task.Title = value;
                OnPropertyChanged();
            }
        }
    }
    public DateTime TaskDate => _task.TaskDate;

    [ObservableProperty]
    private bool _isCompleted;

    public bool IsMondayTask => _task.TaskDate.DayOfWeek == DayOfWeek.Monday;
    public bool IsTuesdayTask => _task.TaskDate.DayOfWeek == DayOfWeek.Tuesday;
    public bool IsWednesdayTask => _task.TaskDate.DayOfWeek == DayOfWeek.Wednesday;
    public bool IsThursdayTask => _task.TaskDate.DayOfWeek == DayOfWeek.Thursday;
    public bool IsFridayTask => _task.TaskDate.DayOfWeek == DayOfWeek.Friday;

    public DailyTask Task => _task;

    public WeeklyTaskViewModel(DailyTask task)
    {
        _task = task;
        _isCompleted = task.IsCompleted;
    }
}
