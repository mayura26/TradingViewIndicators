#region Using declarations
using System;
using System.Drawing;
using System.Windows.Documents;
using System.Windows.Media;
using NinjaTrader.Cbi;
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
    // TODO: Create standalone volume indicator
    // TODO: Create chop indicator with trend chop detection
    // TODO: Create inputs for indicator including: Disable trading times, close on reversal only and number of bars to leave vol trade open, TP SL and vol trade length
    // TODO: Add timeout after three bad trades
    // TODO: Add pre calculated levels
    // TODO: Add code to close trade on pre calculated level
    public class TradingLevelsAlgo : Strategy
    {
        private Order entryOrder = null;
        private double entryPrice = 0.0;
        private int entryBar = -1;

        private Order entryOrderShort = null;
        private double entryPriceShort = 0.0;
        private int entryBarShort = -1;

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
        private bool buyVolCloseTrigger;
        private bool sellVolCloseTrigger;
        private int volTradeLength;
        private bool buyVolSignal = false;
        private bool sellVolSignal = false;
        bool validTriggerPeriod = false;

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
        private int aveVolPeriod = 14;
        private int volSmooth = 5;
        private double regVolLevel = 60;

        // Trend/Chop Constants
        private int chopCalcLength = 14;
        private double volatileLimit = 5.0;
        private double trendLimit = 2.5;
        private double chopLimit = 1.5;
        private double deltaMomentumChopLimt = 0.5;
        private double deltaMomentumVolLimt = 2.5;

        // Trading Times
        private TimeSpan sessionStart1 = new TimeSpan(10, 00, 00);
        private TimeSpan sessionEnd1 = new TimeSpan(16, 15, 00);
        private TimeSpan sessionStart2 = new TimeSpan(17, 00, 00);
        private TimeSpan sessionEnd2 = new TimeSpan(17, 15, 00);
        private TimeSpan sessionStart3 = new TimeSpan(06, 00, 00);
        private TimeSpan sessionEnd3 = new TimeSpan(09, 00, 00);

        private bool showVolTrade = true;
        private bool showVolTradeClose = true;

        // Trading PnL
        private bool EnableTrading = true;
        private double currentPnL;

        // Inputs
        private double tpLevel = 90;
        private double slLevel = 15;
        private double buySellBuffer = 2;
        private int barsToHoldTrade = 5;
        private int barsToMissTrade = 3;
        private bool disableTradingTimes = false;
        private double MaxLoss = -500;
        private double MaxGain = 20000;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description =
                    @"This is a strategy using pivot levels to enter long and short trades with confluence from EMA, ATR & Volume";
                Name = "TradingLevelsAlgo";
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 5;
                EntryHandling = EntryHandling.AllEntries;
                IncludeCommission = true;
                IsExitOnSessionCloseStrategy = false;
                ExitOnSessionCloseSeconds = 30;
                IsFillLimitOnTouch = false;
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution = OrderFillResolution.Standard;
                Slippage = 0;
                StartBehavior = StartBehavior.WaitUntilFlatSynchronizeAccount;
                TimeInForce = TimeInForce.Gtc;
                TraceOrders = false;
                RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
                StopTargetHandling = StopTargetHandling.PerEntryExecution;
                BarsRequiredToTrade = 20;
                // Disable this property for performance gains in Strategy Analyzer optimizations
                // See the Help Guide for additional information
                IsInstantiatedOnEachOptimizationIteration = true;
            }
            else if (State == State.DataLoaded)
            {
                ClearOutputWindow();
                Print(Time[0] + " ******** TRADING ALGO v1.0 ******** ");
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
            }
        }

        protected override void OnBarUpdate()
        {
            // Reset PnL at the start of the session
            if (Bars.IsFirstBarOfSession)
            {
                currentPnL = 0;
                EnableTrading = true;
                Print(Time[0] + " ******** TRADING ENABLED ******** ");
            }

            // Ensure we have enough data
            if (CurrentBar < 14)
                return;

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

            smoothBuy = WMA(WMA(BuyVol, aveVolPeriod), volSmooth);
            smoothSell = WMA(WMA(SellVol, aveVolPeriod), volSmooth);
            smoothNetVol[0] = smoothBuy[0] - smoothSell[0];

            double netVolH = Math.Max(smoothBuy[0], smoothSell[0]);
            double netVolL = Math.Min(smoothBuy[0], smoothSell[0]);
            bool risingVol = (smoothNetVol[0] - smoothNetVol[1]) > 0;

            double volPumpLevel = volPumpGainLimit / 100;
            double irregVolLevel = volIrregLimit / 100;

            avgVolume = SMA(Volume, aveVolPeriod);
            avgBuyVol = SMA(smoothBuy, aveVolPeriod);
            avgSellVol = SMA(smoothSell, aveVolPeriod);

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

            // PnL Check
            if ((currentPnL < MaxLoss || currentPnL > MaxGain) && EnableTrading)
            {
                EnableTrading = false;
                Print(Time[0] + " ******** TRADING DISABLED ******** : " + currentPnL);
            }

            // if in a position and the realized day's PnL plus the position PnL is greater than the loss limit then exit the order
            if (
                (
                    (
                        (
                            currentPnL
                            + Position.GetUnrealizedProfitLoss(PerformanceUnit.Currency, Close[0])
                        ) <= MaxLoss
                    )
                    || (
                        currentPnL
                        + Position.GetUnrealizedProfitLoss(PerformanceUnit.Currency, Close[0])
                    ) >= MaxGain
                ) && EnableTrading
            )
            {
                EnableTrading = false;
                Print(Time[0] + " ******** TRADING DISABLED (mid-trade) ******** : " + currentPnL);
            }

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

            if (buyTrigger || buyVolSignal)
            {
                buyVolSignal = true;
                if (!EnableTrading || sellTrigger)
                {
                    buyVolSignal = false;
                    buyVolCloseTrigger = true;
                    volTradeLength = 0;
                    barsMissed = 0;
                }
                else if (!(midVolPump[0] || bullVolPump[0]))
                {
                    if (barsMissed < barsToMissTrade)
                    {
                        barsMissed += 1;
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
                    //barsMissed = 0;
                }
            }

            if (sellTrigger || sellVolSignal)
            {
                sellVolSignal = true;
                if (!EnableTrading || buyTrigger)
                {
                    sellVolSignal = false;
                    sellVolCloseTrigger = true;
                    volTradeLength = 0;
                    barsMissed = 0;
                }
                else if (!(midVolDump[0] || bullVolDump[0]))
                {
                    if (barsMissed < barsToMissTrade)
                    {
                        barsMissed += 1;
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
                    barsMissed = 0;
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
                        Brushes.Purple
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
                        Brushes.Purple
                    );
                }
            }

            // Check if we have an open order
            if (entryOrder != null)
            {
                // Manage open orders here, e.g., check if it's time to exit based on bar count

                if (
                    ((CurrentBar >= entryBar + barsToHoldTrade) || buyVolCloseTrigger)
                    && entryOrder.OrderState == OrderState.Working
                )
                {
                    Print(Time[0] + " Long Order cancelled: " + Close[0]);
                    CancelOrder(entryOrder);
                    entryOrder = null; // Reset the entry order variable
                }
            }

            if (entryOrderShort != null)
            {
                // Manage open orders here, e.g., check if it's time to exit based on bar count

                if (
                    ((CurrentBar >= entryBarShort + barsToHoldTrade) || sellVolCloseTrigger)
                    && entryOrderShort.OrderState == OrderState.Working
                )
                {
                    Print(Time[0] + " Short Order cancelled: " + Close[0]);
                    CancelOrder(entryOrderShort);
                    entryOrderShort = null; // Reset the entry order variable
                }
            }

            if (buyVolCloseTrigger)
            {
                ExitLong();
            }
            else
            {
                if (buyTrigger && entryOrder == null)
                {
                    double limitLevel = GetLimitLevel(
                        smoothConfirmMA[0] + buySellBuffer,
                        Close[0],
                        true
                    );

                    Print(Time[0] + " Long triggered: " + limitLevel);
                    entryOrder = EnterLongLimit(0, true, 5, limitLevel, "Long");
                    entryBar = CurrentBar; // Remember the bar at which we entered
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

                    entryOrder.LimitPrice = limitLevel;
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
                            + " Vol Trade Length: "
                            + volTradeLength
                    );
                }
            }

            if (sellVolCloseTrigger)
            {
                ExitShort();
            }
            else
            {
                if (sellTrigger && entryOrderShort == null)
                {
                    double limitLevel = GetLimitLevel(
                        smoothConfirmMA[0] - buySellBuffer,
                        Close[0],
                        false
                    );

                    Print(Time[0] + " Short triggered: " + limitLevel);
                    entryOrderShort = EnterShortLimit(0, true, 5, limitLevel, "Short");
                    entryBarShort = CurrentBar; // Remember the bar at which we entered
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

                    entryOrderShort.LimitPrice = limitLevel;
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
                            + " Vol Trade Length: "
                            + volTradeLength
                    );
                }
            }
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
            if (order.Name == "Long")
            {
                if (orderState == OrderState.Filled)
                {
                    // Set stop loss and profit target for the filled order
                    SetStopLoss("Long", CalculationMode.Price, averageFillPrice - slLevel, false);
                    SetProfitTarget("Long", CalculationMode.Price, averageFillPrice + tpLevel);
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
                    // Set stop loss and profit target for the filled order
                    SetStopLoss("Short", CalculationMode.Price, averageFillPrice + slLevel, false);
                    SetProfitTarget("Short", CalculationMode.Price, averageFillPrice - tpLevel);
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
            }
        }

        protected override void OnPositionUpdate(
            Position position,
            double averagePrice,
            int quantity,
            MarketPosition marketPosition
        )
        {
            if (
                Position.MarketPosition == MarketPosition.Flat
                && SystemPerformance.AllTrades.Count > 0
            )
            {
                // when a position is closed, add the last trade's Profit to the currentPnL
                currentPnL += SystemPerformance
                    .AllTrades[SystemPerformance.AllTrades.Count - 1]
                    .ProfitCurrency;
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
        }

        private bool IsAllowedTime()
        {
            // Convert bar's DateTime to TimeSpan for comparison
            TimeSpan barTime = Time[0].TimeOfDay;

            // Check if bar time is within session 1
            bool isInSession1 = barTime >= sessionStart1 && barTime <= sessionEnd1;
            bool isInSession2 = barTime >= sessionStart2 && barTime <= sessionEnd2;
            bool isInSession3 = barTime >= sessionStart3 && barTime <= sessionEnd3;

            return isInSession1 || isInSession2 || isInSession3 || disableTradingTimes;
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

        // NinjaTrader doesn't have a built-in RoundToNearestTick method, so we define one.
        // This rounds a price to the nearest tick size defined for the instrument.
        private double RoundToNearestTick(double price)
        {
            double tickSize = Instrument.MasterInstrument.TickSize;
            return Math.Round(price / tickSize) * tickSize;
        }
    }
}
