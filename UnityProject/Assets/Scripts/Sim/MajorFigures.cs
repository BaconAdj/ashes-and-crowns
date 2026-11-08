using System;

namespace AshesAndCrowns.Sim
{
    /// <summary>Mirror of figures.json: the real people of the Anarchy, present every run.</summary>
    [Serializable] public sealed class MajorFigure
    {
        public string id, name, title, location, notes;
        public int age;
        public string[] personality = new string[0], goals = new string[0];
        public float loyalty;
        public int military, charisma, cunning;
        public bool alive = true;
    }

    [Serializable] public sealed class FiguresData
    {
        public MajorFigure[] major = new MajorFigure[0];
        public MajorFigure Find(string id) { foreach (var f in major) if (f.id == id) return f; return null; }
    }
}
