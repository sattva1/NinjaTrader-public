#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{
    public class AAA_Clock : Indicator
    {
        private DispatcherTimer updateTimer;
        private TextPosition textPosition;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Displays the current wall clock time in the chart corner";
                Name = "AAA_Clock";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = false;
                DrawOnPricePanel = true;
                PaintPriceMarkers = false;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;

                Timezone = "Eastern Standard Time";
                VerticalAlignment = VerticalAlignment.Bottom;
                HorizontalAlignment = HorizontalAlignment.Right;
                FontSize = 12;
                FontColor = Brushes.White;
                BackgroundColor = Brushes.Black;
                Opacity = 80;
            }
            else if (State == State.DataLoaded)
            {
                textPosition = ToTextPosition(VerticalAlignment, HorizontalAlignment);

                ChartControl.Dispatcher.InvokeAsync(() =>
                {
                    updateTimer = new DispatcherTimer  // (DispatcherPriority.Render)
                    {
                        Interval = TimeSpan.FromSeconds(1)
                    };
                    updateTimer.Tick += (sender, args) => UpdateClock();
                    updateTimer.Start();
                });
			}
            else if (State == State.Terminated)
            {
	            if (ChartControl != null)
	            {
	                ChartControl.Dispatcher.InvokeAsync(() =>
	                {
	                    updateTimer?.Stop();
	                });
	            }
            }
        }

		private void UpdateClock()
        {
            if (ChartControl == null) return;

            var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(Timezone);
            var currentTime = TimeZoneInfo.ConvertTime(DateTime.Now, timeZoneInfo);
            var clockText = currentTime.ToString("HH:mm:ss");

            Draw.TextFixed(this, "WallClock", clockText, textPosition,
                FontColor, new SimpleFont("Arial", FontSize), BackgroundColor, BackgroundColor, Opacity);
            
			if (ChartControl != null)
            {
                ChartControl.Dispatcher.InvokeAsync(() =>
                {
                    ForceRefresh();
                });
            }
        }

        private TextPosition ToTextPosition(VerticalAlignment vertical, HorizontalAlignment horizontal)
        {
            return (vertical, horizontal) switch
            {
                (VerticalAlignment.Top, HorizontalAlignment.Left) => TextPosition.TopLeft,
                (VerticalAlignment.Top, HorizontalAlignment.Right) => TextPosition.TopRight,
                (VerticalAlignment.Bottom, HorizontalAlignment.Left) => TextPosition.BottomLeft,
                (VerticalAlignment.Bottom, HorizontalAlignment.Right) => TextPosition.BottomRight,
                _ => TextPosition.BottomRight
            };
        }

        #region Properties
        [NinjaScriptProperty]
        [Display(Name = "Timezone", Description = "Timezone for the clock", Order = 1, GroupName = "Parameters")]
        public string Timezone { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Vertical Alignment", Description = "Vertical alignment of the clock", Order = 2, GroupName = "Parameters")]
        public VerticalAlignment VerticalAlignment { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Horizontal Alignment", Description = "Horizontal alignment of the clock", Order = 3, GroupName = "Parameters")]
        public HorizontalAlignment HorizontalAlignment { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "Font Size", Description = "Font size of the clock", Order = 4, GroupName = "Parameters")]
        public int FontSize { get; set; }

        [XmlIgnore]
        [NinjaScriptProperty]
        [Display(Name = "Font Color", Description = "Font color of the clock", Order = 5, GroupName = "Parameters")]
        public Brush FontColor { get; set; }

        [Browsable(false)]
        public string FontColorSerializable
        {
            get { return Serialize.BrushToString(FontColor); }
            set { FontColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [NinjaScriptProperty]
        [Display(Name = "Background Color", Description = "Background color of the clock", Order = 6, GroupName = "Parameters")]
        public Brush BackgroundColor { get; set; }

        [Browsable(false)]
        public string BackgroundColorSerializable
        {
            get { return Serialize.BrushToString(BackgroundColor); }
            set { BackgroundColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Opacity", Description = "Opacity of the clock background (in percent)", Order = 7, GroupName = "Parameters")]
        public int Opacity { get; set; }
        #endregion
    }
}
