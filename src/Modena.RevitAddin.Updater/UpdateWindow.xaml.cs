using System.Windows;

namespace Modena.RevitAddin.Updater;

/// <summary>
/// Thin WPF shell for the updater. All logic lives in <see cref="UpdateViewModel"/>.
/// </summary>
public partial class UpdateWindow : Window
{
    private readonly UpdateViewModel _vm;

    public int ExitCode => _vm.ExitCode;

    public UpdateWindow(bool updateMode, int? revitPid, string? revitExePath)
    {
        InitializeComponent();
        _vm = new UpdateViewModel(updateMode, revitPid, revitExePath, Close);
        DataContext = _vm;
        Loaded += async (_, _) => await _vm.StartAsync();
    }
}
