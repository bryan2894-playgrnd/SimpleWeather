using SimpleWeather.Maui.MaterialIcons;

namespace SimpleWeather.Maui.Radar;

public partial class RadarToolbar : ContentView
{
    private bool isButtonChecked = false;

    public RadarToolbar()
    {
        this.InitializeComponent();
    }

    private void PlayButton_Clicked(object sender, EventArgs e)
    {
        var button = sender as ImageButton;
        isButtonChecked = !isButtonChecked;

        if (isButtonChecked)
        {
            button.Source = new MaterialIcon(MaterialSymbol.Pause) { Size = 24 };
            OnPlayAnimation?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            button.Source = new MaterialIcon(MaterialSymbol.Play) { Size = 24 };
            OnPauseAnimation?.Invoke(this, EventArgs.Empty);
        }
    }

    public View? MapContainerChild
    {
        get { return MapContainer.Content; }
        set { MapContainer.Content = value; }
    }

    public bool IsToolbarVisible
    {
        get { return AnimationToolbar.IsVisible; }
        set { AnimationToolbar.IsVisible = value; }
    }

    public event EventHandler OnPlayAnimation;
    public event EventHandler OnPauseAnimation;

    public event EventHandler<ValueChangedEventArgs> OnSliderValueChanged;

    public void UpdateSeekbarRange(int minimumPosition, int maxPosition)
    {
        AnimationSlider.Maximum = Math.Max(maxPosition, 1);
        AnimationSlider.Minimum = minimumPosition;
    }

    public void UpdateTimestamp(int position, long timestamp)
    {
        AnimationSlider.Value = position;

        var dateTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).ToLocalTime();
        TimestampBlock.Text = string.Format("{0} {1}", dateTime.ToString("ddd"), dateTime.ToString("t"));
    }

    private void AnimationSlider_ValueChanged(object sender, ValueChangedEventArgs e)
    {
        OnSliderValueChanged?.Invoke(sender, e);
    }
}