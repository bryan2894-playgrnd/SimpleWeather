﻿using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleWeather.Utils
{
    public static class AsyncTask
    {
        public static ConfiguredTaskAwaitable CreateTask(Func<Task> function)
        {
            if (function is null) throw new ArgumentNullException(nameof(function));
            return function.Invoke().ConfigureAwait(false);
        }

        public static ConfiguredTaskAwaitable<T> CreateTask<T>(Func<Task<T>> function)
        {
            if (function is null) throw new ArgumentNullException(nameof(function));
            return function.Invoke().ConfigureAwait(false);
        }

        public static ConfiguredTaskAwaitable RunAsync(Task task)
        {
            if (task is null) throw new ArgumentNullException(nameof(task));
            return task.ConfigureAwait(false);
        }

        public static ConfiguredTaskAwaitable<T> RunAsync<T>(Task<T> task)
        {
            if (task is null) throw new ArgumentNullException(nameof(task));
            return task.ConfigureAwait(false);
        }

        public static ConfiguredValueTaskAwaitable RunAsync(ValueTask task)
        {
            return task.ConfigureAwait(false);
        }

        public static ConfiguredValueTaskAwaitable<T> RunAsync<T>(ValueTask<T> task)
        {
            return task.ConfigureAwait(false);
        }

        public static void Run(Action action)
        {
            Task.Run(action);
        }

        public static void Run(Action action, CancellationToken token)
        {
            Task.Run(action, token);
        }

        public static void Run(Action action, int millisDelay)
        {
            Task.Run(() =>
            {
                Task.Delay(millisDelay);

                action?.Invoke();
            });
        }

        public static void Run(Action action, int millisDelay, CancellationToken token)
        {
            Task.Run(() =>
            {
                Task.Delay(millisDelay);

                if (token.IsCancellationRequested)
                    return;

                action?.Invoke();
            });
        }

        private static Windows.UI.Core.CoreDispatcher GetDispatcher()
        {
            Windows.UI.Core.CoreDispatcher Dispatcher = null;

            try
            {
                Dispatcher = Windows.ApplicationModel.Core.CoreApplication.MainView.Dispatcher;
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Dispatcher unavailable");
            }

            return Dispatcher;
        }

        public static Task RunOnUIThread(Action action)
        {
            var Dispatcher = GetDispatcher();

            if (Dispatcher == null)
            {
                // Dispatcher is not available
                // Execute action on current thread
                action.Invoke();
                return Task.CompletedTask;
            }
            else
            {
                return Dispatcher.AwaitableRunAsync(action);
            }
        }

        public static Task<T> RunOnUIThread<T>(Func<T> function)
        {
            var Dispatcher = GetDispatcher();

            if (Dispatcher == null)
            {
                // Dispatcher is not available
                // Execute action on current thread
                return Task.FromResult(function.Invoke());
            }
            else
            {
                return Dispatcher.AwaitableRunAsync(function);
            }
        }
    }
}