using CommunityToolkit.Mvvm.ComponentModel;

namespace PFD.Wpf.ViewModels;

public partial class CalendarDayViewModel : ObservableObject
{
    [ObservableProperty]
    private int _day;

    [ObservableProperty]
    private DateTime _date;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isToday;

    [ObservableProperty]
    private bool _isCurrentMonth;

    [ObservableProperty]
    private bool _isVisible = true;

    public CalendarDayViewModel(DateTime date, bool isCurrentMonth)
    {
        Date = date;
        Day = date.Day;
        IsCurrentMonth = isCurrentMonth;
        IsToday = date.Date == DateTime.Today;
        IsVisible = true;
    }
}
