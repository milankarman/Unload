using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace unload;

public static partial class TextBoxValidator
{
    // Ensures a textbox only allows integer inputs
    public static void ClampInteger(TextBox textBox, int minValue, int maxValue, string formatting = "")
    {
        if (!string.IsNullOrEmpty(textBox.Text))
        {
            // Only digits get past ForceInteger, so text that doesn't parse is too large for an int
            int value = Math.Clamp(GetInteger(textBox, maxValue), minValue, maxValue);
            textBox.Text = value.ToString(formatting);
        }
    }

    // Ensures a textbox only allows double inputs
    public static void ClampDouble(TextBox textBox, double minValue, double maxValue, string formatting = "")
    {
        if (!string.IsNullOrEmpty(textBox.Text))
        {
            double value = Math.Clamp(GetDouble(textBox, minValue), minValue, maxValue);
            textBox.Text = value.ToString(formatting);
        }
    }

    // Returns the text box's value, or the fallback when its text isn't a valid number (empty, a lone ".", too large)
    public static int GetInteger(TextBox textBox, int fallback) =>
        int.TryParse(textBox.Text, out int value) ? value : fallback;

    // Returns the text box's value, or the fallback when its text isn't a valid number (empty, a lone ".")
    public static double GetDouble(TextBox textBox, double fallback) =>
        double.TryParse(textBox.Text, out double value) ? value : fallback;

    // Prevents anything other than a round number from being typed or pasted in the text box
    public static void ForceInteger(TextBox textBox) =>
        RestrictInput(textBox, IntegerPattern(), input => input, text => int.TryParse(text, out _));

    // Prevents anything other than any number from being typed or pasted in the text box.
    // "," is turned into "." since the numpad decimal key types "," on some keyboard layouts.
    public static void ForceDouble(TextBox textBox) =>
        RestrictInput(textBox, DecimalPattern(), input => input.Replace(',', '.'), text => double.TryParse(text, out _));

    private static void RestrictInput(TextBox textBox, Regex pattern, Func<string, string> normalize, Func<string, bool> isValid)
    {
        textBox.PreviewTextInput += (_, e) =>
        {
            string input = normalize(e.Text);
            if (!pattern.IsMatch(WithSelectionReplaced(textBox, input)))
            {
                e.Handled = true;
            }
            else if (input != e.Text)
            {
                e.Handled = true;
                int start = textBox.SelectionStart;
                textBox.SelectedText = input;
                textBox.Select(start + input.Length, 0);
            }
        };

        // Pasting and dropping text don't go through PreviewTextInput
        DataObject.AddPastingHandler(textBox, (_, e) =>
        {
            if (e.DataObject.GetData(DataFormats.UnicodeText) is not string text)
            {
                e.CancelCommand();
                return;
            }

            string input = normalize(text.Trim());
            // Dropped text lands under the mouse rather than on the selection, so only the dropped text can be checked
            string result = e.IsDragDrop ? input : WithSelectionReplaced(textBox, input);
            if (!pattern.IsMatch(result))
            {
                e.CancelCommand();
            }
            else if (input != text)
            {
                e.DataObject = new DataObject(DataFormats.UnicodeText, input);
            }
        });

        // Restores the last valid number when focus leaves the text box empty or holding a lone "."
        string lastValidText = textBox.Text;
        textBox.TextChanged += (_, _) =>
        {
            if (isValid(textBox.Text))
            {
                lastValidText = textBox.Text;
            }
        };
        textBox.LostFocus += (_, _) =>
        {
            if (!isValid(textBox.Text))
            {
                textBox.Text = lastValidText;
            }
        };
    }

    private static string WithSelectionReplaced(TextBox textBox, string input) =>
        textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength).Insert(textBox.SelectionStart, input);

    [GeneratedRegex(@"^[0-9]*\z")]
    private static partial Regex IntegerPattern();

    [GeneratedRegex(@"^[0-9]*\.?[0-9]*\z")]
    private static partial Regex DecimalPattern();
}
