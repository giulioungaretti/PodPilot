using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace GUI.Controls;

/// <summary>
/// A custom control that displays a pulsating dot animation.
/// Used as a modern, subtle scanning indicator.
/// </summary>
public sealed partial class PulsatingDot : UserControl
{
    private Storyboard? _pulsationStoryboard;

    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(
            nameof(IsActive),
            typeof(bool),
            typeof(PulsatingDot),
            new PropertyMetadata(false, OnIsActiveChanged));

    public static readonly DependencyProperty SizeProperty =
        DependencyProperty.Register(
            nameof(Size),
            typeof(double),
            typeof(PulsatingDot),
            new PropertyMetadata(20.0));

    public static readonly DependencyProperty DotBrushProperty =
        DependencyProperty.Register(
            nameof(DotBrush),
            typeof(Brush),
            typeof(PulsatingDot),
            new PropertyMetadata(null));

    /// <summary>
    /// Gets or sets whether the pulsating animation is active.
    /// </summary>
    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>
    /// Gets or sets the size of the control in pixels.
    /// </summary>
    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush used for the pulsating dot.
    /// If not set, uses the theme's AccentFillColorDefaultBrush.
    /// </summary>
    public Brush DotBrush
    {
        get => (Brush)GetValue(DotBrushProperty) ?? (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        set => SetValue(DotBrushProperty, value);
    }

    public PulsatingDot()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        CreatePulsationAnimation();
        if (IsActive)
        {
            StartAnimation();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopAnimation();
    }

    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PulsatingDot control)
        {
            if ((bool)e.NewValue)
            {
                control.StartAnimation();
            }
            else
            {
                control.StopAnimation();
            }
        }
    }

    private void CreatePulsationAnimation()
    {
        _pulsationStoryboard = new Storyboard
        {
            RepeatBehavior = RepeatBehavior.Forever
        };

        // Outer ring animation - scales from 1 to 1.8 and fades out
        var outerScaleAnimation = new DoubleAnimation
        {
            From = 1.0,
            To = 1.8,
            Duration = new Duration(TimeSpan.FromSeconds(2.0)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(outerScaleAnimation, OuterRingScale);
        Storyboard.SetTargetProperty(outerScaleAnimation, "ScaleX");
        _pulsationStoryboard.Children.Add(outerScaleAnimation);

        var outerScaleYAnimation = new DoubleAnimation
        {
            From = 1.0,
            To = 1.8,
            Duration = new Duration(TimeSpan.FromSeconds(2.0)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(outerScaleYAnimation, OuterRingScale);
        Storyboard.SetTargetProperty(outerScaleYAnimation, "ScaleY");
        _pulsationStoryboard.Children.Add(outerScaleYAnimation);

        var outerOpacityAnimation = new DoubleAnimation
        {
            From = 0.3,
            To = 0.0,
            Duration = new Duration(TimeSpan.FromSeconds(2.0)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(outerOpacityAnimation, OuterRing);
        Storyboard.SetTargetProperty(outerOpacityAnimation, "Opacity");
        _pulsationStoryboard.Children.Add(outerOpacityAnimation);

        // Middle ring animation - scales from 1 to 1.5 and fades out, slightly delayed
        var middleScaleAnimation = new DoubleAnimation
        {
            From = 1.0,
            To = 1.5,
            Duration = new Duration(TimeSpan.FromSeconds(2.0)),
            BeginTime = TimeSpan.FromSeconds(0.2),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(middleScaleAnimation, MiddleRingScale);
        Storyboard.SetTargetProperty(middleScaleAnimation, "ScaleX");
        _pulsationStoryboard.Children.Add(middleScaleAnimation);

        var middleScaleYAnimation = new DoubleAnimation
        {
            From = 1.0,
            To = 1.5,
            Duration = new Duration(TimeSpan.FromSeconds(2.0)),
            BeginTime = TimeSpan.FromSeconds(0.2),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(middleScaleYAnimation, MiddleRingScale);
        Storyboard.SetTargetProperty(middleScaleYAnimation, "ScaleY");
        _pulsationStoryboard.Children.Add(middleScaleYAnimation);

        var middleOpacityAnimation = new DoubleAnimation
        {
            From = 0.5,
            To = 0.0,
            Duration = new Duration(TimeSpan.FromSeconds(2.0)),
            BeginTime = TimeSpan.FromSeconds(0.2),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(middleOpacityAnimation, MiddleRing);
        Storyboard.SetTargetProperty(middleOpacityAnimation, "Opacity");
        _pulsationStoryboard.Children.Add(middleOpacityAnimation);

        // Core dot animation - subtle pulse
        var coreScaleAnimation = new DoubleAnimation
        {
            From = 1.0,
            To = 1.1,
            Duration = new Duration(TimeSpan.FromSeconds(1.0)),
            AutoReverse = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(coreScaleAnimation, CoreDotScale);
        Storyboard.SetTargetProperty(coreScaleAnimation, "ScaleX");
        _pulsationStoryboard.Children.Add(coreScaleAnimation);

        var coreScaleYAnimation = new DoubleAnimation
        {
            From = 1.0,
            To = 1.1,
            Duration = new Duration(TimeSpan.FromSeconds(1.0)),
            AutoReverse = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(coreScaleYAnimation, CoreDotScale);
        Storyboard.SetTargetProperty(coreScaleYAnimation, "ScaleY");
        _pulsationStoryboard.Children.Add(coreScaleYAnimation);
    }

    private void StartAnimation()
    {
        if (_pulsationStoryboard == null)
        {
            CreatePulsationAnimation();
        }
        _pulsationStoryboard?.Begin();
    }

    private void StopAnimation()
    {
        _pulsationStoryboard?.Stop();
    }
}
