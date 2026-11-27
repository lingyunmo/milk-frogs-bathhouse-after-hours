using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightGame
    {
        internal const string AnnexDiagramId = "annex-wiring-plan";
        const string AnnexDiagramText = "图纸折口正压着三行铅笔字：「屏蔽 Ⅱ，线路 Ⅲ，通报 Ⅰ。」角落补了一句：「别信盖子上的旧漆。」";

        void BuildAnnexObjectives()
        {
            foreach (var supply in NightAnnexLayout.Supplies)
                Cell(supply.Id, supply.Position, supply.Location);

            Vector3 isolation = NightAnnexLayout.IsolationSwitch;
            Target(isolation, "隔离受潮线路", 2.8f, () => Repair(isolation, .75f, () =>
            {
                State.annexIsolated = true;
                Notify("啪的一声。墙里的滋滋声停了。", 6);
            }, "switch"), () => !State.annexIsolated);
            Target(isolation, "检查隔离开关", 0, () => Notify("扳柄落到底了。"), () => State.annexIsolated);

            Vector3 diagram = NightAnnexLayout.WiringPlan;
            Supply(AnnexDiagramId, diagram, "查看外线接线图", "外线接线图 · B 查看",
                () => Notify(AnnexDiagramText, 8), "paper");
            Target(diagram, "重看外线接线图", 0, () => Notify(AnnexDiagramText, 8),
                () => State.collected.Contains(AnnexDiagramId));

            Vector3 relay = NightAnnexLayout.RelayConsole;
            Target(relay, "检查中继机", 0, () =>
            {
                if (!State.power) Notify("灯珠是冷的，楼下听不见机声。", 6);
                else if (!State.annexIsolated) Notify("表针贴着红线不动。封条上印着「二层机修」。", 6);
                else Notify("端子只剩三个钉眼。盖内印着「原图留档」。", 6);
            }, () => !State.annexRelay && !CanWireAnnex());
            Target(relay, "接通外线旁路", 0, () => OpenRepair("中继机旁路", relay,
                new[] { "Ⅰ", "Ⅱ", "Ⅲ" },
                new[] { "屏蔽线的铜辫散开了。", "线路一端还有压痕。", "通报线只剩短短一截。" },
                new[] { 1, 2, 0 }, () => Repair(relay, .92f, () =>
                {
                    State.annexRelay = true;
                    Notify("送话灯亮了。听筒里有人清了清嗓子。", 7);
                }, "signal")), CanWireAnnex);
            Target(relay, "查看中继状态", 0, () => Notify("机柜里传出均匀的嗡声。"), () => State.annexRelay);
        }

        bool CanWireAnnex() => State.power && State.annexIsolated &&
            State.collected.Contains(AnnexDiagramId) && !State.annexRelay;
    }

    internal sealed partial class NightWorld
    {
        Material isolationLamp, relayLamp;

        void DressAnnexEquipment()
        {
            Prop("utility_box_02", "受潮线路隔离箱", new(28, 4.432f, 5.35f), new(.63f, .49f, .37f), 0, false);
            Prop("metal_toolbox", "机修台旧工具箱", new(27.38f, 4.432f, 5.57f), new(.46f, .21f, .27f), 12, false);
            Prop("metal_toolbox", "街尾维修站工具箱", new(159.24f, .85f, 9.86f), new(.48f, .22f, .26f), -14, false);
            Prop("utility_box_02", "泵房壁挂接线箱", new(55.80f, 1.03f, 14.32f), new(.62f, .67f, .26f), -90, false);

            isolationLamp = Indicator("隔离箱指示灯", new Vector3(28.18f, 4.79f, 5.552f));
            relayLamp = Indicator("中继机指示灯", new Vector3(36.42f, 8.22f, 22.19f));

            Material Indicator(string label, Vector3 point)
            {
                var surface = Material(Color.black);
                surface.EnableKeyword("_EMISSION");
                var lamp = Box(label, point, new Vector3(.030f, .022f, .012f), surface);
                lamp.GetComponent<Collider>().enabled = false;
                return surface;
            }
        }

        void RefreshAnnexEquipment()
        {
            SetIndicator(isolationLamp, game.State.annexIsolated);
            SetIndicator(relayLamp, game.State.annexRelay);

            void SetIndicator(Material lamp, bool ready)
            {
                Color color = ready ? new Color(.20f, .80f, .32f) : new Color(.74f, .20f, .08f);
                lamp.color = game.State.power ? color : color * .22f;
                lamp.SetColor("_EmissionColor", game.State.power ? color * 1.8f : Color.black);
            }
        }
    }
}
