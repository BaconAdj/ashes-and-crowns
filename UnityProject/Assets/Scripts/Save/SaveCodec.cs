using System;
using System.IO;
using System.Text;
using AshesAndCrowns.Sim;

namespace AshesAndCrowns.Save
{
    public sealed class SaveCorruptException : Exception { public SaveCorruptException(string m) : base(m) { } }

    /// <summary>
    /// Binary save format: "ASHC" magic, format version, payload length, FNV-1a 64 checksum, payload.
    /// Binary keeps saves small and fast (a 2,000-soldier battle is under 100 KB) and the checksum
    /// catches truncation and casual editing. Not an anti-cheat.
    /// </summary>
    public static class SaveCodec
    {
        private static readonly byte[] Magic = { (byte)'A', (byte)'S', (byte)'H', (byte)'C' };
        public const int HeaderBytes = 4 + 4 + 4 + 8;

        public static byte[] Encode(RunState s)
        {
            byte[] payload;
            using (var ms = new MemoryStream(4096))
            using (var w = new BinaryWriter(ms, Encoding.UTF8))
            {
                WritePayload(w, s, s.Version < 1 || s.Version > RunState.CurrentVersion ? RunState.CurrentVersion : s.Version);
                w.Flush();
                payload = ms.ToArray();
            }
            var result = new byte[HeaderBytes + payload.Length];
            Buffer.BlockCopy(Magic, 0, result, 0, 4);
            int version = s.Version < 1 || s.Version > RunState.CurrentVersion ? RunState.CurrentVersion : s.Version;
            BitConverter.GetBytes(version).CopyTo(result, 4);
            BitConverter.GetBytes(payload.Length).CopyTo(result, 8);
            BitConverter.GetBytes(Fnv1a64(payload, 0, payload.Length)).CopyTo(result, 12);
            Buffer.BlockCopy(payload, 0, result, HeaderBytes, payload.Length);
            return result;
        }

        public static RunState Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < HeaderBytes) throw new SaveCorruptException("file too short");
            for (int i = 0; i < 4; i++) if (bytes[i] != Magic[i]) throw new SaveCorruptException("bad magic");
            int version = BitConverter.ToInt32(bytes, 4);
            int length = BitConverter.ToInt32(bytes, 8);
            ulong checksum = BitConverter.ToUInt64(bytes, 12);
            if (version < 1 || version > RunState.CurrentVersion) throw new SaveCorruptException("unsupported save version " + version);
            if (length < 0 || HeaderBytes + length != bytes.Length) throw new SaveCorruptException("length mismatch (truncated?)");
            if (Fnv1a64(bytes, HeaderBytes, length) != checksum) throw new SaveCorruptException("checksum mismatch");
            using (var ms = new MemoryStream(bytes, HeaderBytes, length, false))
            using (var r = new BinaryReader(ms, Encoding.UTF8))
            {
                try { return ReadPayload(r, version); }
                catch (EndOfStreamException) { throw new SaveCorruptException("payload ended early"); }
            }
        }

        public static ulong Fnv1a64(byte[] data, int offset, int count)
        {
            ulong h = 14695981039346656037UL;
            for (int i = offset; i < offset + count; i++) { h ^= data[i]; h *= 1099511628211UL; }
            return h;
        }

        // ------------------------------------------------------------ payload

        private static void WritePayload(BinaryWriter w, RunState s, int version)
        {
            w.Write(s.Seed); w.Write(s.RngState); w.Write(s.SceneId ?? string.Empty); w.Write(s.SavedAtUtcTicks);
            w.Write((byte)s.Affairs); w.Write(s.Hostility); w.Write(s.EnemiesNearby); w.Write(s.InBattle);
            w.Write(s.CalendarTotalHours);
            w.Write(s.Weather.Kind); w.Write(s.Weather.UntilChange); w.Write(s.Weather.Precipitation); w.Write(s.Weather.Fog); w.Write(s.Weather.Wind); w.Write(s.Weather.Cloud);
            w.Write(s.WeatherRngState);
            w.Write(s.PlayerName ?? string.Empty);
            w.Write(s.PlayerX); w.Write(s.PlayerY); w.Write(s.PlayerZ); w.Write(s.PlayerYaw); w.Write(s.Mounted);
            w.Write(s.HorseX); w.Write(s.HorseY); w.Write(s.HorseZ); w.Write(s.HorseYaw);
            BodySnapshot b = s.Body ?? new BodySnapshot();
            w.Write(b.Blood);
            w.Write(BodyParts.Count); for (int i = 0; i < BodyParts.Count; i++) w.Write(i < b.PartIntegrity.Length ? b.PartIntegrity[i] : 1f);
            w.Write(b.Wounds.Length); foreach (Wound wd in b.Wounds) { w.Write((byte)wd.Part); w.Write(wd.Severity); w.Write(wd.Bleeding); w.Write(wd.AgeSeconds); if (version >= 3) { w.Write(wd.Infected); w.Write(wd.Treated); } }
            w.Write(b.CauseOfDeath ?? string.Empty);
            w.Write(s.Stamina);
            w.Write(s.HoursSurvived); w.Write(s.Kills); w.Write(s.MenLost); w.Write(s.Battles); w.Write(s.PeakWealth);
            w.Write(s.Battle != null);
            if (s.Battle != null) WriteBattle(w, s.Battle);
            if (version >= 2)
            {
                w.Write(s.HasCharacter && s.Character != null);
                if (s.HasCharacter && s.Character != null) WriteCharacter(w, s.Character);
                var fam = s.Family ?? new Npc[0];
                w.Write(fam.Length);
                foreach (Npc n in fam) { w.Write(n.Id); w.Write(n.Name ?? ""); w.Write(n.Female); w.Write(n.Age); w.Write((byte)n.Relation); w.Write(n.Alive); w.Write(n.TraitA ?? ""); w.Write(n.TraitB ?? ""); w.Write(n.Disposition); w.Write(n.Note ?? ""); }
                var lords = s.Baronage ?? new MinorLord[0];
                w.Write(lords.Length);
                foreach (MinorLord l in lords)
                {
                    w.Write(l.Id); w.Write(l.GivenName ?? ""); w.Write(l.House ?? ""); w.Write(l.Epithet ?? ""); w.Write(l.Age); w.Write(l.TraitA ?? ""); w.Write(l.TraitB ?? "");
                    w.Write((byte)l.Declared); w.Write((byte)l.Secret); w.Write(l.Loyalty); w.Write(l.Ambition); w.Write(l.Knights); w.Write(l.SeatName ?? ""); w.Write(l.NearLocationId ?? "");
                    w.Write(l.Nx); w.Write(l.Nz); w.Write(l.FeudWith); w.Write(l.Alive);
                }
                w.Write(s.SpawnLocationId ?? ""); w.Write(s.OpeningSeen);
            }
            if (version >= 4)
            {
                w.Write(s.Hunger); w.Write(s.Rest); w.Write(s.SicknessKind); w.Write(s.SickDays);
                Strings(w, s.InventoryIds); Ints(w, s.InventoryCounts);
                w.Write(s.StandingValue); w.Write(s.MassesAttended); w.Write(s.MassesMissed);
                w.Write(s.ApprenticeTrade ?? ""); w.Write(s.ApprenticeMaster); w.Write(s.ApprenticeSeasons); w.Write(s.ApprenticeCompetence);
                Strings(w, s.MarketIds); Floats(w, s.MarketStocks); w.Write(s.MarketShock); w.Write(s.MarketShockStrength); w.Write(s.MarketWealth);
                w.Write(s.Outbreak); w.Write(s.LastMassDay);
            }
        }

        private static void Strings(BinaryWriter w, string[] a) { a = a ?? new string[0]; w.Write(a.Length); foreach (string x in a) w.Write(x ?? ""); }
        private static string[] Strings(BinaryReader r) { int n = r.ReadInt32(); if (n < 0 || n > 100000) throw new SaveCorruptException("bad string array"); var a = new string[n]; for (int i = 0; i < n; i++) a[i] = r.ReadString(); return a; }

        private static void WriteCharacter(BinaryWriter w, CharacterSheet c)
        {
            w.Write(c.Name ?? ""); w.Write(c.Female); w.Write(c.AgeYears); w.Write((byte)c.Rank); w.Write(c.BackgroundId ?? ""); w.Write(c.Trade ?? ""); w.Write(c.FixedFigureId ?? "");
            w.Write(c.Coins); w.Write(c.Wanted); w.Write(c.AppearancePreset); w.Write(c.Height); w.Write(c.Build); w.Write(c.SkinTone); w.Write(c.HairTone);
            w.Write(4); for (int i = 0; i < 4; i++) w.Write(c.Attributes[i]);
            w.Write(Skills.Count); for (int i = 0; i < Skills.Count; i++) w.Write(c.SkillLevels[i]);
            w.Write(c.AttributeErosion);
        }

        private static CharacterSheet ReadCharacter(BinaryReader r)
        {
            var c = new CharacterSheet
            {
                Name = r.ReadString(), Female = r.ReadBoolean(), AgeYears = r.ReadInt32(), Rank = (Rank)r.ReadByte(), BackgroundId = r.ReadString(), Trade = r.ReadString(), FixedFigureId = r.ReadString(),
                Coins = r.ReadInt32(), Wanted = r.ReadBoolean(), AppearancePreset = r.ReadInt32(), Height = r.ReadSingle(), Build = r.ReadSingle(), SkinTone = r.ReadSingle(), HairTone = r.ReadSingle()
            };
            int na = r.ReadInt32(); if (na < 0 || na > 16) throw new SaveCorruptException("bad attribute count");
            for (int i = 0; i < na; i++) { int v = r.ReadInt32(); if (i < 4) c.Attributes[i] = v; }
            int ns = r.ReadInt32(); if (ns < 0 || ns > 256) throw new SaveCorruptException("bad skill count");
            for (int i = 0; i < ns; i++) { float v = r.ReadSingle(); if (i < Skills.Count) c.SkillLevels[i] = v; }
            c.AttributeErosion = r.ReadSingle();
            return c;
        }

        private static RunState ReadPayload(BinaryReader r, int version)
        {
            var s = new RunState { Version = version };
            s.Seed = r.ReadInt64(); s.RngState = r.ReadUInt64(); s.SceneId = r.ReadString(); s.SavedAtUtcTicks = r.ReadInt64();
            s.Affairs = (AffairsChoice)r.ReadByte(); s.Hostility = r.ReadSingle(); s.EnemiesNearby = r.ReadInt32(); s.InBattle = r.ReadBoolean();
            s.CalendarTotalHours = r.ReadDouble();
            s.Weather = new WeatherSnapshot { Kind = r.ReadByte(), UntilChange = r.ReadSingle(), Precipitation = r.ReadSingle(), Fog = r.ReadSingle(), Wind = r.ReadSingle(), Cloud = r.ReadSingle() };
            s.WeatherRngState = r.ReadUInt64();
            s.PlayerName = r.ReadString();
            s.PlayerX = r.ReadSingle(); s.PlayerY = r.ReadSingle(); s.PlayerZ = r.ReadSingle(); s.PlayerYaw = r.ReadSingle(); s.Mounted = r.ReadBoolean();
            s.HorseX = r.ReadSingle(); s.HorseY = r.ReadSingle(); s.HorseZ = r.ReadSingle(); s.HorseYaw = r.ReadSingle();
            var b = new BodySnapshot { Blood = r.ReadSingle() };
            int parts = r.ReadInt32(); if (parts < 0 || parts > 64) throw new SaveCorruptException("bad body part count");
            b.PartIntegrity = new float[BodyParts.Count];
            for (int i = 0; i < parts; i++) { float v = r.ReadSingle(); if (i < BodyParts.Count) b.PartIntegrity[i] = v; }
            int wounds = r.ReadInt32(); if (wounds < 0 || wounds > 1024) throw new SaveCorruptException("bad wound count");
            b.Wounds = new Wound[wounds];
            for (int i = 0; i < wounds; i++) { var wd = new Wound { Part = (BodyPart)r.ReadByte(), Severity = r.ReadSingle(), Bleeding = r.ReadSingle(), AgeSeconds = r.ReadSingle() }; if (version >= 3) { wd.Infected = r.ReadBoolean(); wd.Treated = r.ReadBoolean(); } b.Wounds[i] = wd; }
            b.CauseOfDeath = r.ReadString(); if (b.CauseOfDeath.Length == 0) b.CauseOfDeath = null;
            s.Body = b;
            s.Stamina = r.ReadSingle();
            s.HoursSurvived = r.ReadDouble(); s.Kills = r.ReadInt32(); s.MenLost = r.ReadInt32(); s.Battles = r.ReadInt32(); s.PeakWealth = r.ReadInt32();
            if (r.ReadBoolean()) s.Battle = ReadBattle(r);
            if (version >= 2)
            {
                s.HasCharacter = r.ReadBoolean();
                if (s.HasCharacter) s.Character = ReadCharacter(r);
                int nf = r.ReadInt32(); if (nf < 0 || nf > 1000) throw new SaveCorruptException("bad family count");
                s.Family = new Npc[nf];
                for (int i = 0; i < nf; i++)
                    s.Family[i] = new Npc { Id = r.ReadInt32(), Name = r.ReadString(), Female = r.ReadBoolean(), Age = r.ReadInt32(), Relation = (Relation)r.ReadByte(), Alive = r.ReadBoolean(), TraitA = r.ReadString(), TraitB = r.ReadString(), Disposition = r.ReadSingle(), Note = r.ReadString() };
                int nl = r.ReadInt32(); if (nl < 0 || nl > 10000) throw new SaveCorruptException("bad baronage count");
                s.Baronage = new MinorLord[nl];
                for (int i = 0; i < nl; i++)
                    s.Baronage[i] = new MinorLord
                    {
                        Id = r.ReadInt32(), GivenName = r.ReadString(), House = r.ReadString(), Epithet = r.ReadString(), Age = r.ReadInt32(), TraitA = r.ReadString(), TraitB = r.ReadString(),
                        Declared = (Allegiance)r.ReadByte(), Secret = (Allegiance)r.ReadByte(), Loyalty = r.ReadSingle(), Ambition = r.ReadSingle(), Knights = r.ReadInt32(), SeatName = r.ReadString(), NearLocationId = r.ReadString(),
                        Nx = r.ReadSingle(), Nz = r.ReadSingle(), FeudWith = r.ReadInt32(), Alive = r.ReadBoolean()
                    };
                s.SpawnLocationId = r.ReadString(); s.OpeningSeen = r.ReadBoolean();
            }
            if (version >= 4)
            {
                s.Hunger = r.ReadSingle(); s.Rest = r.ReadSingle(); s.SicknessKind = r.ReadByte(); s.SickDays = r.ReadInt32();
                s.InventoryIds = Strings(r); s.InventoryCounts = Ints(r);
                s.StandingValue = r.ReadSingle(); s.MassesAttended = r.ReadInt32(); s.MassesMissed = r.ReadInt32();
                s.ApprenticeTrade = r.ReadString(); s.ApprenticeMaster = r.ReadInt32(); s.ApprenticeSeasons = r.ReadSingle(); s.ApprenticeCompetence = r.ReadSingle();
                s.MarketIds = Strings(r); s.MarketStocks = Floats(r); s.MarketShock = r.ReadByte(); s.MarketShockStrength = r.ReadSingle(); s.MarketWealth = r.ReadSingle();
                s.Outbreak = r.ReadSingle(); s.LastMassDay = r.ReadInt64();
            }
            return s;
        }

        private static void WriteBattle(BinaryWriter w, BattleSnapshot b)
        {
            w.Write(b.SoldierCount); w.Write(b.UnitCount); w.Write(b.PlayerSoldierEnd); w.Write(b.Elapsed); w.Write(b.EnemyThinkTimer); w.Write(b.Started); w.Write(b.PlayerCharacterAlive);
            Floats(w, b.Positions); Floats(w, b.Yaws); Floats(w, b.Health); Floats(w, b.Cooldowns);
            Bytes(w, b.Alive); Bytes(w, b.Side); Ints(w, b.UnitOf); Ints(w, b.SlotOf); Ints(w, b.Targets);
            Bytes(w, b.UnitSide); Bytes(w, b.UnitFormation); Bytes(w, b.UnitOrder); Bytes(w, b.UnitAlive);
            Ints(w, b.UnitStart); Ints(w, b.UnitCount_); Ints(w, b.UnitAliveCount);
            Floats(w, b.UnitAnchor); Floats(w, b.UnitYaw); Floats(w, b.UnitTarget);
        }

        private static BattleSnapshot ReadBattle(BinaryReader r)
        {
            var b = new BattleSnapshot
            {
                SoldierCount = r.ReadInt32(), UnitCount = r.ReadInt32(), PlayerSoldierEnd = r.ReadInt32(), Elapsed = r.ReadSingle(), EnemyThinkTimer = r.ReadSingle(),
                Started = r.ReadBoolean(), PlayerCharacterAlive = r.ReadBoolean()
            };
            if (b.SoldierCount < 0 || b.SoldierCount > 100000 || b.UnitCount < 0 || b.UnitCount > 10000) throw new SaveCorruptException("bad battle counts");
            b.Positions = Floats(r); b.Yaws = Floats(r); b.Health = Floats(r); b.Cooldowns = Floats(r);
            b.Alive = Bytes(r); b.Side = Bytes(r); b.UnitOf = Ints(r); b.SlotOf = Ints(r); b.Targets = Ints(r);
            b.UnitSide = Bytes(r); b.UnitFormation = Bytes(r); b.UnitOrder = Bytes(r); b.UnitAlive = Bytes(r);
            b.UnitStart = Ints(r); b.UnitCount_ = Ints(r); b.UnitAliveCount = Ints(r);
            b.UnitAnchor = Floats(r); b.UnitYaw = Floats(r); b.UnitTarget = Floats(r);
            return b;
        }

        private const int MaxArray = 4_000_000;
        private static void Floats(BinaryWriter w, float[] a) { a = a ?? new float[0]; w.Write(a.Length); for (int i = 0; i < a.Length; i++) w.Write(a[i]); }
        private static void Ints(BinaryWriter w, int[] a) { a = a ?? new int[0]; w.Write(a.Length); for (int i = 0; i < a.Length; i++) w.Write(a[i]); }
        private static void Bytes(BinaryWriter w, byte[] a) { w.Write(a.Length); w.Write(a); }
        private static float[] Floats(BinaryReader r) { int n = r.ReadInt32(); if (n < 0 || n > MaxArray) throw new SaveCorruptException("bad array length"); var a = new float[n]; for (int i = 0; i < n; i++) a[i] = r.ReadSingle(); return a; }
        private static int[] Ints(BinaryReader r) { int n = r.ReadInt32(); if (n < 0 || n > MaxArray) throw new SaveCorruptException("bad array length"); var a = new int[n]; for (int i = 0; i < n; i++) a[i] = r.ReadInt32(); return a; }
        private static byte[] Bytes(BinaryReader r) { int n = r.ReadInt32(); if (n < 0 || n > MaxArray) throw new SaveCorruptException("bad array length"); return r.ReadBytes(n); }
    }
}
