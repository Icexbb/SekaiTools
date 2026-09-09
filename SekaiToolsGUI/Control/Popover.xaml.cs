using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace SekaiToolsGUI.Control;

/// <summary>
/// 可承载交互内容的浮层。通过 Target 内嵌锚点，通过 IsOpen 控制显示。
/// </summary>
[TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
[TemplatePart(Name = "PART_Surface", Type = typeof(Border))]
public partial class Popover : ContentControl
{
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen), typeof(bool), typeof(Popover),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(
        nameof(Target), typeof(UIElement), typeof(Popover), new PropertyMetadata(null, OnTargetChanged));

    public static readonly DependencyProperty PlacementTargetProperty = DependencyProperty.Register(
        nameof(PlacementTarget), typeof(UIElement), typeof(Popover),
        new FrameworkPropertyMetadata(null, null, (owner, value) => value ?? ((Popover)owner).Target));

    public static readonly DependencyProperty PlacementProperty = DependencyProperty.Register(
        nameof(Placement), typeof(PlacementMode), typeof(Popover), new PropertyMetadata(PlacementMode.Bottom),
        value => Enum.IsDefined((PlacementMode)value));

    public static readonly DependencyProperty HorizontalOffsetProperty = DependencyProperty.Register(
        nameof(HorizontalOffset), typeof(double), typeof(Popover), new PropertyMetadata(0d),
        value => double.IsFinite((double)value));

    public static readonly DependencyProperty VerticalOffsetProperty = DependencyProperty.Register(
        nameof(VerticalOffset), typeof(double), typeof(Popover), new PropertyMetadata(8d),
        value => double.IsFinite((double)value));

    public static readonly DependencyProperty StaysOpenProperty = DependencyProperty.Register(
        nameof(StaysOpen), typeof(bool), typeof(Popover), new PropertyMetadata(false));

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(CornerRadius), typeof(Popover), new PropertyMetadata(new CornerRadius(8)),
        value => value is CornerRadius radius
            && double.IsFinite(radius.TopLeft) && radius.TopLeft >= 0
            && double.IsFinite(radius.TopRight) && radius.TopRight >= 0
            && double.IsFinite(radius.BottomLeft) && radius.BottomLeft >= 0
            && double.IsFinite(radius.BottomRight) && radius.BottomRight >= 0);

    private Popup? _popup;
    private Border? _surface;
    private Window? _owner;
    private IInputElement? _previousFocus;

    public Popover()
    {
        InitializeComponent();
        Unloaded += (_, _) => Close();
    }

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>显示在普通布局中的目标控件，也是默认的浮层定位锚点。</summary>
    public UIElement? Target
    {
        get => (UIElement?)GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    /// <summary>可显式指定外部锚点；未指定时使用 Target。</summary>
    public UIElement? PlacementTarget
    {
        get => (UIElement?)GetValue(PlacementTargetProperty);
        set => SetValue(PlacementTargetProperty, value);
    }

    public PlacementMode Placement
    {
        get => (PlacementMode)GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    public double HorizontalOffset
    {
        get => (double)GetValue(HorizontalOffsetProperty);
        set => SetValue(HorizontalOffsetProperty, value);
    }

    public double VerticalOffset
    {
        get => (double)GetValue(VerticalOffsetProperty);
        set => SetValue(VerticalOffsetProperty, value);
    }

    /// <summary>为 false 时，点击浮层外部自动关闭。Esc 始终可以关闭浮层。</summary>
    public bool StaysOpen
    {
        get => (bool)GetValue(StaysOpenProperty);
        set => SetValue(StaysOpenProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public event EventHandler? Opened;
    public event EventHandler? Closed;

    public void Open() => SetCurrentValue(IsOpenProperty, true);

    public void Close() => SetCurrentValue(IsOpenProperty, false);

    protected override IEnumerator LogicalChildren
    {
        get
        {
            var children = new List<object>();
            var contentChildren = base.LogicalChildren;
            while (contentChildren.MoveNext())
                children.Add(contentChildren.Current);
            if (Target != null)
                children.Add(Target);
            return children.GetEnumerator();
        }
    }

    private static void OnTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var popover = (Popover)d;
        popover.Close();
        if (e.OldValue != null)
            popover.RemoveLogicalChild(e.OldValue);
        if (e.NewValue != null)
            popover.AddLogicalChild(e.NewValue);
        popover.CoerceValue(PlacementTargetProperty);
    }

    public override void OnApplyTemplate()
    {
        if (_popup != null)
        {
            _popup.SetCurrentValue(Popup.IsOpenProperty, false);
            _popup.Opened -= OnOpened;
            _popup.Closed -= OnClosed;
        }
        if (_surface != null)
            _surface.PreviewKeyDown -= OnPreviewKeyDown;
        DetachOwner();

        base.OnApplyTemplate();
        _popup = GetTemplateChild("PART_Popup") as Popup;
        _surface = GetTemplateChild("PART_Surface") as Border;
        if (_popup != null)
        {
            _popup.Opened += OnOpened;
            _popup.Closed += OnClosed;
        }
        if (_surface != null)
            _surface.PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        DetachOwner();
        _owner = Window.GetWindow(PlacementTarget ?? this);
        if (_owner != null)
        {
            // Popup 使用独立窗口，宿主移动或失焦时关闭，避免浮层悬留。
            _owner.Deactivated += OnOwnerChanged;
            _owner.LocationChanged += OnOwnerChanged;
            _owner.SizeChanged += OnOwnerSizeChanged;
            _owner.AddHandler(Mouse.PreviewMouseDownEvent,
                new MouseButtonEventHandler(OnOwnerMouseDown), true);
        }
        _previousFocus = Keyboard.FocusedElement;
        _surface?.Focus();
        _surface?.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        Opened?.Invoke(this, EventArgs.Empty);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        DetachOwner();
        SetCurrentValue(IsOpenProperty, false);
        // 点击外部已转移焦点时，不抢回焦点。
        if (_surface?.IsKeyboardFocusWithin == true)
            Keyboard.Focus(_previousFocus);
        _previousFocus = null;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;
        Close();
        e.Handled = true;
    }

    private void OnOwnerChanged(object? sender, EventArgs e) => Close();

    private void OnOwnerSizeChanged(object sender, SizeChangedEventArgs e) => Close();

    private void OnOwnerMouseDown(object sender, MouseButtonEventArgs e)
    {
        // 补充 Popup 的鼠标捕获关闭机制，处理内容控件释放或转移捕获后的外部点击。
        if (IsOpen && !StaysOpen && _surface?.IsMouseOver != true)
            Close();
    }

    private void DetachOwner()
    {
        if (_owner == null)
            return;
        _owner.Deactivated -= OnOwnerChanged;
        _owner.LocationChanged -= OnOwnerChanged;
        _owner.SizeChanged -= OnOwnerSizeChanged;
        _owner.RemoveHandler(Mouse.PreviewMouseDownEvent,
            new MouseButtonEventHandler(OnOwnerMouseDown));
        _owner = null;
    }
}
