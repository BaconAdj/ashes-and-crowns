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
                WritePayload(w, s);
                w.Flush();
                payload = ms.ToArray();
            }
            var result = new byte[HeaderBytes + payload.Length];
            Buffer.BlockCopy(Magic, 0, result, 0, 4);
            BitConverter.GetBytes(RunState.CurrentVersion).CopyTo(result, 4);
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

        private static void WritePayload(BinaryWriter w, RunState s)
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
            w.Write(b.Wounds.Length); foreach (Wound wd in b.Wounds) { w.Write((byte)wd.Part); w.Write(wd.Severity); w.Write(wd.Bleeding); w.Write(wd.AgeSeconds); }
            w.Write(b.CauseOfDeath ?? string.Empty);
            w.Write(s.Stamina);
            w.Write(s.HoursSurvived); w.Write(s.Kills); w.Write(s.MenLost); w.Write(s.Battles); w.Write(s.PeakWealth);
            w.Write(s.Battle != null);
            if (s.Battle != null) WriteBattle(w, s.Battle);
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
            for (int i = 0; i < wounds; i++) b.Wounds[i] = new Wound { Part = (BodyPart)r.ReadByte(), Severity = r.ReadSingle(), Bleeding = r.ReadSingle(), AgeSeconds = r.ReadSingle() };
            b.CauseOfDeath = r.ReadString(); if (b.CauseOfDeath.Length == 0) b.CauseOfDeath = null;
            s.Body = b;
            s.Stamina = r.ReadSingle();
            s.HoursSurvived = r.ReadDouble(); s.Kills = r.ReadInt32(); s.MenLost = r.ReadInt32(); s.Battles = r.ReadInt32(); s.PeakWealth = r.ReadInt32();
            if (r.ReadBoolean()) s.Battle = ReadBattle(r);
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
        private static void Floats(BinaryWriter w, float[] a) { w.Write(a.Length); for (int i = 0; i < a.Length; i++) w.Write(a[i]); }
        private static void Ints(BinaryWriter w, int[] a) { w.Write(a.Length); for (int i = 0; i < a.Length; i++) w.Write(a[i]); }
        private static void Bytes(BinaryWriter w, byte[] a) { w.Write(a.Length); w.Write(a); }
        private static float[] Floats(BinaryReader r) { int n = r.ReadInt32(); if (n < 0 || n > MaxArray) throw new SaveCorruptException("bad array length"); var a = new float[n]; for (int i = 0; i < n; i++) a[i] = r.ReadSingle(); return a; }
        private static int[] Ints(BinaryReader r) { int n = r.ReadInt32(); if (n < 0 || n > MaxArray) throw new SaveCorruptException("bad array length"); var a = new int[n]; for (int i = 0; i < n; i++) a[i] = r.ReadInt32(); return a; }
        private static byte[] Bytes(BinaryReader r) { int n = r.ReadInt32(); if (n < 0 || n > MaxArray) throw new SaveCorruptException("bad array length"); return r.ReadBytes(n); }
    }
}
