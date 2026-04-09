using System;
using System.Collections.Generic;

namespace RaceFlow.Admin
{
    public sealed class RaceOutputFrame
    {
        public DateTime CapturedUtc { get; set; } = DateTime.UtcNow;
        public List<RaceOutputRacerVisual> Racers { get; } = new();
    }

    public sealed class RaceOutputRacerVisual
    {
        public string RacerKey { get; set; } = string.Empty;
        public string RacerName { get; set; } = string.Empty;
        public string ColorHex { get; set; } = "#FFFFFF";
        public bool IsActive { get; set; }

        public string? LastConfirmedNodeId { get; set; }
        public string? TargetNodeId { get; set; }

        public double EdgeProgress { get; set; }
        public bool HasFinished { get; set; }

        public string StatusText { get; set; } = string.Empty;
    }
}