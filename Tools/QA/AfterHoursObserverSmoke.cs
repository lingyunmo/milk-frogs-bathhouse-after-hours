// Copy to the isolated QA project's Assets/Scripts, then run -observer-qa <preview.jpg>.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace AfterHours
{
    internal sealed class AfterHoursObserverSmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Application.companyName == "AfterHours QA" && Environment.GetCommandLineArgs().Contains("-observer-qa"))
                new GameObject("Observer QA").AddComponent<AfterHoursObserverSmoke>();
        }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            yield return null;
            var game = FindFirstObjectByType<NightGame>();
            game.enabled = false;
            string[] files = { NightSave.FilePath, NightSave.FilePath + ".bak" };
            byte[][] original = Array.ConvertAll(files, Read);
            int result = 0;
            try
            {
                var verify = Check(game);
                while (true)
                {
                    try { if (!verify.MoveNext()) break; }
                    catch (Exception error) { Debug.LogException(error); result = 82; break; }
                    yield return verify.Current;
                }
            }
            finally
            {
                for (int i=0;i<files.Length;i++)
                    if (original[i] != null) File.WriteAllBytes(files[i], original[i]); else File.Delete(files[i]);
            }
            if (result == 0) Debug.Log("OBSERVER PASS");
            Application.Quit(result);
        }

        static byte[] Read(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;
        static void Require(bool condition, string why) { if (!condition) throw new Exception(why); }

        IEnumerator Check(NightGame game)
        {
            var doc = game.GetComponentInChildren<UIDocument>();
            var root = doc.rootVisualElement;
            void Click(string name)
            {
                var button = root.Q<Button>(name);
                Require(button != null && button.enabledInHierarchy, "Missing button " + name);
                using var evt = NavigationSubmitEvent.GetPooled();
                evt.target = button; button.SendEvent(evt);
            }
            game.NewNight();
            game.SetScreen(NightScreen.Title);
            var slot = Read(NightSave.FilePath);
            var backup = Read(NightSave.FilePath + ".bak");
            Click("observe");
            Require(game.EmptyObservation && game.ScreenState == NightScreen.Observer && !game.OutdoorPatrolCenter().HasValue, "Title entry or privacy failed.");
            var watch = game.Watch;
            var hunter = watch.Hunter;
            Vector3 start = hunter.Position;
            game.Player.ResetPosition(start + Vector3.left, 0);
            Click("watch-speed"); Click("watch-speed");
            Require(watch.Speed == 4, "Speed button failed.");
            Click("watch-pause");
            game.AdvanceWatch(.2f);
            Require(watch.Paused && Time.timeScale == 0 && hunter.Position == start, "Paused hunter moved.");
            Click("watch-pause");
            bool sawStreet = false, sawUpper = false;
            for(int i=0;i<2200;i++)
            {
                game.AdvanceWatch(.05f);
                Require(!hunter.Pursuing && !game.OutdoorPatrolCenter().HasValue, "Ghost attracted the hunter.");
                sawStreet |= game.Atlas.FloorAt(hunter.Position) == NightAtlas.Street && watch.Trail.Count > 8;
                if (FloorRoutes.LevelAt(hunter.Position.y) > 0 && !FloorRoutes.IsOnStair(hunter.Position) &&
                    hunter.CurrentGoal.HasValue && Mathf.Abs(hunter.Position.y - hunter.CurrentGoal.Value.y) < .15f &&
                    Vector3.Distance(hunter.Position, hunter.CurrentGoal.Value) > 3) { sawUpper = true; break; }
            }
            Require(sawStreet && sawUpper && watch.Trail.Count <= 240, "Could not follow the real hunter from the road upstairs.");
            watch.Frame(2);
            var ui = (NightInterface)typeof(NightGame).GetField("hud",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);
            for(int i=0;i<15;i++) { ui.Tick(); yield return null; }
            Require(root.Q<NightMapView>().worldBound.width > 180 && root.Q<Button>("watch-back").worldBound.width > 30,
                "Map or controls missing from rendered panel.");
            var capture = Screenshot(doc,watch.Camera);
            while(capture.MoveNext()) yield return capture.Current;
            Click("watch-back");
            Require(game.Watch == null && game.ScreenState == NightScreen.Title && Time.timeScale == 0 &&
                slot.SequenceEqual(Read(NightSave.FilePath)) && (backup ?? Array.Empty<byte>()).SequenceEqual(Read(NightSave.FilePath+".bak") ?? Array.Empty<byte>()),
                "Rehearsal wrote a save or failed to leave.");
            game.ContinueNight();
            game.SetScreen(NightScreen.Pause);
            Click("observe-live");
            var body = game.Player.Feet;
            var live = game.Watch;
            Require(!live.Rehearsal && live.Hunter == hunter && root.Q<Button>("watch-speed") == null, "Live session restarted the hunter.");
            live.SetPaused(true); live.CycleSpeed();
            game.AdvanceWatch(.02f);
            Require(!live.Paused && live.Speed == 1 && Vector3.Distance(game.Player.Feet, body) < .12f, "Live camera moved or protected the player.");
            Click("watch-back");
            var eye = game.Player.Eyes;
            Require(game.ScreenState == NightScreen.Play && eye.GetComponent<Camera>().enabled &&
                eye.GetComponent<AudioListener>().enabled && !live.Camera.gameObject.activeSelf, "First person view was not restored.");
            game.EnterObserver();
            game.Captured();
            Require(game.ScreenState == NightScreen.Capture && game.Watch == null && eye.GetComponent<Camera>().enabled, "Live observation prevented capture.");
        }

        IEnumerator Screenshot(UIDocument doc, Camera camera)
        {
            var args = Environment.GetCommandLineArgs();
            var output = args[Array.IndexOf(args,"-observer-qa")+1];
            var target = new RenderTexture(1280,720,24);
            target.Create();
            var panel = doc.panelSettings;
            var clear = panel.clearColor;
            var active = RenderTexture.active;
            panel.targetTexture=target; panel.clearColor=false;
            try
            {
                yield return null;
                yield return new WaitForEndOfFrame();
                camera.targetTexture=target;
                camera.Render();
                var renderer=typeof(UIDocument).Assembly.GetType("UnityEngine.UIElements.UIElementsRuntimeUtility",true);
                renderer.GetMethod("RenderOffscreenPanels",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,null);
                RenderTexture.active=target;
                var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
                File.WriteAllBytes(output,image.EncodeToJPG(88));
                Destroy(image);
            }
            finally
            {
                camera.targetTexture=null; panel.targetTexture=null; panel.clearColor=clear;
                RenderTexture.active=active; target.Release(); Destroy(target);
            }
        }
    }
}
