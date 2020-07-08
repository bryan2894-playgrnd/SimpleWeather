﻿using SimpleWeather.Utils;
using SimpleWeather.WeatherData;
using System;
using System.Globalization;

namespace SimpleWeather.Controls
{
    public class HourlyForecastItemViewModel : BaseForecastItemViewModel
    {
        internal HourlyForecast Forecast { get; private set; }

        public HourlyForecastItemViewModel(HourlyForecast hrForecast)
            : base()
        {
            if (hrForecast is null)
            {
                throw new ArgumentNullException(nameof(hrForecast));
            }
            this.Forecast = hrForecast;

            var userlang = Windows.System.UserProfile.GlobalizationPreferences.Languages[0];
            var culture = new CultureInfo(userlang);

            WeatherIcon = hrForecast.icon;

            if (culture.DateTimeFormat.ShortTimePattern.Contains("H"))
            {
                Date = hrForecast.date.ToString("ddd HH:00", culture);
                ShortDate = hrForecast.date.ToString("HH", culture);
            }
            else
            {
                Date = hrForecast.date.ToString("ddd h tt", culture);
                ShortDate = hrForecast.date.ToString("ht", culture);
            }

            Condition = hrForecast.condition;
            try
            {
                if (hrForecast.high_f.HasValue && hrForecast.high_c.HasValue)
                {
                    var value = Settings.IsFahrenheit ? Math.Round(hrForecast.high_f.Value) : Math.Round(hrForecast.high_c.Value);
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

            PoP = hrForecast.pop.HasValue ? hrForecast.pop.Value + "%" : null;

            if (hrForecast.wind_mph.HasValue && hrForecast.wind_mph >= 0 &&
                hrForecast.wind_degrees.HasValue && hrForecast.wind_degrees >= 0)
            {
                WindDirection = hrForecast.wind_degrees.GetValueOrDefault(0);

                WindDir = WeatherUtils.GetWindDirection(WindDirection);

                var speedVal = Settings.IsFahrenheit ? Math.Round(hrForecast.wind_mph.Value) : Math.Round(hrForecast.wind_kph.Value);
                var speedUnit = Settings.IsFahrenheit ? "mph" : "kph";
                WindSpeed = String.Format(culture, "{0} {1}", speedVal, speedUnit);
            }

            // Extras
            if (hrForecast.extras != null)
            {
                if (hrForecast.extras.feelslike_f.HasValue && (hrForecast.extras.feelslike_f != hrForecast.extras.feelslike_c))
                {
                    var value = Settings.IsFahrenheit ? Math.Round(hrForecast.extras.feelslike_f.Value) : Math.Round(hrForecast.extras.feelslike_c.Value);

                    DetailExtras.Add(new DetailItemViewModel(WeatherDetailsType.FeelsLike,
                           String.Format(culture, "{0}º", value)));
                }

                if (WeatherAPI.OpenWeatherMap.Equals(Settings.API) || WeatherAPI.MetNo.Equals(Settings.API))
                {
                    if (hrForecast.extras.qpf_rain_in.HasValue && hrForecast.extras.qpf_rain_in >= 0)
                    {
                        DetailExtras.Add(new DetailItemViewModel(WeatherDetailsType.PoPRain,
                            Settings.IsFahrenheit ?
                            hrForecast.extras.qpf_rain_in.Value.ToString("0.00", culture) + " in" :
                            hrForecast.extras.qpf_rain_mm.Value.ToString("0.00", culture) + " mm"));
                    }
                    if (hrForecast.extras.qpf_snow_in.HasValue && hrForecast.extras.qpf_snow_in >= 0)
                    {
                        DetailExtras.Add(new DetailItemViewModel(WeatherDetailsType.PoPSnow,
                            Settings.IsFahrenheit ?
                            hrForecast.extras.qpf_snow_in.Value.ToString("0.00", culture) + " in" :
                            hrForecast.extras.qpf_snow_cm.Value.ToString("0.00", culture) + " cm"));
                    }
                    if (hrForecast.extras.pop.HasValue && hrForecast.extras.pop >= 0)
                        DetailExtras.Add(new DetailItemViewModel(WeatherDetailsType.PoPCloudiness, PoP));
                }
                else
                {
                    if (hrForecast.extras.pop.HasValue && hrForecast.extras.pop >= 0)
                        DetailExtras.Add(new DetailItemViewModel(WeatherDetailsType.PoPChance, PoP));
                    if (hrForecast.extras.qpf_rain_in.HasValue && hrForecast.extras.qpf_rain_in >= 0)
                    {
                        DetailExtras.Add(new DetailItemViewModel(WeatherDetailsType.PoPRain,
                            Settings.IsFahrenheit ?
                            hrForecast.extras.qpf_rain_in.Value.ToString("0.00", culture) + " in" :
                            hrForecast.extras.qpf_rain_mm.Value.ToString("0.00", culture) + " mm"));
                    }
                    if (hrForecast.extras.qpf_snow_in.HasValue && hrForecast.extras.qpf_snow_in >= 0)
                    {
                        DetailExtras.Add(new DetailItemViewModel(WeatherDetailsType.PoPSnow,
                            Settings.IsFahrenheit ?
                            hrForecast.extras.qpf_snow_in.Value.ToString("0.00", culture) + " in" :
                            hrForecast.extras.qpf_snow_cm.Value.ToString("0.00", culture) + " cm"));
                    }
                }

                if (hrForecast.extras.humidity.HasValue)
                {
                    DetailExtras.Add(new DetailItemViewModel(WeatherDetailsType.Humidity,
                        String.Format(culture, "{0}%", hrForecast.extras.humidity)));
                }

                if (hrForecast.extras.dewpoint_f.HasValue && (hrForecast.extras.dewpoint_f != hrForecast.extras.dewpoint_c))
                {
                    DetailExtras.Add(new DetailItemViewModel(WeatherDetailsType.Dewpoint,
                        String.Format(culture, "{0}º",
                        Settings.IsFahrenheit ?
                                Math.Round(hrForecast.extras.dewpoint_f.Value) :
                                Math.Round(hrForecast.extras.dewpoint_c.Value))));
                }

                if (hrForecast.extras.uv_index.HasValue && hrForecast.extras.uv_index >= 0)
                {
                    UV uv = new UV(hrForecast.extras.uv_index.Value);

                    DetailExtras.Add(new DetailItemViewModel(WeatherDetailsType.UV,
                           string.Format("{0:0.0}, {1}", uv.index, uv.desc)));
                }

                if (hrForecast.extras.pressure_in.HasValue)
                {
                    var pressureVal = Settings.IsFahrenheit ? hrForecast.extras.pressure_in.Value : hrForecast.extras.pressure_mb.Value;
                    var pressureUnit = Settings.IsFahrenheit ? "in" : "mb";

                    DetailExtras.Add(new DetailItemViewModel(WeatherDetailsType.Pressure,
                        String.Format(culture, "{0:0.00} {1}", pressureVal, pressureUnit)));
                }

                if (!String.IsNullOrWhiteSpace(WindSpeed))
                {
                    DetailExtras.Add(new DetailItemViewModel(WeatherDetailsType.WindSpeed,
                        String.Format(culture, "{0}, {1}", WindSpeed, WindDir), WindDirection));
                }

                if (hrForecast.extras.visibility_mi.HasValue && hrForecast.extras.visibility_mi >= 0)
                {
                    var visibilityVal = Settings.IsFahrenheit ? hrForecast.extras.visibility_mi.Value : hrForecast.extras.visibility_km.Value;
                    var visibilityUnit = Settings.IsFahrenheit ? "mi" : "km";

                    DetailExtras.Add(new DetailItemViewModel(WeatherDetailsType.Visibility,
                           String.Format(culture, "{0:0.00} {1}", visibilityVal, visibilityUnit)));
                }
            }
        }
    }
}