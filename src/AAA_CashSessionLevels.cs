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

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{
    public class AAA_CashSessionLevels : Indicator
    {
        private struct PriceLevel
        {
            public DateTime Date;
            public double Price;
            public string Tag; // "open" or "close"
            public string String()
            {
                return $"<{Tag} price ({Date}): {Price}>";
            }
        }

        private List<PriceLevel> priceLevels = new List<PriceLevel>();

        // Time Parameters
        [NinjaScriptProperty]
        [Display(Name = "Session Open Time", Description = "Time in Session Timezone", Order = 1, GroupName = "Time Parameters")]
        public TimeSpan SessionOpenTime { get; set; } = new TimeSpan(9, 30, 0);

        [Browsable(false)]
        public string SessionOpenTimeSerializable
        {
            get { return SessionOpenTime.ToString(); }
            set { SessionOpenTime = TimeSpan.Parse(value); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Session Close Time", Description = "Time in Session Timezone", Order = 2, GroupName = "Time Parameters")]
        public TimeSpan SessionCloseTime { get; set; } = new TimeSpan(16, 0, 0);

        [Browsable(false)]
        public string SessionCloseTimeSerializable
        {
            get { return SessionCloseTime.ToString(); }
            set { SessionCloseTime = TimeSpan.Parse(value); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Session Timezone", Description = "Timezone of the session open/close times", Order = 3, GroupName = "Time Parameters")]
        public string SessionTimeZoneId { get; set; } = "Eastern Standard Time";

        [NinjaScriptProperty]
        [Display(Name = "Chart Timezone", Description = "Timezone of the chart data", Order = 4, GroupName = "Time Parameters")]
        public string ChartTimeZoneId { get; set; } = "Central European Standard Time";

        // Drawing Parameters
        [NinjaScriptProperty]
        [Display(Name = "Line Style", Order = 1, GroupName = "Drawing Parameters")]
        public DashStyleHelper LineStyle { get; set; } = DashStyleHelper.Solid;

        [NinjaScriptProperty]
        [Display(Name = "Line Thickness", Order = 2, GroupName = "Drawing Parameters")]
        public int LineThickness { get; set; } = 4;

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Open Line Color", Order = 3, GroupName = "Drawing Parameters")]
        public Brush OpenLineColor { get; set; } = Brushes.Red;

        [Browsable(false)]
        public string OpenLineColorSerializable
        {
            get { return Serialize.BrushToString(OpenLineColor); }
            set { OpenLineColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Close Line Color", Order = 4, GroupName = "Drawing Parameters")]
        public Brush CloseLineColor { get; set; } = Brushes.Orange;

        [Browsable(false)]
        public string CloseLineColorSerializable
        {
            get { return Serialize.BrushToString(CloseLineColor); }
            set { CloseLineColor = Serialize.StringToBrush(value); }
        }

        // Transparency levels
        [NinjaScriptProperty]
        [Display(Name = "Transparency Level 1", Order = 5, GroupName = "Drawing Parameters")]
        public int TransparencyLevel1 { get; set; } = 100;

        [NinjaScriptProperty]
        [Display(Name = "Transparency Level 2", Order = 6, GroupName = "Drawing Parameters")]
        public int TransparencyLevel2 { get; set; } = 66;

        [NinjaScriptProperty]
        [Display(Name = "Transparency Level 3", Order = 7, GroupName = "Drawing Parameters")]
        public int TransparencyLevel3 { get; set; } = 33;

        private DateTime prevBarTime = DateTime.MinValue;
        private TimeZoneInfo sessionTimeZone;
        private TimeZoneInfo chartTimeZone;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Indicator to plot horizontal lines for the closing prices at session open and close times";
                Name = "AAA_CashSessionLevels";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = false;
                DrawOnPricePanel = true;
                PaintPriceMarkers = false;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;
            }
            else if (State == State.Configure)
            {
                // Initialize timezone info
                try
                {
                    sessionTimeZone = TimeZoneInfo.FindSystemTimeZoneById(SessionTimeZoneId);
                    chartTimeZone = TimeZoneInfo.FindSystemTimeZoneById(ChartTimeZoneId);
                }
                catch (Exception ex)
                {
                    // Set to default timezones if there's an error
                    sessionTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                    chartTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
                }
            }
            else if (State == State.Transition)
            {
                priceLevels.Clear();
                CollectPriceLevels();
                DrawPriceLevels();
				prevBarTime = Bars.GetTime(CurrentBar);
            }
        }

        protected override void OnBarUpdate()
        {
            bool lastBarOnChart = IsFirstTickOfBar && (CurrentBar + (Calculate == Calculate.OnEachTick ? 0 : 1) >= ChartBars.ToIndex);
            if (!lastBarOnChart)
                return;

            DateTime currentBarTime = Bars.GetTime(CurrentBar);

            if (prevBarTime != DateTime.MinValue)
            {
                DateTime sessionOpenDateTime = GetSessionInChartTimezone(SessionOpenTime, sessionTimeZone, currentBarTime.Date);
                DateTime sessionCloseDateTime = GetSessionInChartTimezone(SessionCloseTime, sessionTimeZone, currentBarTime.Date);

                // Check if we have crossed over session open/close time
                if ((prevBarTime < sessionOpenDateTime && currentBarTime >= sessionOpenDateTime) ||
                    (prevBarTime < sessionCloseDateTime && currentBarTime >= sessionCloseDateTime))
                {
                    priceLevels.Clear();
                    CollectPriceLevels();
                    DrawPriceLevels();
                }
            }

            prevBarTime = currentBarTime;
        }

        private DateTime GetSessionInChartTimezone(TimeSpan sessionTime, TimeZoneInfo sessionTimeZone, DateTime date)
        {
            DateTime sessionTimeInSessionTimeZone = date.Add(sessionTime);
            DateTime sessionTimeUtc = TimeZoneInfo.ConvertTimeToUtc(sessionTimeInSessionTimeZone, sessionTimeZone);
            return TimeZoneInfo.ConvertTimeFromUtc(sessionTimeUtc, chartTimeZone);
        }

        private void CollectPriceLevels()
        {
            if (CurrentBar < BarsRequiredToPlot)
                return;

            // Ensure priceLevels is initialized
            if (priceLevels == null)
                priceLevels = new List<PriceLevel>();

            List<PriceLevel> tempPriceLevels = new List<PriceLevel>();

            for (int i = 0; i <= CurrentBar; i++)
            {
                DateTime barTime = Time[i];

                DateTime sessionOpenDateTime = GetSessionInChartTimezone(SessionOpenTime, sessionTimeZone, barTime.Date);
                DateTime sessionCloseDateTime = GetSessionInChartTimezone(SessionCloseTime, sessionTimeZone, barTime.Date);

                // Check for session open time alignment
                if (barTime == sessionOpenDateTime)
                {
                    tempPriceLevels.Add(new PriceLevel { Date = barTime.Date, Price = Closes[0][i], Tag = "open" });
                }

                // Check for session close time alignment
                if (barTime == sessionCloseDateTime)
                {
                    tempPriceLevels.Add(new PriceLevel { Date = barTime.Date, Price = Closes[0][i], Tag = "close" });
                }
            }

            // Sort by date descending
            tempPriceLevels = tempPriceLevels.OrderByDescending(p => p.Date).ToList();
            // Truncate to 6 elements (3 days of open and close levels)
            priceLevels = tempPriceLevels.Take(6).ToList();
        }

        private void DrawPriceLevels()
        {
            // Separate the price levels into open and close for processing
            var openPriceLevels = priceLevels.Where(pl => pl.Tag == "open").ToList();
            var closePriceLevels = priceLevels.Where(pl => pl.Tag == "close").ToList();

            // Process open price levels
            for (int i = 0; i < openPriceLevels.Count; i++)
            {
                int transparency = DetermineTransparency(i); // Determine the transparency based on the index
                DrawHorizontalLineWithTransparency($"SessionOpenLevel_{openPriceLevels[i].Date:yyyyMMdd}", openPriceLevels[i].Price, OpenLineColor, transparency, LineStyle, LineThickness);
            }

            // Process close price levels
            for (int i = 0; i < closePriceLevels.Count; i++)
            {
                int transparency = DetermineTransparency(i); // Determine the transparency based on the index
                DrawHorizontalLineWithTransparency($"SessionCloseLevel_{closePriceLevels[i].Date:yyyyMMdd}", closePriceLevels[i].Price, CloseLineColor, transparency, LineStyle, LineThickness);
            }
        }

        // Helper method to determine the transparency level based on the index
        private int DetermineTransparency(int index)
        {
            switch (index)
            {
                case 0:
                    return TransparencyLevel1;
                case 1:
                    return TransparencyLevel2;
                case 2:
                default:
                    return TransparencyLevel3;
            }
        }

        private SolidColorBrush CreateTransparentBrush(Brush lineColor, int lineTransparency)
        {
            byte alpha = (byte)(lineTransparency * 255 / 100);
            var color = ((SolidColorBrush)lineColor).Color;
            return new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
        }

        private void DrawHorizontalLineWithTransparency(string tag, double priceLevel, Brush lineColor, int lineTransparency, DashStyleHelper lineStyle, int lineThickness)
        {
            SolidColorBrush transparentBrush = CreateTransparentBrush(lineColor, lineTransparency);
            HorizontalLine horizontalLine = Draw.HorizontalLine(this, tag, priceLevel, transparentBrush, lineStyle, lineThickness) as HorizontalLine;
            if (horizontalLine != null)
            {
                horizontalLine.Stroke = new Stroke(transparentBrush, lineStyle, lineThickness);
            }
        }
    }
}
