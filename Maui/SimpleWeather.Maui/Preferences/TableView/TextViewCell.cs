﻿using System;
using System.Windows.Input;
using CommunityToolkit.Maui.Markup;
using SimpleWeather.Utils;

namespace SimpleWeather.Maui.Preferences
{
	public class TextViewCell : ViewCell
	{
        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly BindableProperty TextProperty =
            BindableProperty.Create(nameof(Text), typeof(string), typeof(TextViewCell), default(string));

        public Color TextColor
        {
            get { return (Color)GetValue(TextColorProperty); }
            set { SetValue(TextColorProperty, value); }
        }

        public static readonly BindableProperty TextColorProperty =
            BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(TextViewCell), null);

        public string Detail
        {
            get { return (string)GetValue(DetailProperty); }
            set { SetValue(DetailProperty, value); }
        }

        public static readonly BindableProperty DetailProperty =
            BindableProperty.Create(nameof(Detail), typeof(string), typeof(TextViewCell), default(string));

        public Color DetailColor
        {
            get { return (Color)GetValue(DetailColorProperty); }
            set { SetValue(DetailColorProperty, value); }
        }

        public static readonly BindableProperty DetailColorProperty =
            BindableProperty.Create(nameof(DetailColor), typeof(Color), typeof(TextViewCell), null);

		public ICommand Command
        {
            get { return (ICommand)GetValue(CommandProperty); }
            set { SetValue(CommandProperty, value); }
        }

        public object CommandParameter
        {
            get { return GetValue(CommandParameterProperty); }
            set { SetValue(CommandParameterProperty, value); }
        }

        /// <summary>Bindable property for <see cref="Command"/>.</summary>
		public static readonly BindableProperty CommandProperty = BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(TextViewCell), default(ICommand),
            propertyChanging: (bindable, oldvalue, newvalue) =>
            {
                var textViewCell = (TextViewCell)bindable;
                var oldcommand = (ICommand)oldvalue;
                if (oldcommand != null)
                    oldcommand.CanExecuteChanged -= textViewCell.OnCommandCanExecuteChanged;
            }, propertyChanged: (bindable, oldvalue, newvalue) =>
            {
                var textViewCell = (TextViewCell)bindable;
                var newcommand = (ICommand)newvalue;
                if (newcommand != null)
                {
                    textViewCell.IsEnabled = newcommand.CanExecute(textViewCell.CommandParameter);
                    newcommand.CanExecuteChanged += textViewCell.OnCommandCanExecuteChanged;
                }
            });

        /// <summary>Bindable property for <see cref="CommandParameter"/>.</summary>
        public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(TextViewCell), default(object),
            propertyChanged: (bindable, oldvalue, newvalue) =>
            {
                var textViewCell = (TextViewCell)bindable;
                if (textViewCell.Command != null)
                {
                    textViewCell.IsEnabled = textViewCell.Command.CanExecute(newvalue);
                }
            });

        public TextViewCell()
        {
            this.View = new Grid()
            {
                MinimumHeightRequest = 48,
                MaximumHeightRequest = double.PositiveInfinity,
                RowDefinitions =
                {
                    new RowDefinition(GridLength.Star),
                    new RowDefinition(GridLength.Auto)
                },
                Padding = new Thickness(16, 8),
                Children =
                {
                    new Label()
                    {
                        FontSize = 17,
                        FontFamily = Microsoft.Maui.Font.Default.Family,
                        VerticalTextAlignment = TextAlignment.Center
                    }
                    .Bind(Label.TextProperty, nameof(Text), BindingMode.OneWay, source: this)
                    .Bind(Label.TextColorProperty, nameof(TextColor), BindingMode.OneWay, source: this)
                    .Bind(Label.IsEnabledProperty, nameof(IsEnabled), BindingMode.OneWay, source: this)
                    .Bind<Label, bool, double>(Label.OpacityProperty, nameof(IsEnabled), BindingMode.OneWay, source: this,
                        convert: (enabled) =>
                        {
                            return enabled ? 1.0d : 0.5d;
                        }
                     )
                    .CenterVertical()
                    .Column(0)
                    .Row(0),
                    new Label()
                    {
                        FontSize = 12,
                        FontFamily = Microsoft.Maui.Font.Default.Family
                    }
                    .Paddings(top: 4)
                    .Bind(Label.TextProperty, nameof(Detail), BindingMode.OneWay, source: this)
                    .Bind(Label.TextColorProperty, nameof(DetailColor), BindingMode.OneWay, source: this)
                    .Bind<Label, string, bool>(Label.IsVisibleProperty, nameof(Detail), BindingMode.OneWay, source: this,
                        convert: (s) =>
                        {
                            return !string.IsNullOrWhiteSpace(s?.ToString());
                        }
                    )
                    .Bind(Label.IsEnabledProperty, nameof(IsEnabled), BindingMode.OneWay, source: this)
                    .Bind<Label, bool, double>(Label.OpacityProperty, nameof(IsEnabled), BindingMode.OneWay, source: this,
                        convert: (enabled) =>
                        {
                            return enabled ? 1.0d : 0.5d;
                        }
                     )
                    .CenterVertical()
                    .Column(0)
                    .Row(1)
                }
            }
            .FillVertical();
        }

        protected override void OnTapped()
        {
            base.OnTapped();

            if (!IsEnabled)
            {
                return;
            }

            Command?.Execute(CommandParameter);
        }

        void OnCommandCanExecuteChanged(object sender, EventArgs eventArgs)
        {
            IsEnabled = Command.CanExecute(CommandParameter);
        }
    }
}

