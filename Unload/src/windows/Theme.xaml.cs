using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Themes;

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

    // Attached in code because Slider already handled the press:
    // an EventSetter then skips it, a class handler runs too early
    private void Slider_Loaded(object sender, RoutedEventArgs e)
    {
        Slider slider = (Slider)sender;
        MouseButtonEventHandler handler = Slider_PreviewMouseLeftButtonDown;
        // Loaded can fire more than once, remove first to avoid attaching twice
        slider.RemoveHandler(UIElement.PreviewMouseLeftButtonDownEvent, handler);
        slider.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, handler, handledEventsToo: true);
    }

    // IsMoveToPointEnabled handles the press, so the thumb never starts dragging. Forward it to the thumb.
    private void Slider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Slider slider = (Slider)sender;

        if (!e.Handled
            || !slider.IsMoveToPointEnabled
            || slider.Template?.FindName("PART_Track", slider) is not Track { Thumb: Thumb thumb }
            || thumb.IsMouseOver
            || thumb.IsDragging)
        {
            return;
        }

        // Move the thumb first, or the drag starts from its old position
        thumb.UpdateLayout();
        thumb.RaiseEvent(new MouseButtonEventArgs(e.MouseDevice, e.Timestamp, MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseLeftButtonDownEvent,
        });
    }
}
