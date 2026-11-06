using System.Collections.Generic;
using UnityEngine;

namespace BowArc
{
    // Predicts and draws the arrow's flight path for the local player while a bow is being drawn.
    internal class TrajectoryLine : MonoBehaviour
    {
        private const int MaxSteps = 2000;

        private static readonly RaycastHit[] Hits = new RaycastHit[32];
        private static int _solidMask;

        private readonly List<Vector3> _points = new List<Vector3>(256);
        private readonly List<Vector3> _dots = new List<Vector3>(512);

        private GameObject _root;
        private ParticleSystem _dotSystem;
        private ParticleSystem.Particle[] _particles = new ParticleSystem.Particle[512];
        private LineRenderer _marker;
        private Material _material;

        private void LateUpdate()
        {
            if (!Plugin.Enabled.Value || !TryPredict(out float draw, out Vector3 hitPoint, out Vector3 hitNormal, out bool hit))
            {
                Hide();
                return;
            }
            EnsureVisuals();
            float t = Mathf.InverseLerp(Plugin.ShowAtDraw.Value, 1f, draw);
            Color c = Plugin.LineColor.Value;
            c.a = Mathf.Lerp(c.a, Plugin.FullDrawAlpha.Value, t);
            DrawDots(c);
            DrawMarker(hit && Plugin.ShowImpactMarker.Value, hitPoint, hitNormal, c);
        }

        // Mirrors Attack.FireProjectileBurst / GetProjectileSpawnPoint and Projectile.FixedUpdate.
        private bool TryPredict(out float draw, out Vector3 hitPoint, out Vector3 hitNormal, out bool hit)
        {
            draw = 0f; hitPoint = default; hitNormal = Vector3.up; hit = false;
            _points.Clear();

            Player player = Player.m_localPlayer;
            if (!player || player.IsDead() || !player.IsDrawingBow()) return false;

            ItemDrop.ItemData weapon = player.GetCurrentWeapon();
            if (weapon == null) return false;
            Attack attack = weapon.m_shared.m_attack;
            if (!attack.m_bowDraw) return false;

            draw = player.GetAttackDrawPercentage();
            if (draw < Plugin.ShowAtDraw.Value) return false;

            ItemDrop.ItemData ammo = FindAmmo(player, weapon);
            if (!string.IsNullOrEmpty(weapon.m_shared.m_ammoType) && ammo == null) return false;

            GameObject projectilePrefab = attack.m_attackProjectile;
            float vel = attack.m_projectileVel;
            float velMin = attack.m_projectileVelMin;
            AnimationCurve curve = attack.m_drawVelocityCurve;
            if (ammo != null && ammo.m_shared.m_attack.m_attackProjectile)
            {
                Attack a = ammo.m_shared.m_attack;
                projectilePrefab = a.m_attackProjectile;
                vel += a.m_projectileVel;
                velMin += a.m_projectileVelMin;
                curve = a.m_drawVelocityCurve;
            }
            if (!projectilePrefab) return false;
            Projectile proj = projectilePrefab.GetComponent<Projectile>();
            if (!proj) return false;

            float speed = Mathf.Lerp(velMin, vel, curve != null ? curve.Evaluate(draw) : draw);

            Transform body = player.transform;
            Transform origin = body;
            if (!string.IsNullOrEmpty(attack.m_attackOriginJoint))
            {
                Transform joint = Utils.FindChild(player.GetVisual().transform, attack.m_attackOriginJoint);
                if (joint) origin = joint;
            }
            Vector3 pos = origin.position + body.up * attack.m_attackHeight + body.forward * attack.m_attackRange
                          + body.right * attack.m_attackOffset;
            Vector3 dir = player.GetAimDir(pos);
            if (attack.m_launchAngle != 0f)
                dir = Quaternion.AngleAxis(attack.m_launchAngle, Vector3.Cross(Vector3.up, dir)) * dir;

            if (_solidMask == 0)
                _solidMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid",
                    "terrain", "character", "character_net", "character_ghost", "hitbox", "character_noenv", "vehicle");

            Vector3 v = dir.normalized * speed;
            float dt = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;
            float maxTime = proj.m_ttl > 0f ? proj.m_ttl : 4f;
            float maxDist = Plugin.MaxDistance.Value;
            float travelled = 0f;
            _points.Add(pos);

            for (int i = 0; i < MaxSteps && i * dt < maxTime && travelled < maxDist; i++)
            {
                v += Vector3.down * (proj.m_gravity * dt);
                float dragLoss = v.sqrMagnitude * proj.m_drag * dt;
                v -= dragLoss * v.normalized;
                Vector3 next = pos + v * dt;

                Vector3 seg = next - pos;
                float len = seg.magnitude;
                if (len > 0f && Cast(player, pos, seg / len, len, out RaycastHit rh))
                {
                    hitPoint = rh.point;
                    hitNormal = rh.normal;
                    hit = true;
                    _points.Add(rh.point);
                    break;
                }
                if (Floating.GetLiquidLevel(next) > next.y && proj.m_canHitWater)
                {
                    hitPoint = next;
                    hit = true;
                    _points.Add(next);
                    break;
                }
                travelled += len;
                pos = next;
                _points.Add(pos);
            }
            return _points.Count > 1;
        }

        private static ItemDrop.ItemData FindAmmo(Player player, ItemDrop.ItemData weapon)
        {
            string type = weapon.m_shared.m_ammoType;
            if (string.IsNullOrEmpty(type)) return null;
            Inventory inv = player.GetInventory();
            ItemDrop.ItemData ammo = player.GetAmmoItem();
            if (ammo != null && inv.ContainsItem(ammo) && ammo.m_shared.m_ammoType == type) return ammo;
            return inv.GetAmmoItem(type);
        }

        // Closest solid hit along a segment, ignoring the player's own colliders and triggers.
        private static bool Cast(Player player, Vector3 from, Vector3 dir, float len, out RaycastHit best)
        {
            best = default;
            int n = Physics.RaycastNonAlloc(from, dir, Hits, len, _solidMask, QueryTriggerInteraction.Ignore);
            float bestDist = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = Hits[i];
                if (h.distance >= bestDist || !h.collider) continue;
                if (h.collider.transform.IsChildOf(player.transform)) continue;
                best = h;
                bestDist = h.distance;
                found = true;
            }
            return found;
        }

        // Places evenly spaced dots along the predicted path.
        private void DrawDots(Color c)
        {
            _dots.Clear();
            float spacing = Mathf.Max(0.05f, Plugin.DotSpacing.Value);
            float carry = spacing * 0.5f;   // first dot a little ahead of the bow
            for (int i = 1; i < _points.Count; i++)
            {
                Vector3 a = _points[i - 1], b = _points[i];
                float segLen = Vector3.Distance(a, b);
                float d = carry;
                while (d <= segLen)
                {
                    _dots.Add(Vector3.Lerp(a, b, d / segLen));
                    d += spacing;
                }
                carry = d - segLen;
            }

            if (_particles.Length < _dots.Count) _particles = new ParticleSystem.Particle[_dots.Count * 2];
            float size = Plugin.DotSize.Value;
            int count = _dots.Count;
            for (int i = 0; i < count; i++)
            {
                // Fade the far end slightly so the arc reads as a guide, not a laser.
                float fade = Mathf.Lerp(1f, 0.6f, count > 1 ? (float)i / (count - 1) : 0f);
                Color pc = c;
                pc.a *= fade;
                _particles[i].position = _dots[i];
                _particles[i].startSize = size;
                _particles[i].startColor = pc;
                _particles[i].remainingLifetime = 1f;
                _particles[i].startLifetime = 1f;
            }
            _dotSystem.SetParticles(_particles, count);
            _root.SetActive(true);
        }

        private void DrawMarker(bool show, Vector3 point, Vector3 normal, Color c)
        {
            _marker.enabled = show;
            if (!show) return;
            const int segs = 24;
            float r = Mathf.Max(0.15f, Plugin.DotSize.Value * 4f);
            Vector3 n = normal.sqrMagnitude > 0f ? normal.normalized : Vector3.up;
            Vector3 t1 = Vector3.Cross(n, Mathf.Abs(n.y) < 0.99f ? Vector3.up : Vector3.forward).normalized;
            Vector3 t2 = Vector3.Cross(n, t1);
            Vector3 centre = point + n * 0.03f;
            _marker.positionCount = segs;
            for (int i = 0; i < segs; i++)
            {
                float ang = i * Mathf.PI * 2f / segs;
                _marker.SetPosition(i, centre + (t1 * Mathf.Cos(ang) + t2 * Mathf.Sin(ang)) * r);
            }
            _marker.startWidth = _marker.endWidth = Plugin.DotSize.Value * 0.5f;
            _marker.startColor = _marker.endColor = c;
        }

        private void Hide()
        {
            if (_root && _root.activeSelf) _root.SetActive(false);
        }

        private void EnsureVisuals()
        {
            if (_root) return;

            _material = new Material(Shader.Find("Sprites/Default")) { mainTexture = MakeDotTexture(32) };
            _material.renderQueue = 4000;   // after the world, so dots aren't lost behind foliage alpha

            _root = new GameObject("BowArc");
            DontDestroyOnLoad(_root);

            var dotsGo = new GameObject("Dots");
            dotsGo.transform.SetParent(_root.transform, false);
            _dotSystem = dotsGo.AddComponent<ParticleSystem>();
            _dotSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _dotSystem.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.maxParticles = 4096;
            main.startSpeed = 0f;
            main.loop = false;
            var emission = _dotSystem.emission;
            emission.enabled = false;
            var shape = _dotSystem.shape;
            shape.enabled = false;
            var psr = dotsGo.GetComponent<ParticleSystemRenderer>();
            psr.material = _material;
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psr.receiveShadows = false;
            psr.minParticleSize = 0f;
            psr.maxParticleSize = 1f;

            var markerGo = new GameObject("ImpactMarker");
            markerGo.transform.SetParent(_root.transform, false);
            _marker = markerGo.AddComponent<LineRenderer>();
            _marker.useWorldSpace = true;
            _marker.loop = true;
            _marker.material = new Material(Shader.Find("Sprites/Default")) { renderQueue = 4000 };
            _marker.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _marker.receiveShadows = false;
            _marker.enabled = false;
        }

        // Soft round dot with an alpha falloff at the edge.
        private static Texture2D MakeDotTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = Mathf.Clamp01((1f - d) * 3f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return tex;
        }

        private void OnDestroy()
        {
            if (_root) Destroy(_root);
            if (_material) Destroy(_material);
        }
    }
}
