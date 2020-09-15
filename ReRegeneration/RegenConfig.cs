using System;

namespace ReRegeneration
{
    class RegenConfig
    {
        public Double staminaRegenPerSecond { get; set; } = 1.0;
        public Int32 staminaIdleSeconds { get; set; } = 5;
        public Double maxStaminaRatioToRegen { get; set; } = 0.8;
        public Double scaleStaminaRegenRateTo { get; set; } = 0.5;
        public Double scaleStaminaRegenDelayTo { get; set; } = 0.5;

        public Double healthRegenPerSecond { get; set; } = 0.1;
        public Int32 healthIdleSeconds { get; set; } = 10;
        public Double maxHealthRatioToRegen { get; set; } = 0.5;
        public Double scaleHealthRegenRateTo { get; set; } = 0.75;
        public Double scaleHealthRegenDelayTo { get; set; } = 0.75;

        public Boolean percentageMode { get; set; } = false;
        public Double regenWhileActiveRate { get; set; } = 0.8;
        public Double regenWhileRunningRate { get; set; } = 0.2;
        public Double endExhaustionAt { get; set; } = 0.25;
        public Double exhuastionPenalty { get; set; } = 0.9;
        public Double shortenDelayWhenStillBy { get; set; } = 0.5;
        public Double lengthenDelayWhenRunningBy { get; set; } = 0.5;

        public Double timeInterval { get; set; } = 0.25;
        public Boolean verboseMode { get; set; } = false;
    }
}
