using System.Collections.Generic;
using AshesAndCrowns.Combat;
using AshesAndCrowns.Sim;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>Turns a character-creation choice into a living run: sheet, family, baronage, spawn.</summary>
    public static class RunFactory
    {
        public static RunContext Create(CharacterFactory.Choice choice, PlayerVitals vitals)
        {
            var boot = GameBootstrap.Instance;
            Balance balance = boot.Balance;
            BackgroundsData backgrounds = DataFiles.LoadBackgrounds();
            NamesData names = DataFiles.LoadNames();
            WorldData world = boot.World;

            var ctx = new RunContext();
            ctx.Character = CharacterFactory.Create(choice, backgrounds, vitals != null ? vitals.Body : null);
            ctx.Figures = DataFiles.LoadFigures();
            var nameGen = new NameGenerator(names, boot.Rng.Fork(41));
            BackgroundDef bg = backgrounds.Find(choice.BackgroundId);
            ctx.Family = FamilyGenerator.Generate(bg.family, ctx.Character, nameGen, boot.Rng.Fork(42));

            var anchors = new List<BaronageGenerator.Anchor>();
            var villages = new List<string>(); var castles = new List<string>();
            if (world.locations != null)
                foreach (Location l in world.locations)
                {
                    if (l.type == "village") villages.Add(l.id);
                    else { anchors.Add(new BaronageGenerator.Anchor { LocationId = l.id, Nx = l.nx, Nz = l.nz }); if (l.type == "castle" || l.type == "town") castles.Add(l.id); }
                }
            CoastData coast = DataFiles.LoadCoast();
            ctx.Baronage = BaronageGenerator.Generate(balance.baronage, anchors, coast.mapSideKm, nameGen, boot.Rng.Fork(43));
            ctx.SpawnLocationId = RunSetup.ChooseSpawn(ctx.Character, villages, castles, boot.Rng.Fork(44));
            var townScene = UnityEngine.Object.FindAnyObjectByType<TownScene>();
            if (townScene != null && !string.IsNullOrEmpty(townScene.settlementId)) ctx.SpawnLocationId = townScene.settlementId;   // a life begun in a town scene begins in that town
            ctx.EnsureSurvival(balance, DataFiles.LoadItems(), DataFiles.LoadTrades());
            ctx.Names = names;
            ctx.MapSideKm = coast.mapSideKm;
            ctx.Settlements = EnglandSetup.Generate(balance, world, coast, ctx.Items, ctx.Baronage, nameGen, boot.Rng.Fork(45));
            ctx.EnsureEngland(balance);
            ctx.HomeSettlementId = ctx.SpawnLocationId;
            var homeSettlement = ctx.FindSettlement(ctx.HomeSettlementId);
            if (homeSettlement != null) ctx.Market = homeSettlement.Market;
            StartingKit(ctx);
            Debug.Log($"[run] {ctx.Character.Name}, {ctx.Character.Rank} ({bg.name}); {ctx.Family.Count} kin, {ctx.Baronage.Count} minor lords; spawn {ctx.SpawnLocationId}");
            return ctx;
        }

        private static void StartingKit(RunContext ctx)
        {
            var inv = ctx.Inventory; var c = ctx.Character;
            inv.Add("bread", 3); inv.Add("ale", 1);
            TradeDef trade = string.IsNullOrEmpty(c.Trade) ? null : ctx.Trades.Find(c.Trade);
            if (trade != null)
            {
                if (!string.IsNullOrEmpty(trade.tool)) inv.Add(trade.tool, 1);
                if (!string.IsNullOrEmpty(trade.input)) inv.Add(trade.input, 4);
            }
            if (c.Rank == Rank.Warrior) { inv.Add("spear", 1); inv.Add("gambeson", 1); inv.Add("knife", 1); }
            else if (c.Rank == Rank.Knight) { inv.Add("sword", 1); inv.Add("mail", 1); inv.Add("helm", 1); inv.Add("shield", 1); }
            else if (c.Rank >= Rank.Lord) { inv.Add("sword", 1); inv.Add("mail", 1); inv.Add("helm", 1); inv.Add("shield", 1); inv.Add("cheese", 2); }
            else inv.Add("knife", 1);
        }

        /// <summary>A character without the screen, for test scenes and smoke tests.</summary>
        public static RunContext CreateDefault(PlayerVitals vitals)
        {
            BackgroundsData backgrounds = DataFiles.LoadBackgrounds();
            var rank = backgrounds.Rank(Rank.Warrior);
            var choice = new CharacterFactory.Choice { Name = "A nameless rider", Rank = Rank.Warrior, BackgroundId = rank.backgrounds[0].id, Allocation = new[] { 7, 7, 6, 6 } };
            return Create(choice, vitals);
        }
    }
}
