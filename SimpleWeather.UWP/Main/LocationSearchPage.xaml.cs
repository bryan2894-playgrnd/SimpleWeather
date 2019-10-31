﻿using SimpleWeather.Controls;
using SimpleWeather.Location;
using SimpleWeather.Utils;
using SimpleWeather.UWP.Controls;
using SimpleWeather.UWP.Helpers;
using SimpleWeather.WeatherData;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace SimpleWeather.UWP.Main
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LocationSearchPage : CustomPage, IBackRequestedPage, IDisposable
    {
        private CancellationTokenSource cts = new CancellationTokenSource();
        private WeatherManager wm;
        public ObservableCollection<LocationQueryViewModel> LocationQuerys { get; set; }
        private ProgressRing LoadingRing { get { return Location?.ProgressRing; } }

        public LocationSearchPage()
        {
            this.InitializeComponent();

            wm = WeatherManager.GetInstance();

            LocationQuerys = new ObservableCollection<LocationQueryViewModel>();

            // CommandBar
            CommandBarLabel = App.ResLoader.GetString("Nav_Locations/Label");
            PrimaryCommands = new List<ICommandBarElement>(0);
        }

        public Task<bool> OnBackRequested()
        {
            if (Frame.CanGoBack) Frame.GoBack(); else Frame.Navigate(typeof(LocationsPage));

            var tcs = new TaskCompletionSource<bool>();
            tcs.SetResult(true);

            return tcs.Task;
        }

        public void Dispose()
        {
            cts.Dispose();
        }

        private DispatcherTimer timer;

        private void Location_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            // Cancel pending searches
            cts.Cancel();
            cts = new CancellationTokenSource();
            var ctsToken = cts.Token;
            // user is typing: reset already started timer (if existing)
            if (timer != null && timer.IsEnabled)
                timer.Stop();

            if (String.IsNullOrWhiteSpace(sender.Text))
            {
                // Cancel pending searches
                cts.Cancel();
                cts = new CancellationTokenSource();
                // Hide flyout if query is empty or null
                LocationQuerys.Clear();
                sender.IsSuggestionListOpen = false;
            }
            else
            {
                timer = new DispatcherTimer()
                {
                    Interval = TimeSpan.FromMilliseconds(1000)
                };
                timer.Tick += (t, e) =>
                {
                    if (!String.IsNullOrWhiteSpace(sender.Text) && args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                    {
                        String query = sender.Text;

                        Task.Run(async () =>
                        {
                            if (ctsToken.IsCancellationRequested) return;

                            ObservableCollection<LocationQueryViewModel> results;

                            try
                            {
                                results = await wm.GetLocations(query);
                            }
                            catch (WeatherException ex)
                            {
                                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                {
                                    ShowSnackbar(Snackbar.Make(ex.Message, SnackbarDuration.Short));
                                });
                                results = new ObservableCollection<LocationQueryViewModel>() { new LocationQueryViewModel() };
                            }

                            if (ctsToken.IsCancellationRequested) return;

                            // Refresh list
                            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                LocationQuerys = results;
                                sender.ItemsSource = null;
                                sender.ItemsSource = LocationQuerys;
                                sender.IsSuggestionListOpen = true;
                            });
                        });
                    }
                    else if (String.IsNullOrWhiteSpace(sender.Text))
                    {
                        // Cancel pending searches
                        cts.Cancel();
                        cts = new CancellationTokenSource();
                        // Hide flyout if query is empty or null
                        LocationQuerys.Clear();
                        sender.IsSuggestionListOpen = false;
                    }

                    timer.Stop();
                };
                timer.Start();
            }
        }

        private void Location_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is LocationQueryViewModel theChosenOne)
            {
                if (String.IsNullOrEmpty(theChosenOne.LocationQuery))
                    sender.Text = String.Empty;
                else
                    sender.Text = theChosenOne.LocationName;
            }

            sender.IsSuggestionListOpen = false;
        }

        private async void Location_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            LocationQueryViewModel query_vm = null;

            if (args.ChosenSuggestion != null)
            {
                // User selected an item from the suggestion list, take an action on it here.
                var theChosenOne = args.ChosenSuggestion as LocationQueryViewModel;

                if (!String.IsNullOrEmpty(theChosenOne.LocationQuery))
                    query_vm = theChosenOne;
                else
                    query_vm = new LocationQueryViewModel();
            }
            else if (!String.IsNullOrEmpty(args.QueryText))
            {
                // Use args.QueryText to determine what to do.
                query_vm = await Task.Run(async () => 
                {
                    ObservableCollection<LocationQueryViewModel> results;

                    try
                    {
                        results = await wm.GetLocations(args.QueryText);
                    }
                    catch (WeatherException ex)
                    {
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            ShowSnackbar(Snackbar.Make(ex.Message, SnackbarDuration.Short));
                        });
                        results = new ObservableCollection<LocationQueryViewModel>() { new LocationQueryViewModel() };
                    }

                    var result = results.FirstOrDefault();
                    if (result != null && !String.IsNullOrWhiteSpace(result.LocationQuery))
                    {
                        sender.Text = result.LocationName;
                        return result;
                    }
                    else
                    {
                        return new LocationQueryViewModel();
                    }
                });
            }
            else if (String.IsNullOrWhiteSpace(args.QueryText))
            {
                // Stop since there is no valid query
                return;
            }

            if (String.IsNullOrWhiteSpace(query_vm.LocationQuery))
            {
                // Stop since there is no valid query
                return;
            }

            // Cancel other tasks
            cts.Cancel();
            cts = new CancellationTokenSource();
            var ctsToken = cts.Token;

            LoadingRing.IsActive = true;

            if (ctsToken.IsCancellationRequested)
            {
                LoadingRing.IsActive = false;
                return;
            }

            // Need to get FULL location data for HERE API
            // Data provided is incomplete
            if (WeatherAPI.Here.Equals(query_vm.LocationSource)
                    && query_vm.LocationLat == -1 && query_vm.LocationLong == -1
                    && query_vm.LocationTZ_Long == null)
            {
                try
                {
                    query_vm = await new HERE.HERELocationProvider().GetLocationFromLocID(query_vm.LocationQuery, query_vm.WeatherSource);
                }
                catch (WeatherException ex)
                {
                    ShowSnackbar(Snackbar.Make(ex.Message, SnackbarDuration.Short));
                    LoadingRing.IsActive = false;
                    return;
                }
            }
            else if (WeatherAPI.BingMaps.Equals(query_vm.LocationSource)
                    && query_vm.LocationLat == -1 && query_vm.LocationLong == -1
                    && query_vm.LocationTZ_Long == null)
            {
                try
                {
                    query_vm = await new Bing.BingMapsLocationProvider().GetLocationFromAddress(query_vm.LocationQuery, query_vm.WeatherSource);
                }
                catch (WeatherException ex)
                {
                    ShowSnackbar(Snackbar.Make(ex.Message, SnackbarDuration.Short));
                    LoadingRing.IsActive = false;
                    return;
                }
            }

            // Check if location already exists
            var locData = await Settings.GetLocationData();
            if (locData.Exists(l => l.query == query_vm.LocationQuery))
            {
                Frame.Navigate(typeof(LocationsPage));
                return;
            }

            if (ctsToken.IsCancellationRequested)
            {
                LoadingRing.IsActive = false;
                return;
            }

            var location = new LocationData(query_vm);
            if (!location.IsValid())
            {
                ShowSnackbar(Snackbar.Make(App.ResLoader.GetString("WError_NoWeather"), SnackbarDuration.Short));
                LoadingRing.IsActive = false;
                return;
            }
            var weather = await Settings.GetWeatherData(location.query);
            if (weather == null)
            {
                try
                {
                    weather = await wm.GetWeather(location);
                }
                catch (WeatherException wEx)
                {
                    weather = null;
                    ShowSnackbar(Snackbar.Make(wEx.Message, SnackbarDuration.Short));
                }
            }

            if (weather == null)
            {
                LoadingRing.IsActive = false;
                return;
            }

            // We got our data so disable controls just in case
            sender.IsSuggestionListOpen = false;

            // Save data
            await Settings.AddLocation(location);
            if (wm.SupportsAlerts && weather.weather_alerts != null)
                await Settings.SaveWeatherAlerts(location, weather.weather_alerts);
            await Settings.SaveWeatherData(weather);

            var panelView = new LocationPanelViewModel(weather)
            {
                LocationData = location
            };

            // Hide add locations panel
            LoadingRing.IsActive = false;
            if (Frame.CanGoBack) Frame.GoBack(); else Frame.Navigate(typeof(LocationsPage));
        }
    }
}
