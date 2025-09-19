using beatleader_parser.Timescale;
using JoshaParity.Helper;
using Parser.Map.Difficulty.V3.Grid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace JoshaParity
{
    /// <summary>
    /// Contains map entities (Bombs, Walls, Notes)
    /// </summary>
    public class MapObjects
    {
        public List<Note> Notes { get; set; }
        public List<Bomb> Bombs { get; }
        public List<Wall> Obstacles { get; }
        public List<Arc> Arcs { get; }
        public List<Chain> Chains { get; }

        /// <summary>
        /// Creates a new, clean copy of MapObjects
        /// </summary>
        public MapObjects(List<Note> notes, List<Bomb> bombs, List<Wall> walls, List<Arc> arcs, List<Chain> chains)
        {
            Notes = new List<Note>(notes);
            Bombs = new List<Bomb>(bombs);
            Obstacles = new List<Wall>(walls);
            Arcs = new List<Arc>(arcs);
            Chains = new List<Chain>(chains);
        }
    }

    /// <summary>
    /// Functionality for generating swing data about a given difficulty
    /// </summary>
    public static class SwingDataGeneration
    {
        #region Variables

        private static IParityMethod ParityMethodology = new GenericParityCheck();
        public static Timescale BpmHandler = Timescale.Create(0,new(),0);
        public static MapSwingContainer mainContainer = new();
        public static MapObjects? _mapData;

        #endregion

        /// <summary>
        /// Called to check a specific map difficulty
        /// </summary>
        /// <param name="mapObjects">Map Objects</param>
        /// <param name="BPMHandler">BPM Handler for the difficulty containing base BPM and BPM Changes</param>
        /// <param name="parityMethod">Optional: Parity Check Logic</param>
        public static MapSwingContainer Run(MapObjects mapObjects, Timescale BPMHandler, IParityMethod? parityMethod = null)
        {
            ParityMethodology = parityMethod ?? new GenericParityCheck();
            BpmHandler = BPMHandler;
            mainContainer = new();
            _mapData = new(mapObjects.Notes, mapObjects.Bombs, mapObjects.Obstacles, mapObjects.Arcs, mapObjects.Chains);
            MapSwingContainer finishedState = SimulateSwings(mainContainer, _mapData);
            finishedState.SetPlayerOffsetData(CalculateOffsetData(_mapData.Obstacles));
            return finishedState;
        }

        /// <summary>
        /// Simulates swings for a given state and map data
        /// </summary>
        /// <param name="curState">Current swing container state</param>
        /// <param name="mapData">Notes, obstacles and walls</param>
        /// <returns></returns>
        public static MapSwingContainer SimulateSwings(MapSwingContainer curState, MapObjects mapData) {
            // Reference Fix and Remove Prior Notes
            MapObjects mapObjects = new(mapData.Notes, mapData.Bombs, mapData.Obstacles, mapData.Arcs, mapData.Chains);
            mapObjects.Notes.RemoveAll(x => x.Seconds * 1000 < curState.timeValue);
            mapObjects.Chains.RemoveAll(x => x.Seconds * 1000 < curState.timeValue);

            List<Note> parityObjects = TempChain.PreprocessDataForBuffer(mapObjects.Chains).Cast<Note>().ToList();

            if (parityObjects.Count == 0) { return curState; }

            int lastLeftIndex = parityObjects.FindLastIndex(x => x.Color == 0);
            int lastRightIndex = parityObjects.FindLastIndex(x => x.Color == 1);

            // Foreach note going forwards
            for (int i = 0; i < parityObjects.Count; i++)
            {
                Note currentNote = parityObjects[i];

                // Depending on hand, update buffer
                if (currentNote.Color == 0) { 
                    (SwingType leftSwingType, List<Note> leftNotesInSwing) = curState.leftHandConstructor.UpdateBuffer(currentNote);
                    if (leftSwingType != SwingType.Undecided) {
                        if (leftNotesInSwing.Count != 0) curState.AddSwing(ConfigureSwing(curState, mapObjects, leftNotesInSwing, leftSwingType, false), false);
                    }
                }
                else if (currentNote.Color == 1) { 
                    (SwingType rightSwingType, List<Note> rightNotesInSwing) = curState.rightHandConstructor.UpdateBuffer(currentNote);
                    if (rightSwingType != SwingType.Undecided) {
                        if (rightNotesInSwing.Count != 0) curState.AddSwing(ConfigureSwing(curState, mapObjects, rightNotesInSwing, rightSwingType, true), true);
                    }
                }
                
                // If this is the last swing for either hand we need to clear out the remainder of the buffer
                // This fixes the behaviour for now and it acts like it did before. May change it to just be a version
                // of update buffer without a note, then an IF earlier to remove duplicate code.
                if (i == lastLeftIndex) { 
                    curState.leftHandConstructor.EndBuffer();
                    (SwingType leftSwingType, List<Note> leftNotesInSwing) = curState.leftHandConstructor.UpdateBuffer(currentNote);
                    if (leftSwingType != SwingType.Undecided)
                    {
                        if (leftNotesInSwing.Count != 0) curState.AddSwing(ConfigureSwing(curState, mapObjects, leftNotesInSwing, leftSwingType, false), false);
                    }
                }

                if (i == lastRightIndex) { 
                    curState.rightHandConstructor.EndBuffer();
                    (SwingType rightSwingType, List<Note> rightNotesInSwing) = curState.rightHandConstructor.UpdateBuffer(currentNote);
                    if (rightSwingType != SwingType.Undecided)
                    {
                        if (rightNotesInSwing.Count != 0) curState.AddSwing(ConfigureSwing(curState, mapObjects, rightNotesInSwing, rightSwingType, true), true);
                    }
                }
            }

            return curState;
        }

        /// <summary>
        /// Calculates and returns a list of swing data for a set of map objects
        /// </summary>
        /// <param name="curState">Current swing container state</param>
        /// <param name="mapData">Information about notes, walls and obstacles</param>
        /// <param name="notes">Notes in swing</param>
        /// <param name="type">Type of swing</param>
        /// <param name="isRightHand">Right Hand Notes?</param>
        /// <returns></returns>
        internal static SwingData ConfigureSwing(MapSwingContainer curState, MapObjects mapData, List<Note> notes, SwingType type, bool isRightHand)
        {
            // Generate base swing
            bool firstSwing = false;
            if ((isRightHand && curState.RightHandSwings.Count == 0) || (!isRightHand && curState.LeftHandSwings.Count == 0)) firstSwing = true;
            SwingData sData = new(type, notes, isRightHand, firstSwing);
            sData.swingStartSeconds = BpmHandler.ToRealTime(sData.swingStartBeat);
            sData.swingEndSeconds = BpmHandler.ToRealTime(sData.swingEndBeat);

            // If first we leave
            if (firstSwing) return sData;

            // Get previous swing
            SwingData lastSwing = isRightHand ?
                curState.RightHandSwings[curState.RightHandSwings.Count - 1] :
                curState.LeftHandSwings[curState.LeftHandSwings.Count - 1];

            // Get swing EBPM
            Note lastNote = lastSwing.notes[lastSwing.notes.Count - 1];
            Note currentNote = sData.notes[0];
            sData.swingEBPM = TimeUtils.SwingEBPM(BpmHandler, lastNote.Beats, currentNote.Beats);
            if (lastSwing.IsReset) { sData.swingEBPM *= 2; }

            // Calculate Parity
            sData.swingParity = ParityMethodology.ParityCheck(ref sData, new ParityCheckContext(curState, mapData));

            // Setting angles for: Single-Note Swings
            if (sData.notes.Count == 1) {
                if (sData.notes.All(x => x.CutDirection == 8)) { 
                    SwingUtils.DotCutDirectionCalc(lastSwing, ref sData, true); 
                } else {
                    // Get Parity Dictionary
                    Dictionary<int, float> parityDict = (sData.swingParity == Parity.Backhand) ?
                        ParityUtils.BackhandDict(isRightHand) : ParityUtils.ForehandDict(isRightHand);

                    sData.SetStartAngle(parityDict[sData.notes[0].CutDirection]);
                    sData.SetEndAngle(parityDict[sData.notes[0].CutDirection]);
                }
            } else {
                // Setting angles for: Multi-note Snapped Swings
                if (sData.notes.All(x => Math.Abs(sData.notes[0].Beats - x.Beats) < 0.01f)) {
                    // Snapped all dots, else:
                    if (sData.notes.All(x => x.CutDirection == 8)) { 
                        SwingUtils.SnappedDotSwingAngleCalc(lastSwing, ref sData); 
                    } else { 
                        SwingUtils.SliderAngleCalc(ref sData); 
                    }
                } else {
                    SwingUtils.SliderAngleCalc(ref sData);
                }
            }

            // Temporary Angle Flip till lean is fully implemented:
            if (sData.upsideDown)
            {
                if (sData.notes.All(x => x.CutDirection != 8))
                {
                    sData.SetStartAngle(sData.startPos.rotation * -1);
                    sData.SetEndAngle(sData.endPos.rotation * -1);
                }
            }

            return sData;
        }

        /// <summary>
        /// Returns the Player Offset a wall influences
        /// </summary>
        /// <param name="obstacle"></param>
        /// <returns></returns>
        private static Vector2 WallImpactAssess(Wall obstacle, Wall lastObstacle)
        {
            Vector2 returnVec = Vector2.Zero;

            if ((obstacle.Width >= 3 && obstacle.x <= 1) || (obstacle.Width >= 2 && obstacle.x == 1))
            {
                returnVec.Y = -0.7f;  // Duck
            }
            else if ((obstacle.x == 1 || (obstacle.x == 0 && obstacle.Width > 1)) && (lastObstacle.x == 2) && obstacle.Beats + obstacle.DurationInBeats - (lastObstacle.Beats + lastObstacle.DurationInBeats) < 0.5f)
            {
                return new(0,-0.7f);  // Duck
            }
            else if ((obstacle.x == 2) && (lastObstacle.x == 1 || (lastObstacle.x == 0 && lastObstacle.Width > 1)) && obstacle.Beats + obstacle.DurationInBeats - (lastObstacle.Beats + lastObstacle.DurationInBeats) < 0.5f)
            {
                return new(0, -0.7f);  // Duck
            }

            if ((obstacle.x == 1 && obstacle.Width <= 1) || (obstacle.x == 0 && obstacle.Width == 2)) {
                returnVec.X = 0.55f;  // Dodge Right
            }
            else if (obstacle.x == 2)
            {
                returnVec.X = -0.55f;  // Dodge Left
            }

            return returnVec;
        }

        /// <summary>
        /// Given some obstacles, return all player head avoidance data for the map
        /// </summary>
        /// <param name="obstacles">List of V3 obstacles</param>
        /// <returns></returns>
        private static List<OffsetData> CalculateOffsetData(List<Wall> obstacles)
        {
            List<OffsetData> offsetData = new();
            Wall lastInteractive = new();

            // Old Method:
            foreach (Wall obstacle in obstacles) {
                Vector2 pOffset = WallImpactAssess(obstacle, lastInteractive);
                if (pOffset == Vector2.Zero && obstacle.Beats < lastInteractive.Beats + lastInteractive.DurationInBeats) { continue; }
                if (obstacle.Beats > lastInteractive.Beats + lastInteractive.DurationInBeats + 1f) { offsetData.Add(new() { timeValue = lastInteractive.Beats + lastInteractive.DurationInBeats + 1f, offsetValue = new(0, 0) }); }
                lastInteractive = obstacle;
                offsetData.Add(new() { timeValue = obstacle.Beats, offsetValue = pOffset });
                offsetData.Add(new() { timeValue = obstacle.Beats + obstacle.DurationInBeats, offsetValue = pOffset });
            }

            offsetData.OrderBy(x => x.timeValue);

            // Apply modifications to offsetData to maintain ducks (prevents overlap issues as frequent)
            for (int i = 0; i < offsetData.Count; i++)
            {
                // If not ducking we dont care
                if (offsetData[i].offsetValue.Y >= 0) { continue; }

                for (int j = i + 1; j < offsetData.Count; j++)
                {
                    if (offsetData[j].timeValue > offsetData[i].timeValue + 0.35f) { break; }
                    if (offsetData[j].offsetValue.Y >= 0) { offsetData[j].offsetValue.Y = offsetData[i].offsetValue.Y; }
                }
            }

            return offsetData;
        }
    }
}