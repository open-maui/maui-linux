using System;
using System.ComponentModel;

namespace TodoApp;

public class TodoItem : INotifyPropertyChanged
{
    private string _title = "";
    private string _notes = "";
    private bool _isCompleted;
    private int _index;

    public int Id { get; set; }

    public int Index
    {
        get => _index;
        set { if (_index != value) { _index = value; OnPropertyChanged(nameof(Index)); } }
    }

    public string Title
    {
        get => _title;
        set { if (_title != value) { _title = value; OnPropertyChanged(nameof(Title)); } }
    }

    public string Notes
    {
        get => _notes;
        set { if (_notes != value) { _notes = value; OnPropertyChanged(nameof(Notes)); } }
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        set { if (_isCompleted != value) { _isCompleted = value; OnPropertyChanged(nameof(IsCompleted)); } }
    }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
