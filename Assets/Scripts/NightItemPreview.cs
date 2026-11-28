
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace AfterHours
{
    internal sealed class NightItemPreview : VisualElement
    {
        const string Models = "Interface/PreviewMeshes/";
        const string Torch = "Props/small_plastic_torch/";
        const string WeightBox = "Props/metal_toolbox/";
        const int PreviewLayer = 10;
        const float DragScale = .42f;
        static readonly Vector3 StagePosition = new(2048, 256, 2048);
        readonly Transform owner;
        readonly NightItemId item;
        readonly GameObject source;
        readonly Shader surfaceShader;
        readonly Image image;
        readonly Dictionary<string, Material> materials = new();
        Camera camera;
        GameObject stage;
        Transform orbit;
        RenderTexture targetTexture;
        Quaternion restPose;
        Vector2 angles;
        int pointer = -1;
        bool drawRequested;

        internal Vector2 RotationOffset => angles;
        internal Quaternion OrbitRotation => orbit ? orbit.localRotation : Quaternion.identity;
        internal RenderTexture PreviewTexture => targetTexture;

        internal static NightItemPreview TryCreate(Transform owner, NightItemId item)
        {
            string path = item switch
            {
                NightItemId.Torch => Torch + "Model",
                NightItemId.Counterweight => WeightBox + "Model",
                NightItemId.Battery => Models + "Cell",
                NightItemId.Stone => Models + "Stone",
                NightItemId.Starter or NightItemId.RelayFuse => Models + "Starter",
                NightItemId.Seal => Models + "Seal",
                NightItemId.Receipt or NightItemId.AnnexDiagram => Models + "Receipt",
                _ => null
            };
            if (path == null || !owner) return null;

            var shader = Resources.Load<Shader>("Interface/InventoryStudio");
            var model = Resources.Load<GameObject>(path);
            return shader && shader.isSupported && model ? new NightItemPreview(owner, item, model, shader) : null;
        }

        NightItemPreview(Transform owner, NightItemId item, GameObject model, Shader shader)
        {
            this.owner = owner;
            this.item = item;
            source = model;
            surfaceShader = shader;
            name = "item-preview-3d";
            focusable = true;
            image = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            image.StretchToParentSize();
            Add(image);
            RegisterCallback<AttachToPanelEvent>(_ => OpenStage());
            RegisterCallback<DetachFromPanelEvent>(_ => CloseStage());
            RegisterCallback<GeometryChangedEvent>(_ => ResizeStage());
            RegisterCallback<PointerDownEvent>(BeginDrag);
            RegisterCallback<PointerMoveEvent>(MoveDrag);
            RegisterCallback<PointerUpEvent>(FinishDrag);
            RegisterCallback<PointerCaptureOutEvent>(_ => pointer = -1);
            RegisterCallback<FocusInEvent>(_ => RequestDraw());
            RegisterCallback<KeyDownEvent>(RotateFromKey);
            RegisterCallback<NavigationMoveEvent>(RotateFromNavigation);
        }

        void OpenStage()
        {
            if (stage || !owner) return;
            stage = new GameObject("Inventory inspection stage") { hideFlags = HideFlags.HideAndDontSave };
            stage.transform.SetParent(owner, false);
            stage.transform.position = StagePosition;

            orbit = new GameObject("Inspection orbit").transform;
            orbit.SetParent(stage.transform, false);
            var pivot = new GameObject("Centered model").transform;
            pivot.SetParent(orbit, false);
            var model = Object.Instantiate(source, pivot, false);
            foreach (var part in model.GetComponentsInChildren<Transform>())
                part.gameObject.layer = PreviewLayer;
            foreach (var collider in model.GetComponentsInChildren<Collider>())
                collider.enabled = false;

            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                CloseStage();
                return;
            }

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; ++i)
                    slots[i] = CreateSurface(slots[i] ? slots[i].name : "", i);
                renderer.sharedMaterials = slots;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            float fit = .95f / Mathf.Max(.001f, bounds.extents.magnitude);
            Vector3 center = pivot.InverseTransformPoint(bounds.center);
            pivot.localScale = Vector3.one * fit;
            pivot.localPosition = -center * fit;
            restPose = Presentation(bounds.size);
            orbit.localRotation = restPose;
            angles = Vector2.zero;

            var viewpoint = new GameObject("Inspection camera");
            viewpoint.transform.SetParent(stage.transform, false);
            viewpoint.transform.localPosition = new Vector3(0, 0, -4);
            camera = viewpoint.AddComponent<Camera>();
            camera.enabled = false;
            camera.cullingMask = 1 << PreviewLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 9;
            camera.orthographic = true;
            camera.orthographicSize = 1.08f;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.useOcclusionCulling = false;
            camera.renderingPath = RenderingPath.Forward;
            ResizeStage();
        }

        Quaternion Presentation(Vector3 size)
        {
            if (item is NightItemId.Receipt or NightItemId.AnnexDiagram or NightItemId.Seal)
            {
                if (size.y < size.x && size.y < size.z) return Quaternion.Euler(-69, -8, -8);
                if (size.x < size.z) return Quaternion.Euler(10, 71, -10);
            }
            if (item == NightItemId.Torch && size.z > size.x && size.z > size.y)
                return Quaternion.Euler(18, 64, 14);
            return Quaternion.Euler(11, -18, -11);
        }

        void ResizeStage()
        {
            if (!camera || !float.IsFinite(layout.width) || !float.IsFinite(layout.height)
                || layout.width < 2 || layout.height < 2) return;
            // The square buffer supplies extra samples for the bevels even on a small inspector.
            int edge = Mathf.Clamp(Mathf.CeilToInt(Mathf.Min(layout.width, layout.height) * 1.6f / 64) * 64, 384, 768);
            if (targetTexture && targetTexture.width == edge && targetTexture.IsCreated()) return;
            ReleaseTexture();
            targetTexture = new RenderTexture(edge, edge, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "Inventory item surface",
                antiAliasing = 4,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            targetTexture.Create();
            camera.targetTexture = targetTexture;
            RequestDraw();
        }

        void RequestDraw()
        {
            if (drawRequested || !camera || !targetTexture) return;
            drawRequested = true;
            schedule.Execute(() =>
            {
                drawRequested = false;
                if (!camera || !targetTexture) return;
                camera.Render();
                if (image.image != targetTexture) image.image = targetTexture;
            });
        }

        internal void RotateBy(Vector2 degrees)
        {
            if (!orbit) return;
            angles.x = Mathf.Repeat(angles.x + degrees.x + 180, 360) - 180;
            angles.y = Mathf.Clamp(angles.y + degrees.y, -85, 85);
            orbit.localRotation = Quaternion.Euler(angles.y, angles.x, 0) * restPose;
            RequestDraw();
        }

        internal void ResetRotation()
        {
            if (!orbit) return;
            angles = Vector2.zero;
            orbit.localRotation = restPose;
            RequestDraw();
        }

        void BeginDrag(PointerDownEvent evt)
        {
            if (evt.button != 0 || pointer != -1) return;
            Focus();
            if (evt.clickCount > 1) ResetRotation();
            pointer = evt.pointerId;
            this.CapturePointer(pointer);
            evt.StopPropagation();
        }

        void MoveDrag(PointerMoveEvent evt)
        {
            if (evt.pointerId != pointer) return;
            RotateBy(new Vector2(-evt.deltaPosition.x, -evt.deltaPosition.y) * DragScale);
            evt.StopPropagation();
        }

        void FinishDrag(PointerUpEvent evt)
        {
            if (evt.pointerId != pointer) return;
            this.ReleasePointer(pointer);
            pointer = -1;
            evt.StopPropagation();
        }

        void RotateFromKey(KeyDownEvent evt)
        {
            if (evt.keyCode is not (KeyCode.Home or KeyCode.R)) return;
            ResetRotation();
            evt.StopImmediatePropagation();
        }

        void RotateFromNavigation(NavigationMoveEvent evt)
        {
            Vector2 delta = evt.direction switch
            {
                NavigationMoveEvent.Direction.Left => new(-12, 0),
                NavigationMoveEvent.Direction.Right => new(12, 0),
                NavigationMoveEvent.Direction.Up => new(0, -9),
                NavigationMoveEvent.Direction.Down => new(0, 9),
                _ => Vector2.zero
            };
            if (delta == Vector2.zero) return;
            RotateBy(delta);
            evt.StopImmediatePropagation();
        }

        Material CreateSurface(string importedName, int slot)
        {
            string key = item == NightItemId.Torch
                ? importedName.EndsWith("_1", System.StringComparison.Ordinal) || slot > 0 ? "Lens" : "Housing"
                : importedName;
            if (materials.TryGetValue(key, out var cached)) return cached;

            var material = new Material(surfaceShader) { name = "Inventory " + key };
            materials.Add(key, material);
            void Set(Color color, float metal, float roughness, float variation, string map = null)
            {
                material.SetColor("_Color", color);
                material.SetFloat("_Metalness", metal);
                material.SetFloat("_Roughness", roughness);
                material.SetFloat("_Variation", variation);
                if (map != null) material.mainTexture = Resources.Load<Texture2D>(map);
            }

            if (item==NightItemId.Counterweight)
            {
                Set(Color.white,.74f,.51f,0,WeightBox+"metal_toolbox_diff_2k");
                material.SetTexture("_PackedMap",Resources.Load<Texture2D>(WeightBox+"metal_toolbox_arm_2k_Unity"));
                material.SetTexture("_BumpMap",Resources.Load<Texture2D>(WeightBox+"metal_toolbox_nor_gl_2k"));
                material.SetFloat("_UsesPacked",1); material.SetFloat("_UsesNormal",1);
                return material;
            }

            switch (key)
            {
                case "Housing":
                case "Lens":
                    Set(Color.white, .13f, .44f, 0, Torch + "small_plastic_torch_diff_2k");
                    material.SetTexture("_PackedMap", Resources.Load<Texture2D>(Torch + "small_plastic_torch_arm_2k_Unity"));
                    material.SetFloat("_UsesPacked", 1);
                    if (key == "Housing")
                    {
                        material.SetTexture("_BumpMap", Resources.Load<Texture2D>(Torch + "small_plastic_torch_nor_gl_2k"));
                        material.SetFloat("_UsesNormal", 1);
                    }
                    break;
                case "BatteryWrap":
                    Set(Color.white, 0, .72f, .06f, Models + "CellWrap");
                    break;
                case "BatteryTin":
                    Set(new(.44f, .47f, .45f), .68f, .38f, .34f);
                    break;
                case "BatteryCap":
                    Set(new(.16f, .18f, .17f), .32f, .54f, .24f);
                    break;
                case "StarterBody":
                    Set(new(.39f, .41f, .38f), .74f, .46f, .47f);
                    break;
                case "StarterSteel":
                    Set(new(.56f, .59f, .55f), .74f, .36f, .27f);
                    break;
                case "Brass":
                    Set(new(.57f, .38f, .17f), .66f, .48f, .41f);
                    break;
                case "Bakelite":
                    Set(new(.29f, .16f, .10f), .07f, .55f, .18f);
                    break;
                case "Slate":
                    Set(new(.42f, .44f, .43f), 0, .83f, .57f);
                    break;
                case "RubberSeal":
                    Set(new(.17f, .20f, .18f), 0, .52f, .17f);
                    break;
                case "FaultPaper":
                    Set(Color.white, 0, .88f, .06f, Models + "FaultPaper");
                    break;
                default:
                    Set(new(.54f, .53f, .48f), 0, .63f, .15f);
                    break;
            }

            return material;
        }

        void ReleaseTexture()
        {
            if (!targetTexture) return;
            image.image = null;
            if (camera) camera.targetTexture = null;
            targetTexture.Release();
            Object.Destroy(targetTexture);
            targetTexture = null;
        }

        void CloseStage()
        {
            if (pointer != -1 && panel != null && this.HasPointerCapture(pointer)) this.ReleasePointer(pointer);
            pointer = -1;
            drawRequested = false;
            ReleaseTexture();
            foreach (var material in materials.Values) Object.Destroy(material);
            materials.Clear();
            if (stage)
            {
                stage.SetActive(false);
                Object.Destroy(stage);
            }
            camera = null;
            stage = null;
            orbit = null;
        }
    }
}
