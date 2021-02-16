﻿using SimpleWeather.Keys;
using SimpleWeather.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Maps;

namespace SimpleWeather.UWP.Radar.RainViewer
{
    public class RainViewerViewProvider : MapTileRadarViewProvider
    {
        private const string MapsURL = "https://api.rainviewer.com/public/weather-maps.json";
        private const string URLTemplate = "{host}{path}/256/{zoomlevel}/{x}/{y}/1/1_1.png";

        private List<RadarFrame> AvailableRadarFrames;
        private MapTileSource TileSource;

        private DispatcherTimer AnimationTimer;
        private int AnimationPosition = 0;

        public RainViewerViewProvider(Border container) : base(container)
        {
            AvailableRadarFrames = new List<RadarFrame>();
        }

        public override async void UpdateMap(MapControl mapControl)
        {
            if (TileSource == null)
            {
                var dataSrc = new HttpMapTileDataSource()
                {
                    AllowCaching = true
                };
                dataSrc.UriRequested += CachingHttpMapTileDataSource_UriRequested;
                TileSource = new MapTileSource(dataSrc);
                if (IsAnimationAvailable)
                {
                    TileSource.FrameCount = AvailableRadarFrames.Count;
                    TileSource.FrameDuration = TimeSpan.FromMilliseconds(500);
                    TileSource.AutoPlay = false;

                    RadarMapContainer.OnPlayAnimation += RadarMapContainer_OnPlayAnimation;
                    RadarMapContainer.OnPauseAnimation += RadarMapContainer_OnPauseAnimation;
                };
                mapControl.TileSources.Add(TileSource);
            }

            await GetRadarFrames().ContinueWith((t) =>
            {
                if (IsAnimationAvailable)
                {
                    TileSource.FrameCount = InteractionsEnabled() && AvailableRadarFrames.Count > 0 ? AvailableRadarFrames.Count : 0;
                    RadarMapContainer.UpdateSeekbarRange(0, TileSource.FrameCount - 1);
                    AnimationTimer?.Stop();
                    TileSource?.Stop();
                    AnimationPosition = 0;
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        public override void OnDestroyView()
        {
            AnimationTimer?.Stop();
            base.OnDestroyView();
            AvailableRadarFrames?.Clear();
            TileSource = null;
        }

        private void RadarMapContainer_OnPlayAnimation(object sender, EventArgs e)
        {
            if (AnimationTimer == null)
            {
                AnimationTimer = new DispatcherTimer()
                {
                    Interval = TimeSpan.FromMilliseconds(490)
                };
                AnimationTimer.Tick += (s, ev) =>
                {
                    // Update toolbar
                    RadarMapContainer.Dispatcher.RunOnUIThread(() =>
                    {
                        if (TileSource.FrameCount > 0)
                        {
                            if (TileSource.AnimationState == MapTileAnimationState.Stopped)
                            {
                                TileSource?.Play();
                                RadarMapContainer.UpdateTimestamp(AnimationPosition = 0, AvailableRadarFrames.LastOrDefault()?.TimeStamp ?? 0);
                            }
                            else
                            {
                                AnimationPosition = (AnimationPosition + 1) % TileSource.FrameCount;
                                if (AnimationPosition <= 0)
                                {
                                    TileSource?.Stop();
                                    AnimationPosition = -1;
                                    RadarMapContainer.UpdateTimestamp(0, AvailableRadarFrames.LastOrDefault()?.TimeStamp ?? 0);
                                }
                                else
                                {
                                    RadarMapContainer.UpdateTimestamp(AnimationPosition, AvailableRadarFrames[AnimationPosition].TimeStamp);
                                }
                            }
                        }
                    });
                };
            }
            TileSource?.Play();
            AnimationTimer?.Start();
        }

        private void RadarMapContainer_OnPauseAnimation(object sender, EventArgs e)
        {
            TileSource?.Pause();
            AnimationTimer?.Stop();
        }

        private void CachingHttpMapTileDataSource_UriRequested(HttpMapTileDataSource sender, MapTileUriRequestedEventArgs args)
        {
            RadarFrame mapFrame = null;
            if (AvailableRadarFrames?.Count > 0 && args.FrameIndex < AvailableRadarFrames.Count)
            {
                if (InteractionsEnabled() && IsAnimationAvailable)
                {
                    mapFrame = AvailableRadarFrames[args.FrameIndex];
                }
                else
                {
                    mapFrame = AvailableRadarFrames.LastOrDefault();
                }
            }

            // Get the custom Uri.
            if (mapFrame != null)
            {
                args.Request.Uri = new Uri(URLTemplate.Replace("{host}", mapFrame.Host).Replace("{path}", mapFrame.Path));
            }
            else
            {
                args.Request.Uri = new Uri("about:blank");
            }
        }

        private async Task GetRadarFrames()
        {
            var HttpClient = SimpleLibrary.GetInstance().WebClient;

            try
            {
                using (var response = await HttpClient.GetAsync(new Uri(MapsURL)))
                {
                    var stream = await response.Content.ReadAsInputStreamAsync();
                    var root = JSONParser.Deserializer<Rootobject>(stream.AsStreamForRead());

                    AvailableRadarFrames.Clear();

                    if (root?.radar != null)
                    {
                        if (root.radar?.past?.Count > 0)
                        {
                            root.radar.past.RemoveAll(t => t == null);
                            AvailableRadarFrames.AddRange(
                                root.radar.past.Select(f => new RadarFrame(f.time, root.host, f.path))
                                );
                        }

                        if (root.radar?.nowcast?.Count > 0)
                        {
                            root.radar.nowcast.RemoveAll(t => t == null);
                            AvailableRadarFrames.AddRange(
                                root.radar.nowcast.Select(f => new RadarFrame(f.time, root.host, f.path))
                                );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LoggerLevel.Error, ex);
            }
        }
    }

    internal sealed class RadarFrame
    {
        public String Host { get; }
        public String Path { get; }
        public long TimeStamp { get; }

        public RadarFrame(long timeStamp, string host, string path)
        {
            Host = host;
            Path = path;
            TimeStamp = timeStamp;
        }
    }
}