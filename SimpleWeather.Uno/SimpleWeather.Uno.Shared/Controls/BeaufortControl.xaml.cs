﻿using SimpleWeather.Common.Controls;
using SimpleWeather.Icons;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SimpleWeather.UWP.Controls
{
    public sealed partial class BeaufortControl : UserControl
    {
        public BeaufortViewModel ViewModel
        {
            get { return (this.DataContext as BeaufortViewModel); }
        }

        private readonly WeatherIconsManager wim = SharedModule.Instance.WeatherIconsManager;

        public BeaufortControl()
        {
            this.InitializeComponent();
            this.DataContextChanged += (sender, args) =>
            {
                this.Bindings.Update();
                this.BeaufortIcon.UpdateWeatherIcon();
            };
        }
    }
}