﻿using System;
using System.Linq;
using Android.Content;
using Android.OS;
using Android.Support.V7.App;
using Android.Views;
using Android.Support.V4.App;
using Android.Webkit;
using Android.Util;
using Android.Support.V4.Content;
using Android;
using Android.Content.PM;
using Android.Runtime;
using Android.Widget;
using SimpleWeather.Utils;
using Android.Text.Style;
using Android.Graphics;
using Android.Text;
using Android.Support.V7.Preferences;
using SimpleWeather.Droid.Widgets;

namespace SimpleWeather.Droid
{
    [Android.App.Activity(Label = "@string/title_activity_settings", Theme = "@style/SettingsTheme")]
    public class SettingsActivity : AppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SupportActionBar.SetDisplayHomeAsUpEnabled(true);

            // Display the fragment as the main content.
            Fragment fragment = SupportFragmentManager.FindFragmentById(Android.Resource.Id.Content);

            // Check if fragment exists
            if (fragment == null)
            {
                SupportFragmentManager.BeginTransaction()
                        .Replace(Android.Resource.Id.Content, new SettingsFragment())
                        .Commit();
            }
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            int id = item.ItemId;
            if (id == Android.Resource.Id.Home)
            {
                if (!base.OnOptionsItemSelected(item))
                {
                    OnBackPressed();
                }
                return true;
            }
            return base.OnOptionsItemSelected(item);
        }

        public override void OnBackPressed()
        {
            string KEY_API = "API";

            if (SupportFragmentManager.FindFragmentById(Android.Resource.Id.Content) is SettingsFragment fragment)
            {
                ListPreference keyPref = fragment.FindPreference(KEY_API) as ListPreference;
                if (String.IsNullOrWhiteSpace(Settings.API_KEY) && WeatherData.WeatherManager.IsKeyRequired(keyPref.Value))
                {
                    // Set keyentrypref color to red
                    Toast.MakeText(this, Resource.String.message_enter_apikey, ToastLength.Long).Show();
                    return;
                }
            }

            base.OnBackPressed();
        }

        public class SettingsFragment : PreferenceFragmentCompat, ActivityCompat.IOnRequestPermissionsResultCallback
        {
            private const int PERMISSION_LOCATION_REQUEST_CODE = 0;

            // Preference Keys
            private const string KEY_ABOUTAPP = "key_aboutapp";
            private const string KEY_FOLLOWGPS = "key_followgps";
            private const string KEY_API = "API";
            private const string KEY_APIKEY = "API_KEY";
            private const string KEY_APIKEY_VERIFIED = "API_KEY_VERIFIED";
            private const string KEY_ONGOINGNOTIFICATION = "key_ongoingnotification";
            private const string KEY_NOTIFICATIONICON = "key_notificationicon";
            private const string KEY_USEALERTS = "key_usealerts";

            private const string CATEGORY_NOTIFICATION = "category_notification";
            private const string CATEGORY_API = "category_api";

            // Preferences
            private SwitchPreferenceCompat followGps;
            private DropDownPreference providerPref;
            private EditTextPreference keyEntry;
            private ISharedPreferences wuSharedPrefs;
            private bool keyVerified { get { return IsKeyVerfied(); } set { SetKeyVerified(value); } }
            private SwitchPreferenceCompat onGoingNotification;
            private DropDownPreference notificationIcon;
            private SwitchPreferenceCompat alertNotification;

            private PreferenceCategory notCategory;
            private PreferenceCategory apiCategory;

            private bool IsKeyVerfied()
            {
                return wuSharedPrefs.GetBoolean(KEY_APIKEY_VERIFIED, false);
            }

            private void SetKeyVerified(bool value)
            {
                wuSharedPrefs.Edit().PutBoolean(KEY_APIKEY_VERIFIED, value).Apply();
            }

            public override void OnCreatePreferences(Bundle savedInstanceState, string rootKey)
            {
                SetPreferencesFromResource(Resource.Xml.pref_general, null);
                HasOptionsMenu = false;
                // TODO: change pref name since this will eventually be used by other APIs
                wuSharedPrefs = Activity.GetSharedPreferences(WeatherData.WeatherAPI.WeatherUnderground, FileCreationMode.Private);

                notCategory = (PreferenceCategory)FindPreference(CATEGORY_NOTIFICATION);
                apiCategory = (PreferenceCategory)FindPreference(CATEGORY_API);

                FindPreference(KEY_ABOUTAPP).PreferenceClick += (object sender, Preference.PreferenceClickEventArgs e) =>
                {
                    // Display the fragment as the main content.
                    FragmentManager.BeginTransaction()
                            .Replace(Android.Resource.Id.Content, new AboutAppFragment())
                            .AddToBackStack(null)
                            .Commit();
                };

                followGps = (SwitchPreferenceCompat)FindPreference(KEY_FOLLOWGPS);
                followGps.PreferenceChange += (object sender, Preference.PreferenceChangeEventArgs e) =>
                {
                    SwitchPreferenceCompat pref = e.Preference as SwitchPreferenceCompat;
                    if ((bool)e.NewValue)
                    {
                        if (ContextCompat.CheckSelfPermission(Activity, Manifest.Permission.AccessFineLocation) != Permission.Granted &&
                            ContextCompat.CheckSelfPermission(Activity, Manifest.Permission.AccessCoarseLocation) != Permission.Granted)
                        {
                            ActivityCompat.RequestPermissions(this.Activity, new String[] { Manifest.Permission.AccessCoarseLocation, Manifest.Permission.AccessFineLocation },
                                    PERMISSION_LOCATION_REQUEST_CODE);
                            return;
                        }
                        else
                        {
                            // Reset home location data
                            //Settings.SaveLastGPSLocData(new WeatherData.LocationData());
                        }
                    }
                };

                keyEntry = (EditTextPreference)FindPreference(KEY_APIKEY);

                var providers = WeatherData.WeatherAPI.APIs;
                providerPref = (DropDownPreference)FindPreference(KEY_API);
                providerPref.SetEntries(providers.Select(provider => provider.Display).ToArray());
                providerPref.SetEntryValues(providers.Select(provider => provider.Value).ToArray());
                providerPref.Persistent = false;
                providerPref.PreferenceChange += (object sender, Preference.PreferenceChangeEventArgs e) => 
                {
                    ListPreference pref = e.Preference as ListPreference;

                    if (WeatherData.WeatherManager.IsKeyRequired(e.NewValue.ToString()))
                    {
                        keyEntry.Enabled = true;

                        // Reset to old value if not verified
                        if (!keyVerified)
                            Settings.API = pref.Value;
                        else
                            Settings.API = e.NewValue.ToString();

                        if (apiCategory.FindPreference(KEY_APIKEY) == null)
                            apiCategory.AddPreference(keyEntry);
                        UpdateKeySummary(providers.Find(provider => provider.Value == e.NewValue.ToString()).Display);
                    }
                    else
                    {
                        keyVerified = false;
                        wuSharedPrefs.Edit().Remove(KEY_APIKEY_VERIFIED).Apply();
                        keyEntry.Enabled = false;

                        Settings.API = e.NewValue.ToString();
                        // Clear API KEY entry to avoid issues
                        Settings.API_KEY = String.Empty;

                        apiCategory.RemovePreference(keyEntry);
                        UpdateKeySummary();
                    }

                    UpdateAlertPreference(WeatherData.WeatherManager.GetInstance().SupportsAlerts);
                };

                // Set key as verified if API Key is req for API and its set
                if (WeatherData.WeatherManager.GetInstance().KeyRequired)
                {
                    keyEntry.Enabled = true;

                    if (!String.IsNullOrWhiteSpace(Settings.API_KEY) && !keyVerified)
                        keyVerified = true;
                }
                else
                {
                    keyEntry.Enabled = false;
                    apiCategory.RemovePreference(keyEntry);
                    wuSharedPrefs.Edit().Remove(KEY_APIKEY_VERIFIED).Apply();
                }

                UpdateKeySummary();

                onGoingNotification = (SwitchPreferenceCompat)FindPreference(KEY_ONGOINGNOTIFICATION);
                onGoingNotification.PreferenceChange += (object sender, Preference.PreferenceChangeEventArgs e) =>
                {
                    SwitchPreferenceCompat pref = e.Preference as SwitchPreferenceCompat;
                    var context = App.Context;

                    // On-going notification
                    if ((bool)e.NewValue)
                    {
                        WeatherWidgetService.EnqueueWork(context, new Intent(context, typeof(WeatherWidgetService))
                            .SetAction(WeatherWidgetService.ACTION_REFRESHNOTIFICATION));

                        if (notCategory.FindPreference(KEY_NOTIFICATIONICON) == null)
                            notCategory.AddPreference(notificationIcon);
                    }
                    else
                    {
                        WeatherWidgetService.EnqueueWork(context, new Intent(context, typeof(WeatherWidgetService))
                            .SetAction(WeatherWidgetService.ACTION_REMOVENOTIFICATION));

                        notCategory.RemovePreference(notificationIcon);
                    }
                };

                notificationIcon = (DropDownPreference)FindPreference(KEY_NOTIFICATIONICON);
                notificationIcon.PreferenceChange += (object sender, Preference.PreferenceChangeEventArgs e) =>
                {
                    var context = App.Context;
                    WeatherWidgetService.EnqueueWork(context, new Intent(context, typeof(WeatherWidgetService))
                        .SetAction(WeatherWidgetService.ACTION_REFRESHNOTIFICATION));
                };

                // Remove preferences
                if (!onGoingNotification.Checked)
                {
                    notCategory.RemovePreference(notificationIcon);
                }

                alertNotification = (SwitchPreferenceCompat)FindPreference(KEY_USEALERTS);
                alertNotification.PreferenceChange += (object sender, Preference.PreferenceChangeEventArgs e) =>
                {
                    SwitchPreferenceCompat pref = e.Preference as SwitchPreferenceCompat;
                    var context = App.Context;

                    // Alert notification
                    if ((bool)e.NewValue)
                    {
                        WeatherWidgetService.EnqueueWork(context, new Intent(context, typeof(WeatherWidgetService))
                            .SetAction(WeatherWidgetService.ACTION_STARTALARM));
                    }
                    else
                    {
                        WeatherWidgetService.EnqueueWork(context, new Intent(context, typeof(WeatherWidgetService))
                            .SetAction(WeatherWidgetService.ACTION_CANCELALARM));
                    }
                };
                UpdateAlertPreference(WeatherData.WeatherManager.GetInstance().SupportsAlerts);
            }

            public override void OnDisplayPreferenceDialog(Preference preference)
            {
                const String TAG = "KeyEntryPreferenceDialogFragment";

                if (FragmentManager.FindFragmentByTag(TAG) != null)
                    return;

                if (preference is EditTextPreference && preference.Key == KEY_APIKEY)
                {
                    var fragment = KeyEntryPreferenceDialogFragment.NewInstance(preference.Key);
                    fragment.PositiveButtonClick += async (sender, e) =>
                    {
                        String key = fragment.EditText.Text;

                        String API = providerPref.Value;
                        if (await WeatherData.WeatherManager.IsKeyValid(key, API))
                        {
                            Settings.API_KEY = key;
                            Settings.API = API;

                            keyVerified = true;
                            UpdateKeySummary();
                            UpdateAlertPreference(WeatherData.WeatherManager.GetInstance().SupportsAlerts);

                            fragment.Dialog.Dismiss();
                        }
                    };

                    fragment.SetTargetFragment(this, 0);
                    fragment.Show(this.FragmentManager, TAG);
                }
                else
                {
                    base.OnDisplayPreferenceDialog(preference);
                }
            }

            private void UpdateKeySummary()
            {
                UpdateKeySummary(providerPref.Entry);
            }

            private void UpdateKeySummary(string providerAPI)
            {
                if (wuSharedPrefs.Contains(KEY_APIKEY_VERIFIED))
                {
                    ForegroundColorSpan colorSpan = new ForegroundColorSpan(keyVerified ?
                        Color.Green : Color.Red);
                    ISpannable summary = new SpannableString(keyVerified ?
                        GetString(Resource.String.message_keyverified) : GetString(Resource.String.message_keyinvalid));
                    summary.SetSpan(colorSpan, 0, summary.Length(), 0);
                    keyEntry.SummaryFormatted = summary;
                }
                else
                {
                    keyEntry.Summary = Activity.GetString(Resource.String.pref_summary_apikey, providerAPI);
                }
            }

            private void UpdateAlertPreference(bool enable)
            {
                alertNotification.Enabled = enable;
                alertNotification.Summary = enable ? 
                    Activity.GetString(Resource.String.pref_summary_alerts) : Activity.GetString(Resource.String.pref_summary_alerts_disabled);
            }

            public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
            {
                switch (requestCode)
                {
                    case PERMISSION_LOCATION_REQUEST_CODE:
                        {
                            // If request is cancelled, the result arrays are empty.
                            if (grantResults.Length > 0
                                    && grantResults[0] == Permission.Granted)
                            {
                                // permission was granted, yay!
                                // Do the task you need to do.
                                // Reset home location data
                                //Settings.SaveLastGPSLocData(new WeatherData.LocationData());
                            }
                            else
                            {
                                // permission denied, boo! Disable the
                                // functionality that depends on this permission.
                                Toast.MakeText(Activity, Resource.String.error_location_denied, ToastLength.Short).Show();
                                followGps.Checked = false;
                                Console.WriteLine(Settings.FollowGPS);
                            }
                            return;
                        }
                }
            }

            public override void OnResume()
            {
                base.OnResume();

                // Title
                AppCompatActivity activity = (AppCompatActivity)Activity;
                activity.SupportActionBar.Title = GetString(Resource.String.title_activity_settings);
            }
        }

        public class KeyEntryPreferenceDialogFragment : EditTextPreferenceDialogFragmentCompat
        {
            public EventHandler PositiveButtonClick { get; set; }
            public EventHandler NegativeButtonClick { get; set; }

            public EditText EditText;

            public KeyEntryPreferenceDialogFragment() :
                base()
            {
            }

            public static new KeyEntryPreferenceDialogFragment NewInstance(String key)
            {
                KeyEntryPreferenceDialogFragment fragment = new KeyEntryPreferenceDialogFragment();
                Bundle b = new Bundle(1);
                b.PutString(ArgKey, key);
                fragment.Arguments = b;
                return fragment;
            }

            protected override void OnBindDialogView(View view)
            {
                base.OnBindDialogView(view);

                EditText = view.FindViewById<EditText>(Android.Resource.Id.Edit);
            }

            public override void SetupDialog(Android.App.Dialog dialog, int style)
            {
                base.SetupDialog(dialog, style);
                AlertDialog alertdialog = dialog as AlertDialog;
                alertdialog.ShowEvent += (s, e) => 
                {
                    View posButton = alertdialog.GetButton((int)DialogButtonType.Positive);
                    View negButton = alertdialog.GetButton((int)DialogButtonType.Negative);
                    posButton.Click += PositiveButtonClick;
                    if (NegativeButtonClick == null)
                        negButton.Click += delegate { dialog.Dismiss(); };
                    else
                        negButton.Click += NegativeButtonClick;
                };

                EditText.Text = Settings.API_KEY;
            }
        }

        public class AboutAppFragment : PreferenceFragmentCompat
        {
            // Preference Keys
            private static string KEY_ABOUTCREDITS = "key_aboutcredits";
            private static string KEY_ABOUTOSLIBS = "key_aboutoslibs";
            private static string KEY_ABOUTVERSION = "key_aboutversion";

            public override void OnCreatePreferences(Bundle savedInstanceState, string rootKey)
            {
                SetPreferencesFromResource(Resource.Xml.pref_aboutapp, null);
                HasOptionsMenu = false;

                FindPreference(KEY_ABOUTCREDITS).PreferenceClick += (object sender, Preference.PreferenceClickEventArgs e) =>
                {
                    // Display the fragment as the main content.
                    FragmentManager.BeginTransaction()
                            .Replace(Android.Resource.Id.Content, new CreditsFragment())
                            .AddToBackStack(null)
                            .Commit();
                };
                FindPreference(KEY_ABOUTOSLIBS).PreferenceClick += (object sender, Preference.PreferenceClickEventArgs e) =>
                {
                    // Display the fragment as the main content.
                    FragmentManager.BeginTransaction()
                            .Replace(Android.Resource.Id.Content, new OSSCreditsFragment())
                            .AddToBackStack(null)
                            .Commit();
                };

                var packageInfo = Activity.PackageManager.GetPackageInfo(Activity.PackageName, 0);
                FindPreference(KEY_ABOUTVERSION).Summary = string.Format("v{0}", packageInfo.VersionName);
            }

            public override void OnResume()
            {
                base.OnResume();

                // Title
                AppCompatActivity activity = (AppCompatActivity)Activity;
                activity.SupportActionBar.Title = GetString(Resource.String.pref_title_about);
            }
        }

        public class CreditsFragment : PreferenceFragmentCompat
        {
            public override void OnCreatePreferences(Bundle savedInstanceState, string rootKey)
            {
                SetPreferencesFromResource(Resource.Xml.pref_credits, null);
                HasOptionsMenu = false;
            }

            public override void OnResume()
            {
                base.OnResume();

                // Title
                AppCompatActivity activity = (AppCompatActivity)Activity;
                activity.SupportActionBar.Title = GetString(Resource.String.pref_title_credits);
            }
        }

        public class OSSCreditsFragment : PreferenceFragmentCompat
        {
            public override void OnCreatePreferences(Bundle savedInstanceState, string rootKey)
            {
                SetPreferencesFromResource(Resource.Xml.pref_oslibs, null);
                HasOptionsMenu = false;
            }

            public override void OnResume()
            {
                base.OnResume();

                // Title
                AppCompatActivity activity = (AppCompatActivity)Activity;
                activity.SupportActionBar.Title = GetString(Resource.String.pref_title_oslibs);
            }
        }

        public class OSSCreditsPreference : Preference
        {
            public OSSCreditsPreference(Context context)
                : base(context)
            {
            }

            public OSSCreditsPreference(Context context, IAttributeSet attrs)
                : base(context, attrs)
            {
            }

            public OSSCreditsPreference(Context context, IAttributeSet attrs, int defStyleAttr)
                : base(context, attrs, defStyleAttr)
            {
            }

            public OSSCreditsPreference(Context context, IAttributeSet attrs, int defStyleAttr, int defStyleRes)
                : base(context, attrs, defStyleAttr, defStyleRes)
            {
            }

            public override void OnBindViewHolder(PreferenceViewHolder holder)
            {
                base.OnBindViewHolder(holder);

                WebView webview = holder.ItemView.FindViewById<WebView>(Resource.Id.webview);
                webview.Settings.SetLayoutAlgorithm(WebSettings.LayoutAlgorithm.SingleColumn);
                webview.LoadUrl("file:///android_asset/credits/licenses.html");
            }
        }

        public class CustomDropDownPreference : DropDownPreference
        {
            public CustomDropDownPreference(Context context)
                : base(context)
            {
            }

            public CustomDropDownPreference(Context context, IAttributeSet attrs)
                : base(context, attrs)
            {
            }

            public CustomDropDownPreference(Context context, IAttributeSet attrs, int defStyleAttr)
                : base(context, attrs, defStyleAttr)
            {
            }

            public CustomDropDownPreference(Context context, IAttributeSet attrs, int defStyleAttr, int defStyleRes)
                : base(context, attrs, defStyleAttr, defStyleRes)
            {
            }

            public CustomDropDownPreference(IntPtr handle, JniHandleOwnership transer)
                : base(handle, transer)
            {
            }

            protected override ArrayAdapter CreateAdapter()
            {
                return new ArrayAdapter(Context, Resource.Layout.dropdown_item);
            }

            public override void OnBindViewHolder(PreferenceViewHolder holder)
            {
                base.OnBindViewHolder(holder);

                float scale = Context.Resources.DisplayMetrics.Density;

                // Show spinner under preference title
                Spinner spinner = holder.ItemView.FindViewById<Spinner>(Resource.Id.spinner);
                int titlePx = (int)TypedValue.ApplyDimension(ComplexUnitType.Sp, 14, Context.Resources.DisplayMetrics);
                int paddingPx = (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, 16, Context.Resources.DisplayMetrics);
                spinner.DropDownVerticalOffset = titlePx + paddingPx - 1;
            }
        }
    }
}