// Run in the isolated QA player with -afterhours-fair-patrol-smoke after copying this file into its Assets/Scripts.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace AfterHours
{
    internal sealed class AfterHoursFairPatrolSmoke : MonoBehaviour
    {
        const float Step = .05f;
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        NightGame game;
        NightHunter hunter;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Application.companyName == "AfterHours QA" &&
                Array.IndexOf(Environment.GetCommandLineArgs(), "-afterhours-fair-patrol-smoke") >= 0)
                new GameObject("Solo pursuit QA").AddComponent<AfterHoursFairPatrolSmoke>();
        }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            yield return null;
            try
            {
                game = FindFirstObjectByType<NightGame>();
                game.enabled = false;
                hunter = (NightHunter)typeof(NightGame).GetField("hunter", Fields).GetValue(game);
                CheckBuildingBoundaries();
                CheckFixes();
                CheckTorch();
                CheckStreetReturn();
                CheckRoundClues();
                CheckWingExit(new(27,3.6f,12), "annex");
                CheckWingExit(new(10,-3.6f,12), "basement");
                CheckVisualEvidence();
                CheckRunningChase();
                game.State.night=NightState.MaxNights;
                CheckRunningChase();
                game.State.night=1;
                CheckLureBeforePatrol();
                CheckFootsteps();
                CheckExitCapture();
                Debug.Log("FAIR PATROL SMOKE: PASS");
                Application.Quit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Application.Quit(81);
            }
        }

        Vector3? Fix => Read<Vector3?>("positionFix");
        void Set(string field, object value) => typeof(NightHunter).GetField(field, Fields).SetValue(hunter,value);

        void CheckBuildingBoundaries()
        {
            var inside = new List<Vector3> {
                new(-3.5f,3.6f,18), new(-4,7.2f,-6), new(0,0,-6), new(-14,-3.6f,5),
                new(-22,-3.6f,5), new(14,1.8f,5), new(27,3.6f,18), new(35,7.2f,7),
                new(20,3.6f,12), new(20,7.2f,12), new(41,NightAnnexLayout.RampHeight(13),13),
                new(5.4f,.45f,36.6f), new(29,0,31.25f), new(192,0,8.25f)
            };
            foreach (var room in NightWorld.StreetRoomAreas) inside.Add(new(room.Bounds.center.x,0,room.Bounds.center.y));
            foreach (var room in NightWorld.RiverBuildings) inside.Add(new(room.Bounds.center.x,0,room.Bounds.center.y));
            for (int i=1;i<=ReturnWing.Rooms.Length;i++) inside.Add(new(ReturnWing.X(i),0,36));
            foreach (var pos in inside)
            {
                PlacePlayer(pos);
                Require(game.IsInsideBuilding(pos), $"Interior disclosed at {pos}.");
            }
            Vector3[] outside = {
                new(29,0,30.75f), new(192,0,7.75f), new(29,0,18), new(20,0,12), new(41,0,13),
                new(10,3.6f,26), new(-21.3f,1.90f,5), new(-11.55f,1.90f,25.7f), new(10,5.5f,25.4f),
                new(5.4f,.28f,34.57f), new(0,0,34.5f), new(-22,0,5), new(40.3f,0,-5.8f),
                new(102,0,-9), new(53,-.86f,3), new(225,0,-18)
            };
            foreach (var pos in outside)
            {
                PlacePlayer(pos);
                Require(!game.IsInsideBuilding(pos),
                    $"Open air misclassified at {pos}.");
            }
        }

        void CheckFixes()
        {
            var roll = typeof(NightHunter).GetMethod("UpdateTracking",Fields);
            int hits = 0;
            // Isolate the simulation clock from all other consumers of Unity's random stream.
            foreach (var place in new[]{new Vector3(156,0,4),new Vector3(176,0,-18)})
            {
                Prepare(new(156,0,-18),place,180);
                Set("nextFix",3f);
                UnityEngine.Random.InitState(8321+(int)place.x);
                int localHits = 0;
                for(int n=1;n<=500;n++)
                {
                    float due=n*3;
                    Set("positionFix",null);
                    Set("elapsed",due-.01f);
                    roll.Invoke(hunter,new object[]{false,.01f});
                    Require(!Fix.HasValue && Read<float>("nextFix")==due,"Early position read");
                    var rng=UnityEngine.Random.state;
                    bool expect=UnityEngine.Random.value<.10f;
                    UnityEngine.Random.state=rng;
                    Set("elapsed",due);
                    roll.Invoke(hunter,new object[]{false,.01f});
                    Require(Fix.HasValue==expect && Read<float>("nextFix")==due+3,"Wrong roll or interval");
                    if(!expect) continue;
                    localHits++;
                    Require(Fix==game.Player.Feet,"Remote fix lost its exact floor or position");
                }
                Require(localHits>25 && localHits<75,"Unexpected fix frequency");
                hits+=localHits;
            }
            // The same winning roll must miss a quiet target beyond the hard distance cap.
            Prepare(new(106,0,-18),new(132,0,-18),-90);
            for(int seed=1;seed<1000;seed++)
            {
                UnityEngine.Random.InitState(seed);
                var state=UnityEngine.Random.state;
                if(UnityEngine.Random.value>=.10f) continue;
                UnityEngine.Random.state=state; Set("nextFix",Step); break;
            }
            var winningRoll = UnityEngine.Random.state;
            hunter.Tick(Step);
            Require(!Fix.HasValue && !Read<bool>("tracking") && !hunter.Pursuing,"Long-range position leaked beyond 24m");
            Prepare(new(106,0,-18),new(129.8f,0,-18),-90);
            Set("nextFix",Step);
            UnityEngine.Random.state=winningRoll;
            hunter.Tick(Step);
            Require(Fix==game.Player.Feet && hunter.Pursuing,"Successful remote fix did not drive a pursuit route");
            var snapshot=Fix;
            PlacePlayer(new(185,0,-18));
            for(int n=0;n<32;n++) hunter.Tick(Step);
            Require(Fix==snapshot && (Read<Vector3>("destination")-snapshot.Value).sqrMagnitude<.1f,
                "Remote fix followed a silent relocation before another roll");
            Debug.Log($"FIX PASS {hits}/1000 exact reads over indoor/outdoor trials; failed rolls and remote snapshot verified");
        }

        void CheckTorch()
        {
            Prepare(new(97,0,-18),new(118,0,-18),-90);
            Set("nextDecision",3f);
            game.Player.SetTorch(true);
            game.Player.Eyes.LookAt(hunter.Position+Vector3.up*1.26f);
            for (int i=0;i<26;i++) hunter.Tick(Step);
            Require(!hunter.Pursuing && !Read<bool>("recognized"),"Direct beam revealed its source from beyond its range");
            Prepare(new(97,0,-18),new(114,0,-18),-90);
            Set("nextDecision",3f);
            game.Player.SetTorch(true);
            game.Player.Eyes.LookAt(hunter.Position+Vector3.up*1.26f);
            for (int i=0;i<26 && !hunter.Pursuing;i++) hunter.Tick(Step);
            Require(hunter.Pursuing && Read<bool>("recognized"),"Torch beam at 17m did not reveal its source");
        }

        void CheckStreetReturn()
        {
            var street = new Vector3(150, 0, -18);
            Prepare(street, street + Vector3.right * 24, 180);
            Vector3 impact = street + Vector3.left * 6;
            hunter.HearNoise(impact, .90f, "land");
            hunter.Tick(Step);
            Require(Read<object>("activeIntent").ToString() == "Investigate", "Street bait was ignored.");
            PlacePlayer(new(-14, -3.6f, 5));
            int returns = 0, departures = 0;
            bool checkedUpper = false;
            for (int i = 0; i < 14400 && departures < 2; i++)
            {
                hunter.Tick(Step);
                Vector3 feet = hunter.Position;
                bool main = NightAtlas.MainInterior(new(feet.x, feet.z));
                Require(!hunter.Pursuing && !Fix.HasValue, "A silent indoor player was disclosed.");
                if (main && FloorRoutes.LevelAt(feet.y) >= 1 && Mathf.Abs(feet.y - FloorRoutes.LevelHeight(FloorRoutes.LevelAt(feet.y))) < .15f)
                {
                    if (!checkedUpper) returns++;
                    checkedUpper = true;
                }
                if (checkedUpper && !main && !game.IsInsideBuilding(feet) && (feet.x > 23 || feet.z < -16 || feet.z > 30))
                {
                    departures++;
                    checkedUpper = false;
                }
            }
            Require(departures == 2, $"After street bait: {returns} upper-floor visits, {departures} departures in 12 simulated minutes; last feet {hunter.Position}.");
            Debug.Log($"STREET RETURN: two quiet street / main upper floor / street circuits in {Read<float>("elapsed"):F1}s.");

            PlacePlayer(new(270,0,15)); // The closed cold store keeps the patrol trial free of real sightings.
            bool basement = false, annex = false, eastBank = false;
            var floors = new HashSet<int>();
            for (int i=0;i<38000 && !(eastBank && basement && annex && floors.Count == 2);i++)
            {
                hunter.Tick(Step);
                Vector3 feet = hunter.Position;
                Vector2 plan = new(feet.x, feet.z);
                int floor = FloorRoutes.LevelAt(feet.y);
                if (Mathf.Abs(feet.y - FloorRoutes.LevelHeight(floor)) > .15f || FloorRoutes.IsOnStair(feet)) continue;
                if (NightAtlas.MainInterior(plan) && floor > 0) floors.Add(floor);
                eastBank |= feet.x > 290f && !game.IsInsideBuilding(feet);
                basement |= floor == -1 && NightAtlas.MainInterior(plan);
                annex |= floor > 0 && NightAnnexLayout.Footprint.Contains(plan);
                Require(!Fix.HasValue && !hunter.Pursuing, "Side rounds needed the remote indoor player's location.");
            }
            Require(basement && annex && floors.Count == 2, $"Quiet coverage missed a wing: upper floors {floors.Count}, basement {basement}, annex {annex}.");
            Require(eastBank, "Quiet patrol never reached the far eastern levee.");
            Debug.Log("ROUND COVERAGE: both main upper floors, basement, annex and east bank beyond x=290 reached on foot.");
        }

        void CheckRoundClues()
        {
            var start = new Vector3(310, 0, -18);
            Vector3 FirstStop(Vector3 silentPlayer)
            {
                UnityEngine.Random.InitState(7163);
                Prepare(start, silentPlayer, 180);
                // Start both trials when the same scheduled round is due.
                hunter.Tick(Step);
                Require(Read<bool>("roundLeg") && !Fix.HasValue, "No independent return from the far road.");
                return Read<Vector3>("destination");
            }
            Vector3 hall = FirstStop(new(27, 3.6f, 17));
            Require(FirstStop(new(-14, -3.6f, 5)) == hall, "Changing the unseen player's building changed the planned hall.");
            PlacePlayer(new(200, 0, -18));
            Vector3 impact = start + Vector3.left * 5;
            hunter.HearNoise(impact, .90f, "land");
            for (int i=0;i<15 && Read<object>("activeIntent").ToString() != "Investigate";i++) hunter.Tick(Step);
            Require(!Read<bool>("roundLeg") && Read<object>("activeIntent").ToString() == "Investigate" &&
                Vector3.Distance(Read<Vector3>("destination"), impact) < .2f, "A new impact failed to interrupt the return.");
            bool investigated = false, resumed = false, reached = false;
            for (int i=0;i<7000;i++)
            {
                if (i==460) PlacePlayer(new(35,7.2f,7));
                hunter.Tick(Step);
                Require(!Fix.HasValue, "A committed return was redirected by an unobserved doorway crossing.");
                investigated |= Vector3.Distance(hunter.Position, impact) < 1;
                resumed |= investigated && Read<bool>("roundLeg");
                if (resumed && FloorRoutes.LevelAt(hunter.Position.y) > 0 &&
                    NightAtlas.MainInterior(new(hunter.Position.x, hunter.Position.z))) { reached = true; break; }
            }
            Require(reached, $"Patrol never resumed from the end of the road after an impact; last feet {hunter.Position}.");
            Debug.Log("ROUND CLUES: identical first hall from two indoor hiding areas; investigated an impact and returned from the far road.");
        }

        void CheckWingExit(Vector3 spawn, string wing)
        {
            Prepare(spawn, new(192,0,11), 90);
            bool visitedMain = false, left = false;
            for(int i=0;i<6800;i++)
            {
                hunter.Tick(Step);
                Vector3 feet = hunter.Position;
                Require(!Fix.HasValue && !hunter.Pursuing, $"Patrol from {wing} disclosed a distant indoor player.");
                visitedMain |= NightAtlas.MainInterior(new(feet.x, feet.z)) && FloorRoutes.LevelAt(feet.y) == 0;
                if (visitedMain && !game.IsInsideBuilding(feet) && (feet.x > 23 || feet.z < -16 || feet.z > 30))
                { left = true; break; }
            }
            Require(left, $"Hunter failed to leave {wing} via the main ground floor; last feet {hunter.Position}.");
            Debug.Log($"WING ROUND: left {wing}, visited main ground floor, and returned outdoors.");
        }

        void CheckVisualEvidence()
        {
            Prepare(new(29,0,31.65f), new(29,0,34.5f), 0);
            // Isolate reaction time from an unrelated patrol departure.
            typeof(NightHunter).GetField("nextDecision", Fields).SetValue(hunter, 2f);
            for (int i = 0; i < 4; i++) hunter.Tick(Step);
            Require(!hunter.Pursuing, "A 200 ms glimpse should leave time to take cover.");
            for (int i = 0; i < 25 && !hunter.Pursuing; i++) hunter.Tick(Step);
            Require(hunter.Pursuing, "Continuous unobstructed sight should start pursuit.");
            hunter.Pose(new(29.4f, 0, 33.65f), 90);
            PlacePlayer(new(34.05f, 0, 34.65f));
            Require(Physics.Linecast(hunter.Position + Vector3.up * 1.26f, game.Player.Eyes.position,
                ~(1 << NightPlayer.PlayerLayer), QueryTriggerInteraction.Ignore), "Laundry partition missing.");
            hunter.Tick(Step);
            for(int i=0;i<15;i++) hunter.Tick(Step);
            Require(hunter.Pursuing && Read<bool>("tracking") && game.ScreenState!=NightScreen.Capture &&
                (Read<Vector3>("destination")-game.Player.Feet).sqrMagnitude<1,
                "A real corner broke nearby pursuit or allowed capture through a wall");
            var last = Read<Vector3>("lastSeen");
            PlacePlayer(new(122,0,-18));
            for (int i=0;i<30;i++) hunter.Tick(Step);
            Require(Read<bool>("tracking") && Read<Vector3>("lastSeen")==last,"Short separation broke tracking or telegraphed a remote target");
            for (int i=0;i<15;i++) hunter.Tick(Step);
            Require(!hunter.Pursuing && !Read<bool>("tracking") && Read<Vector3>("lastSeen")==last,
                "A distant silent player could not escape");
            float searchEnds = Read<float>("searchUntil");
            Require(searchEnds - Read<float>("elapsed") >= 5f, "Last-sighting search expired before the hunter reached it");
            while (Read<float>("elapsed") < searchEnds)
            {
                hunter.Tick(Step);
                Require(!Fix.HasValue && !hunter.Pursuing && Read<Vector3>("lastSeen") == last,
                    "Searching the old sighting disclosed the runner's new position");
            }
        }

        void CheckRunningChase()
        {
            var start = new Vector3(60,0,-18);
            var playerStart = start + Vector3.right * 8;
            Prepare(start, playerStart, 90);
            Set("nextDecision", 2f);
            for(int i=0;i<30 && !hunter.Pursuing;i++) hunter.Tick(Step);
            Require(hunter.Pursuing,"Could not acquire running target in the street");
            for(int i=1;i<=200;i++)
            {
                PlacePlayer(playerStart + Vector3.right*(3.55f*Step*i));
                // Keep an unobstructed beam aimed at the hunter so the first 10 seconds measure speed.
                game.Player.SetTorch(true);
                game.Player.Eyes.LookAt(hunter.Position+Vector3.up*1.26f);
                hunter.Tick(Step);
            }
            float advance=hunter.Position.x-start.x;
            float leadGained=(game.Player.Feet.x-hunter.Position.x)-(playerStart.x-start.x);
            Require(advance>20f && advance<28.5f && leadGained>7f,"Sprint could not open a gap: "+advance+" / "+leadGained);
            for(int i=201;i<=300;i++)
            {
                PlacePlayer(playerStart + Vector3.right*(3.55f*Step*i));
                hunter.Tick(Step);
            }
            Require(!Read<bool>("tracking") && !hunter.Pursuing,"A runner outside the reduced sight and retention ranges stayed locked");
            Debug.Log($"OPEN ROAD CHASE: night {game.State.night}, hunter advanced {advance:F1}m; runner gained {leadGained:F1}m in 10s");
        }

        void CheckLureBeforePatrol()
        {
            Prepare(new(105,0,-17), new(164,0,-18), 0);
            Vector3 lure = new(105,0,-23);
            hunter.HearNoise(lure, .90f, "land");
            hunter.Tick(Step);
            Require(!Fix.HasValue &&
                Read<object>("activeIntent").ToString() == "Investigate" &&
                Vector3.Distance(Read<Vector3>("destination"), lure) < .2f,
                "A real sound should be investigated before taking a quiet snapshot.");
            Prepare(new(29,0,31.65f),new(29,0,34.5f),180);
            hunter.HearNoise(game.Player.Feet,.57f,"step");
            hunter.Tick(Step);
            Require(!Fix.HasValue && Read<object>("activeIntent").ToString() == "Investigate" &&
                Vector3.Distance(Read<Vector3>("destination"),game.Player.Feet)<.2f,
                "An audible indoor run should lead to its source without an outdoor fix.");
        }

        void CheckFootsteps()
        {
            Prepare(new(105,0,-17), new(-5,0,12), 0);
            hunter.HearNoise(new(105,0,-35), .33f, "step");
            Require(Read<IList>("noises").Count==0,"Walking at 18m exceeded the reduced range");
            hunter.HearNoise(new(105,0,-29), .33f, "step");
            var cues = Read<IList>("noises");
            Require(cues.Count == 1, "A walk at 12 metres should be audible.");
            for (int i = 0; i < 12; i++) hunter.Tick(Step);
            hunter.HearNoise(new(109,0,-23), .57f, "step");
            Require(cues.Count == 1, "Consecutive steps should not relay each change of direction.");
            hunter.HearNoise(new(108,0,-22), .90f, "land");
            Require(cues.Count == 2, "A distinct impact should be audible between sampled footsteps.");
            Prepare(new(29.4f,0,33.65f), new(34.05f,0,34.65f), 90);
            hunter.HearNoise(game.Player.Feet, .33f, "step");
            Require(Read<IList>("noises").Count == 1, "Nearby walking behind a partition was inaudible.");
            hunter.Tick(Step);
            Require(Read<object>("activeIntent").ToString()=="Investigate","Obstructed walking failed to interrupt patrol");
            Prepare(new(100,0,-18),new(150,0,-18),180);
            hunter.HearNoise(new(132,0,-18), .57f, "step");
            Require(Read<IList>("noises").Count==0, "A sprint at 32m exceeded the reduced range");
            hunter.HearNoise(new(122,0,-18), .57f, "step");
            Require(Read<IList>("noises").Count==1, "A nearby outdoor sprint at 22m was inaudible");
            Prepare(new(100,0,-18),new(150,0,-18),180);
            hunter.HearNoise(new(120,0,-18), .13f, "step");
            Require(Read<IList>("noises").Count==0, "Distant crouched steps sounded like running");
        }

        void CheckExitCapture()
        {
            var files = new[] { NightSave.FilePath, NightSave.FilePath + ".bak" };
            var previous = Array.ConvertAll(files, path => File.Exists(path) ? File.ReadAllBytes(path) : null);
            try
            {
                // The entrance in the south wall of the hydrology station opens directly onto the levee.
                game.NewNight();
                Prepare(new(192, 0, 10.8f), new(192, 0, 14f), 0);
                Set("nextDecision", 2f);
                for (int i = 0; i < 30 && !hunter.Pursuing; i++) hunter.Tick(Step);
                Require(hunter.Pursuing, "Hunter did not recognise the player inside the station.");
                PlacePlayer(new(192, 0, 4));
                for (int i = 0; i < 280 && game.ScreenState != NightScreen.Capture; i++) hunter.Tick(Step);
                Require(game.ScreenState == NightScreen.Capture && !game.IsInsideBuilding(hunter.Position) &&
                    Vector3.Distance(hunter.Position, game.Player.Feet) < 1f,
                    $"Did not follow the player through the eastern doorway for a close capture: {hunter.Position}");
                Debug.Log("STREET CAPTURE: sight indoors, physical pursuit through the station exit, close capture on the levee.");
            }
            finally
            {
                for (int i = 0; i < files.Length; i++)
                    if (previous[i] != null) File.WriteAllBytes(files[i], previous[i]); else File.Delete(files[i]);
            }
        }

        void Prepare(Vector3 feet, Vector3 player, float yaw)
        {
            hunter.Reset();
            Set("nextFix",float.PositiveInfinity); // Exercise real sensory clues in isolation except in CheckFixes.
            hunter.Pose(feet, yaw);
            PlacePlayer(player);
        }

        void PlacePlayer(Vector3 feet)
        {
            game.Player.ResetPosition(feet + Vector3.up * .035f, 0);
            game.Player.SetTorch(false);
            game.Player.SettleView(.5f);
            Physics.SyncTransforms();
        }

        T Read<T>(string field) => (T)typeof(NightHunter).GetField(field, Fields).GetValue(hunter);

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
