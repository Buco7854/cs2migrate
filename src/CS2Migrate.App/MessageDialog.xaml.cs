using System.Windows;
using System.Windows.Input;

namespace CS2Migrate.App;

internal enum DialogChoice
{
    Primary,
    Secondary,
    Cancel
}

public partial class MessageDialog : Window
{
    private DialogChoice _choice = DialogChoice.Cancel;

    private MessageDialog()
    {
        InitializeComponent();
        ThemeService.RegisterWindow(this);
    }

    /// <summary>A yes/no question. Returns true only when the primary action was chosen.</summary>
    internal static bool Confirm(Window owner, string title, string message, string primaryLabel) =>
        Show(owner, title, message, primaryLabel, secondaryLabel: null) == DialogChoice.Primary;

    /// <summary>A question with two ways to say yes, plus cancel.</summary>
    internal static DialogChoice Ask(
        Window owner,
        string title,
        string message,
        string primaryLabel,
        string secondaryLabel) =>
        Show(owner, title, message, primaryLabel, secondaryLabel);

    internal static void Inform(Window owner, string title, string message)
    {
        var dialog = Create(owner, title, message, LanguageService.Text("CloseButton"), secondaryLabel: null);
        dialog.CancelButton.Visibility = Visibility.Collapsed;
        dialog.ShowDialog();
    }

    private static DialogChoice Show(
        Window owner,
        string title,
        string message,
        string primaryLabel,
        string? secondaryLabel)
    {
        var dialog = Create(owner, title, message, primaryLabel, secondaryLabel);
        dialog.ShowDialog();
        return dialog._choice;
    }

    private static MessageDialog Create(
        Window owner,
        string title,
        string message,
        string primaryLabel,
        string? secondaryLabel)
    {
        var dialog = new MessageDialog { Owner = owner };
        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;
        dialog.PrimaryButton.Content = primaryLabel;
        dialog.CancelButton.Content = LanguageService.Text("CancelButton");
        if (secondaryLabel is not null)
        {
            dialog.SecondaryButton.Content = secondaryLabel;
            dialog.SecondaryButton.Visibility = Visibility.Visible;
        }

        return dialog;
    }

    private void Primary_Click(object sender, RoutedEventArgs e) => Close(DialogChoice.Primary);

    private void Secondary_Click(object sender, RoutedEventArgs e) => Close(DialogChoice.Secondary);

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close(DialogChoice.Cancel);

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close(DialogChoice.Cancel);
        }
        else if (e.Key == Key.Enter)
        {
            Close(DialogChoice.Primary);
        }
    }

    private void Close(DialogChoice choice)
    {
        _choice = choice;
        DialogResult = choice != DialogChoice.Cancel;
    }
}
