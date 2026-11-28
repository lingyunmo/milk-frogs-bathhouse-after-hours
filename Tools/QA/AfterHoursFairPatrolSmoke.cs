// Run in the isolated QA player with -afterhours-fair-patrol-smoke after copying this file into its Assets/Scripts.
using System;
using System.Collections;
using System.Collections.Generic;
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
        Transform actor;

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
                actor = Read<Transform>("actor");
                CheckBuildingBoundaries();
                CheckLocalPatrol();
                CheckQuietPatrol();
                CheckStreetReturn();
                CheckRoundClues();
                CheckWingExit(new(27,3.6f,12), "annex");
                CheckWingExit(new(10,-3.6f,12), "basement");
                CheckVisualEvidence();
                CheckLureBeforePatrol();
                CheckFootsteps();
                Debug.Log("FAIR PATROL SMOKE: PASS");
                Application.Quit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Application.Quit(81);
            }
        }

        Vector3? OutdoorCenter => Read<Vector3?>("outdoorPatrolCenter");

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
                Require(game.IsInsideBuilding(pos) && !game.OutdoorPatrolCenter().HasValue, $"Interior disclosed at {pos}.");
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
                Require(!game.IsInsideBuilding(pos) && game.OutdoorPatrolCenter() == new Vector3(pos.x,0,pos.z),
                    $"Open air misclassified at {pos}.");
            }
            PlacePlayer(new(119,0,-18));
            Require(game.Player.Vault(new(120,0,-18)) && !game.OutdoorPatrolCenter().HasValue,
                "A vault published an intermediate position.");
            PlacePlayer(new(120,0,-18));
            Debug.Log($"PATROL BOUNDARIES: {inside.Count} interiors, {outside.Length} outdoor positions; vault skipped.");
        }

        void CheckLocalPatrol()
        {
            Prepare(new(115,0,-18),new(-14,-3.6f,5),-90);
            var start = hunter.Position;
            float farthest = 0;
            for (int i=0;i<2800;i++)
            {
                if(i==900) PlacePlayer(new(27,3.6f,17));
                if(i==1800) PlacePlayer(new(32,0,35));
                hunter.Tick(Step);
                Require(!OutdoorCenter.HasValue && !hunter.Pursuing,"A quiet indoor move supplied a patrol target.");
                farthest = Mathf.Max(farthest,Vector3.Distance(hunter.Position,start));
            }
            Require(farthest > 3f, "Hunter stood still instead of patrolling its own surroundings.");
            Debug.Log($"INDOOR PRIVACY: 140 simulated seconds without a fix; local patrol displaced {farthest:F1}m.");
        }

        void CheckQuietPatrol()
        {
            Vector3 street = new(120,0,-18);
            Prepare(new(106,0,-18),street,-90);
            hunter.Tick(Step);
            Require(OutdoorCenter == street,"No initial outdoor patrol.");
            PlacePlayer(new(29,0,31.25f)); // Just inside the street laundry.
            var posts = new HashSet<int>();
            int previous = -1, arrivals = 0;
            float startedAt = -1, expiredAt = -1;
            for(int i=0;i<2400;i++)
            {
                if (i==60) PlacePlayer(new(-5,3.60f,12));
                hunter.Tick(Step);
                float t = Read<float>("elapsed");
                Require(!hunter.Pursuing && !Read<bool>("recognized"), "A quiet snapshot became a sighting.");
                if (!OutdoorCenter.HasValue) { expiredAt=t; break; }
                Require(OutdoorCenter == street, "Crossing a door or changing floor moved the outdoor fix.");
                Vector3 dest = Read<Vector3>("destination");
                float radius = Vector2.Distance(new(dest.x,dest.z),new(street.x,street.z));
                Require(!game.IsInsideBuilding(dest) && radius >= 4.99f && radius <= 10.01f, $"Invalid outdoor post: {dest}.");
                Require(Vector2.Distance(new(hunter.Position.x,hunter.Position.z),new(street.x,street.z)) >= 2.44f,
                    "Quiet route cut straight through the last known outdoor position.");
                int post = Read<int>("lastPost");
                if (post >= 0 && post != previous)
                {
                    if(startedAt < 0) startedAt=t;
                    previous=post; posts.Add(post); arrivals++;
                }
            }
            Require(posts.Count==2 && arrivals>=3, $"Expected an outdoor return leg, saw {posts.Count} posts and {arrivals} arrivals.");
            Require(startedAt>0 && expiredAt-startedAt>=29.9f && expiredAt-startedAt<=60.6f,
                $"Indoor player changed an outdoor patrol too early or too late: {startedAt} / {expiredAt}.");

            // A duty round takes precedence over another fix when the old street circuit ends.
            // Separately exercise leaving a building just after a missed check.
            Prepare(new(106,0,-18),new(-14,-3.6f,5),-90);
            hunter.Tick(Step);
            float missedAt = Read<float>("elapsed");
            float next = Read<float>("nextPatrolCheck");
            Require(next-missedAt>=29.99f && next-missedAt<=60.01f,"An indoor skip did not schedule a fresh interval.");
            Vector3 backOutside = new(245,0,-18);
            PlacePlayer(backOutside);
            for (int i=0;i<9600 && !OutdoorCenter.HasValue;i++)
            {
                hunter.Tick(Step);
                Require(!hunter.Pursuing,"Going outdoors triggered a chase.");
                if(Read<float>("elapsed") < next) Require(!OutdoorCenter.HasValue,"Leaving the building bypassed the timer.");
            }
            Require(OutdoorCenter == backOutside,"Outdoor sampling never resumed after the independent building round.");
            PlacePlayer(new(-14,-3.6f,5));
            for(int i=0;i<3200 && !Read<bool>("patrolStarted");i++) hunter.Tick(Step);
            Require(OutdoorCenter == backOutside && Read<bool>("patrolStarted") && !hunter.Pursuing,
                "Hunter failed to physically reach the new outdoor circuit.");
            Debug.Log($"OUTDOOR PATROL: {arrivals} arrivals, {expiredAt-startedAt:F2}s dwell; skipped indoors, resumed outside.");
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
                Require(!hunter.Pursuing && !OutdoorCenter.HasValue, "A silent indoor player was disclosed.");
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

            PlacePlayer(new(192,0,11)); // Outside the duty route, behind the hydrology station walls.
            bool basement = false, annex = false;
            var floors = new HashSet<int>();
            for (int i=0;i<28000 && !(basement && annex && floors.Count == 2);i++)
            {
                hunter.Tick(Step);
                Vector3 feet = hunter.Position;
                Vector2 plan = new(feet.x, feet.z);
                int floor = FloorRoutes.LevelAt(feet.y);
                if (Mathf.Abs(feet.y - FloorRoutes.LevelHeight(floor)) > .15f || FloorRoutes.IsOnStair(feet)) continue;
                if (NightAtlas.MainInterior(plan) && floor > 0) floors.Add(floor);
                basement |= floor == -1 && NightAtlas.MainInterior(plan);
                annex |= floor > 0 && NightAnnexLayout.Footprint.Contains(plan);
                Require(!OutdoorCenter.HasValue && !hunter.Pursuing, "Side rounds needed the remote indoor player's location.");
            }
            Require(basement && annex && floors.Count == 2, $"Quiet coverage missed a wing: upper floors {floors.Count}, basement {basement}, annex {annex}.");
            Debug.Log("ROUND COVERAGE: both main upper floors, the basement and the annex reached on foot.");
        }

        void CheckRoundClues()
        {
            var start = new Vector3(310, 0, -18);
            Vector3 FirstStop(Vector3 silentPlayer)
            {
                UnityEngine.Random.InitState(7163);
                Prepare(start, silentPlayer, 180);
                // Start both trials when the same scheduled round is due.
                typeof(NightHunter).GetField("nextRound", Fields).SetValue(hunter, 0f);
                hunter.Tick(Step);
                Require(Read<bool>("roundLeg") && !OutdoorCenter.HasValue, "No independent return from the far road.");
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
                Require(!OutdoorCenter.HasValue, "A committed return was redirected by an unobserved doorway crossing.");
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
                Require(!OutdoorCenter.HasValue && !hunter.Pursuing, $"Patrol from {wing} disclosed a distant indoor player.");
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
            var last = Read<Vector3>("lastSeen");
            hunter.Pose(new(29.4f, 0, 33.65f), 90);
            PlacePlayer(new(34.05f, 0, 34.65f));
            Require(Physics.Linecast(hunter.Position + Vector3.up * 1.26f, game.Player.Eyes.position,
                ~(1 << NightPlayer.PlayerLayer), QueryTriggerInteraction.Ignore), "Laundry partition missing.");
            hunter.Tick(Step);
            Require(!hunter.Pursuing && Read<Vector3>("lastSeen") == last, "A wall should break tracking.");
            PlacePlayer(new(-8, 3.6f, 12));
            for (int i = 0; i < 160; i++) hunter.Tick(Step);
            Require(Read<Vector3>("lastSeen") == last && Read<object>("activeIntent").ToString() == "Patrol",
                "The first night should return to patrol after the last sighting goes cold.");
        }

        void CheckLureBeforePatrol()
        {
            Prepare(new(105,0,-17), new(164,0,-18), 0);
            Vector3 lure = new(105,0,-23);
            hunter.HearNoise(lure, .90f, "land");
            hunter.Tick(Step);
            Require(!OutdoorCenter.HasValue &&
                Read<object>("activeIntent").ToString() == "Investigate" &&
                Vector3.Distance(Read<Vector3>("destination"), lure) < .2f,
                "A real sound should be investigated before taking a quiet snapshot.");
            Prepare(new(29,0,31.65f),new(29,0,34.5f),180);
            hunter.HearNoise(game.Player.Feet,.57f,"step");
            hunter.Tick(Step);
            Require(!OutdoorCenter.HasValue && Read<object>("activeIntent").ToString() == "Investigate" &&
                Vector3.Distance(Read<Vector3>("destination"),game.Player.Feet)<.2f,
                "An audible indoor run should lead to its source without an outdoor fix.");
        }

        void CheckFootsteps()
        {
            Prepare(new(105,0,-17), new(-5,0,12), 0);
            hunter.HearNoise(new(105,0,-23), .57f, "step");
            var cues = Read<IList>("noises");
            Require(cues.Count == 1, "A nearby run should be audible.");
            for (int i = 0; i < 12; i++) hunter.Tick(Step);
            hunter.HearNoise(new(109,0,-23), .57f, "step");
            Require(cues.Count == 1, "Consecutive steps should not relay each change of direction.");
            hunter.HearNoise(new(108,0,-22), .90f, "land");
            Require(cues.Count == 2, "A distinct impact should be audible between sampled footsteps.");
            Prepare(new(29.4f,0,33.65f), new(34.05f,0,34.65f), 90);
            hunter.HearNoise(game.Player.Feet, .33f, "step");
            Require(Read<IList>("noises").Count == 0, "Walking beyond the laundry partition gave away an exact position.");
        }

        void Prepare(Vector3 feet, Vector3 player, float yaw)
        {
            hunter.Reset();
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
