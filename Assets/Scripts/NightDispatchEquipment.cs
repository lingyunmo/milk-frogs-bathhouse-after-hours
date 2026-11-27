using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightWorld
    {
        readonly Material[] stationLenses = new Material[NightDispatch.Stations.Length];
        GameObject haulCase;
        Transform boom, winchHandle, aerial;

        void DressDispatch()
        {
            Prop("tool_cart", "前台电台推车", new(7,0,5.78f),new(.92f,.86f,.56f),180);
            Prop("vintage_radio_transceiver", "前台夜班电台", new(7,.86f,5.78f),
                new(.62f,.31f,.41f), 180, false);
            for (int i = 0; i < NightDispatch.Stations.Length; i++)
            {
                var station = NightDispatch.Stations[i];
                Vector3 p = station.Feet;
                Prop("portable_generator", station.Name + "发电机", p, new(1.66f,1.14f,.98f), 180);
                var post = NightEventMount("机组引路灯", p + new Vector3(.98f,0,.29f));
                EventPart(post,"灯柱",post.position + Vector3.up * 1.16f,new(.075f,2.32f,.075f),paintedIron);
                var lens = Material(Color.black);
                lens.EnableKeyword("_EMISSION");
                stationLenses[i] = lens;
                EventPart(post,"信标灯壳",post.position + Vector3.up * 2.30f,new(.31f,.46f,.19f),steel);
                EventPart(post,"信标玻璃",post.position + new Vector3(0,2.30f,-.10f),new(.24f,.33f,.036f),lens);
                WallSign("0" + (i + 1), p + new Vector3(0,.82f,-.51f), 180, new(.31f,.16f), .07f);
            }

            haulCase = Prop("metal_toolbox", "绞盘配重箱", NightDispatch.LoadDepot, new(.64f,.42f,.39f), 9, false);
            var pylon = NightDispatch.Barrier;
            Prop("utility_box_02", "街尾绞盘座", pylon, new(.83f,1.26f,.84f));
            var spindle = pylon + new Vector3(0,.89f,-.49f);
            winchHandle = NightEventMount("绞盘曲柄轴", spindle);
            EventPart(winchHandle, "曲柄摇臂", spindle + Vector3.up * .16f, new(.061f,.34f,.062f), brass);
            EventPart(winchHandle, "曲柄握柄", spindle + new Vector3(0,.31f,-.09f), new(.087f,.087f,.24f), wood);
            boom = NightEventMount("接应道栏杆枢轴", pylon + Vector3.up * 1.22f);
            // Leave the road passable on foot. The driver waits for vertical clearance.
            EventPart(boom,"旧接应栏杆",boom.position + Vector3.forward * 2.4f,new(.13f,.145f,4.8f),wood);
            for (int i = 0; i < 6; i++)
                EventPart(boom,"栏杆漆带",boom.position + Vector3.forward * (.36f + i * .78f),
                    new(.138f,.15f,.38f),paintedIron);

            Vector3 mount = NightDispatch.ReceiverDish + new Vector3(.16f,.20f,.24f);
            aerial = NightEventMount("街尾定向天线", mount);
            EventPart(aerial,"定向天线主梁",mount + Vector3.up * .25f,new(.025f,.024f,.68f),brass);
            EventPart(aerial,"天线座",mount + Vector3.up * .12f,new(.03f,.27f,.03f),paintedIron);
            for (int i = 0; i < 3; i++)
                EventPart(aerial,"振子",mount + new Vector3(0,.25f,-.23f+i*.22f), new(.47f-i*.08f,.02f,.023f),brass);
        }

        internal void SetAerial(float degrees) => aerial.localRotation = Quaternion.Euler(0,degrees,0);

        internal void SetBarrierLift(float teeth)
        {
            boom.localRotation = Quaternion.Euler(-83f * teeth / NightDispatch.Teeth,0,0);
            winchHandle.localRotation = Quaternion.Euler(0,0,-teeth * 180f);
        }

        internal void RefreshDispatch()
        {
            var job = game.State.dispatch;
            for (int i = 0; i < stationLenses.Length; i++)
            {
                Color bulb = job.steps[i] == NightDispatch.Contacts ? new(.58f,.89f,.69f)
                    : job.accepted ? new(.79f,.40f,.095f) : new(.13f,.11f,.085f);
                stationLenses[i].color = bulb;
                stationLenses[i].SetColor("_EmissionColor", bulb * (job.accepted ? 1.7f : .22f));
            }
            haulCase.SetActive(!job.carrying);
            haulCase.transform.position = job.delivered ? NightDispatch.Barrier + new Vector3(.64f,0,.12f) : job.loadFeet;
            SetBarrierLift(job.teeth);
        }
    }
}
