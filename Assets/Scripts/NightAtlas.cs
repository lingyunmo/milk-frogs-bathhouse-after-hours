
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AfterHours
{
    internal sealed class NightAtlas
    {
        internal const int Street = 4;
        internal static readonly string[] FloorNames = { "地下室", "一楼", "二楼", "三楼", "街区" };
        internal static readonly Rect Building = Rect.MinMaxRect(-20, 0, 16, 24);
        internal static readonly Rect StreetBounds = Rect.MinMaxRect(NightWorld.WestEdge, NightWorld.SouthEdge,
            NightWorld.EastEdge, NightWorld.NorthEdge);

        internal sealed class Area
        {
            internal readonly string Name, Key;
            internal readonly Rect Bounds;
            internal readonly Vector2? Entrance;
            internal readonly bool Stair, Route;

            internal Area(int floor, string name, Rect bounds, bool stair = false, bool route = false, Vector2? entrance = null)
            {
                Name = name;
                Key = floor + "/" + name;
                Bounds = bounds;
                Stair = stair;
                Route = route;
                Entrance = entrance;
            }
        }

        readonly List<Area>[] areas = { new(), new(), new(), new(), new() };
        internal int Floor { get; private set; } = 2;
        internal Area Current { get; private set; }
        internal List<Area> Areas(int floor) => areas[floor];

        internal NightAtlas()
        {
            foreach (Transform floor in GameObject.Find("浴场").transform)
            {
                var hall = floor.Find("横廊");
                if (!hall) continue;
                int index = Mathf.Clamp(Mathf.RoundToInt(hall.position.y / 3.6f) + 1, 0, 3);
                foreach (Transform room in floor)
                {
                    var slab = room.Find("地坪");
                    if (!slab) continue;
                    Vector3 center = slab.position;
                    Vector3 size = slab.localScale;
                    Add(index, room.name, new Rect(center.x - size.x / 2, center.z - size.z / 2, size.x, size.z),
                        entrance: new Vector2(center.x, center.z > 12 ? 14 : 10));
                }
                Add(index, "勤务梯", Rect.MinMaxRect(-20, 14, -16, 24), stair: true);
                Add(index, "前厅梯", Rect.MinMaxRect(12, 0, 16, 10), stair: true);
                Add(index, "横廊", Rect.MinMaxRect(-20, 10, 16, 14), route: true);
            }

            Add(2, "北露台", Rect.MinMaxRect(7.25f, 24, 12.75f, 28.5f));
            Add(3, "检修平台", Rect.MinMaxRect(8.75f, 24, 11.25f, 26.7f));
            Add(2, "卸货平台", Rect.MinMaxRect(-13.3f, 24, -9.7f, 27.4f));
            Add(2, "西侧雨棚", Rect.MinMaxRect(-22.6f, 3.7f, -20, 6.3f));
            Add(Street, "救援站牌", Rect.MinMaxRect(148, -30, 172, -9));
            Add(Street, "候车亭", Rect.MinMaxRect(90, -14, 114, -5));
            Add(Street, "停车棚", Rect.MinMaxRect(43, -2, 62, 11));
            Add(Street, "卸货区", Rect.MinMaxRect(-18, 24, 12, 36));
            Add(Street, "西通道", Rect.MinMaxRect(-32, -3, -20, 35));
            Add(Street, "前院", Rect.MinMaxRect(-20, -11, 20, 0));
            Add(Street, "东侧步道", Rect.MinMaxRect(16, 0, 32, 29));
            Add(Street, "后巷", Rect.MinMaxRect(-23, 28, 57, 41), route: true);
            Add(Street, "石桥路", Rect.MinMaxRect(-32, -29, 172, -13), route: true);
        }

        void Add(int floor, string name, Rect bounds, bool stair = false, bool route = false, Vector2? entrance = null) =>
            areas[floor].Add(new Area(floor, name, bounds, stair, route, entrance));

        internal void Track(NightState state, Vector3 feet)
        {
            Vector2 point = new(feet.x, feet.z);
            int level = Mathf.Clamp(Mathf.RoundToInt((feet.y - .07f) / 3.6f) + 1, 0, 3);
            if (!Building.Contains(point))
            {
                if (feet.y > 4.8f && point.y >= 24) level = 3;
                else level = feet.y > 1.1f ? 2 : Street;
            }

            Floor = level;
            Current = areas[level].Find(area => area.Bounds.Contains(point));
            if (Current != null && !state.explored.Contains(Current.Key)) state.explored.Add(Current.Key);
        }

        internal string Place => Current?.Name ?? (Floor == Street ? "浴场外" : "过道");
    }

    internal sealed class NightMapView : VisualElement
    {
        static readonly Color Ink = new(.045f, .063f, .052f, .26f);
        static readonly Color Grid = new(.71f, .67f, .51f, .15f);
        static readonly Color Seen = new(.26f, .34f, .27f, .77f);
        static readonly Color Unknown = new(.10f, .15f, .12f, .73f);
        static readonly Color Edge = new(.51f, .57f, .49f);
        static readonly Color Faint = new(.31f, .36f, .30f);
        static readonly Color Stair = new(.52f, .42f, .29f);
        static readonly Color Amber = new(.93f, .72f, .43f);
        static readonly Color Current = new(.44f, .45f, .32f, .81f);
        readonly NightGame game;
        readonly bool small;
        readonly List<Label> names = new();
        Rect plan;
        int sheet;
        float scale;
        Vector2 origin;

        internal NightMapView(NightGame game, bool small)
        {
            this.game = game;
            this.small = small;
            pickingMode = PickingMode.Ignore;
            AddToClassList(small ? "minimap-drawing" : "map-drawing");
            generateVisualContent += Draw;
            if (!small) RegisterCallback<GeometryChangedEvent>(_ => PlaceNames());
        }

        internal void SetSheet(int floor)
        {
            sheet = floor;
            PlaceNames();
            MarkDirtyRepaint();
        }

        void Fit()
        {
            float width = Mathf.Max(1, contentRect.width);
            float height = Mathf.Max(1, contentRect.height);
            if (small)
            {
                var here = game.Player.Feet;
                float span = game.Atlas.Floor == NightAtlas.Street ? 38 : 23;
                plan = new Rect(here.x, here.z, span, span);
                plan.center = new Vector2(here.x, here.z);
                float side = Mathf.Max(width, height);
                scale = side / span;
                origin = new Vector2((width - side) / 2, (height - side) / 2);
                return;
            }

            plan = sheet == NightAtlas.Street ? NightAtlas.StreetBounds : new Rect(-23, -1, 41, 32);
            scale = Mathf.Max(.01f, Mathf.Min((width - 32) / plan.width, (height - 32) / plan.height));
            origin = new Vector2((width - plan.width * scale) / 2, (height - plan.height * scale) / 2);
        }

        Vector2 Point(float x, float z) => origin + new Vector2((x - plan.xMin) * scale, (plan.yMax - z) * scale);
        static void Quad(Painter2D p, Vector2 a, Vector2 b, Color fill, Color stroke)
        {
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(new Vector2(b.x, a.y));
            p.LineTo(b);
            p.LineTo(new Vector2(a.x, b.y));
            p.ClosePath();
            p.fillColor = fill;
            p.Fill();
            if (stroke.a > 0)
            {
                p.lineWidth = 1;
                p.strokeColor = stroke;
                p.Stroke();
            }
        }

        void AreaBox(Painter2D p, Rect bounds, Color baseColor, Color border) =>
            Quad(p, Point(bounds.xMin, bounds.yMax), Point(bounds.xMax, bounds.yMin), baseColor, border);

        static void Line(Painter2D p, Vector2 start, Vector2 end, Color ink, float width = 1)
        {
            p.BeginPath();
            p.MoveTo(start);
            p.LineTo(end);
            p.strokeColor = ink;
            p.lineWidth = width;
            p.Stroke();
        }

        bool SeenBefore(NightAtlas.Area area) => game.State.explored.Contains(area.Key);

        void Draw(MeshGenerationContext ctx)
        {
            Fit();
            var p = ctx.painter2D;
            Quad(p, Vector2.zero, contentRect.size, Ink, Color.clear);
            float spacing = small && game.Atlas.Floor == NightAtlas.Street || sheet == NightAtlas.Street ? 8 : 4;
            for (float x = Mathf.Ceil(plan.xMin / spacing) * spacing; x < plan.xMax; x += spacing)
                Line(p, Point(x, plan.yMin), Point(x, plan.yMax), Grid);
            for (float z = Mathf.Ceil(plan.yMin / spacing) * spacing; z < plan.yMax; z += spacing)
                Line(p, Point(plan.xMin, z), Point(plan.xMax, z), Grid);

            int floor = small ? game.Atlas.Floor : sheet;
            var areas = game.Atlas.Areas(floor);
            if (floor == NightAtlas.Street)
                AreaBox(p, NightAtlas.StreetBounds, new Color(.17f, .23f, .19f, .33f), Faint);

            for (int pass = 0; pass < 2; ++pass)
            foreach (var area in areas)
            {
                if (area.Route != (pass == 0)) continue;
                bool seen = SeenBefore(area);
                bool occupied = game.Atlas.Current == area;
                AreaBox(p, area.Bounds, occupied ? Current : seen ? Seen : Unknown, area.Stair ? Stair : seen ? Edge : Faint);
                if (area.Stair && (!small || area.Bounds.Overlaps(plan)))
                    for (int step = 1; step < 8; ++step)
                    {
                        float z = area.Bounds.yMin + step * area.Bounds.height / 8;
                        Line(p, Point(area.Bounds.xMin + .30f, z), Point(area.Bounds.xMax - .30f, z), Stair);
                    }
            }

            foreach (var area in areas)
                if (area.Entrance is Vector2 door)
                    Line(p, Point(door.x - .60f, door.y), Point(door.x + .60f, door.y),
                        SeenBefore(area) ? Amber : Edge, 2);

            if (floor == NightAtlas.Street)
                AreaBox(p, NightAtlas.Building, new Color(.24f, .26f, .21f), Edge);

            if (floor != game.Atlas.Floor) return;
            Vector3 feet = game.Player.Feet;
            Vector3 direction = game.Player.transform.forward;
            Vector2 center = Point(feet.x, feet.z);
            var forward = new Vector2(direction.x, -direction.z);
            var across = new Vector2(forward.y, -forward.x);
            p.BeginPath();
            p.MoveTo(center + forward * 10);
            p.LineTo(center - forward * 5 + across * 6);
            p.LineTo(center - forward * 2);
            p.LineTo(center - forward * 5 - across * 6);
            p.ClosePath();
            p.fillColor = Amber;
            p.Fill();
            p.strokeColor = new Color(.08f, .085f, .058f);
            p.lineWidth = 1.5f;
            p.Stroke();
        }

        void PlaceNames()
        {
            if (small) return;
            Fit();
            foreach (var label in names) label.RemoveFromHierarchy();
            names.Clear();
            foreach (var area in game.Atlas.Areas(sheet))
                if (SeenBefore(area)) PlaceName(area.Name, area.Bounds.center, area == game.Atlas.Current, area.Bounds.width * scale);
            if (sheet == NightAtlas.Street) PlaceName("浴场", NightAtlas.Building.center, false, 110);
        }

        void PlaceName(string name, Vector2 at, bool current, float space)
        {
            Vector2 center = Point(at.x, at.y);
            Vector3 feet = game.Player.Feet;
            if (current && (center - Point(feet.x, feet.z)).sqrMagnitude < 900) center.y += 24;
            float width = Mathf.Min(146, Mathf.Max(36, space));
            var label = new Label(name) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("map-label");
            label.EnableInClassList("map-label--here", current);
            label.style.left = center.x - width / 2;
            label.style.top = center.y - 13;
            label.style.width = width;
            label.style.fontSize = Mathf.Clamp(width / (name.Length + .6f), 10, 15);
            Add(label);
            names.Add(label);
        }
    }
}
