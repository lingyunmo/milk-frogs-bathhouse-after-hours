using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace AfterHours
{
    internal sealed partial class NightInterface
    {
        readonly Dictionary<string, Texture2D> inventoryArt = new();
        bool showNotes;
        NightItemId? selectedItem;
        VisualElement inventoryGrid, itemInspection;
        Button supplyTab, recordTab;

        void BuildInventory(VisualElement veil)
        {
            veil.AddToClassList("inventory-veil");
            Area(veil, "screen menu-scrim").style.backgroundImage = new StyleBackground(NightInterfaceArt.LeftScrim);
            var workspace = Area(veil, "inventory-workspace");
            Reveal(workspace);
            var heading = Area(workspace, "inventory-header");
            var titles = Area(heading, "inventory-headings");
            Type(titles, LocationName(), "overline");
            Type(titles, "随身背包", "inventory-heading serif");
            Act(heading, "B　收起", Resume, "chip-button", "inventory-close");

            var work = Area(workspace, "inventory-work");
            var library = Area(work, "inventory-library");
            var tabs = Area(library, "inventory-tabs");
            supplyTab = Act(tabs, "随身物资", () => ChangeInventoryTab(false), "inventory-tab serif", "inventory-tab-supplies");
            recordTab = Act(tabs, "线索记录", () => ChangeInventoryTab(true), "inventory-tab serif", "inventory-tab-notes");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("inventory-scroll");
            library.Add(scroll);
            inventoryGrid = Area(scroll.contentContainer, "inventory-grid");
            itemInspection = Area(work, "item-inspection");

            var footer = Area(workspace, "inventory-footer");
            toast = Type(footer, "", "inventory-feedback");
            var keys = Area(footer, "key-hints");
            Keys(keys, "Tab", "切换焦点");
            Keys(keys, "Esc", "返回");
            RefreshInventory(true);
        }

        void ChangeInventoryTab(bool notes)
        {
            if (showNotes == notes) return;
            showNotes = notes;
            selectedItem = null;
            RefreshInventory();
        }

        void RefreshInventory(bool focusSelected = false)
        {
            supplyTab.EnableInClassList("inventory-tab--active", !showNotes);
            recordTab.EnableInClassList("inventory-tab--active", showNotes);
            inventoryGrid.Clear();
            itemInspection.Clear();

            var items = game.InventoryItems.Where(item => (item.Id is NightItemId.Receipt or NightItemId.AnnexDiagram) == showNotes).ToList();
            if (items.Count == 0)
            {
                selectedItem = null;
                var empty = Area(inventoryGrid, "inventory-empty");
                Type(empty, showNotes ? "还没有找到线索。" : "背包是空的。", "inventory-empty-text serif");
                Type(empty, showNotes ? "前台电话旁留着夜班故障单。" : "更衣室的长凳上放着一支手电。", "inventory-empty-hint");
                return;
            }

            if (!items.Any(item => item.Id == selectedItem)) selectedItem = items[0].Id;

            foreach (var item in items)
            {
                string art = ArtFor(item.Id);
                var button = Act(inventoryGrid, "", () =>
                {
                    selectedItem = item.Id;
                    RefreshInventory(true);
                }, "inventory-item", "inventory-item-" + item.Id);
                button.tooltip = item.Name;
                Area(button, "inventory-item-glow").style.backgroundImage = new StyleBackground(NightInterfaceArt.Glow);
                SetItemArt(Area(button, "inventory-item-art"), art);
                if (item.Count > 1 || item.Id is NightItemId.Battery or NightItemId.Stone)
                    Type(button, "× " + item.Count, "inventory-count");
                Type(button, item.Name, "inventory-item-name");
                bool active = item.Id == selectedItem;
                button.EnableInClassList("inventory-item--selected", active);
                if (!active) continue;
                BuildInspection(item, art);
                if (focusSelected) Focus(button);
            }
        }

        void BuildInspection(NightInventoryItem item, string art)
        {
            var stand = Area(itemInspection, "item-stand");
            Area(stand, "item-spotlight").style.backgroundImage = new StyleBackground(NightInterfaceArt.Glow);
            var interactive = NightItemPreview.TryCreate(game.transform, item.Id);
            if (interactive == null)
            {
                var preview = Area(stand, "item-preview");
                SetItemArt(preview, art);
                Reveal(preview);
            }
            else
            {
                interactive.AddToClassList("item-orbit");
                stand.Add(interactive);
                Reveal(interactive);
                Type(stand, "拖动旋转　·　双击复位", "orbit-caption");
            }

            var details = Area(itemInspection, "item-details");
            Reveal(details);
            Type(details, item.Kind, "item-kind");
            Type(details, item.Name, "inspection-title serif");
            if (!string.IsNullOrEmpty(item.Status)) Type(details, item.Status, "inspection-status");
            Area(details, "inspection-rule");
            Type(details, item.Description, "inspection-copy");
            if (string.IsNullOrEmpty(item.ActionLabel)) return;

            var action = Act(details, item.ActionLabel, () =>
            {
                game.UseInventoryItem(item.Id);
                RefreshInventory(true);
            }, "sheet-action sheet-action--primary serif inventory-use", "inventory-use");
            action.SetEnabled(item.CanUse);
        }

        void SetItemArt(VisualElement picture, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (name == "key")
            {
                picture.generateVisualContent += context =>
                {
                    var pen = context.painter2D;
                    float unit = Mathf.Min(picture.contentRect.width, picture.contentRect.height);
                    Vector2 center = picture.contentRect.center;
                    Vector2 Point(float x, float y) => center + new Vector2(x, y) * unit;
                    pen.strokeColor = new Color(.76f, .62f, .35f, .94f);
                    pen.lineWidth = Mathf.Max(1.2f, unit * .042f);
                    pen.BeginPath();
                    pen.Arc(Point(-.16f, .12f), unit * .115f, 0, 360);
                    pen.Stroke();
                    pen.BeginPath();
                    pen.MoveTo(Point(-.068f, .052f));
                    pen.LineTo(Point(.23f, -.21f));
                    pen.LineTo(Point(.308f, -.118f));
                    pen.MoveTo(Point(.155f, -.142f));
                    pen.LineTo(Point(.214f, -.067f));
                    pen.Stroke();
                };
                return;
            }
            if (!inventoryArt.TryGetValue(name, out var texture))
            {
                texture = Resources.Load<Texture2D>(InterfaceFolder + "Items/" + name);
                inventoryArt.Add(name, texture);
            }
            if (texture) picture.style.backgroundImage = new StyleBackground(texture);
        }

        static string ArtFor(NightItemId id) => id switch
        {
            NightItemId.Torch => "torch",
            NightItemId.Battery => "cell",
            NightItemId.Stone => "stone",
            NightItemId.Starter or NightItemId.RelayFuse => "starter",
            NightItemId.Seal => "seal",
            NightItemId.LinenKey or NightItemId.StaffKey or NightItemId.UplinkKey => "key",
            _ => "receipt"
        };
    }
}
