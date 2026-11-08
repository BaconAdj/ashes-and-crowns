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
            Debug.Log($"[run] {ctx.Character.Name}, {ctx.Character.Rank} ({bg.name}); {ctx.Family.Count} kin, {ctx.Baronage.Count} minor lords; spawn {ctx.SpawnLocationId}");
            return ctx;
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
