﻿using SimpleWeather.Controls;
using SimpleWeather.Location;
using SimpleWeather.Utils;
using SimpleWeather.UWP.BackgroundTasks;
using SimpleWeather.UWP.Controls;
using SimpleWeather.UWP.Helpers;
using SimpleWeather.UWP.Tiles;
using SimpleWeather.UWP.WeatherAlerts;
using SimpleWeather.WeatherData;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.System.UserProfile;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.StartScreen;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238
namespace SimpleWeather.UWP.Main
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class WeatherNow : CustomPage, IDisposable, IWeatherErrorListener
    {
        private WeatherManager wm;
        private WeatherDataLoader wLoader = null;

        private LocationData location = null;
        private bool loaded = false;
        private WeatherNowViewModel WeatherView { get; set; }

        private double BGAlpha = 1.0;
        private double GradAlpha = 1.0;
        private CancellationTokenSource cts;

        private Geolocator geolocal = null;
        private Geoposition geoPos = null;

        public void Dispose()
        {
            cts?.Dispose();
        }

        public void OnWeatherLoaded(LocationData location, Weather weather)
        {
            AsyncTask.Run(async () =>
            {
                if (cts?.IsCancellationRequested == true)
                    return;

                if (weather?.IsValid() == true)
                {
                    await AsyncTask.RunOnUIThread(() =>
                    {
                        WeatherView.UpdateView(weather);
                        LoadingRing.IsActive = false;
                    }).ConfigureAwait(false);

                    if (wm.SupportsAlerts)
                    {
                        if (weather.weather_alerts != null && weather.weather_alerts.Any())
                        {
                            // Alerts are posted to the user here. Set them as notified.
                            AsyncTask.Run(async () =>
                            {
#if DEBUG
                                await WeatherAlertHandler.PostAlerts(location, weather.weather_alerts)
                                .ConfigureAwait(false);
#endif
                                await WeatherAlertHandler.SetasNotified(location, weather.weather_alerts)
                                .ConfigureAwait(false);
                            });
                        }
                    }

                    // Update home tile if it hasn't been already
                    if (Settings.HomeData.Equals(location)
                        && (TimeSpan.FromTicks(DateTime.Now.Ticks - Settings.UpdateTime.Ticks).TotalMinutes > Settings.RefreshInterval)
                        || !WeatherTileCreator.TileUpdated)
                    {
                        AsyncTask.Run(async () => await WeatherUpdateBackgroundTask.RequestAppTrigger()
                        .ConfigureAwait(false));
                    }
                    else if (SecondaryTileUtils.Exists(location?.query))
                    {
                        AsyncTask.Run(() =>
                        {
                            WeatherTileCreator.TileUpdater(location);
                        });
                    }
                }
            });
        }

        public void OnWeatherError(WeatherException wEx)
        {
            AsyncTask.RunOnUIThread(() =>
            {
                if (cts?.IsCancellationRequested == true)
                    return;

                switch (wEx.ErrorStatus)
                {
                    case WeatherUtils.ErrorStatus.NetworkError:
                    case WeatherUtils.ErrorStatus.NoWeather:
                        // Show error message and prompt to refresh
                        Snackbar snackbar = Snackbar.Make(wEx.Message, SnackbarDuration.Long);
                        snackbar.SetAction(App.ResLoader.GetString("Action_Retry"), () =>
                        {
                            AsyncTask.Run(() => RefreshWeather(false));
                        });
                        ShowSnackbar(snackbar);
                        break;

                    case WeatherUtils.ErrorStatus.QueryNotFound:
                        if (WeatherAPI.NWS.Equals(Settings.API))
                        {
                            ShowSnackbar(Snackbar.Make(App.ResLoader.GetString("Error_WeatherUSOnly"), SnackbarDuration.Long));
                        }
                        break;

                    default:
                        // Show error message
                        ShowSnackbar(Snackbar.Make(wEx.Message, SnackbarDuration.Long));
                        break;
                }
            });
        }

        public WeatherNow()
        {
            this.InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
            Application.Current.Resuming += WeatherNow_Resuming;
            cts = new CancellationTokenSource();

            wm = WeatherManager.GetInstance();
            WeatherView = new WeatherNowViewModel();
            WeatherView.PropertyChanged += WeatherView_PropertyChanged;

            geolocal = new Geolocator() { DesiredAccuracyInMeters = 5000, ReportInterval = 900000, MovementThreshold = 1600 };

            // CommandBar
            CommandBarLabel = App.ResLoader.GetString("Nav_WeatherNow/Label");
            PrimaryCommands = new List<ICommandBarElement>()
            {
                new AppBarButton()
                {
                    Icon = new SymbolIcon(Symbol.Pin),
                    Label = App.ResLoader.GetString("Label_Pin/Text"),
                    Tag = "pin",
                    Visibility = Visibility.Collapsed
                },
                new AppBarButton()
                {
                    Icon = new SymbolIcon(Symbol.Refresh),
                    Label = App.ResLoader.GetString("Button_Refresh/Label"),
                    Tag = "refresh"
                }
            };
            GetRefreshBtn().Click += RefreshButton_Click;
            GetPinBtn().Click += PinButton_Click;

            MainGrid.Loaded += (s, e) =>
            {
                UpdateWindowColors();
            };

            loaded = true;
        }

        private void UpdateWindowColors()
        {
            if (cts?.IsCancellationRequested == true)
                return;

            if ((Settings.UserTheme == UserThemeMode.Dark || (Settings.UserTheme == UserThemeMode.System && App.IsSystemDarkTheme)) && WeatherView?.PendingBackgroundColor != App.AppColor)
            {
                var color = ColorUtils.BlendColor(WeatherView.PendingBackgroundColor, Colors.Black, 0.75f);
                MainGrid.Background = new SolidColorBrush(color);
                Shell.Instance.AppBarColor = color;
            }
            else
            {
                var color = WeatherView.PendingBackgroundColor;
                MainGrid.Background = new SolidColorBrush(color);
                Shell.Instance.AppBarColor = color;
            }
        }

        private async void WeatherView_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "Sunrise":
                case "Sunset":
                    if (!String.IsNullOrWhiteSpace(WeatherView.Sunrise) && !String.IsNullOrWhiteSpace(WeatherView.Sunset))
                    {
                        while (SunPhasePanel == null || (bool)!SunPhasePanel?.ReadyToDraw)
                        {
                            await Task.Delay(1).ConfigureAwait(true);
                        }

                        var userlang = GlobalizationPreferences.Languages[0];
                        var culture = new CultureInfo(userlang);

                        SunPhasePanel?.SetSunriseSetTimes(
                            DateTime.Parse(WeatherView.Sunrise, culture).TimeOfDay, DateTime.Parse(WeatherView.Sunset, culture).TimeOfDay,
                            location?.tz_offset);
                    }
                    break;

                case "Alerts":
                    ResizeAlertPanel();
                    break;

                case "PendingBackgroundColor":
                    UpdateWindowColors();
                    break;
            }
        }

        private AppBarButton GetRefreshBtn()
        {
            return PrimaryCommands.Last() as AppBarButton;
        }

        private AppBarButton GetPinBtn()
        {
            return PrimaryCommands.First() as AppBarButton;
        }

        private void WeatherNow_Resuming(object sender, object e)
        {
            AsyncTask.RunOnUIThread(async () =>
            {
                if (Shell.Instance.AppFrame.SourcePageType == this.GetType())
                {
                    // Check pin tile status
                    await AsyncTask.RunAsync(CheckTiles);

                    await AsyncTask.RunAsync(Resume);
                }
            });
        }

        private void MainViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (sender is ScrollViewer viewer)
            {
                // Default adj = 1.25f
                float adj = 1.25f;
                double backAlpha = 1 - (1 * adj * viewer.VerticalOffset / ConditionPanel.ActualHeight);
                double gradAlpha = 1 - (1 * adj * viewer.VerticalOffset / ConditionPanel.ActualHeight);
                BGAlpha = Math.Max(backAlpha, (float)0x25 / 0xFF); // 0x25
                GradAlpha = Math.Max(gradAlpha, 0);
                BackgroundOverlay.Opacity = BGAlpha;
                GradientOverlay.Opacity = GradAlpha;
            }
        }

        private void MainGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AdjustViewLayout();
        }

        private void DeferedControl_Loaded(object sender, RoutedEventArgs e)
        {
            AdjustViewLayout();
        }

        private void ResizeAlertPanel()
        {
            double w = this.ActualWidth;

            if (w <= 0 || AlertButton == null)
                return;

            if (w <= 640)
                AlertButton.Width = w;
            else if (w <= 1080)
                AlertButton.Width = w * (0.75);
            else
                AlertButton.Width = w * (0.50);
        }

        private void AdjustViewLayout()
        {
            if (Window.Current == null) return;
            var Bounds = Window.Current.Bounds;

            if (MainViewer == null) return;

            double w = MainViewer.ActualWidth - MainViewer.Padding.Left - MainViewer.Padding.Right;
            double h = MainViewer.ActualHeight - MainViewer.Padding.Top - MainViewer.Padding.Bottom;

            ResizeAlertPanel();

            if (ConditionPanel != null)
            {
                ConditionPanel.Height = h;
                if (w >= 1280)
                    ConditionPanel.Width = 1280;
                else
                    ConditionPanel.Width = w;
            }

            if (Bounds.Height >= 691)
            {
                if (WeatherBox != null) WeatherBox.Height = WeatherBox.Width = 155;
                if (SunPhasePanel != null) SunPhasePanel.Height = 250;
            }
            else if (Bounds.Height >= 641)
            {
                if (WeatherBox != null) WeatherBox.Height = WeatherBox.Width = 130;
                if (SunPhasePanel != null) SunPhasePanel.Height = 250;
            }
            else if (Bounds.Height >= 481)
            {
                if (WeatherBox != null) WeatherBox.Height = WeatherBox.Width = 100;
                if (SunPhasePanel != null) SunPhasePanel.Height = 180;
            }
            else if (Bounds.Height >= 361)
            {
                if (WeatherBox != null) WeatherBox.Height = WeatherBox.Width = 75;
                if (SunPhasePanel != null) SunPhasePanel.Height = 180;
            }
            else
            {
                if (WeatherBox != null) WeatherBox.Height = WeatherBox.Width = 50;
                if (SunPhasePanel != null) SunPhasePanel.Height = 180;
            }

            if (Bounds.Width >= 1007)
            {
                if (Location != null) Location.FontSize = 32;
                if (CurTemp != null) CurTemp.FontSize = 32;
                if (CurCondition != null) CurCondition.FontSize = 32;
            }
            else if (Bounds.Width >= 641)
            {
                if (Location != null) Location.FontSize = 28;
                if (CurTemp != null) CurTemp.FontSize = 28;
                if (CurCondition != null) CurCondition.FontSize = 28;
            }
            else
            {
                if (Location != null) Location.FontSize = 24;
                if (CurTemp != null) CurTemp.FontSize = 24;
                if (CurCondition != null) CurCondition.FontSize = 24;
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            cts?.Cancel();
            loaded = false;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            cts = new CancellationTokenSource();

            MainViewer?.ChangeView(null, 0, null, true);
            BGAlpha = GradAlpha = 1.0f;

            WeatherNowArgs args = e.Parameter as WeatherNowArgs;

            bool locationChanged = false;
            if (!loaded)
            {
                // Load new favorite location if argument data is present
                if (args?.Location != null && !Object.Equals(location, args?.Location))
                {
                    location = args?.Location;
                    locationChanged = true;
                }
                else if (args?.IsHome == true)
                {
                    // Check if home location changed
                    // For ex. due to GPS setting change
                    LocationData homeData = Settings.HomeData;
                    if (!location.Equals(homeData))
                    {
                        location = homeData;
                        locationChanged = true;
                    }
                }
            }
            else
            {
                if (location == null)
                    location = args?.Location;
            }

            // New page instance -> loaded = true
            // Navigating back to existing page instance => loaded = false
            // Weather location changed (ex. due to GPS setting) -> locationChanged = true
            if (loaded || locationChanged || wLoader == null)
            {
                await AsyncTask.RunAsync(Restore);
            }
            else
            {
                var userlang = GlobalizationPreferences.Languages[0];
                var culture = new CultureInfo(userlang);
                var locale = wm.LocaleToLangCode(culture.TwoLetterISOLanguageName, culture.Name);

                if (!String.Equals(WeatherView.WeatherSource, Settings.API) ||
                    wm.SupportsWeatherLocale && !String.Equals(WeatherView.WeatherLocale, locale))
                {
                    await AsyncTask.RunAsync(Restore);
                }
                else
                {
                    await AsyncTask.RunAsync(Resume);
                }
            }

            loaded = true;
        }

        private async Task Resume()
        {
            // Check pin tile status
            await AsyncTask.RunAsync(CheckTiles);

            // Update weather if needed on resume
            if (Settings.FollowGPS && await AsyncTask.RunAsync(UpdateLocation))
            {
                // Setup loader from updated location
                wLoader = new WeatherDataLoader(location);
            }

            if (cts?.IsCancellationRequested == true)
                return;

            RefreshWeather(false);

            loaded = true;
        }

        private async Task Restore()
        {
            bool forceRefresh = false;

            // GPS Follow location
            if (Settings.FollowGPS && (location == null || location.locationType == LocationType.GPS))
            {
                LocationData locData = await Settings.GetLastGPSLocData();

                if (locData == null)
                {
                    // Update location if not setup
                    await AsyncTask.RunAsync(UpdateLocation);
                    forceRefresh = true;
                }
                else
                {
                    // Reset locdata if source is different
                    if (locData.weatherSource != Settings.API)
                        Settings.SaveLastGPSLocData(new LocationData());

                    if (await AsyncTask.RunAsync(UpdateLocation))
                    {
                        // Setup loader from updated location
                        forceRefresh = true;
                    }
                    else
                    {
                        // Setup loader saved location data
                        location = locData;
                    }
                }
            }
            // Regular mode
            else if (location == null && wLoader == null)
            {
                // Weather was loaded before. Lets load it up...
                location = Settings.HomeData;
            }

            if (cts?.IsCancellationRequested == true)
                return;

            // Check pin tile status
            await AsyncTask.RunAsync(CheckTiles);

            if (location != null)
                wLoader = new WeatherDataLoader(location);

            // Load up weather data
            RefreshWeather(forceRefresh);
        }

        private async Task<bool> UpdateLocation()
        {
            bool locationChanged = false;

            if (Settings.FollowGPS && (location == null || location.locationType == LocationType.GPS))
            {
                Geoposition newGeoPos = null;

                try
                {
                    newGeoPos = await geolocal.GetGeopositionAsync(TimeSpan.FromMinutes(15), TimeSpan.FromSeconds(10));
                }
                catch (Exception)
                {
                    var geoStatus = GeolocationAccessStatus.Unspecified;

                    try
                    {
                        geoStatus = await Geolocator.RequestAccessAsync();
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine(LoggerLevel.Error, ex, "WeatherNow: GetWeather error");
                    }
                    finally
                    {
                        if (geoStatus == GeolocationAccessStatus.Allowed)
                        {
                            try
                            {
                                newGeoPos = await geolocal.GetGeopositionAsync(TimeSpan.FromMinutes(15), TimeSpan.FromSeconds(10));
                            }
                            catch (Exception ex)
                            {
                                Logger.WriteLine(LoggerLevel.Error, ex, "WeatherNow: GetWeather error");
                            }
                        }
                        else if (geoStatus == GeolocationAccessStatus.Denied)
                        {
                            // Disable gps feature
                            Settings.FollowGPS = false;
                        }
                    }

                    if (!Settings.FollowGPS)
                        return false;
                }

                if (cts?.IsCancellationRequested == true)
                    return false;

                // Access to location granted
                if (newGeoPos != null)
                {
                    LocationData lastGPSLocData = await Settings.GetLastGPSLocData();

                    // Check previous location difference
                    if (lastGPSLocData.query != null
                        && geoPos != null && ConversionMethods.CalculateGeopositionDistance(geoPos, newGeoPos) < geolocal.MovementThreshold)
                    {
                        return false;
                    }

                    if (lastGPSLocData.query != null
                        && Math.Abs(ConversionMethods.CalculateHaversine(lastGPSLocData.latitude, lastGPSLocData.longitude,
                        newGeoPos.Coordinate.Point.Position.Latitude, newGeoPos.Coordinate.Point.Position.Longitude)) < geolocal.MovementThreshold)
                    {
                        return false;
                    }

                    LocationQueryViewModel view = null;

                    if (cts?.IsCancellationRequested == true)
                        return false;

                    await AsyncTask.RunAsync(async () =>
                    {
                        try
                        {
                            view = await AsyncTask.RunAsync(wm.GetLocation(newGeoPos));

                            if (String.IsNullOrEmpty(view.LocationQuery))
                                view = new LocationQueryViewModel();
                        }
                        catch (WeatherException ex)
                        {
                            view = new LocationQueryViewModel();

                            await AsyncTask.RunOnUIThread(() =>
                            {
                                ShowSnackbar(Snackbar.Make(ex.Message, SnackbarDuration.Short));
                            }).ConfigureAwait(false);
                        }
                    });

                    if (String.IsNullOrWhiteSpace(view.LocationQuery))
                    {
                        // Stop since there is no valid query
                        return false;
                    }

                    if (cts?.IsCancellationRequested == true)
                        return false;

                    // Save oldkey
                    string oldkey = lastGPSLocData.query;

                    // Save location as last known
                    lastGPSLocData.SetData(view, newGeoPos);
                    Settings.SaveLastGPSLocData(lastGPSLocData);

                    // Update tile id for location
                    if (oldkey != null && SecondaryTileUtils.Exists(oldkey))
                    {
                        await AsyncTask.RunAsync(SecondaryTileUtils.UpdateTileId(oldkey, lastGPSLocData.query));
                    }

                    location = lastGPSLocData;
                    geoPos = newGeoPos;
                    locationChanged = true;
                }
            }

            return locationChanged;
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (Settings.FollowGPS && await AsyncTask.RunAsync(UpdateLocation))
                // Setup loader from updated location
                wLoader = new WeatherDataLoader(location);

            RefreshWeather(true);
        }

        private void RefreshWeather(bool forceRefresh)
        {
            AsyncTask.RunOnUIThread(() => LoadingRing.IsActive = true);
            AsyncTask.Run(() =>
            {
                if (cts?.IsCancellationRequested == false)
                    wLoader?.LoadWeatherData(new WeatherRequest.Builder()
                            .ForceRefresh(forceRefresh)
                            .LoadAlerts()
                            .SetErrorListener(this)
                            .Build())
                            .ContinueWith((t) => 
                            {
                                if (t.IsCompletedSuccessfully)
                                    OnWeatherLoaded(location, t.Result);
                            });
            });
        }

        private void LeftButton_Click(object sender, RoutedEventArgs e)
        {
            var controlName = (sender as FrameworkElement)?.Name;

            if ((bool)controlName?.Contains("Hourly"))
            {
                ScrollViewerHelper.ScrollLeft(HourlyGraphPanel?.ScrollViewer);
            }
            else
            {
                ScrollViewerHelper.ScrollLeft(ForecastGraphPanel?.ScrollViewer);
            }
        }

        private void RightButton_Click(object sender, RoutedEventArgs e)
        {
            var controlName = (sender as FrameworkElement)?.Name;

            if ((bool)controlName?.Contains("Hourly"))
            {
                ScrollViewerHelper.ScrollRight(HourlyGraphPanel?.ScrollViewer);
            }
            else
            {
                ScrollViewerHelper.ScrollRight(ForecastGraphPanel?.ScrollViewer);
            }
        }

        private void GotoAlertsPage()
        {
            Frame.Navigate(typeof(WeatherAlertPage), new WeatherPageArgs() 
            {
                Location = location,
                WeatherNowView = WeatherView
            });
        }

        private void AlertButton_Click(object sender, RoutedEventArgs e)
        {
            GotoAlertsPage();
        }

        private async Task CheckTiles()
        {
            var pinBtn = GetPinBtn();

            if (pinBtn != null)
            {
                await AsyncTask.RunOnUIThread(() =>
                {
                    pinBtn.IsEnabled = false;
                }).ConfigureAwait(false);

                // Check if your app is currently pinned
                bool isPinned = SecondaryTileUtils.Exists(location?.query);

                await AsyncTask.RunOnUIThread(async () =>
                {
                    await SetPinButton(isPinned).ConfigureAwait(true);
                    pinBtn.Visibility = Visibility.Visible;
                    pinBtn.IsEnabled = true;
                }).ConfigureAwait(false);
            }
        }

        private async Task SetPinButton(bool isPinned)
        {
            await AsyncTask.RunOnUIThread(() =>
            {
                var pinBtn = GetPinBtn();

                if (pinBtn != null)
                {
                    if (isPinned)
                    {
                        pinBtn.Icon = new SymbolIcon(Symbol.UnPin);
                        pinBtn.Label = App.ResLoader.GetString("Label_Unpin/Text");
                    }
                    else
                    {
                        pinBtn.Icon = new SymbolIcon(Symbol.Pin);
                        pinBtn.Label = App.ResLoader.GetString("Label_Pin/Text");
                    }
                }
            }).ConfigureAwait(false);
        }

        private async void PinButton_Click(object sender, RoutedEventArgs e)
        {
            var pinBtn = sender as AppBarButton;
            pinBtn.IsEnabled = false;

            if (SecondaryTileUtils.Exists(location?.query))
            {
                bool deleted = await new SecondaryTile(
                    SecondaryTileUtils.GetTileId(location.query)).RequestDeleteAsync();
                if (deleted)
                {
                    SecondaryTileUtils.RemoveTileId(location.query);
                }

                await SetPinButton(!deleted).ConfigureAwait(true);

                pinBtn.IsEnabled = true;
            }
            else
            {
                // Initialize the tile with required arguments
                var tileID = DateTime.Now.Ticks.ToString();
                var tile = new SecondaryTile(
                    tileID,
                    "SimpleWeather",
                    "action=view-weather&query=" + location.query,
                    new Uri("ms-appx:///Assets/Square150x150Logo.png"),
                    TileSize.Default);

                // Enable wide and large tile sizes
                tile.VisualElements.Wide310x150Logo = new Uri("ms-appx:///Assets/Wide310x150Logo.png");
                tile.VisualElements.Square310x310Logo = new Uri("ms-appx:///Assets/Square310x310Logo.png");

                // Add a small size logo for better looking small tile
                tile.VisualElements.Square71x71Logo = new Uri("ms-appx:///Assets/Square71x71Logo.png");

                // Add a unique corner logo for the secondary tile
                tile.VisualElements.Square44x44Logo = new Uri("ms-appx:///Assets/Square44x44Logo.png");

                // Show the display name on all sizes
                tile.VisualElements.ShowNameOnSquare150x150Logo = true;
                tile.VisualElements.ShowNameOnWide310x150Logo = true;
                tile.VisualElements.ShowNameOnSquare310x310Logo = true;

                bool isPinned = await tile.RequestCreateAsync();
                if (isPinned)
                {
                    // Update tile with notifications
                    SecondaryTileUtils.AddTileId(location.query, tileID);
                    await WeatherTileCreator.TileUpdater(location).ConfigureAwait(true);
                    await tile.UpdateAsync();
                }

                await SetPinButton(isPinned).ConfigureAwait(true);

                pinBtn.IsEnabled = true;
            }
        }

        private void GotoDetailsPage(bool IsHourly, int Position)
        {
            Frame.Navigate(typeof(WeatherDetailsPage),
                new DetailsPageArgs()
                {
                    Location = location,
                    WeatherNowView = WeatherView,
                    IsHourly = IsHourly,
                    ScrollToPosition = Position
                });
        }

        private void LineView_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var control = sender as LineView;
            FrameworkElement controlParent = null;

            try
            {
                controlParent = VisualTreeHelperExtensions.GetParent<ForecastGraphPanel>(control);
            }
            catch (Exception) { }

            GotoDetailsPage((bool)controlParent?.Name.StartsWith("Hourly"),
                control.GetItemPositionFromPoint((float)(e.GetPosition(control).X + control?.ScrollViewer?.HorizontalOffset)));
        }
    }
}