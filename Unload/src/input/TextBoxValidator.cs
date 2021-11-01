using System;
using System.Windows.Input;
using System.Windows.Controls;
using System.Text.RegularExpressions;

namespace unload
{
    public static class TextBoxValidator
    {
        // Ensures a textbox only allows integer inputs
        public static void ClampInteger(TextBox textBox, int minValue, int maxValue, string formatting = "")
        {
            if (!string.IsNullOrEmpty(textBox.Text))
            {
                int value = Math.Clamp(int.Parse(textBox.Text), minValue, maxValue);
                textBox.Text = value.ToString(formatting);
            }
        }

        // Ensures a textbox only allows double inputs
        public static void ClampDouble(TextBox textBox, double minValue, double maxValue, string formatting = "")
        {
            if (!string.IsNullOrEmpty(textBox.Text))
            {
                double value = Math.Clamp(double.Parse(textBox.Text), minValue, maxValue);
                textBox.Text = value.ToString(formatting);
            }
        }

        // When applied to a text box this prevents anything other than a round number to be entered
        public static void ForceInteger(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        // When applied to a text box this prevents anything other than any number to be entered
        public static void ForceDouble(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9.,]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}
