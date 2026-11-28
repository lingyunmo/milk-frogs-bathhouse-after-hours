using UnityEngine;
using UnityEngine.Rendering;

namespace AfterHours
{
    // Each channel is one archived exterior still. Capture once, near the player,
    // then leave only the phosphor shader running on the small in-world screen.
    internal sealed class NightBrineMonitor : MonoBehaviour
    {
        readonly RenderTexture[] frames = new RenderTexture[4];
        readonly Material[] panels = new Material[4];
        Renderer[] feeds;
        Camera survey;
        Material type;
        Font font;
        int channel, reel = -1;

        internal void SetFeeds(Renderer[] outputs, GameObject standby, GameObject online, Font screenFont)
        {
            feeds = outputs;
            font = screenFont;
            var shader = Resources.Load<Shader>("BrineMonitor");
            type = new Material(shader);
            type.SetFloat("_Lettering", 1);
            RefreshAtlas(font);
            Font.textureRebuilt += RefreshAtlas;
            foreach (var group in new[] { standby, online })
                foreach (var line in group.GetComponentsInChildren<TextMesh>(true))
                    line.GetComponent<MeshRenderer>().sharedMaterial = type;
            for (int i = 0; i < outputs.Length; i++)
            {
                panels[i] = new Material(shader);
                panels[i].SetTexture("_MainTex", Texture2D.blackTexture);
                outputs[i].sharedMaterial = panels[i];
                outputs[i].shadowCastingMode = ShadowCastingMode.Off;
                outputs[i].receiveShadows = false;
            }
        }

        internal void Power(bool on, int wheel)
        {
            int next = on ? wheel : -1;
            if (next == reel) return;
            reel = next;
            channel = 0;
            if (survey) Destroy(survey.gameObject);
            survey = null;
            for (int i = 0; i < frames.Length; i++)
            {
                if (!frames[i]) continue;
                panels[i].mainTexture = null;
                frames[i].Release();
                Destroy(frames[i]);
                frames[i] = null;
            }
            enabled = on;
        }

        void RefreshAtlas(Font updated)
        {
            if (updated == font && type) type.mainTexture = font.material.mainTexture;
        }

        void LateUpdate()
        {
            if (feeds == null || !feeds[0].gameObject.activeInHierarchy) return;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { enabled = false; return; }
            var player = Camera.main;
            if (!player || (player.transform.position - BrineInquiry.Display).sqrMagnitude > 18 * 18) return;
            if (!survey)
            {
                var node = new GameObject("巡检带定格采样");
                node.transform.SetParent(transform, false);
                survey = node.AddComponent<Camera>();
                survey.enabled = false;
                survey.allowHDR = false;
                survey.allowMSAA = false;
                survey.nearClipPlane = .17f;
                survey.farClipPlane = 32f;
                survey.fieldOfView = 48f;
                survey.cullingMask = (1 << 0) | (1 << 1);
                survey.clearFlags = CameraClearFlags.SolidColor;
                survey.backgroundColor = new Color(.04f, .05f, .045f);
            }
            var mark = BrineRoute.Marks[channel].Point;
            survey.transform.position = mark + new Vector3(channel % 2 == 0 ? -2.9f : 2.9f, 1.55f, -7.4f);
            survey.transform.LookAt(mark + Vector3.up * .40f);
            var image = new RenderTexture(384, 216, 16, RenderTextureFormat.ARGB32) { name = "巡堤定格 " + (channel + 1) };
            frames[channel] = image;
            image.Create();
            survey.targetTexture = image;
            survey.Render();
            survey.targetTexture = null;
            panels[channel].mainTexture = image;
            if (++channel < feeds.Length) return;
            Destroy(survey.gameObject);
            survey = null;
            enabled = false;
        }

        void OnDestroy()
        {
            Font.textureRebuilt -= RefreshAtlas;
            if (type) Destroy(type);
            foreach (var panel in panels) if (panel) Destroy(panel);
            foreach (var image in frames)
                if (image) { image.Release(); Destroy(image); }
        }
    }
}
