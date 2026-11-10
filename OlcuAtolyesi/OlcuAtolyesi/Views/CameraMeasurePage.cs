using OlcuAtolyesi.Controls;
using OlcuAtolyesi.Drawing;
using OlcuAtolyesi.Models;
using OlcuAtolyesi.Resources.Strings;
using OlcuAtolyesi.Services;

namespace OlcuAtolyesi.Views;

public sealed class CameraMeasurePage : PrototypePage
{
    private readonly SettingsService _settings;
    private readonly CameraSceneDrawable _drawable = new();
    private readonly GraphicsView _graphics;
    private readonly Label _result;

    public CameraMeasurePage() : base(AppText.Camera.Title, "camera")
    {
        _settings = App.GetService<SettingsService>();
        _graphics = new GraphicsView
        {
            Drawable = _drawable,
            HeightRequest = 310,
            MinimumHeightRequest = 250
        };
        SemanticProperties.SetDescription(_graphics, AppText.Camera.SceneDescription);

        _result = ViewFactory.StyledLabel(string.Empty, "ResultStyle", SemanticHeadingLevel.Level2);

        var steps = ViewFactory.Stack(8);
        steps.Add(Step(AppText.Camera.StepOne));
        steps.Add(Step(AppText.Camera.StepTwo));
        steps.Add(Step(AppText.Camera.StepThree));

        var stack = ViewFactory.Stack(12);
        stack.Add(ViewFactory.InfoCard(AppText.DemoWarning, AppText.Camera.Intro));
        stack.Add(_graphics);
        stack.Add(ViewFactory.Secondary(AppText.Camera.ExampleResult));
        stack.Add(_result);
        stack.Add(ViewFactory.SectionTitle("Kısa kullanım"));
        stack.Add(steps);
        stack.Add(ViewFactory.InfoCard("Kamera ve AR durumu", AppText.Camera.Notice, "camera.png", "WarningCardStyle"));

        Content = PageBody(ViewFactory.Scroll(stack));
        UpdateResult();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateResult();
        _graphics.Invalidate();
    }

    private static Border Step(string text)
    {
        var label = ViewFactory.Body(text);
        return ViewFactory.Card(label);
    }

    private void UpdateResult()
    {
        var length = new LengthValue(1800, LengthUnit.Millimeter)
            .ConvertTo(_settings.DefaultLengthUnit)
            .Format();
        _drawable.ResultText = length;
        _result.Text = $"{AppText.DemoWarning} • {length}";
        SemanticProperties.SetDescription(_result, $"{AppText.Camera.ExampleResult}: {length}. Gerçek ölçüm değildir.");
    }
}
