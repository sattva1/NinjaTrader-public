#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Net;
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
    public class AAA_EconomicCalendar : Indicator
    {
        public class Event
        {
            public string Title { get; set; }
            public string Country { get; set; }
            public string Date { get; set; }
            public string Impact { get; set; }
            public string Forecast { get; set; }
            public string Previous { get; set; }
        }
		
        private static List<Event> cachedEvents = null;
        private static DateTime lastDownloadTime = DateTime.MinValue; // Track when data was last downloaded
        private DateTime lastDrawnDate = DateTime.MinValue; // Track the last date events were drawn
        private Bars primarySeries;

        [NinjaScriptProperty]
        [Display(Name = "Currency Codes", Order = 1, GroupName = "Parameters")]
        public string CurrencyCodes { get; set; } = "USD EUR GBP CAD JPY CNY";

        [NinjaScriptProperty]
        [Display(Name = "Impact Levels", Order = 2, GroupName = "Parameters")]
        public string ImpactLevels { get; set; } = "low medium high";

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name="Line Color", Order=3, GroupName="Parameters")]
        public Brush LineColor { get; set; } = Brushes.Yellow;

        [Browsable(false)]
        public string LineColorSerializable
        {
            get { return Serialize.BrushToString(LineColor); }
            set { LineColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name="Line Thickness", Order=4, GroupName="Parameters")]
        public int LineThickness { get; set; } = 5;

		[NinjaScriptProperty]
        [Display(Name="Line Style", Order=5, GroupName="Parameters")]
        public DashStyleHelper LineStyle { get; set; } = DashStyleHelper.Solid;

        [NinjaScriptProperty]
        [Display(Name="Line Transparency (High Impact)", Order=6, GroupName="Parameters")]
        public int LineTransparencyHigh { get; set; } = 75;

		[NinjaScriptProperty]
        [Display(Name="Line Transparency (Med Impact)", Order=7, GroupName="Parameters")]
        public int LineTransparencyMed { get; set; } = 50;

		[NinjaScriptProperty]
        [Display(Name="Line Transparency (Low Impact & Other)", Order=8, GroupName="Parameters")]
        public int LineTransparencyLow { get; set; } = 25;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Indicator to draw economic events from the ForexFactory calendar";
                Name = "AAA_EconomicCalendar";
		        Calculate = Calculate.OnBarClose;
		        IsOverlay = true;
		        DisplayInDataBox = false;
		        DrawOnPricePanel = true;
		        PaintPriceMarkers = false;
		        ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
		        IsSuspendedWhileInactive = false;
            }
		    else if (State == State.DataLoaded)
		    {
		        // Store reference to primary data series, otherwise we might end up with drawings on a wrong series
		        if (BarsArray != null && BarsArray.Length > 0)
		        {
		            primarySeries = BarsArray[0];
		        }
		    }
		}

        protected override void OnBarUpdate()
        {
            bool lastBarOnChart = IsFirstTickOfBar && (CurrentBar + (Calculate == Calculate.OnEachTick ? 0 : 1) >= ChartBars.ToIndex);
			if (!lastBarOnChart || CurrentBar < BarsRequiredToPlot)
                return;
			
            // Check if an hour has passed since the last download
            if ((DateTime.UtcNow - lastDownloadTime).TotalHours >= 1)
            {
                // Download new data and redraw events
                LoadEconomicCalendarData();
            }
            else if (lastDrawnDate != DateTime.UtcNow.Date)
            {
                // Redraw events for the new day
                DrawEventsForToday();
            }
        }
		
        private void LoadEconomicCalendarData()
        {
            try
            {
                //Print("Debug EC: downloading calendar");
                using (WebClient webClient = new WebClient())
                {
                    string csvData = webClient.DownloadString("https://nfs.faireconomy.media/ff_calendar_thisweek.csv");
                    cachedEvents = new List<Event>(); // Initialize or clear the cache
                    ParseCsvData(csvData);
                    lastDownloadTime = DateTime.UtcNow; // Update the last download time
                    DrawEventsForToday(); // Draw events after fresh download
                }
            }
            catch (Exception ex)
            {
                Print($"Debug EC: Error loading economic calendar data: {ex.Message}");
            }
        }

        private void ParseCsvData(string csvData)
        {
            var lines = csvData.Split('\n').Skip(1);
            //Print("Debug EC: lines total: " + lines.Count().ToString());

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var columns = line.Split(',');
                if (columns.Length < 7) continue;

                var evt = new Event
                {
                    Title = columns[0].Trim(),
                    Country = columns[1].Trim(),
                    Date = $"{columns[2].Trim()} {columns[3].Trim()} UTC",
                    Impact = columns[4].Trim(),
                    Forecast = columns[5].Trim(),
                    Previous = columns[6].Trim()
                };

                cachedEvents.Add(evt); // Store all events without filtering
            }
        }

        private void DrawEventsForToday()
        {
            RemoveDrawObjects();

            var today = DateTime.UtcNow.Date;

            var currencyCodesList = CurrencyCodes.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().ToUpperInvariant()).ToList();
            var impactLevelsList = ImpactLevels.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().ToLowerInvariant()).ToList();

            foreach (var evt in cachedEvents)
            {
                var eventDateTime = DateTime.ParseExact(evt.Date, "MM-dd-yyyy h:mmtt 'UTC'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

                if (eventDateTime.Date == today)
                {
                    // Apply filtering
                    var evtCountryNormalized = evt.Country.Trim().ToUpperInvariant();
                    var evtImpactNormalized = evt.Impact.Trim().ToLowerInvariant();

                    if (currencyCodesList.Contains(evtCountryNormalized) && impactLevelsList.Contains(evtImpactNormalized))
                    {
                        var transparency = GetTransparencyForImpact(evt);
                        var title = $"DataRelease_{evt.Impact}_{evt.Country}: {evt.Title} {eventDateTime}";
                        //Print($"Debug EC: drawing line: '{title}'");
                        DrawVerticalLineWithTransparency(title, eventDateTime, LineColor, transparency, LineStyle, LineThickness);
                    }
                }
            }
            lastDrawnDate = today;
        }
		
        private int GetTransparencyForImpact(Event evt)
        {
            switch (evt.Impact.Trim().ToLowerInvariant())
            {
                case "high": return LineTransparencyHigh;
                case "medium": return LineTransparencyMed;
                default: return LineTransparencyLow;
            }
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
