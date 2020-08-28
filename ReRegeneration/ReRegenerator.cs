﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StardewValley;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using Microsoft.Xna.Framework;


namespace ReRegeneration
{

    class ModConfig
    {
        public Double healthRegenPerSecond { get; set; }
        public Int32 healthIdleSeconds { get; set; }
        public Double regenHealthWhileRunningRate { get; set; }

        public Double staminaRegenPerSecond { get; set; }
        public Int32 staminaIdleSeconds { get; set; }
        public Double regenStaminaWhileRunningRate { get; set; }

        public Boolean percentageMode { get; set; }
        public Boolean verboseMode { get; set; }


        public ModConfig()
        {
            this.healthRegenPerSecond = 0.1;
            this.healthIdleSeconds = 15;
            this.regenHealthWhileRunningRate = 0.0;

            this.staminaRegenPerSecond = 1.0;
            this.staminaIdleSeconds = 10;
            this.regenStaminaWhileRunningRate = 0.25;

            this.percentageMode = false;
            this.verboseMode = false;
        }
    }


    class ReRegenerator : Mod
    {
        //Health values
        double healthRegenInterval;             //How often to add health, needed due to weird math constraints...
        int lastHealth;                         //Last recorded player health value.
        int lastMaxHealth;                      //Last recorded max health value.
        double healthAccum;                     //Accumulated health regenerated while running.
        double healthCooldown;                  //How long to wait before beginning health regen.
        double healthRunRate;                   //Running rate

        //Stamina values
        double stamRegenVal;                    //How much stamina to regen
        double lastStamina;                     //Last recorded player stamina value.
        double lastMaxStamina;                  //Last recorded max stamina value.
        double staminaCooldown;                 //How long to wait before beginning stamina regen.
        double stamRunRate;                     //Running rate.

        //Control values
        double currentTime;                     //The... current time. (In seconds?)
        double timeElapsed;                     //Time since last check.
        double lastLogTime;                     //Time since last log event.
        double lastTickTime;                    //The time at the last tick processed.
        bool moveBlocked;                       //Whether movement will block regeneration.
        bool percentageMode;                    //Whether we're using this mode of operation.
        bool verbose;                           //Whether to log regular diagnostics

        Farmer Player;                          //Our player.
        ModConfig myConfig;                     //Config data.

        public override void Entry(IModHelper helper)
        {
            myConfig = helper.ReadConfig<ModConfig>();

            //What mode
            percentageMode = myConfig.percentageMode;

            //Verbosity?
            verbose = myConfig.verboseMode;

            //Running rates
            healthRunRate = myConfig.regenHealthWhileRunningRate;
            stamRunRate = myConfig.regenStaminaWhileRunningRate;

            //Max running rate can't be higher than base.
            if (healthRunRate > 1.0) { healthRunRate = 1.0; }
            if (stamRunRate > 1.0) { stamRunRate = 1.0; }

            //Starting values at extremes which prevent unexpected behavior. (Why not just 0...?)
            lastHealth = 9999;
            lastMaxHealth = 0;
            healthCooldown = 0;
            healthAccum = 0.0;

            lastStamina = 9999;
            lastMaxStamina = 0.0;
            staminaCooldown = 0;

            currentTime = 0.0;
            timeElapsed = 0.0;
            lastTickTime = 0.0;
            lastLogTime = 0.0;

            //Regen values
            SetRegenVals();

            moveBlocked = false;

            helper.Events.GameLoop.UpdateTicked += OnUpdate;

            Monitor.Log("ReRegeneration => Initialized", LogLevel.Info);

        }

        /* Figure out what the regen values are. May be called frequently.
         * 
         * Health regen is expected to be slower than stamina. This will effectively increase the interval time in that case.
         * We have to do this because unlike stamina, health is an int. So, in the default case of 0.1 health/sec, what
         * really happens is that the player gets 1 health every 10 seconds (1 / 0.1 = 10).
         * 
        */
        void SetRegenVals()
        {
            //What to do if calculating by percentages.
            if (percentageMode && Game1.hasLoadedGame)
            {
                Player = Game1.player;

                int maxHealthNow = Player.maxHealth;
                int maxStamNow = Player.maxStamina;

                //Only (re)calculate if max health changed or first time.
                if (lastMaxHealth < maxHealthNow)
                {
                    healthRegenInterval = myConfig.healthRegenPerSecond;

                    //Turn into a percentage
                    healthRegenInterval *= 0.01;

                    //Calculate according to current max health
                    healthRegenInterval *= maxHealthNow;

                    //Turn into a length of time in seconds.
                    healthRegenInterval = 1 / healthRegenInterval;

                    //Record the last seen max health
                    lastMaxHealth = maxHealthNow;
                }

                //Only (re)calculate if max stamina changed or first time.
                if (lastMaxStamina < maxStamNow)
                {
                    stamRegenVal = myConfig.staminaRegenPerSecond;

                    //Turn into a percentage
                    stamRegenVal *= 0.01;

                    //Calculate according to current max stamina
                    stamRegenVal *= maxStamNow;

                    //Record the last seen max stamina
                    lastMaxStamina = maxStamNow;
                }

                return;
            }

            healthRegenInterval = 1 / myConfig.healthRegenPerSecond;

            stamRegenVal = myConfig.staminaRegenPerSecond;

        }

        //Timed logging.
        void LogIt(string msg, bool doIt = true, LogLevel lvl = LogLevel.Debug)
        {
            if (doIt)
            {
                Monitor.Log(msg, lvl);
                lastLogTime = Game1.currentGameTime.TotalGameTime.TotalSeconds;
            }
        }

        //Formatted stat report.
        string StatReport(bool doAll = true, bool doMod = false, bool doStam = false, bool doHealth = false)
        {
            //only if in verbose mode
            if (!verbose) { return ""; }

            string retStr = String.Format("\n\n========= ReRegenerator Status after {0} seconds ==========", Math.Round(Game1.currentGameTime.TotalGameTime.TotalSeconds));

            Player = Game1.player;

            if (doAll || doMod)
            {
                retStr += String.Format("\n\n-- Mod Status --\n*Stam recovery rate: {0}\n*While running: {1}\n*Health recovery interval: {2}\n*While running: {3}\n*Percent mode: {4}",
                    stamRegenVal, stamRunRate, healthRegenInterval, healthRunRate, percentageMode);
            }

            if (doAll || doStam)
            {
                retStr += String.Format("\n\n-- Stamina Status --\n*Lost stamina: {0}\n*Cooldown time: {1}", (Player.maxStamina - Player.stamina), Math.Round(staminaCooldown));
            }

            if (doAll || doHealth)
            {
                retStr += String.Format("\n\n-- Health Status --\n*Lost Health: {0}\n*Cooldown time: {1}\n*Accumulator: {2}", (Player.maxHealth - Player.health), Math.Round(healthCooldown), healthAccum);
            }

            //On mon-mod updates, give player stats.
            if (!doMod)
            {
                retStr += String.Format("\n\n-- Player Status --\n*Is running: {0}\n*Moved last tick: {1}\n*On horseback: {2}", Player.running, Player.movedDuringLastTick(), Player.isRidingHorse());
            }

            retStr += "\n\n========== ReRegenerator Status Report Complete ==========\n\n";

            return retStr;
        }

        private void OnUpdate(object sender, EventArgs e)
        {
            Player = Game1.player;

            //If game is running, and time can pass (i.e., are not in an event/cutscene/menu/festival)
            if (Game1.hasLoadedGame && Game1.shouldTimePass())
            {
                //Make sure we know exactly how much time has elapsed (?)
                currentTime = Game1.currentGameTime.TotalGameTime.TotalSeconds;
                timeElapsed = currentTime - lastTickTime;
                lastTickTime = currentTime;

                //Do this once.
                LogIt(StatReport(false, true, false, false), ((currentTime < 30.0) && lastLogTime == 0.0));

                //Every 15 secs report on all.
                LogIt(StatReport(true, false, false, false), ((currentTime - lastLogTime) >= 15));

                //Check for player injury. If player has been injured since last tick, reset the cooldown.
                //Decrement how long we've been on health cooldown otherwise.
                if (Player.health < lastHealth) { healthCooldown = myConfig.healthIdleSeconds; }
                else if (healthCooldown > 0) { healthCooldown -= timeElapsed; }

                //Check for player exertion. If player has used stamina since last tick, reset the cooldown.
                //Decrement how long we've been on stamina cooldown otherwise.
                if (Player.stamina < lastStamina) { staminaCooldown = myConfig.staminaIdleSeconds; }
                else if (staminaCooldown > 0) { staminaCooldown -= timeElapsed; }

                /* Determine whether movement status will block normal regeneration: If player is... 
                 * 1. running, and
                 * 2. has moved recently, and
                 * 3. is not on horseback, then
                 * movement blocks normal regen and the running rate prevails. (If the running rate is 0, there is no regeneration.)
                */
                if (Player.running && Player.movedDuringLastTick() && !Player.isRidingHorse()) { moveBlocked = true; }
                else { moveBlocked = false; }

                /*
                 * Process health regeneration. Here are the criteria:
                 * -Must have some health regen value.
                 * -Must have less than max health.
                 * -The health cooldown must be over.
                */
                if (healthRegenInterval > 0 && Player.health < Player.maxHealth && healthCooldown <= 0)
                {
                    //Only update as needed.
                    SetRegenVals();

                    /* 
                     * Basically, we need to try to restore 1 health every interval, but we absolutely need a round number. 
                     * So we "accumulate" fractional health each interval while running. (If not running, we accumulate 1 which gets applied ASAP.)
                     * In this case, the fraction is not applied to the regen amount so much as accumulated per (extended) interval.
                     * This might lead to some oddities where the player runs, stops running, and then later starts again. But eh.
                    */
                    if (moveBlocked) { healthAccum += healthRunRate; }
                    else { healthAccum += 1; }

                    //If we've accumulated 1 or more health, apply it and reduce the accumulation accordingly.
                    //Note: I think it's vaguely theoretically possible that healthAccum could go above 2, and I am not sure how well this code would handle that. Probably fine...
                    if (healthAccum >= 1)
                    {
                        Player.health += 1;
                        healthAccum -= 1.0;
                    }
                    //If we have achieved a round positive number accumulated, apply it and reset the accumulation.
                    //This probably shouldn't ever really happen because of the above, but it's a fallback just in case...
                    else if (healthAccum > 0 && healthAccum % 1 == 0)
                    {
                        Player.health += (int)healthAccum;
                        healthAccum = 0.0;
                    }

                    //Final sanity check
                    if (Player.health > Player.maxHealth) { Player.health = Player.maxHealth; }

                    //Every second give health/stam update.
                    LogIt(StatReport(false, false, true, true), ((currentTime - lastLogTime) >= 1));

                    healthCooldown = healthRegenInterval;
                }

                /*
                 * Process stamina regeneration. Here are the criteria:
                 * -Must have some stamina regen value.
                 * -Must have less than max stamina.
                 * -The stamina cooldown must be over.
                */
                if (stamRegenVal > 0 && Player.stamina < Player.maxStamina && staminaCooldown <= 0)
                {
                    //Only update as needed.
                    SetRegenVals();

                    //Here, we can actually add fractional increments.
                    //Implicitly, the math works out such that if you don't want to regen while running, the amount regenerated will be 0 while running.
                    if (moveBlocked) { Player.stamina += (float)(stamRegenVal * stamRunRate); }
                    else { Player.stamina += (float)stamRegenVal; }

                    //Final sanity check
                    if (Player.stamina > Player.maxStamina) { Player.stamina = Player.maxStamina; }

                    //Every second give health/stam update.
                    LogIt(StatReport(false, false, true, true), ((currentTime - lastLogTime) >= 1));

                    staminaCooldown = 1;
                }

                // Updated stored health/stamina values.
                lastHealth = Player.health;
                lastStamina = Player.stamina;
            }
        }
    }

}
