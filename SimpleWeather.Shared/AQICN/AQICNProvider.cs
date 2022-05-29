﻿using SimpleWeather.Keys;
using SimpleWeather.Location;
using SimpleWeather.Utils;
using SimpleWeather.WeatherData;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using System.Net.Http;
using System.Net.Sockets;
using static SimpleWeather.Utils.APIRequestUtils;
using System.Net.Http.Headers;
using SimpleWeather.HttpClientExtensions;

namespace SimpleWeather.AQICN
{
    public sealed class AQICNProvider : IAirQualityProvider, IRateLimitedRequest
    {
        private const String QUERY_URL = "https://api.waqi.info/feed/geo:{0:0.####};{1:0.####}/?token={2}";

        private const string API_ID = "waqi";

        public long GetRetryTime() => 1000;

        public async Task<AirQualityData> GetAirQualityData(LocationData location)
        {
            AQICNData aqiData = null;

            string key = APIKeys.GetAQICNKey();
            if (String.IsNullOrWhiteSpace(key))
                return null;

            try
            {
                CheckRateLimit(API_ID);

                Uri queryURL = new Uri(string.Format(CultureInfo.InvariantCulture, QUERY_URL, location.latitude, location.longitude, key));

                // Connect to webstream
                HttpClient webClient = SharedModule.Instance.WebClient;
                var request = new HttpRequestMessage(HttpMethod.Get, queryURL);

                request.Headers.UserAgent.AddAppUserAgent();
                request.Headers.CacheControl = new CacheControlHeaderValue() 
                {
                    MaxAge = TimeSpan.FromHours(1)
                };

                using (request)
                using (var cts = new CancellationTokenSource(Settings.READ_TIMEOUT))
                using (var response = await webClient.SendAsync(request, cts.Token))
                {
                    await this.CheckForErrors(API_ID, response);
                    response.EnsureSuccessStatusCode();

                    Stream contentStream = await response.Content.ReadAsStreamAsync();

                    // Load data
                    var root = await JSONParser.DeserializerAsync<Rootobject>(contentStream);

                    aqiData = new AQICNData(root);
                }
            }
            catch (Exception ex)
            {
                aqiData = null;
                Logger.WriteLine(LoggerLevel.Error, ex, "AQICNProvider: error getting air quality data");
            }

            return aqiData;
        }
    }
}
