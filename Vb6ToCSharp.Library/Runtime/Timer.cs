using System;
using System.Windows.Threading;

namespace Vb6ToCSharp.Runtime;

public class Timer
{
    public Action Action;

    public Timer(Action e = null, int vInterval = 1000, bool vEnabled = false)
    {
        timer.Tick += dispatcherTimer_Tick;
        Action = e;
        Interval = vInterval;
        Enabled = vEnabled;
    }

    public DispatcherTimer timer { get; } = new DispatcherTimer();

    public bool IsEnabled
    {
        get => timer.IsEnabled;
        set
        {
            timer.IsEnabled = value;
            if (value)
            {
                timer.Start();
            }
            else
            {
                timer.Stop();
            }
        }
    }

    public bool Enabled
    {
        get => IsEnabled;
        set => IsEnabled = value;
    }

    public int Interval
    {
        get => (int)timer.Interval.TotalMilliseconds;
        set => timer.Interval = new TimeSpan(0, 0, 0, 0, value);
    }

    public int IntervalSeconds
    {
        get => (int)timer.Interval.TotalSeconds;
        set => timer.Interval = new TimeSpan(0, 0, 0, value);
    }

    public dynamic Tag { get; set; }

    public Timer Discard()
    {
        Enabled = false;
        return null;
    }

    public TimeSpan getInterval()
    {
        return timer.Interval;
    }

    public void setInterval(TimeSpan value)
    {
        timer.Interval = value;
    }

    public void startTimer(int milliSeconds)
    {
        Enabled = false;
        Interval = milliSeconds;
        Enabled = true;
    }

    public void startTimer(int milliSeconds, dynamic setTag)
    {
        Tag = setTag;
        startTimer(milliSeconds);
    }

    public void startTimerSeconds(int seconds)
    {
        Enabled = false;
        IntervalSeconds = seconds; // was Interval (milliseconds)
        Enabled = true;
    }

    public void startTimerSeconds(int seconds, dynamic setTag)
    {
        Tag = setTag;
        startTimerSeconds(seconds);
    }

    public void stopTimer()
    {
        Enabled = false;
    }

    private void dispatcherTimer_Tick(object sender, EventArgs e)
    {
        if (Action != null)
        {
            Action.Invoke();
        }
    }
}