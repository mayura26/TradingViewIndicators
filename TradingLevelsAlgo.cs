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
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
    public class TradingLevelsAlgo : Strategy
    {
        private Order entryOrder = null;
        private int entryBar = -1;
        private EMA ema5;
        private EMA ema8;
        private EMA ema13;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"This is a strategy using pivot levels to enter long and short trades with confluence from EMA";
                Name = "TradingLevelsAlgo";
                Calculate = Calculate.OnEachTick;
                EntriesPerDirection = 5;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 30;
                IsFillLimitOnTouch = false;
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution = OrderFillResolution.Standard;
                Slippage = 0;
                StartBehavior = StartBehavior.WaitUntilFlat;
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
                // Initialize EMAs
                ema5 = EMA(5);
                ema8 = EMA(8);
                ema13 = EMA(13);

                // Add our EMAs to the chart for visualization
                AddChartIndicator(ema5);
                AddChartIndicator(ema8);
                AddChartIndicator(ema13);
            }
        }

        protected override void OnBarUpdate()
        {
            // Ensure we have enough data
            if (CurrentBar < 13) return;

            // Check if we have an open order
            if (entryOrder != null)
            {
                // Manage open orders here, e.g., check if it's time to exit based on bar count
                if (CurrentBar >= entryBar + 5)
                {
                    CancelOrder(entryOrder);
                    entryOrder = null; // Reset the entry order variable
                }
                return; // Skip the rest of the method if we're managing an open order
            }

            // Entry condition: 5 EMA crosses above 13 EMA
            if (CrossAbove(ema5, ema13, 1))
            {
                // Enter a long trade at the 8 EMA level
                entryOrder = EnterLongLimit(0, true, 1, ema8[0], "EMA_5_8_13_Long");
                entryBar = CurrentBar; // Remember the bar at which we entered
            }
            // Entry condition: 5 EMA crosses below 13 EMA
            if (CrossBelow(ema5, ema13, 1))
            {
                // Enter a short trade at the 8 EMA level
                entryOrder = EnterShortLimit(0, true, 1, ema8[0], "EMA_5_8_13_Short");
                entryBar = CurrentBar; // Remember the bar at which we entered
            }
        }
        protected override void OnOrderUpdate(Cbi.Order order, double limitPrice, double stopPrice,
                                              int quantity, int filled, double averageFillPrice,
                                              Cbi.OrderState orderState, DateTime time, Cbi.ErrorCode error, string comment)
        {
            // This method is called on order updates. Use it to manage order state changes.
            if (order.Name == "EMA_5_8_13_Long" && orderState == OrderState.Filled)
            {
                // Set stop loss and profit target for the filled order
                SetStopLoss("EMA_5_8_13_Long", CalculationMode.Price, averageFillPrice - 15, false);
                SetProfitTarget("EMA_5_8_13_Long", CalculationMode.Price, averageFillPrice + 15);
            }
        }
    }
}