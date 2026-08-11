using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Brush = System.Windows.Media.Brush;
using Point = System.Windows.Point;


namespace CSharp_ImGui_Client
{
    public partial class LoginWindow : Window
    {
        private bool _licenseMode = true;
        private bool _busy;

        public LoginWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => ShowLicenseMode();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TabLicenseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            ShowLicenseMode();
        }

        private void TabAccountBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            ShowAccountMode();
        }

        private void ShowLicenseMode()
        {
            _licenseMode = true;
            LicensePanel.Visibility = Visibility.Visible;
            AccountPanel.Visibility = Visibility.Collapsed;
            TabLicenseBtn.Foreground = (Brush)FindResource("TextBrush");
            TabAccountBtn.Foreground = (Brush)FindResource("TextMutedBrush");

            // Slide indicator animation
            AnimateTabIndicator(0);
            StatusTxt.Text = "";
            LicenseBox.Focus();
        }

        private void ShowAccountMode()
        {
            _licenseMode = false;
            LicensePanel.Visibility = Visibility.Collapsed;
            AccountPanel.Visibility = Visibility.Visible;
            TabLicenseBtn.Foreground = (Brush)FindResource("TextMutedBrush");
            TabAccountBtn.Foreground = (Brush)FindResource("TextBrush");

            // Slide indicator animation
            double targetX = TabAccountBtn.TransformToAncestor(TabLicenseBtn.Parent as Visual)
                                .Transform(new Point(0, 0)).X;
            AnimateTabIndicator(targetX - 2); // adjustment for margins
            StatusTxt.Text = "";
            UserBox.Focus();
        }

        private void AnimateTabIndicator(double toValue)
        {
            var transform = TabIndicator.RenderTransform as TranslateTransform;
            if (transform == null)
            {
                transform = new TranslateTransform();
                TabIndicator.RenderTransform = transform;
            }
            var animation = new DoubleAnimation
            {
                To = toValue,
                Duration = TimeSpan.FromSeconds(0.18),
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };
            transform.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                _ = ExecuteLoginAsync();
            }
        }

        private async void LoginBtn_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteLoginAsync();
        }

        private async Task ExecuteLoginAsync()
        {
            if (_busy) return;

            _busy = true;
            LoginBtn.IsEnabled = false;
            LoginBtn.Content = "AUTHENTICATING...";
            StatusTxt.Foreground = (Brush)FindResource("TextMutedBrush");
            StatusTxt.Text = "Establishing connection...";

            // Disable fields
            LicenseBox.IsEnabled = false;
            UserBox.IsEnabled = false;
            PassBox.IsEnabled = false;

            try
            {
                (bool ok, string message) result;

                if (_licenseMode)
                {
                    string license = LicenseBox.Text;
                    result = await Task.Run(() => KeyAuthAppService.LoginWithLicense(license));
                }
                else
                {
                    string username = UserBox.Text;
                    string password = PassBox.Password;
                    result = await Task.Run(() => KeyAuthAppService.LoginWithCredentials(username, password));
                }

                if (result.ok)
                {
                    StatusTxt.Foreground = (Brush)FindResource("AccentGreen");
                    StatusTxt.Text = result.message;
                    await Task.Delay(500);
                    DialogResult = true;
                }
                else
                {
                    StatusTxt.Foreground = (Brush)FindResource("AccentRed");
                    StatusTxt.Text = result.message;
                    EnableForm();
                }
            }
            catch (Exception ex)
            {
                StatusTxt.Foreground = (Brush)FindResource("AccentRed");
                StatusTxt.Text = ex.Message;
                EnableForm();
            }
        }

        private void EnableForm()
        {
            LoginBtn.Content = "ENTER TANISH REGEDIT";
            LoginBtn.IsEnabled = true;
            _busy = false;
            LicenseBox.IsEnabled = true;
            UserBox.IsEnabled = true;
            PassBox.IsEnabled = true;
        }
    }
}
