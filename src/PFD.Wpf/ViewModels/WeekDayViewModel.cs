using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PFD.Shared.Models;

namespace PFD.Wpf.ViewModels;

public partial class WeekDayViewModel : ObservableObject
{
    [ObservableProperty]
    private DateTime _date;

    [ObservableProperty]
    private string _dayName = string.Empty;

    [ObservableProperty]
    private string _dayNumber = string.Empty;

    [ObservableProperty]
    private bool _isToday;

    [ObservableProperty]
    private ObservableCollection<DailyTask> _tasks = new();

    public WeekDayViewModel(DateTime date, IEnumerable<DailyTask> tasks)
    {
        Date = date;
        DayName = date.ToString("ddd");
        DayNumber = date.Day.ToString();
        IsToday = date.Date == DateTime.Today;
        Tasks = new ObservableCollection<DailyTask>(tasks);
    }
}
