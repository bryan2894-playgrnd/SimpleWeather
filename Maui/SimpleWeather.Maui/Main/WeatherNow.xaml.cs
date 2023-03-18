using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using SimpleWeather.Common.Controls;
using SimpleWeather.Common.Location;
using SimpleWeather.Common.Utils;
using SimpleWeather.Common.ViewModels;
using SimpleWeather.Icons;
using SimpleWeather.LocationData;
using SimpleWeather.Maui.Controls;
using SimpleWeather.Maui.Controls.Flow;
using SimpleWeather.Maui.Controls.Graphs;
using SimpleWeather.Maui.Helpers;
using SimpleWeather.Maui.MaterialIcons;
using SimpleWeather.Maui.Utils;
using SimpleWeather.NET.Controls;
using SimpleWeather.NET.Controls.Graphs;
using SimpleWeather.NET.Radar;
using SimpleWeather.Preferences;
using SimpleWeather.Utils;
using SimpleWeather.Weather_API;
using SimpleWeather.Weather_API.WeatherData;
using System.ComponentModel;
using ResStrings = SimpleWeather.Resources.Strings.Resources;

namespace SimpleWeather.Maui.Main;

public partial class WeatherNow : ScopePage, ISnackbarManager, ISnackbarPage, IBannerManager, IBannerPage
{
    private SnackbarManager SnackMgr { get; set; }
    private BannerManager BannerMgr { get; set; }

    private readonly WeatherProviderManager wm = WeatherModule.Instance.WeatherManager;
    private readonly SettingsManager SettingsManager = Ioc.Default.GetService<SettingsManager>();

    private RadarViewProvider radarViewProvider;

    private WeatherNowArgs args;

    private WeatherNowViewModel WNowViewModel { get; } = AppShell.Instance.GetViewModel<WeatherNowViewModel>();
    private ForecastsNowViewModel ForecastView { get; } = AppShell.Instance.GetViewModel<ForecastsNowViewModel>();
    private WeatherAlertsViewModel AlertsView { get; } = AppShell.Instance.GetViewModel<WeatherAlertsViewModel>();

    public Color ConditionPanelTextColor
    {
        get => (Color)GetValue(ConditionPanelTextColorProperty);
        set => SetValue(ConditionPanelTextColorProperty, value);
    }

    public static readonly BindableProperty ConditionPanelTextColorProperty =
        BindableProperty.Create(nameof(ConditionPanelTextColor), typeof(Color), typeof(WeatherNow), Colors.White);

    private bool UpdateBindings = false;
    private bool UpdateTheme = false;
    private bool ClearGraphIconCache = false;

    public WeatherNow()
    {
        InitializeComponent();

        AnalyticsLogger.LogEvent("WeatherNow");

        Utils.FeatureSettings.OnFeatureSettingsChanged += FeatureSettings_OnFeatureSettingsChanged;
        SettingsManager.OnSettingsChanged += Settings_OnSettingsChanged;
        RadarProvider.RadarProviderChanged += RadarProvider_RadarProviderChanged;
        this.Loaded += WeatherNow_Loaded;
        this.Unloaded += WeatherNow_Unloaded;

        BindingContext = WNowViewModel;

        InitControls();
    }

    internal WeatherNow(WeatherNowArgs args) : this()
    {
        this.args = args;
    }

    public void InitSnackManager()
    {
        if (SnackMgr == null)
        {
            SnackMgr = new SnackbarManager(SnackbarContainer);
        }
    }

    public void ShowSnackbar(Snackbar snackbar)
    {
        Dispatcher.Dispatch(() =>
        {
            SnackMgr?.Show(snackbar);
        });
    }
    public void DismissAllSnackbars()
    {
        Dispatcher.Dispatch(() =>
        {
            SnackMgr?.DismissAll();
        });
    }

    public void UnloadSnackManager()
    {
        DismissAllSnackbars();
        SnackMgr = null;
    }

    public void InitBannerManager()
    {
        if (BannerMgr == null)
        {
            BannerMgr = new BannerManager(MainGrid);
        }
    }

    public void ShowBanner(Banner banner)
    {
        Dispatcher.Dispatch(() =>
        {
            BannerMgr?.Show(banner);
        });
    }

    public void DismissBanner()
    {
        Dispatcher.Dispatch(() =>
        {
            BannerMgr?.Dismiss();
        });
    }

    public void UnloadBannerManager()
    {
        DismissBanner();
        BannerMgr = null;
    }

    private void Settings_OnSettingsChanged(SettingsChangedEventArgs e)
    {
        if (e.Key == SettingsManager.KEY_ICONSSOURCE)
        {
            // When page is loaded again from cache, clear icon cache
            ClearGraphIconCache = true;
            UpdateBindings = true;
        }
        else if (e.Key == SettingsManager.KEY_USERTHEME)
        {
            UpdateBindings = true;
            // Update theme
            UpdateTheme = true;
        }
    }

    private void FeatureSettings_OnFeatureSettingsChanged(FeatureSettingsChangedEventArgs e)
    {
        // When page is loaded again from cache, update bindings
        UpdateBindings = true;
        UpdateTheme = true;
    }

    private async void WeatherView_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(WNowViewModel.UiState):
                var uiState = WNowViewModel.UiState;

                if (Utils.FeatureSettings.WeatherRadar)
                {
                    WNowViewModel.Weather?.LocationCoord?.Let(coords =>
                    {
                        radarViewProvider?.UpdateCoordinates(coords, true);
                    });
                }

                if (uiState.NoLocationAvailable)
                {
                    await Dispatcher.DispatchAsync(() =>
                    {
                        var banner = Banner.Make(ResStrings.prompt_location_not_set);
                        banner.Icon = new MaterialIcon(MaterialSymbol.Map);
                        banner.SetAction(ResStrings.label_fab_add_location, async () =>
                        {
                            await Navigation.PushAsync(new LocationsPage());
                        });
                        ShowBanner(banner);
                    });
                }
                else
                {
                    DismissBanner();
                }
                break;

            case nameof(WNowViewModel.Weather):
                WNowViewModel.UiState?.LocationData?.Let(locationData =>
                {
                    ForecastView.UpdateForecasts(locationData);
                    AlertsView.UpdateAlerts(locationData);

                    /*
                    Task.Run(async () =>
                    {
                        // Update home tile if it hasn't been already
                        bool isHome = Equals(locationData, await SettingsManager.GetHomeData());
                        if (isHome && (TimeSpan.FromTicks((long)(DateTime.Now.Ticks - SettingsManager.UpdateTime.Ticks)).TotalMinutes > SettingsManager.RefreshInterval))
                        {
                            await WeatherUpdateBackgroundTask.RequestAppTrigger();
                        }
                        else if (isHome || SecondaryTileUtils.Exists(locationData?.query))
                        {
                            await WeatherTileCreator.TileUpdater(locationData);
                        }
                    });
                    */
                });
                break;

            case nameof(WNowViewModel.Alerts):
                {
                    var weatherAlerts = WNowViewModel.Alerts;
                    var locationData = WNowViewModel.UiState?.LocationData;

                    /*
                    if (wm.SupportsAlerts && locationData != null)
                    {
                        _ = Task.Run(async () =>
                        {
                            // Alerts are posted to the user here. Set them as notified.
#if DEBUG
                            await WeatherAlertHandler.PostAlerts(locationData, weatherAlerts);
#endif
                            await WeatherAlertHandler.SetasNotified(locationData, weatherAlerts);

                        });
                    }
                    */
                }
                break;

            case nameof(WNowViewModel.ErrorMessages):
                {
                    var errorMessages = WNowViewModel.ErrorMessages;

                    var error = errorMessages.FirstOrDefault();

                    if (error != null)
                    {
                        OnErrorMessage(error);
                    }
                }
                break;
        }
    }

    private void InitControls()
    {
        // Refresh toolbar item
        if (DeviceInfo.Idiom == DeviceIdiom.Desktop)
        {
            ToolbarItems.Add(CreateRefreshToolbarButton());
        }

        // Condition Panel
        {
            if (DeviceInfo.Idiom == DeviceIdiom.Phone || DeviceInfo.Idiom == DeviceIdiom.Tablet)
            {

            }
            else
            {
                ListLayout.Add(
                    CreateConditionPanel()
                    .Row(0)
                );

                // Overlay
                ListLayout.Add(
                    new BoxView()
                    {
                        CornerRadius = new CornerRadius(8, 8, 0, 0)
                    }
                    .DynamicResource(BoxView.ColorProperty, "RegionColor")
                    .Row(1)
                );
            }
        }

        // Add Grid
        var GridLayout = new Grid()
        {
            RowDefinitions =
            {
                // Forecast
                new RowDefinition(GridLength.Auto),
                // Hourly Forecast
                new RowDefinition(GridLength.Auto),
                // Charts
                new RowDefinition(GridLength.Auto),
                // Details
                new RowDefinition(GridLength.Auto),
                // Sun Phase
                new RowDefinition(GridLength.Auto),
                // Radar
                new RowDefinition(GridLength.Auto),
                // Credits
                new RowDefinition(GridLength.Auto),
            }
        }.Paddings(16, 4, 16, 0);
        ListLayout.Add(GridLayout, row: 1);

        // Forecast Panel
        if (Utils.FeatureSettings.Forecast)
        {
            GridLayout.Add(
                CreateForecastPanel()
                .Row(0)
            );
        }

        // HourlyForecast Panel
        if (Utils.FeatureSettings.HourlyForecast)
        {
            GridLayout.Add(
                CreateHourlyForecastPanel()
                .Row(1)
            );
        }

        // Charts
        if (Utils.FeatureSettings.Charts)
        {
            GridLayout.Add(
                CreateChartsPanel()
                .Row(2)
            );
        }

        // Details
        if (Utils.FeatureSettings.DetailsEnabled)
        {
            GridLayout.Add(
                CreateDetailsPanel()
                .Row(3)
            );
        }

        // Sun Phase
        if (Utils.FeatureSettings.SunPhase)
        {
            GridLayout.Add(
                CreateSunPhasePanel()
                .Row(4)
            );
        }

        if (Utils.FeatureSettings.WeatherRadar)
        {
            GridLayout.Add(
                CreateRadarPanel()
                .Row(5)
            );
        }

        // Weather Credit
        GridLayout.Add(
            CreateWeatherCredit()
            .Row(6)
        );

        AdjustViewsLayout(0);
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () =>
        {
            ListLayout.WidthRequest = width;
            ListLayout.MaximumWidthRequest = width;
            MainGrid.WidthRequest = width;
            MainGrid.MaximumWidthRequest = width;

            AdjustViewsLayout(width);
        });
    }

    private void AdjustViewsLayout(double? width = null)
    {
        if (DeviceInfo.Idiom != DeviceIdiom.Phone)
        {
            var maxWidth = 1280;
            var requestedWidth = width ?? MainGrid.Width;

            foreach (var element in ResizeElements)
            {
                if (element.WidthRequest > requestedWidth)
                {
                    element.WidthRequest = requestedWidth;
                }
                else if (element.WidthRequest >= maxWidth)
                {
                    element.WidthRequest = maxWidth;
                }
                else if (element.WidthRequest != requestedWidth)
                {
                    element.WidthRequest = requestedWidth;
                }
            }
        }
    }

    protected override void OnNavigatingFrom(NavigatingFromEventArgs args)
    {
        radarViewProvider?.OnDestroyView();
        base.OnNavigatingFrom(args);
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs e)
    {
        base.OnNavigatedFrom(e);
        WNowViewModel.PropertyChanged -= WeatherView_PropertyChanged;
        UnloadBannerManager();
        UnloadSnackManager();
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs e)
    {
        base.OnNavigatedTo(e);

        AnalyticsLogger.LogEvent("WeatherNow: OnNavigatedTo");
        InitSnackManager();
        InitBannerManager();

        WNowViewModel.PropertyChanged += WeatherView_PropertyChanged;

        MainViewer.ScrollToAsync(0, 0, false);

        if (UpdateTheme)
        {
            UpdateControlTheme();
            UpdateTheme = false;
        }

        if (ClearGraphIconCache)
        {
            WeatherBox?.UpdateWeatherIcon();
            ClearGraphIconCache = false;
        }

        if (UpdateBindings)
        {
            this.ApplyBindings();
            UpdateBindings = false;
        }

        await Dispatcher.DispatchAsync(async () =>
        {
            await InitializeState();
        });
    }

    private void OnErrorMessage(ErrorMessage error)
    {
        Dispatcher.Dispatch(() =>
        {
            switch (error)
            {
                case ErrorMessage.String err:
                    {
                        ShowSnackbar(Snackbar.MakeError(err.Message, SnackbarDuration.Short));
                    }
                    break;
                case ErrorMessage.WeatherError err:
                    {
                        OnWeatherError(err.Exception);
                    }
                    break;
            }
        });

        WNowViewModel.SetErrorMessageShown(error);
    }

    private void OnWeatherError(WeatherException wEx)
    {
        switch (wEx.ErrorStatus)
        {
            case WeatherUtils.ErrorStatus.NetworkError:
            case WeatherUtils.ErrorStatus.NoWeather:
                // Show error message and prompt to refresh
                Snackbar snackbar = Snackbar.MakeError(wEx.Message, SnackbarDuration.Long);
                snackbar.SetAction(ResStrings.action_retry, () =>
                {
                    WNowViewModel.RefreshWeather(false);
                });
                ShowSnackbar(snackbar);
                break;

            case WeatherUtils.ErrorStatus.QueryNotFound:
                ShowSnackbar(Snackbar.MakeError(wEx.Message, SnackbarDuration.Long));
                break;

            default:
                // Show error message
                ShowSnackbar(Snackbar.MakeError(wEx.Message, SnackbarDuration.Long));
                break;
        }
    }

    private async Task<LocationResult> VerifyLocationData()
    {
        var locationData = WNowViewModel.UiState?.LocationData;
        bool locationChanged = false;

        // Check if current location still exists (is valid)
        if (locationData?.locationType == LocationType.Search)
        {
            if (await SettingsManager.GetLocation(locationData?.query).ConfigureAwait(true) == null)
            {
                locationData = null;
                locationChanged = true;
            }
        }

        // Load new favorite location if argument data is present
        if (args == null || args?.IsHome == true)
        {
            // Check if home location changed
            // For ex. due to GPS setting change
            var homeData = await SettingsManager.GetHomeData().ConfigureAwait(true);
            if (!Equals(locationData, homeData))
            {
                locationData = homeData;
                locationChanged = true;
            }
        }
        else if (args?.Location != null && !Equals(locationData, args?.Location))
        {
            locationData = args?.Location;
            locationChanged = true;
        }

        if (locationChanged)
        {
            if (locationData != null)
            {
                return new LocationResult.Changed(locationData);
            }
            else
            {
                return new LocationResult.ChangedInvalid(null);
            }
        }
        else
        {
            return new LocationResult.NotChanged(locationData);
        }
    }

    private async Task InitializeState()
    {
        var result = await VerifyLocationData();

        await result.Data?.Let(async locationData =>
        {
            if (locationData.locationType == LocationType.GPS && SettingsManager.FollowGPS)
            {
                if (!await this.LocationPermissionEnabled())
                {
                    var snackbar = Snackbar.Make(ResStrings.Msg_LocDeniedSettings, SnackbarDuration.Short);
                    snackbar.SetAction(ResStrings.action_settings, async () =>
                    {
                        await this.LaunchLocationSettings();
                    });
                    ShowSnackbar(snackbar);
                    return;
                }
            }
        });

        if (result is LocationResult.Changed || result is LocationResult.ChangedInvalid)
        {
            WNowViewModel.Initialize(result.Data);
        }
        else
        {
            WNowViewModel.RefreshWeather();
        }
    }

    private void RefreshBtn_Clicked(object sender, EventArgs e)
    {
        AnalyticsLogger.LogEvent("WeatherNow: RefreshButton_Click");
        if (SettingsManager.FollowGPS || WNowViewModel.UiState?.LocationData?.IsValid() == true)
            WNowViewModel.RefreshWeather(true);
    }

    private async void GotoAlertsPage()
    {
        AnalyticsLogger.LogEvent("WeatherNow: GotoAlertsPage");
        await Navigation.PushAsync(new WeatherAlertPage());
    }

    private async void GotoDetailsPage(bool IsHourly, int Position)
    {
        await Navigation.PushAsync(new WeatherDetailsPage(new DetailsPageArgs()
        {
            IsHourly = IsHourly,
            ScrollToPosition = Position
        }));
    }

    private void RadarProvider_RadarProviderChanged(RadarProviderChangedEventArgs e)
    {
        if (Utils.FeatureSettings.WeatherRadar && RadarWebViewContainer != null)
        {
            radarViewProvider?.OnDestroyView();
            radarViewProvider = RadarProvider.GetRadarViewProvider(RadarWebViewContainer);
        }
    }

    private void WeatherNow_Loaded(object sender, EventArgs e)
    {
        OnAppThemeChanged(App.Current.CurrentTheme);
        App.Current.RequestedThemeChanged += WeatherNow_RequestedThemeChanged;
    }

    private void WeatherNow_Unloaded(object sender, EventArgs e)
    {
        App.Current.RequestedThemeChanged -= WeatherNow_RequestedThemeChanged;
    }

    private void WeatherNow_RequestedThemeChanged(object sender, AppThemeChangedEventArgs args)
    {
        OnAppThemeChanged(args.RequestedTheme);
    }

    private void OnAppThemeChanged(AppTheme requestedTheme)
    {
        var isLight = requestedTheme switch
        {
            AppTheme.Light => true,
            AppTheme.Dark => false,
            _ => !App.Current.IsSystemDarkTheme, // AppTheme.Unspecified
        };

        ForecastView.IsLight = isLight;
    }

    private void UpdateControlTheme()
    {
        UpdateControlTheme(Utils.FeatureSettings.BackgroundImage);
    }

    private void UpdateControlTheme(bool backgroundEnabled)
    {
        if (backgroundEnabled)
        {
            if (GradientOverlay != null)
            {
                GradientOverlay.IsVisible = true;
            }
            ConditionPanelTextColor = Colors.White;
            if (CurTemp != null)
            {
                CurTemp.TextColor = GetTempColor();
            }
        }
        else
        {
            if (GradientOverlay != null)
            {
                GradientOverlay.IsVisible = false;
            }
            this.SetAppThemeColor(ConditionPanelTextColorProperty, Colors.Black, Colors.White);
            if (CurTemp != null)
            {
                CurTemp.TextColor = GetTempColor();
            }
        }
    }

    private Color GetTempColor()
    {
        string temp = WNowViewModel?.Weather?.CurTemp;
        string temp_str = temp?.RemoveNonDigitChars();

        if (float.TryParse(temp_str, out float temp_f))
        {
            var tempUnit = SettingsManager.TemperatureUnit;

            if (Equals(tempUnit, Units.CELSIUS) || temp.EndsWith(Units.CELSIUS))
            {
                temp_f = ConversionMethods.CtoF(temp_f);
            }

            var color = WeatherUtils.GetColorFromTempF(temp_f, Colors.Transparent);

            if (color != Colors.Transparent)
            {
                return color;
            }
        }

        return ConditionPanelTextColor;
    }

    private void BackgroundOverlay_Loaded(object sender, EventArgs e)
    {
        var bgImage = sender as Image;
        UpdateControlTheme(bgImage.Source != null ? !bgImage.Source.IsEmpty : false);
    }

    private void BackgroundOverlay_PropertyChanging(object sender, Microsoft.Maui.Controls.PropertyChangingEventArgs e)
    {
        var bgImage = sender as Image;
        if (e.PropertyName == nameof(bgImage.Source))
        {
            if (bgImage.Source != null)
            {
                bgImage.Source.PropertyChanged -= Source_PropertyChanged;
            }
        }
    }

    private void BackgroundOverlay_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        var bgImage = sender as Image;
        if (e.PropertyName == nameof(bgImage.Source))
        {
            if (bgImage.Source != null)
            {
                bgImage.Source.PropertyChanged += Source_PropertyChanged;
                UpdateControlTheme(!bgImage.Source.IsEmpty);
            }
            else
            {
                UpdateControlTheme(false);
            }
        }
    }

    private void Source_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        var src = sender as ImageSource;
        UpdateControlTheme(!src.IsEmpty);
    }
}