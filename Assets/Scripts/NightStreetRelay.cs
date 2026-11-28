using UnityEngine;

namespace AfterHours
{
    internal static class NightStreetRelay
    {
        internal const string FuseId = "street-fuse";
        internal const string SeatedId = "street-fuse-seated";
        internal static readonly Vector3 Fuse = new(1.10f, .87f, 35.25f);
        internal static readonly Vector3 Radio = new(116.30f, .96f, -25.62f);
        internal static readonly Vector3 Clue = new(115.74f, .97f, -25.64f);
    }

    internal sealed partial class NightGame
    {
        void BuildStreetRelayObjectives()
        {
            Vector3 radio = NightStreetRelay.Radio;
            Supply(NightStreetRelay.FuseId, NightStreetRelay.Fuse, "取备用保险管", "值守点保险管");
            Target(NightStreetRelay.Clue, "查看搪瓷牌", 0,
                () => Notify("值守频道 · 天线 Ⅱ，呼叫 Ⅲ。", 5));
            Target(radio, "检查转接机", 0,
                () => Notify("电表纹丝不动。电源线上印着浴场的名字。", 5),
                () => !State.power && !State.contacted);
            Target(radio, "检查保险座", 0,
                () => Notify("瓷座空了一截。盒盖内贴着旧领料章：「后场检修」。", 5),
                () => WorkingOn(FieldOrder.Radio) && State.power && !State.contacted && !State.collected.Contains(NightStreetRelay.FuseId));
            Target(radio, "装上备用保险管", 2.45f,
                () => Repair(radio, .86f, () => State.collected.Add(NightStreetRelay.SeatedId), "tool"),
                () => WorkingOn(FieldOrder.Radio) && State.power && !State.contacted && State.collected.Contains(NightStreetRelay.FuseId)
                    && !State.collected.Contains(NightStreetRelay.SeatedId));
            Target(radio, "调节转接机", 0, () => OpenRepair("街外应急转接", radio,
                new[] { "Ⅰ", "Ⅱ", "Ⅲ" },
                new[] { "天线旋钮的漆掉了。搪瓷牌上的字还在。", "呼叫旋钮响了一格。" },
                new[] { 1, 2 }, () => Repair(radio, .76f, () =>
                {
                    State.contacted = true;
                    Notify("「看见街尾的牌子，我就靠边。」对面松开了话筒。", 7);
                }, "signal")),
                () => WorkingOn(FieldOrder.Radio) && State.power && State.annexRelay && !State.contacted && State.collected.Contains(NightStreetRelay.SeatedId));
            Target(radio, "检查应急线路", 0,
                () => Notify("机身热了，耳机里全是沙声。回路线挂着「副楼中继」的签。", 6),
                () => WorkingOn(FieldOrder.Radio) && State.power && !State.annexRelay && !State.contacted && State.collected.Contains(NightStreetRelay.SeatedId));
        }
    }

    internal sealed partial class NightWorld
    {
        void DressStreetRelay()
        {
            Vector3 fuse = NightStreetRelay.Fuse + Vector3.down * .022f;
            var ceramic = Material(new Color(.72f, .67f, .55f));
            var fuseBody = Shape("备用保险管", PrimitiveType.Cylinder, fuse,
                new Vector3(.050f, .058f, .050f), ceramic, new Vector3(0, 0, 90));
            foreach (int side in new[] { -1, 1 })
            {
                var cap = Shape("保险管接点", PrimitiveType.Cylinder, fuse + Vector3.right * (side * .066f),
                    new Vector3(.054f, .021f, .054f), brass, new Vector3(0, 0, 90));
                cap.transform.SetParent(fuseBody.transform, true);
            }
            pickups.Add((NightStreetRelay.FuseId, fuseBody));

            Vector3 radio = NightStreetRelay.Radio + new Vector3(0, -.10f, -.13f);
            Prop("vintage_radio_transceiver", "值守点应急转接机", radio,
                new Vector3(.61f, .23f, .42f), 180, false);
            WallSign("天线 Ⅱ · 呼叫 Ⅲ", NightStreetRelay.Clue - Vector3.forward * .07f,
                180, new Vector2(.48f, .21f), .060f);
        }
    }
}
