namespace PasswordManager.Services;

/// <summary>Highlights a "Field" border with the accent color while its input has focus.</summary>
public static class FieldFocus
{
    public static void Attach(View input, Border field)
    {
        input.Focused += (_, _) =>
        {
            field.SetDynamicResource(Border.StrokeProperty, "Accent");
            field.StrokeThickness = 2;
        };
        input.Unfocused += (_, _) =>
        {
            field.SetDynamicResource(Border.StrokeProperty, "Border");
            field.StrokeThickness = 1;
        };
    }

    /// <summary>Sets a strength bar (4 BoxViews + label) for a password.</summary>
    public static void ShowStrength(string password, BoxView[] bars, Label label)
    {
        int score = string.IsNullOrEmpty(password) ? 0 : Strength.Score(password);
        for (int i = 0; i < bars.Length; i++)
            bars[i].SetDynamicResource(BoxView.ColorProperty, i < score ? Strength.BarKey(score) : "StrengthEmpty");

        label.Text = score > 0 ? $"{Strength.Label(score)} · {password.Length} belgi" : " ";
        label.SetDynamicResource(Label.TextColorProperty, score > 0 ? Strength.ColorKey(score) : "TextMuted");
    }
}
