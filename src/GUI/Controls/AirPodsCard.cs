using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using PodPilot.Core.ViewModels;
using System;

namespace GUI.Controls;

/// <summary>
/// Card component for displaying detailed AirPods information.
/// </summary>
public sealed partial class AirPodsCard : UserControl
{
    public static readonly DependencyProperty DeviceProperty =
        DependencyProperty.Register(
            nameof(Device),
            typeof(AirPodsDeviceViewModel),
            typeof(AirPodsCard),
            new PropertyMetadata(null, OnDeviceChanged));

    public AirPodsDeviceViewModel? Device
    {
        get => (AirPodsDeviceViewModel?)GetValue(DeviceProperty);
        set => SetValue(DeviceProperty, value);
    }

    public AirPodsCard()
    {
        InitializeComponent();
    }

    private static void OnDeviceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AirPodsCard card)
        {
            if (e.OldValue is AirPodsDeviceViewModel oldDevice)
            {
                oldDevice.PropertyChanged -= card.OnDevicePropertyChanged;
            }

            if (e.NewValue is AirPodsDeviceViewModel newDevice)
            {
                newDevice.PropertyChanged += card.OnDevicePropertyChanged;
            }
        }
    }

    private void OnDevicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AirPodsDeviceViewModel.IsConnected))
        {
            if (Device?.IsConnected == true)
            {
                AnimateConnectedBadge();
            }
        }
    }

    private void ConnectedBadge_Loaded(object sender, RoutedEventArgs e)
    {
        if (Device?.IsConnected == true)
        {
            AnimateConnectedBadge();
        }
    }

    private void AnimateConnectedBadge()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                var connectedBadge = this.FindName("ConnectedBadge") as Border;
                var connectedTransform = this.FindName("ConnectedTransform") as CompositeTransform;

                if (connectedBadge == null || connectedTransform == null)
                    return;

                var storyboard = new Storyboard();

                // Scale animation with bounce effect
                var scaleXAnimation = new DoubleAnimationUsingKeyFrames();
                scaleXAnimation.KeyFrames.Add(new EasingDoubleKeyFrame
                {
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero),
                    Value = 0
                });
                scaleXAnimation.KeyFrames.Add(new EasingDoubleKeyFrame
                {
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150)),
                    Value = 1.15,
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
                scaleXAnimation.KeyFrames.Add(new EasingDoubleKeyFrame
                {
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300)),
                    Value = 1.0,
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                });

                var scaleYAnimation = new DoubleAnimationUsingKeyFrames();
                scaleYAnimation.KeyFrames.Add(new EasingDoubleKeyFrame
                {
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero),
                    Value = 0
                });
                scaleYAnimation.KeyFrames.Add(new EasingDoubleKeyFrame
                {
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150)),
                    Value = 1.15,
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
                scaleYAnimation.KeyFrames.Add(new EasingDoubleKeyFrame
                {
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300)),
                    Value = 1.0,
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                });

                // Opacity fade-in
                var opacityAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = new Duration(TimeSpan.FromMilliseconds(250)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                Storyboard.SetTarget(scaleXAnimation, connectedTransform);
                Storyboard.SetTargetProperty(scaleXAnimation, "ScaleX");

                Storyboard.SetTarget(scaleYAnimation, connectedTransform);
                Storyboard.SetTargetProperty(scaleYAnimation, "ScaleY");

                Storyboard.SetTarget(opacityAnimation, connectedBadge);
                Storyboard.SetTargetProperty(opacityAnimation, "Opacity");

                storyboard.Children.Add(scaleXAnimation);
                storyboard.Children.Add(scaleYAnimation);
                storyboard.Children.Add(opacityAnimation);

                storyboard.Begin();
            });
        }

        /// <summary>
        /// Starts the pulsating animation for the pairing warning icon.
        /// </summary>
        private void PairingWarningButton_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement button)
                return;

            var warningIcon = button.FindName("PairingWarningIcon") as FrameworkElement;
            if (warningIcon == null)
                return;

            var storyboard = new Storyboard();

            var opacityAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.3,
                Duration = new Duration(TimeSpan.FromMilliseconds(800)),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            Storyboard.SetTarget(opacityAnimation, warningIcon);
            Storyboard.SetTargetProperty(opacityAnimation, "Opacity");

            storyboard.Children.Add(opacityAnimation);
            storyboard.Begin();
        }
    }

