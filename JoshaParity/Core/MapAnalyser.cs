using beatleader_parser.Timescale;
using Parser.Map;
using Parser.Map.Difficulty.V3.Base;
using Parser.Map.Difficulty.V3.Grid;
using System.Collections.Generic;
using System.Linq;

namespace JoshaParity
{
    /// <summary>
    /// Handles loading and analysing a map
    /// </summary>
    public class MapAnalyser {
        private readonly Dictionary<string, List<DiffAnalysis>> _mapDiffAnalyse = [];

        public BeatmapV3 MapData { get; set; }
        public Dictionary<string, List<DiffAnalysis>> MapDiffAnalyse => _mapDiffAnalyse;

        public MapAnalyser(string mapPath, bool preRun = true, IParityMethod? parityMethod = null) {
            parityMethod ??= new GenericParityCheck();

            MapData = DiffAnalysis.parser.TryLoadPath(mapPath);
            if (preRun) AnalyseMap(parityMethod);
        }

        public void AnalyseMap(IParityMethod? parityMethod = null)
        {
            foreach (DifficultySet diffInfo in MapData.Difficulties)
            {
                DifficultyV3 data = diffInfo.Data;
                Timescale bpmHandler = Timescale.Create(MapData.Info._beatsPerMinute, data.bpmEvents, MapData.Info._songTimeOffset);
                MapObjects mapObjects = MapObjectsFromDiff(data, bpmHandler);
                MapSwingContainer swingContainer = SwingDataGeneration.Run(mapObjects, bpmHandler, parityMethod);

                string charID = diffInfo.Characteristic.ToLower();

                if (!_mapDiffAnalyse.ContainsKey(charID)) {
                    _mapDiffAnalyse.Add(charID, []);
                }

                _mapDiffAnalyse[charID].Add(new DiffAnalysis(diffInfo.Difficulty, swingContainer, bpmHandler, mapObjects));
            }
        }

        internal static MapObjects MapObjectsFromDiff(DifficultyV3 data, Timescale bpmHandler)
        {
            List<Note> notes = new(data.Notes);
            List<Bomb> bombs = new(data.Bombs);
            List<Wall> obstacles = new(data.Walls);
            List<Arc> arcs = new(data.Arcs);
            List<Chain> chains = new(data.Chains);

            notes = notes.Select(x => { x.Seconds = bpmHandler.ToRealTime(x.Beats); x.CutDirection = Validate(x.CutDirection); return x; }).ToList();
            bombs = bombs.Select(x => { x.Seconds = bpmHandler.ToRealTime(x.Beats); return x; }).ToList();
            obstacles = obstacles.Select(x => { x.Seconds = bpmHandler.ToRealTime(x.Beats); return x; }).ToList();
            arcs = arcs.Select(x => { x.Seconds = bpmHandler.ToRealTime(x.Beats); x.TailInSeconds = bpmHandler.ToRealTime(x.TailInBeats); x.CutDirection = Validate(x.CutDirection); return x; }).ToList();
            chains = chains.Select(x => { x.Seconds = bpmHandler.ToRealTime(x.Beats); x.TailInSeconds = bpmHandler.ToRealTime(x.TailInBeats); x.CutDirection = Validate(x.CutDirection); return x; }).ToList();

            return new MapObjects(notes, bombs, obstacles, arcs, chains);
        }

        internal static int Validate(int d)
        {
            return (d > 8) ? 8 : (d < 0) ? 0 : d;
        }

        /// <summary>
        /// Returns all difficulty analysis objects
        /// </summary>
        /// <returns></returns>
        public List<DiffAnalysis> GetAllDiffAnalysis()
        {
            List<DiffAnalysis> result = [];
            foreach (KeyValuePair<string, List<DiffAnalysis>> characteristicData in _mapDiffAnalyse) {
                foreach (DiffAnalysis diffAnalysis in characteristicData.Value) {
                    result.Add(diffAnalysis);
                }
            }
            return result;
        }

        /// <summary>
        /// Returns a difficulty analysis object based on Rank and Characteristic
        /// </summary>
        /// <param name="difficultyRank">Specific difficulty rank to retrieve data for</param>
        /// <param name="characteristic">Characteristic difficulty belongs to</param>
        /// <returns></returns>
        public DiffAnalysis? GetDiffAnalysis(string difficulty, string characteristic = "standard")
        {
            characteristic = characteristic.ToLower();
            if (!_mapDiffAnalyse.ContainsKey(characteristic)) return null;

            List<DiffAnalysis> diffAnalysis = _mapDiffAnalyse[characteristic];
            foreach (DiffAnalysis analysis in diffAnalysis) {
                if (analysis.difficulty == difficulty) {
                    return analysis;
                }
            }
            return null;
        }
    }
}
