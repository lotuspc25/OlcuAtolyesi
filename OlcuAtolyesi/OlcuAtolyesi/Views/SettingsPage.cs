using OlcuAtolyesi.Controls;
using OlcuAtolyesi.Models;
using OlcuAtolyesi.Resources.Strings;
using OlcuAtolyesi.Services;

namespace OlcuAtolyesi.Views;

public sealed class SettingsPage : PrototypePage
{
    private readonly SettingsService _settings;
    private readonly Label _savedMessage;

    public SettingsPage() : base(AppText.Tabs.Settings, "general")
    {
        _settings = App.GetService<SettingsService>();
        _savedMessage = ViewFactory.Caption(string.Empty);
        _savedMessage.IsVisible = false;

        var themeStack = ViewFactory.Stack(2);
        themeStack.Add(CreateThemeRadio(AppText.Settings.Light, ThemePreference.Light));
        themeStack.Add(CreateThemeRadio(AppText.Settings.Dark, ThemePreference.Dark));
        themeStack.Add(CreateThemeRadio(AppText.Settings.System, ThemePreference.System));

        var unitPicker = new Picker
        {
            Title = AppText.Settings.DefaultUnit,
            ItemsSource = Enum.GetValues<LengthUnit>().Select(unit => $"{unit.DisplayName()} ({unit.Symbol()})").ToList(),
            SelectedIndex = (int)_settings.DefaultLengthUnit,
            HorizontalOptions = LayoutOptions.Fill
        };
        SemanticProperties.SetDescription(unitPicker, AppText.Settings.DefaultUnit);
        unitPicker.SelectedIndexChanged += (_, _) =>
        {
            if (unitPicker.SelectedIndex >= 0)
            {
                _settings.SetDefaultLengthUnit((LengthUnit)unitPicker.SelectedIndex);
                ShowSavedMessage();
            }
        };

        var appearance = ViewFactory.Stack(8);
        appearance.Add(ViewFactory.SectionTitle(AppText.Settings.Theme));
        appearance.Add(themeStack);

        var measurement = ViewFactory.Stack(8);
        measurement.Add(ViewFactory.SectionTitle(AppText.Settings.DefaultUnit));
        measurement.Add(unitPicker);
        measurement.Add(ViewFactory.InfoCard(AppText.Settings.Calibration, AppText.Settings.CalibrationStatus, "ruler.png", "WarningCardStyle"));

        var information = ViewFactory.Stack(8);
        information.Add(ViewFactory.SecondaryButton(AppText.Help, async (_, _) =>
            await Shell.Current.GoToAsync($"{nameof(HelpPage)}?topic=general")));
        information.Add(ViewFactory.SecondaryButton(AppText.Settings.About, async (_, _) =>
            await Shell.Current.GoToAsync(nameof(AboutPage))));
        information.Add(ViewFactory.Caption($"{AppText.Settings.VersionLabel}: {AppText.Version}"));

        var stack = ViewFactory.Stack(12);
        stack.Add(ViewFactory.SectionTitle(AppText.Settings.Appearance));
        stack.Add(ViewFactory.Card(appearance));
        stack.Add(ViewFactory.SectionTitle(AppText.Settings.Measurement));
        stack.Add(ViewFactory.Card(measurement));
        stack.Add(_savedMessage);
        stack.Add(ViewFactory.SectionTitle(AppText.Settings.Information));
        stack.Add(ViewFactory.Card(information));

        Content = PageBody(ViewFactory.Scroll(stack));
    }

    private RadioButton CreateThemeRadio(string text, ThemePreference value)
    {
        var radio = new RadioButton
        {
            Content = text,
            Value = value,
            GroupName = "themePreference",
            IsChecked = _settings.Theme == value,
            MinimumHeightRequest = 48
        };
        SemanticProperties.SetDescription(radio, $"{text} tema");
        radio.CheckedChanged += (_, args) =>
        {
            if (args.Value)
            {
                _settings.SetTheme(value);
                ShowSavedMessage();
            }
        };
        return radio;
    }

    private void ShowSavedMessage()
    {
        _savedMessage.Text = AppText.Settings.PreferenceSaved;
        _savedMessage.IsVisible = true;
        SemanticScreenReader.Announce(AppText.Settings.PreferenceSaved);
    }
}
