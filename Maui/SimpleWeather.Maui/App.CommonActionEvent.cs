﻿using SimpleWeather.Preferences;
using SimpleWeather.Utils;

namespace SimpleWeather.Maui
{
    public partial class App
    {
        private void App_OnCommonActionChanged(object sender, CommonActionChangedEventArgs e)
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            if (e.Action == CommonActions.ACTION_WEATHER_UPDATETILELOCATION)
            {
            }
            else if (e.Action == CommonActions.ACTION_SETTINGS_UPDATEAPI ||
                e.Action == CommonActions.ACTION_WEATHER_UPDATE)
            {
            }
            else if (e.Action == CommonActions.ACTION_SETTINGS_UPDATEUNIT)
            {
            }
            else if (e.Action == CommonActions.ACTION_SETTINGS_UPDATEREFRESH ||
                e.Action == CommonActions.ACTION_WEATHER_REREGISTERTASK)
            {
            }
            else if (e.Action == CommonActions.ACTION_SETTINGS_UPDATEGPS)
            {
                // Reset notification time for new location
                SettingsManager.LastPoPChanceNotificationTime = DateTimeOffset.MinValue;
            }
            else if (e.Action == CommonActions.ACTION_WEATHER_SENDLOCATIONUPDATE)
            {
                object forceUpdate = true;
                if (e.Extras?.TryGetValue(CommonActions.EXTRA_FORCEUPDATE, out forceUpdate) != true || (bool)forceUpdate)
                {
                }

                // Reset notification time for new location
                SettingsManager.LastPoPChanceNotificationTime = DateTimeOffset.MinValue;
            }
            else if (e.Action == CommonActions.ACTION_SETTINGS_UPDATEDAILYNOTIFICATION)
            {
            }
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }
    }
}
