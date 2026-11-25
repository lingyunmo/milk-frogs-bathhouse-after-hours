
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
            var workspace = Area(veil, "inventory-workspace");
            Reveal(workspace);
            var heading = Area(workspace, "line inventory-header");
            var titles = Area(heading, "inventory-headings");
            Type(titles, LocationName(), "overline");
            Type(titles, "随身背包", "inventory-heading");
            Act(heading, "B　收起", Resume, "duty-close", "inventory-close");

            var work = Area(workspace, "inventory-work");
            var library = Area(work, "inventory-library");
            var tabs = Area(library, "inventory-tabs");
            supplyTab = Act(tabs, "随身物资", () => ChangeInventoryTab(false), "inventory-tab", "inventory-tab-supplies");
            recordTab = Act(tabs, "线索记录", () => ChangeInventoryTab(true), "inventory-tab", "inventory-tab-notes");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("inventory-scroll");
            library.Add(scroll);
            inventoryGrid = Area(scroll.contentContainer, "inventory-grid");
            itemInspection = Area(work, "item-inspection");

            var footer = Area(workspace, "line inventory-footer");
            toast = Type(footer, "", "inventory-feedback");
            Type(footer, "Esc 返回", "muted");
            RefreshInventory();
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

            var items = game.InventoryItems.Where(item => (item.Id == NightItemId.Receipt) == showNotes).ToList();
            if (items.Count == 0)
            {
                selectedItem = null;
                Type(inventoryGrid, showNotes ? "还没有找到线索。" : "背包是空的。", "inventory-empty");
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
                SetItemArt(Area(button, "inventory-item-art"), art);
                if (item.Count > 1 || item.Id is NightItemId.Battery or NightItemId.Stone)
                    Type(button, "× " + item.Count, "inventory-count");
                Type(button, item.Name, "inventory-item-name");
                bool active = item.Id == selectedItem;
                button.EnableInClassList("inventory-item--selected", active);
                if (!active) continue;
                BuildInspection(item, art);
                if (focusSelected) button.Focus();
            }
        }

        void BuildInspection(NightInventoryItem item, string art)
        {
            var stand = Area(itemInspection, "item-stand");
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
                Type(stand, "拖动旋转 · 双击复位", "orbit-caption");
            }

            var details = Area(itemInspection, "item-details");
            Type(details, item.Kind, "item-kind");
            Type(details, item.Name, "inspection-title");
            if (!string.IsNullOrEmpty(item.Status)) Type(details, item.Status, "inspection-status");
            Type(details, item.Description, "inspection-copy");
            if (string.IsNullOrEmpty(item.ActionLabel)) return;

            var action = Act(details, item.ActionLabel, () =>
            {
                game.UseInventoryItem(item.Id);
                RefreshInventory(true);
            }, "menu-action primary inventory-use", "inventory-use");
            action.SetEnabled(item.CanUse);
        }

        void SetItemArt(VisualElement picture, string name)
        {
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
            NightItemId.Starter => "starter",
            NightItemId.Seal => "seal",
            _ => "receipt"
        };
    }
}
