using System;
using UnityEngine;

namespace AfterHours
{
    internal static class RiverSurvey
    {
        internal static readonly Vector3 CameraDesk = new(196, .98f, 19.8f);
        internal static readonly Vector3 Trays = new(249, .98f, 13.2f);
        internal static readonly Vector3 ContactPrinter = new(254, 1.02f, 18.8f);
        // All measurements face south. The two survey pins must be visible in the same exposure.
        internal static readonly (string Name, Vector3 Center)[] Plates =
        {
            ("停车棚西侧", new(43, 1.55f, 7.5f)), ("水文站南墙", new(204, 1.55f, 7.70f)),
            ("闸井房南墙", new(320, 1.55f, 11.70f)), ("后巷洗衣店", new(32, 1.55f, 30.68f)),
            ("旧照相馆", new(251, 1.55f, 4.69f)), ("守闸宿舍", new(291, 1.55f, -10.30f))
        };
        internal static string Subject(int i) => i < 3 ? "洪痕" : "旧址";
        internal static bool Recorded(NightState s, int i) => (s.survey.negatives & (1 << i)) != 0;
        internal static Vector3 Pin(int i, int side) => Plates[i].Center + new Vector3(side * .86f, side * .22f, -.05f);

        internal static (string action, string place) Lead(NightState s)
        {
            if (!s.survey.camera) return ("领测绘相机", "石桥路向东 · 水文站值班厅");
            if (s.survey.soundings < 3) return ("把测深艇开到金色浮标", "水文站值班厅 · 遥测台");
            for (int i = 0; i < 3; i++)
                if (!Recorded(s,i)) return ("拍下两枚洪痕白标", Plates[i].Name + "　·　V 取景");
            return ("冲洗三张水尺底片", "东堤 · 旧照相馆暗房");
        }
    }

    [Serializable]
    internal sealed class SurveyFilm
    {
        public bool camera, printed, archive, balanced, spliced;
        public int leftStrip = -2, rightStrip = 3;
        public int weirs, head;
        public float bellReadyAt, boatMetres, boatOffset;
        public int soundings;
        public int negatives, sheets;
        public string[] prints = new string[3];
        internal bool CanPrint => (negatives & 7) == 7 && !printed;
        internal bool ReportReady => printed && soundings == 3;
        internal bool HasSites => (negatives & 56) == 56;
        internal bool CanRestore => HasSites && spliced && !archive;
        internal void Normalize()
        {
            soundings = Mathf.Clamp(soundings,0,3);
            boatMetres = float.IsFinite(boatMetres) ? Mathf.Clamp(boatMetres,0,SoundingReach.Length) : 0;
            boatOffset = float.IsFinite(boatOffset) ? Mathf.Clamp(boatOffset,-13,13) : 0;
            negatives &= 63;
            weirs &= 7;
            if (!float.IsFinite(bellReadyAt) || bellReadyAt < 0) bellReadyAt = 0;
            head = Mathf.Clamp(head, 0, 6);
            leftStrip = Mathf.Clamp(leftStrip,-4,4);
            rightStrip = Mathf.Clamp(rightStrip,-4,4);
            sheets = Mathf.Clamp(sheets, 0, 3);
            if (prints == null || prints.Length != 3) Array.Resize(ref prints, 3);
        }
    }

    internal sealed partial class NightGame
    {
        internal bool Framing { get; private set; }
        internal bool Processing { get; private set; }
        internal bool Splicing { get; private set; }
        internal bool RightStrip { get; private set; }
        internal bool AlbumOpen { get; private set; }
        internal float Density { get; private set; }
        internal bool Sluicing { get; private set; }
        internal int SelectedGate { get; private set; }
        internal float SteadyFlow { get; private set; }
        internal int AlbumSheet { get; private set; }
        internal int Flow => ((State.survey.weirs & 1) != 0 ? 1 : 0) + ((State.survey.weirs & 2) != 0 ? 2 : 0)
            + ((State.survey.weirs & 4) != 0 ? 4 : 0);
        internal int Subject { get; private set; } = -1;
        internal bool Aligned { get; private set; }
        internal float FlashUntil { get; private set; }
        internal Vector2[] Pins { get; } = new Vector2[2];
        float darkroomAge;
        internal bool AlbumIsArchive { get; private set; }

        void BuildSurveyTargets()
        {
            BuildSoundingTarget();
            Target(RiverSurvey.CameraDesk, "领测绘相机", .65f, () =>
            {
                State.survey.camera = true;
                Emit(RiverSurvey.CameraDesk, .22f, "tool");
                Notify("V 举起相机。退几步，让一对标钉都进框，再按 F。", 5);
                SaveProgress();
            }, () => WorkingOn(FieldOrder.Survey) && !State.survey.camera);
            Target(RiverSurvey.Trays, "冲洗水尺底片", .45f, () =>
            {
                CloseDispatch();
                Processing = true;
            }, () => WorkingOn(FieldOrder.Survey) && State.survey.CanPrint);
            Target(RiverSurvey.Trays, "查看水尺照片", 0, () => OpenSurveyAlbum(false),
                () => State.survey.printed && !State.detained);
            Target(RiverSurvey.ContactPrinter, "查看翻拍委托", 0, () =>
                Notify("「谁还记得八七年的水？后巷洗衣店、照相馆、守闸宿舍各留了两颗钉。照原位各拍一张，我替你把合影印出来。」", 8),
                () => WorkingOn(FieldOrder.Archive) && State.survey.camera && !State.survey.CanRestore && !State.survey.archive);
            Target(new Vector3(295,1.0f,-4.83f),"拼接旧底片",.45f,() =>
            {
                CloseDispatch();
                RightStrip=false;
                Splicing=true;
            },()=>WorkingOn(FieldOrder.Archive) && State.survey.HasSites && !State.survey.spliced && !State.detained);
            Target(RiverSurvey.ContactPrinter, "印出夜班合影", 1.1f, () =>
            {
                State.survey.archive = true;
                State.cells += 2;
                SaveProgress();
                Notify("夜班合影已归档　·　领取备用电池 × 2");
                OpenSurveyAlbum(true);
            }, () => WorkingOn(FieldOrder.Archive) && State.survey.CanRestore && !State.detained);
            Target(RiverSurvey.ContactPrinter, "重看夜班合影", 0, () => OpenSurveyAlbum(true),
                () => NightShiftTrail.CanReadArchive(State) && !State.detained);
            Target(new Vector3(316, 1f, 24.5f), "接管分洪闸台", .6f, () =>
            {
                CloseDispatch();
                if (State.survey.head == 0) State.survey.head = UnityEngine.Random.Range(3,7);
                Sluicing = true;
                SaveProgress();
            }, () => State.survey.ReportReady && !State.dispatch.orderHeld && !State.detained && !State.survey.balanced);
            Target(new Vector3(316, 1f, 24.5f), "试鸣堤外警铃", .55f, () =>
            {
                var bell = new Vector3(274, 1.5f, -5);
                Emit(bell, 1.2f, "signal");
                hunter.HearGeneratorTrip(new Vector3(bell.x, 0, bell.z));
                State.survey.bellReadyAt = State.dispatch.playSeconds + 48;
                SaveProgress();
                Notify("堤外的警铃响了。");
            }, () => !State.detained && State.survey.balanced && State.dispatch.playSeconds >= State.survey.bellReadyAt);
        }

        void OpenSurveyAlbum(bool archive)
        {
            CloseDispatch();
            AlbumSheet = 0;
            AlbumIsArchive = archive;
            AlbumOpen = true;
        }

        void ResetSurveyTools()
        {
            Framing = Processing = AlbumOpen = Sluicing = Splicing = Aligned = Piloting = false;
            sideways = 0; LaunchKnock = -1;
            SelectedGate = 0;
            SteadyFlow = 0;
            Subject = -1;
            Density = darkroomAge = 0;
        }

        internal void OperateWeirs(int select, bool toggle, float dt)
        {
            if (!Sluicing) return;
            SelectedGate = (SelectedGate + select + 3) % 3;
            if (toggle)
            {
                State.survey.weirs ^= 1 << SelectedGate;
                SteadyFlow = 0;
                Emit(new Vector3(316,1,24.5f), Flow > State.survey.head ? 1f : .44f, "tool");
                SaveProgress();
            }
            SteadyFlow = Flow == State.survey.head ? Mathf.Clamp01(SteadyFlow + dt / 4f) : 0;
            if (SteadyFlow < 1) return;
            State.survey.balanced = true;
            CloseDispatch();
            SaveProgress();
            Notify("水压稳住了。试鸣线已通，可以用堤外警铃引开奶娃。", 6);
        }

        void TickSurveyEquipment(float dt)
        {
            int step = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) ? -1 :
                Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) ? 1 : 0;
            if (Piloting) SteerLaunch(Input.GetAxisRaw("Horizontal"),Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow),Input.GetKeyDown(KeyCode.Space),dt);
            else if (Splicing)
            {
                int nudge=Input.GetKeyDown(KeyCode.W)||Input.GetKeyDown(KeyCode.UpArrow) ? -1 :
                    Input.GetKeyDown(KeyCode.S)||Input.GetKeyDown(KeyCode.DownArrow) ? 1 : 0;
                AlignStrips(step,nudge,Input.GetKeyDown(InteractKey));
            }
            else if (Sluicing) OperateWeirs(step, Input.GetKeyDown(InteractKey),dt);
            else if (Processing) Develop(dt,Input.GetKeyDown(KeyCode.Space));
            else if (AlbumOpen && !AlbumIsArchive) AlbumSheet = (AlbumSheet + step + 3) % 3;
        }

        internal void AlignStrips(int side,int nudge,bool press)
        {
            if (!Splicing) return;
            var film=State.survey;
            if(side!=0) RightStrip=side>0;
            if(nudge!=0)
            {
                if(RightStrip) film.rightStrip=Mathf.Clamp(film.rightStrip+nudge,-4,4);
                else film.leftStrip=Mathf.Clamp(film.leftStrip+nudge,-4,4);
                soundscape.Play(Player.Feet,"paper",.26f);
            }
            if(!press) return;
            if(film.leftStrip!=0 || film.rightStrip!=0) { Notify("两边水线还没对齐。"); return; }
            film.spliced=true;
            CloseDispatch();
            Notify("接缝合上了。回暗房印相。");
            SaveProgress();
        }

        void ToggleFinder()
        {
            if (!State.survey.camera || State.detained || Player.Vaulting) return;
            Framing = !Framing;
            Subject = -1;
            Aligned = false;
        }

        internal void AimSurvey()
        {
            Subject = -1;
            Aligned = false;
            var eye = Player.Eyes;
            var cam = eye.GetComponent<Camera>();
            float nearest = 18f;
            for (int i = 0; i < RiverSurvey.Plates.Length; i++)
            {
                if (!WorkingOn(i < 3 ? FieldOrder.Survey : FieldOrder.Archive)) continue;
                Vector3 delta = RiverSurvey.Plates[i].Center - eye.position;
                float distance = delta.magnitude;
                if (distance > nearest || delta.z < .5f || Vector3.Dot(delta.normalized, eye.forward) < .72f) continue;
                // Never project points through walls or around the side of the reference surface.
                if (!ClearView(RiverSurvey.Plates[i].Center)) continue;
                Subject = i;
                nearest = distance;
            }
            if (Subject < 0) return;
            Aligned = true;
            for (int k = 0; k < 2; k++)
            {
                var pin = RiverSurvey.Pin(Subject, k == 0 ? -1 : 1);
                var p = cam.WorldToViewportPoint(pin);
                Pins[k] = new Vector2(p.x, 1 - p.y);
                Aligned &= p.z > 0 && p.x > .25f && p.x < .75f && p.y > .24f && p.y < .76f && ClearView(pin);
            }
            float span = Mathf.Abs(Pins[1].x - Pins[0].x);
            Aligned &= span >= .085f && span <= .42f;

            bool ClearView(Vector3 point)
            {
                var ray = point - eye.position;
                return !Physics.Raycast(eye.position, ray.normalized, Mathf.Max(0, ray.magnitude-.09f),
                    ~(1 << NightPlayer.PlayerLayer), QueryTriggerInteraction.Ignore);
            }
        }

        internal void ExposeSurvey()
        {
            AimSurvey();
            if (!Framing || Subject < 0 || !Aligned || RiverSurvey.Recorded(State, Subject)) return;
            if (Subject < 3)
            {
                string print = grade.SurveyExposure();
                if (string.IsNullOrEmpty(print)) { Notify("快门没响，再拍一次。"); return; }
                State.survey.prints[Subject] = print;
            }
            State.survey.negatives |= 1 << Subject;
            FlashUntil = Time.unscaledTime + .17f;
            Emit(Player.Feet, .48f, "switch");
            if (Subject >= 3 && State.survey.HasSites && !State.survey.spliced) Notify("三处旧址齐了。到守闸宿舍的灯台拼底片。");
            else if (Subject < 3 && State.survey.CanPrint) Notify("水尺底片齐了。回照相馆暗房。");
            SaveProgress();
        }

        internal void Develop(float elapsed, bool lift)
        {
            if (!Processing) return;
            darkroomAge += elapsed;
            // Rock the tray until the emulsion takes. Each successive sheet develops a little faster.
            Density = Mathf.Clamp01(darkroomAge / (7.5f - State.survey.sheets * .85f));
            if (!lift && Density < 1) return;
            bool clear = Density >= .64f && Density <= .90f;
            Density = darkroomAge = 0;
            Emit(RiverSurvey.Trays, clear ? .34f : .95f, clear ? "tool" : "signal");
            if (!clear)
            {
                hunter.HearGeneratorTrip(RiverSurvey.Trays);
                Notify("废片。用备用相纸重印这一张。", 3);
                return;
            }
            var film = State.survey;
            if (++film.sheets == 3)
            {
                film.printed = true;
                CloseDispatch();
                Notify(film.ReportReady ? "水尺已洗清。司机可以核对水位了。" : "相纸干了。还差值班厅的测深读数。");
            }
            SaveProgress();
        }
    }
}
