using System.Windows;
using Switchly.Application.Security;

namespace Switchly.App.Views;

/// <summary>Local master-password unlock prompt shown before workspace launch (UI-010, REQ-010, TEST-011).</summary>
public partial class UnlockDialog : Window
{
    private readonly ILocalSecurityService _securityService;

    public UnlockDialog(ILocalSecurityService securityService)
    {
        InitializeComponent();
        _securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));
        Loaded += (_, _) => MasterPasswordBox.Focus();
    }

    private void OnUnlockClick(object sender, RoutedEventArgs e)
    {
        var password = MasterPasswordBox.Password;
        if (_securityService.VerifyMasterPassword(password))
        {
            MasterPasswordBox.Clear();
            DialogResult = true;
            Close();
            return;
        }

        // Generic error message — never leak secret or crypto details (TEST-011, TEST-022).
        ErrorText.Text = "Kata sandi utama tidak cocok. Silakan coba lagi.";
        ErrorText.Visibility = Visibility.Visible;
        MasterPasswordBox.Clear();
        MasterPasswordBox.Focus();
    }
}
