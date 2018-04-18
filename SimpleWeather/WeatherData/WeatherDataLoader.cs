﻿using System;
using System.Linq;
using System.Threading.Tasks;
using SimpleWeather.Utils;
using System.Diagnostics;
using System.Globalization;
#if WINDOWS_UWP
using SimpleWeather.UWP;
using SimpleWeather.UWP.Helpers;
using Windows.System.UserProfile;
#elif __ANDROID__
using Android.App;
using SimpleWeather.Droid;
#if !__ANDROID_WEAR__
using SimpleWeather.Droid.App;
#endif
#endif

namespace SimpleWeather.WeatherData
{
    public class WeatherDataLoader
    {
        private IWeatherLoadedListener callback;
        private IWeatherErrorListener errorCallback;

        private LocationData location = null;
        private Weather weather = null;
        private WeatherManager wm;

        public WeatherDataLoader(IWeatherLoadedListener listener, LocationData location)
        {
            wm = WeatherManager.GetInstance();

            callback = listener;
            this.location = location;
        }

        public WeatherDataLoader(IWeatherLoadedListener listener, IWeatherErrorListener errorListener, LocationData location)
        {
            wm = WeatherManager.GetInstance();

            callback = listener;
            errorCallback = errorListener;
            this.location = location;
        }

        public void SetWeatherLoadedListener(IWeatherLoadedListener listener)
        {
            callback = listener;
        }

        public void SetWeatherErrorListener(IWeatherErrorListener listener)
        {
            errorCallback = listener;
        }

        private async Task GetWeatherData()
        {
            WeatherException wEx = null;
            bool loadedSavedData = false;

            try
            {
                weather = await wm.GetWeather(location);
            }
            catch (WeatherException weatherEx)
            {
                wEx = weatherEx;
                weather = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.StackTrace);
                weather = null;
            }

            // Load old data if available and we can't get new data
            if (weather == null)
            {
                loadedSavedData = await LoadSavedWeatherData(true);
            }
            else if (weather != null)
            {
                // Handle upgrades
                if (String.IsNullOrEmpty(location.name) || String.IsNullOrEmpty(location.tz_long))
                {
                    location.name = weather.location.name;
                    location.tz_long = weather.location.tz_long;

#if !__ANDROID_WEAR__
                    await Settings.UpdateLocation(location);
#else
                    Settings.SaveHomeData(location);
#endif
                }
                if (location.latitude == 0 && location.longitude == 0)
                {
                    location.latitude = double.Parse(weather.location.latitude);
                    location.longitude = double.Parse(weather.location.longitude);
#if !__ANDROID_WEAR__
                    await Settings.UpdateLocation(location);
#else
                    Settings.SaveHomeData(location);
#endif
                }

                await SaveWeatherData();
            }

            // Throw exception if we're unable to get any weather data
            if (weather == null && wEx != null)
            {
                throw wEx;
            }
            else if (weather == null && wEx == null)
            {
                throw new WeatherException(WeatherUtils.ErrorStatus.NoWeather);
            }
            else if (weather != null && wEx != null && loadedSavedData)
            {
                throw wEx;
            }
        }

        public async Task LoadWeatherData(bool forceRefresh)
        {
            if (forceRefresh)
            {
                try
                {
                    await GetWeatherData();
                }
                catch (WeatherException wEx)
                {
                    errorCallback?.OnWeatherError(wEx);
                }
            }
            else
                await LoadWeatherData();

            callback?.OnWeatherLoaded(location, weather);
        }

        private async Task LoadWeatherData()
        {
            /*
             * If unable to retrieve saved data, data is old, or units don't match
             * Refresh weather data
            */

            bool gotData = await LoadSavedWeatherData();

            if (!gotData)
            {
                try
                {
                    if (weather != null && weather.source != Settings.API ||
                        weather == null && location != null && location.source != Settings.API)
                    {
                        // Update location query and source for new API
                        string oldKey = location.query;

                        if (weather != null)
                            location.query = await wm.UpdateLocationQuery(weather);
                        else
                            location.query = await wm.UpdateLocationQuery(location);

                        location.source = Settings.API;

                        // Update database as well
#if !__ANDROID_WEAR__
                        if (location.locationType == LocationType.GPS)
                        {
                            Settings.SaveLastGPSLocData(location);
#if __ANDROID__
                            App.Context.StartService(
                                new Android.Content.Intent(App.Context, typeof(WearableDataListenerService))
                                    .SetAction(WearableDataListenerService.ACTION_SENDLOCATIONUPDATE));
#endif
                        }
                        else
                            await Settings.UpdateLocationWithKey(location, oldKey);
#else
                        Settings.SaveHomeData(location);
#endif

#if WINDOWS_UWP
                        // Update tile id for location
                        if (SecondaryTileUtils.Exists(oldKey))
                        {
                            SecondaryTileUtils.UpdateTileId(oldKey, location.query);
                        }
#endif
                    }

                    await GetWeatherData();
                }
                catch (WeatherException wEx)
                {
                    errorCallback?.OnWeatherError(wEx);
                }
            }
        }

        private async Task<bool> LoadSavedWeatherData(bool _override)
        {
            if (_override)
            {
                // Load weather data
                try
                {
                    weather = await Settings.GetWeatherData(location.query);

                    if (wm.SupportsAlerts)
                        weather.weather_alerts = await Settings.GetWeatherAlertData(location.query);
                }
                catch (Exception ex)
                {
                    weather = null;
                    Debug.WriteLine(ex.StackTrace);
                }

#if WINDOWS_UWP
                var userlang = GlobalizationPreferences.Languages.First();
                var culture = new CultureInfo(userlang);
#else
                var culture = CultureInfo.CurrentCulture;
#endif
                var locale = wm.LocaleToLangCode(culture.TwoLetterISOLanguageName, culture.Name);

                bool isValid = weather == null || !weather.IsValid() || weather.source != Settings.API;
                if (wm.SupportsWeatherLocale && !isValid)
                    isValid = weather.locale != locale;

                if (isValid) return false;

                return true;
            }
            else
                return await LoadSavedWeatherData();
        }

        private async Task<bool> LoadSavedWeatherData()
        {
            // Load weather data
            try
            {
                weather = await Settings.GetWeatherData(location.query);

                if (wm.SupportsAlerts)
                    weather.weather_alerts = await Settings.GetWeatherAlertData(location.query);
            }
            catch (Exception ex)
            {
                weather = null;
                Debug.WriteLine(ex.StackTrace);
            }

#if WINDOWS_UWP
            var userlang = GlobalizationPreferences.Languages.First();
            var culture = new CultureInfo(userlang);
#else
            var culture = CultureInfo.CurrentCulture;
#endif
            var locale = wm.LocaleToLangCode(culture.TwoLetterISOLanguageName, culture.Name);

            bool isValid = weather == null || !weather.IsValid() || weather.source != Settings.API;
            if (wm.SupportsWeatherLocale && !isValid)
                isValid = weather.locale != locale;

            if (isValid) return false;

            // Weather data expiration
            if (!int.TryParse(weather.ttl, out int ttl))
                ttl = Settings.DefaultInterval;

            // Check file age
            DateTimeOffset updateTime = weather.update_time;

            TimeSpan span = DateTimeOffset.Now - updateTime;
            if (span.TotalMinutes < ttl)
                return true;
            else
                return false;
        }

        private async Task SaveWeatherData()
        {
            // Save location query
            weather.query = location.query;

            // Save weather alerts
            await SaveWeatherAlerts();

            await Settings.SaveWeatherData(weather);

            // Update weather data for Wearables
#if !__ANDROID_WEAR__ && __ANDROID__
            Application.Context.StartService(
                new Android.Content.Intent(Application.Context, typeof(WearableDataListenerService))
                    .SetAction(WearableDataListenerService.ACTION_SENDWEATHERUPDATE));
#elif __ANDROID_WEAR__
            Settings.UpdateTime = weather.update_time.UtcDateTime;
#endif
        }

        private async Task SaveWeatherAlerts()
        {
            if (weather.weather_alerts != null)
            {
                // Check for previously saved alerts
                var previousAlerts = await Settings.GetWeatherAlertData(location.query);

                if (previousAlerts != null && previousAlerts.Count > 0)
                {
                    // If any previous alerts were flagged before as notified
                    // make sure to set them here as such
                    // bc notified flag gets reset when retrieving weatherdata
                    foreach(WeatherAlert alert in weather.weather_alerts)
                    {
                        if (previousAlerts.Find(walert => walert.Equals(alert)) is WeatherAlert prevAlert)
                        {
                            if (prevAlert.Notified)
                                alert.Notified = prevAlert.Notified;
                        }
                    }
                }


                await Settings.SaveWeatherAlerts(location, weather.weather_alerts);
            }
        }

        public async Task ForceLoadSavedWeatherData()
        {
            await LoadSavedWeatherData(true);

            if (weather != null)
                callback?.OnWeatherLoaded(location, weather);
            else
                errorCallback?.OnWeatherError(new WeatherException(WeatherUtils.ErrorStatus.NoWeather));
        }

        public Weather GetWeather()
        {
            return weather;
        }
    }
}
