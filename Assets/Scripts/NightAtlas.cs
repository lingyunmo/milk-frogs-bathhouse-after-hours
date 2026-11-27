
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
            foreach (var room in NightWorld.StreetRoomAreas)
                Add(Street, room.Name, room.Bounds);
            foreach (var pocket in NightWorld.StreetPockets)
                Add(Street, pocket.Name, pocket.Bounds);
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
        // Survey-plan palette: ink lines on near-black, brass for the reader's position and doorways.
        static readonly Color PaperShade = new(.012f, .018f, .017f, .42f);
        static readonly Color Grid = new(.62f, .74f, .70f, .055f);
        static readonly Color Wall = new(.83f, .80f, .71f, .86f);
        static readonly Color WallLight = new(.86f, .83f, .74f, .12f);
        static readonly Color QuietWall = new(.48f, .55f, .52f, .42f);
        static readonly Color Amber = new(.95f, .79f, .53f);
        static readonly Color PlanShade = new(.03f, .045f, .042f, .62f);
        static readonly Color HereFill = new(.80f, .63f, .38f, .21f);
        static readonly Color SeenFill = new(.60f, .68f, .63f, .11f);
        static readonly Color UnknownFill = new(.05f, .07f, .065f, .40f);
        static readonly Color Treads = new(.80f, .66f, .44f, .50f);
        readonly NightGame game;
        readonly bool small;
        readonly List<(NightAtlas.Area Area, Label Title)> captions = new();
        Label building, ruler;
        Rect plan;
        int sheet = -1, pointer = -1;
        float scale, zoom = 1;
        Vector2 origin, pan;
        internal float ZoomLevel => zoom;

        internal NightMapView(NightGame game, bool small)
        {
            this.game = game;
            this.small = small;
            pickingMode = small ? PickingMode.Ignore : PickingMode.Position;
            AddToClassList(small ? "minimap-drawing" : "map-drawing");
            generateVisualContent += Draw;
            if (small) return;

            ruler = new Label { pickingMode = PickingMode.Ignore };
            ruler.AddToClassList("map-ruler");
            Add(ruler);
            RegisterCallback<GeometryChangedEvent>(_ => Redraw());
            RegisterCallback<WheelEvent>(evt =>
            {
                Zoom(Mathf.Pow(1.10f, Mathf.Clamp(-evt.delta.y / 3, -4, 4)), evt.localMousePosition);
                evt.StopPropagation();
            });
            RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || pointer >= 0) return;
                pointer = evt.pointerId;
                this.CapturePointer(pointer);
                EnableInClassList("map-dragging", true);
                evt.StopPropagation();
            });
            RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (evt.pointerId != pointer || !this.HasPointerCapture(pointer)) return;
                pan += (Vector2)evt.deltaPosition;
                KeepMapInView();
                Redraw();
                evt.StopPropagation();
            });
            RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.pointerId == pointer) this.ReleasePointer(pointer);
            });
            RegisterCallback<PointerCaptureOutEvent>(evt =>
            {
                if (evt.pointerId != pointer) return;
                pointer = -1;
                EnableInClassList("map-dragging", false);
            });
        }

        internal void SetSheet(int floor)
        {
            if (floor == sheet) return;
            sheet = floor;
            if (ruler != null) ruler.text = floor == NightAtlas.Street ? "10 m" : "2 m";
            foreach (var entry in captions) entry.Title.RemoveFromHierarchy();
            captions.Clear();
            building?.RemoveFromHierarchy();
            building = null;
            if (!small)
            {
                foreach (var area in game.Atlas.Areas(sheet))
                    if (SeenBefore(area)) captions.Add((area, Name(area.Name, area == game.Atlas.Current)));
                if (sheet == NightAtlas.Street) building = Name("浴场", false);
            }
            ResetView();
        }

        internal void Zoom(float ratio) => Zoom(ratio, contentRect.size * .5f);

        void Zoom(float ratio, Vector2 anchor)
        {
            float next = Mathf.Clamp(zoom * ratio, 1, 2.65f);
            if (Mathf.Approximately(next, zoom)) return;
            Fit();
            Vector2 coordinate = (anchor - origin) / scale;
            zoom = next;
            Fit();
            pan += anchor - (origin + coordinate * scale);
            KeepMapInView();
            Redraw();
        }

        internal void ResetView()
        {
            zoom = 1;
            pan = Vector2.zero;
            Redraw();
        }

        void KeepMapInView()
        {
            Fit();
            var edge = Vector2.Max(Vector2.zero, (plan.size * scale - contentRect.size) * .5f +
                new Vector2(52, 38));
            pan.x = Mathf.Clamp(pan.x, -edge.x, edge.x);
            pan.y = Mathf.Clamp(pan.y, -edge.y, edge.y);
        }

        void Fit()
        {
            float width = Mathf.Max(1, contentRect.width);
            float height = Mathf.Max(1, contentRect.height);
            if (small)
            {
                var here = game.Player.Feet;
                float span = game.Atlas.Floor == NightAtlas.Street ? 38 : 23;
                plan = new Rect(here.x - span / 2, here.z - span / 2, span, span);
                float side = Mathf.Max(width, height);
                scale = side / span;
                origin = new Vector2((width - side) / 2, (height - side) / 2);
                return;
            }

            plan = sheet == NightAtlas.Street ? NightAtlas.StreetBounds : new Rect(-23, -1, 41, 32);
            float fitScale = Mathf.Max(.01f, Mathf.Min((width - 96) / plan.width, (height - 58) / plan.height));
            scale = fitScale * zoom;
            origin = new Vector2((width - plan.width * scale) / 2, (height - plan.height * scale) / 2) + pan;
        }

        Vector2 Point(float x, float z) => origin + new Vector2((x - plan.xMin) * scale, (plan.yMax - z) * scale);

        static void Quad(Painter2D paint, Vector2 a, Vector2 b, Color fill, Color line)
        {
            paint.BeginPath();
            paint.MoveTo(a);
            paint.LineTo(new Vector2(b.x, a.y));
            paint.LineTo(b);
            paint.LineTo(new Vector2(a.x, b.y));
            paint.ClosePath();
            paint.fillColor = fill;
            paint.Fill();
            if (line.a == 0) return;
            paint.lineWidth = 1;
            paint.strokeColor = line;
            paint.Stroke();
        }

        void AreaBox(Painter2D paint, Rect bounds, Color fill, Color border) =>
            Quad(paint, Point(bounds.xMin, bounds.yMax), Point(bounds.xMax, bounds.yMin), fill, border);

        static void Line(Painter2D paint, Vector2 start, Vector2 end, Color ink, float width = 1)
        {
            paint.BeginPath();
            paint.MoveTo(start);
            paint.LineTo(end);
            paint.strokeColor = ink;
            paint.lineWidth = width;
            paint.Stroke();
        }

        bool SeenBefore(NightAtlas.Area area) => game.State.explored.Contains(area.Key);

        void Draw(MeshGenerationContext context)
        {
            Fit();
            var paint = context.painter2D;
            Quad(paint, Vector2.zero, contentRect.size, PaperShade, Color.clear);
            int floor = small ? game.Atlas.Floor : sheet;
            if (floor < 0) return;
            bool street = floor == NightAtlas.Street;
            float spacing = street ? 8 : 4;
            for (float x = Mathf.Ceil(plan.xMin / spacing) * spacing; x < plan.xMax; x += spacing)
                Line(paint, Point(x, plan.yMin), Point(x, plan.yMax), Grid);
            for (float z = Mathf.Ceil(plan.yMin / spacing) * spacing; z < plan.yMax; z += spacing)
                Line(paint, Point(plan.xMin, z), Point(plan.xMax, z), Grid);

            if (!street)
                AreaBox(paint, NightAtlas.Building, PlanShade, WallLight);
            foreach (var area in game.Atlas.Areas(floor))
            {
                bool explored = SeenBefore(area);
                bool current = game.Atlas.Current == area;
                Color fill = current ? HereFill : explored ? SeenFill : UnknownFill;
                AreaBox(paint, area.Bounds, fill, explored ? Wall : QuietWall);
                if (!small && !street && !area.Route)
                {
                    var from = Point(area.Bounds.xMin, area.Bounds.yMax) + Vector2.one * 2;
                    var to = Point(area.Bounds.xMax, area.Bounds.yMin) - Vector2.one * 2;
                    Quad(paint, from, to, Color.clear, WallLight);
                }
                if (area.Stair)
                    for (int step = 1; step < 9; step++)
                    {
                        float z = area.Bounds.yMin + step * area.Bounds.height / 9;
                        Line(paint, Point(area.Bounds.xMin + .28f, z), Point(area.Bounds.xMax - .28f, z), Treads);
                    }
            }
            foreach (var area in game.Atlas.Areas(floor))
                if (area.Entrance is Vector2 opening)
                    Line(paint, Point(opening.x - .65f, opening.y), Point(opening.x + .65f, opening.y),
                        SeenBefore(area) ? Amber : QuietWall, small ? 1.6f : 2.4f);

            if (street) AreaBox(paint, NightAtlas.Building, new Color(.10f, .13f, .12f, .88f), Wall);
            if (!small) DrawScale(paint, street);
            if (floor != game.Atlas.Floor) return;

            Vector3 boots = game.Player.Feet;
            Vector2 center = Point(boots.x, boots.z);
            Vector3 yaw = game.Player.transform.forward;
            Vector2 forward = new Vector2(yaw.x, -yaw.z).normalized;
            Vector2 across = new(forward.y, -forward.x);
            // Field of view wedge, then a soft disc so the arrow reads at a glance.
            float reach = small ? 46 : 34;
            paint.BeginPath();
            paint.MoveTo(center);
            paint.LineTo(center + (forward * .87f + across * .5f) * reach);
            paint.LineTo(center + (forward * .87f - across * .5f) * reach);
            paint.ClosePath();
            paint.fillColor = new Color(Amber.r, Amber.g, Amber.b, .11f);
            paint.Fill();
            paint.BeginPath();
            paint.Arc(center, small ? 11 : 13, 0, 360);
            paint.fillColor = new Color(Amber.r, Amber.g, Amber.b, .16f);
            paint.Fill();
            paint.BeginPath();
            paint.MoveTo(center + forward * 12);
            paint.LineTo(center - forward * 6 + across * 7);
            paint.LineTo(center - forward * 2);
            paint.LineTo(center - forward * 6 - across * 7);
            paint.ClosePath();
            paint.fillColor = Amber;
            paint.Fill();
            paint.strokeColor = new Color(.13f, .09f, .04f);
            paint.lineWidth = 1.6f;
            paint.Stroke();
        }

        void DrawScale(Painter2D paint, bool isStreet)
        {
            var left = new Vector2(22, contentRect.height - 18);
            float length = (isStreet ? 10 : 2) * scale;
            var right = left + Vector2.right * length;
            Line(paint, left, right, Wall, 1);
            for (int tick = 0; tick <= 2; ++tick)
            {
                Vector2 x = Vector2.Lerp(left, right, tick * .5f);
                Line(paint, x, x - Vector2.up * 5, Wall);
            }
        }

        void Redraw()
        {
            Fit();
            Vector3 boots = game.Player.Feet;
            foreach (var entry in captions)
            {
                Rect bounds = entry.Area.Bounds;
                Vector2 center = Point(bounds.center.x, bounds.center.y);
                if (entry.Area == game.Atlas.Current && (center - Point(boots.x, boots.z)).sqrMagnitude < 900)
                    center.y += 23;
                PositionName(entry.Title, center, bounds.width * scale);
            }
            if (building != null) PositionName(building, Point(NightAtlas.Building.center.x, NightAtlas.Building.center.y), 110);
            MarkDirtyRepaint();
        }

        Label Name(string name, bool current)
        {
            var label = new Label(name) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("map-label");
            label.EnableInClassList("map-label--here", current);
            Add(label);
            return label;
        }

        static void PositionName(Label label, Vector2 center, float space)
        {
            float width = Mathf.Clamp(space, 36, 148);
            label.style.left = center.x - width / 2;
            label.style.top = center.y - 13;
            label.style.width = width;
            label.style.fontSize = Mathf.Clamp(width / (label.text.Length + 1f), 11, 16);
        }
    }
}
