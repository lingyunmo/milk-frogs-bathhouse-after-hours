
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AfterHours
{
    internal sealed partial class NightWorld
    {
        void DressArchitecturalDetails()
        {
            const string serviceFinish = "Finishes/Service/Steel";
            var iron = Pbr("氧化的灰蓝钢", serviceFinish + "Color", serviceFinish + "Normal",
                serviceFinish + "Finish", new Color(.72f, .78f, .77f), .46f, true);
            iron.SetFloat("_GlossMapScale", .67f);
            iron.SetFloat("_BumpScale", .86f);
            var darkIron = new Material(iron) { name = "油污铸铁", color = new Color(.44f, .49f, .44f) };
            var handleMetal = new Material(iron) { name = "旧镀镍把手", color = new Color(.78f, .73f, .65f) };
            var slot = Material(new Color(.052f, .061f, .052f));

            // These trims are render meshes only. The original openings and encounter collisions stay in charge.
            for (int floor = -1; floor < 3; floor++)
            {
                float y = floor * 3.6f;
                var frame = new DetailMesh(.57f);
                var ironwork = new DetailMesh(.67f);
                var recesses = new DetailMesh(.7f);
                foreach (var door in Doors)
                {
                    Vector3 p = door.Position;
                    if (Mathf.Abs(p.y - y) > .04f || Mathf.Abs(door.Inward.x) > .50f) continue;
                    bool fireDoor = door.Fire;
                    float width = door.Leaf.transform.lossyScale.x;
                    float jamb = width * .5f + .029f;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Vector3 center = p + new Vector3(side * jamb, 1.216f, 0);
                        frame.Box(center, new Vector3(.092f, 2.42f, .252f));
                        ironwork.Box(center + new Vector3(side * .057f, 1f, 0), new Vector3(.039f, .266f, .276f));
                        ironwork.Box(center + new Vector3(side * .057f, -1f, 0), new Vector3(.039f, .266f, .276f));
                    }
                    frame.Box(p + new Vector3(0, 2.453f, 0), new Vector3(width + .15f, .089f, .252f));
                    ironwork.Box(p + new Vector3(0, .008f, 0), new Vector3(width + .13f, .016f, .245f));

                    var latch = new DetailMesh(.43f);
                    foreach (Transform child in door.Leaf.transform)
                        if (child.name == "门把") child.GetComponent<Renderer>().enabled = false;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Vector3 spindle = p + new Vector3(fireDoor ? .63f : .45f, 1.10f, .054f * side);
                        latch.Box(spindle, new Vector3(.088f, .205f, .018f));
                        latch.Pipe(spindle, spindle + Vector3.forward * (.060f * side), .020f, 10);
                        Vector3 reach = spindle + Vector3.forward * (.059f * side);
                        latch.Pipe(reach, reach + Vector3.left * .112f, .014f, 10);
                        latch.Pipe(reach + Vector3.left * .112f, reach + Vector3.left * .117f, .016f, 10);
                        if (fireDoor)
                            latch.Pipe(p + new Vector3(-.69f, 1.04f, .09f * side),
                                p + new Vector3(.37f, 1.04f, .09f * side), .023f, 10);
                    }
                    latch.Place(door.Leaf.transform, "铰接门把与锁板", handleMetal);
                }

                if (floor == -1)
                {
                    RouteHeader(ironwork, frame, y, 13.59f, 2.98f, .075f);
                    RouteHeader(ironwork, frame, y, 13.37f, 3.12f, .043f);
                    RouteHeader(ironwork, frame, y, 10.25f, 3.12f, .025f);
                    DropPipe(ironwork, frame, new Vector3(-5.0f, y, 13.62f), 2.98f);
                    DropPipe(ironwork, frame, new Vector3(6.5f, y, 13.62f), 2.98f);
                    ConduitBox(ironwork, frame, recesses, new Vector3(-5.44f, y + 1.78f, 13.81f), -1);
                    Grate(ironwork, recesses, new Vector3(-6.8f, y, 12.80f), .64f, .30f);
                    Grate(ironwork, recesses, new Vector3(2.1f, y, 13.03f), .64f, .30f);

                    // Flanged shut-off line and disconnect near the generator, with room left at the repair position.
                    var rear = new Vector3(-8.62f, y, 23.72f);
                    DropPipe(ironwork, frame, rear, 2.84f);
                    ironwork.Pipe(rear + new Vector3(-4.65f, 2.84f, 0), rear + new Vector3(.58f, 2.84f, 0), .074f);
                    ConduitBox(ironwork, frame, recesses, new Vector3(-11.67f, y + 1.84f, 23.86f), -1);
                    Grate(ironwork, recesses, new Vector3(-7.78f, y, 20.54f), .54f, .35f);
                }
                if (floor == 0)
                {
                    RouteHeader(ironwork, frame, y, 13.75f, 3.12f, .026f);
                    ConduitBox(ironwork, frame, recesses, new Vector3(7.16f, y + 1.67f, 13.81f), -1);
                    Grate(ironwork, recesses, new Vector3(6.46f, y, 12.50f), .54f, .25f);
                    Grate(ironwork, recesses, new Vector3(9.03f, y, 21.64f), .55f, .28f);
                }
                if (floor == 1)
                {
                    ironwork.Pipe(new Vector3(-5.64f, y + 2.88f, 23.77f), new Vector3(3.17f, y + 2.88f, 23.77f), .041f);
                    ConduitBox(ironwork, frame, recesses, new Vector3(-4.36f, y + 2.29f, 23.86f), -1);
                    Grate(ironwork, recesses, new Vector3(-1.43f, y, 20.06f), .48f, .24f);
                }

                frame.Place(root, "搪瓷门框和检修包边", iron);
                ironwork.Place(root, "旧管道、法兰和地漏", darkIron);
                recesses.Place(root, "地漏缝和电箱接缝", slot);
            }
        }

        static void RouteHeader(DetailMesh pipes, DetailMesh fixtures, float y, float z, float height, float radius)
        {
            Vector3 at = new(0, y + height, z);
            pipes.Pipe(at + Vector3.left * 16.15f, at + Vector3.right * 11.90f, radius);
            foreach (float x in new[] { -14.7f, -10.5f, -6.6f, -2.55f, 1.43f, 5.83f, 9.88f })
            {
                Vector3 join = at + Vector3.right * x;
                pipes.Pipe(join + Vector3.left * .066f, join + Vector3.right * .066f, radius * 1.43f);
                float wall = z > 12 ? 13.91f : 10.09f;
                fixtures.Box(new Vector3(join.x, join.y - radius * .93f, (join.z + wall) / 2),
                    new Vector3(.105f, .040f, Mathf.Abs(wall - join.z) + .03f));
                fixtures.Box(new Vector3(join.x, join.y - radius * .93f, wall),
                    new Vector3(.128f, .173f, .031f));
            }
        }

        static void DropPipe(DetailMesh pipes, DetailMesh fixtures, Vector3 foot, float height)
        {
            pipes.Pipe(foot + Vector3.up * .22f, foot + Vector3.up * height, .078f);
            foreach (float h in new[] { .34f, 1.21f, 2.26f, height - .17f })
                fixtures.Pipe(foot + Vector3.up * (h - .053f), foot + Vector3.up * (h + .053f), .109f);
            Vector3 valve = foot + Vector3.up * 1.24f;
            pipes.Pipe(valve, valve + Vector3.back * .16f, .039f, 10);
            Vector3 handwheel = valve + Vector3.back * .16f;
            for (int spoke = 0; spoke < 12; ++spoke)
            {
                float start = spoke * (Mathf.PI / 6);
                float end = (spoke + 1) * (Mathf.PI / 6);
                Vector3 a = handwheel + new Vector3(Mathf.Cos(start), Mathf.Sin(start), 0) * .125f;
                Vector3 b = handwheel + new Vector3(Mathf.Cos(end), Mathf.Sin(end), 0) * .125f;
                fixtures.Pipe(a, b, .014f, 8);
                if (spoke % 3 == 0) fixtures.Pipe(handwheel, a, .012f, 8);
            }
        }

        static void ConduitBox(DetailMesh pipes, DetailMesh fixtures, DetailMesh recess, Vector3 center, float facing)
        {
            fixtures.Box(center, new Vector3(.39f, .48f, .14f));
            recess.Box(center + Vector3.forward * (.074f * facing), new Vector3(.343f, .403f, .014f));
            fixtures.Box(center + Vector3.forward * (.091f * facing), new Vector3(.305f, .369f, .025f));
            fixtures.Box(center + new Vector3(.08f, -.09f, .110f * facing), new Vector3(.068f, .11f, .025f));
            for (int i = 0; i < 3; i++)
                recess.Box(center + new Vector3(-.057f, .118f - i * .032f, .107f * facing), new Vector3(.13f, .010f, .009f));
            pipes.Pipe(center + Vector3.up * .22f, center + Vector3.up * 1.14f, .024f, 10);
            pipes.Pipe(center + Vector3.down * .23f, center + Vector3.down * .70f, .026f, 10);
        }

        static void Grate(DetailMesh steelMesh, DetailMesh recess, Vector3 floor, float w, float d)
        {
            const float rim = .018f;
            recess.Box(floor + Vector3.up * .004f, new Vector3(w, .008f, d));
            foreach (float side in new[] { -1f, 1f })
            {
                steelMesh.Box(floor + new Vector3(side * (w / 2 - rim), .014f, 0), new Vector3(rim * 2, .018f, d));
                steelMesh.Box(floor + new Vector3(0, .014f, side * (d / 2 - rim)), new Vector3(w, .018f, rim * 2));
            }
            for (int rib = 1; rib < 16; ++rib)
                steelMesh.Box(floor + new Vector3((rib / 16f - .5f) * (w - 2 * rim), .013f, 0),
                    new Vector3(.014f, .014f, d - 2 * rim));
        }

        sealed class DetailMesh
        {
            readonly List<Vector3> points = new();
            readonly List<Vector3> normals = new();
            readonly List<Vector2> uvs = new();
            readonly List<int> triangles = new();
            readonly float metres;

            internal DetailMesh(float metresPerRepeat) => metres = metresPerRepeat;

            internal void Box(Vector3 center, Vector3 size)
            {
                Vector3 a = center - size * .5f, b = center + size * .5f;
                Quad(new(a.x, a.y, a.z), new(a.x, a.y, b.z), new(a.x, b.y, b.z), new(a.x, b.y, a.z), Vector3.left);
                Quad(new(b.x, a.y, b.z), new(b.x, a.y, a.z), new(b.x, b.y, a.z), new(b.x, b.y, b.z), Vector3.right);
                Quad(new(a.x, a.y, b.z), new(a.x, a.y, a.z), new(b.x, a.y, a.z), new(b.x, a.y, b.z), Vector3.down);
                Quad(new(a.x, b.y, a.z), new(a.x, b.y, b.z), new(b.x, b.y, b.z), new(b.x, b.y, a.z), Vector3.up);
                Quad(new(b.x, a.y, a.z), new(a.x, a.y, a.z), new(a.x, b.y, a.z), new(b.x, b.y, a.z), Vector3.back);
                Quad(new(a.x, a.y, b.z), new(b.x, a.y, b.z), new(b.x, b.y, b.z), new(a.x, b.y, b.z), Vector3.forward);
            }

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
            {
                int start = points.Count;
                SurfacePoint(a, n);
                SurfacePoint(b, n);
                SurfacePoint(c, n);
                SurfacePoint(d, n);
                AddQuad(start, start + 1, start + 2, start + 3);
            }

            void SurfacePoint(Vector3 p, Vector3 n)
            {
                points.Add(p);
                normals.Add(n);
                var uv = Mathf.Abs(n.y) > .5f ? new Vector2(p.x, p.z) :
                    Mathf.Abs(n.x) > .5f ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y);
                uvs.Add(uv / metres);
            }

            void AddQuad(int a, int b, int c, int d)
            {
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(a); triangles.Add(c); triangles.Add(d);
            }

            internal void Pipe(Vector3 a, Vector3 b, float radius, int sides = 16)
            {
                Vector3 direction = (b - a).normalized;
                Vector3 tangent = Vector3.Cross(direction, Mathf.Abs(direction.y) > .8f ? Vector3.forward : Vector3.up).normalized;
                Vector3 bitangent = Vector3.Cross(direction, tangent);
                int first = points.Count;
                float length = Vector3.Distance(a, b);
                for (int i = 0; i <= sides; i++)
                {
                    float angle = 2 * Mathf.PI * i / sides;
                    Vector3 n = tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle);
                    float cross = angle * radius / metres;
                    points.Add(a + n * radius);
                    points.Add(b + n * radius);
                    normals.Add(n);
                    normals.Add(n);
                    uvs.Add(new Vector2(cross, 0));
                    uvs.Add(new Vector2(cross, length / metres));
                    if (i > 0)
                    {
                        int row = first + (i - 1) * 2;
                        AddQuad(row, row + 2, row + 3, row + 1);
                    }
                }
            }

            internal void Place(Transform parent, string name, Material material)
            {
                if (points.Count == 0) return;
                var part = new GameObject(name);
                part.transform.SetParent(parent, false);
                Matrix4x4 toLocal = part.transform.worldToLocalMatrix;
                Matrix4x4 toNormal = part.transform.localToWorldMatrix.transpose;
                for (int i = 0; i < points.Count; i++)
                {
                    points[i] = toLocal.MultiplyPoint3x4(points[i]);
                    normals[i] = toNormal.MultiplyVector(normals[i]).normalized;
                }
                var mesh = new Mesh { name = name };
                mesh.SetVertices(points);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateTangents();
                mesh.UploadMeshData(true);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = part.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            }
        }
    }
}
