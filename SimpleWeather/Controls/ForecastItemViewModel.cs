﻿using SimpleWeather.Utils;
using SimpleWeather.WeatherData;
using System;
using System.Globalization;
using System.Text;
using Windows.System.UserProfile;

namespace SimpleWeather.Controls
{
    public class ForecastItemViewModel : BaseForecastItemViewModel
    {
        public string LoTemp { get; set; }

        public string ConditionLong { get; set; }
        public bool ShowExtraDetail { get; set; }

        public ForecastItemViewModel(Forecast forecast, params TextForecast[] txtForecasts)
            : base()
        {
            if (forecast is null)
            {
                throw new ArgumentNullException(nameof(forecast));
            }

            var userlang = GlobalizationPreferences.Languages[0];
            var culture = new CultureInfo(userlang);

            WeatherIcon = forecast.icon;
            Date = forecast.date.ToString("ddd dd", culture);
            ShortDate = forecast.date.ToString("ddd", culture);
            Condition = forecast.condition;
            try
            {
                if (forecast.high_f.HasValue && forecast.high_c.HasValue)
                {
                    var value = Settings.IsFahrenheit ? Math.Round(forecast.high_f.Value) : Math.Round(forecast.high_c.Value);
                    HiTemp = String.Format(culture, "{0}º", value);
                }
                else
                {
                    HiTemp = "--";
                }
            }
            catch (FormatException ex)
            {
                HiTemp = "--";
                Logger.WriteLine(LoggerLevel.Error, "Invalid number format", ex);
            }
            try
            {
                if (forecast.low_f.HasValue && forecast.low_c.HasValue)
                {
                    var value = Settings.IsFahrenheit ? Math.Round(forecast.low_f.Value) : Math.Round(forecast.low_c.Value);
                    LoTemp = String.Format(culture, "{0}º", value);
                }
                else
                {
                    LoTemp = "--";
                }
            }
            catch (FormatException ex)
            {
                LoTemp = "--";
                Logger.WriteLine(LoggerLevel.Error, "Invalid number format", ex);
            }

            // Extras
            if (forecast.extras != null)
            {
                if (forecast.extras.feelslike_f.HasValue && (forecast.extras.feelslike_f != forecast.extras.feelslike_c))
                {
                    var value = Settings.IsFahrenheit ? Math.Round(forecast.extras.feelslike_f.Value) : Math.Round(forecast.extras.feelslike_c.Value);

                    DetailExtras.Add(new DetailItemViewModel(WeatherDetailsType.FeelsLike,
                           String.Format(culture, "{0}º", value)));
                }

                string Chance = PoP = forecast.extras.pop.HasValue ? forecast.extras.pop.Value + "%" : null;

                if (WeatherAPI.OpenWeatherMap.Equals(Settings.API) || WeatherAPI.MetNo.Equals(Settings.API))
                {
                    if (forecast.extras.qpf_rain_in.HasValue && forecast.extras.qpf_rain_in >= 0)
                    {
                        DetailExtras.Add(new DetailItemViewModel(WeatherDetailsType.PoPRain,
                            Settings.IsFahrenheit ?
                            forecast.extras.qpf_rain_in.Value.ToString("0.00", culture) + " in" :
                            forecast.extras.qpf_rain_mm.Value.ToString("0.00", culture) + " mm"));
                    }
                    if (forecast.extras.qpf_snow_in.HasValue && forecast.extras.qpf_snow_in >= 0)
                    {
                        DetailExtras.Add(new DetailItemViewModel(WeatherDetailsType.PoPSnow,
                            Settings.IsFahrenheit ?
                            forecast.extras.qpf_snow_in.Value.ToString("0.00", culture) + " in" :
                            forecast.extras.qpf_snow_cm.Value.ToString("0.00", culture) + " cm"));
                    }
                    if (forecast.extras.pop.HasValue && forecast.extras.pop >= 0)
                        DetailExtras.Add(new DetailItemViewModel(WeatherDetailsType.PoPCloudiness, Chance));
                }
                else
                {
                    if (forecast.extras.pop.HasValue && forecast.extras.pop >= 0)
                        DetailExtras.Add(new DetailItemViewModel(WeatherDetailsType.PoPChance, Chance));
                    if (forecast.extras.qpf_rain_in.HasValue && forecast.extras.qpf_rain_in >= 0)
                    {
                        DetailExtras.Add(new DetailItemViewModel(WeatherDetailsType.PoPRain,
                            Settings.IsFahrenheit ?
                            forecast.extras.qpf_rain_in.Value.ToString("0.00", culture) + " in" :
                            forecast.extras.qpf_rain_mm.Value.ToString("0.00", culture) + " mm"));
                    }
                    if (forecast.extras.qpf_snow_in.HasValue && forecast.extras.qpf_snow_in >= 0)
                    {
                        DetailExtras.Add(new DetailItemViewModel(WeatherDetailsType.PoPSnow,
                            Settings.IsFahrenheit ?
                            forecast.extras.qpf_snow_in.Value.ToString("0.00", culture) + " in" :
                            forecast.extras.qpf_snow_cm.Value.ToString("0.00", culture) + " cm"));
                    }
                }

                if (forecast.extras.humidity.HasValue)
                {
                    DetailExtras.Add(new DetailItemViewModel(WeatherDetailsType.Humidity,
                        String.Format(culture, "{0}%", forecast.extras.humidity.Value)));
                }

                if (forecast.extras.dewpoint_f.HasValue && (forecast.extras.dewpoint_f != forecast.extras.dewpoint_c))
                {
                    DetailExtras.Add(new DetailItemViewModel(WeatherDetailsType.Dewpoint,
                        String.Format(culture, "{0}º",
                        Settings.IsFahrenheit ?
                                Math.Round(forecast.extras.dewpoint_f.Value) :
                                Math.Round(forecast.extras.dewpoint_c.Value))));
                }

                if (forecast.extras.uv_index.HasValue && forecast.extras.uv_index >= 0)
                {
                    UV uv = new UV(forecast.extras.uv_index.Value);

                    DetailExtras.Add(new DetailItemViewModel(WeatherDetailsType.UV,
                           string.Format("{0:0.0}, {1}", uv.index, uv.desc)));
                }

                if (forecast.extras.pressure_in.HasValue)
                {
                    var pressureVal = Settings.IsFahrenheit ? forecast.extras.pressure_in.Value : forecast.extras.pressure_mb.Value;
                    var pressureUnit = Settings.IsFahrenheit ? "in" : "mb";

                    DetailExtras.Add(new DetailItemViewModel(WeatherDetailsType.Pressure,
                        String.Format(culture, "{0:0.00} {1}", pressureVal, pressureUnit)));
                }

                if (forecast.extras.wind_mph.HasValue && forecast.extras.wind_mph >= 0 &&
                    forecast.extras.wind_degrees.HasValue && forecast.extras.wind_degrees >= 0)
                {
                    var speedVal = Settings.IsFahrenheit ? Math.Round(forecast.extras.wind_mph.Value) : Math.Round(forecast.extras.wind_kph.Value);
                    var speedUnit = Settings.IsFahrenheit ? "mph" : "kph";

                    WindSpeed = String.Format(culture, "{0} {1}", speedVal, speedUnit);

                    WindDirection = forecast.extras.wind_degrees.Value;
                    WindDir = WeatherUtils.GetWindDirection(forecast.extras.wind_degrees.Value);

                    DetailExtras.Add(new DetailItemViewModel(WeatherDetailsType.WindSpeed,
                           String.Format(culture, "{0}, {1}", WindSpeed, WindDir), WindDirection));
                }

                if (forecast.extras.visibility_mi.HasValue && forecast.extras.visibility_mi >= 0)
                {
                    var visibilityVal = Settings.IsFahrenheit ? forecast.extras.visibility_mi.Value : forecast.extras.visibility_km.Value;
                    var visibilityUnit = Settings.IsFahrenheit ? "mi" : "km";

                    DetailExtras.Add(new DetailItemViewModel(WeatherDetailsType.Visibility,
                           String.Format(culture, "{0:0.00} {1}", visibilityVal, visibilityUnit)));
                }
            }

            if (txtForecasts?.Length > 0)
            {
                try
                {
                    bool dayAndNt = txtForecasts.Length == 2;
                    StringBuilder sb = new StringBuilder();

                    TextForecast fctDay = txtForecasts[0];
                    sb.Append(Settings.IsFahrenheit ? fctDay.fcttext : fctDay.fcttext_metric);

                    if (dayAndNt)
                    {
                        sb.Append(Environment.NewLine).Append(Environment.NewLine);

                        TextForecast fctNt = txtForecasts[1];
                        sb.Append(Settings.IsFahrenheit ? fctNt.fcttext : fctNt.fcttext_metric);
                    }

                    ConditionLong = sb.ToString();
                }
                catch (Exception ex)
                {
                    Logger.WriteLine(LoggerLevel.Debug, ex, "error!");
                }
            }

            ShowExtraDetail = !(String.IsNullOrWhiteSpace(WindSpeed) || String.IsNullOrWhiteSpace(PoP.Replace("%", "")));
        }
    }
}