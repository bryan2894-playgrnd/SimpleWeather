﻿using GLib;
using SimpleWeather.UWP;
using System;
using Uno.UI.Runtime.Skia;

namespace SimpleWeather.Uno.Skia.Gtk
{
    internal class Program
    {
        static void Main(string[] args)
        {
            ExceptionManager.UnhandledException += delegate (UnhandledExceptionArgs expArgs)
            {
                Console.WriteLine("GLIB UNHANDLED EXCEPTION" + expArgs.ExceptionObject.ToString());
                expArgs.ExitApplication = true;
            };

            var host = new GtkHost(() => new App())
            {
                RenderSurfaceType = RenderSurfaceType.Software
            };
            host.Run();
        }
    }
}
