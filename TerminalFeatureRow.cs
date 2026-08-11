using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using CheckBox = System.Windows.Controls.CheckBox;
using Button = System.Windows.Controls.Button;
using Brushes = System.Windows.Media.Brushes;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Cursors = System.Windows.Input.Cursors;

namespace CSharp_ImGui_Client
{
    public class TerminalFeatureRow : Border
    {
        private readonly Grid _grid;
        private readonly CheckBox _toggle;
        private readonly TextBlock _label;
        private readonly TextBlock _subtitle;

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get => _toggle.IsChecked ?? false;
            set
            {
                if (_toggle.IsChecked != value)
                {
                    _toggle.IsChecked = value;
                }
            }
        }

        public string LabelText
        {
            get => _label.Text;
            set => _label.Text = value;
        }

        public string SubtitleText
        {
            get => _subtitle.Text;
            set
            {
                _subtitle.Text = value;
                _subtitle.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
            }
        }



        public TerminalFeatureRow()
        {
            Height = 54;
            Margin = new Thickness(0, 4, 0, 4);
            CornerRadius = new CornerRadius(6);
            Background = new SolidColorBrush(Color.FromRgb(12, 14, 18)); // Dark, almost black background
            BorderBrush = new SolidColorBrush(Color.FromRgb(25, 30, 38));
            BorderThickness = new Thickness(1);

            _grid = new Grid();
            _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });

            // Apply hover background effect
            MouseEnter += (s, e) => Background = new SolidColorBrush(Color.FromRgb(20, 24, 30));
            MouseLeave += (s, e) => { Background = new SolidColorBrush(Color.FromRgb(12, 14, 18)); };

            // Child 1: Toggle CheckBox
            _toggle = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 10, 0)
            };
            _toggle.Loaded += (s, e) =>
            {
                if (TryFindResource("PremiumSwitch") is Style switchStyle)
                    _toggle.Style = switchStyle;
            };
            _toggle.Checked += (s, e) => { CheckedChanged?.Invoke(this, EventArgs.Empty); Background = new SolidColorBrush(Color.FromArgb(40, 0, 229, 255)); };
            _toggle.Unchecked += (s, e) => { CheckedChanged?.Invoke(this, EventArgs.Empty); Background = new SolidColorBrush(Color.FromArgb(12, 0, 229, 255)); };
            _grid.Children.Add(_toggle);
            Grid.SetColumn(_toggle, 2);

            // Left StackPanel for Title and Subtitle
            var textStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };

            // Title
            _label = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Segoe UI"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 2)
            };
            
            // Subtitle
            _subtitle = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0, 229, 255)), // Cyan subtitle
                FontSize = 9.5,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Segoe UI"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Visibility = Visibility.Collapsed
            };

            _label.Loaded += (s, e) =>
            {
                if (TryFindResource("TextBrush") is SolidColorBrush brush) _label.Foreground = brush;
                if (TryFindResource("MainFont") is FontFamily font) { _label.FontFamily = font; _subtitle.FontFamily = font; }
            };

            textStack.Children.Add(_label);
            textStack.Children.Add(_subtitle);
            _grid.Children.Add(textStack);
            Grid.SetColumn(textStack, 0);

            // Click to toggle
            MouseLeftButtonDown += (s, e) =>
            {
                Checked = !Checked;
                e.Handled = true;
            };

            Child = _grid;
        }
    }
}
