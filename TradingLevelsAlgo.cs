#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using NinjaTrader.Cbi;
using NinjaTrader.CQG.ProtoBuf;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.MarketAnalyzerColumns;
using NinjaTrader.NinjaScript.SuperDomColumns;
using SharpDX;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Tab;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it.
namespace NinjaTrader.NinjaScript.Strategies
{
    /* TODO LIST
    // BUG: Delta shading not showing always
    // TODO: Create dynamic trim mode. Have a level at which we will trim the trade. This level will also be linked into the main levels. Offset from limit (2) and searchrange (10) [Dynamic Trim] ***** IMPORTANT *****
    // TODO: Change to process on tick and have trading on first tick ***** IMPORTANT *****
    // TODO: Big win cutoffs (if we get 3 big wins in a day, stop trading) [Gain Protection]
    // TODO: Create trailing drawdown stop. If we hit a certain drawdown, stop trading [Gain Protection]
    // TODO: Look at fib levels to improve drawing of levels
    // FEATURE: Cancel order when in chopzone
    // FEATURE: Add timeout after two bad trades in succession
    // FEATURE: Add more levels to protective trades
    // FEATURE: Use wicksize to identify chop
    // FEATURE: Create standalone volume indicator
    // FEATURE: Create chop indicator with trend chop detection and momentum and delta momentum
    */
    public class TradingLevelsAlgo : Strategy
    {
        #region Properties
        #region 1. Main Parameters
        [NinjaScriptProperty]
        [Display(Name = "MiniContracts", Description = "Enable mini contracts", Order = 1, GroupName = "1. Main Parameters")]
        public bool MiniContracts
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "TradeQuantity", Description = "Number of contracts to trade", Order = 2, GroupName = "1. Main Parameters")]
        public int TradeQuantity
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, double.MaxValue)]
        [Display(Name = "MaxLossRatio", Description = "Maximum loss before trading stops", Order = 4, GroupName = "1. Main Parameters")]
        public double MaxLossRatio
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, double.MaxValue)]
        [Display(Name = "MaxGainRatio", Description = "Maximum daily gain before trading stops", Order = 5, GroupName = "1. Main Parameters")]
        public double MaxGainRatio
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, double.MaxValue)]
        [Display(Name = "LossCutOffRatio", Description = "Price to be considered a loss for consec. losses check", Order = 6, GroupName = "1. Main Parameters")]
        public double LossCutOffRatio
        { get; set; }
        #endregion

        #region 2. Volume
        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "AveVolPeriod", Description = "Average Volume Period", Order = 42, GroupName = "2. Volume")]
        public int AveVolPeriod
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "VolSmooth", Description = "Volume smoothing period", Order = 43, GroupName = "2. Volume")]
        public int VolSmooth
        { get; set; }
        #endregion

        #region 3. Dynamic Trades
        [NinjaScriptProperty]
        [Display(Name = "EnableDynamicSettings", Description = "Use Dynamic Parameters based on delta volume", Order = 44, GroupName = "3. Dynamic Trades")]
        public bool EnableDynamicSettings
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "BarsToMissNegDelta", Description = "Number of bars to miss when in negative delta volume", Order = 45, GroupName = "3. Dynamic Trades")]
        public int BarsToMissNegDelta
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "BarsToMissPosDelta", Description = "Number of bars to miss when in positive delta volume", Order = 46, GroupName = "3. Dynamic Trades")]
        public int BarsToMissPosDelta
        { get; set; }

        [NinjaScriptProperty]
        [Range(-100, 100)]
        [Display(Name = "DeltaPosCutOff", Description = "Delta volume cutoff for positive delta trades (%)", Order = 47, GroupName = "3. Dynamic Trades")]
        public double DeltaPosCutOff
        { get; set; }

        [NinjaScriptProperty]
        [Range(-100, 100)]
        [Display(Name = "DeltaNegCutOff", Description = "Delta volume cutoff for negative delta trades (%)", Order = 48, GroupName = "3. Dynamic Trades")]
        public double DeltaNegCutOff
        { get; set; }
        #endregion

        #region 3. Dynamic Stoploss
        [NinjaScriptProperty]
        [Display(Name = "EnableDynamicSL", Description = "Enable SL move on profit", Order = 48, GroupName = "3. Dynamic Stoploss")]
        public bool EnableDynamicSL
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "ProfitToMoveSL", Description = "Profit level to move SL", Order = 49, GroupName = "3. Dynamic Stoploss")]
        public double ProfitToMoveSL
        { get; set; }

        [NinjaScriptProperty]
        [Range(-10, double.MaxValue)]
        [Display(Name = "SLNewLevel", Description = "New SL level after profit", Order = 50, GroupName = "3. Dynamic Stoploss")]
        public double SLNewLevel
        { get; set; }
        #endregion

        #region 3. Dynamic Takeprofit
        [NinjaScriptProperty]
        [Display(Name = "EnableDynamicTP", Description = "Enable dynamic TP based on delta momentum", Order = 56, GroupName = "3. Dynamic Takeprofit")]
        public bool EnableDynamicTP
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "MoveLowerOnly", Description = "Move TP lower only", Order = 57, GroupName = "3. Dynamic Takeprofit")]
        public bool MoveLowerOnly
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "LevelDetectRange", Description = "Range to detect level to adjust TP to", Order = 58, GroupName = "3. Dynamic Takeprofit")]
        public double LevelDetectRange
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "TPOffset", Description = "Offset from detected level for TP", Order = 59, GroupName = "3. Dynamic Takeprofit")]
        public double TPOffset
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "TPCalcFromInitTrigger", Description = "Calculate TP level from initial trigger level", Order = 68, GroupName = "3. Dynamic Takeprofit")]
        public bool TPCalcFromInitTrigger
        { get; set; }
        #endregion

        #region 4. Chop Zone
        [NinjaScriptProperty]
        [Display(Name = "EnableChopZone", Description = "Enable chop zone filter", Order = 50, GroupName = "4. Chop Zone")]
        public bool EnableChopZone
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "EnableExtendedChopZone", Description = "Enable extended chopzone detection", Order = 51, GroupName = "4. Chop Zone")]
        public bool EnableExtendedChopZone
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, double.MaxValue)]
        [Display(Name = "ChopZoneMaxRange", Description = "Max range for chop zone", Order = 52, GroupName = "4. Chop Zone")]
        public double ChopZoneMaxRange
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "ChopZoneMinDir", Description = "Min direction changes for chop zone", Order = 53, GroupName = "4. Chop Zone")]
        public int ChopZoneMinDir
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "ChopZoneTimeFrame", Description = "Time frame for chop zone", Order = 54, GroupName = "4. Chop Zone")]
        public int ChopZoneTimeFrame
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "ChopZoneResetTime", Description = "Time to reset chop zone", Order = 55, GroupName = "4. Chop Zone")]
        public int ChopZoneResetTime
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "ChopZoneLookBack", Description = "Look back period for chop zone", Order = 56, GroupName = "4. Chop Zone")]
        public int ChopZoneLookBack
        { get; set; }
        #endregion

        #region 4. Protective Trades
        [NinjaScriptProperty]
        [Display(Name = "ExitOnATRReversal", Description = "Exit on ATR reversal", Order = 56, GroupName = "4. Protective Trades")]
        public bool ExitOnATRReversal
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "EnableProtectiveLevelTrades", Description = "Enable protective level trades", Order = 57, GroupName = "4. Protective Trades")]
        public bool EnableProtectiveLevelTrades
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, double.MaxValue)]
        [Display(Name = "ProtectiveLevelRangeCheck", Description = "Range to check for protective level trades", Order = 58, GroupName = "4. Protective Trades")]
        public double ProtectiveLevelRangeCheck
        { get; set; }
        #endregion

        #region 4. Dynamic Entry/Exit
        [NinjaScriptProperty]
        [Display(Name = "EnableDynamicEntry", Description = "Enable dynamic entry based on delta volume", Order = 59, GroupName = "4. Dynamic Entry/Exit")]
        public bool EnableDynamicEntry
        { get; set; }

        [NinjaScriptProperty]
        [Range(-100, 100)]
        [Display(Name = "TrendOffset", Description = "Offset from delta trend for dynamic entry", Order = 61, GroupName = "4. Dynamic Entry/Exit")]
        public double DynamicEntryOffsetTrend
        { get; set; }

        [NinjaScriptProperty]
        [Range(-100, 100)]
        [Display(Name = "PosOffset", Description = "Offset from positive delta for dynamic entry", Order = 62, GroupName = "4. Dynamic Entry/Exit")]
        public double DynamicEntryOffsetPos
        { get; set; }

        [NinjaScriptProperty]
        [Range(-100, 100)]
        [Display(Name = "NegOffset", Description = "Offset from negative delta for dynamic entry", Order = 63, GroupName = "4. Dynamic Entry/Exit")]
        public double DynamicEntryOffsetNeg
        { get; set; }
        #endregion

        #region 4. Dynamic Trim
        [NinjaScriptProperty]
        [Display(Name = "EnableDynamicTrim", Description = "Enable dynamic trim based on offset TP level", Order = 64, GroupName = "4. Dynamic Trim")]
        public bool EnableDynamicTrim
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 200)]
        [Display(Name = "ExitTPLevel", Description = "Exit TP level for dynamic trim", Order = 65, GroupName = "4. Dynamic Trim")]
        public double ExitTPLevel
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "TrimPercent", Description = "Percentage of contracts to trim", Order = 66, GroupName = "4. Dynamic Trim")]
        public double TrimPercent
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "ExitLevelRange", Description = "Range to check for exit level", Order = 67, GroupName = "4. Dynamic Trim")]
        public double ExitLevelRange
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "ExitLevelOffset", Description = "Offset from exit level for trim", Order = 68, GroupName = "4. Dynamic Trim")]
        public double ExitLevelOffset
        { get; set; }
        #endregion

        #region 6. Sessions
        [NinjaScriptProperty]
        [Display(Name = "EnableTradingTS1", Description = "Enable trading for time session 1", Order = 8, GroupName = "6. Sessions")]
        public bool EnableTradingTS1
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "EnableTradingTS2", Description = "Enable trading for time session 2", Order = 11, GroupName = "6. Sessions")]
        public bool EnableTradingTS2
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "EnableTradingTS3", Description = "Enable trading for time session 3", Order = 14, GroupName = "6. Sessions")]
        public bool EnableTradingTS3
        { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "TS1Start", Description = "Time session 1 start", Order = 9, GroupName = "6. Sessions")]
        public DateTime TS1Start
        { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "TS1End", Description = "Time session 1 end", Order = 10, GroupName = "6. Sessions")]
        public DateTime TS1End
        { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "TS2Start", Description = "Time session 2 start", Order = 12, GroupName = "6. Sessions")]
        public DateTime TS2Start
        { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "TS2End", Description = "Time session 2 end", Order = 13, GroupName = "6. Sessions")]
        public DateTime TS2End
        { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "TS3Start", Description = "Time session 3 start", Order = 15, GroupName = "6. Sessions")]
        public DateTime TS3Start
        { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "TS3End", Description = "Time session 3 end", Order = 16, GroupName = "6. Sessions")]
        public DateTime TS3End
        { get; set; }
        #endregion

        #region 6. Time Session 1
        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "TPLevelTS1", Description = "Take profit level", Order = 15, GroupName = "6. Time Session 1")]
        public double TPLevelTS1
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "SLLevelTS1", Description = "Stop loss level", Order = 16, GroupName = "6. Time Session 1")]
        public double SLLevelTS1
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "BuySellBufferTS1", Description = "Buffer for buy/sell limit levels", Order = 17, GroupName = "6. Time Session 1")]
        public double BuySellBufferTS1
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "BarsToHoldTradeTS1", Description = "Bars to hold trade", Order = 18, GroupName = "6. Time Session 1")]
        public int BarsToHoldTradeTS1
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "BarsToMissTradeTS1", Description = "Bars to miss trade", Order = 19, GroupName = "6. Time Session 1")]
        public int BarsToMissTradeTS1
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, double.MaxValue)]
        [Display(Name = "OffsetFromEntryToCancelTS1", Description = "Offset from entry to cancel", Order = 20, GroupName = "6. Time Session 1")]
        public double OffsetFromEntryToCancelTS1
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "MaxLossConsecTS1", Description = "Max consecutive losses", Order = 21, GroupName = "6. Time Session 1")]
        public int MaxLossConsecTS1
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ResetBarsMissedOnLongTS1", Description = "Reset bars missed on long", Order = 22, GroupName = "6. Time Session 1")]
        public bool ResetBarsMissedOnLongTS1
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ResetBarsMissedOnShortTS1", Description = "Reset bars missed on short", Order = 23, GroupName = "6. Time Session 1")]
        public bool ResetBarsMissedOnShortTS1
        { get; set; }
        #endregion

        #region 6. Time Session 2
        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "TPLevelTS2", Description = "Take profit level", Order = 24, GroupName = "6. Time Session 2")]
        public double TPLevelTS2
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "SLLevelTS2", Description = "Stop loss level", Order = 25, GroupName = "6. Time Session 2")]
        public double SLLevelTS2
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "BuySellBufferTS2", Description = "Buffer for buy/sell limit levels", Order = 26, GroupName = "6. Time Session 2")]
        public double BuySellBufferTS2
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "BarsToHoldTradeTS2", Description = "Bars to hold trade", Order = 27, GroupName = "6. Time Session 2")]
        public int BarsToHoldTradeTS2
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "BarsToMissTradeTS2", Description = "Bars to miss trade", Order = 28, GroupName = "6. Time Session 2")]
        public int BarsToMissTradeTS2
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, double.MaxValue)]
        [Display(Name = "OffsetFromEntryToCancelTS2", Description = "Offset from entry to cancel", Order = 29, GroupName = "6. Time Session 2")]
        public double OffsetFromEntryToCancelTS2
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "MaxLossConsecTS2", Description = "Max consecutive losses", Order = 30, GroupName = "6. Time Session 2")]
        public int MaxLossConsecTS2
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ResetBarsMissedOnLongTS2", Description = "Reset bars missed on long", Order = 31, GroupName = "6. Time Session 2")]
        public bool ResetBarsMissedOnLongTS2
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ResetBarsMissedOnShortTS2", Description = "Reset bars missed on short", Order = 32, GroupName = "6. Time Session 2")]
        public bool ResetBarsMissedOnShortTS2
        { get; set; }
        #endregion

        #region 6. Time Session 3
        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "TPLevelTS3", Description = "Take profit level", Order = 33, GroupName = "6. Time Session 3")]
        public double TPLevelTS3
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "SLLevelTS3", Description = "Stop loss level", Order = 34, GroupName = "6. Time Session 3")]
        public double SLLevelTS3
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "BuySellBufferTS3", Description = "Buffer for buy/sell limit levels", Order = 35, GroupName = "6. Time Session 3")]
        public double BuySellBufferTS3
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "BarsToHoldTradeTS3", Description = "Bars to hold trade", Order = 36, GroupName = "6. Time Session 3")]
        public int BarsToHoldTradeTS3
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "BarsToMissTradeTS3", Description = "Bars to miss trade", Order = 37, GroupName = "6. Time Session 3")]
        public int BarsToMissTradeTS3
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, double.MaxValue)]
        [Display(Name = "OffsetFromEntryToCancelTS3", Description = "Offset from entry to cancel", Order = 38, GroupName = "6. Time Session 3")]
        public double OffsetFromEntryToCancelTS3
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "MaxLossConsecTS3", Description = "Max consecutive losses", Order = 39, GroupName = "6. Time Session 3")]
        public int MaxLossConsecTS3
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ResetBarsMissedOnLongTS3", Description = "Reset bars missed on long", Order = 40, GroupName = "6. Time Session 3")]
        public bool ResetBarsMissedOnLongTS3
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ResetBarsMissedOnShortTS3", Description = "Reset bars missed on short", Order = 41, GroupName = "6. Time Session 3")]
        public bool ResetBarsMissedOnShortTS3
        { get; set; }
        #endregion

        #region 7. Gain Protection
        [NinjaScriptProperty]
        [Display(Name = "EnableTrailingDrawdown", Description = "Enable trailing drawdown to stop trading", Order = 1, GroupName = "7. Gain Protection")]
        public bool EnableTrailingDrawdown
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, double.MaxValue)]
        [Display(Name = "TrailingDrawdownRatio", Description = "Trailing drawdown to stop trading", Order = 41, GroupName = "7. Gain Protection")]
        public double TrailingDrawdownRatio
        { get; set; }
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "BigWinCutoffCount", Description = "Number of big wins before stopping trading", Order = 42, GroupName = "7. Gain Protection")]
        public int BigWinCutoffCount
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, double.MaxValue)]
        [Display(Name = "BigWinCutoffRatio", Description = "Size of winner to be a big win", Order = 43, GroupName = "7. Gain Protection")]
        public double BigWinCutoffRatio
        { get; set; }
        #endregion

        #region 9. Trade Settings
        [NinjaScriptProperty]
        [Display(Name = "RealTimePnlOnly", Description = "Track PnL only during realtime trading", Order = 1, GroupName = "9. Trade Settings")]
        public bool RealTimePnlOnly
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "EnableBannedDays", Description = "Enable banned days for backtesting", Order = 2, GroupName = "9. Trade Settings")]
        public bool EnableBannedDays
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "DisableTradingTimes", Description = "Disable preset trading times", Order = 2, GroupName = "9. Trade Settings")]
        public bool DisableTradingTimes
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "DisablePNLLimits", Description = "Disable PnL limits for the day", Order = 2, GroupName = "9. Trade Settings")]
        public bool DisablePNLLimits
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ResetConsecOnTime", Description = "Reset consec. losses on time session switch", Order = 3, GroupName = "9. Trade Settings")]
        public bool ResetConsecOnTime
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "EnableNewFeatures", Description = "Enable new features", Order = 4, GroupName = "9. Trade Settings")]
        public bool EnableNewFeatures
        { get; set; }
        #endregion
        #endregion

        #region Variables
        #region Trade Variables
        private Cbi.Order entryOrder = null;
        private double entryPrice = 0.0;
        private int entryBar = -1;

        private Cbi.Order entryOrderShort = null;
        private double entryPriceShort = 0.0;
        private int entryBarShort = -1;

        int consecutiveLosses = 0;
        private double triggerPrice = 0.0;
        #endregion

        #region Trend/Momentum Variables
        private DynamicTrendLine smoothConfirmMA;
        private Indicators.VWAP vwap;
        private CurrentDayOHL currentDayOHL;

        // Momentum Variables
        private EMA momentumMA;
        private EMA momentumMain;
        private EMA momentumSignal;
        private Series<double> momentum;

        // Trend/Chop Variables
        private ChoppinessIndex chopIndex;
        private Series<bool> chopIndexDetect;
        private Series<double> trendDirection;
        private Series<double> deltaMomentum;
        private Series<bool> chopDetect;
        private Series<bool> volatileMove;

        // ATR Variables
        private double upperATRRange = 0.0;
        private double lowerATRRange = 0.0;
        private Series<double> movingRange;
        private Series<double> atrTR;
        private HMA atrHMA;
        private Series<bool> buyATRSignal;
        private Series<bool> sellATRSignal;
        private Series<bool> atrCloseGrtMR;
        private Series<bool> atrCloseLessMR;
        private Series<bool> atrMainGrtSignal;
        private Series<bool> atrMainLessSignal;
        #endregion

        #region Volume Variables
        // Volume Variables
        private Series<double> BuyVol;
        private Series<double> SellVol;
        private WMA smoothBuy;
        private WMA smoothSell;
        private Series<double> smoothNetVol;
        private SMA avgVolume;
        private SMA avgBuyVol;
        private SMA avgSellVol;
        private Series<bool> bullVolPump;
        private Series<bool> bullVolDump;
        private Series<bool> midVolPump;
        private Series<bool> midVolDump;
        private Series<bool> volCrossBuy;
        private Series<bool> volCrossSell;
        private Series<bool> irregVol;
        private int barsMissed = 0;
        #endregion

        #region Trading Variables
        // Trade Variables
        private Series<bool> buyVolTrigger;
        private Series<bool> sellVolTrigger;
        private int volTradeLength;
        private bool buyVolSignal = false;
        private bool sellVolSignal = false;
        bool validTriggerPeriod = false;
        bool reverseSellTrade = false;
        bool reverseBuyTrade = false;
        private int localBarsToMissTrade = 0;
        private int localBarsToMissPrev = 0;
        #endregion

        #region Constants
        // Momentum Constants
        private int dataLength = 8;
        private int atrMALength = 5;
        private int atrSmoothLength = 3;
        private double atrMultiplier = 1.0;
        private int numATR = 4;

        // Volume Constants
        private double volTopLimit = 85;
        private double volUpperLimit = 75;
        private double volPumpGainLimit = 5;
        private double volIrregLimit = 150;
        private double regVolLevel = 60;

        // Trend/Chop Constants
        private int chopCalcLength = 14;
        private double volatileLimit = 5.0;
        private double trendLimit = 2.5;
        private double chopLimit = 1.5;
        private double deltaMomentumChopLimt = 0.5;
        private double deltaMomentumVolLimt = 2.5;
        #endregion

        #region Display Variables
        // Display Symbol Variables
        private bool showVolTrade = true;
        private bool showVolTradeClose = true;
        #endregion

        #region Trading PnL Variables
        // Trading PnL
        private bool EnableTrading = true;
        private double currentPnL;
        private List<DateTime> TradingBanDays;
        public double MaxGain;
        public double MaxLoss;
        public double LossCutOff;
        #endregion

        #region Time Specific Trade Variables
        // Time Specific Trade Variables
        private double tpLevel = 90;
        private double slLevel = 15;
        private double buySellBuffer = 4;
        private int barsToHoldTrade = 5;
        private int barsToMissTrade = 3;
        private double offsetFromEntryToCancel = 50;
        private int maxLossConsec = 3;
        private int lastTimeSession = 0;
        private bool resetBarsMissedOnLong = false;
        private bool resetBarsMissedOnShort = true;
        #endregion

        #region Delta Shading Variables
        // Delta Shading
        public Brush DeltaVolNegShade = Brushes.Gold;
        public Brush DeltaVolBuyShade = Brushes.LimeGreen;
        public Brush DeltaVolSellShade = Brushes.Salmon;
        public Brush DeltaVolTrendShade = Brushes.SkyBlue;
        public Brush ChopShade = Brushes.Silver;
        public int DeltaShadeOpacity = 25;
        public Brush BackBrushLast = null;
        #endregion

        #region ORB Variables
        DateTime ORBStart;
        DateTime ORBEnd;
        #endregion

        #region Chop Zone Variables
        //Chop Zone Variables
        private Series<bool> inChopZone;
        private double upperChopZone;
        private double lowerChopZone;
        private int timeSinceChopZone;
        private bool showChopZone = false;
        private bool reenterChopZoneTop = false;
        private bool reenterChopZoneBot = false;
        #endregion

        #region Levels Variables
        private List<double> CalculatedLevels;
        private List<double> ProtectiveBuyLevels;
        private List<double> ProtectiveSellLevels;
        #endregion
        #endregion

        #region NinjaScript Method Implementations
        // Initialize the strategy
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                #region Basic Settings
                Description =
                    @"This is a strategy using pivot levels to enter long and short trades with confluence from EMA, ATR & Volume";
                Name = "TradingLevelsAlgo";
                Calculate = Calculate.OnBarClose;
                EntryHandling = EntryHandling.AllEntries;
                IncludeCommission = true;
                IsExitOnSessionCloseStrategy = false;
                ExitOnSessionCloseSeconds = 30;
                IsFillLimitOnTouch = false;
                IncludeTradeHistoryInBacktest = true;
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution = OrderFillResolution.Standard;
                Slippage = 0;
                StartBehavior = StartBehavior.WaitUntilFlatSynchronizeAccount;
                TimeInForce = TimeInForce.Gtc;
                TraceOrders = false;
                RealtimeErrorHandling = RealtimeErrorHandling.StopCancelCloseIgnoreRejects;
                StopTargetHandling = StopTargetHandling.ByStrategyPosition;
                BarsRequiredToTrade = 20;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 30;
                // Disable this property for performance gains in Strategy Analyzer optimizations
                // See the Help Guide for additional information
                IsInstantiatedOnEachOptimizationIteration = true;
                #endregion

                #region Properties Defaults
                #region Trading Settings
                RealTimePnlOnly = false;
                DisableTradingTimes = false;
                DisablePNLLimits = false;
                EnableBannedDays = false;
                EnableNewFeatures = false;
                #endregion
                #region Main Parameters
                TradeQuantity = 3;
                MiniContracts = false;
                MaxLossRatio = 150;
                MaxGainRatio = 300;
                LossCutOffRatio = 25;
                ResetConsecOnTime = true;
                EnableTradingTS1 = true;
                EnableTradingTS2 = true;
                EnableTradingTS3 = false;
                TS1Start = DateTime.Parse("10:00", System.Globalization.CultureInfo.InvariantCulture);
                TS1End = DateTime.Parse("16:15", System.Globalization.CultureInfo.InvariantCulture);
                TS2Start = DateTime.Parse("09:00", System.Globalization.CultureInfo.InvariantCulture);
                TS2End = DateTime.Parse("10:00", System.Globalization.CultureInfo.InvariantCulture);
                TS3Start = DateTime.Parse("17:00", System.Globalization.CultureInfo.InvariantCulture);
                TS3End = DateTime.Parse("17:15", System.Globalization.CultureInfo.InvariantCulture);
                #endregion
                #region Time Session 1
                TPLevelTS1 = 50;
                SLLevelTS1 = 19;
                BuySellBufferTS1 = 6;
                BarsToHoldTradeTS1 = 5;
                BarsToMissTradeTS1 = 4;
                OffsetFromEntryToCancelTS1 = 40;
                MaxLossConsecTS1 = 3;
                ResetBarsMissedOnLongTS1 = false;
                ResetBarsMissedOnShortTS1 = false;
                #endregion
                #region Time Session 2
                TPLevelTS2 = 40;
                SLLevelTS2 = 16;
                BuySellBufferTS2 = 5;
                BarsToHoldTradeTS2 = 4;
                BarsToMissTradeTS2 = 3;
                OffsetFromEntryToCancelTS2 = 30;
                MaxLossConsecTS2 = 2;
                ResetBarsMissedOnLongTS2 = false;
                ResetBarsMissedOnShortTS2 = false;
                #endregion
                #region Time Session 3
                TPLevelTS3 = 40;
                SLLevelTS3 = 15;
                BuySellBufferTS3 = 5;
                BarsToHoldTradeTS3 = 4;
                BarsToMissTradeTS3 = 3;
                OffsetFromEntryToCancelTS3 = 30;
                MaxLossConsecTS3 = 2;
                ResetBarsMissedOnLongTS3 = false;
                ResetBarsMissedOnShortTS3 = false;
                #endregion 
                #region Volume Settings
                AveVolPeriod = 15;
                VolSmooth = 8;
                #endregion
                #region Delta Volume Settings
                EnableDynamicSettings = true;
                BarsToMissNegDelta = 2;
                BarsToMissPosDelta = 3;
                DeltaPosCutOff = 2.5;
                DeltaNegCutOff = 1.0;
                #endregion
                #region Dyanmic SL/TP Settings
                EnableDynamicSL = true;
                ProfitToMoveSL = 32;
                SLNewLevel = -2;
                TPCalcFromInitTrigger = false;

                EnableDynamicTP = true;
                MoveLowerOnly = true;
                LevelDetectRange = 10;
                TPOffset = 8;
                #endregion
                #region Chop Zone Settings
                EnableChopZone = true;
                EnableExtendedChopZone = true;
                ChopZoneMaxRange = 30;
                ChopZoneMinDir = 2;
                ChopZoneTimeFrame = 10;
                ChopZoneResetTime = 120;
                ChopZoneLookBack = 3;
                #endregion
                #region Protective Trades
                ExitOnATRReversal = false;
                EnableProtectiveLevelTrades = true;
                ProtectiveLevelRangeCheck = 10;
                #endregion
                #region Dynamic Entry/Exit
                EnableDynamicEntry = true;
                DynamicEntryOffsetTrend = 4;
                DynamicEntryOffsetPos = 0;
                DynamicEntryOffsetNeg = -3;
                #endregion
                #region Gain Protection
                EnableTrailingDrawdown = false;
                BigWinCutoffCount = 5;
                BigWinCutoffRatio = 500;
                TrailingDrawdownRatio = 1000;
                #endregion
                #region Dynamic Trim
                EnableDynamicTrim = false;
                ExitTPLevel = 20;
                TrimPercent = 50;
                ExitLevelRange = 5;
                ExitLevelOffset = 2;
                #endregion
                #endregion

                #region Banned Trading Days
                TradingBanDays = new List<DateTime>
                {
                    DateTime.Parse("2024-05-22", System.Globalization.CultureInfo.InvariantCulture), // Tight range from OPEX, identified in the morning with NVDA on the bell
                    DateTime.Parse("2024-05-20", System.Globalization.CultureInfo.InvariantCulture), // Post opex monday
                    DateTime.Parse("2024-05-13", System.Globalization.CultureInfo.InvariantCulture),
                    DateTime.Parse("2024-05-03", System.Globalization.CultureInfo.InvariantCulture),
                    DateTime.Parse("2024-04-09", System.Globalization.CultureInfo.InvariantCulture),
                    DateTime.Parse("2024-04-10", System.Globalization.CultureInfo.InvariantCulture),
                    DateTime.Parse("2024-04-11", System.Globalization.CultureInfo.InvariantCulture)
                };
                #endregion

                #region Update Dynamic Variables
                EntriesPerDirection = TradeQuantity;
                #endregion
            }
            else if (State == State.DataLoaded)
            {
                ClearOutputWindow();
                Print(Time[0] + " ******** TRADING ALGO v1.8 ******** ");
                #region Initialise all variables
                momentum = new Series<double>(this);
                chopIndexDetect = new Series<bool>(this);
                trendDirection = new Series<double>(this);
                deltaMomentum = new Series<double>(this);
                chopDetect = new Series<bool>(this);
                volatileMove = new Series<bool>(this);
                movingRange = new Series<double>(this);
                atrTR = new Series<double>(this);
                buyATRSignal = new Series<bool>(this);
                sellATRSignal = new Series<bool>(this);
                atrCloseGrtMR = new Series<bool>(this);
                atrCloseLessMR = new Series<bool>(this);
                atrMainGrtSignal = new Series<bool>(this);
                atrMainLessSignal = new Series<bool>(this);
                BuyVol = new Series<double>(this);
                SellVol = new Series<double>(this);
                smoothNetVol = new Series<double>(this);
                bullVolPump = new Series<bool>(this);
                bullVolDump = new Series<bool>(this);
                midVolPump = new Series<bool>(this);
                midVolDump = new Series<bool>(this);
                volCrossBuy = new Series<bool>(this);
                volCrossSell = new Series<bool>(this);
                irregVol = new Series<bool>(this);
                buyVolTrigger = new Series<bool>(this);
                sellVolTrigger = new Series<bool>(this);
                volTradeLength = 0;
                inChopZone = new Series<bool>(this);
                inChopZone[0] = false;
                CalculatedLevels = new List<double>();
                ProtectiveBuyLevels = new List<double>();
                ProtectiveSellLevels = new List<double>();
                ORBStart = DateTime.Parse("09:30", System.Globalization.CultureInfo.InvariantCulture);
                ORBEnd = DateTime.Parse("10:00", System.Globalization.CultureInfo.InvariantCulture);

                // Initialize EMAs
                smoothConfirmMA = DynamicTrendLine(8, 13, 21);
                vwap = VWAP();
                currentDayOHL = CurrentDayOHL();
                atrHMA = HMA(atrTR, numATR);

                // Add our EMAs to the chart for visualization
                AddChartIndicator(smoothConfirmMA);
                AddChartIndicator(vwap);
                #endregion

                #region SL/TP
                SetStopLoss("Long", CalculationMode.Ticks, slLevel / TickSize, false);
                SetProfitTarget("Long", CalculationMode.Ticks, tpLevel / TickSize);
                SetStopLoss("Short", CalculationMode.Ticks, slLevel / TickSize, false);
                SetProfitTarget("Short", CalculationMode.Ticks, tpLevel / TickSize);
                #endregion

                #region Delta Shading
                Brush negShade = DeltaVolNegShade.Clone(); //Copy the brush into a temporary brush
                negShade.Opacity = DeltaShadeOpacity / 100.0; // set the opacity
                negShade.Freeze(); // freeze the temp brush
                DeltaVolNegShade = negShade; // assign the temp brush value to DeltaVolNegShade.

                Brush buyShade = DeltaVolBuyShade.Clone(); //Copy the brush into a temporary brush
                buyShade.Opacity = DeltaShadeOpacity / 100.0; // set the opacity
                buyShade.Freeze(); // freeze the temp brush
                DeltaVolBuyShade = buyShade; // assign the temp brush value to DeltaVolBuyShade.

                Brush sellShade = DeltaVolSellShade.Clone(); //Copy the brush into a temporary brush
                sellShade.Opacity = DeltaShadeOpacity / 100.0; // set the opacity
                sellShade.Freeze(); // freeze the temp brush
                DeltaVolSellShade = sellShade; // assign the temp brush value to DeltaVolSellShade.

                Brush trendShade = DeltaVolTrendShade.Clone(); //Copy the brush into a temporary brush
                trendShade.Opacity = DeltaShadeOpacity / 100.0; // set the opacity
                trendShade.Freeze(); // freeze the temp brush
                DeltaVolTrendShade = trendShade; // assign the temp brush value to DeltaVolTrendShade.

                Brush chopShade = ChopShade.Clone(); //Copy the brush into a temporary brush
                chopShade.Opacity = DeltaShadeOpacity / 100.0; // set the opacity
                chopShade.Freeze(); // freeze the temp brush
                ChopShade = chopShade; // assign the temp brush value to ChopShade.
                #endregion

            }
            else if (State == State.Configure)
            {
                AddDataSeries(BarsPeriodType.Minute, ChopZoneTimeFrame);
                AddDataSeries(BarsPeriodType.Day, 1);
                AddDataSeries(BarsPeriodType.Week, 1);
            }
        }

        // Main Strategy Logic
        protected override void OnBarUpdate()
        {
            // Ensure we have enough data
            if (CurrentBar < BarsRequiredToTrade || BarsInProgress != 0)
                return;

            #region Time Session Functions
            // Reset PnL at the start of the session
            if (Bars.IsFirstBarOfSession)
            {
                currentPnL = 0;
                consecutiveLosses = 0;
                EnableTrading = true;
                Print(Time[0] + " ******** TRADING ENABLED ******** ");
            }

            if (TradingBanDays.Contains(Time[0].Date) && EnableBannedDays)
            {
                EnableTrading = false;
                Print(Time[0] + " ******** TRADING DISABLED ******** : Banned Trading Day");
            }

            // Load variables
            GetTimeSessionVariables();

            #endregion

            #region Trend/Chop/ATR Calculation
            #region Central Chop Zone
            double ChopO = Opens[1][0];
            double ChopC = Closes[1][0];
            double ChopH = Highs[1][0];
            double ChopL = Lows[1][0];
            bool chopZoneFound = false;
            int directionChange = 0;
            double chopRangeTop = ChopH;
            double chopRangeBot = ChopL;
            bool trendUp = ChopC > ChopO;
            if (CurrentBar > 5 * ChopZoneTimeFrame)
                for (int i = 0; i < 5; i++)
                {
                    if (trendUp)
                    {
                        if (Closes[1][i] < Opens[1][i])
                        {
                            directionChange++;
                        }
                    }
                    else
                    {
                        if (Closes[1][i] > Opens[1][i])
                        {
                            directionChange++;
                        }
                    }

                    if (Closes[1][i] < Opens[1][i])
                        trendUp = false;
                    else
                        trendUp = true;

                    if (Highs[1][i] > chopRangeTop)
                        chopRangeTop = Highs[1][i];

                    if (Lows[1][i] < chopRangeBot)
                        chopRangeBot = Lows[1][i];
                }

            // Define chop zone based on calculated values
            if (directionChange >= ChopZoneMinDir && High[0] < chopRangeTop && Low[0] > chopRangeBot && (chopRangeTop - chopRangeBot < ChopZoneMaxRange))
            {
                chopZoneFound = true;
            }

            if (chopZoneFound && EnableChopZone && validTriggerPeriod)
            {
                if (inChopZone[1] == false)
                {
                    inChopZone[0] = true;
                    upperChopZone = chopRangeTop;
                    lowerChopZone = chopRangeBot;
                    timeSinceChopZone = 0;
                    showChopZone = true;

                }
                inChopZone[0] = true;
                reenterChopZoneTop = false;
                reenterChopZoneBot = false;
            }
            else if (!validTriggerPeriod)
            {
                inChopZone[0] = false;
            }

            if (inChopZone[1])
            {
                if (Low[1] > upperChopZone || High[1] < lowerChopZone)
                {
                    inChopZone[0] = false;
                }
                else
                {
                    inChopZone[0] = true;
                }
            }
            else if (EnableChopZone && validTriggerPeriod)
            {
                timeSinceChopZone += BarsPeriod.Value;

                if (Close[0] >= lowerChopZone && Close[0] <= upperChopZone && (High[0] <= upperChopZone || Low[0] >= lowerChopZone) && EnableExtendedChopZone)
                {
                    inChopZone[0] = true;
                    if (Close[1] < lowerChopZone)
                        reenterChopZoneBot = true;
                    if (Close[1] > upperChopZone)
                        reenterChopZoneTop = true;
                }
                else
                {
                    inChopZone[0] = false;
                }

                if (!EnableExtendedChopZone)
                    showChopZone = false;
            }

            if (timeSinceChopZone > 0)
            {
                if (Close[0] < lowerChopZone)
                    reenterChopZoneBot = true;
                if (Close[0] > upperChopZone)
                    reenterChopZoneTop = true;
            }

            if ((timeSinceChopZone > ChopZoneResetTime) || (reenterChopZoneBot && reenterChopZoneTop))
            {
                showChopZone = false;
                reenterChopZoneTop = false;
                reenterChopZoneBot = false;
                upperChopZone = 0;
                lowerChopZone = 0;
            }

            if (showChopZone)
            {
                Draw.Line(this, "ChopZoneTop" + CurrentBar, true, 1, upperChopZone, 0, upperChopZone, Brushes.SteelBlue, DashStyleHelper.Solid, 2);
                Draw.Line(this, "ChopZoneBot" + CurrentBar, true, 1, lowerChopZone, 0, lowerChopZone, Brushes.Orange, DashStyleHelper.Solid, 2);
            }

            if (inChopZone[0])
                Draw.Diamond(this, "ChopZoneIndicator" + CurrentBar, true, 0, Low[0] - TickSize * 80, Brushes.Cyan);

            bool chopZoneTrade = false;
            for (int i = 0; i < ChopZoneLookBack; i++)
            {
                if (inChopZone[i] && !IsORBSession())
                    chopZoneTrade = true;
            }
            #endregion

            #region Momentum/Chop Calculation
            // Generate momentum signals
            momentum[0] = 0;
            for (int i = 0; i < dataLength; i++)
                momentum[0] += (
                    Close[0] > Open[i]
                        ? 1
                        : Close[0] < Open[i]
                            ? -1
                            : 0
                );

            momentumMA = EMA(momentum, atrMALength);
            momentumMain = EMA(momentumMA, atrSmoothLength);
            momentumSignal = EMA(momentumMain, atrSmoothLength);

            // Chop calculation
            chopIndex = ChoppinessIndex(chopCalcLength);
            chopIndexDetect[0] = chopIndex[0] > 61.8;

            // Trend calculation
            trendDirection[0] = momentumMain[0] + (momentumMain[0] - momentumSignal[0]);
            deltaMomentum[0] = Math.Abs((momentumMA[0] - momentumMA[1]) / momentumMA[0]);

            chopDetect[0] =
                (
                    Math.Abs(trendDirection[0]) < chopLimit
                    && deltaMomentum[0] < deltaMomentumChopLimt
                ) || chopIndexDetect[0] || chopZoneTrade;

            volatileMove[0] =
                Math.Abs(trendDirection[0]) > volatileLimit
                || deltaMomentum[0] > deltaMomentumVolLimt;
            #endregion

            #region ATR Dot Calculation
            double hl2 = (High[0] + Low[0]) / 2;
            atrTR[0] = Math.Max(High[0] - Low[0], Math.Max(Math.Abs(High[0] - Close[1]), Math.Abs(Low[0] - Close[1])));
            upperATRRange = hl2 + atrMultiplier * atrHMA[0];
            lowerATRRange = hl2 + -atrMultiplier * atrHMA[0];

            if (Close[0] < movingRange[1])
                movingRange[0] = upperATRRange;
            else
                movingRange[0] = lowerATRRange;

            atrCloseGrtMR[0] = Close[0] > movingRange[0];
            atrCloseLessMR[0] = Close[0] < movingRange[0];
            atrMainGrtSignal[0] = momentumMain[0] > momentumSignal[0];
            atrMainLessSignal[0] = momentumMain[0] < momentumSignal[0];

            buyATRSignal[0] = atrCloseGrtMR[0] && atrCloseGrtMR[1] && atrCloseGrtMR[2] && atrCloseGrtMR[3] && momentumMain[0] > momentumSignal[0] && !atrMainGrtSignal[1];
            sellATRSignal[0] = atrCloseLessMR[0] && atrCloseLessMR[1] && atrCloseLessMR[2] && atrCloseLessMR[3] && momentumMain[0] < momentumSignal[0] && !atrMainLessSignal[1];
            bool closeLongOnATRReversal = false;
            bool closeShortOnATRReversal = false;

            if (buyATRSignal[0])
            {
                if (validTriggerPeriod)
                    Print(Time[0] + " Buy ATR Signal");
                if (ExitOnATRReversal && Position.MarketPosition == MarketPosition.Short && Close[0] <= Position.AveragePrice - 5)
                {
                    Print(Time[0] + " Protective Trades: Close Sell Trade due to ATR Signal");
                    closeShortOnATRReversal = true;
                }
                Draw.TriangleUp(this, "BuyATRSignal" + CurrentBar, true, 0, Low[0] - TickSize * 110, Brushes.Navy);
            }
            else if (sellATRSignal[0])
            {
                if (validTriggerPeriod)
                    Print(Time[0] + " Sell ATR Signal");
                if (ExitOnATRReversal && Position.MarketPosition == MarketPosition.Long && Close[0] >= Position.AveragePrice + 5)
                {
                    Print(Time[0] + " Protective Trades: Close Buy Trade due to ATR Signal");
                    closeLongOnATRReversal = true;
                }
                Draw.TriangleDown(this, "SellATRSignal" + CurrentBar, true, 0, High[0] + TickSize * 110, Brushes.Gold);
            }
            #endregion
            #endregion

            #region Volume Analysis
            #region Volume Calculation
            double gap = Open[0] - Close[1];

            double bull_gap = Math.Max(gap, 0);
            double bear_gap = Math.Abs(Math.Min(gap, 0));

            double body = Math.Abs(Close[0] - Open[0]);
            double BarRange = High[0] - Low[0];
            double wick = BarRange - body;

            bool up_bar = Close[0] > Open[0];

            double bull = wick + (up_bar ? body : 0) + bull_gap;
            double bear = wick + (up_bar ? 0 : body) + bear_gap;
            double VolRange = bull + bear;
            double BScore = VolRange > 0 ? bull / VolRange : 0.5;
            BuyVol[0] = BScore * Volume[0];
            SellVol[0] = Volume[0] - BuyVol[0];
            double buy_percent = (BuyVol[0] / Volume[0]) * 100;
            double sell_percent = (SellVol[0] / Volume[0]) * 100;

            smoothBuy = WMA(WMA(BuyVol, AveVolPeriod), VolSmooth);
            smoothSell = WMA(WMA(SellVol, AveVolPeriod), VolSmooth);
            smoothNetVol[0] = smoothBuy[0] - smoothSell[0];

            double netVolH = Math.Max(smoothBuy[0], smoothSell[0]);
            double netVolL = Math.Min(smoothBuy[0], smoothSell[0]);
            bool risingVol = (smoothNetVol[0] - smoothNetVol[1]) > 0;

            double volPumpLevel = volPumpGainLimit / 100;
            double irregVolLevel = volIrregLimit / 100;

            avgVolume = SMA(Volume, AveVolPeriod);
            avgBuyVol = SMA(smoothBuy, AveVolPeriod);
            avgSellVol = SMA(smoothSell, AveVolPeriod);

            bullVolPump[0] =
                risingVol
                && Math.Abs(smoothBuy[0] - smoothBuy[1]) / avgBuyVol[0] > volPumpLevel
                && smoothBuy[0] > smoothSell[0];

            bullVolDump[0] =
                !risingVol
                && Math.Abs(smoothSell[0] - smoothSell[1]) / avgSellVol[0] > volPumpLevel
                && smoothBuy[0] < smoothSell[0];

            midVolPump[0] = smoothBuy[0] > smoothSell[0] && risingVol;
            midVolDump[0] = smoothBuy[0] < smoothSell[0] && !risingVol;
            volCrossBuy[0] = CrossAbove(smoothBuy, smoothSell, 1);
            volCrossSell[0] = CrossAbove(smoothSell, smoothBuy, 1);
            irregVol[0] = Volume[0] / avgVolume[0] > irregVolLevel;

            double deltaBuyVol = (smoothBuy[0] - smoothBuy[1]) / smoothBuy[1];
            double deltaSellVol = (smoothSell[0] - smoothSell[1]) / smoothSell[1];
            #endregion

            #region Volume Chart Display
            // Volume Momentum Buy/Sell
            double symbolOffset = 50;
            Brush volColor = Brushes.White;
            if (bullVolPump[0])
            {
                volColor = Brushes.Lime;
            }
            else if (bullVolDump[0])
            {
                volColor = Brushes.Red;
            }
            else if (midVolPump[0])
            {
                volColor = Brushes.MediumSeaGreen;
            }
            else if (midVolDump[0])
            {
                volColor = Brushes.Tomato;
            }
            else if (buy_percent > volUpperLimit)
            {
                volColor = Brushes.DarkGreen;
            }
            else if (sell_percent > volUpperLimit)
            {
                volColor = Brushes.DarkRed;
            }

            if (volCrossBuy[0])
            {
                Draw.TriangleUp(
                    this,
                    "volCrossBuy" + CurrentBar,
                    true,
                    0,
                    Low[0] - TickSize * symbolOffset,
                    volColor
                );
            }
            else if (volCrossSell[0])
            {
                Draw.TriangleDown(
                    this,
                    "volCrossSell" + CurrentBar,
                    true,
                    0,
                    High[0] + TickSize * symbolOffset,
                    volColor
                );
            }
            else if (bullVolPump[0])
            {
                Draw.Square(
                    this,
                    "bullVolPump" + CurrentBar,
                    true,
                    0,
                    Low[0] - TickSize * symbolOffset,
                    volColor
                );
            }
            else if (bullVolDump[0])
            {
                Draw.Square(
                    this,
                    "bullVolDump" + CurrentBar,
                    true,
                    0,
                    High[0] + TickSize * symbolOffset,
                    volColor
                );
            }
            else if (midVolPump[0])
            {
                Draw.Square(
                    this,
                    "midVolPump" + CurrentBar,
                    true,
                    0,
                    Low[0] - TickSize * symbolOffset,
                    volColor
                );
            }
            else if (midVolDump[0])
            {
                Draw.Square(
                    this,
                    "midVolDump" + CurrentBar,
                    true,
                    0,
                    High[0] + TickSize * symbolOffset,
                    volColor
                );
            }

            if (irregVol[0])
            {
                double symbolLocation;
                if (buy_percent > 50)
                {
                    symbolLocation = Low[0] - TickSize * (symbolOffset + 15);
                }
                else
                {
                    symbolLocation = High[0] + TickSize * (symbolOffset + 15);
                }

                Draw.Diamond(this, "irregVol" + CurrentBar, true, 0, symbolLocation, volColor);
            }
            #endregion
            #endregion

            #region Level Management
            if (CurrentBar > 10 * 16 * 60 / BarsPeriod.Value)
            {
                #region Level Calculation
                // Daily/Weekly Levels
                double yesterdayClose = Closes[2][0];
                double yesterdayHigh = Highs[2][0];
                double yesterdayLow = Lows[2][0];
                double lastWeekClose = Closes[3][0];
                double lastWeekHigh = Highs[3][0];
                double lastWeekLow = Lows[3][0];
                double todayHigh = currentDayOHL.CurrentHigh[0];
                double todayLow = currentDayOHL.CurrentLow[0];

                // ATR Calculation
                int atr_length = 14;
                double trigger_percentage = 0.236;
                double atr = ATR(BarsArray[2], atr_length)[1];
                double range_1 = todayHigh - todayLow;
                double tr_percent_of_atr = range_1 / atr * 100;
                double atrBear = yesterdayClose - trigger_percentage * atr;
                double atrBull = yesterdayClose + trigger_percentage * atr;
                double atrNeg618 = yesterdayClose - atr * 0.618;
                double atr618 = yesterdayClose + atr * 0.618;
                double atrNeg100 = yesterdayClose - atr;
                double atr100 = yesterdayClose + atr;

                //Calculate camarilla pivots
                double r = yesterdayHigh - yesterdayLow;
                double R6 = yesterdayHigh / yesterdayLow * yesterdayClose; //Bull target 2
                double R4 = yesterdayClose + r * (1.1 / 2); //Bear Last Stand
                double R3 = yesterdayClose + r * (1.1 / 4); //Bear Reversal Low
                double S4 = yesterdayClose - r * (1.1 / 2); //Bull Last Stand
                double S6 = yesterdayClose - (R6 - yesterdayClose); //Bear Target 2
                double S3 = yesterdayClose - r * (1.1 / 4); //Bull Reversal High

                // TODO: Add ORB
                // TODO: Add Day High/Low
                #endregion

                #region Calculated Levels Array
                CalculatedLevels.Clear();
                CalculatedLevels.Add(vwap[0]);
                AddLevel(lastWeekHigh, "Previous Week High");
                AddLevel(lastWeekLow, "Previous Week Low");
                AddLevel(lastWeekClose, "Previous Week Close");

                AddLevel(yesterdayHigh, "Previous High");
                AddLevel(yesterdayLow, "Previous Low");
                AddLevel(yesterdayClose, "Previous Close");

                AddLevel(atrBear, "ATR Bearish");
                AddLevel(atrBull, "ATR Bullish");
                AddLevel(atrNeg618, "ATR -0.618");
                AddLevel(atr618, "ATR +0.618");
                AddLevel(atrNeg100, "ATR -1.0");
                AddLevel(atr100, "ATR +1.0");

                AddLevel(R6, "Bull Target");
                AddLevel(R4, "Bear Reversal");
                AddLevel(R3, "Ex. Range High");
                AddLevel(S4, "Bull Reversal");
                AddLevel(S6, "Bear Target");
                AddLevel(S3, "Ex. Range Low");
                #endregion

                #region Protective Levels Array
                ProtectiveBuyLevels.Clear();
                ProtectiveBuyLevels.Add(vwap[0]);
                ProtectiveBuyLevels.Add(lastWeekHigh);
                ProtectiveBuyLevels.Add(yesterdayHigh);
                ProtectiveBuyLevels.Add(atr618);
                ProtectiveBuyLevels.Add(atr100);
                ProtectiveBuyLevels.Add(R6); // Bull Target
                ProtectiveBuyLevels.Add(R4); // Bear Reversal


                ProtectiveSellLevels.Clear();
                ProtectiveSellLevels.Add(vwap[0]);
                ProtectiveSellLevels.Add(lastWeekLow);
                ProtectiveSellLevels.Add(yesterdayLow);
                ProtectiveSellLevels.Add(atrNeg618);
                ProtectiveSellLevels.Add(atrNeg100);
                ProtectiveSellLevels.Add(S6); // Bear Target
                ProtectiveSellLevels.Add(S4); // Bull Reversal

                #endregion
            }
            #endregion

            #region PnL Calculation
            #region Dynamic Gain/Loss
            MaxGain = MaxGainRatio * TradeQuantity;
            MaxLoss = MaxLossRatio * TradeQuantity * -1;
            LossCutOff = LossCutOffRatio * TradeQuantity * -1;
            if (MiniContracts)
            {
                MaxGain = MaxGain * 10;
                MaxLoss = MaxLoss * 10;
                LossCutOff = LossCutOff * 10;
            }
            #endregion

            #region Trading Cutoff
            if (
                (currentPnL < MaxLoss || currentPnL > MaxGain)
                && EnableTrading
                && !DisablePNLLimits
            )
            {
                EnableTrading = false;
                Print(Time[0] + " ******** TRADING DISABLED ******** : $" + currentPnL);
            }

            double realtimPnL = Math.Round(currentPnL + Position.GetUnrealizedProfitLoss(PerformanceUnit.Currency, Close[0]), 1);
            // if in a position and the realized day's PnL plus the position PnL is greater than the loss limit then exit the order
            if ((((realtimPnL) <= MaxLoss) || (realtimPnL) >= MaxGain)
                && EnableTrading
                && !DisablePNLLimits
            )
            {
                EnableTrading = false;
                Print(Time[0] + " ******** TRADING DISABLED (mid-trade) ******** : $" + realtimPnL);
            }
            #endregion
            #endregion

            #region Trading Logic
            #region Buy/Sell Triggers
            if (!chopDetect[0])
            {
                buyVolTrigger[0] = (midVolPump[0] || bullVolPump[0]) && !sellVolTrigger[1];
                sellVolTrigger[0] = (midVolDump[0] || bullVolDump[0]) && !buyVolTrigger[1];
            }
            else
            {
                buyVolTrigger[0] = false;
                sellVolTrigger[0] = false;
            }

            bool buyTrigger = buyVolTrigger[0] && !buyVolTrigger[1] && validTriggerPeriod;
            bool sellTrigger = sellVolTrigger[0] && !sellVolTrigger[1] && validTriggerPeriod;
            bool buyVolCloseTrigger = false;
            bool sellVolCloseTrigger = false;

            if (validTriggerPeriod && !EnableTrading)
            {
                buyVolCloseTrigger = true;
                sellVolCloseTrigger = true;
            }

            validTriggerPeriod = EnableTrading;
            #endregion

            #region Delta Shading

            if (!buyVolSignal && !sellVolSignal)
            {
                BackBrush = null;
            }
            else
            {
                BackBrush = BackBrushLast;
            }

            // Load in new variables if delta volume is weak
            if (EnableDynamicSettings)
            {
                if (midVolPump[0])
                {
                    if (deltaBuyVol >= DeltaPosCutOff / 100)
                    {
                        if (deltaSellVol >= -1 * DeltaNegCutOff / 100)
                        {
                            localBarsToMissTrade = barsToMissTrade;
                            if (buyVolSignal)
                                BackBrush = DeltaVolTrendShade;
                        }
                        else
                        {
                            localBarsToMissTrade = BarsToMissPosDelta;
                            if (buyVolSignal)
                                BackBrush = DeltaVolBuyShade;
                        }
                    }
                    else
                    {
                        localBarsToMissTrade = BarsToMissNegDelta;
                        if (buyVolSignal)
                            BackBrush = DeltaVolNegShade;
                    }
                }
                else if (midVolDump[0])
                {
                    if (deltaSellVol >= DeltaPosCutOff / 100)
                    {
                        if (deltaBuyVol >= -1 * DeltaNegCutOff / 100)
                        {
                            localBarsToMissTrade = barsToMissTrade;
                            if (sellVolSignal)
                                BackBrush = DeltaVolTrendShade;
                        }
                        else
                        {
                            localBarsToMissTrade = BarsToMissPosDelta;
                            if (sellVolSignal)
                                BackBrush = DeltaVolSellShade;
                        }
                    }
                    else
                    {
                        localBarsToMissTrade = BarsToMissNegDelta;
                        if (sellVolSignal)
                            BackBrush = DeltaVolNegShade;
                    }
                }
            }
            else
            {
                localBarsToMissTrade = barsToMissTrade;
            }

            BackBrushLast = BackBrush;

            if (BackBrush == null && chopDetect[0])
                BackBrush = ChopShade;

            if (localBarsToMissTrade != localBarsToMissPrev && validTriggerPeriod)
            {
                Print(Time[0] + " Bars to Miss Trade Changed from " + localBarsToMissPrev + " to " + localBarsToMissTrade + ". Delta Buy: " + Math.Round(deltaBuyVol, 3) * 100 + "% Delta Sell: " + Math.Round(deltaSellVol, 3) * 100 + "%");
            }
            localBarsToMissPrev = localBarsToMissTrade;
            #endregion

            #region Buy/Sell Signals
            if (buyTrigger || buyVolSignal)
            {
                buyVolSignal = true;
                if (!EnableTrading || midVolDump[0] || bullVolDump[0] || closeLongOnATRReversal)
                {
                    buyVolSignal = false;
                    buyVolCloseTrigger = true;
                    volTradeLength = 0;
                    barsMissed = 0;
                }
                else if (!(midVolPump[0] || bullVolPump[0]))
                {
                    if (barsMissed < localBarsToMissTrade)
                    {
                        barsMissed += 1;
                        Print(
                            Time[0]
                                + " Long Trade - Bars Missed: "
                                + barsMissed
                                + " of "
                                + localBarsToMissTrade
                        );
                    }
                    else
                    {
                        buyVolSignal = false;
                        buyVolCloseTrigger = true;
                        volTradeLength = 0;
                        barsMissed = 0;
                    }
                }
                else
                {
                    volTradeLength += 1;
                    if (resetBarsMissedOnLong)
                    {
                        Print(Time[0] + " Bars Missed Reset on Long");
                        barsMissed = 0;
                    }
                }
            }

            if (sellTrigger || sellVolSignal)
            {
                sellVolSignal = true;
                if (!EnableTrading || midVolPump[0] || bullVolPump[0] || closeShortOnATRReversal)
                {
                    sellVolSignal = false;
                    sellVolCloseTrigger = true;
                    volTradeLength = 0;
                    barsMissed = 0;
                }
                else if (!(midVolDump[0] || bullVolDump[0]))
                {
                    if (barsMissed < localBarsToMissTrade)
                    {
                        barsMissed += 1;
                        Print(
                            Time[0]
                                + " Short Trade - Bars Missed: "
                                + barsMissed
                                + " of "
                                + localBarsToMissTrade
                        );
                    }
                    else
                    {
                        sellVolSignal = false;
                        sellVolCloseTrigger = true;
                        volTradeLength = 0;
                        barsMissed = 0;
                    }
                }
                else
                {
                    volTradeLength += 1;
                    if (resetBarsMissedOnShort)
                    {
                        Print(Time[0] + " Bars Missed Reset on Short");
                        barsMissed = 0;
                    }
                }
            }

            if (buyTrigger && showVolTrade)
            {
                Draw.ArrowUp(
                    this,
                    "buyTrigger" + CurrentBar,
                    true,
                    0,
                    Low[0] - TickSize * (symbolOffset + 35),
                    Brushes.Green
                );
            }

            if (sellTrigger && showVolTrade)
            {
                Draw.ArrowDown(
                    this,
                    "sellTrigger" + CurrentBar,
                    true,
                    0,
                    High[0] + TickSize * (symbolOffset + 35),
                    Brushes.Red
                );
            }

            if ((buyVolCloseTrigger || sellVolCloseTrigger) && showVolTradeClose)
            {
                if (buyVolCloseTrigger)
                {
                    Draw.ArrowDown(
                        this,
                        "buyCloseTrigger" + CurrentBar,
                        true,
                        0,
                        High[0] + TickSize * (symbolOffset + 35),
                        Brushes.Gold
                    );
                }
                else if (sellVolCloseTrigger)
                {
                    Draw.ArrowUp(
                        this,
                        "sellCloseTrigger" + CurrentBar,
                        true,
                        0,
                        Low[0] - TickSize * (symbolOffset + 35),
                        Brushes.Gold
                    );
                }
            }
            #endregion
            #endregion

            #region Trade Management
            #region TP/SL management
            // Resets the stop loss to the original value when all positions are closed
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                SetStopLoss("Long", CalculationMode.Ticks, slLevel / TickSize, false);
                SetStopLoss("Short", CalculationMode.Ticks, slLevel / TickSize, false);
                SetProfitTarget("Long", CalculationMode.Ticks, tpLevel / TickSize);
                SetProfitTarget("Short", CalculationMode.Ticks, tpLevel / TickSize);
            }
            else if (Position.MarketPosition == MarketPosition.Long)
            {
                if (EnableDynamicSL)
                    if (High[0] > Position.AveragePrice + ProfitToMoveSL)
                    {
                        Print(Time[0] + " Dynamic SL: SL Level Updated to: " + (Position.AveragePrice + SLNewLevel * TickSize));
                        SetStopLoss("Long", CalculationMode.Price, Position.AveragePrice - SLNewLevel * TickSize, false);
                    }
                if (EnableDynamicTP)
                {
                    double originalTP = Position.AveragePrice + tpLevel;
                    if (TPCalcFromInitTrigger)
                    {
                        Print(Time[0] + " Dynamic Exit: TP Level Updated from: " + originalTP + " to: " + (triggerPrice + tpLevel));
                        originalTP = triggerPrice + tpLevel;
                    }
                    double TPNewLevel = UpdateTPLevel(originalTP, true);
                    SetProfitTarget("Long", CalculationMode.Price, TPNewLevel);
                }
            }
            else if (Position.MarketPosition == MarketPosition.Short)
            {
                if (EnableDynamicSL)
                    if (Low[0] < Position.AveragePrice - ProfitToMoveSL)
                    {
                        Print(Time[0] + " Dynamic SL: SL Level Updated to: " + (Position.AveragePrice + SLNewLevel * TickSize));
                        SetStopLoss("Short", CalculationMode.Price, Position.AveragePrice + SLNewLevel * TickSize, false);
                    }

                if (EnableDynamicTP)
                {
                    double originalTP = Position.AveragePrice - tpLevel;
                    if (TPCalcFromInitTrigger)
                    {
                        Print(Time[0] + " Dynamic Exit: TP Level Updated from: " + originalTP + " to: " + (triggerPrice - tpLevel));
                        originalTP = triggerPrice - tpLevel;
                    }
                    double TPNewLevel = UpdateTPLevel(originalTP, false);
                    SetProfitTarget("Short", CalculationMode.Price, TPNewLevel);
                }
            }
            #endregion

            #region Close Trades that are too far away from entry
            if (entryOrder != null)
            {
                // Manage open orders here, e.g., check if it's time to exit based on bar count
                bool barTooFarFromEntry = High[0] > entryPrice + offsetFromEntryToCancel;
                if (
                    (
                        (CurrentBar >= entryBar + barsToHoldTrade)
                        || buyVolCloseTrigger
                        || barTooFarFromEntry
                    )
                    && entryOrder.OrderState == OrderState.Working
                )
                {
                    Print(
                        Time[0]
                            + " Long Order cancelled: "
                            + Close[0]
                            + (barTooFarFromEntry ? " - Bar too far from entry" : "")
                    );
                    CancelOrder(entryOrder);
                    entryOrder = null; // Reset the entry order variable
                }
            }

            if (entryOrderShort != null)
            {
                // Manage open orders here, e.g., check if it's time to exit based on bar count
                bool barTooFarFromEntry = Low[0] < entryPriceShort - offsetFromEntryToCancel;
                if (
                    (
                        (CurrentBar >= entryBarShort + barsToHoldTrade)
                        || sellVolCloseTrigger
                        || barTooFarFromEntry
                    )
                    && entryOrderShort.OrderState == OrderState.Working
                )
                {
                    Print(
                        Time[0]
                            + " Short Order cancelled: "
                            + Close[0]
                            + (barTooFarFromEntry ? " - Bar too far from entry" : "")
                    );
                    CancelOrder(entryOrderShort);
                    entryOrderShort = null; // Reset the entry order variable
                }
            }
            #endregion

            #region Close trades on close signal
            if (buyVolCloseTrigger)
            {
                if (entryOrder != null && entryOrder.OrderState == OrderState.Filled)
                {
                    Print(Time[0] + " Long Order Closed: " + Close[0]);
                }
                if (Position.MarketPosition == MarketPosition.Long)
                {
                    ExitLong("Long");
                }
            }
            else if (sellVolCloseTrigger)
            {
                if (entryOrderShort != null && entryOrderShort.OrderState == OrderState.Filled)
                {
                    Print(Time[0] + " Short Order Closed: " + Close[0]);
                }
                if (Position.MarketPosition == MarketPosition.Short)
                {
                    ExitShort("Short");
                }
            }
            else if (!EnableTrading)
            {
                if (Position.MarketPosition == MarketPosition.Long)
                {
                    ExitLong();
                }
                else if (Position.MarketPosition == MarketPosition.Short)
                {
                    ExitShort();
                }
            }
            else if (Position.MarketPosition == MarketPosition.Long)
            {
                if ((Close[0] > triggerPrice + tpLevel) && TPCalcFromInitTrigger)
                {
                    ExitLong("Long");
                }
            }
            else if (Position.MarketPosition == MarketPosition.Short)
            {
                if ((Close[0] < triggerPrice - tpLevel) && TPCalcFromInitTrigger)
                {
                    ExitShort("Short");
                }
            }
            #endregion

            #region Buy/Sell Orders
            if (!buyVolCloseTrigger && !sellVolCloseTrigger)
            {
                if ((buyTrigger || reverseBuyTrade) && entryOrder == null)
                {
                    double dynamicOffset = GetDynamicEntryOffset(true, deltaBuyVol, deltaSellVol);
                    double limitLevel = GetLimitLevel(
                        smoothConfirmMA[0] + buySellBuffer + dynamicOffset,
                        Close[0],
                        true
                    );

                    if ((EnableProtectiveLevelTrades && IsEntrySafe(smoothConfirmMA[0] + buySellBuffer, true)) || !EnableProtectiveLevelTrades)
                    {
                        Print(Time[0] + " Long triggered: " + limitLevel);
                        entryOrder = EnterLongLimit(0, true, TradeQuantity, limitLevel, "Long");
                        entryBar = CurrentBar; // Remember the bar at which we entered
                        entryPrice = limitLevel; // Assuming immediate execution at the close price
                        triggerPrice = limitLevel;
                        Draw.Line(
                            this,
                            "entryLine" + CurrentBar,
                            true,
                            1,
                            limitLevel,
                            -1,
                            limitLevel,
                            Brushes.Green,
                            DashStyleHelper.Solid,
                            2
                        );
                    }
                }
                else if (
                    buyVolSignal
                    && entryOrder != null
                    && entryOrder.OrderState != OrderState.Filled
                )
                {
                    double dynamicOffset = GetDynamicEntryOffset(true, deltaBuyVol, deltaSellVol);
                    double limitLevel = GetLimitLevel(
                        smoothConfirmMA[0] + buySellBuffer + dynamicOffset,
                        Close[0],
                        true
                    );

                    if ((EnableProtectiveLevelTrades && IsEntrySafe(smoothConfirmMA[0] + buySellBuffer, true)) || !EnableProtectiveLevelTrades)
                    {
                        ChangeOrder(entryOrder, entryOrder.Quantity, limitLevel, 0);
                        entryPrice = limitLevel; // Assuming immediate execution at the close price
                        Draw.Line(
                            this,
                            "entryLine" + CurrentBar,
                            true,
                            1,
                            limitLevel,
                            -1,
                            limitLevel,
                            Brushes.Green,
                            DashStyleHelper.Solid,
                            2
                        );
                        Print(
                            Time[0]
                                + " Long updated: "
                                + limitLevel
                                + " Bars Held: "
                                + (CurrentBar - entryBar).ToString()
                        );
                    }
                    else
                    {
                        Print(Time[0] + " Long Order Cancelled due to protective trades at: " + Close[0]);
                        CancelOrder(entryOrder);
                        entryOrder = null; // Reset the entry order variable
                    }
                }
            }

            if (!sellVolCloseTrigger && !buyVolCloseTrigger)
            {
                if ((sellTrigger || reverseSellTrade) && entryOrderShort == null)
                {
                    double dynamicOffset = GetDynamicEntryOffset(false, deltaBuyVol, deltaSellVol);
                    double limitLevel = GetLimitLevel(
                        smoothConfirmMA[0] - buySellBuffer - dynamicOffset,
                        Close[0],
                        false
                    );

                    if ((EnableProtectiveLevelTrades && IsEntrySafe(smoothConfirmMA[0] - buySellBuffer, false)) || !EnableProtectiveLevelTrades)
                    {
                        Print(Time[0] + " Short triggered: " + limitLevel);
                        entryOrderShort = EnterShortLimit(0, true, TradeQuantity, limitLevel, "Short");
                        entryBarShort = CurrentBar; // Remember the bar at which we entered
                        entryPriceShort = limitLevel; // Assuming immediate execution at the close price
                        triggerPrice = limitLevel;
                        Draw.Line(
                            this,
                            "entryLineShort" + CurrentBar,
                            true,
                            1,
                            limitLevel,
                            -1,
                            limitLevel,
                            Brushes.Red,
                            DashStyleHelper.Solid,
                            2
                        );
                    }
                }
                else if (
                    sellVolSignal
                    && entryOrderShort != null
                    && entryOrderShort.OrderState != OrderState.Filled
                )
                {
                    double dynamicOffset = GetDynamicEntryOffset(false, deltaBuyVol, deltaSellVol);
                    double limitLevel = GetLimitLevel(
                        smoothConfirmMA[0] - buySellBuffer - dynamicOffset,
                        Close[0],
                        false
                    );

                    if ((EnableProtectiveLevelTrades && IsEntrySafe(smoothConfirmMA[0] - buySellBuffer, false)) || !EnableProtectiveLevelTrades)
                    {
                        ChangeOrder(entryOrderShort, entryOrderShort.Quantity, limitLevel, 0);
                        entryPriceShort = limitLevel; // Assuming immediate execution at the close price
                        Draw.Line(
                            this,
                            "entryLineShort" + CurrentBar,
                            true,
                            1,
                            limitLevel,
                            -1,
                            limitLevel,
                            Brushes.Red,
                            DashStyleHelper.Solid,
                            2
                        );
                        Print(
                            Time[0]
                                + " Short updated: "
                                + limitLevel
                                + " Bars Held: "
                                + (CurrentBar - entryBarShort).ToString()
                        );
                    }
                    else
                    {
                        Print(Time[0] + " Short Order Cancelled due to protective trades at: " + Close[0]);
                        CancelOrder(entryOrderShort);
                        entryOrderShort = null; // Reset the entry order variable
                    }
                }
            }

            reverseBuyTrade = sellVolCloseTrigger && buyTrigger;
            reverseSellTrade = buyVolCloseTrigger && sellTrigger;
            #endregion
            #endregion

            #region Dashboard
            string dashBoard =
                $"PnL ({(RealTimePnlOnly ? "RT" : "ALL")}): $"
                + realtimPnL.ToString()
                + " | Trading: "
                + (EnableTrading || DisablePNLLimits ? "Active" : "Off")
                + "\nConsec: " + consecutiveLosses + " of " + maxLossConsec;

            if (Position.MarketPosition != MarketPosition.Flat)
            {
                dashBoard += " | Bars Missed: " + barsMissed + " of " + localBarsToMissTrade;
            }
            else
            {
                if (entryOrder != null)
                {
                    string barHeld = "0";
                    if (entryOrder.OrderState == OrderState.Working)
                        barHeld = (CurrentBar - entryBar).ToString();
                    dashBoard += " | Bars Held: " + barHeld + " of " + barsToHoldTrade;
                }
                else if (entryOrderShort != null)
                {
                    string barHeld = "0";
                    if (entryOrderShort.OrderState == OrderState.Working)
                        barHeld = (CurrentBar - entryBarShort).ToString();
                    dashBoard += " | Bars Held: " + barHeld + " of " + barsToHoldTrade;
                }
            }

            dashBoard += "\nDelta Buy: " + Math.Round(deltaBuyVol, 3) * 100 + "% Delta Sell: " + Math.Round(deltaSellVol, 3) * 100 + "%";

            string tradeStatus = "";

            if (buyVolSignal)
                tradeStatus += ((tradeStatus != "" ? " | " : "") + "Long Signal");

            if (sellVolSignal)
                tradeStatus += ((tradeStatus != "" ? " | " : "") + "Short Signal");

            if (Math.Abs(trendDirection[0]) < chopLimit && deltaMomentum[0] < deltaMomentumChopLimt)
                tradeStatus += ((tradeStatus != "" ? " | " : "") + "Chop (TM)");
            if (chopIndexDetect[0])
                tradeStatus += ((tradeStatus != "" ? " | " : "") + $"Chop (CI)");

            if (chopZoneTrade)
            {
                tradeStatus += ((tradeStatus != "" ? " | " : "") + "Chop (CZ)");
                if (timeSinceChopZone > 0)
                {
                    tradeStatus += $" Time: {timeSinceChopZone}s";
                    if (!reenterChopZoneTop)
                        tradeStatus += $" // Top RE";
                    if (!reenterChopZoneBot)
                        tradeStatus += $" // Bot RE";

                }
            }

            if (tradeStatus != "")
                dashBoard += "\nTriggers: " + tradeStatus;

            Draw.TextFixed(this, "Dashboard", dashBoard, TextPosition.BottomRight);
            #endregion
        }

        // Order Update
        protected override void OnOrderUpdate(Cbi.Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string comment)
        {
            // One time only, as we transition from historical
            // Convert any old historical order object references to the live order submitted to the real-time account
            if (entryOrder != null && entryOrder.IsBacktestOrder && State == State.Realtime)
                entryOrder = GetRealtimeOrder(entryOrder);

            if (
                entryOrderShort != null
                && entryOrderShort.IsBacktestOrder
                && State == State.Realtime
            )
                entryOrderShort = GetRealtimeOrder(entryOrderShort);

            if (order.Name == "Long")
            {
                if (orderState == OrderState.Filled)
                {
                    Print(
                        Time[0]
                            + " LONG FILLED: "
                            + averageFillPrice
                            + " Vol Trade Length: "
                            + volTradeLength
                    );
                }
            }

            if (order.Name == "Short")
            {
                if (orderState == OrderState.Filled)
                {
                    Print(
                        Time[0]
                            + " SHORT FILLED: "
                            + averageFillPrice
                            + " Vol Trade Length: "
                            + volTradeLength
                    );
                }
            }

            if (orderState == OrderState.Rejected || orderState == OrderState.Cancelled)
            {
                entryOrder = null;
                entryOrderShort = null;
                if (order.Name == "Stop loss" && order.OrderState == OrderState.Rejected)
                {
                    ExitLong();
                    ExitShort();
                }
            }
        }

        // Execution Update
        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            if (
                execution.Order.OrderState == OrderState.Filled
                && (
                    execution.Order.Name.Contains("Stop loss")
                    || execution.Order.Name.Contains("Profit target")
                    || execution.Order.Name.Contains("to cover")
                )
            )
            {
                Print(
                    time.ToString()
                        + " TRADE CLOSED: "
                        + execution.Order.Name
                        + " at Price: "
                        + price
                );
            }

            if (
                Position.MarketPosition == MarketPosition.Flat
                && SystemPerformance.AllTrades.Count > 0
            )
            {
                if (RealTimePnlOnly && State == State.Realtime || !RealTimePnlOnly)
                {
                    Cbi.Trade lastTrade = SystemPerformance.AllTrades[
                        SystemPerformance.AllTrades.Count - 1
                    ];

                    // Sum the profits of trades with similar exit times
                    double totalTradePnL = lastTrade.ProfitCurrency;
                    DateTime exitTime = lastTrade.Exit.Time;
                    for (int i = SystemPerformance.AllTrades.Count - 2; i >= 0; i--)
                    {
                        Cbi.Trade trade = SystemPerformance.AllTrades[i];
                        if (Math.Abs((trade.Exit.Time - exitTime).TotalSeconds) <= 10)
                        {
                            totalTradePnL += trade.ProfitCurrency;
                        }
                        else
                        {
                            break; // Exit the loop if the exit time is different
                        }
                    }


                    currentPnL += totalTradePnL;

                    Print(Time[0] + " Trade PnL: $" + totalTradePnL);
                    Print(Time[0] + " Current PnL: $" + currentPnL);

                    if (totalTradePnL < LossCutOff)
                    {
                        consecutiveLosses++;
                        Print(Time[0] + " ******** CONSECUTIVE LOSSES: " + consecutiveLosses);
                    }
                    else if (totalTradePnL >= 0)
                    {
                        consecutiveLosses = 0; // Reset the count on a non-loss trade
                        Print(Time[0] + " ******** CONSECUTIVE LOSSES RESET ********");
                    }

                    // Check if there have been three consecutive losing trades
                    if (consecutiveLosses >= maxLossConsec && !DisablePNLLimits)
                    {
                        EnableTrading = false;
                        Print(
                            Time[0]
                                + $" ******** TRADING DISABLED ({consecutiveLosses} losses in a row) ******** : $"
                                + currentPnL
                        );
                    }
                }
            }
        }

        public override string DisplayName
        {
            get
            {
                if (State == State.SetDefaults)
                    return "TradingLevelsAlgo";
                else
                    return "";
            }
        }
        #endregion

        #region Helper Functions
        private double UpdateTPLevel(double targetTP, bool isBuy)
        {
            // Sort the list of target prices
            CalculatedLevels.Sort();

            // If it's a sell check, reverse the order to start from the highest level
            if (!isBuy)
            {
                CalculatedLevels.Reverse();
            }

            double? closestLevel = null;
            double closestDifference = double.MaxValue;

            // Iterate through the sorted list and find the closest level within the target offset
            foreach (double level in CalculatedLevels)
            {
                double difference = Math.Abs(targetTP - level);

                if (difference <= LevelDetectRange)
                {
                    if (MoveLowerOnly)
                    {
                        if (isBuy && level <= targetTP)
                        {
                            if (difference < closestDifference)
                            {
                                closestLevel = level;
                                closestDifference = difference;
                            }
                        }
                        else if (!isBuy && level >= targetTP)
                        {
                            if (difference < closestDifference)
                            {
                                closestLevel = level;
                                closestDifference = difference;
                            }
                        }
                    }
                    else
                    {
                        if (difference < closestDifference)
                        {
                            closestLevel = level;
                            closestDifference = difference;
                        }
                    }
                }
            }

            // Return the closest level found within the target offset or the original targetTP if no level is found
            if (closestLevel.HasValue)
            {
                double newTPLevel = 0;
                if (isBuy)
                {
                    newTPLevel = RoundToNearestTick(closestLevel.Value - TPOffset);
                }
                else
                {
                    newTPLevel = RoundToNearestTick(closestLevel.Value + TPOffset);
                }
                Print(Time[0] + " Dynamic TP: TP Level Updated to: " + newTPLevel + " from previous TP of: " + targetTP);
                return newTPLevel;
            }
            else
            {
                return targetTP;
            }
        }

        private void AddLevel(double level, string levelName)
        {
            double textOffset = 2;
            // Add the level to the targetPrices list
            CalculatedLevels.Add(level);

            // Plot the level on the chart
            Draw.HorizontalLine(this, "TargetLevel" + levelName, level, Brushes.Aquamarine);

            // Draw text on the rightmost side of the horizontal line
            Draw.Text(this, "Label" + levelName, levelName, -10, level + textOffset, Brushes.Aqua);
        }

        private bool IsEntrySafe(double entryPrice, bool isBuy)
        {
            if (isBuy)
            {
                foreach (double level in ProtectiveBuyLevels)
                {
                    if (entryPrice >= level - ProtectiveLevelRangeCheck && entryPrice <= level)
                    {
                        Print(Time[0] + " Long not safe at: " + RoundToNearestTick(entryPrice) + " due to level: " + RoundToNearestTick(level));
                        return false;
                    }
                }
            }
            else
            {
                foreach (double level in ProtectiveSellLevels)
                {
                    if (entryPrice <= level + ProtectiveLevelRangeCheck && entryPrice >= level)
                    {
                        Print(Time[0] + " Short not safe at: " + RoundToNearestTick(entryPrice) + " due to level: " + RoundToNearestTick(level));
                        return false;
                    }
                }
            }
            return true;
        }

        private double GetDynamicEntryOffset(bool buyDir, double deltaBuyVol, double deltaSellVol)
        {
            if (EnableDynamicEntry)
            {
                if (buyDir)
                {
                    if (deltaBuyVol >= DeltaPosCutOff / 100)
                    {
                        if (deltaSellVol >= -1 * DeltaNegCutOff / 100)
                        {
                            Print(Time[0] + " Dynamic Entry: Price Offset by: " + DynamicEntryOffsetTrend + " for Dynamic Delta Trend");
                            return DynamicEntryOffsetTrend;
                        }
                        else
                        {
                            Print(Time[0] + " Dynamic Entry: Price Offset by: " + DynamicEntryOffsetPos + " for Dynamic Delta Positive");
                            return DynamicEntryOffsetPos;
                        }
                    }
                    else
                    {
                        Print(Time[0] + " Dynamic Entry: Price Offset by: " + DynamicEntryOffsetNeg + " for Dynamic Delta Negative");
                        return DynamicEntryOffsetNeg;
                    }
                }
                else
                {
                    if (deltaSellVol >= DeltaPosCutOff / 100)
                    {
                        if (deltaBuyVol >= -1 * DeltaNegCutOff / 100)
                        {
                            Print(Time[0] + " Dynamic Entry: Price Offset by: " + DynamicEntryOffsetTrend + " for Dynamic Delta Trend");
                            return DynamicEntryOffsetTrend;
                        }
                        else
                        {
                            Print(Time[0] + " Dynamic Entry: Price Offset by: " + DynamicEntryOffsetPos + " for Dynamic Delta Positive");
                            return DynamicEntryOffsetPos;
                        }
                    }
                    else
                    {
                        Print(Time[0] + " Dynamic Entry: Price Offset by: " + DynamicEntryOffsetNeg + " for Dynamic Delta Negative");
                        return DynamicEntryOffsetNeg;
                    }
                }
            }
            else
            {
                return 0;
            }
        }

        private double GetLimitLevel(double priceTarget, double close, bool buyDir)
        {
            // Calculate limit level based on direction


            double limitLevel = buyDir
                ? Math.Min(priceTarget, close)
                : Math.Max(priceTarget, close);

            // Round to nearest tick size (if necessary for display or calculation purposes)
            // Note: When actually placing orders, NinjaTrader handles rounding based on tick size.
            limitLevel = RoundToNearestTick(limitLevel);

            return limitLevel;
        }

        private double RoundToNearestTick(double price)
        {
            double tickSize = Instrument.MasterInstrument.TickSize;
            return Math.Round(price / tickSize) * tickSize;
        }

        private bool IsORBSession()
        {
            TimeSpan barTime = Time[0].TimeOfDay;
            if (barTime >= ORBStart.TimeOfDay && barTime < ORBEnd.TimeOfDay)
            {
                return true;
            }
            return false;
        }

        private void GetTimeSessionVariables()
        {

            TimeSpan barTime = Time[0].TimeOfDay;
            if (barTime >= TS1Start.TimeOfDay && barTime < TS1End.TimeOfDay)
            {
                tpLevel = TPLevelTS1;
                slLevel = SLLevelTS1;
                buySellBuffer = BuySellBufferTS1;
                barsToHoldTrade = BarsToHoldTradeTS1;
                barsToMissTrade = BarsToMissTradeTS1;
                offsetFromEntryToCancel = OffsetFromEntryToCancelTS1;
                maxLossConsec = MaxLossConsecTS1;
                resetBarsMissedOnLong = ResetBarsMissedOnLongTS1;
                resetBarsMissedOnShort = ResetBarsMissedOnShortTS1;

                if (lastTimeSession != 1)
                {
                    lastTimeSession = 1;
                    if (EnableTradingTS1)
                    {
                        Print(Time[0] + " ******** TRADING SESSION 1 ******** ");
                        Draw.VerticalLine(this, "Session1", 0, Brushes.Aquamarine, DashStyleHelper.Dash, 2);
                        if (ResetConsecOnTime)
                        {
                            consecutiveLosses = 0;
                            EnableTrading = true;
                            Print(Time[0] + " ******** CONSECUTIVE LOSSES RESET ON SESSION CHANGE ******** ");
                        }
                        SetStopLoss("Long", CalculationMode.Ticks, slLevel / TickSize, false);
                        SetProfitTarget("Long", CalculationMode.Ticks, tpLevel / TickSize);
                        SetStopLoss("Short", CalculationMode.Ticks, slLevel / TickSize, false);
                        SetProfitTarget("Short", CalculationMode.Ticks, tpLevel / TickSize);
                    }
                }
                if (!EnableTradingTS1)
                {
                    EnableTrading = false;
                }
            }
            else if (barTime >= TS2Start.TimeOfDay && barTime < TS2End.TimeOfDay)
            {
                tpLevel = TPLevelTS2;
                slLevel = SLLevelTS2;
                buySellBuffer = BuySellBufferTS2;
                barsToHoldTrade = BarsToHoldTradeTS2;
                barsToMissTrade = BarsToMissTradeTS2;
                offsetFromEntryToCancel = OffsetFromEntryToCancelTS2;
                maxLossConsec = MaxLossConsecTS2;
                resetBarsMissedOnLong = ResetBarsMissedOnLongTS2;
                resetBarsMissedOnShort = ResetBarsMissedOnShortTS2;


                if (lastTimeSession != 2)
                {
                    lastTimeSession = 2;
                    if (EnableTradingTS2)
                    {
                        Print(Time[0] + " ******** TRADING SESSION 2 ******** ");
                        Draw.VerticalLine(this, "Session2", 0, Brushes.Aquamarine, DashStyleHelper.Dash, 2);
                        if (ResetConsecOnTime)
                        {
                            consecutiveLosses = 0;
                            EnableTrading = true;
                            Print(Time[0] + " ******** CONSECUTIVE LOSSES RESET ON SESSION CHANGE ******** ");
                        }
                        SetStopLoss("Long", CalculationMode.Ticks, slLevel / TickSize, false);
                        SetProfitTarget("Long", CalculationMode.Ticks, tpLevel / TickSize);
                        SetStopLoss("Short", CalculationMode.Ticks, slLevel / TickSize, false);
                        SetProfitTarget("Short", CalculationMode.Ticks, tpLevel / TickSize);
                    }
                }
                if (!EnableTradingTS2)
                {
                    EnableTrading = false;
                }
            }
            else if (barTime >= TS3Start.TimeOfDay && barTime < TS3End.TimeOfDay)
            {
                tpLevel = TPLevelTS3;
                slLevel = SLLevelTS3;
                buySellBuffer = BuySellBufferTS3;
                barsToHoldTrade = BarsToHoldTradeTS3;
                barsToMissTrade = BarsToMissTradeTS3;
                offsetFromEntryToCancel = OffsetFromEntryToCancelTS3;
                maxLossConsec = MaxLossConsecTS3;
                resetBarsMissedOnLong = ResetBarsMissedOnLongTS3;
                resetBarsMissedOnShort = ResetBarsMissedOnShortTS3;

                if (lastTimeSession != 3)
                {
                    lastTimeSession = 3;
                    if (EnableTradingTS3)
                    {
                        Print(Time[0] + " ******** TRADING SESSION 3 ******** ");
                        Draw.VerticalLine(this, "Session3", 0, Brushes.Aquamarine, DashStyleHelper.Dash, 2);
                        if (ResetConsecOnTime && EnableTradingTS3)
                        {
                            consecutiveLosses = 0;
                            EnableTrading = true;
                            Print(Time[0] + " ******** CONSECUTIVE LOSSES RESET ON SESSION CHANGE ******** ");
                        }
                        SetStopLoss("Long", CalculationMode.Ticks, slLevel / TickSize, false);
                        SetProfitTarget("Long", CalculationMode.Ticks, tpLevel / TickSize);
                        SetStopLoss("Short", CalculationMode.Ticks, slLevel / TickSize, false);
                        SetProfitTarget("Short", CalculationMode.Ticks, tpLevel / TickSize);
                    }
                }
                if (!EnableTradingTS3)
                {
                    EnableTrading = false;
                }
            }
            else if (DisableTradingTimes)
            {
                tpLevel = TPLevelTS3;
                slLevel = SLLevelTS3;
                buySellBuffer = BuySellBufferTS3;
                barsToHoldTrade = BarsToHoldTradeTS3;
                barsToMissTrade = BarsToMissTradeTS3;
                offsetFromEntryToCancel = OffsetFromEntryToCancelTS3;
                maxLossConsec = MaxLossConsecTS3;
                resetBarsMissedOnLong = ResetBarsMissedOnLongTS3;
                resetBarsMissedOnShort = ResetBarsMissedOnShortTS3;

                if (lastTimeSession != 4)
                {
                    lastTimeSession = 4;
                    Print(Time[0] + " ******** TRADING SESSION 4 ******** ");
                    Draw.VerticalLine(this, "Session4", 0, Brushes.Aquamarine, DashStyleHelper.Dash, 2);
                    if (ResetConsecOnTime)
                    {
                        consecutiveLosses = 0;
                        EnableTrading = true;
                        Print(Time[0] + " ******** CONSECUTIVE LOSSES RESET ON SESSION CHANGE ******** ");
                    }
                    SetStopLoss("Long", CalculationMode.Ticks, slLevel / TickSize, false);
                    SetProfitTarget("Long", CalculationMode.Ticks, tpLevel / TickSize);
                    SetStopLoss("Short", CalculationMode.Ticks, slLevel / TickSize, false);
                    SetProfitTarget("Short", CalculationMode.Ticks, tpLevel / TickSize);
                }
            }
            else if (!DisableTradingTimes)
            {
                EnableTrading = false;
            }


            if (EnableTradingTS1 && barTime == TS1End.TimeOfDay)
            {
                Print(Time[0] + " ******** TRADING SESSION 1 ENDED ********");
                Draw.VerticalLine(this, "Session1End", 0, Brushes.Orange, DashStyleHelper.Dot, 2);
            }
            if (EnableTradingTS2 && barTime == TS2End.TimeOfDay)
            {
                Print(Time[0] + " ******** TRADING SESSION 2 ENDED ********");
                Draw.VerticalLine(this, "Session2End", 0, Brushes.Orange, DashStyleHelper.Dot, 2);
            }
            if (EnableTradingTS3 && barTime == TS3End.TimeOfDay)
            {
                Print(Time[0] + " ******** TRADING SESSION 3 ENDED ********");
                Draw.VerticalLine(this, "Session3End", 0, Brushes.Orange, DashStyleHelper.Dot, 2);
            }
        }
        #endregion
    }
}
