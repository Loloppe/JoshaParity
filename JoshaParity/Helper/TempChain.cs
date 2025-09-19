using Parser.Map.Difficulty.V3.Grid;
using System.Collections.Generic;
using System.Linq;

namespace JoshaParity.Helper
{
    internal class TempChain : Note
    {
        public float TailInBeats { get; set; }

        public int TailCutDirection { get; set; }

        public float TailInSeconds { get; set; }
        public float TailBpmTime { get; set; }

        public int tx { get; set; }

        public int ty { get; set; }

        public static List<TempChain> PreprocessDataForBuffer(List<Chain> chains)
        {
            List<TempChain> result = new();
            foreach (var chain in chains)
            {
                TempChain tempChain = new()
                {
                    x = chain.x,
                    y = chain.y,
                    tx = chain.tx,
                    ty = chain.ty,
                    TailInBeats = chain.TailInBeats,
                    TailInSeconds = chain.TailInSeconds,
                    TailBpmTime = chain.TailBpmTime,
                    TailCutDirection = chain.TailCutDirection,
                    CutDirection = chain.CutDirection,
                    Beats = chain.Beats,
                    Seconds = chain.Seconds,
                    njs = chain.njs,
                    Color = chain.Color,
                    AngleOffset = 0
                };
                result.Add(tempChain);
            }

            result = result.OrderBy(x => x.Beats).ToList();

            return result;
        }

        public static List<Note> RemoveHeadNote(List<Note> notes, List<TempChain> chains)
        {
            List<Note> result = notes.ToList();
            List<Note> found = new();
            foreach (var chain in chains)
            {
                found.AddRange(notes.Where(x => x.Beats == chain.Beats && x.x == chain.x && x.y == chain.y && x.Color == chain.Color && x.CutDirection == chain.CutDirection).ToList());
            }

            found.ForEach(x => result.Remove(x));
            result = result.OrderBy(x => x.Beats).ToList();

            return result;
        }
    }
}
