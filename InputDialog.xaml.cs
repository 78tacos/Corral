using System.Windows;

namespace Corral;

public partial class InputDialog : Window
{
    private InputDialog(string title, string prompt, string initial)
    {
        InitializeComponent();
        Title = title;
        Prompt.Text = prompt;
        Input.Text = initial;
        Loaded += (_, _) =>
        {
            Activate();
            Input.Focus();
            Input.SelectAll();
        };
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    public static string? Ask(string title, string prompt, string initial)
    {
        var dlg = new InputDialog(title, prompt, initial);
        return dlg.ShowDialog() == true ? dlg.Input.Text : null;
    }
}
