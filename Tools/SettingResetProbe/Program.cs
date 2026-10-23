using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SekaiToolsGUI.Model;
using SekaiToolsGUI.View.Setting;
using SekaiToolsGUI.View.Setting.Components;
using SekaiToolsGUI.ViewModel.Setting;
using Wpf.Ui.Controls;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(output);
        var app = new Application();
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary());
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/SekaiToolsGUI;component/Converter/Converters.xaml", UriKind.Relative) });
        var path = Path.Combine(output, "settings.json");
        File.Delete(path);
        var constructor = typeof(SettingPageModel).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(string)], null)!;
        var model = (SettingPageModel)constructor.Invoke([path]);
        var pageConstructor = typeof(SettingPage).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(SettingPageModel)], null)!;
        var page = (SettingPage)pageConstructor.Invoke([model]);
        var window = new Window { Content = page, Width = 1100, Height = 850, Left = -15000, Top = -15000, ShowActivated = false, ShowInTaskbar = false };
        window.Show(); Pump();
        foreach (var expander in Nodes(page).OfType<CardExpander>()) expander.IsExpanded = true;
        Pump();
        var buttons = Nodes(page).OfType<SettingResetButton>().ToArray();
        Require(buttons.Length == 20, $"Expected 20 reset buttons, got {buttons.Length}");
        foreach (var button in buttons)
        {
            Require(button.Visibility == Visibility.Collapsed, "Default unexpectedly modified: " + button.Properties);
            var names = button.Properties.Split(',');
            var property = typeof(SettingPageModel).GetProperty(names[0])!;
            if (property.Name == nameof(model.RecognitionPeriodicColor) && !model.IsPeriodicColorAvailable) continue;
            var initial = property.GetValue(model)!;
            property.SetValue(model, initial switch { bool b => !b, int number => number + 1, string text => text + " changed", _ => throw new Exception() });
            Pump();
            Require(button.Visibility == Visibility.Visible, "Modified button hidden: " + button.Properties);
            var untouched = typeof(Setting).GetProperties().Where(p => typeof(SettingPageModel).GetProperty(p.Name)?.CanWrite == true && !names.Contains(p.Name))
                .ToDictionary(p => p.Name, p => typeof(SettingPageModel).GetProperty(p.Name)!.GetValue(model));
            button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Pump();
            Require(!model.ArePropertiesModified(names) && button.Visibility == Visibility.Collapsed, "Reset failed: " + button.Properties);
            foreach (var pair in untouched) Require(Equals(pair.Value, typeof(SettingPageModel).GetProperty(pair.Key)!.GetValue(model)), "Unrelated setting changed: " + pair.Key);
            var saved = Setting.Load(path);
            foreach (var name in names) Require(Equals(typeof(Setting).GetProperty(name)!.GetValue(saved), typeof(Setting).GetProperty(name)!.GetValue(Setting.Default)), "Reset not saved: " + name);
        }
        model.TypewriterCharTime = 90;
        Pump();
        var undo = buttons.Single(button => button.Properties.Contains("TypewriterCharTime"));
        Require(undo.BorderThickness == new Thickness(0) && undo.Margin.Left == 8, "Inline reset chrome/spacing incorrect");
        Require(undo.Content is SymbolIcon { Symbol: SymbolRegular.ArrowUndo24 }, "Wrong undo icon");
        Require(Nodes(undo).All(node => node is not Border), "Inline undo still contains a border");
        VerifyConfigurationChanges(constructor, output);
        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Dark);
        Pump();
        Require(((SymbolIcon)undo.Content).Foreground == app.FindResource("AccentTextFillColorPrimaryBrush"), "Undo does not follow dark accent resource");
        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Light);
        Pump();
        Require(((SymbolIcon)undo.Content).Foreground == app.FindResource("AccentTextFillColorPrimaryBrush"), "Undo does not follow light accent resource");
        var animationFrame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        timer.Tick += (_, _) => { timer.Stop(); animationFrame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(animationFrame);
        var bitmap = new RenderTargetBitmap((int)page.ActualWidth, (int)page.ActualHeight, 96,96,PixelFormats.Pbgra32);
        bitmap.Render(page);
        var encoder = new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = File.Create(Path.Combine(output,"settings.png"))) encoder.Save(file);
        Console.WriteLine($"PASS: {buttons.Length} cards, visibility, grouped reset, isolation, persistence; periodic available={model.IsPeriodicColorAvailable}");
        window.Close();app.Shutdown();
    }

    private static void VerifyConfigurationChanges(ConstructorInfo constructor, string output)
    {
        const string custom = """
        [{"title":"配置生成的分组","icon":"Settings24","children":[
          {"title":"配置生成的选项","icon":"Play24","fields":[
            {"kind":"choice","property":"PreviewPlaybackBackend","options":["自定义 A","自定义 B","自定义 C"],"width":310}]},
          {"title":"配置生成的数值","icon":"Memory16","fields":[
            {"kind":"number","property":"RecognitionMemoryBudgetMiB","minimum":128,"maximum":4096,"smallChange":128,"width":180}]}]}]
        """;
        var section = SettingCatalog.Parse(custom).Single();
        Require(section.Properties.Order().SequenceEqual(new[] { "PreviewPlaybackBackend", "RecognitionMemoryBudgetMiB" }.Order()), "Derived reset group incorrect");
        var built = new SettingViewBuilder(_ => Task.FromResult(""), _ => throw new InvalidOperationException()).Build(section);
        var variantPath = Path.Combine(output,"config-variant.json");
        File.Delete(variantPath);
        var model = (SettingPageModel)constructor.Invoke([variantPath]);
        built.DataContext = model;
        var expander = (CardExpander)built;
        Require(!expander.IsExpanded, "Groups must default collapsed");
        expander.IsExpanded = true;
        var window = new Window { Content = built, Width = 900, Height = 400, Left = -15000, Top = -15000, ShowActivated = false, ShowInTaskbar = false };
        window.Show(); Pump();
        var combo = Nodes(built).OfType<ComboBox>().Single();
        Require(combo.Width == 310 && ((ComboBoxItem)combo.Items[1]).Content.Equals("自定义 B"), "Config did not update UI options/width");
        combo.SetCurrentValue(System.Windows.Controls.Primitives.Selector.SelectedIndexProperty, 2);
        Pump(); Require(model.PreviewPlaybackBackend == 2, "Config editor did not update model");
        var number = Nodes(built).OfType<NumberBox>().Single();
        Require(number.Minimum == 128 && number.Maximum == 4096 && number.SmallChange == 128, "Config numeric bounds ignored");
        foreach (var invalid in new[]
        {
            custom.Replace("PreviewPlaybackBackend", "MissingProperty"),
            custom.Replace("\"kind\":\"choice\"", "\"kind\":\"unknown\""),
            custom.Replace("\"icon\":\"Settings24\"", "\"icon\":\"MissingIcon\""),
            custom.Replace("\"maximum\":4096", "\"maximum\":1024")
        })
        {
            try { SettingCatalog.Parse(invalid); throw new Exception("Invalid config accepted"); }
            catch (InvalidDataException) { }
        }
        window.Close();
    }
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => {}, DispatcherPriority.ApplicationIdle);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private static IEnumerable<DependencyObject> Nodes(DependencyObject node)
    {
        yield return node;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            foreach (var child in Nodes(VisualTreeHelper.GetChild(node,i))) yield return child;
    }
}
