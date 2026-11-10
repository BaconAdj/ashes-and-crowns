using System;

namespace AshesAndCrowns.Sim
{
    public enum VillagerActivity : byte { Asleep = 0, Rising = 1, Working = 2, Eating = 3, Idling = 4, Drinking = 5, Praying = 6, Home = 7 }

    [Serializable] public sealed class VillageDayBalance
    {
        public float wakeHour = 5f, workStartHour = 6f, dinnerHour = 11.5f, dinnerEndHour = 12.5f, workEndHour = 17f, greenEndHour = 19f, bedHour = 21.5f;
        public float walkSpeed = 1.35f, hurrySpeed = 2.0f;
        public float awakeRadiusMetres = 160f;     // villagers farther than this from the player are placed, not walked
    }

    /// <summary>The shape of a villager's day. Pure: hour in, activity out. Variant (0..n) spreads a village so not everyone drinks.</summary>
    public static class VillageDay
    {
        public static VillagerActivity At(float hour, bool sunday, bool massNow, int variant, VillageDayBalance b)
        {
            if (massNow) return VillagerActivity.Praying;
            if (hour < b.wakeHour || hour >= b.bedHour) return VillagerActivity.Asleep;
            if (hour < b.workStartHour) return VillagerActivity.Rising;
            if (sunday)
            {
                if (hour < b.dinnerHour) return VillagerActivity.Idling;
                if (hour < b.dinnerEndHour) return VillagerActivity.Eating;
                if (hour < b.greenEndHour) return variant % 2 == 0 ? VillagerActivity.Idling : VillagerActivity.Drinking;
                return variant % 3 == 0 ? VillagerActivity.Drinking : VillagerActivity.Home;
            }
            if (hour < b.dinnerHour) return VillagerActivity.Working;
            if (hour < b.dinnerEndHour) return VillagerActivity.Eating;
            if (hour < b.workEndHour) return VillagerActivity.Working;
            if (hour < b.greenEndHour) return VillagerActivity.Idling;
            return variant % 3 == 0 ? VillagerActivity.Home : VillagerActivity.Drinking;
        }

        public static bool Indoors(VillagerActivity a) => a == VillagerActivity.Asleep || a == VillagerActivity.Rising || a == VillagerActivity.Eating || a == VillagerActivity.Home || a == VillagerActivity.Drinking || a == VillagerActivity.Praying;

        /// <summary>One line for the talk screen: what this person is doing and whether they will talk.</summary>
        public static string Describe(VillagerActivity a, string trade)
        {
            switch (a)
            {
                case VillagerActivity.Asleep: return "asleep under a blanket; a grunt from the dark tells you to come back at Prime";
                case VillagerActivity.Rising: return "pulling on boots by a cold hearth";
                case VillagerActivity.Working: return WorkLine(trade);
                case VillagerActivity.Eating: return "at the hearth over pottage, spoon in hand";
                case VillagerActivity.Idling: return "on the green, trading the day's talk";
                case VillagerActivity.Drinking: return "in the alehouse with a pot of ale";
                case VillagerActivity.Praying: return "in the nave with the rest of the village";
                default: return "at home, the door half open";
            }
        }

        private static string WorkLine(string trade)
        {
            switch (trade)
            {
                case "blacksmith": return "at the anvil, sparks in the straw";
                case "miller": return "at the quern, white to the elbow";
                case "hunter": return "out past the wood's edge with a bow";
                case "brewer": return "behind the casks, skimming the wort";
                case "carpenter": return "at the shaving horse in the yard";
                case "shepherd": return "out on the pasture with the flock";
                case "fisherman": return "down at the weir mending a trap";
                case "priest": return "at the altar, muttering the office";
                default: return "out in the strips behind a plough";
            }
        }
    }
}
