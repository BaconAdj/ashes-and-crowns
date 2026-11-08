using System.Collections.Generic;

namespace AshesAndCrowns.Sim
{
    /// <summary>Where a new character starts and what the narrator says. Pure rules; the Game layer places objects.</summary>
    public static class RunSetup
    {
        public enum SpawnKind : byte { Village, Castle, Capital }

        public static SpawnKind SpawnFor(Rank rank)
        {
            switch (rank)
            {
                case Rank.Peasant: case Rank.Warrior: return SpawnKind.Village;
                case Rank.Monarch: return SpawnKind.Capital;
                default: return SpawnKind.Castle;
            }
        }

        /// <summary>Pick a start among candidates of the right kind. Monarchs go to their own seat.</summary>
        public static string ChooseSpawn(CharacterSheet sheet, IList<string> villages, IList<string> castles, DeterministicRng rng)
        {
            switch (SpawnFor(sheet.Rank))
            {
                case SpawnKind.Capital:
                    if (sheet.FixedFigureId == "stephen") return "london";
                    if (sheet.FixedFigureId == "matilda") return "gloucester";
                    return "bristol";
                case SpawnKind.Castle:
                    return castles != null && castles.Count > 0 ? castles[rng.Range(0, castles.Count)] : "wallingford";
                default:
                    return villages != null && villages.Count > 0 ? villages[rng.Range(0, villages.Count)] : "ashby_village";
            }
        }

        public static string[] OpeningNarration(CharacterSheet sheet, Calendar calendar)
        {
            string date = calendar != null ? calendar.LongDate() : "the winter of 1135";
            var lines = new List<string>(5)
            {
                "In the year of our Lord eleven hundred and thirty-five, the king died, and left no son.",
                "His nephew took the crown. His daughter would not forgive it.",
            };
            switch (sheet.Rank)
            {
                case Rank.Peasant:
                    lines.Add($"None of it reached you yet. You are {sheet.Name}, a {sheet.Trade} of a village nobody has heard of, and the bells have rung for Terce.");
                    break;
                case Rank.Warrior:
                    lines.Add($"It reached you as it reaches every sword: as wages. You are {sheet.Name}, and nobody pays you yet.");
                    break;
                case Rank.Knight:
                    lines.Add($"You are {sheet.Name}, a knight of another man's household. His side is your side, until it is not.");
                    break;
                case Rank.Lord:
                    lines.Add($"You are {sheet.Name}, who holds a castle, a village and a choice. Both sides have written to you.");
                    break;
                default:
                    lines.Add(sheet.FixedFigureId == "matilda"
                        ? "You are Matilda, empress, daughter of the king, and England has been given to someone else."
                        : sheet.FixedFigureId == "stephen" ? "You are Stephen, king of England since Sunday. The oaths they swore to her are not forgotten."
                        : $"You are {sheet.Name}, who says the old king was your father. Some of the men behind you believe it.");
                    break;
            }
            lines.Add($"It is {date}. Men will later say that Christ and his saints slept.");
            return lines.ToArray();
        }
    }
}
