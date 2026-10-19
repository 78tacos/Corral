using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Corral;

/// <summary>A single file/folder/shortcut shown inside a fence.</summary>
public sealed class FenceItem : INotifyPropertyChanged
{
    private ImageSource? _icon;

    public string Path { get; }
    public string Name { get; }

    public ImageSource? Icon
    {
        get => _icon;
        private set { _icon = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon))); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public FenceItem(string path, int iconPixels)
    {
        Path = path;
        Name = ShellIcons.GetDisplayName(path);
        _ = LoadIconAsync(iconPixels);
    }

    private async Task LoadIconAsync(int pixels)
    {
        Icon = await ShellIcons.GetIconAsync(Path, pixels);
    }
}
