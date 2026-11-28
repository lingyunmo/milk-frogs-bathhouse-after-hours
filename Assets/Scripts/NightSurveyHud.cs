using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace AfterHours
{
    internal sealed partial class NightInterface
    {
        VisualElement finder, frameMarks, contactTable, contactImage, afterglow, gateBoard;
        Label frameSubject, frameHint, plateCaption, plateAction, gateHead, gateAction;
        readonly Label[] valves = new Label[3];
        readonly VisualElement[] spot = new VisualElement[2];
        Meter flowHold;
        VisualElement lightBox, helm;
        SoundingRaster radar;
        Label reachName, helmKeys;
        readonly Image[] filmStrips=new Image[3];
        Texture2D contactPrint;
        string loadedNegative;
        bool archiveOnTable;

        void BuildSurveyHud(VisualElement layer)
        {
            helm=Area(layer,"sounding-desk");
            radar=new SoundingRaster(game); radar.AddToClassList("sounding-raster"); helm.Add(radar);
            reachName=Type(helm,"","sounding-label");
            helmKeys=Type(helm,"","sounding-keys finder-hint");
            Visible(helm,false);
            finder = Area(layer, "screen survey-finder");
            frameMarks = Area(finder, "finder-frame");
            foreach (var corner in new[] { "tl","tr","bl","br" }) Area(frameMarks,"finder-corner " + corner);
            for (int i=0;i<2;i++) spot[i] = Area(finder,"finder-pin");
            var legend = Area(finder,"finder-legend");
            frameSubject = Type(legend,"","finder-subject serif");
            frameHint = Type(legend,"","finder-hint");
            afterglow = Area(layer,"screen shutter-glint");

            contactTable = Area(layer,"contact-table");
            var mount = Area(contactTable,"contact-mount");
            contactImage = Area(mount,"contact-image");
            plateCaption = Type(mount,"","contact-caption serif");
            plateAction = Type(contactTable,"","finder-hint");
            gateBoard = Area(layer,"river-weirs");
            gateHead = Type(gateBoard,"","finder-subject serif");
            var bank = Area(gateBoard,"weir-bank");
            for (int i=0;i<valves.Length;i++) valves[i] = Type(bank,"","weir-valve");
            flowHold = new Meter(gateBoard,"station-track","station-fill");
            gateAction = Type(gateBoard,"","finder-hint");
            lightBox=Area(layer,"contact-table");
            var glass=Area(lightBox,"splice-glass");
            for(int i=0;i<3;i++)
            {
                var strip=new Image { pickingMode=PickingMode.Ignore, scaleMode=ScaleMode.StretchToFill,
                    uv=new Rect(i/3f,0,1f/3f,1) };
                strip.AddToClassList("splice-strip");
                strip.style.left=Length.Percent(i*100f/3);
                filmStrips[i]=strip;
                glass.Add(strip);
            }
            Type(lightBox,"A / D 选底片　 W / S 对水线　 F 压紧　 Esc 松手","finder-hint");
            foreach (var el in new[]{finder,afterglow,contactTable,gateBoard,lightBox}) Visible(el,false);
        }

        void ClearContactPrint()
        {
            if (contactPrint) UnityEngine.Object.Destroy(contactPrint);
            contactPrint = null;
            loadedNegative = null;
            archiveOnTable = false;
        }

        void DisplayPrint(string encoded, bool archive)
        {
            if (encoded == loadedNegative && archive == archiveOnTable) return;
            ClearContactPrint();
            loadedNegative = encoded;
            archiveOnTable = archive;
            if (archive)
                contactImage.style.backgroundImage = new StyleBackground(Resources.Load<Texture2D>("Interface/FloodWatch"));
            else
            {
                if (!string.IsNullOrEmpty(encoded))
                {
                    contactPrint = new Texture2D(2,2,TextureFormat.RGB24,false);
                    try { contactPrint.LoadImage(Convert.FromBase64String(encoded)); }
                    catch (FormatException) { UnityEngine.Object.Destroy(contactPrint); contactPrint=null; }
                }
                contactImage.style.backgroundImage = new StyleBackground(contactPrint);
            }
        }

        void TickSurveyHud()
        {
            Visible(helm,game.Piloting);
            if(game.Piloting)
            {
                var trip=game.State.survey;
                Copy(reachName,SoundingReach.Names[trip.soundings]+"　 "+(trip.soundings+1)+" / 3");
                bool near=trip.boatMetres>=SoundingReach.Length-3;
                Copy(helmKeys,near ? "靠近金环　·　空格投测铅" : Time.unscaledTime<game.LaunchKnock ?
                    "擦浅了　·　松开油门，打舵绕开" : "W 给油　 A / D 绕开浅滩　 Esc 松手");
                radar.MarkDirtyRepaint();
            }
            bool looking = game.Framing;
            Visible(finder, looking);
            Visible(afterglow, Time.unscaledTime < game.FlashUntil);
            var state = game.State;
            if (looking)
            {
                int i = game.Subject;
                bool already = i >= 0 && RiverSurvey.Recorded(state,i);
                frameMarks.EnableInClassList("finder-ready",game.Aligned);
                Copy(frameSubject,i < 0 ? "测绘相机" : RiverSurvey.Subject(i) + "　/　" + RiverSurvey.Plates[i].Name);
                Copy(frameHint,already ? "已拍摄　·　V 收起" : game.Aligned ? "F　按下快门" :
                    i < 0 ? "寻找成对的标钉　·　V 收起" : "调整距离，让两枚标钉进入框内");
                for (int k=0;k<spot.Length;k++)
                {
                    Vector2 point=game.Pins[k];
                    Visible(spot[k],i>=0 && point.x>0 && point.x<1 && point.y>0 && point.y<1);
                    spot[k].style.left=Length.Percent(point.x*100);
                    spot[k].style.top=Length.Percent(point.y*100);
                    spot[k].EnableInClassList("pin-ready",game.Aligned);
                }
            }
            Visible(contactTable,game.Processing || game.AlbumOpen);
            if (game.Processing || game.AlbumOpen)
            {
                int sheet = game.Processing ? state.survey.sheets : game.AlbumSheet;
                bool archive = game.AlbumOpen && game.AlbumIsArchive;
                DisplayPrint(archive ? null : state.survey.prints[sheet],archive);
                float density = game.Density;
                contactImage.style.opacity = game.Processing ? Mathf.SmoothStep(.025f,1,density/.67f)
                    * (1 - Mathf.InverseLerp(.90f,1f,density)*.85f) : 1;
                Copy(plateCaption,archive ? "八七年七月　·　背面写着：接应板面向水塔。她套上黄布头，笑三声，岸上的孩子就往那边游。" : RiverSurvey.Plates[sheet].Name + "　　" + (sheet+1) + " / 3");
                Copy(plateAction,game.Processing ? density < .64f ? "等白标显清　·　Esc 松手" :
                    density <= .90f ? "空格　移入定影盘" : "相纸发黑了" :
                    archive ? "Esc　收起" : "A / D　翻看　　Esc　收起");
            }
            else if (contactPrint || archiveOnTable)
            {
                contactImage.style.backgroundImage = StyleKeyword.None;
                ClearContactPrint();
            }
            Visible(lightBox,game.Splicing);
            if(game.Splicing)
            {
                for(int i=0;i<3;i++)
                {
                    var strip=filmStrips[i];
                    if(!strip.image) strip.image=Resources.Load<Texture2D>("Interface/FloodWatch");
                    int offset=i==0?state.survey.leftStrip:i==2?state.survey.rightStrip:0;
                    strip.style.top=Length.Percent(12+offset*3);
                    strip.EnableInClassList("splice-active",i==(game.RightStrip?2:0));
                }
            }
            Visible(gateBoard,game.Sluicing);
            if (!game.Sluicing) return;
            Copy(gateHead,$"进水　{state.survey.head}　　　分洪　{game.Flow}");
            for (int i=0;i<valves.Length;i++)
            {
                var valve=valves[i];
                bool open=(state.survey.weirs & (1<<i))!=0;
                Copy(valve,(1<<i) + (open?"　开":"　关"));
                valve.EnableInClassList("valve-open",open);
                valve.EnableInClassList("valve-selected",i==game.SelectedGate);
            }
            flowHold.Set(game.SteadyFlow);
            Copy(gateAction,game.Flow==state.survey.head ? "水位正在回稳" : "A / D 选闸　 F 开关　 配平进水量");
        }
    }
}
