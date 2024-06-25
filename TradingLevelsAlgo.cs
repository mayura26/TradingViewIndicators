#region Using declarations
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Security.Policy;
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
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;
using static NinjaTrader.CQG.ProtoBuf.MarketDataSubscription.Types;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it.
namespace NinjaTrader.NinjaScript.Strategies
{
    /* TODO LIST  / bug liquidity level offset right? 
	// TODO: On fill, if bounce protect do we still check and close trade? need to check close is less than level bounced
    // FEATURE: Rework delta volume code to allow for delta diff
    // FEATURE: Change color of background when in chase mode
    // FEATURE: Look at differntial difference between buy and sell as a percentage and if its too small then don't trade
	// FEATURE: Look at  parabolic stop and reverse (PSAR)  and supertrend as trailing stop
    // FEATURE: If px comes back through VWAP then we shouldn't consider it a proetctive level
    // FEATURE: Cancel order when in chopzone
    // FEATURE: Power hour protect (don't trade in first and last 30 mins of the day if its a Monday or a Friday)
    // FEATURE: LOok at height of wicks and candle size combined with direction change to create a protective no trades mode.
    // FEATURE: Create chop indicator with trend chop detection and momentum and delta momentum
    // REVIEW: Review level calcs with S1/S2/S3 levels
    // TODO: ATR Exit to be base on trade being held for x bars
    // FEATURE: EMA levels to exit trades
    // FEATURE: Design dynamic calc of TP level using ATR or similar
    // FEATURE: Add timeout after two bad trades in succession
    // FEATURE: Change to process on tick and have trading on first tick ***** IMPORTANT *****
    // FEATURE: Look at fib levels to improve drawing of levels
    // FEATURE: Dynamic entry for blue volume is high and maybe needs to adjust if trade goes into key level? If last candle is a bounce then we reduce the dynamic entry?
    */
    public class TradingLevelsAlgo : Strategy
    {
        #region Properties
        #region 1. Main Parameters
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "TradeQuantity", Description = "Number of contracts to trade", Order = 2, GroupName = "1. Main Parameters")]
        public int TradeQuantity
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, double.MaxValue)]
        [Display(Name = "MaxGainRatio", Description = "Maximum daily gain before trading stops", Order = 3, GroupName = "1. Main Parameters")]
        public double MaxGainRatio
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, double.MaxValue)]
        [Display(Name = "MaxLossRatio", Description = "Maximum loss before trading stops", Order = 4, GroupName = "1. Main Parameters")]
        public double MaxLossRatio
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

        #region 2. Chase Mode
        [NinjaScriptProperty]
        [Display(Name = "EnableChaseMode", Description = "Enable chase mode", Order = 43, GroupName = "2. Chase Mode")]
        public bool EnableChaseMode
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "EnableChaseModeRestart", Description = "Enable chase mode restart", Order = 44, GroupName = "2. Chase Mode")]
        public bool EnableChaseModeRestart
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "ChaseMaxBars", Description = "Max bars to chase", Order = 45, GroupName = "2. Chase Mode")]
        public int ChaseMaxBars
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "ChaseDeltaMinDiff", Description = "Min delta difference to chase", Order = 46, GroupName = "2. Chase Mode")]
        public double ChaseDeltaMinDiff
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "ChaseDeltaBigDiff", Description = "Big delta difference to chase", Order = 47, GroupName = "2. Chase Mode")]
        public double ChaseDeltaBigDiff
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "ChaseNewTPLevel", Description = "New TP level for chase", Order = 48, GroupName = "2. Chase Mode")]
        public double ChaseNewTPLevel
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

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "DeltaDiffCutOff", Description = "Delta volume cutoff for difference between pos and neg delta trades (%)", Order = 49, GroupName = "3. Dynamic Trades")]
        public double DeltaDiffCutOff
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "EnableMinDiffMode", Description = "Enable minimum difference mode", Order = 50, GroupName = "3. Dynamic Trades")]
        public bool EnableMinDiffMode
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "MinDiffCutOff", Description = "Minimum difference between pos and neg delta trades (%)", Order = 51, GroupName = "3. Dynamic Trades")]
        public double MinDiffCutOff
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

        [NinjaScriptProperty]
        [Display(Name = "EnableTPChopZone", Description = "Enable TP level based on chop zone", Order = 69, GroupName = "3. Dynamic Takeprofit")]
        public bool EnableTPChopZone
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, double.MaxValue)]
        [Display(Name = "TPChopZoneSearchRange", Description = "Search range for chop zone TP mode", Order = 70, GroupName = "3. Dynamic Takeprofit")]
        public double TPChopZoneSearchRange
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "TPChopZoneOffset", Description = "Offset from chop zone top for TP", Order = 71, GroupName = "3. Dynamic Takeprofit")]
        public double TPChopZoneOffset
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "EnableTPVWAP", Description = "Enable TP level based on VWAP", Order = 72, GroupName = "3. Dynamic Takeprofit")]
        public bool EnableTPVWAP
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, double.MaxValue)]
        [Display(Name = "TPVWAPSearchRange", Description = "Search range for VWAP TP mode", Order = 73, GroupName = "3. Dynamic Takeprofit")]
        public double TPVWAPSearchRange
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "TPVWAPOffset", Description = "Offset from VWAP for TP", Order = 74, GroupName = "3. Dynamic Takeprofit")]
        public double TPVWAPOffset
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "EnableTPDayHLPriority", Description = "Enable TP level based on day high/low", Order = 75, GroupName = "3. Dynamic Takeprofit")]
        public bool EnableTPDayHLPriority
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, double.MaxValue)]
        [Display(Name = "TPDayHLSearchRange", Description = "Search range for day high/low TP mode", Order = 76, GroupName = "3. Dynamic Takeprofit")]
        public double TPDayHLSearchRange
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "TPDayHLOffset", Description = "Offset from day high/low for TP", Order = 77, GroupName = "3. Dynamic Takeprofit")]
        public double TPDayHLOffset
        { get; set; }
        #endregion

        #region 3. Liquidity Levels 
        [NinjaScriptProperty]
        [Display(Name = "EnableLiquidityFills", Description = "Enable liquidity level fills", Order = 60, GroupName = "3. Liquidity Levels")]
        public bool EnableLiquidityFills
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "LiquidityFillSearchRange", Description = "Search range for liquidity fills", Order = 61, GroupName = "3. Liquidity Levels")]
        public double LiquidityFillSearchRange
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "EnableLiquidityTP", Description = "Enable liquidity level TP", Order = 62, GroupName = "3. Liquidity Levels")]
        public bool EnableLiquidityTP
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "LiquidityTPUpperRange", Description = "Upper range for liquidity TP", Order = 63, GroupName = "3. Liquidity Levels")]
        public double LiquidityTPUpperRange
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "LiquidityTPLowerRange", Description = "Lower range for liquidity TP", Order = 64, GroupName = "3. Liquidity Levels")]
        public double LiquidityTPLowerRange
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "LiquidityTrimRange", Description = "Trim range for liquidity TP", Order = 65, GroupName = "3. Liquidity Levels")]
        public double LiquidityTrimRange
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "LiquidityTPOffset", Description = "Offset from liquidity level for TP", Order = 66, GroupName = "3. Liquidity Levels")]
        public double LiquidityTPOffset
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

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "ChopZoneClearOffset", Description = "Offset from chop zone for clearing zone", Order = 57, GroupName = "4. Chop Zone")]
        public double ChopZoneClearOffset
        { get; set; }
        #endregion

        #region 4. Protective Trades
        [NinjaScriptProperty]
        [Display(Name = "EnableProtectiveLevelTrades", Description = "Enable protective level trades", Order = 57, GroupName = "4. Protective Trades")]
        public bool EnableProtectiveLevelTrades
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, double.MaxValue)]
        [Display(Name = "ProtectiveLevelRangeCheck", Description = "Range to check for protective level trades", Order = 59, GroupName = "4. Protective Trades")]
        public double ProtectiveLevelRangeCheck
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "EnableDynamicRangeProtect", Description = "Enable dynamic range protection", Order = 64, GroupName = "4. Protective Trades")]
        public bool EnableDynamicRangeProtect
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "DynamicRangeLookback", Description = "Lookback bars for dynamic range protection", Order = 65, GroupName = "4. Protective Trades")]
        public int DynamicRangeLookback
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "DynamicRangePosition", Description = "Position in range for dynamic range protection to enter trade", Order = 66, GroupName = "4. Protective Trades")]
        public double DynamicRangePosition
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 10000)]
        [Display(Name = "DynamicRangeMinRange", Description = "Minimum range for dynamic range protection", Order = 67, GroupName = "4. Protective Trades")]
        public double DynamicRangeMinRange
        { get; set; }
        #endregion

        #region 4a. Extra Protective Trades
        [NinjaScriptProperty]
        [Display(Name = "EnableSuperProtectMode", Description = "Enable super protective mode", Order = 50, GroupName = "4a. Protective Trades")]
        public bool EnableSuperProtectMode
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "EnableVWAPBlock", Description = "Enable VWAP block for protective trades", Order = 60, GroupName = "4a. Protective Trades")]
        public bool EnableVWAPBlock
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "EnableChopZoneBlock", Description = "Enable chop zone block for protective trades", Order = 61, GroupName = "4a. Protective Trades")]
        public bool EnableChopZoneBlock
        { get; set; }
        #endregion

        #region 4. Bounce Trades
        [NinjaScriptProperty]
        [Display(Name = "EnableBounceProtect", Description = "Enable bounce protection", Order = 62, GroupName = "4. Bounce Trades")]
        public bool EnableBounceProtect
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "BounceLookback", Description = "Lookback bars for bounce protection", Order = 63, GroupName = "4. Bounce Trades")]
        public int BounceLookback
        { get; set; }

        [NinjaScriptProperty]
        [Range(-100, 100)]
        [Display(Name = "BounceOffset", Description = "Offset for identifying bounce", Order = 64, GroupName = "4. Bounce Trades")]
        public double BounceOffset
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, double.MaxValue)]
        [Display(Name = "BounceCheckRange", Description = "Range to check for bounce", Order = 65, GroupName = "4. Bounce Trades")]
        public double BounceCheckRange
        { get; set; }
        #endregion

        #region 4. ATR Trades
        [NinjaScriptProperty]
        [Display(Name = "EnableRestartOnATR", Description = "Enable restart on ATR trigger", Order = 49, GroupName = "4. ATR Trades")]
        public bool EnableRestartOnATR
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ExitOnATRReversal", Description = "Exit on ATR reversal", Order = 51, GroupName = "4. ATR Trades")]
        public bool ExitOnATRReversal
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "EnableATRProtect", Description = "Enable ATR protection", Order = 52, GroupName = "4. ATR Trades")]
        public bool EnableATRProtect
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "ATRProtectLookback", Description = "Lookback bars for ATR protection", Order = 53, GroupName = "4. ATR Trades")]
        public int ATRProtectLookback
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
        [Display(Name = "EnableNewFeatures", Description = "Enable new features", Order = 1, GroupName = "9. Trade Settings")]
        public bool EnableNewFeatures
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "RealTimePnlOnly", Description = "Track PnL only during realtime trading", Order = 4, GroupName = "9. Trade Settings")]
        public bool RealTimePnlOnly
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "EnableBannedDays", Description = "Enable banned days for backtesting", Order = 5, GroupName = "9. Trade Settings")]
        public bool EnableBannedDays
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "DisableTradingTimes", Description = "Disable preset trading times", Order = 3, GroupName = "9. Trade Settings")]
        public bool DisableTradingTimes
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "DisablePNLLimits", Description = "Disable PnL limits for the day", Order = 2, GroupName = "9. Trade Settings")]
        public bool DisablePNLLimits
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ResetConsecOnTime", Description = "Reset consec. losses on time session switch", Order = 6, GroupName = "9. Trade Settings")]
        public bool ResetConsecOnTime
        { get; set; }
        #endregion
        #endregion

        #region Variables
        #region Trade Variables
        private Cbi.Order entryOrder = null;
        private Cbi.Order entryOrderTrim = null;
        private double entryPrice = 0.0;
        private int entryBar = -1;

        private Cbi.Order entryOrderShort = null;
        private Cbi.Order entryOrderTrimShort = null;
        private double entryPriceShort = 0.0;
        private int entryBarShort = -1;

        int consecutiveLosses = 0;
        int bigWinCount = 0;
        private double triggerPrice = 0.0;

        int chaseBars = 0;
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
        private double currentTradeTP = 0;
        private double limitLevelPrev = 0;
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

        // Day Level Constants
        private int dayBarsToUse = 10;
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
        private double currentTrailingDrawdown;
        private List<DateTime> TradingBanDays;
        public double MaxGain;
        public double MaxLoss;
        public double TrailingDrawdownLimit;
        public double BigWinCutoff;
        public double LossCutOff;
        private double highestDailyPnL = 0;
        private int lastTradeChecked = -1;
        private double currentTradePnL = 0;
        private int partialTradeQty = 0;
        private double partialTradePnL = 0;
        private bool newTradeCalculated = false;
        private bool partialTradeCalculated = false;
        private bool newTradeExecuted = false;
        private int barsInTrade = 0;
        private string tradeExecuteType = "";
        private double tradeExecPrice = 0;
        private double oldDynamicTP = 0;
        private double oldLiquidityTP = 0;
        private double oldTrimTP = 0;
        private double oldLiquidityTrim = 0;
        private double oldDynamicSL = 0;
        private bool chaseModePrev = false;

        private int numTrades = 0;
        private int numWins = 0;
        private int numLosses = 0;
        private int numBlockedProtective = 0;
        private int numBlockedDynamicRange = 0;
        private int numBlockedBounce = 0;
        private int numATRRestart = 0;
        private int numATRProtect = 0;
        private int numChaseModeTrades = 0;
        private int numChaseModeRestarts = 0;
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
        public Brush ChaseShadeTrend = Brushes.Fuchsia;
        public Brush ChaseShadePos = Brushes.Cyan;
        public int DeltaShadeOpacity = 25;
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
        private List<double> BounceHighLevels;
        private List<double> BounceLowLevels;
        private List<double> LiquidityLevels;

        private double orbHigh = double.MinValue;
        private double orbLow = double.MaxValue;
        private double dayHigh = double.MinValue;
        private double dayLow = double.MaxValue;
        private int dayHighBar = -1;
        private int dayLowBar = -1;
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
                EntryHandling = EntryHandling.UniqueEntries;
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
                StopTargetHandling = StopTargetHandling.PerEntryExecution;
                BarsRequiredToTrade = 20;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 30;
                EntriesPerDirection = 1;
                // Disable this property for performance gains in Strategy Analyzer optimizations
                // See the Help Guide for additional information
                IsInstantiatedOnEachOptimizationIteration = true;
                #endregion

                #region Properties Defaults
                #region Trading Settings
                RealTimePnlOnly = false;
                DisableTradingTimes = false;
                DisablePNLLimits = false;
                EnableBannedDays = true;
                EnableNewFeatures = false;
                #endregion
                #region Main Parameters
                TradeQuantity = 5;
                MaxLossRatio = 115;
                MaxGainRatio = 125;
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
                SLLevelTS2 = 18;
                BuySellBufferTS2 = 6;
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
                DeltaPosCutOff = 3;
                DeltaNegCutOff = -1.5;
                DeltaDiffCutOff = 5;
                EnableMinDiffMode = false;
                MinDiffCutOff = 2.5;
                #endregion
                #region Dyanmic SL/TP Settings
                EnableDynamicSL = true;
                ProfitToMoveSL = 32;
                SLNewLevel = -2;
                TPCalcFromInitTrigger = false;

                EnableDynamicTP = true;
                MoveLowerOnly = true;
                LevelDetectRange = 15;
                TPOffset = 8;

                EnableTPChopZone = true;
                TPChopZoneSearchRange = 15;
                TPChopZoneOffset = 1;

                EnableTPVWAP = true;
                TPVWAPSearchRange = 10;
                TPVWAPOffset = 1;

                EnableTPDayHLPriority = true;
                TPDayHLSearchRange = 30;
                TPDayHLOffset = 5;
                #endregion
                #region Chop Zone Settings
                EnableChopZone = true;
                EnableExtendedChopZone = true;
                ChopZoneMaxRange = 30;
                ChopZoneMinDir = 2;
                ChopZoneTimeFrame = 10;
                ChopZoneResetTime = 120;
                ChopZoneLookBack = 3;
                ChopZoneClearOffset = 5;
                #endregion
                #region Protective Trades
                EnableProtectiveLevelTrades = true;
                ProtectiveLevelRangeCheck = 12;
                EnableDynamicRangeProtect = false;
                DynamicRangeLookback = 4;
                DynamicRangePosition = 75;
                DynamicRangeMinRange = 25;
                #endregion
                #region Extra Protective Trades 
                EnableSuperProtectMode = false;
                EnableVWAPBlock = false;
                EnableChopZoneBlock = false;
                #endregion
                #region Dynamic Entry/Exit
                EnableDynamicEntry = true;
                DynamicEntryOffsetTrend = 3;
                DynamicEntryOffsetPos = 0;
                DynamicEntryOffsetNeg = -3;
                #endregion
                #region Gain Protection
                EnableTrailingDrawdown = false;
                BigWinCutoffCount = 5;
                BigWinCutoffRatio = 50;
                TrailingDrawdownRatio = 100;
                #endregion
                #region Dynamic Trim
                EnableDynamicTrim = true;
                ExitTPLevel = 10;
                TrimPercent = 60;
                ExitLevelRange = 3;
                ExitLevelOffset = 1;
                #endregion
                #region Bounce Trades
                EnableBounceProtect = true;
                BounceLookback = 2;
                BounceOffset = 1;
                BounceCheckRange = 40;
                #endregion
                #region ATR Trades
                EnableRestartOnATR = true;
                ExitOnATRReversal = false;
                EnableATRProtect = false;
                ATRProtectLookback = 3;
                #endregion
                #region Liquidity Levels
                EnableLiquidityFills = true;
                LiquidityFillSearchRange = 3.5;
                EnableLiquidityTP = true;
                LiquidityTPUpperRange = 3.5;
                LiquidityTPLowerRange = 9;
                LiquidityTrimRange = 2;
                LiquidityTPOffset = 1;
                #endregion
                #region Chase Mode
                EnableChaseMode = false;
                EnableChaseModeRestart = false;
                ChaseMaxBars = 3;
                ChaseDeltaMinDiff = 4;
                ChaseDeltaBigDiff = 30;
                ChaseNewTPLevel = 75;
                #endregion
                #endregion

                #region Banned Trading Days
                TradingBanDays = new List<DateTime>
                {
                    DateTime.Parse("2024-06-19", System.Globalization.CultureInfo.InvariantCulture), // Halfday
                    DateTime.Parse("2024-06-12", System.Globalization.CultureInfo.InvariantCulture), // FOMC + CPI
                    DateTime.Parse("2024-05-24", System.Globalization.CultureInfo.InvariantCulture), // Friday before long weekend
                    DateTime.Parse("2024-05-22", System.Globalization.CultureInfo.InvariantCulture), // Tight range from OPEX, identified in the morning with NVDA on the bell
                    DateTime.Parse("2024-05-20", System.Globalization.CultureInfo.InvariantCulture), // Post opex monday
                    DateTime.Parse("2024-05-13", System.Globalization.CultureInfo.InvariantCulture),
                    DateTime.Parse("2024-05-03", System.Globalization.CultureInfo.InvariantCulture),
                    DateTime.Parse("2024-04-09", System.Globalization.CultureInfo.InvariantCulture),
                    DateTime.Parse("2024-04-10", System.Globalization.CultureInfo.InvariantCulture),
                    DateTime.Parse("2024-04-11", System.Globalization.CultureInfo.InvariantCulture)
                };
                #endregion
            }
            else if (State == State.DataLoaded)
            {
                ClearOutputWindow();
                Print(Time[0] + " ******** TRADING ALGO v2.2 ******** ");
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
                BounceHighLevels = new List<double>();
                BounceLowLevels = new List<double>();
                ORBStart = DateTime.Parse("09:30", System.Globalization.CultureInfo.InvariantCulture);
                ORBEnd = DateTime.Parse("10:00", System.Globalization.CultureInfo.InvariantCulture);

                // Initialize EMAs
                smoothConfirmMA = DynamicTrendLine(8, 13, 21);
                vwap = VWAP();
                currentDayOHL = CurrentDayOHL();
                atrHMA = HMA(atrTR, numATR);

                // Liquidity Levels
                LiquidityLevels = new List<double>
                {
                    20,
                    33.5,
                    46,
                    66,
                    80,
                    93,
                    3.5
                };

                // Add our EMAs to the chart for visualization
                AddChartIndicator(smoothConfirmMA);
                AddChartIndicator(vwap);
                #endregion

                #region SL/TP
                SetStopLoss("Long", CalculationMode.Ticks, slLevel / TickSize, false);
                SetProfitTarget("Long", CalculationMode.Ticks, tpLevel / TickSize);
                SetStopLoss("Short", CalculationMode.Ticks, slLevel / TickSize, false);
                SetProfitTarget("Short", CalculationMode.Ticks, tpLevel / TickSize);
                SetStopLoss("LongTrim", CalculationMode.Ticks, slLevel / TickSize, false);
                SetProfitTarget("LongTrim", CalculationMode.Ticks, ExitTPLevel / TickSize);
                SetStopLoss("ShortTrim", CalculationMode.Ticks, slLevel / TickSize, false);
                SetProfitTarget("ShortTrim", CalculationMode.Ticks, ExitTPLevel / TickSize);
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

                Brush chaseShadeTrend = ChaseShadeTrend.Clone(); //Copy the brush into a temporary brush
                chaseShadeTrend.Opacity = DeltaShadeOpacity / 100.0; // set the opacity
                chaseShadeTrend.Freeze(); // freeze the temp brush
                ChaseShadeTrend = chaseShadeTrend; // assign the temp brush value to ChaseShadeTrend.

                Brush chaseShadePos = ChaseShadePos.Clone(); //Copy the brush into a temporary brush
                chaseShadePos.Opacity = DeltaShadeOpacity / 100.0; // set the opacity
                ChaseShadePos.Freeze();
                ChaseShadePos = chaseShadePos; // assign the temp brush value to ChaseShadePos.
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

            #region Time Session Functions/Initialisation
            // Reset PnL at the start of the session
            if (Bars.IsFirstBarOfSession)
            {
                currentPnL = 0;
                consecutiveLosses = 0;
                bigWinCount = 0;
                TrailingDrawdownLimit = 0;
                dayHigh = double.MinValue;
                dayLow = double.MaxValue;
                orbHigh = 0;
                orbLow = double.MaxValue;
                highestDailyPnL = 0;

                numWins = 0;
                numLosses = 0;
                numTrades = 0;
                numBlockedProtective = 0;
                numBlockedDynamicRange = 0;
                numBlockedBounce = 0;
                numATRRestart = 0;
                numATRProtect = 0;
                numChaseModeTrades = 0;
                numChaseModeRestarts = 0;

                RemoveDrawObject("TargetLevel" + "ORB High");
                RemoveDrawObject("TargetLevel" + "ORB Low");
                RemoveDrawObject("Label" + "ORB High");
                RemoveDrawObject("Label" + "ORB Low");

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
                if (Close[0] < lowerChopZone && (Close[1] < lowerChopZone || Close[0] < lowerChopZone - ChopZoneClearOffset))
                    reenterChopZoneBot = true;
                if (Close[0] > upperChopZone && (Close[1] > upperChopZone || Close[0] > upperChopZone + ChopZoneClearOffset))
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
            bool protectiveChopZone = !(Math.Abs(trendDirection[0]) < chopLimit && deltaMomentum[0] < deltaMomentumChopLimt)
                                        && !chopIndexDetect[0]
                                        && !inChopZone[0];
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
                if (Position.MarketPosition == MarketPosition.Short && Close[0] <= Position.AveragePrice - 5)
                {
                    Print(Time[0] + (ExitOnATRReversal ? "" : " [NOT ACTIVE]") + " [Protective Trades]: Close Sell Trade due to ATR Signal");
                    if (ExitOnATRReversal)
                        closeShortOnATRReversal = true;
                }
                Draw.TriangleUp(this, "BuyATRSignal" + CurrentBar, true, 0, Low[0] - TickSize * 110, Brushes.Navy);
            }
            else if (sellATRSignal[0])
            {
                if (ExitOnATRReversal && Position.MarketPosition == MarketPosition.Long && Close[0] >= Position.AveragePrice + 5)
                {
                    Print(Time[0] + (ExitOnATRReversal ? "" : " [NOT ACTIVE]") + " [Protective Trades]: Close Buy Trade due to ATR Signal");
                    if (ExitOnATRReversal)
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
            double deltaDiffVol = Math.Abs(deltaBuyVol - deltaSellVol);
            double volDelta = 0;
            if (smoothBuy[0] > smoothSell[0])
            {
                volDelta = smoothNetVol[0] / smoothSell[0] * 100;
            }
            else
            {
                volDelta = smoothNetVol[0] / smoothBuy[0] * 100;
            }
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
            bool dayHighLevelUsable = false;
            bool dayLowLevelUsable = false;
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
                if (EnableSuperProtectMode)
                {
                    ProtectiveBuyLevels.Add(R3);
                }

                ProtectiveSellLevels.Clear();
                ProtectiveSellLevels.Add(vwap[0]);
                ProtectiveSellLevels.Add(lastWeekLow);
                ProtectiveSellLevels.Add(yesterdayLow);
                ProtectiveSellLevels.Add(atrNeg618);
                ProtectiveSellLevels.Add(atrNeg100);
                ProtectiveSellLevels.Add(S6); // Bear Target
                ProtectiveSellLevels.Add(S4); // Bull Reversal
                if (EnableSuperProtectMode)
                {
                    ProtectiveSellLevels.Add(S3);
                }
                #endregion

                #region Bounce Levels Array
                BounceHighLevels.Clear();
                BounceHighLevels.Add(vwap[0]);
                BounceHighLevels.Add(lastWeekHigh);
                BounceHighLevels.Add(yesterdayHigh);
                BounceHighLevels.Add(atr618);
                BounceHighLevels.Add(atr100);
                BounceHighLevels.Add(atrBull);
                BounceHighLevels.Add(R6); // Bull Target
                BounceHighLevels.Add(R4); // Bear Reversal
                if (EnableSuperProtectMode)
                {
                    BounceHighLevels.Add(R3);
                }

                BounceLowLevels.Clear();
                BounceLowLevels.Add(vwap[0]);
                BounceLowLevels.Add(lastWeekLow);
                BounceLowLevels.Add(yesterdayLow);
                BounceLowLevels.Add(atrNeg618);
                BounceLowLevels.Add(atrNeg100);
                BounceLowLevels.Add(atrBear);
                BounceLowLevels.Add(S6); // Bear Target
                BounceLowLevels.Add(S4); // Bull Reversal
                if (EnableSuperProtectMode)
                {
                    BounceLowLevels.Add(S3);
                }
                #endregion

                #region Dynamic Levels
                #region ORB Levels
                TimeSpan barTime = Time[0].TimeOfDay;
                if (barTime >= ORBStart.TimeOfDay && barTime <= ORBEnd.TimeOfDay)
                {
                    if (High[0] > orbHigh)
                    {
                        orbHigh = High[0];
                    }
                    if (Low[0] < orbLow)
                    {
                        orbLow = Low[0];
                    }
                }
                else if (barTime > ORBEnd.TimeOfDay)
                {
                    if (orbHigh > 0)
                    {
                        AddLevel(orbHigh, "ORB High");
                        if (EnableSuperProtectMode)
                        {
                            ProtectiveBuyLevels.Add(orbHigh);
                        }
                    }
                    if (orbLow < double.MaxValue)
                    {
                        AddLevel(orbLow, "ORB Low");
                        if (EnableSuperProtectMode)
                        {
                            ProtectiveSellLevels.Add(orbLow);
                        }
                    }
                }
                #endregion

                #region Day High/Low
                if (High[0] > dayHigh)
                {
                    dayHigh = High[0];
                    dayHighBar = CurrentBar;
                    RemoveDrawObject("TargetLevel" + "Day High");
                    RemoveDrawObject("Label" + "Day High");
                }

                if (Low[0] < dayLow)
                {
                    dayLow = Low[0];
                    dayLowBar = CurrentBar;
                    RemoveDrawObject("TargetLevel" + "Day Low");
                    RemoveDrawObject("Label" + "Day Low");
                }

                if (CurrentBar - dayHighBar > dayBarsToUse && dayHigh > 0)
                {
                    AddLevel(dayHigh, "Day High");
                    ProtectiveBuyLevels.Add(dayHigh);
                    BounceHighLevels.Add(dayHigh);
                    dayHighLevelUsable = true;
                }

                if (CurrentBar - dayLowBar > dayBarsToUse && dayLow < double.MaxValue)
                {
                    AddLevel(dayLow, "Day Low");
                    ProtectiveSellLevels.Add(dayLow);
                    BounceLowLevels.Add(dayLow);
                    dayLowLevelUsable = true;
                }
                #endregion
                #endregion
            }
            #endregion

            #region PnL Calculation
            #region Dynamic Gain/Loss
            MaxGain = MaxGainRatio * TradeQuantity;
            MaxLoss = MaxLossRatio * TradeQuantity * -1;
            TrailingDrawdownLimit = TrailingDrawdownRatio * TradeQuantity * -1;
            BigWinCutoff = BigWinCutoffRatio * TradeQuantity;
            LossCutOff = LossCutOffRatio * TradeQuantity * -1;
            #endregion

            #region Big Win/Consecutive Losses & Trade Updates
            if (newTradeExecuted)
            {
                Print(Time[0] + " TRADE CLOSED: " + tradeExecuteType + " at Price: " + tradeExecPrice);
                newTradeExecuted = false;
            }

            if (partialTradeCalculated)
            {
                Print(Time[0] + " CURRENT TRADE PnL: $" + partialTradePnL +
                    " | Current PnL: $" + currentPnL +
                    " | Points : " + RoundToNearestTick(partialTradePnL / partialTradeQty / Bars.Instrument.MasterInstrument.PointValue) +
                    " | Quantity: " + partialTradeQty);
                partialTradePnL = 0;
                partialTradeQty = 0;
                partialTradeCalculated = false;
            }

            if (Position.MarketPosition == MarketPosition.Flat && newTradeCalculated)
            {
                Print(Time[0] + " COMPLETED TRADE PnL: $" + currentTradePnL + " | Total PnL: $" + currentPnL);
                if (currentTradePnL < LossCutOff)
                {
                    consecutiveLosses++;
                    Print(Time[0] + " ******** CONSECUTIVE LOSSES: " + consecutiveLosses);
                }
                else if (currentTradePnL >= 0 && consecutiveLosses > 0)
                {
                    consecutiveLosses = 0; // Reset the count on a non-loss trade
                    Print(Time[0] + " ******** CONSECUTIVE LOSSES RESET ********");
                }

                if (currentTradePnL > BigWinCutoff)
                {
                    bigWinCount++;
                    Print(Time[0] + " ******** BIG WINS: " + bigWinCount);
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

                if (bigWinCount >= BigWinCutoffCount && !DisablePNLLimits)
                {
                    EnableTrading = false;
                    Print(Time[0] + $" ******** TRADING DISABLED ({bigWinCount} big wins) ******** : $" + currentPnL);
                }

                if (currentTradePnL / TradeQuantity / Bars.Instrument.MasterInstrument.PointValue > 0.1 * tpLevel)
                {
                    numWins++;
                }
                else if (currentTradePnL / TradeQuantity / Bars.Instrument.MasterInstrument.PointValue < -0.1 * tpLevel)
                {
                    numLosses++;
                }

                numTrades++;
                Print(Time[0] + " ******** TRADES: " + numTrades + " | WINS: " + numWins + " (" + (Math.Round((double)numWins / numTrades, 3) * 100) + "%) | LOSSES: " + numLosses + " (" + (Math.Round((double)numLosses / numTrades, 3) * 100) + "%) ********");

                currentTradePnL = 0;
                newTradeCalculated = false;
                barsInTrade = 0;
            }

            if (Position.MarketPosition != MarketPosition.Flat)
            {
                barsInTrade++;
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

            if (realtimPnL > highestDailyPnL)
                highestDailyPnL = realtimPnL;

            currentTrailingDrawdown = realtimPnL - highestDailyPnL;

            // if in a position and the realized day's PnL plus the position PnL is greater than the loss limit then exit the order
            if ((((realtimPnL) <= MaxLoss) || (realtimPnL) >= MaxGain || (EnableTrailingDrawdown && currentTrailingDrawdown < TrailingDrawdownLimit))
                && EnableTrading
                && !DisablePNLLimits
            )
            {
                EnableTrading = false;
                Print(Time[0] + " ******** TRADING DISABLED (mid-trade) ******** : $" + realtimPnL);
            }
            #endregion
            #endregion

            #region Trading Logic/Delta Code
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

            #region Delta Bars to Miss Trade
            // Load in new variables if delta volume is weak
            if (EnableDynamicSettings)
            {
                if (midVolPump[0])
                {
                    if (deltaBuyVol >= DeltaPosCutOff / 100 && deltaBuyVol > deltaSellVol)
                    {
                        if (deltaSellVol >= -1 * DeltaNegCutOff / 100)
                        {
                            localBarsToMissTrade = barsToMissTrade;
                        }
                        else
                        {
                            localBarsToMissTrade = BarsToMissPosDelta;
                        }
                    }
                    else
                    {
                        localBarsToMissTrade = BarsToMissNegDelta;
                    }
                }
                else if (midVolDump[0])
                {
                    if (deltaSellVol >= DeltaPosCutOff / 100 && deltaSellVol > deltaBuyVol)
                    {
                        if (deltaBuyVol >= -1 * DeltaNegCutOff / 100)
                        {
                            localBarsToMissTrade = barsToMissTrade;
                        }
                        else
                        {
                            localBarsToMissTrade = BarsToMissPosDelta;
                        }
                    }
                    else
                    {
                        localBarsToMissTrade = BarsToMissNegDelta;
                    }
                }
                else if (Close[0] < smoothConfirmMA[0] && buyVolSignal)
                {
                    if (deltaBuyVol >= DeltaPosCutOff / 100 && deltaBuyVol > deltaSellVol)
                    {
                        if (deltaSellVol >= -1 * DeltaNegCutOff / 100)
                        {
                            localBarsToMissTrade = barsToMissTrade;
                        }
                        else
                        {
                            localBarsToMissTrade = BarsToMissPosDelta;
                        }
                    }
                    else
                    {
                        localBarsToMissTrade = BarsToMissNegDelta;
                    }
                }
                else if (Close[0] > smoothConfirmMA[0] && sellVolSignal)
                {
                    if (deltaSellVol >= DeltaPosCutOff / 100 && deltaSellVol > deltaBuyVol)
                    {
                        if (deltaBuyVol >= -1 * DeltaNegCutOff / 100)
                        {
                            localBarsToMissTrade = barsToMissTrade;
                        }
                        else
                        {
                            localBarsToMissTrade = BarsToMissPosDelta;
                        }
                    }
                    else
                    {
                        localBarsToMissTrade = BarsToMissNegDelta;
                    }
                }
            }
            else
            {
                localBarsToMissTrade = barsToMissTrade;
            }

            if (localBarsToMissTrade != localBarsToMissPrev && validTriggerPeriod)
            {
                Print(Time[0] + " Bars to Miss Trade Changed from " + localBarsToMissPrev + " to " + localBarsToMissTrade + ". Delta Buy: " + Math.Round(deltaBuyVol, 3) * 100 + "% Delta Sell: " + Math.Round(deltaSellVol, 3) * 100 + "%");
            }
            localBarsToMissPrev = localBarsToMissTrade;
            #endregion

            #region Buy/Sell Signals
            #region Count Chase Bars
            if (buyVolSignal && IsChaseBar(true, deltaBuyVol, deltaSellVol, deltaDiffVol) && Time[0].TimeOfDay > ORBEnd.TimeOfDay)
            {
                chaseBars++;
                Print(Time[0] + " Chase Buy Bar Detected | Bars: " + chaseBars + " | Delta Buy: " + Math.Round(deltaBuyVol, 3) * 100 + "% | Delta Sell: " + Math.Round(deltaSellVol, 3) * 100 + "%" + " | Delta Diff: " + Math.Round(deltaDiffVol, 3) * 100 + "%");
            }
            else if (sellVolSignal && IsChaseBar(false, deltaBuyVol, deltaSellVol, deltaDiffVol) && Time[0].TimeOfDay > ORBEnd.TimeOfDay)
            {
                chaseBars++;
                Print(Time[0] + " Chase Sell Bar Detected | Bars: " + chaseBars + " | Delta Buy: " + Math.Round(deltaBuyVol, 3) * 100 + "% | Delta Sell: " + Math.Round(deltaSellVol, 3) * 100 + "%" + " | Delta Diff: " + Math.Round(deltaDiffVol, 3) * 100 + "%");
            }
            else
            {
                chaseBars = 0;
            }
            #endregion
            #region Trigger Logic
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

                if (buyVolSignal && EnableRestartOnATR && buyATRSignal[0] && !buyTrigger)
                {
                    numATRRestart++;
                    buyTrigger = true;
                    Print(Time[0] + " [ATR Restart]: Long Trade triggered again | Restarts: " + numATRRestart);
                }

                if (buyVolSignal && chaseBars > 0 && chaseBars <= ChaseMaxBars && !buyTrigger)
                {
                    if (EnableChaseModeRestart && EnableChaseMode)
                        buyTrigger = true;
                    numChaseModeRestarts++;
                    Print(Time[0] + (EnableChaseModeRestart ? "" : " [NOT ACTIVE]") + " [Chase Mode - Restarts]: Long Trade Triggered | Restarts: " + numChaseModeRestarts);
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

                if (sellVolSignal && EnableRestartOnATR && sellATRSignal[0] && !sellTrigger)
                {
                    numATRRestart++;
                    Print(Time[0] + " [ATR Restart]: Short Trade triggered again | Restarts: " + numATRRestart);
                    sellTrigger = true;
                }

                if (sellVolSignal && chaseBars > 0 && chaseBars <= ChaseMaxBars && !sellTrigger)
                {
                    if (EnableChaseModeRestart && EnableChaseMode)
                        sellTrigger = true;
                    numChaseModeRestarts++;
                    Print(Time[0] + (EnableChaseModeRestart ? "" : " [NOT ACTIVE]") + " [Chase Mode - Restarts]: Short Trade Triggered | Restarts: " + numChaseModeRestarts);
                }
            }
            #endregion

            #region Delta Brush Updates
            if (EnableDynamicSettings)
                if (buyVolSignal || sellVolSignal)
                {
                    if (localBarsToMissTrade == barsToMissTrade)
                    {
                        if (Math.Abs(deltaBuyVol - deltaSellVol) > 0.08)
                        {
                            BackBrush = ChaseShadeTrend;
                        }
                        else
                        {
                            BackBrush = DeltaVolTrendShade;
                        }
                    }
                    else if (localBarsToMissTrade == BarsToMissPosDelta)
                    {
                        if (Math.Abs(deltaBuyVol - deltaSellVol) > 0.04)
                        {
                            BackBrush = ChaseShadePos;
                        }
                        else if (buyVolSignal)
                        {
                            BackBrush = DeltaVolBuyShade;
                        }
                        else if (sellVolSignal)
                        {
                            BackBrush = DeltaVolSellShade;
                        }
                    }
                    else if (localBarsToMissTrade == BarsToMissNegDelta)
                    {
                        BackBrush = DeltaVolNegShade;
                    }
                }
                else if (chopDetect[0] && !protectiveChopZone)
                {
                    BackBrush = ChopShade;
                }
                else
                {
                    if (deltaBuyVol >= DeltaPosCutOff / 100 && deltaBuyVol > deltaSellVol)
                    {
                        if (deltaSellVol >= -1 * DeltaNegCutOff / 100)
                        {
                            BackBrush = DeltaVolTrendShade;
                        }
                        else
                        {
                            BackBrush = DeltaVolBuyShade;
                        }
                    }
                    else if (deltaSellVol >= DeltaPosCutOff / 100 && deltaSellVol > deltaBuyVol)
                    {
                        if (deltaBuyVol >= -1 * DeltaNegCutOff / 100)
                        {
                            BackBrush = DeltaVolTrendShade;
                        }
                        else
                        {
                            BackBrush = DeltaVolSellShade;
                        }
                    }
                    else
                    {
                        BackBrush = null;
                    }
                }
            #endregion

            #region Trigger Display
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
                SetStopLoss("LongTrim", CalculationMode.Ticks, slLevel / TickSize, false);
                SetProfitTarget("LongTrim", CalculationMode.Ticks, ExitTPLevel / TickSize);
                SetStopLoss("ShortTrim", CalculationMode.Ticks, slLevel / TickSize, false);
                SetProfitTarget("ShortTrim", CalculationMode.Ticks, ExitTPLevel / TickSize);
            }
            else if (Position.MarketPosition == MarketPosition.Long)
            {
                if (EnableDynamicSL)
                    if (High[0] > Position.AveragePrice + ProfitToMoveSL && barsInTrade > 1)
                    {
                        if (oldDynamicSL != Position.AveragePrice - SLNewLevel * TickSize)
                        {
                            Print(Time[0] + " [Dynamic SL]: SL Level Updated to: " + (Position.AveragePrice - SLNewLevel * TickSize));
                            SetStopLoss("Long", CalculationMode.Price, Position.AveragePrice - SLNewLevel * TickSize, false);
                            SetStopLoss("LongTrim", CalculationMode.Price, Position.AveragePrice - SLNewLevel * TickSize, false);
                            oldDynamicSL = Position.AveragePrice - SLNewLevel * TickSize;
                        }
                    }

                if (EnableDynamicTP)
                {
                    double originalTP = Position.AveragePrice + tpLevel;
                    if (TPCalcFromInitTrigger)
                    {
                        if (triggerPrice + tpLevel != currentTradeTP)
                            Print(Time[0] + " [Dynamic Exit - Init Calc]: TP Level Updated from: " + originalTP + " to: " + (triggerPrice + tpLevel));

                        originalTP = triggerPrice + tpLevel;
                    }

                    if (chaseBars > 0 && EnableChaseMode)
                    {
                        if (Position.AveragePrice + ChaseNewTPLevel != currentTradeTP)
                            Print(Time[0] + " [Dynamic Exit - Chase]: TP Level Updated from: " + originalTP + " to: " + (Position.AveragePrice + ChaseNewTPLevel));
                        originalTP = Position.AveragePrice + ChaseNewTPLevel;
                    }

                    double TPNewLevel = UpdateTPLevel(originalTP, true);
                    TPNewLevel = GetLiquidityTPLevel(TPNewLevel, true);
                    if (TPNewLevel > dayHigh && Math.Abs(TPNewLevel - dayHigh) <= TPDayHLSearchRange && EnableTPDayHLPriority && dayHighLevelUsable)
                    {
                        if (dayHigh - TPOffset != currentTradeTP)
                            Print(Time[0] + " [Dynamic Exit - Day High]: TP Level too close to Day High: " + TPNewLevel + " - Adjusting to: " + RoundToNearestTick(dayHigh - TPOffset));

                        TPNewLevel = dayHigh - TPDayHLOffset;
                    }

                    if (TPNewLevel > upperChopZone && Math.Abs(TPNewLevel - upperChopZone) <= TPChopZoneSearchRange && EnableTPChopZone)
                    {
                        if (upperChopZone - TPChopZoneOffset != currentTradeTP)
                            Print(Time[0] + " [Dynamic Exit - Chop Zone]: TP Level too close to Chop Zone: " + TPNewLevel + " - Adjusting to: " + RoundToNearestTick(upperChopZone - TPChopZoneOffset));

                        TPNewLevel = upperChopZone - TPChopZoneOffset;
                    }

                    if (TPNewLevel > vwap[0] && Math.Abs(TPNewLevel - vwap[0]) <= TPVWAPSearchRange && EnableTPVWAP)
                    {
                        if (vwap[0] - TPVWAPOffset != currentTradeTP)
                            Print(Time[0] + " [Dynamic Exit - VWAP]: TP Level too close to VWAP: " + TPNewLevel + " - Adjusting to: " + RoundToNearestTick(vwap[0] - TPVWAPOffset));

                        TPNewLevel = vwap[0] - TPVWAPOffset;
                    }

                    SetProfitTarget("Long", CalculationMode.Price, TPNewLevel);
                    currentTradeTP = TPNewLevel;
                }

                if (EnableDynamicTrim)
                {
                    double trimLevel = UpdateTrimLevel(Position.AveragePrice + ExitTPLevel, true);
                    trimLevel = GetLiquidityTrimLevel(trimLevel, true);
                    SetProfitTarget("LongTrim", CalculationMode.Price, trimLevel);
                }
            }
            else if (Position.MarketPosition == MarketPosition.Short)
            {
                if (EnableDynamicSL)
                    if (Low[0] < Position.AveragePrice - ProfitToMoveSL && barsInTrade > 1)
                    {
                        if (oldDynamicSL != Position.AveragePrice + SLNewLevel * TickSize)
                        {
                            Print(Time[0] + " [Dynamic SL]: SL Level Updated to: " + (Position.AveragePrice + SLNewLevel * TickSize));
                            SetStopLoss("Short", CalculationMode.Price, Position.AveragePrice + SLNewLevel * TickSize, false);
                            SetStopLoss("ShortTrim", CalculationMode.Price, Position.AveragePrice + SLNewLevel * TickSize, false);
                            oldDynamicSL = Position.AveragePrice + SLNewLevel * TickSize;
                        }
                    }

                if (EnableDynamicTP)
                {
                    double originalTP = Position.AveragePrice - tpLevel;
                    if (TPCalcFromInitTrigger)
                    {
                        if (triggerPrice - tpLevel != currentTradeTP)
                            Print(Time[0] + " [Dynamic Exit - TP Init]: TP Level Updated from: " + originalTP + " to: " + (triggerPrice - tpLevel));
                        originalTP = triggerPrice - tpLevel;
                    }

                    if (chaseBars > 0 && EnableChaseMode)
                    {
                        if (Position.AveragePrice - ChaseNewTPLevel != currentTradeTP)
                            Print(Time[0] + " [Dynamic Exit - Chase]: TP Level Updated from: " + originalTP + " to: " + (Position.AveragePrice - ChaseNewTPLevel));
                        originalTP = Position.AveragePrice - ChaseNewTPLevel;
                    }

                    double TPNewLevel = UpdateTPLevel(originalTP, false);
                    TPNewLevel = GetLiquidityTPLevel(TPNewLevel, false);
                    if (TPNewLevel < dayLow && Math.Abs(TPNewLevel - dayLow) <= TPDayHLSearchRange && EnableTPDayHLPriority && dayLowLevelUsable)
                    {
                        if (dayLow + TPOffset != currentTradeTP)
                            Print(Time[0] + " [Dynamic Exit - Day Low]: TP Level too close to Day Low: " + TPNewLevel + " - Adjusting to: " + RoundToNearestTick(dayLow + TPOffset));

                        TPNewLevel = dayLow + TPDayHLOffset;
                    }

                    if (TPNewLevel < lowerChopZone && Math.Abs(TPNewLevel - lowerChopZone) <= TPChopZoneSearchRange && EnableTPChopZone)
                    {
                        if (lowerChopZone + TPChopZoneOffset != currentTradeTP)
                            Print(Time[0] + " [Dynamic Exit - Chop Zone]: TP Level too close to Chop Zone: " + TPNewLevel + " - Adjusting to: " + (lowerChopZone + TPChopZoneOffset));

                        TPNewLevel = lowerChopZone + TPChopZoneOffset;
                    }

                    if (TPNewLevel < vwap[0] && Math.Abs(TPNewLevel - vwap[0]) <= TPVWAPSearchRange && EnableTPVWAP)
                    {
                        if (vwap[0] + TPVWAPOffset != currentTradeTP)
                            Print(Time[0] + " [Dynamic Exit - VWAP]: TP Level too close to VWAP: " + TPNewLevel + " - Adjusting to: " + (vwap[0] + TPVWAPOffset));

                        TPNewLevel = vwap[0] + TPVWAPOffset;
                    }

                    SetProfitTarget("Short", CalculationMode.Price, TPNewLevel);
                    currentTradeTP = TPNewLevel;
                }

                if (EnableDynamicTrim)
                {
                    double trimLevel = UpdateTrimLevel(Position.AveragePrice - ExitTPLevel, false);
                    trimLevel = GetLiquidityTrimLevel(trimLevel, false);
                    SetProfitTarget("ShortTrim", CalculationMode.Price, trimLevel);
                }
            }
            #endregion

            #region Close Trades that are too far away from entry
            if (entryOrder != null && entryOrder.OrderState == OrderState.Working && (chaseBars == 0 || !EnableChaseMode))
            {
                double priceToCheck = smoothConfirmMA[0] + offsetFromEntryToCancel;
                bool barTooFarFromEntry = High[0] > priceToCheck;

                if (((CurrentBar >= entryBar + barsToHoldTrade) || buyVolCloseTrigger || barTooFarFromEntry))
                {
                    Print(Time[0] + " Long Order cancelled: " + Close[0] + (barTooFarFromEntry ? " - Bar too far from entry" : ""));
                    CancelOrder(entryOrder);
                    entryOrder = null; // Reset the entry order variable
                    if (entryOrderTrim != null && entryOrderTrim.OrderState == OrderState.Working)
                    {
                        CancelOrder(entryOrderTrim);
                        entryOrderTrim = null;
                    }
                }
            }

            if (entryOrderShort != null && entryOrderShort.OrderState == OrderState.Working && (chaseBars == 0 || !EnableChaseMode))
            {
                double priceToCheck = smoothConfirmMA[0] - offsetFromEntryToCancel;
                bool barTooFarFromEntry = Low[0] < priceToCheck;

                if (((CurrentBar >= entryBarShort + barsToHoldTrade) || sellVolCloseTrigger || barTooFarFromEntry))
                {
                    Print(Time[0] + " Short Order cancelled: " + Close[0] + (barTooFarFromEntry ? " - Bar too far from entry" : ""));
                    CancelOrder(entryOrderShort);
                    entryOrderShort = null; // Reset the entry order variable
                    if (entryOrderTrimShort != null && entryOrderTrimShort.OrderState == OrderState.Working)
                    {
                        CancelOrder(entryOrderTrimShort);
                        entryOrderTrimShort = null;
                    }
                }
            }
            #endregion

            #region Close/Trim Trades
            if (buyVolCloseTrigger)
            {
                if (entryOrder != null && entryOrder.OrderState == OrderState.Filled)
                {
                    Print(Time[0] + " Long Order Closed: " + Close[0]);
                }
                if (Position.MarketPosition == MarketPosition.Long)
                {
                    ExitLong("Long");
                    if (EnableDynamicTrim)
                        ExitLong("LongTrim");
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
                    if (EnableDynamicTrim)
                        ExitShort("ShortTrim");
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
                    if (EnableDynamicTrim)
                        ExitLong("LongTrim");
                }
            }
            else if (Position.MarketPosition == MarketPosition.Short)
            {
                if ((Close[0] < triggerPrice - tpLevel) && TPCalcFromInitTrigger)
                {
                    ExitShort("Short");
                    if (EnableDynamicTrim)
                        ExitShort("ShortTrim");
                }
            }
            #endregion

            #region Buy/Sell Orders
            int mainTradeQuantity = TradeQuantity;
            int trimTradeQuantity = (int)Math.Round(TradeQuantity * TrimPercent / 100, 0);
            if (EnableDynamicTrim)
                mainTradeQuantity = TradeQuantity - trimTradeQuantity;

            if (!buyVolCloseTrigger && !sellVolCloseTrigger)
            {
                if (Position.MarketPosition == MarketPosition.Flat)
                    if (buyTrigger || reverseBuyTrade)
                    {
                        double dynamicOffset = GetDynamicEntryOffset(true, deltaBuyVol, deltaSellVol);
                        double limitLevel = GetLimitLevel(
                            smoothConfirmMA[0] + buySellBuffer + dynamicOffset,
                            Close[0],
                            true
                        );

                        if (IsEntrySafe(smoothConfirmMA[0] + buySellBuffer, true)
                            && IsEntryWithinDynamicRange(smoothConfirmMA[0] + buySellBuffer, true)
                            && IsEntrySafeBounceProtect(smoothConfirmMA[0] + buySellBuffer, true)
                            && IsEntrySafeATRProtect(true))
                        {
                            limitLevel = GetLiquidityFillLevel(limitLevel);
                            limitLevel = GetChasePrice(limitLevel, true, chaseBars);
                            Print(Time[0] + " Long triggered: " + limitLevel);
                            entryOrder = EnterLongLimit(0, true, mainTradeQuantity, limitLevel, "Long");
                            if (EnableDynamicTrim)
                                entryOrderTrim = EnterLongLimit(0, true, trimTradeQuantity, limitLevel, "LongTrim");
                            entryBar = CurrentBar; // Remember the bar at which we entered
                            entryPrice = limitLevel; // Assuming immediate execution at the close price
                            triggerPrice = limitLevel;
                            limitLevelPrev = limitLevel;
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

                        if (IsEntrySafe(smoothConfirmMA[0] + buySellBuffer, true) && IsEntrySafeBounceProtect(smoothConfirmMA[0] + buySellBuffer, true))
                        {
                            limitLevel = GetLiquidityFillLevel(limitLevel);
                            limitLevel = GetChasePrice(limitLevel, true, chaseBars);
                            ChangeOrder(entryOrder, entryOrder.Quantity, limitLevel, 0);
                            if (entryOrderTrim != null && entryOrderTrim.OrderState != OrderState.Filled)
                                ChangeOrder(entryOrderTrim, entryOrderTrim.Quantity, limitLevel, 0);
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
                            Print(Time[0] + " Long updated: " + limitLevel + " | Bars Held: " + (CurrentBar - entryBar).ToString() + " | Previous Low: " + Low[0] + " (" + (Low[0] - limitLevelPrev) + ") | Delta Buy: " + Math.Round(deltaBuyVol, 3) * 100 + "% | Delta Sell: " + Math.Round(deltaSellVol, 3) * 100 + "%");
                            limitLevelPrev = limitLevel;
                        }
                        else
                        {
                            Print(Time[0] + " [Protective Trades]: Long Order Cancelled due to protective trades at: " + Close[0]);
                            CancelOrder(entryOrder);
                            entryOrder = null; // Reset the entry order variable
                            if (entryOrderTrim != null)
                            {
                                CancelOrder(entryOrderTrim);
                                entryOrderTrim = null;
                            }
                        }
                    }
            }

            if (!sellVolCloseTrigger && !buyVolCloseTrigger)
            {
                if (Position.MarketPosition == MarketPosition.Flat)
                    if ((sellTrigger || reverseSellTrade))
                    {
                        double dynamicOffset = GetDynamicEntryOffset(false, deltaBuyVol, deltaSellVol);
                        double limitLevel = GetLimitLevel(
                            smoothConfirmMA[0] - buySellBuffer - dynamicOffset,
                            Close[0],
                            false
                        );

                        if (IsEntrySafe(smoothConfirmMA[0] - buySellBuffer, false)
                            && IsEntryWithinDynamicRange(smoothConfirmMA[0] - buySellBuffer, false)
                            && IsEntrySafeBounceProtect(smoothConfirmMA[0] - buySellBuffer, false)
                            && IsEntrySafeATRProtect(false))
                        {
                            limitLevel = GetLiquidityFillLevel(limitLevel);
                            limitLevel = GetChasePrice(limitLevel, false, chaseBars);
                            Print(Time[0] + " Short triggered: " + limitLevel);
                            entryOrderShort = EnterShortLimit(0, true, mainTradeQuantity, limitLevel, "Short");
                            if (EnableDynamicTrim)
                                entryOrderTrimShort = EnterShortLimit(0, true, trimTradeQuantity, limitLevel, "ShortTrim");
                            entryBarShort = CurrentBar; // Remember the bar at which we entered
                            entryPriceShort = limitLevel; // Assuming immediate execution at the close price
                            triggerPrice = limitLevel;
                            limitLevelPrev = limitLevel;
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

                        if (IsEntrySafe(smoothConfirmMA[0] - buySellBuffer, false) && IsEntrySafeBounceProtect(smoothConfirmMA[0] - buySellBuffer, false))
                        {
                            limitLevel = GetLiquidityFillLevel(limitLevel);
                            limitLevel = GetChasePrice(limitLevel, false, chaseBars);
                            ChangeOrder(entryOrderShort, entryOrderShort.Quantity, limitLevel, 0);
                            if (entryOrderTrimShort != null && entryOrderTrimShort.OrderState != OrderState.Filled)
                                ChangeOrder(entryOrderTrimShort, entryOrderTrimShort.Quantity, limitLevel, 0);
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
                            Print(Time[0] + " Short updated: " + limitLevel + " | Bars Held: " + (CurrentBar - entryBarShort).ToString() + " | Previous High: " + High[0] + " (" + (limitLevelPrev - High[0]) + ") | Delta Buy: " + Math.Round(deltaBuyVol, 3) * 100 + "% | Delta Sell: " + Math.Round(deltaSellVol, 3) * 100 + "%");
                            limitLevelPrev = limitLevel;
                        }
                        else
                        {
                            Print(Time[0] + " [Protective Trades]: Short Order Cancelled due to protective trades at: " + Close[0]);
                            CancelOrder(entryOrderShort);
                            entryOrderShort = null; // Reset the entry order variable
                            if (entryOrderTrimShort != null)
                            {
                                CancelOrder(entryOrderTrimShort);
                                entryOrderTrimShort = null;
                            }
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
                + " | TDD: $" + currentTrailingDrawdown
                + " | Trading: "
                + (EnableTrading || DisablePNLLimits ? "Active" : "Off")
                + "\nConsec: " + consecutiveLosses + " of " + maxLossConsec;

            if (Position.MarketPosition != MarketPosition.Flat)
            {
                dashBoard += " | Bars Missed: " + barsMissed + " of " + localBarsToMissTrade;
                dashBoard += " | Bars Held: " + barsInTrade;
            }
            else
            {
                if (entryOrder != null && entryOrder.OrderState == OrderState.Working)
                {
                    string barHeld = "0";
                    if (entryOrder.OrderState == OrderState.Working)
                        barHeld = (CurrentBar - entryBar).ToString();
                    dashBoard += " | Bars Held: " + barHeld + " of " + barsToHoldTrade;
                }
                else if (entryOrderShort != null && entryOrderShort.OrderState == OrderState.Working)
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

            if (chaseBars > 0)
                tradeStatus += ((tradeStatus != "" ? " | " : "") + "Chase: " + chaseBars);

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

            if (entryOrderTrim != null && entryOrderTrim.IsBacktestOrder && State == State.Realtime)
                entryOrderTrim = GetRealtimeOrder(entryOrderTrim);

            if (entryOrderShort != null && entryOrderShort.IsBacktestOrder && State == State.Realtime)
                entryOrderShort = GetRealtimeOrder(entryOrderShort);

            if (entryOrderTrimShort != null && entryOrderTrimShort.IsBacktestOrder && State == State.Realtime)
                entryOrderTrimShort = GetRealtimeOrder(entryOrderTrimShort);

            if (order.Name == "Long")
            {
                if (orderState == OrderState.Filled)
                {
                    Print(Time[0] + " LONG FILLED: " + averageFillPrice + " | Bar Low: " + Low[0] + "(" + (averageFillPrice - Low[0]) + ") | Vol Trade Length: " + volTradeLength);
                }
            }

            if (order.Name == "Short")
            {
                if (orderState == OrderState.Filled)
                {
                    Print(Time[0] + " SHORT FILLED: " + averageFillPrice + " | Bar High: " + High[0] + "(" + (High[0] - averageFillPrice) + ") | Vol Trade Length: " + volTradeLength);
                }
            }

            if (orderState == OrderState.Rejected || orderState == OrderState.Cancelled)
            {
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
            #region Trade Closed
            if (
                execution.Order.OrderState == OrderState.Filled
                && (
                    execution.Order.Name.Contains("Stop loss")
                    || execution.Order.Name.Contains("Profit target")
                    || execution.Order.Name.Contains("to cover")
                )
            )
            {
                newTradeExecuted = true;
                tradeExecPrice = price;
                tradeExecuteType = execution.Order.Name;
            }
            #endregion

            #region PnL Calculation
            if (SystemPerformance.AllTrades.Count > 0)
            {
                if (RealTimePnlOnly && State == State.Realtime || !RealTimePnlOnly)
                {
                    Cbi.Trade lastTrade = SystemPerformance.AllTrades[
                        SystemPerformance.AllTrades.Count - 1
                    ];

                    // Sum the profits of trades with similar exit times
                    double execTradePnL = lastTrade.ProfitCurrency;
                    int execQty = lastTrade.Quantity;
                    DateTime exitTime = lastTrade.Exit.Time;
                    if (lastTrade.TradeNumber > lastTradeChecked)
                    {
                        for (int i = SystemPerformance.AllTrades.Count - 2; i >= 0; i--)
                        {
                            Cbi.Trade trade = SystemPerformance.AllTrades[i];
                            if (Math.Abs((trade.Exit.Time - exitTime).TotalSeconds) <= 10 && trade.TradeNumber > lastTradeChecked)
                            {
                                execTradePnL += trade.ProfitCurrency;
                                execQty += trade.Quantity;
                            }
                            else
                            {
                                break; // Exit the loop if the exit time is different
                            }
                        }
                        lastTradeChecked = lastTrade.TradeNumber;
                        currentPnL += execTradePnL;
                        currentTradePnL += execTradePnL;
                        partialTradePnL += execTradePnL;
                        partialTradeQty += execQty;
                        newTradeCalculated = true;
                        partialTradeCalculated = true;

                        if (currentTradePnL >= 0 && consecutiveLosses > 0)
                        {
                            consecutiveLosses = 0; // Reset the count on a non-loss trade
                            Print(Time[0] + " ******** CONSECUTIVE LOSSES RESET ********");
                        }
                    }
                }
            }
            #endregion
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
        #region Levels Mgmt/Updates
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
                if (oldDynamicTP != newTPLevel)
                {
                    Print(Time[0] + " [Dynamic TP]: TP Level Updated to: " + newTPLevel + " from previous TP of: " + targetTP);
                    oldDynamicTP = newTPLevel;
                }
                return newTPLevel;
            }
            else
            {
                return targetTP;
            }
        }

        private double UpdateTrimLevel(double targetTP, bool isBuy)
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

                if (difference <= ExitLevelRange)
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
                    newTPLevel = RoundToNearestTick(closestLevel.Value - ExitLevelOffset);
                }
                else
                {
                    newTPLevel = RoundToNearestTick(closestLevel.Value + ExitLevelOffset);
                }
                if (oldTrimTP != newTPLevel)
                {
                    Print(Time[0] + " [Dynamic Trim]: Trim Level Updated to: " + newTPLevel + " from previous TP of: " + targetTP);
                    oldTrimTP = newTPLevel;
                }
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
        #endregion

        #region Liquidity Levels
        private double GetLiquidityFillLevel(double entryPrice)
        {
            if (!EnableLiquidityFills)
                return entryPrice;

            // Extract the last two digits of the entry price
            double entryPriceEnd = entryPrice % 100;

            // Initialize the closest level and the smallest difference
            double closestLevel = entryPriceEnd;
            double minDifference = double.MaxValue;

            foreach (double level in LiquidityLevels)
            {
                // Calculate the difference between the entry price ending and the liquidity level
                double difference = Math.Abs(entryPriceEnd - level);

                // Check if the liquidity level is within the search range
                if (difference <= LiquidityFillSearchRange)
                {
                    // Update the closest level if the current difference is smaller
                    if (difference < minDifference)
                    {
                        minDifference = difference;
                        closestLevel = level;
                    }
                }
            }

            // If a closest level was found within the search range, update the entry price
            if (minDifference != double.MaxValue)
            {
                Print(Time[0] + " [Liquidity Fill]: Entry Price Updated to: " + (entryPrice - entryPriceEnd + closestLevel) + " from previous entry price: " + entryPrice);
                return entryPrice - entryPriceEnd + closestLevel;
            }
            else
            {
                // If no level was found within the search range, return the original entry price
                return entryPrice;
            }
        }

        private double GetLiquidityTPLevel(double tpLevel, bool buyDir)
        {
            if (!EnableLiquidityTP)
                return tpLevel;

            // Extract the last two digits of the entry price
            double priceEnd = tpLevel % 100;

            // Initialize the closest level and the smallest difference
            double closestLevel = priceEnd;
            double minDifference = double.MaxValue;

            foreach (double level in LiquidityLevels)
            {
                if (buyDir)
                {
                    if ((level >= priceEnd && level <= priceEnd + LiquidityTPUpperRange) ||
                        (level <= priceEnd && level >= priceEnd - LiquidityTPLowerRange))
                    {
                        double difference = Math.Abs(priceEnd - level);
                        {
                            // Update the closest level if the current difference is smaller
                            if (difference < minDifference)
                            {
                                minDifference = difference;
                                closestLevel = level;
                            }
                        }
                    }
                }
                else
                {
                    if ((level >= priceEnd && level <= priceEnd + LiquidityTPLowerRange) ||
                                               (level <= priceEnd && level >= priceEnd - LiquidityTPUpperRange))
                    {
                        double difference = Math.Abs(priceEnd - level);
                        {
                            // Update the closest level if the current difference is smaller
                            if (difference < minDifference)
                            {
                                minDifference = difference;
                                closestLevel = level;
                            }
                        }
                    }
                }
            }

            if (minDifference != double.MaxValue)
            {
                double newTPLevel = tpLevel - priceEnd + closestLevel;
                if (buyDir)
                {
                    newTPLevel -= LiquidityTPOffset;
                }
                else
                {
                    newTPLevel += LiquidityTPOffset;
                }

                if (oldLiquidityTP != newTPLevel)
                {
                    Print(Time[0] + " [Liquidity TP]: TP Level Updated to: " + (newTPLevel) + " from previous TP price: " + tpLevel);
                    oldLiquidityTP = newTPLevel;
                }
                return newTPLevel;
            }
            else
            {
                return tpLevel;
            }
        }

        private double GetLiquidityTrimLevel(double tpLevel, bool buyDir)
        {
            if (!EnableLiquidityTP)
                return tpLevel;

            // Extract the last two digits of the entry price
            double priceEnd = tpLevel % 100;

            // Initialize the closest level and the smallest difference
            double closestLevel = priceEnd;
            double minDifference = double.MaxValue;

            foreach (double level in LiquidityLevels)
            {
                // Calculate the difference between the entry price ending and the liquidity level
                double difference = Math.Abs(priceEnd - level);

                // Check if the liquidity level is within the search range
                if (difference <= LiquidityTrimRange)
                {
                    // Update the closest level if the current difference is smaller
                    if (difference < minDifference)
                    {
                        minDifference = difference;
                        closestLevel = level;
                    }
                }
            }

            if (minDifference != double.MaxValue)
            {
                double newTPLevel = tpLevel - priceEnd + closestLevel;
                if (buyDir)
                {
                    newTPLevel -= LiquidityTPOffset;
                }
                else
                {
                    newTPLevel += LiquidityTPOffset;
                }

                if (oldLiquidityTrim != newTPLevel)
                {
                    Print(Time[0] + " [Liquidity TP]: Trim Level Updated to: " + (newTPLevel) + " from trim price: " + tpLevel);
                    oldLiquidityTrim = newTPLevel;
                }
                return newTPLevel;
            }
            else
            {
                return tpLevel;
            }
        }
        #endregion

        #region Entry Protection
        private bool IsEntrySafe(double entryPrice, bool isBuy)
        {
            if (!EnableProtectiveLevelTrades)
                return true;

            if (isBuy)
            {
                foreach (double level in ProtectiveBuyLevels)
                {
                    if (entryPrice >= level - ProtectiveLevelRangeCheck && entryPrice <= level)
                    {
                        numBlockedProtective++;
                        Print(Time[0] + " [Protective Trades]: Long not safe at: " + RoundToNearestTick(entryPrice) + " due to level: " + RoundToNearestTick(level) + " | Blocked: " + numBlockedProtective);
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
                        numBlockedProtective++;
                        Print(Time[0] + " [Protective Trades]: Short not safe at: " + RoundToNearestTick(entryPrice) + " due to level: " + RoundToNearestTick(level) + " | Blocked: " + numBlockedProtective);
                        return false;
                    }
                }
            }

            if (EnableVWAPBlock)
            {
                if (Math.Abs(entryPrice - vwap[0]) < ProtectiveLevelRangeCheck)
                {
                    numBlockedProtective++;
                    Print(Time[0] + " [Protective Trades]: Trade not safe at: " + RoundToNearestTick(entryPrice) + " due to VWAP protect: " + RoundToNearestTick(vwap[0]) + " | Blocked: " + numBlockedProtective);
                    return false;
                }
            }

            if (EnableChopZoneBlock)
            {
                if (Math.Abs(entryPrice - upperChopZone) < ProtectiveLevelRangeCheck)
                {
                    numBlockedProtective++;
                    Print(Time[0] + " [Protective Trades]: Trade not safe at: " + RoundToNearestTick(entryPrice) + " due to Chop Zone protect: " + RoundToNearestTick(upperChopZone) + " | Blocked: " + numBlockedProtective);
                    return false;
                }

                if (Math.Abs(entryPrice - lowerChopZone) < ProtectiveLevelRangeCheck)
                {
                    numBlockedProtective++;
                    Print(Time[0] + " [Protective Trades]: Trade not safe at: " + RoundToNearestTick(entryPrice) + " due to Chop Zone protect: " + RoundToNearestTick(lowerChopZone) + " | Blocked: " + numBlockedProtective);
                    return false;
                }
            }

            return true;
        }

        private bool IsEntryWithinDynamicRange(double entryPrice, bool isBuy)
        {
            double lowestLow = double.MaxValue;
            double highestHigh = double.MinValue;

            for (int i = 1; i <= DynamicRangeLookback; i++)
            {
                if (Low[i] < lowestLow)
                    lowestLow = Low[i];

                if (High[i] > highestHigh)
                    highestHigh = High[i];
            }

            double range = highestHigh - lowestLow;
            double position = Math.Round(((entryPrice - lowestLow) / range) * 100, 0);

            if (range <= DynamicRangeMinRange)
                return true;

            if (isBuy)
            {
                if ((100 - position) > DynamicRangePosition)
                {
                    numBlockedDynamicRange++;
                    Print(Time[0] + (EnableDynamicRangeProtect ? "" : " [NOT ACTIVE]") + " [Dynamic Range Protect]: Long not safe at: " + RoundToNearestTick(entryPrice) + ". Position " + (100 - position) + " %" + " | Blocked: " + numBlockedDynamicRange);
                    if (!EnableDynamicRangeProtect)
                        return true;
                    return false;
                }
            }
            else
            {
                if (position > DynamicRangePosition)
                {
                    numBlockedDynamicRange++;
                    Print(Time[0] + (EnableDynamicRangeProtect ? "" : " [NOT ACTIVE]") + " [Dynamic Range Protect]: Short not safe at: " + RoundToNearestTick(entryPrice) + ". Position: " + position + "%" + " | Blocked: " + numBlockedDynamicRange);
                    if (!EnableDynamicRangeProtect)
                        return true;
                    return false;
                }
            }
            Print(Time[0] + (isBuy ? " Long" : " Short") + " Dynamic Trade Position: " + position + "% Range: " + range);
            return true;
        }

        private bool IsEntrySafeATRProtect(bool isBuy)
        {
            for (int i = 1; i <= ATRProtectLookback; i++)
            {
                if (isBuy)
                {
                    if (sellATRSignal[i])
                    {
                        numATRProtect++;
                        Print(Time[0] + (EnableATRProtect ? "" : " [NOT ACTIVE]") + " [ATR Protect]: Long not safe at: " + RoundToNearestTick(entryPrice) + " due to ATR signal" + " | Blocked: " + numATRProtect);
                        if (!EnableATRProtect)
                            return true;
                        return false;
                    }
                }
                else
                {
                    if (buyATRSignal[i])
                    {
                        numATRProtect++;
                        Print(Time[0] + (EnableATRProtect ? "" : " [NOT ACTIVE]") + " [ATR Protect]: Short not safe at: " + RoundToNearestTick(entryPrice) + " due to ATR signal" + " | Blocked: " + numATRProtect);
                        if (!EnableATRProtect)
                            return true;
                        return false;
                    }
                }
            }
            return true;
        }

        private bool IsEntrySafeBounceProtect(double entryPrice, bool isBuy)
        {
            for (int i = 1; i <= BounceLookback; i++)
            {
                if (isBuy && BounceOffHighLevel(entryPrice, i))
                {
                    numBlockedBounce++;
                    Print(Time[0] + (EnableBounceProtect ? "" : " [NOT ACTIVE]") + " [Bounce Protect]: Long not safe at: " + RoundToNearestTick(entryPrice) + " due to bounce off high level" + " | Blocked: " + numBlockedBounce);
                    if (!EnableBounceProtect)
                        return true;
                    return false;
                }
                else if (!isBuy && BounceOffLowLevel(entryPrice, i))
                {
                    numBlockedBounce++;
                    Print(Time[0] + (EnableBounceProtect ? "" : " [NOT ACTIVE]") + " [Bounce Protect]: Short not safe at: " + RoundToNearestTick(entryPrice) + " due to bounce off low level" + " | Blocked: " + numBlockedBounce);
                    if (!EnableBounceProtect)
                        return true;
                    return false;
                }
            }
            return true;
        }

        private bool BounceOffHighLevel(double entryLevel, int barNumber)
        {
            double symbolOffset = 5;
            foreach (double level in BounceHighLevels)
            {
                if (High[barNumber] > level - BounceOffset && Open[barNumber] < level && Close[barNumber] < level && Math.Abs(entryLevel - level) < BounceCheckRange && Close[barNumber] > entryLevel + BounceOffset)
                {
                    Draw.TriangleDown(this, "BounceProtect" + CurrentBar, true, barNumber, level + symbolOffset, Brushes.MediumVioletRed);
                    return true;
                }
            }
            return false;
        }

        private bool BounceOffLowLevel(double entryLevel, int barNumber)
        {
            double symbolOffset = 5;
            foreach (double level in BounceLowLevels)
            {
                if (Low[barNumber] < level + BounceOffset && Open[barNumber] > level && Close[barNumber] > level && Math.Abs(entryLevel - level) < BounceCheckRange && Close[barNumber] < entryLevel - BounceOffset)
                {
                    Draw.TriangleUp(this, "BounceProtect" + CurrentBar, true, barNumber, level - symbolOffset, Brushes.MediumSeaGreen);
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region Entry Functions
        private double GetDynamicEntryOffset(bool buyDir, double deltaBuyVol, double deltaSellVol)
        {
            if (EnableDynamicEntry)
            {
                if (buyDir)
                {
                    if (deltaBuyVol >= DeltaPosCutOff / 100)
                    {
                        if (deltaSellVol >= -1 * DeltaNegCutOff / 100 && !IsORBSession())
                        {
                            Print(Time[0] + " [Dynamic Entry]: Price Offset by: " + DynamicEntryOffsetTrend + " for Dynamic Delta Trend");
                            return DynamicEntryOffsetTrend;
                        }
                        else
                        {
                            Print(Time[0] + " [Dynamic Entry]: Price Offset by: " + DynamicEntryOffsetPos + " for Dynamic Delta Positive");
                            return DynamicEntryOffsetPos;
                        }
                    }
                    else
                    {
                        Print(Time[0] + " [Dynamic Entry]: Price Offset by: " + DynamicEntryOffsetNeg + " for Dynamic Delta Negative");
                        return DynamicEntryOffsetNeg;
                    }
                }
                else
                {
                    if (deltaSellVol >= DeltaPosCutOff / 100)
                    {
                        if (deltaBuyVol >= -1 * DeltaNegCutOff / 100 && !IsORBSession())
                        {
                            Print(Time[0] + " [Dynamic Entry]: Price Offset by: " + DynamicEntryOffsetTrend + " for Dynamic Delta Trend");
                            return DynamicEntryOffsetTrend;

                        }
                        else
                        {
                            Print(Time[0] + " [Dynamic Entry]: Price Offset by: " + DynamicEntryOffsetPos + " for Dynamic Delta Positive");
                            return DynamicEntryOffsetPos;
                        }
                    }
                    else
                    {
                        Print(Time[0] + " [Dynamic Entry]: Price Offset by: " + DynamicEntryOffsetNeg + " for Dynamic Delta Negative");
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
        #endregion

        #region Chase Mode
        private double GetChasePrice(double entryPrice, bool isBuy, int chaseBars)
        {
            if (chaseBars == 0)
            {
                chaseModePrev = false;
                return entryPrice;
            }

            if (chaseBars <= ChaseMaxBars)
            {
                if (isBuy)
                {
                    double chasePrice = (Close[0] * 2 + High[0] + Low[0]) / 4;
                    if (!chaseModePrev)
                        numChaseModeTrades++;
                    Print(Time[0] + (EnableChaseMode ? "" : " [NOT ACTIVE]") + " [Chase Mode]: Chase Price Set to " + chasePrice + " from previous price: " + entryPrice + " | Chase Mode Trades: " + numChaseModeTrades);
                    chaseModePrev = true;
                    if (!EnableChaseMode)
                        return entryPrice;
                    return chasePrice;
                }
                else
                {
                    double chasePrice = (Close[0] * 2 + High[0] + Low[0]) / 4;
                    if (!chaseModePrev)
                        numChaseModeTrades++;
                    Print(Time[0] + (EnableChaseMode ? "" : " [NOT ACTIVE]") + " [Chase Mode]: Chase Price Set to " + chasePrice + " from previous price: " + entryPrice + " | Chase Mode Trades: " + numChaseModeTrades);
                    chaseModePrev = true;
                    if (!EnableChaseMode)
                        return entryPrice;
                    return chasePrice;
                }
            }
            else
            {
                chaseModePrev = false;
                return entryPrice;
            }
        }

        private bool IsChaseBar(bool isBuy, double deltaBuyVol, double deltaSellVol, double deltaDiffVol)
        {
            if (isBuy)
            {
                if (deltaBuyVol > deltaSellVol && deltaDiffVol > ChaseDeltaMinDiff / 100)
                {
                    if ((deltaBuyVol >= DeltaPosCutOff / 100 && deltaSellVol < -DeltaNegCutOff / 100) || deltaDiffVol > ChaseDeltaBigDiff / 100)
                        return true;
                }

            }
            else
            {
                if (deltaSellVol > deltaBuyVol && deltaDiffVol > ChaseDeltaMinDiff / 100)
                {
                    if ((deltaSellVol >= DeltaPosCutOff / 100 && deltaBuyVol < -DeltaNegCutOff / 100) || deltaDiffVol > ChaseDeltaBigDiff / 100)
                        return true;
                }
            }
            return false;
        }
        #endregion

        #region Time Session Functions
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
            TimeSpan endOfDay = new TimeSpan(16, 15, 00);
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
                        Print(Time[0] + " ******** TRADING SESSION 1 (Main) ******** ");
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
                        Print(Time[0] + " ******** TRADING SESSION 2 (Market Open) ******** ");
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
                Print(Time[0] + " ******** TRADING SESSION 1 (Main) ENDED ********");
                Draw.VerticalLine(this, "Session1End", 0, Brushes.Orange, DashStyleHelper.Dot, 2);
            }
            if (EnableTradingTS2 && barTime == TS2End.TimeOfDay)
            {
                Print(Time[0] + " ******** TRADING SESSION 2 (Market Open) ENDED ********");
                Draw.VerticalLine(this, "Session2End", 0, Brushes.Orange, DashStyleHelper.Dot, 2);
            }
            if (EnableTradingTS3 && barTime == TS3End.TimeOfDay)
            {
                Print(Time[0] + " ******** TRADING SESSION 3 ENDED ********");
                Draw.VerticalLine(this, "Session3End", 0, Brushes.Orange, DashStyleHelper.Dot, 2);
            }

            if (barTime == endOfDay)
            {
                Print(Time[0] + " ******** END OF DAY STATS ********");
                Print(Time[0] + " TOTAL TRADES: " + numTrades + " | WINS: " + numWins + " (" + (Math.Round((double)numWins / numTrades, 3) * 100) + "%) | LOSSES: " + numLosses + " (" + (Math.Round((double)numLosses / numTrades, 3) * 100) + "%) ********");
                Print(Time[0] + " TOTAL PNL: $" + Math.Round(currentPnL, 2) + " | Trailing Drawdown: $" + currentTrailingDrawdown + " ********");
                Print(Time[0] + " BLOCKED TRADES: Protective: " + numBlockedProtective + " | Dynamic Range: " + numBlockedDynamicRange + " | Bounce Protect: " + numBlockedBounce + " | ATR Protect: " + numATRProtect + " ********");
                Print(Time[0] + " ATR Restarts: " + numATRRestart + " | Chase Mode Trades: " + numChaseModeTrades + " | Chase Mode Restarts: " + numChaseModeRestarts + " ********");
            }
        }
        #endregion
        #endregion
    }
}
