using System.Windows;

namespace Themes
{
    public partial class Theme
    {
        private void CloseWindow_Event(object sender, RoutedEventArgs e)
        {
            Window window = Window.GetWindow((FrameworkElement)e.Source);
            window.Close();
        }

        private void AutoMinimize_Event(object sender, RoutedEventArgs e)
        {
            Window window = Window.GetWindow((FrameworkElement)e.Source);

            if (window.WindowState == WindowState.Maximized)
            {
                window.WindowState = WindowState.Normal;
            }
            else if (window.WindowState == WindowState.Normal)
            {
                window.WindowState = WindowState.Maximized;
            }
        }

        private void Minimize_Event(object sender, RoutedEventArgs e)
        {
            Window window = Window.GetWindow((FrameworkElement)e.Source);
            window.WindowState = WindowState.Minimized;
        }
    }
}
