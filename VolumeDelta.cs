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
	public class VolumeDelta : Indicator
	{
		VolumeSplit volumeSplit;
        protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Volume Delta Indicator";
				Name										= "VolumeDelta";
				Calculate									= Calculate.OnBarClose;
				IsOverlay									= false;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= true;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				//Disable this property if your indicator requires custom values that cumulate with each new market data event. 
				//See Help Guide for additional information.
				IsSuspendedWhileInactive					= true;
				AveVolPeriod					= 15;
				VolSmooth					= 8;
				AddPlot(Brushes.ForestGreen, "DeltaBuyVol");
				AddPlot(Brushes.Firebrick, "DeltaSellVol");
				AddPlot(Brushes.MediumTurquoise, "DeltaDiffVol");
				AddPlot(new Stroke(Brushes.Fuchsia), PlotStyle.Bar, "VolDelta");
                Plots[2].Width = 2;
                Plots[3].AutoWidth = true;
            }
			else if (State == State.Configure)
			{
                volumeSplit = VolumeSplit(AveVolPeriod, VolSmooth);
            }
		}

		protected override void OnBarUpdate()
		{
            if (CurrentBar < AveVolPeriod)
                return;

            //Add your custom indicator logic here.
            Values[0][0] = (volumeSplit.SmoothSell[0] - volumeSplit.SmoothBuy[1]) / volumeSplit.SmoothBuy[1] * 100;
            Values[1][0] = (volumeSplit.SmoothSell[0] - volumeSplit.SmoothSell[1]) / volumeSplit.SmoothSell[1] * 100;
            Values[2][0] = DeltaBuyVol[0] - DeltaSellVol[0];
            Values[3][0] = 0;
            if (volumeSplit.SmoothBuy[0] > volumeSplit.SmoothSell[0])
            {
                Values[3][0] = volumeSplit.SmoothNetVol[0] / volumeSplit.SmoothSell[0] * 100;
				PlotBrushes[3][0] = Brushes.LimeGreen;
            }
            else
            {
                Values[3][0] = volumeSplit.SmoothNetVol[0] / volumeSplit.SmoothBuy[0] * 100;
                PlotBrushes[3][0] = Brushes.Salmon;
            }

			Draw.HorizontalLine(this, "ZeroLine", 0, Brushes.Gray, DashStyleHelper.Solid, 2);
        }

		#region Properties
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="AveVolPeriod", Description="Average Volume Period", Order=1, GroupName="Parameters")]
		public int AveVolPeriod
		{ get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="VolSmooth", Description="Volume smoothing period", Order=2, GroupName="Parameters")]
		public int VolSmooth
		{ get; set; }

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> DeltaBuyVol
		{
			get { return Values[0]; }
		}

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> DeltaSellVol
		{
			get { return Values[1]; }
		}

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> DeltaDiffVol
		{
			get { return Values[2]; }
		}

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> VolDelta
		{
			get { return Values[3]; }
		}
		#endregion

	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private VolumeDelta[] cacheVolumeDelta;
		public VolumeDelta VolumeDelta(int aveVolPeriod, int volSmooth)
		{
			return VolumeDelta(Input, aveVolPeriod, volSmooth);
		}

		public VolumeDelta VolumeDelta(ISeries<double> input, int aveVolPeriod, int volSmooth)
		{
			if (cacheVolumeDelta != null)
				for (int idx = 0; idx < cacheVolumeDelta.Length; idx++)
					if (cacheVolumeDelta[idx] != null && cacheVolumeDelta[idx].AveVolPeriod == aveVolPeriod && cacheVolumeDelta[idx].VolSmooth == volSmooth && cacheVolumeDelta[idx].EqualsInput(input))
						return cacheVolumeDelta[idx];
			return CacheIndicator<VolumeDelta>(new VolumeDelta(){ AveVolPeriod = aveVolPeriod, VolSmooth = volSmooth }, input, ref cacheVolumeDelta);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.VolumeDelta VolumeDelta(int aveVolPeriod, int volSmooth)
		{
			return indicator.VolumeDelta(Input, aveVolPeriod, volSmooth);
		}

		public Indicators.VolumeDelta VolumeDelta(ISeries<double> input , int aveVolPeriod, int volSmooth)
		{
			return indicator.VolumeDelta(input, aveVolPeriod, volSmooth);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.VolumeDelta VolumeDelta(int aveVolPeriod, int volSmooth)
		{
			return indicator.VolumeDelta(Input, aveVolPeriod, volSmooth);
		}

		public Indicators.VolumeDelta VolumeDelta(ISeries<double> input , int aveVolPeriod, int volSmooth)
		{
			return indicator.VolumeDelta(input, aveVolPeriod, volSmooth);
		}
	}
}

#endregion
