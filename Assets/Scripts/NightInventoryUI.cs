using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace AfterHours
{
    internal sealed partial class NightInterface
    {
        readonly Dictionary<string, Texture2D> inventoryArt = new();
        bool showNotes, photoBack, fromPause;
        internal void CloseInventory() => game.SetScreen(fromPause ? NightScreen.Pause : NightScreen.Play);
        NightItemId? selectedItem;
        VisualElement inventoryGrid, itemInspection;
        Button supplyTab, recordTab;

        void BuildInventory(VisualElement veil)
        {
            veil.AddToClassList("inventory-veil");
            Area(veil, "screen desk-surface");
            var workspace = Area(veil, "inventory-workspace");
            Reveal(workspace);
            var heading = Area(workspace, "inventory-header");
            var titles = Area(heading, "inventory-headings");
            Type(titles, LocationName(), "overline");
            Type(titles, "随身背包", "inventory-heading serif");
            Act(heading, "B　收起", CloseInventory, "chip-button", "inventory-close");

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

        internal void OpenPack()
        {
            if (game.State.dispatch.carrying)
            {
                showNotes=false;
                selectedItem=NightItemId.Counterweight;
            }
            game.SetScreen(NightScreen.Inventory);
        }

        void OpenEvidence(NightItemId id)
        {
            fromPause = game.ScreenState == NightScreen.Pause;
            showNotes = true;
            selectedItem = id;
            photoBack = false;
            game.SetScreen(NightScreen.Inventory);
        }

        void ChangeInventoryTab(bool notes)
        {
            if (showNotes == notes) return;
            showNotes = notes;
            photoBack = false;
            selectedItem = null;
            RefreshInventory();
        }

        void RefreshInventory(bool focusSelected = false)
        {
            supplyTab.EnableInClassList("inventory-tab--active", !showNotes);
            recordTab.EnableInClassList("inventory-tab--active", showNotes);
            inventoryGrid.EnableInClassList("evidence-index", showNotes);
            inventoryGrid.Clear();
            itemInspection.Clear();

            var items = game.InventoryItems.Where(item => (item.Kind is "记录" or "证物") == showNotes).ToList();
            if (items.Count == 0)
            {
                selectedItem = null;
                var empty = Area(inventoryGrid, "inventory-empty");
                Type(empty, showNotes ? "还没有找到线索。" : "背包是空的。", "inventory-empty-text serif");
                return;
            }

            if (!items.Any(item => item.Id == selectedItem))
            {
                selectedItem = items[0].Id;
                photoBack = false;
            }

            foreach (var item in items)
            {
                string art = ArtFor(item.Id);
                var button = Act(inventoryGrid, "", () =>
                {
                    if (selectedItem != item.Id) photoBack = false;
                    selectedItem = item.Id;
                    RefreshInventory(true);
                }, "inventory-item", "inventory-item-" + item.Id);
                button.tooltip = item.Name;
                if (showNotes)
                {
                    var entry = Area(button, "evidence-entry");
                    Type(entry, item.Name, "entry-title serif");
                    Type(entry, item.Status, "entry-source");
                }
                else
                {
                    Area(button, "inventory-item-glow").style.backgroundImage = new StyleBackground(NightInterfaceArt.Glow);
                    SetItemArt(Area(button, "inventory-item-art"), art);
                    if (item.Count > 1 || item.Id is NightItemId.Battery or NightItemId.Stone)
                        Type(button, "× " + item.Count, "inventory-count");
                    Type(button, item.Name, "inventory-item-name");
                }
                bool active = item.Id == selectedItem;
                button.EnableInClassList("inventory-item--selected", active);
                if (!active) continue;
                BuildInspection(item, art);
                if (focusSelected) Focus(button);
            }
        }

        void BuildInspection(NightInventoryItem item, string art)
        {
            if (showNotes && item.Id is not (NightItemId.Receipt or NightItemId.AnnexDiagram))
            {
                InspectEvidence(item);
                return;
            }
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
            Type(details, item.Description, "inspection-copy serif");
            if (string.IsNullOrEmpty(item.ActionLabel)) return;

            var action = Act(details, item.ActionLabel, () =>
            {
                if (game.UseInventoryItem(item.Id) && item.Id==NightItemId.Counterweight) { CloseInventory(); return; }
                RefreshInventory(true);
            }, "sheet-action sheet-action--primary serif inventory-use", "inventory-use");
            action.SetEnabled(item.CanUse);
        }

        void InspectEvidence(NightInventoryItem item)
        {
            bool photograph = item.Id is NightItemId.Print or NightItemId.LostPair;
            var table = Area(itemInspection, "evidence-table");
            var page = Area(table, photograph && !photoBack ? "evidence-photo" : "evidence-paper");
            Reveal(page);
            if (photograph && !photoBack)
            {
                var image = Area(page, "evidence-emulsion");
                image.name = "evidence-front";
                SetItemArt(image, ArtFor(item.Id));
                Type(page, item.Name, "photo-pencil serif");
            }
            else
            {
                var scroll = new ScrollView(ScrollViewMode.Vertical);
                scroll.AddToClassList("paper-scroll");
                page.Add(scroll);
                var leaf = scroll.contentContainer;
                Type(leaf, item.Status, "paper-origin");
                if (!photograph) Type(leaf, item.Name, "paper-title serif");
                Area(leaf, "paper-rule");
                Type(leaf, item.Description, "paper-writing serif").name = "evidence-transcript";
                if(item.Id == NightItemId.BrineBook)
                {
                    foreach(string film in game.State.brine.exposures)
                    {
                        if(string.IsNullOrEmpty(film)) continue;
                        var image=new Image {scaleMode=ScaleMode.ScaleToFit};
                        var print=new Texture2D(2,2);
                        if(!print.LoadImage(System.Convert.FromBase64String(film))) { UnityEngine.Object.Destroy(print); continue; }
                        image.image=print;
                        image.style.height=160;
                        image.RegisterCallback<DetachFromPanelEvent>(_=>UnityEngine.Object.Destroy(print));
                        leaf.Add(image);
                    }
                }
            }
            var partner = item.Id switch
            {
                NightItemId.LostPair => NightItemId.WashSlip,
                NightItemId.WashSlip => NightItemId.LostPair,
                NightItemId.Print => NightItemId.Names,
                NightItemId.Names => NightItemId.Print,
                _ => (NightItemId?)null
            };
            if (partner.HasValue && (!photograph || photoBack))
                foreach (var other in game.InventoryItems)
                    if (other.Id == partner.Value)
                        Act(table, "对照　" + other.Name, () =>
                        {
                            selectedItem = other.Id;
                            photoBack = false;
                            RefreshInventory(true);
                        }, "evidence-link", "compare-record");
            if (!photograph) return;
            var turn = Act(table, photoBack ? "看正面" : "翻到背面", () =>
            {
                photoBack = !photoBack;
                itemInspection.Clear();
                InspectEvidence(item);
                Focus(itemInspection.Q<Button>("turn-print"));
            }, "sheet-action serif print-turn", "turn-print");
            turn.tooltip = photoBack ? "看照片" : "看背面的字";
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
                texture = Resources.Load<Texture2D>(name == "FloodWatch" ? InterfaceFolder + name : InterfaceFolder + "Items/" + name);
                inventoryArt.Add(name, texture);
            }
            if (texture) picture.style.backgroundImage = new StyleBackground(texture);
        }

        static string ArtFor(NightItemId id) => id switch
        {
            NightItemId.Print => "FloodWatch",
            NightItemId.LostPair => "LostPair",
            NightItemId.Torch => "torch",
            NightItemId.Battery => "cell",
            NightItemId.BrineSample => "BrineCase",
            NightItemId.Counterweight => "counterweight",
            NightItemId.Stone => "stone",
            NightItemId.Starter or NightItemId.RelayFuse or NightItemId.PlaybackCard => "starter",
            NightItemId.Seal => "seal",
            NightItemId.MonitorKey or NightItemId.LinenKey or NightItemId.StaffKey or NightItemId.UplinkKey => "key",
            _ => "receipt"
        };
    }
}
