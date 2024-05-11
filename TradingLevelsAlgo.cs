#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Windows.Documents;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.CQG.ProtoBuf;
using NinjaTrader.Gui;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
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
    // TODO: Change to process on tick and have trading on first tick
    // TODO: Look at split TP
	// TODO: Consider TP to be from initial entry level
	// TODO: Close trade if over x in so many candles/volatile move?
    // FEATURE: Add a check to see if we are in a chopzone and if so , disable trading
    // FEATURE: Create standalone volume indicator
    // FEATURE: Create chop indicator with trend chop detection and momentum and delta momentum
    // FEATURE: Add timeout after two bad trades in succession
    // FEATURE: Add pre calculated levels
    // FEATURE: Add code to close trade on pre calculated level
    public class TradingLevelsAlgo : Strategy
    {
        private Cbi.Order entryOrder = null;
        private double entryPrice = 0.0;
        private int entryBar = -1;

        private Cbi.Order entryOrderShort = null;
        private double entryPriceShort = 0.0;
        private int entryBarShort = -1;

        int consecutiveLosses = 0;
        private double triggerPrice = 0.0;

        private DynamicTrendLine smoothConfirmMA;

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

        // Momentum Constants
        private int dataLength = 8;
        private int atrMALength = 5;
        private int atrSmoothLength = 3;
        private double atrMultiplier = 1.0;
        private int numATR = 4;
        private bool checkPastSignal = true;

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

        // Display Symbol Variables
        private bool showVolTrade = true;
        private bool showVolTradeClose = true;

        // Trading PnL
        private bool EnableTrading = true;
        private double currentPnL;
        private List<DateTime> TradingBanDays;

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
		private int tradeQuantity = 3;

        // Delta Shading
        public Brush DeltaVolNegShade						= Brushes.Gold;
        public Brush DeltaVolBuyShade						= Brushes.LimeGreen;
        public Brush DeltaVolSellShade = Brushes.Salmon;
        public Brush DeltaVolTrendShade = Brushes.SkyBlue;
        public int DeltaShadeOpacity						= 25;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description =
                    @"This is a strategy using pivot levels to enter long and short trades with confluence from EMA, ATR & Volume";
                Name = "TradingLevelsAlgo";
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = tradeQuantity;
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

                RealTimePnlOnly = false;
                DisableTradingTimes = false;
                DisablePNLLimits = false;
                EnableBannedDays = true;
                MaxLoss = -500;
                MaxGain = 1200;
                LossCutOff = -70;
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

                TPLevelTS1 = 70;
                SLLevelTS1 = 19;
                BuySellBufferTS1 = 6;
                BarsToHoldTradeTS1 = 5;
                BarsToMissTradeTS1 = 4;
                OffsetFromEntryToCancelTS1 = 40;
                MaxLossConsecTS1 = 3;
                ResetBarsMissedOnLongTS1 = false;
                ResetBarsMissedOnShortTS1 = true;

                TPLevelTS2 = 70;
                SLLevelTS2 = 16;
                BuySellBufferTS2 = 5;
                BarsToHoldTradeTS2 = 4;
                BarsToMissTradeTS2 = 3;
                OffsetFromEntryToCancelTS2 = 30;
                MaxLossConsecTS2 = 2;
                ResetBarsMissedOnLongTS2 = false;
                ResetBarsMissedOnShortTS2 = false;

                TPLevelTS3 = 45;
                SLLevelTS3 = 15;
                BuySellBufferTS3 = 2;
                BarsToHoldTradeTS3 = 3;
                BarsToMissTradeTS3 = 3;
                OffsetFromEntryToCancelTS3 = 50;
                MaxLossConsecTS3 = 2;
                ResetBarsMissedOnLongTS3 = true;
                ResetBarsMissedOnShortTS3 = true;

                AveVolPeriod = 15;
                VolSmooth = 6;

                EnableDynamicSettings = true;
                BarsToMissNegDelta = 2;
                BarsToMissPosDelta = 3;
                DeltaNegCutOff = 1.0;

                EnableDynamicSL = true;
                ProfitToMoveSL = 29;
                SLNewLevel = -2;

                #region Banned Trading Days
                TradingBanDays = new List<DateTime>
                {
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
                Print(Time[0] + " ******** TRADING ALGO v1.5 ******** ");
                // Initialise all variables
                momentum = new Series<double>(this);
                chopIndexDetect = new Series<bool>(this);
                trendDirection = new Series<double>(this);
                deltaMomentum = new Series<double>(this);
                chopDetect = new Series<bool>(this);
                volatileMove = new Series<bool>(this);
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

                // Initialize EMAs
                smoothConfirmMA = DynamicTrendLine(8, 13, 21);

                // Add our EMAs to the chart for visualization
                AddChartIndicator(smoothConfirmMA);

                // SL/TP
                SetStopLoss("Long", CalculationMode.Ticks, slLevel / TickSize, false);
                SetProfitTarget("Long", CalculationMode.Ticks, tpLevel / TickSize);
                SetStopLoss("Short", CalculationMode.Ticks, slLevel / TickSize, false);
                SetProfitTarget("Short", CalculationMode.Ticks, tpLevel / TickSize);

                // Delta Shading
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
            }
        }

        protected override void OnBarUpdate()
        {
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

            // Ensure we have enough data
            if (CurrentBar < 14)
                return;
            #endregion

            #region Trend/Chop Calculation
            // ********** TREND CALCULATION **********
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
                ) || chopIndexDetect[0];

            volatileMove[0] =
                Math.Abs(trendDirection[0]) > volatileLimit
                || deltaMomentum[0] > deltaMomentumVolLimt;
            #endregion

            #region Volume Analysis
            // ********** VOLUME ANALYSIS **********
            // calculate difference between current open and previous close
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

            #region PnL Calculation
            // PnL Check
            if (
                (currentPnL < MaxLoss || currentPnL > MaxGain)
                && EnableTrading
                && !DisablePNLLimits
            )
            {
                EnableTrading = false;
                Print(Time[0] + " ******** TRADING DISABLED ******** : $" + currentPnL);
            }

            double realtimPnL = currentPnL + Position.GetUnrealizedProfitLoss(PerformanceUnit.Currency, Close[0]);
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

            #region Trading Logic
            // Trading Logic
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

            if (validTriggerPeriod && !IsAllowedTime())
            {
                buyVolCloseTrigger = true;
                sellVolCloseTrigger = true;
            }

            validTriggerPeriod = IsAllowedTime() && EnableTrading;
            BackBrush = null;
            // Load in new variables if delta volume is weak
            if (EnableDynamicSettings)
            {
                if (midVolPump[0] || midVolDump[0] || bullVolPump[0] || bullVolDump[0])
                {
                    if (deltaBuyVol < -1 * DeltaNegCutOff/100 && deltaSellVol < -1 * DeltaNegCutOff /100)
                    {
                        localBarsToMissTrade = BarsToMissNegDelta;
                        BackBrush = DeltaVolNegShade;
                    }
                    else if (deltaBuyVol < -1 * DeltaNegCutOff / 100 || deltaSellVol < -1 * DeltaNegCutOff / 100)
                    {
                        localBarsToMissTrade = BarsToMissPosDelta;
                        if (deltaBuyVol < deltaSellVol)
                        {
                            BackBrush = DeltaVolSellShade;
                        }
                        else
                        {
                            BackBrush = DeltaVolBuyShade;
                        }
                    }
                    else
                    {
                        localBarsToMissTrade = barsToMissTrade;
                        BackBrush = DeltaVolTrendShade;
                    }
                }
            } 
            else
            {
                localBarsToMissTrade = barsToMissTrade;
            }
            
            if (localBarsToMissTrade != localBarsToMissPrev && validTriggerPeriod)
            {
                Print(Time[0] + " Bars to Miss Trade Changed from " + localBarsToMissPrev + " to " + localBarsToMissTrade + ". Delta Buy: " + Math.Round(deltaBuyVol,3)*100 + "% Delta Sell: " + Math.Round(deltaSellVol,3)*100 + "%");
            }
            localBarsToMissPrev = localBarsToMissTrade;

            if (buyTrigger || buyVolSignal)
            {
                buyVolSignal = true;
                if (!EnableTrading || midVolDump[0] || bullVolDump[0])
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
                if (!EnableTrading || midVolPump[0] || bullVolPump[0])
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

            #region Trade Management
            #region Stoploss management
            // Resets the stop loss to the original value when all positions are closed
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                SetStopLoss("Long", CalculationMode.Ticks, slLevel / TickSize, false);
                SetStopLoss("Short", CalculationMode.Ticks, slLevel / TickSize, false);
            }
            else if (Position.MarketPosition == MarketPosition.Long && EnableDynamicSL)
            {
                if (High[0] > Position.AveragePrice + ProfitToMoveSL)
                {
                    SetStopLoss("Long", CalculationMode.Price, Position.AveragePrice - SLNewLevel * TickSize, false);
                }
            }
            else if (Position.MarketPosition == MarketPosition.Short && EnableDynamicSL)
            {
                if (Low[0] < Position.AveragePrice - ProfitToMoveSL)
                {
                    SetStopLoss("Short", CalculationMode.Price, Position.AveragePrice + SLNewLevel * TickSize, false);
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
            } else if (sellVolCloseTrigger)
            {
                if (entryOrderShort != null && entryOrderShort.OrderState == OrderState.Filled)
                {
                    Print(Time[0] + " Short Order Closed: " + Close[0]);
                }
                if (Position.MarketPosition == MarketPosition.Short)
                {
                    ExitShort("Short");
                }
            } else if (!EnableTrading)
            {
                if (Position.MarketPosition == MarketPosition.Long)
                {
                    ExitLong();
                }
                else if (Position.MarketPosition == MarketPosition.Short)
                {
                    ExitShort();
                }
            } else if (Position.MarketPosition == MarketPosition.Long)
            {
                if (Close[0] > triggerPrice + tpLevel)
                {
                    ExitLong("Long");
                }
            } else if (Position.MarketPosition == MarketPosition.Short)
            {
                if (Close[0] < triggerPrice - tpLevel)
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
                    double limitLevel = GetLimitLevel(
                        smoothConfirmMA[0] + buySellBuffer,
                        Close[0],
                        true
                    );

                    Print(Time[0] + " Long triggered: " + limitLevel);
                    entryOrder = EnterLongLimit(0, true, tradeQuantity, limitLevel, "Long");
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
                else if (
                    buyVolSignal
                    && entryOrder != null
                    && entryOrder.OrderState != OrderState.Filled
                )
                {
                    double limitLevel = GetLimitLevel(
                        smoothConfirmMA[0] + buySellBuffer,
                        Close[0],
                        true
                    );
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
            }

            if (!sellVolCloseTrigger && !buyVolCloseTrigger)
            {
                if ((sellTrigger || reverseSellTrade) && entryOrderShort == null)
                {
                    double limitLevel = GetLimitLevel(
                        smoothConfirmMA[0] - buySellBuffer,
                        Close[0],
                        false
                    );

                    Print(Time[0] + " Short triggered: " + limitLevel);
                    entryOrderShort = EnterShortLimit(0, true, tradeQuantity, limitLevel, "Short");
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
                else if (
                    sellVolSignal
                    && entryOrderShort != null
                    && entryOrderShort.OrderState != OrderState.Filled
                )
                {
                    double limitLevel = GetLimitLevel(
                        smoothConfirmMA[0] - buySellBuffer,
                        Close[0],
                        false
                    );

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
            }

            reverseBuyTrade = sellVolCloseTrigger && buyTrigger;
            reverseSellTrade = buyVolCloseTrigger && sellTrigger;
            #endregion
            #endregion

            #region Dashboard
            string dashBoard =
                $"PnL ({(RealTimePnlOnly ? "RT" : "ALL")}): $"
                + realtimPnL.ToString()
                + " | Consec: " + consecutiveLosses + " of " + maxLossConsec
                + "\nTrading: "
                + (EnableTrading || DisablePNLLimits ? "Active" : "Off");
            if (buyVolSignal || sellVolSignal)
                dashBoard += "\nBars Missed: " + barsMissed + " of " + barsToMissTrade;
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
            Draw.TextFixed(this, "Dashboard", dashBoard, TextPosition.BottomRight);
            #endregion
        }

        protected override void OnOrderUpdate(
            Cbi.Order order,
            double limitPrice,
            double stopPrice,
            int quantity,
            int filled,
            double averageFillPrice,
            Cbi.OrderState orderState,
            DateTime time,
            Cbi.ErrorCode error,
            string comment
        )
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

        protected override void OnExecutionUpdate(
            Cbi.Execution execution,
            string executionId,
            double price,
            int quantity,
            Cbi.MarketPosition marketPosition,
            string orderId,
            DateTime time
        )
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
                                + " ******** TRADING DISABLED (3 losses in a row) ******** : $"
                                + currentPnL
                        );
                    }
                }
            }
        }

        private bool IsAllowedTime()
        {
            // Convert bar's DateTime to TimeSpan for comparison
            TimeSpan barTime = Time[0].TimeOfDay;

            // Check if bar time is within session 1
            bool isInSession1 = barTime >= TS1Start.TimeOfDay && barTime < TS1End.TimeOfDay;
            bool isInSession2 = barTime >= TS2Start.TimeOfDay && barTime < TS2End.TimeOfDay;
            bool isInSession3 = barTime >= TS3Start.TimeOfDay && barTime < TS3End.TimeOfDay;

            return isInSession1 || isInSession2 || isInSession3 || DisableTradingTimes;
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

        #region Properties
        [NinjaScriptProperty]
        [Display(Name = "RealTimePnlOnly", Description = "Track PnL only during realtime trading", Order = 1, GroupName = "Main Parameters")]
        public bool RealTimePnlOnly
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "EnableBannedDays", Description = "Enable banned days for backtesting", Order = 2, GroupName = "Main Parameters")]
        public bool EnableBannedDays
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "DisableTradingTimes", Description = "Disable preset trading times", Order = 2, GroupName = "Main Parameters")]
        public bool DisableTradingTimes
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "DisablePNLLimits", Description = "Disable PnL limits for the day", Order = 2, GroupName = "Main Parameters")]
        public bool DisablePNLLimits
        { get; set; }

        [NinjaScriptProperty]
        [Range(double.MinValue, -100)]
        [Display(Name = "MaxLoss", Description = "Maximum loss before trading stops", Order = 4, GroupName = "Main Parameters")]
        public double MaxLoss
        { get; set; }

        [NinjaScriptProperty]
        [Range(100, double.MaxValue)]
        [Display(Name = "MaxGain", Description = "Maximum daily gain before trading stops", Order = 5, GroupName = "Main Parameters")]
        public double MaxGain
        { get; set; }

        [NinjaScriptProperty]
        [Range(double.MinValue, 0)]
        [Display(Name = "LossCutOff", Description = "Price to be considered a loss for consec. losses check", Order = 6, GroupName = "Main Parameters")]
        public double LossCutOff
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ResetConsecOnTime", Description = "Reset consec. losses on time session switch", Order = 7, GroupName = "Main Parameters")]
        public bool ResetConsecOnTime
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "EnableTradingTS1", Description = "Enable trading for time session 1", Order = 8, GroupName = "Sessions")]
        public bool EnableTradingTS1
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "EnableTradingTS2", Description = "Enable trading for time session 2", Order = 11, GroupName = "Sessions")]
        public bool EnableTradingTS2
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "EnableTradingTS3", Description = "Enable trading for time session 3", Order = 14, GroupName = "Sessions")]
        public bool EnableTradingTS3
        { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "TS1Start", Description = "Time session 1 start", Order = 9, GroupName = "Sessions")]
        public DateTime TS1Start
        { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "TS1End", Description = "Time session 1 end", Order = 10, GroupName = "Sessions")]
        public DateTime TS1End
        { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "TS2Start", Description = "Time session 2 start", Order = 12, GroupName = "Sessions")]
        public DateTime TS2Start
        { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "TS2End", Description = "Time session 2 end", Order = 13, GroupName = "Sessions")]
        public DateTime TS2End
        { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "TS3Start", Description = "Time session 3 start", Order = 15, GroupName = "Sessions")]
        public DateTime TS3Start
        { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "TS3End", Description = "Time session 3 end", Order = 16, GroupName = "Sessions")]
        public DateTime TS3End
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "TPLevelTS1", Description = "Take profit level", Order = 15, GroupName = "Time Session 1")]
        public double TPLevelTS1
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "SLLevelTS1", Description = "Stop loss level", Order = 16, GroupName = "Time Session 1")]
        public double SLLevelTS1
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "BuySellBufferTS1", Description = "Buffer for buy/sell limit levels", Order = 17, GroupName = "Time Session 1")]
        public double BuySellBufferTS1
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "BarsToHoldTradeTS1", Description = "Bars to hold trade", Order = 18, GroupName = "Time Session 1")]
        public int BarsToHoldTradeTS1
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "BarsToMissTradeTS1", Description = "Bars to miss trade", Order = 19, GroupName = "Time Session 1")]
        public int BarsToMissTradeTS1
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, double.MaxValue)]
        [Display(Name = "OffsetFromEntryToCancelTS1", Description = "Offset from entry to cancel", Order = 20, GroupName = "Time Session 1")]
        public double OffsetFromEntryToCancelTS1
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "MaxLossConsecTS1", Description = "Max consecutive losses", Order = 21, GroupName = "Time Session 1")]
        public int MaxLossConsecTS1
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ResetBarsMissedOnLongTS1", Description = "Reset bars missed on long", Order = 22, GroupName = "Time Session 1")]
        public bool ResetBarsMissedOnLongTS1
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ResetBarsMissedOnShortTS1", Description = "Reset bars missed on short", Order = 23, GroupName = "Time Session 1")]
        public bool ResetBarsMissedOnShortTS1
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "TPLevelTS2", Description = "Take profit level", Order = 24, GroupName = "Time Session 2")]
        public double TPLevelTS2
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "SLLevelTS2", Description = "Stop loss level", Order = 25, GroupName = "Time Session 2")]
        public double SLLevelTS2
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "BuySellBufferTS2", Description = "Buffer for buy/sell limit levels", Order = 26, GroupName = "Time Session 2")]
        public double BuySellBufferTS2
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "BarsToHoldTradeTS2", Description = "Bars to hold trade", Order = 27, GroupName = "Time Session 2")]
        public int BarsToHoldTradeTS2
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "BarsToMissTradeTS2", Description = "Bars to miss trade", Order = 28, GroupName = "Time Session 2")]
        public int BarsToMissTradeTS2
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, double.MaxValue)]
        [Display(Name = "OffsetFromEntryToCancelTS2", Description = "Offset from entry to cancel", Order = 29, GroupName = "Time Session 2")]
        public double OffsetFromEntryToCancelTS2
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "MaxLossConsecTS2", Description = "Max consecutive losses", Order = 30, GroupName = "Time Session 2")]
        public int MaxLossConsecTS2
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ResetBarsMissedOnLongTS2", Description = "Reset bars missed on long", Order = 31, GroupName = "Time Session 2")]
        public bool ResetBarsMissedOnLongTS2
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ResetBarsMissedOnShortTS2", Description = "Reset bars missed on short", Order = 32, GroupName = "Time Session 2")]
        public bool ResetBarsMissedOnShortTS2
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "TPLevelTS3", Description = "Take profit level", Order = 33, GroupName = "Time Session 3")]
        public double TPLevelTS3
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "SLLevelTS3", Description = "Stop loss level", Order = 34, GroupName = "Time Session 3")]
        public double SLLevelTS3
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "BuySellBufferTS3", Description = "Buffer for buy/sell limit levels", Order = 35, GroupName = "Time Session 3")]
        public double BuySellBufferTS3
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "BarsToHoldTradeTS3", Description = "Bars to hold trade", Order = 36, GroupName = "Time Session 3")]
        public int BarsToHoldTradeTS3
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "BarsToMissTradeTS3", Description = "Bars to miss trade", Order = 37, GroupName = "Time Session 3")]
        public int BarsToMissTradeTS3
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, double.MaxValue)]
        [Display(Name = "OffsetFromEntryToCancelTS3", Description = "Offset from entry to cancel", Order = 38, GroupName = "Time Session 3")]
        public double OffsetFromEntryToCancelTS3
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "MaxLossConsecTS3", Description = "Max consecutive losses", Order = 39, GroupName = "Time Session 3")]
        public int MaxLossConsecTS3
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ResetBarsMissedOnLongTS3", Description = "Reset bars missed on long", Order = 40, GroupName = "Time Session 3")]
        public bool ResetBarsMissedOnLongTS3
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ResetBarsMissedOnShortTS3", Description = "Reset bars missed on short", Order = 41, GroupName = "Time Session 3")]
        public bool ResetBarsMissedOnShortTS3
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "AveVolPeriod", Description = "Average Volume Period", Order = 42, GroupName = "Volume")]
        public int AveVolPeriod
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "VolSmooth", Description = "Volume smoothing period", Order = 43, GroupName = "Volume")]
        public int VolSmooth
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "EnableDynamicSettings", Description = "Use Dynamic Parameters based on delta volume", Order = 44, GroupName = "Dynamic Trades")]
        public bool EnableDynamicSettings
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "BarsToMissNegDelta", Description = "Number of bars to miss when in negative delta volume", Order = 45, GroupName = "Dynamic Trades")]
        public int BarsToMissNegDelta
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "BarsToMissPosDelta", Description = "Number of bars to miss when in positive delta volume", Order = 46, GroupName = "Dynamic Trades")]
        public int BarsToMissPosDelta
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "DeltaNegCutOff", Description = "Delta volume cutoff for negative delta trades (%)", Order = 47, GroupName = "Dynamic Trades")]
        public double DeltaNegCutOff
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "EnableDynamicSL", Description = "Enable SL move on profit", Order = 48, GroupName = "Dynamic Stoploss")]
        public bool EnableDynamicSL
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "ProfitToMoveSL", Description = "Profit level to move SL", Order = 49, GroupName = "Dynamic Stoploss")]
        public double ProfitToMoveSL
        { get; set; }

        [NinjaScriptProperty]
        [Range(-10, double.MaxValue)]
        [Display(Name = "SLNewLevel", Description = "New SL level after profit", Order = 50, GroupName = "Dynamic Stoploss")]
        public double SLNewLevel
        { get; set; }
        #endregion
    }
}
