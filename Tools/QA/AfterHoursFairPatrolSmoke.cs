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
                CheckQuietPatrol();
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

        void CheckQuietPatrol()
        {
            Vector3 street = new(120,0,-18);
            Prepare(new(106,0,-18), street, -90);
            hunter.Tick(Step);
            var snapshot = Read<(Vector3 Center,bool Inside)?>("patrolArea").Value;
            var posts = new HashSet<int>();
            int previous = -1, arrivals = 0;
            float startedAt = -1, retargetedAt = -1;
            Vector3 upstairs = new(-5,3.60f,12);
            for (int i = 0; i < 2100; i++)
            {
                if (i == 0 || i == 160) PlacePlayer(upstairs);
                if (i == 80) PlacePlayer(new(-12,0,-5));
                hunter.Tick(Step);
                float t = (i+1)*Step;
                var area = Read<(Vector3 Center,bool Inside)?>("patrolArea").Value;
                if (area != snapshot)
                {
                    Require(startedAt > 0 && t-startedAt >= 29.9f, $"Silent player rerouted the patrol at {t:F2}s; first arrival {startedAt:F2}s.");
                    Require(area.Inside && Mathf.Abs(area.Center.y-upstairs.y) < .1f &&
                        Vector2.Distance(new(area.Center.x,area.Center.z),new(upstairs.x,upstairs.z)) < .1f,
                        "Quiet resample did not pick the later floor.");
                    retargetedAt = t;
                    break;
                }
                Require(!hunter.Pursuing && !Read<bool>("recognized"), "A quiet snapshot was treated as sight.");
                Vector3 dest = Read<Vector3>("destination");
                float radius = Vector2.Distance(new(dest.x,dest.z),new(street.x,street.z));
                Require(Vector2.Distance(new(hunter.Position.x,hunter.Position.z),new(street.x,street.z)) >= 2.44f,
                    "Quiet route cut directly through the sampled spot.");
                Require(radius >= 4.99f && radius <= 10.01f, $"Patrol target is outside the band: {radius}.");
                int post = Read<int>("lastPost");
                if (post >= 0 && post != previous)
                {
                    if (startedAt < 0) startedAt = t;
                    previous = post;
                    posts.Add(post);
                    arrivals++;
                }
            }
            Require(posts.Count == 2 && arrivals >= 3, $"Expected a return leg, saw {posts.Count} posts and {arrivals} arrivals.");
            Require(startedAt > 0 && retargetedAt-startedAt >= 29.9f && retargetedAt-startedAt <= 60.6f,
                $"Quiet patrol changed too soon or failed to return: travel={startedAt}, resample={retargetedAt}.");
            Require(Read<Vector3>("destination").y > 3f && !hunter.Pursuing, "Returning from the street should use a real upper-floor patrol route.");
            PlacePlayer(new(160,0,-19));
            for (int i = 0; i < 3200 && !Read<bool>("patrolStarted"); i++)
            {
                hunter.Tick(Step);
                Require(!hunter.Pursuing, "The cross-floor return should not promote the snapshot to a chase.");
            }
            Require(Read<bool>("patrolStarted") && Mathf.Abs(hunter.Position.y-upstairs.y) < .4f &&
                Vector2.Distance(new(hunter.Position.x,hunter.Position.z),new(upstairs.x,upstairs.z)) <= 10.05f,
                $"Hunter failed to physically return from the street: {hunter.Position}.");
            Debug.Log($"FAIR PATROL: outdoor arrival at {startedAt:F2}s, {arrivals} arrivals, sampled again at {retargetedAt:F2}s ({retargetedAt-startedAt:F2}s dwell).");
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
            Prepare(new(105,0,-17), new(-5,0,12), 0);
            Vector3 lure = new(105,0,-23);
            hunter.HearNoise(lure, .90f, "land");
            hunter.Tick(Step);
            Require(!Read<(Vector3 Center,bool Inside)?>("patrolArea").HasValue &&
                Read<object>("activeIntent").ToString() == "Investigate" &&
                Vector3.Distance(Read<Vector3>("destination"), lure) < .2f,
                "A real sound should be investigated before taking a quiet snapshot.");
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
