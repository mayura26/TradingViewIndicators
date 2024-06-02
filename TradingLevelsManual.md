Welcome to my indicator. TradingLevelsAlgo is a trading assistant designed to generate trade signals using various confluences and provide visual indications to enhance the trading experience. This indicator is built around the concept of trading based on levels. You can add levels to the system, as well as it generates its own, and combines these levels with volume, EMA and ATR analysis to create trade alerts and help the user quickly identify opportunities. There are tooltips through the system to help you, but below I go over the basic concepts. This has been optimised specifically for trading use, and recommend the 3M timeframe. Most of this has been tested with NQ/MNQ but should work well with ES/MES. I believe this will work well on SPY/QQQ/BTC as well but more testing is needed and I am open to feedback.

[b]Levels[/b]
The levels components is compromised of two parts. You have the main ticker which is used to generate buy/sell indications and the secondary ticker which is used for creating supporting indications. A typical trade pair could be NQ or ES paired with SPY. You can trade on NQ, and use SPY levels to help aid your trades. 

There are four types of levels available to the user. By default, this is set as buy, sell, pivot and intraday levels. You can customise this as you wish.  When the price moves against these levels, the algo tries to identify a bounce. This is what create a trade alert as the goal is to find opportunities when price retests or reverses as a level, as a suitable entry point.

There are also calculated levels which can be shown based on dynamic calculations.  These are listed below
[list]
[*]Daily High/Low/Close
[*]Yesterday High/Low/Close
[*]Weekly High/Low/Close
[*]ATR Levels (-1/-0.618/-0.236/+0.236/+0.618/+1)
[*]Camarilla Pivots (S3/S4/S6/R3/R4/R6) 
[*]ORB Levels
[/list]

The goals with the levels is to use them as entry/exit points for trades. The bounce concept is also utilised in the trading signals functionality (described below) as a trigger condition. Ideally you don't want to have too many levels stacked together when trying to use this system because it will then alert you too much.

[b]EMA/Trend[/b]
I have built a system which uses three EMA and combines it with momentum and delta calculations to create a dynamic trend line which acts as a suitable entry/exit point for trades. The support EMAs are also displayed as shadows and offer a chance to get a more granular approach to exit trades if you wish to hold trades longer period.

[b]Volume[/b]
Using the structure of the candle, I am calculating the buy/sell volume for each candle. This is displayed to the user at the bottom of the screen with various systems showing how aggressive the volume is. You can use this as a way to identify trends (a lot of green volume symbols in a row). I use the volume cross (so the shift from red to green) to act as a buy signal and combine this with delta analysis to show how strong the volume behind the trade is. 

[b]ATR[/b]
ATR buy/sell signals are also included. These are when the momentum signal starts to shift, and when combined with the breakout of the close past the HL2 of the ATR, it shows as a green/red arrow pointing up or down. The shadowed circles act as pre-warnings to when this signal could occur.

[b]Chopzone[/b]
There is a system which can analysed the last 5 10M candles on the chart and based on how the price is reacting, will identify a chopzone. This is a zone where price is ranging between two levels. Once in this zone, every candle is marked with a yellow x to alert the user they are in the chopzone. The upper and lower chopzone lines are also plotted. Once you exit this range, the system will again start to offer trade signals. I also have a chop index and trend calculation, which are also used on top of this to protect against chop. These will show up in the dashboard when in effect.

[b]Trading Signals[/b]
There are four type of signals in this system. Directional, reversal, volume and cross trades.
[list]
[*]Volume trades are triggered by the volume cross and show as Bv/Sv/Cv for buy/sell/close. The volume delta settings can be used to fine tune how reactive these trades can be
[*]Directional trade are when we get a bounce in the same direction as the trend and carry on. They are generally safe trades because we have retest against a support and carried on moving.
[*]Cross trades are when we get a combination of the confluences. You can set the number of confluences needed in the settings, and the more you have the more reliable a trade can be.
[*]Reversal trade are when we get a bounce against the opposite direction of the trend. They are generally risky trades but usually can work well when bouncing off a key level.

[b]Dashboard[/b]
There is a dashboard which shows, which give you realtime information of the trend, volume breakdown, colour coded symbols for whether you should be trading, as well as the last 10m trends of your key tickers.