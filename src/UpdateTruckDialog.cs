using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace BBDesktop;

// Local native WPF drawing; no WebView overlay or remote animation resource.
internal sealed class UpdateTruckDialog : Window
{
    private readonly TextBlock _heading, _detail;
    private readonly ProgressBar _progress;
    private readonly TaskCompletionSource<bool> _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal bool IsOpen { get; private set; }
    private static SolidColorBrush Brush(uint value) => new(Color.FromRgb((byte)(value >> 16), (byte)(value >> 8), (byte)value));
    private static TextBlock Label(string text, double size, uint color, bool bold = false) => new()
    {
        Text = text, FontSize = size, Foreground = Brush(color),
        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
        TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 3, 0, 3)
    };
    internal UpdateTruckDialog(Window owner, string title, string detail, bool downloading = false)
    {
        Owner = owner; Title = "BIG BROTHER Desktop — Application Update";
        Width = 440; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = false;
        Background = Brushes.White;
        var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
        panel.Children.Add(Label("BIG BROTHER", 26, 0x173b70, true));
        panel.Children.Add(Label("D E S K T O P", 12, 0x52718e, true));
        panel.Children.Add(new UpdateTruckDrawing { Height = 165, Margin = new Thickness(0, 8, 0, 8) });
        _heading = Label(title, 17, 0x173b70, true); panel.Children.Add(_heading);
        _detail = Label(detail, 12, 0x52718e); panel.Children.Add(_detail);
        _progress = new ProgressBar { Height = 7, Minimum = 0, Maximum = 100,
            IsIndeterminate = !downloading, Foreground = Brush(0x174a91),
            Margin = new Thickness(0, 14, 0, 12) };
        panel.Children.Add(_progress);
        panel.Children.Add(Label("Fast. Reliable. Always Moving.", 12, 0x52718e));
        var close = new Button { Content = "Close", Width = 100, Height = 32,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        close.Click += (_, _) => CloseIfOpen(); panel.Children.Add(close);
        Content = panel;
        Loaded += (_, _) => IsOpen = true;
        Closed += (_, _) => { IsOpen = false; _closed.TrySetResult(true); };
    }
    internal void CloseIfOpen() { if (IsOpen) Close(); }
    internal async Task FinishAsync(string title, string detail)
    {
        if (!IsOpen) return;
        _heading.Text = title; _detail.Text = detail;
        _progress.IsIndeterminate = false; _progress.Value = 100;
        await Task.WhenAny(Task.Delay(5000), _closed.Task);
        CloseIfOpen();
    }
    internal void SetDownloadProgress(int percent, string version)
    {
        if (!IsOpen) return;
        _progress.Value = percent;
        _detail.Text = "BIG BROTHER Desktop v" + version + " · " + percent + "% downloaded";
    }
}
internal sealed class UpdateTruckDrawing : FrameworkElement
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    internal UpdateTruckDrawing()
    {
        _timer.Tick += (_, _) => InvalidateVisual();
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
        IsHitTestVisible = false;
    }
    private static SolidColorBrush B(uint value) => new(Color.FromRgb((byte)(value >> 16), (byte)(value >> 8), (byte)value));
    private static void DrawRect(DrawingContext d, double x, double y, double w, double h, double r, uint color)
        => d.DrawRoundedRectangle(B(color), null, new Rect(x,y,w,h),r,r);
    private static void Line(DrawingContext d,double x,double y,double x2,double y2,uint color,double width)
        => d.DrawLine(new Pen(B(color),width){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},new Point(x,y),new Point(x2,y2));
    private static void Circle(DrawingContext d,double x,double y,double r,uint color)
        => d.DrawEllipse(B(color),null,new Point(x,y),r,r);
    private static void Polygon(DrawingContext d,uint color,params Point[] points)
    {
        var g=new StreamGeometry();
        using(var c=g.Open()){c.BeginFigure(points[0],true,true);for(int i=1;i<points.Length;i++)c.LineTo(points[i],true,false);}
        d.DrawGeometry(B(color),null,g);
    }
    private void Text(DrawingContext d,string value,double x,double y,double size,uint color,bool bold)
    {
        var t=new FormattedText(value,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,bold?FontWeights.Bold:FontWeights.Normal,FontStretches.Normal),
            size,B(color),VisualTreeHelper.GetDpi(this).PixelsPerDip);
        d.DrawText(t,new Point(x-t.Width/2,y-t.Height/2));
    }
    protected override void OnRender(DrawingContext d)
    {
        base.OnRender(d);
        double scale=Math.Min(ActualWidth/320,ActualHeight/165);
        if(scale<=0)return;
        double elapsed=_clock.Elapsed.TotalSeconds;
        d.PushTransform(new TranslateTransform((ActualWidth-320*scale)/2,(ActualHeight-165*scale)/2));
        d.PushTransform(new ScaleTransform(scale,scale));
        d.PushClip(new RectangleGeometry(new Rect(0,0,320,165)));
        double shift=elapsed%0.9/0.9*25;
        Line(d,45-shift,70,80-shift,70,0x94b4d5,3);
        Line(d,23-shift,86,67-shift,86,0x94b4d5,3);
        Line(d,48-shift,101,72-shift,101,0x94b4d5,3);
        d.PushTransform(new TranslateTransform(0,-1-Math.Sin(elapsed*Math.PI*4)));
        DrawRect(d,84,40,115,76,7,0x174a91);
        Polygon(d,0x3287c5,new(203,62),new(239,62),new(265,93),new(265,116),new(203,116));
        Polygon(d,0xdceefb,new(211,70),new(235,70),new(254,93),new(211,93));
        DrawRect(d,79,111,191,10,4,0x173b70);
        Text(d,"BIG BROTHER",141,72,13,0xffffff,true);
        Text(d,"D E S K T O P",141,89,8,0xc5ddf5,false);
        foreach(double x in new double[]{116,239}){
            Circle(d,x,122,16,0x253447);Circle(d,x,122,7,0xb5c8d9);
            d.PushTransform(new RotateTransform(elapsed*360,x,122));
            Line(d,x-5,122,x+5,122,0x657c94,1.5);d.Pop();
        }
        DrawRect(d,255,101,10,5,2,0xffe29a);
        d.Pop();
        Line(d,10,140,310,140,0xa7b5c5,2);
        double road=elapsed%0.9/0.9*50;
        for(int x=10;x<=360;x+=50)Line(d,x-road,149,x+25-road,149,0xa7b5c5,3);
        d.Pop();d.Pop();d.Pop();
    }
}
