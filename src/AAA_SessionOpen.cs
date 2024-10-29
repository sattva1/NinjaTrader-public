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

// This namespace holds Indicators in this folder and is required. Do not change it.
namespace NinjaTrader.NinjaScript.Indicators
{
    public class AAA_SessionOpen : Indicator
    {
        [NinjaScriptProperty]
        [Display(Name="US Session Open", Description="Time in NYSE timezone", Order=1, GroupName="Time Parameters")]
        public TimeSpan USSessionOpenTime { get; set; } = new TimeSpan(9, 30, 0);  // NYSE opens at 9:30 AM
		
		[Browsable(false)]
		public string USSessionOpenTimeSerializable
		{
		    get { return USSessionOpenTime.ToString(); }
		    set { USSessionOpenTime = TimeSpan.Parse(value); }
		}

		[NinjaScriptProperty]
        [Display(Name="NYSE Timezone", Description="Timezone of NYSE", Order=2, GroupName="Time Parameters")]
        public string NyseTimeZoneId { get; set; } = "Eastern Standard Time";

        [NinjaScriptProperty]
        [Display(Name="EU Session Open", Description="Time in London timezone", Order=3, GroupName="Time Parameters")]
        public TimeSpan EUSessionOpenTime { get; set; } = new TimeSpan(8, 00, 0);  // London opens at 8:00 AM
		
		[Browsable(false)]
		public string EUSessionOpenTimeSerializable
		{
		    get { return EUSessionOpenTime.ToString(); }
		    set { EUSessionOpenTime = TimeSpan.Parse(value); }
		}

		[NinjaScriptProperty]
        [Display(Name="London Timezone", Description="Timezone of London", Order=4, GroupName="Time Parameters")]
        public string LondonTimeZoneId { get; set; } = "GMT Standard Time";

        [NinjaScriptProperty]
        [Display(Name="Chart Timezone", Description="Timezone of the chart data", Order=5, GroupName="Time Parameters")]
        public string ChartTimeZoneId { get; set; } = "Central European Standard Time";

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name="Line Color", Order=6, GroupName="Drawing Parameters")]
        public Brush LineColor { get; set; } = Brushes.White;

        [Browsable(false)]
        public string LineColorSerializable
        {
            get { return Serialize.BrushToString(LineColor); }
            set { LineColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name="Line Thickness", Order=7, GroupName="Drawing Parameters")]
        public int LineThickness { get; set; } = 5;

        [NinjaScriptProperty]
        [Display(Name="Line Transparency", Order=8, GroupName="Drawing Parameters")]
        public int LineTransparency { get; set; } = 50;

		[NinjaScriptProperty]
        [Display(Name="Line Style", Order=9, GroupName="Drawing Parameters")]
        public DashStyleHelper LineStyle { get; set; } = DashStyleHelper.Dot;

        private TimeZoneInfo chartTimeZone;
        private TimeZoneInfo nyseTimeZone;
        private TimeZoneInfo londonTimeZone;
		
        private DateTime lastProcessedDate = DateTime.MinValue;
        private DateTime lastUpdateTime = DateTime.MinValue;
		
        protected override void OnStateChange()
        {
			Print($"Debug SO: Entered state: {State}");
			
            if (State == State.SetDefaults)
            {
                Description = @"Indicator to draw NYSE cash session open.";
                Name = "AAA_SessionOpen";
		        Calculate = Calculate.OnBarClose;
		        IsOverlay = true;
		        DisplayInDataBox = false;
		        DrawOnPricePanel = true;
		        PaintPriceMarkers = false;
		        ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
		        IsSuspendedWhileInactive = false;
		    }
            else if (State == State.Configure)
            {
                // Initialize timezone info
                try
                {
                    chartTimeZone = TimeZoneInfo.FindSystemTimeZoneById(ChartTimeZoneId);
                    nyseTimeZone = TimeZoneInfo.FindSystemTimeZoneById(NyseTimeZoneId);
                    londonTimeZone = TimeZoneInfo.FindSystemTimeZoneById(LondonTimeZoneId);
                }
                catch (Exception ex)
                {
                    //Print($"Debug SO: Error initializing timezones: {ex.Message}");
                    // Set to default timezones if there's an error
                    chartTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
                    nyseTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                    londonTimeZone = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
                }
            }
		}

        protected override void OnBarUpdate()
        {
            bool lastBarOnChart = IsFirstTickOfBar && (CurrentBar + (Calculate == Calculate.OnEachTick ? 0 : 1) >= ChartBars.ToIndex);
			if (!lastBarOnChart)
                return;
            
			// Only process on the last bar to minimize resource usage
            if (CurrentBar < BarsRequiredToPlot)
                return;

            DateTime currentBarDate = Bars.GetTime(CurrentBar).Date;

            // Update at most once per hour or when the date changes
            if ((DateTime.Now - lastUpdateTime).TotalHours >= 1 || currentBarDate != lastProcessedDate)
            {
                DrawSessionOpenLineIfNeeded("US", USSessionOpenTime, nyseTimeZone, currentBarDate);
                DrawSessionOpenLineIfNeeded("EU", EUSessionOpenTime, londonTimeZone, currentBarDate);

                lastProcessedDate = currentBarDate;
                lastUpdateTime = DateTime.Now;
            }
        }
		
        protected void DrawSessionOpenLineIfNeeded(string tag, TimeSpan sessionOpenTime, TimeZoneInfo timezone, DateTime date)
        {
            // Define a unique key for today's session open line to avoid duplication
            string sessionOpenLineTag = $"SessionOpenTime_{tag}_{date.ToString("yyyyMMdd")}";

            // Check if the line has already been drawn by looking for an existing object with the same tag
            if (!DrawObjects.Any(d => d.Tag.Equals(sessionOpenLineTag)))
            {
                // Get the session open date time for the specified date
                DateTime sessionOpenInChartTimezone = GetSessionInChartTimezone(sessionOpenTime, timezone, date);
                Print($"Debug SO: drawing session open at {sessionOpenInChartTimezone}");
                DrawVerticalLineWithTransparency(sessionOpenLineTag, sessionOpenInChartTimezone, LineColor, LineTransparency, LineStyle, LineThickness);
            }
        }

        private DateTime GetSessionInChartTimezone(TimeSpan sessionOpenTime, TimeZoneInfo timezone, DateTime date)
        {
            DateTime openTimeInSessionTimezone = date.Add(sessionOpenTime);
            DateTime openTimeUtc = TimeZoneInfo.ConvertTimeToUtc(openTimeInSessionTimezone, timezone);
            return TimeZoneInfo.ConvertTimeFromUtc(openTimeUtc, chartTimeZone);
        }

		private SolidColorBrush CreateTransparentBrush(Brush lineColor, int lineTransparency)
		{
		    byte alpha = (byte)(lineTransparency * 255 / 100);
		    var color = ((SolidColorBrush)lineColor).Color;
		    return new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
		}
		
		private void DrawVerticalLineWithTransparency(string tag, DateTime dateTime, Brush lineColor, int lineTransparency, DashStyleHelper lineStyle, int lineThickness)
		{
		    SolidColorBrush transparentBrush = CreateTransparentBrush(lineColor, lineTransparency);
		    VerticalLine verticalLine = Draw.VerticalLine(this, tag, dateTime, transparentBrush, lineStyle, lineThickness) as VerticalLine;
		    if (verticalLine != null)
		    {
		        verticalLine.Stroke = new Stroke(transparentBrush, lineStyle, lineThickness);
		    }
		}
	}
}
