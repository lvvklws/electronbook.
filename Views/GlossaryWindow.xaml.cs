using System.Windows;
using System.Windows.Input;
using Computer_networks.ViewModels;

namespace Computer_networks.Views
{
    public partial class GlossaryWindow : Window
    {
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Key.F2)
            {
                Close();
                e.Handled = true;
            }
        }
        public GlossaryWindow()
        {
            InitializeComponent();
            DataContext = new GlossaryViewModel();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}