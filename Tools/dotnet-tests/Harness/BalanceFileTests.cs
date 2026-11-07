using System.IO;
using System.Text.Json;
using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Harness
{
    /// <summary>Validates the committed Assets/Data/balance.json outside Unity (System.Text.Json, fields included).</summary>
    public class BalanceFileTests
    {
        private static string RepoRoot()
        {
            string dir = TestContext.CurrentContext.TestDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "CLAUDE.md"))) dir = Path.GetDirectoryName(dir);
            Assert.IsNotNull(dir, "could not locate repo root (CLAUDE.md)");
            return dir;
        }

        [Test]
        public void CommittedBalanceJson_ParsesAndValidates()
        {
            string path = Path.Combine(RepoRoot(), "UnityProject", "Assets", "Data", "balance.json");
            var options = new JsonSerializerOptions { IncludeFields = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
            var balance = JsonSerializer.Deserialize<Balance>(File.ReadAllText(path), options);
            Assert.IsNotNull(balance);
            Assert.AreEqual(50, balance.battle.soldiersPerSide, "phase 0 fields must round-trip");
            Assert.IsEmpty(BalanceValidator.Validate(balance));
        }
    }
}
