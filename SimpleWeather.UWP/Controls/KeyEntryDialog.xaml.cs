﻿using SimpleWeather.Utils;
using SimpleWeather.WeatherData;
using SimpleWeather.WeatherData.Auth;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace SimpleWeather.UWP.Controls
{
    public sealed partial class KeyEntryDialog : ContentDialog
    {
        public string Key => ProviderKey?.ToString();
        public string APIProvider { get; private set; }

        private AuthType AuthType { get; set; } = AuthType.ApiKey;
        private ProviderKey ProviderKey { get; set; }

        public bool CanClose { get; set; }

        public KeyEntryDialog(string APIProvider)
        {
            this.APIProvider = APIProvider;
            Initialize();
        }

        private void Initialize()
        {
            this.InitializeComponent();
            this.Closing += KeyEntryDialog_Closing;
            KeyEntry1.TextChanged += KeyEntry1_TextChanged;
            KeyEntry2.TextChanged += KeyEntry2_TextChanged;
            PasswordEntry.PasswordChanged += PasswordEntry_PasswordChanged;
            UpdateAuthType();
        }

        private void KeyEntryDialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            if (!CanClose)
                args.Cancel = true;
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            CanClose = true;
            this.Hide();
        }

        private void UpdateAuthType()
        {
            AuthType = WeatherManager.GetAuthType(APIProvider);
            var key = Settings.APIKeys[APIProvider];
            ProviderKey = AuthType switch
            {
                AuthType.AppID_AppCode => new ProviderAppKey().Apply((it) =>
                {
                    key?.Let(input => it.FromString(input));
                }),
                AuthType.Basic => new BasicAuthProviderKey().Apply((it) =>
                {
                    key?.Let(input => it.FromString(input));
                }),
                _ => new ProviderApiKey().Apply((it) =>
                {
                    key?.Let(input => it.FromString(input));
                }),
            };

            switch (AuthType)
            {
                case AuthType.AppID_AppCode:
                    {
                        var credentials = ProviderKey as ProviderAppKey;

                        KeyEntry1.PlaceholderText = App.ResLoader.GetString("hint_appid");
                        KeyEntry1.Text = credentials?.AppID ?? string.Empty;
                        KeyEntry1.Visibility = Windows.UI.Xaml.Visibility.Visible;

                        KeyEntry2.PlaceholderText = App.ResLoader.GetString("hint_appcode");
                        KeyEntry2.Text = credentials?.AppCode ?? string.Empty;
                        KeyEntry2.Visibility = Windows.UI.Xaml.Visibility.Visible;

                        PasswordEntry.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    }
                    break;
                case AuthType.Basic:
                    {
                        var credentials = ProviderKey as BasicAuthProviderKey;

                        KeyEntry1.PlaceholderText = App.ResLoader.GetString("hint_username");
                        KeyEntry1.Text = credentials?.UserName ?? string.Empty;
                        KeyEntry1.Visibility = Windows.UI.Xaml.Visibility.Visible;

                        KeyEntry2.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

                        PasswordEntry.Password = credentials?.Password ?? string.Empty;
                        PasswordEntry.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    }
                    break;
                default:
                    {
                        KeyEntry1.PlaceholderText = App.ResLoader.GetString("key_hint");
                        KeyEntry1.Text = key ?? string.Empty;
                        KeyEntry1.Visibility = Windows.UI.Xaml.Visibility.Visible;

                        KeyEntry2.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

                        PasswordEntry.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    }
                    break;
            }
        }

        private void KeyEntry1_TextChanged(object sender, TextChangedEventArgs e)
        {
            switch (AuthType)
            {
                case AuthType.AppID_AppCode:
                    {
                        if (ProviderKey is ProviderAppKey credentials)
                        {
                            credentials.AppID = KeyEntry1.Text ?? string.Empty;
                        }
                    }
                    break;
                case AuthType.Basic:
                    {
                        if (ProviderKey is BasicAuthProviderKey credentials)
                        {
                            credentials.UserName = KeyEntry1.Text ?? string.Empty;
                        }
                    }
                    break;
                default:
                    {
                        if (ProviderKey is ProviderApiKey credentials)
                        {
                            credentials.Key = KeyEntry1.Text ?? string.Empty;
                        }
                    }
                    break;
            }
        }

        private void KeyEntry2_TextChanged(object sender, TextChangedEventArgs e)
        {
            switch (AuthType)
            {
                case AuthType.AppID_AppCode:
                    {
                        if (ProviderKey is ProviderAppKey credentials)
                        {
                            credentials.AppCode = KeyEntry2.Text ?? string.Empty;
                        }
                    }
                    break;
            }
        }

        private void PasswordEntry_PasswordChanged(object sender, RoutedEventArgs e)
        {
            switch (AuthType)
            {
                case AuthType.Basic:
                    {
                        if (ProviderKey is BasicAuthProviderKey credentials)
                        {
                            credentials.Password = PasswordEntry.Password ?? string.Empty;
                        }
                    }
                    break;
            }
        }
    }
}