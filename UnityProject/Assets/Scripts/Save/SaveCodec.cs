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
            if (s.Battle != null) WriteBattle(w, s.Battle, version);
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
            if (version >= 5)
            {
                var sets = s.Settlements ?? new SettlementSnapshot[0];
                w.Write(sets.Length);
                foreach (var t in sets)
                {
                    w.Write(t.Id ?? ""); w.Write(t.Name ?? ""); w.Write(t.Type); w.Write(t.Nx); w.Write(t.Nz); w.Write(t.Population); w.Write(t.Wealth); w.Write(t.Allegiance); w.Write(t.LordId);
                    w.Write(t.Outbreak); w.Write(t.WantedHere); w.Write(t.Coastal); w.Write(t.Upland); w.Write(t.River); w.Write(t.IsFixed);
                    Strings(w, t.GoodIds); Floats(w, t.GoodStocks); Floats(w, t.GoodBaselines); Floats(w, t.GoodDemands); w.Write(t.Shock); w.Write(t.ShockStrength);
                    var jobs = t.Jobs ?? new JobSnapshot[0]; w.Write(jobs.Length); foreach (var j in jobs) WriteJob(w, j);
                }
                var rep = s.Reputation ?? new float[4]; for (int i = 0; i < 4; i++) w.Write(i < rep.Length ? rep[i] : 0f); w.Write(s.Notoriety);
                var crimes = s.Crimes ?? new CrimeSnapshot[0]; w.Write(crimes.Length); foreach (var c in crimes) { w.Write(c.Kind); w.Write(c.Where ?? ""); w.Write(c.Day); w.Write(c.Witnesses); w.Write(c.Known); w.Write(c.Tried); }
                w.Write(s.Outlawed); w.Write(s.SanctuaryDaysLeft); w.Write(s.SanctuaryExpired);
                w.Write(s.ActiveJob != null); if (s.ActiveJob != null) WriteJob(w, s.ActiveJob);
                w.Write(s.NextJobId); w.Write(s.HomeSettlementId ?? "");
            }
            if (version >= 7)
            {
                w.Write(s.ServiceRole); w.Write(s.ServiceSide); w.Write(s.ServiceCompany ?? ""); w.Write(s.ServiceCaptain ?? ""); w.Write(s.ServicePatron ?? "");
                w.Write(s.ServiceWage); w.Write(s.ServiceDays); w.Write(s.ServiceOwed); w.Write(s.ServiceBattles); w.Write(s.ServiceRenown); w.Write(s.ServiceKills); w.Write(s.ServiceLines);
                w.Write(s.ServiceInfractions); w.Write(s.ServicePatronFavour); w.Write(s.ServiceDeserter); w.Write(s.ServiceInCamp);
                var cos = s.Companies ?? new CompanySnapshot[0]; w.Write(cos.Length);
                foreach (var c in cos) { w.Write(c.Name ?? ""); w.Write(c.Captain ?? ""); w.Write(c.BasedAtId ?? ""); w.Write(c.Employer); w.Write(c.Size); w.Write(c.ContractDaysLeft); w.Write(c.Reputation); }
            }
            if (version >= 8)
            {
                w.Write(s.HasSiege); w.Write(s.SiegeTargetId ?? ""); w.Write(s.SiegeTargetName ?? ""); w.Write(s.SiegeBesieger); w.Write(s.SiegeOutcome);
                w.Write(s.SiegePlayerAttacking); w.Write(s.SiegePlayerCommands); w.Write(s.SiegeRamBuilt); w.Write(s.SiegeTowerBuilt); w.Write(s.SiegeGateBroken); w.Write(s.SiegeInAssault);
                w.Write(s.SiegeDay); w.Write(s.SiegeAttackers); w.Write(s.SiegeDefenders); w.Write(s.SiegeCoinSpent); w.Write(s.SiegeLadders); w.Write(s.SiegeRamDays); w.Write(s.SiegeTowerDays); w.Write(s.SiegeAssaults); w.Write(s.SiegeLordId); w.Write(s.SiegeAttDead); w.Write(s.SiegeDefDead);
                w.Write(s.SiegeFood); w.Write(s.SiegeDefSick); w.Write(s.SiegeAttSick); w.Write(s.SiegeDefMorale); w.Write(s.SiegeRelief);
                w.Write(s.HasCastle); w.Write(s.CastleName ?? ""); w.Write(s.CastleSiteId ?? ""); w.Write(s.CastleNx); w.Write(s.CastleNz); w.Write(s.CastleStage); w.Write(s.CastleDays); w.Write(s.CastleCoin); w.Write(s.CastleObjector);
                w.Write(s.Garrison);
            }
            if (version >= 9)
            {
                w.Write(s.HasEstate); w.Write(s.EstateSettlementId ?? ""); w.Write(s.EstateName ?? ""); w.Write(s.StewardName ?? ""); w.Write(s.EstateTax); w.Write(s.EstateJustice); w.Write(s.EstateCrisis);
                w.Write(s.StewardHonesty); w.Write(s.StewardCompetence); w.Write(s.EstateUnrest); w.Write(s.EstateProsperity); w.Write(s.EstateGranary);
                w.Write(s.EstateTreasury); w.Write(s.EstateDaysSinceCourt); w.Write(s.EstateGarrison); w.Write(s.EstateIncomeMonth); w.Write(s.EstateSkimMonth); w.Write(s.EstateRevolt);
                w.Write(s.LordDeclared); w.Write(s.LordSecret); w.Write(s.LordDeals); w.Write(s.LordRefusedStephen); w.Write(s.LordRefusedMatilda); w.Write(s.LordSuspicion); w.Write(s.LordDiscovered);
                w.Write(s.HasSuit); w.Write(s.SuitHeiress ?? ""); w.Write(s.SuitDowryId ?? ""); w.Write(s.SuitFather); w.Write(s.SuitVisits); w.Write(s.SuitFavour); w.Write(s.SuitConsented); w.Write(s.SuitMarried);
                w.Write(s.HasRealm); w.Write(s.RealmTreasury); w.Write(s.RealmSeasons); w.Write(s.RealmChurch);
                var ch = s.RealmChronicle ?? new string[0]; w.Write(ch.Length); foreach (var line in ch) w.Write(line ?? "");
                w.Write(s.DaysIntoSeason); w.Write(s.NextNpcId); w.Write(s.PendingDemand);
            }
            if (version >= 10)
            {
                w.Write(s.HasPlot); w.Write(s.PlotKind); w.Write(s.PlotterKind); w.Write(s.PlotterId); w.Write(s.PlotDaysUntil); w.Write(s.PlotWarnings); w.Write(s.PlotterName ?? ""); w.Write(s.PlotGuarded); w.Write(s.PlotTasted);
                var wl = s.PlotWarningLines ?? new string[0]; w.Write(wl.Length); foreach (var line in wl) w.Write(line ?? "");
                w.Write(s.PendingRaiders); w.Write(s.EstateDaysThisMonth); w.Write(s.WarHeat);
            }
        }

        private static void WriteJob(BinaryWriter w, JobSnapshot j)
        {
            w.Write(j.Id); w.Write(j.Kind); w.Write(j.FromId ?? ""); w.Write(j.ToId ?? ""); w.Write(j.Employer ?? ""); w.Write(j.Blurb ?? "");
            w.Write(j.Pay); w.Write(j.Advance); w.Write(j.DaysRequired); w.Write(j.DaysServed); w.Write(j.DeadlineDay); w.Write(j.Danger); w.Write(j.State);
        }

        private static JobSnapshot ReadJob(BinaryReader r) => new JobSnapshot
        {
            Id = r.ReadInt32(), Kind = r.ReadByte(), FromId = r.ReadString(), ToId = r.ReadString(), Employer = r.ReadString(), Blurb = r.ReadString(),
            Pay = r.ReadInt32(), Advance = r.ReadInt32(), DaysRequired = r.ReadInt32(), DaysServed = r.ReadInt32(), DeadlineDay = r.ReadInt64(), Danger = r.ReadSingle(), State = r.ReadByte()
        };

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
            if (r.ReadBoolean()) s.Battle = ReadBattle(r, version);
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
            if (version >= 5)
            {
                int ns = r.ReadInt32(); if (ns < 0 || ns > 10000) throw new SaveCorruptException("bad settlement count");
                s.Settlements = new SettlementSnapshot[ns];
                for (int i = 0; i < ns; i++)
                {
                    var t = new SettlementSnapshot
                    {
                        Id = r.ReadString(), Name = r.ReadString(), Type = r.ReadByte(), Nx = r.ReadSingle(), Nz = r.ReadSingle(), Population = r.ReadInt32(), Wealth = r.ReadSingle(), Allegiance = r.ReadByte(), LordId = r.ReadInt32(),
                        Outbreak = r.ReadSingle(), WantedHere = r.ReadSingle(), Coastal = r.ReadBoolean(), Upland = r.ReadBoolean(), River = r.ReadBoolean(), IsFixed = r.ReadBoolean()
                    };
                    t.GoodIds = Strings(r); t.GoodStocks = Floats(r); t.GoodBaselines = Floats(r); t.GoodDemands = Floats(r); t.Shock = r.ReadByte(); t.ShockStrength = r.ReadSingle();
                    int nj = r.ReadInt32(); if (nj < 0 || nj > 1000) throw new SaveCorruptException("bad job count"); t.Jobs = new JobSnapshot[nj]; for (int k = 0; k < nj; k++) t.Jobs[k] = ReadJob(r);
                    s.Settlements[i] = t;
                }
                s.Reputation = new float[4]; for (int i = 0; i < 4; i++) s.Reputation[i] = r.ReadSingle(); s.Notoriety = r.ReadSingle();
                int nc = r.ReadInt32(); if (nc < 0 || nc > 100000) throw new SaveCorruptException("bad crime count");
                s.Crimes = new CrimeSnapshot[nc]; for (int i = 0; i < nc; i++) s.Crimes[i] = new CrimeSnapshot { Kind = r.ReadByte(), Where = r.ReadString(), Day = r.ReadInt64(), Witnesses = r.ReadInt32(), Known = r.ReadBoolean(), Tried = r.ReadBoolean() };
                s.Outlawed = r.ReadBoolean(); s.SanctuaryDaysLeft = r.ReadSingle(); s.SanctuaryExpired = r.ReadBoolean();
                if (r.ReadBoolean()) s.ActiveJob = ReadJob(r);
                s.NextJobId = r.ReadInt32(); s.HomeSettlementId = r.ReadString();
            }
            if (version >= 7)
            {
                s.ServiceRole = r.ReadByte(); s.ServiceSide = r.ReadByte(); s.ServiceCompany = r.ReadString(); s.ServiceCaptain = r.ReadString(); s.ServicePatron = r.ReadString();
                s.ServiceWage = r.ReadInt32(); s.ServiceDays = r.ReadInt32(); s.ServiceOwed = r.ReadInt32(); s.ServiceBattles = r.ReadInt32(); s.ServiceRenown = r.ReadInt32(); s.ServiceKills = r.ReadInt32(); s.ServiceLines = r.ReadInt32();
                s.ServiceInfractions = r.ReadSingle(); s.ServicePatronFavour = r.ReadSingle(); s.ServiceDeserter = r.ReadBoolean(); s.ServiceInCamp = r.ReadBoolean();
                int nc = r.ReadInt32(); if (nc < 0 || nc > 1000) throw new SaveCorruptException("bad company count");
                s.Companies = new CompanySnapshot[nc];
                for (int i = 0; i < nc; i++) s.Companies[i] = new CompanySnapshot { Name = r.ReadString(), Captain = r.ReadString(), BasedAtId = r.ReadString(), Employer = r.ReadByte(), Size = r.ReadInt32(), ContractDaysLeft = r.ReadInt32(), Reputation = r.ReadSingle() };
            }
            if (version >= 8)
            {
                s.HasSiege = r.ReadBoolean(); s.SiegeTargetId = r.ReadString(); s.SiegeTargetName = r.ReadString(); s.SiegeBesieger = r.ReadByte(); s.SiegeOutcome = r.ReadByte();
                s.SiegePlayerAttacking = r.ReadBoolean(); s.SiegePlayerCommands = r.ReadBoolean(); s.SiegeRamBuilt = r.ReadBoolean(); s.SiegeTowerBuilt = r.ReadBoolean(); s.SiegeGateBroken = r.ReadBoolean(); s.SiegeInAssault = r.ReadBoolean();
                s.SiegeDay = r.ReadInt32(); s.SiegeAttackers = r.ReadInt32(); s.SiegeDefenders = r.ReadInt32(); s.SiegeCoinSpent = r.ReadInt32(); s.SiegeLadders = r.ReadInt32(); s.SiegeRamDays = r.ReadInt32(); s.SiegeTowerDays = r.ReadInt32(); s.SiegeAssaults = r.ReadInt32(); s.SiegeLordId = r.ReadInt32(); s.SiegeAttDead = r.ReadInt32(); s.SiegeDefDead = r.ReadInt32();
                s.SiegeFood = r.ReadSingle(); s.SiegeDefSick = r.ReadSingle(); s.SiegeAttSick = r.ReadSingle(); s.SiegeDefMorale = r.ReadSingle(); s.SiegeRelief = r.ReadSingle();
                s.HasCastle = r.ReadBoolean(); s.CastleName = r.ReadString(); s.CastleSiteId = r.ReadString(); s.CastleNx = r.ReadSingle(); s.CastleNz = r.ReadSingle(); s.CastleStage = r.ReadByte(); s.CastleDays = r.ReadInt32(); s.CastleCoin = r.ReadInt32(); s.CastleObjector = r.ReadInt32();
                s.Garrison = r.ReadInt32();
            }
            if (version >= 9)
            {
                s.HasEstate = r.ReadBoolean(); s.EstateSettlementId = r.ReadString(); s.EstateName = r.ReadString(); s.StewardName = r.ReadString(); s.EstateTax = r.ReadByte(); s.EstateJustice = r.ReadByte(); s.EstateCrisis = r.ReadByte();
                s.StewardHonesty = r.ReadSingle(); s.StewardCompetence = r.ReadSingle(); s.EstateUnrest = r.ReadSingle(); s.EstateProsperity = r.ReadSingle(); s.EstateGranary = r.ReadSingle();
                s.EstateTreasury = r.ReadInt32(); s.EstateDaysSinceCourt = r.ReadInt32(); s.EstateGarrison = r.ReadInt32(); s.EstateIncomeMonth = r.ReadInt32(); s.EstateSkimMonth = r.ReadInt32(); s.EstateRevolt = r.ReadBoolean();
                s.LordDeclared = r.ReadByte(); s.LordSecret = r.ReadByte(); s.LordDeals = r.ReadInt32(); s.LordRefusedStephen = r.ReadInt32(); s.LordRefusedMatilda = r.ReadInt32(); s.LordSuspicion = r.ReadSingle(); s.LordDiscovered = r.ReadBoolean();
                s.HasSuit = r.ReadBoolean(); s.SuitHeiress = r.ReadString(); s.SuitDowryId = r.ReadString(); s.SuitFather = r.ReadInt32(); s.SuitVisits = r.ReadInt32(); s.SuitFavour = r.ReadSingle(); s.SuitConsented = r.ReadBoolean(); s.SuitMarried = r.ReadBoolean();
                s.HasRealm = r.ReadBoolean(); s.RealmTreasury = r.ReadInt32(); s.RealmSeasons = r.ReadInt32(); s.RealmChurch = r.ReadSingle();
                int nch = r.ReadInt32(); if (nch < 0 || nch > 1000) throw new SaveCorruptException("bad chronicle count"); s.RealmChronicle = new string[nch]; for (int i = 0; i < nch; i++) s.RealmChronicle[i] = r.ReadString();
                s.DaysIntoSeason = r.ReadInt32(); s.NextNpcId = r.ReadInt32(); s.PendingDemand = r.ReadByte();
            }
            if (version >= 10)
            {
                s.HasPlot = r.ReadBoolean(); s.PlotKind = r.ReadByte(); s.PlotterKind = r.ReadByte(); s.PlotterId = r.ReadInt32(); s.PlotDaysUntil = r.ReadInt32(); s.PlotWarnings = r.ReadInt32(); s.PlotterName = r.ReadString(); s.PlotGuarded = r.ReadBoolean(); s.PlotTasted = r.ReadBoolean();
                int nw = r.ReadInt32(); if (nw < 0 || nw > 100) throw new SaveCorruptException("bad warning count"); s.PlotWarningLines = new string[nw]; for (int i = 0; i < nw; i++) s.PlotWarningLines[i] = r.ReadString();
                s.PendingRaiders = r.ReadInt32(); s.EstateDaysThisMonth = r.ReadInt32(); s.WarHeat = r.ReadSingle();
            }
            return s;
        }

        private static void WriteBattle(BinaryWriter w, BattleSnapshot b, int version)
        {
            w.Write(b.SoldierCount); w.Write(b.UnitCount); w.Write(b.PlayerSoldierEnd); w.Write(b.Elapsed); w.Write(b.EnemyThinkTimer); w.Write(b.Started); w.Write(b.PlayerCharacterAlive);
            Floats(w, b.Positions); Floats(w, b.Yaws); Floats(w, b.Health); Floats(w, b.Cooldowns);
            Bytes(w, b.Alive); Bytes(w, b.Side); Ints(w, b.UnitOf); Ints(w, b.SlotOf); Ints(w, b.Targets);
            Bytes(w, b.UnitSide); Bytes(w, b.UnitFormation); Bytes(w, b.UnitOrder); Bytes(w, b.UnitAlive);
            Ints(w, b.UnitStart); Ints(w, b.UnitCount_); Ints(w, b.UnitAliveCount);
            Floats(w, b.UnitAnchor); Floats(w, b.UnitYaw); Floats(w, b.UnitTarget);
            if (version >= 6) { Bytes(w, b.UnitStance); Floats(w, b.UnitMorale); }
            if (version >= 8) { w.Write(b.SiegeGateOpen); w.Write(b.RamSeconds); }
        }

        private static BattleSnapshot ReadBattle(BinaryReader r, int version)
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
            if (version >= 6) { b.UnitStance = Bytes(r); b.UnitMorale = Floats(r); }
            if (version >= 8) { b.SiegeGateOpen = r.ReadBoolean(); b.RamSeconds = r.ReadSingle(); }
            return b;
        }

        private const int MaxArray = 4_000_000;
        private static void Floats(BinaryWriter w, float[] a) { a = a ?? new float[0]; w.Write(a.Length); for (int i = 0; i < a.Length; i++) w.Write(a[i]); }
        private static void Ints(BinaryWriter w, int[] a) { a = a ?? new int[0]; w.Write(a.Length); for (int i = 0; i < a.Length; i++) w.Write(a[i]); }
        private static void Bytes(BinaryWriter w, byte[] a) { a = a ?? new byte[0]; w.Write(a.Length); w.Write(a); }
        private static float[] Floats(BinaryReader r) { int n = r.ReadInt32(); if (n < 0 || n > MaxArray) throw new SaveCorruptException("bad array length"); var a = new float[n]; for (int i = 0; i < n; i++) a[i] = r.ReadSingle(); return a; }
        private static int[] Ints(BinaryReader r) { int n = r.ReadInt32(); if (n < 0 || n > MaxArray) throw new SaveCorruptException("bad array length"); var a = new int[n]; for (int i = 0; i < n; i++) a[i] = r.ReadInt32(); return a; }
        private static byte[] Bytes(BinaryReader r) { int n = r.ReadInt32(); if (n < 0 || n > MaxArray) throw new SaveCorruptException("bad array length"); return r.ReadBytes(n); }
    }
}
