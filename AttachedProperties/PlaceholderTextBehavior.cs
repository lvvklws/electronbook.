// #13 ИСПРАВЛЕНО: Плейсхолдер реализован через Attached Property
// вместо ручных обработчиков GotFocus/LostFocus (которые ненадёжны при программном сбросе фокуса)
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Computer_networks.AttachedProperties
{
    public static class PlaceholderTextBehavior
    {
        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.RegisterAttached(
                "Placeholder",
                typeof(string),
                typeof(PlaceholderTextBehavior),
                new PropertyMetadata(string.Empty, OnPlaceholderChanged));

        public static string GetPlaceholder(DependencyObject obj)
            => (string)obj.GetValue(PlaceholderProperty);

        public static void SetPlaceholder(DependencyObject obj, string value)
            => obj.SetValue(PlaceholderProperty, value);

        private static void OnPlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBox tb) return;
            tb.GotFocus -= OnGotFocus;
            tb.LostFocus -= OnLostFocus;
            tb.GotFocus += OnGotFocus;
            tb.LostFocus += OnLostFocus;
            ApplyPlaceholder(tb);
        }

        private static void OnGotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox tb) return;
            string ph = GetPlaceholder(tb);
            if (tb.Text == ph)
            {
                tb.Text = string.Empty;
                tb.Foreground = SystemColors.ControlTextBrush;
            }
        }

        private static void OnLostFocus(object sender, RoutedEventArgs e)
            => ApplyPlaceholder(sender as TextBox);

        private static void ApplyPlaceholder(TextBox tb)
        {
            if (tb == null) return;
            string ph = GetPlaceholder(tb);
            if (string.IsNullOrWhiteSpace(tb.Text))
            {
                tb.Text = ph;
                tb.Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 160));
            }
        }
    }
}