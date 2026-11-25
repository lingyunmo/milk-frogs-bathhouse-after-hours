using UnityEngine;

namespace AfterHours
{
    internal sealed class NightPlayer : MonoBehaviour
    {
        internal const int PlayerLayer = 8;
        internal const int ViewModelLayer = 9;
        const int SceneMask = ~(1 << PlayerLayer);
        const float WalkSpeed = 1.90f;
        const float RunSpeed = 3.55f;
        const float CrouchSpeed = 1.10f;
        const float StandingHeight = 1.70f;
        const float CrouchHeight = .54f;
        const float TorchStrength = 2.25f;

        internal CharacterController Body { get; private set; }
        internal Transform Eyes { get; private set; }
        internal bool Crouched { get; private set; }
        internal bool Sprinting { get; private set; }
        internal bool Exhausted { get; private set; }
        internal float Stamina { get; private set; } = 1;
        internal bool TorchOn => torch.enabled;
        internal bool Vaulting => vaultTime > 0;
        internal Vector3 Feet => transform.position;
        float pitch, vertical, footDistance, vaultTime, breathDelay, bobPhase, eyeHeight;
        Vector3 planarVelocity, vaultStart, vaultEnd, lastGround;
        Light torch;
        Camera handsCamera;
        Transform heldTorch;

        internal void SetUp(Camera camera)
        {
            gameObject.layer = PlayerLayer;
            Body = gameObject.AddComponent<CharacterController>();
            Body.radius = .25f;
            Body.skinWidth = .04f;
            Body.stepOffset = .31f;
            Body.minMoveDistance = 0;
            Eyes = camera.transform;
            Eyes.SetParent(transform, false);
            Eyes.localPosition = Vector3.up * 1.60f;
            Eyes.localRotation = Quaternion.identity;
            if (!camera.GetComponent<AudioListener>()) camera.gameObject.AddComponent<AudioListener>();
            camera.nearClipPlane = .035f;
            camera.cullingMask &= ~(1 << ViewModelLayer);

            var flashlight = new GameObject("Hand torch beam").transform;
            flashlight.SetParent(Eyes, false);
            flashlight.localPosition = new Vector3(.10f, -.13f, .075f);
            flashlight.localRotation = Quaternion.Euler(-1.5f, -.55f, 0);
            torch = flashlight.gameObject.AddComponent<Light>();
            torch.type = LightType.Spot;
            torch.spotAngle = 63;
            torch.innerSpotAngle = 19;
            torch.range = 17;
            torch.intensity = TorchStrength;
            torch.renderMode = LightRenderMode.ForcePixel;
            torch.shadows = LightShadows.Soft;
            torch.shadowStrength = .92f;
            torch.shadowBias = .07f;
            torch.shadowNormalBias = .19f;
            torch.shadowNearPlane = .10f;
            torch.color = new Color(1, .92f, .76f);
            torch.cullingMask &= ~(1 << ViewModelLayer);
            torch.cookie = SoftBeam();
            torch.enabled = false;
            ApplyHeight(StandingHeight);
        }

        internal void AttachHeldTorch(GameObject model)
        {
            if (!model) return;
            heldTorch = model.transform;
            var mount = new GameObject("First person torch camera").transform;
            mount.SetParent(Eyes, false);
            handsCamera = mount.gameObject.AddComponent<Camera>();
            var worldCamera = Eyes.GetComponent<Camera>();
            handsCamera.depth = worldCamera.depth + 1;
            handsCamera.clearFlags = CameraClearFlags.Depth;
            handsCamera.cullingMask = 1 << ViewModelLayer;
            handsCamera.nearClipPlane = .035f;
            handsCamera.farClipPlane = 3;
            handsCamera.fieldOfView = 57;
            handsCamera.enabled = false;
            var fill = new GameObject("Viewmodel light").AddComponent<Light>();
            fill.transform.SetParent(mount, false);
            fill.transform.localPosition = new Vector3(.30f, .27f, .34f);
            fill.type = LightType.Point;
            fill.color = new Color(.88f, .94f, 1);
            fill.range = 1.15f;
            fill.intensity = .44f;
            fill.cullingMask = 1 << ViewModelLayer;
            heldTorch.SetParent(mount, false);
            SetTorchVisible(false);
        }

        internal void SetTorchVisible(bool visible)
        {
            if (handsCamera) handsCamera.enabled = visible;
        }

        internal void ApplyPreferences(NightPreferences preferences)
        {
            Eyes.GetComponent<Camera>().fieldOfView = preferences.FieldOfView;
        }

        static Texture2D SoftBeam()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float radius = new Vector2((x + .5f) * 2 / size - 1, (y + .5f) * 2 / size - 1).magnitude;
                float beam = .64f * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.18f, .97f, radius)))
                    + .28f * Mathf.Exp(-radius * radius * 5.9f);
                byte light = (byte)(Mathf.Clamp01(beam) * 255);
                pixels[y * size + x] = new Color32(light, light, light, light);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        internal void Tick(NightGame game, float dt)
        {
            var options = game.Preferences;
            float yaw = Input.GetAxisRaw("Mouse X") * options.Sensitivity;
            transform.Rotate(0, yaw, 0);
            pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * options.Sensitivity, -85, 85);
            Eyes.localRotation = Quaternion.Euler(pitch, 0, 0);
            TickTorch(game, dt);

            if (Vaulting)
            {
                TickVault(game, dt);
                return;
            }

            if (Input.GetKeyDown(KeyCode.C)) ToggleCrouch();
            Vector2 input = new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            float stride = Move(input, shift, dt);
            UpdateFootsteps(game, stride);
            UpdateCamera(dt, options.CameraMotion);

            if (!NightWorld.ContainsPlayable(Feet))
            {
                SetFeet(lastGround, transform.eulerAngles.y);
                game.Notify("退回了可以落脚的地方。");
            }
        }

        internal float Move(Vector2 input, bool sprint, float dt)
        {
            Vector2 keys = Vector2.ClampMagnitude(input, 1);
            Sprinting = sprint && !Crouched && !Exhausted && keys.sqrMagnitude > .01f && keys.y > -.2f;
            float speed = Crouched ? CrouchSpeed : Sprinting ? RunSpeed : WalkSpeed;
            var desired = (transform.right * keys.x + transform.forward * keys.y) * speed;
            if (desired.sqrMagnitude < .0001f) planarVelocity = Vector3.zero;
            else planarVelocity = Vector3.MoveTowards(planarVelocity, desired, 13 * dt);
            vertical = Body.isGrounded ? -2 : Mathf.Max(-15, vertical - 19 * dt);

            var start = Feet;
            var contacts = Body.Move((planarVelocity + Vector3.up * vertical) * dt);
            if ((contacts & CollisionFlags.Above) != 0 && vertical > 0) vertical = 0;
            Vector3 displacement = Feet - start;
            float walked = new Vector2(displacement.x, displacement.z).magnitude;
            bool moving = walked > dt * .20f;
            if (Body.isGrounded && NightWorld.ContainsPlayable(Feet)) lastGround = Feet + Vector3.up * .025f;
            UpdateStamina(Sprinting && moving, moving, dt);
            return walked;
        }

        void UpdateStamina(bool running, bool moving, float dt)
        {
            if (running)
            {
                Stamina = Mathf.Max(0, Stamina - .218f * dt);
                breathDelay = .85f;
                if (Stamina <= 0)
                {
                    Exhausted = true;
                    Sprinting = false;
                    planarVelocity = Vector3.ClampMagnitude(planarVelocity, WalkSpeed);
                }
                return;
            }
            breathDelay -= dt;
            if (breathDelay > 0) return;
            Stamina = Mathf.Min(1, Stamina + (moving ? .16f : .24f) * dt);
            if (Stamina >= .26f) Exhausted = false;
        }

        internal void ToggleCrouch()
        {
            if (Crouched && !CanStand(Feet)) return;
            Crouched = !Crouched;
            ApplyHeight(Crouched ? CrouchHeight : StandingHeight);
        }

        static bool CanStand(Vector3 feet) => !Physics.CheckCapsule(feet + Vector3.up * .64f,
            feet + Vector3.up * 1.43f, .23f, SceneMask, QueryTriggerInteraction.Ignore);

        void UpdateFootsteps(NightGame game, float stride)
        {
            if (!Body.isGrounded || stride <= .002f)
            {
                footDistance = 0;
                return;
            }
            footDistance += stride;
            float interval = Crouched ? .69f : Sprinting ? 1.02f : .81f;
            if (footDistance < interval) return;
            footDistance -= interval;
            game.Emit(Feet, Crouched ? .13f : Sprinting ? .57f : .33f, "step");
        }

        void UpdateCamera(float dt, float motion)
        {
            float speed = Body.isGrounded ? new Vector2(Body.velocity.x, Body.velocity.z).magnitude : 0;
            eyeHeight = Mathf.MoveTowards(eyeHeight, Crouched ? .41f : 1.60f, 4.4f * dt);
            bobPhase += speed * dt * Mathf.PI * 2 / 1.72f;
            float weight = Mathf.Clamp01(speed / WalkSpeed) * motion;
            Vector3 bob = new(Mathf.Cos(bobPhase) * .013f * weight, Mathf.Sin(bobPhase * 2) * .019f * weight, 0);
            Eyes.localPosition = Vector3.up * eyeHeight + bob;
            if (heldTorch)
            {
                heldTorch.localPosition = new Vector3(.23f, -.305f, .625f) - bob * .45f;
                heldTorch.localRotation = Quaternion.Euler(4, -9 + Mathf.Cos(bobPhase) * weight * 1.3f, -13);
            }
        }

        void TickTorch(NightGame game, float dt)
        {
            bool equipped = game.State.torch;
            if (handsCamera) handsCamera.enabled = equipped;
            if (Input.GetKeyDown(KeyCode.F))
            {
                if (!equipped) game.Notify("先从更衣凳取手电。");
                else if (game.State.charge <= 0) game.Notify("手电没电了。按 R 换电。");
                else SetTorch(!TorchOn);
            }
            if (Input.GetKeyDown(KeyCode.R)) game.ReplaceCell();
            AdvanceTorch(game, dt);
        }

        internal void AdvanceTorch(NightGame game, float dt)
        {
            if (!TorchOn) return;
            float before = game.State.charge;
            game.State.charge = Mathf.Max(0, before - dt);
            float charge = game.State.charge;
            torch.intensity = Mathf.Lerp(TorchStrength * .52f, TorchStrength, Mathf.InverseLerp(0, 26, charge));
            if (charge <= 0)
            {
                SetTorch(false);
                game.Notify("手电没电了。按 R 换电。");
            }
            else if (before > 36 && charge <= 36) game.Notify("手电电量不足。");
        }

        void ApplyHeight(float height)
        {
            Body.stepOffset = height < 1 ? .18f : .31f;
            Body.height = height;
            Body.center = Vector3.up * (height / 2);
        }

        internal void LookDown(float degrees)
        {
            pitch = degrees;
            Eyes.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        internal void SetTorch(bool on)
        {
            torch.enabled = on;
            if (on) torch.intensity = TorchStrength;
        }

        internal bool Vault(Vector3 destination)
        {
            if (!NightWorld.ContainsPlayable(destination) ||
                !Physics.Raycast(destination + Vector3.up * .26f, Vector3.down, out var floor, .56f, SceneMask, QueryTriggerInteraction.Ignore))
                return false;
            Vector3 landing = floor.point + Vector3.up * .055f;
            if (Physics.CheckCapsule(landing + Vector3.up * .31f, landing + Vector3.up * .58f, .23f,
                SceneMask, QueryTriggerInteraction.Ignore)) return false;
            Body.enabled = false;
            vaultStart = Feet;
            vaultEnd = landing;
            vaultTime = .001f;
            vertical = 0;
            Sprinting = false;
            planarVelocity = Vector3.zero;
            return true;
        }

        void TickVault(NightGame game, float dt)
        {
            vaultTime = Mathf.Min(.46f, vaultTime + dt);
            float t = vaultTime / .46f;
            transform.position = Vector3.Lerp(vaultStart, vaultEnd, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * .82f);
            Eyes.localPosition = Vector3.up * eyeHeight;
            if (t < 1) return;

            if (!Crouched && !CanStand(Feet))
            {
                Crouched = true;
                ApplyHeight(CrouchHeight);
            }
            Body.enabled = true;
            vaultTime = 0;
            lastGround = Feet;
            game.Emit(Feet, game.LandingNoise, "land");
        }

        void SetFeet(Vector3 position, float yaw)
        {
            Body.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            vertical = 0;
            pitch = 0;
            vaultTime = 0;
            footDistance = 0;
            planarVelocity = Vector3.zero;
            Sprinting = false;
            Crouched = false;
            ApplyHeight(StandingHeight);
            eyeHeight = 1.60f;
            Eyes.localPosition = Vector3.up * eyeHeight;
            Eyes.localRotation = Quaternion.identity;
            Body.enabled = true;
        }

        internal void ResetPosition(Vector3 position, float yaw)
        {
            Stamina = 1;
            Exhausted = false;
            breathDelay = 0;
            lastGround = position;
            SetFeet(position, yaw);
        }

        void OnDestroy()
        {
            if (torch && torch.cookie) Destroy(torch.cookie);
        }
    }
}
