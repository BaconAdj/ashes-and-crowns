using System;
using System.Collections.Generic;

namespace AshesAndCrowns.Sim
{
    public enum DutyKind : byte { Escort = 0, Patrol = 1, Message = 2, Guard = 3, Feast = 4 }
    public enum DutyState : byte { None = 0, Given = 1, Done = 2, Failed = 3 }

    [Serializable] public sealed class HouseholdBalance
    {
        public float dutyChancePerDay = 0.35f;          // when you report at your lord's seat
        public int escortPay = 6, messagePay = 8, patrolPay = 3, guardPay = 2, feastPay = 0;
        public int escortDays = 6, messageDays = 5;
        public float escortAmbushChance = 0.35f, messageAmbushChance = 0.25f, messageCaptureChanceInHostileTown = 0.3f, patrolBanditChance = 0.4f, guardWhisperChance = 0.5f;
        public float favourPerDuty = 0.06f, favourLostOnFailure = 0.12f, favourLostOnRefusal = 0.08f;
        public int renownPerDangerousDuty = 2, infractionsOnRefusal = 5;
        public float feastInsultChance = 0.5f, feastCharismaWeight = 0.05f;
    }

    public sealed class Duty
    {
        public DutyKind Kind; public DutyState State = DutyState.Given;
        public string FromId = string.Empty, ToId = string.Empty, ToName = string.Empty, Text = string.Empty;
        public long GivenDay, DueDay; public int Pay; public bool Dangerous;
    }

    /// <summary>A household knight lives in the hall and is always on call: the duties and what they do to his standing.</summary>
    public static class HouseholdRules
    {
        public static Duty Assign(MinorLord lord, Settlement seat, IList<Settlement> settlements, long today, NameGenerator names, DeterministicRng rng, HouseholdBalance b)
        {
            if (!rng.Chance(b.dutyChancePerDay)) return null;
            var kind = (DutyKind)rng.Range(0, 5);
            var d = new Duty { Kind = kind, FromId = seat != null ? seat.Id : string.Empty, GivenDay = today };
            Settlement target = null;
            if (kind == DutyKind.Escort || kind == DutyKind.Message)
            {
                int n = 0;
                foreach (var s in settlements)
                {
                    if (seat != null && s.Id == seat.Id) continue;
                    if (s.Type == SettlementType.Village && kind == DutyKind.Message) continue;
                    if (kind == DutyKind.Message && lord != null && s.Allegiance == lord.Declared) continue;   // messages go through hostile country
                    n++; if (rng.Chance(1f / n)) target = s;
                }
                if (target == null) { kind = DutyKind.Patrol; d.Kind = kind; }
            }
            string merchant = names != null ? names.Given(false, false) : "a merchant";
            switch (d.Kind)
            {
                case DutyKind.Escort:
                    d.ToId = target.Id; d.ToName = target.Name; d.Pay = b.escortPay; d.DueDay = today + b.escortDays; d.Dangerous = true;
                    d.Text = $"\"{merchant} the wool-man goes to {target.Name} with two carts and my cut of the profit. See he gets there. {b.escortDays} days.\""; break;
                case DutyKind.Message:
                    d.ToId = target.Id; d.ToName = target.Name; d.Pay = b.messagePay; d.DueDay = today + b.messageDays; d.Dangerous = true;
                    d.Text = $"\"This goes to {target.Name}. It is sealed. If they take you, eat it. {b.messageDays} days, and do not ride by the main road.\""; break;
                case DutyKind.Patrol:
                    d.Pay = b.patrolPay; d.DueDay = today + 1; d.Dangerous = true;
                    d.Text = "\"Ride the bounds today. There were lights in the wood by the mill last night and the reeve is a coward.\""; break;
                case DutyKind.Guard:
                    d.Pay = b.guardPay; d.DueDay = today + 1;
                    d.Text = "\"You have the hall door tonight. Nobody comes in I have not named, whatever they say they carry.\""; break;
                default:
                    d.Pay = b.feastPay; d.DueDay = today + 1;
                    d.Text = "\"Stand behind my chair at the feast tonight. Say nothing, see everything, and if Ranulf's man smiles at you, do not smile back.\""; break;
            }
            return d;
        }

        public static bool Overdue(Duty d, long today) => d != null && d.State == DutyState.Given && today > d.DueDay;

        /// <summary>A duty done. Favour, renown and pay flow to the service record.</summary>
        public static string Complete(Duty d, ServiceRecord s, CharacterSheet c, HouseholdBalance b)
        {
            d.State = DutyState.Done;
            s.PatronFavour = Math.Min(1f, s.PatronFavour + b.favourPerDuty);
            if (d.Dangerous) s.Renown += b.renownPerDangerousDuty;
            c.Coins += d.Pay;
            return d.Kind switch
            {
                DutyKind.Escort => $"The carts roll into {d.ToName} with every bale. The wool-man pays you {d.Pay} pennies and calls you 'sir' as if it costs him.",
                DutyKind.Message => $"The seal is unbroken when you hand it over in {d.ToName}. Nobody says what was in it. {d.Pay} pennies, and a look that remembers your face.",
                DutyKind.Patrol => "A long wet day on the bounds. The lights were charcoal burners. Your lord nods, which is as warm as he gets.",
                DutyKind.Guard => "A night at the door. A man with a cart of wine tried to argue his way in; he is sleeping in the ditch.",
                _ => "You stood behind the chair until your knees ached and the candles guttered. You heard three things you were not meant to.",
            };
        }

        public static string Fail(Duty d, ServiceRecord s, HouseholdBalance b)
        {
            d.State = DutyState.Failed;
            s.PatronFavour = Math.Max(0f, s.PatronFavour - b.favourLostOnFailure);
            return d.Kind == DutyKind.Escort ? "The wool-man arrives without you, or not at all. Your lord hears of it before you do." : d.Kind == DutyKind.Message ? "The letter did not arrive in time. Whatever it said is now worth nothing, and so, for a while, are you." : "You were not where you were told to be. The sergeant notices; the lord notices the sergeant noticing.";
        }

        public static string Refuse(Duty d, ServiceRecord s, HouseholdBalance b)
        {
            d.State = DutyState.Failed;
            s.PatronFavour = Math.Max(0f, s.PatronFavour - b.favourLostOnRefusal); s.Infractions += b.infractionsOnRefusal;
            return "\"No?\" He does not raise his voice. The hall goes very quiet. \"Then eat somewhere else tonight.\"";
        }

        /// <summary>The feast: a rival's man insults you; Charisma decides whether you turn it or take it.</summary>
        public static string Feast(CharacterSheet c, ServiceRecord s, DeterministicRng rng, HouseholdBalance b, out bool insulted)
        {
            insulted = rng.Chance(b.feastInsultChance);
            if (!insulted) return "Roast swan, a bad harper, and your lord drunk enough to be generous. He gives you a ring from his own hand.";
            float turn = 0.4f + (c.Get(Attribute.Charisma) - 8) * b.feastCharismaWeight;
            if (rng.Chance(Math.Max(0.05f, Math.Min(0.95f, turn))))
            {
                s.PatronFavour = Math.Min(1f, s.PatronFavour + 0.04f);
                return "Ranulf's man says your spurs were bought, not won. You answer that his were won and then sold. The hall laughs with you; your lord laughs loudest.";
            }
            s.PatronFavour = Math.Max(0f, s.PatronFavour - 0.03f);
            return "Ranulf's man says your spurs were bought, not won, and you have no answer ready. The hall laughs. Not with you.";
        }
    }
}
