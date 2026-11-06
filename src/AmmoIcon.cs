using UnityEngine;
using UnityEngine.UI;

namespace BowArc
{
    // Briefly shows the equipped arrow's icon just above the crosshair when a bow draw starts.
    internal class AmmoIcon : MonoBehaviour
    {
        private const float FadeTime = 0.25f;

        private GameObject _go;
        private Image _image;
        private bool _wasDrawing;
        private float _shownAt = -999f;

        private void LateUpdate()
        {
            Player player = Player.m_localPlayer;
            bool drawing = Plugin.Enabled.Value && Plugin.ShowAmmoIcon.Value && player && !player.IsDead()
                           && player.IsDrawingBow();

            if (drawing && !_wasDrawing)
            {
                Sprite icon = GetAmmoIcon(player);
                if (icon && EnsureImage())
                {
                    _image.sprite = icon;
                    _shownAt = Time.time;
                }
            }
            _wasDrawing = drawing;

            if (!_go) return;
            float age = Time.time - _shownAt;
            bool flash = Plugin.AmmoIconFlash.Value;
            float duration = flash ? Plugin.AmmoIconFlashSeconds.Value : Plugin.AmmoIconSeconds.Value;
            float end = flash ? duration : duration + FadeTime;
            if (!drawing || age > end)
            {
                if (_go.activeSelf) _go.SetActive(false);
                return;
            }

            float alpha;
            if (flash)
            {
                // Starts visible; a smooth on/off pulse rather than a hard blink.
                float phase = age * Plugin.AmmoIconFlashRate.Value * 2f * Mathf.PI;
                alpha = 0.5f + 0.5f * Mathf.Cos(phase);
                alpha = Mathf.SmoothStep(0f, 1f, alpha);
            }
            else
            {
                alpha = age < FadeTime ? age / FadeTime                                    // fade in
                      : age > duration ? 1f - (age - duration) / FadeTime                   // fade out
                      : 1f;
            }
            _image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha) * 0.9f);

            var rt = (RectTransform)_go.transform;
            var cross = Hud.instance ? Hud.instance.m_crosshair : null;
            Vector2 basePos = cross ? ((RectTransform)cross.transform).anchoredPosition : Vector2.zero;
            rt.anchoredPosition = basePos + new Vector2(0f, Plugin.AmmoIconOffset.Value);
            float s = Plugin.AmmoIconSize.Value;
            rt.sizeDelta = new Vector2(s, s);
            if (!_go.activeSelf) _go.SetActive(true);
        }

        private static Sprite GetAmmoIcon(Player player)
        {
            ItemDrop.ItemData weapon = player.GetCurrentWeapon();
            string type = weapon?.m_shared.m_ammoType;
            if (string.IsNullOrEmpty(type)) return null;
            Inventory inv = player.GetInventory();
            ItemDrop.ItemData ammo = player.GetAmmoItem();
            if (ammo == null || !inv.ContainsItem(ammo) || ammo.m_shared.m_ammoType != type)
                ammo = inv.GetAmmoItem(type);
            return ammo?.GetIcon();
        }

        // Sits next to the vanilla crosshair, using the same anchors so it stays centred at any resolution.
        private bool EnsureImage()
        {
            if (_go) return true;
            Image cross = Hud.instance ? Hud.instance.m_crosshair : null;
            if (!cross) return false;

            _go = new GameObject("BowArcAmmoIcon", typeof(RectTransform));
            var rt = (RectTransform)_go.transform;
            var crossRt = (RectTransform)cross.transform;
            rt.SetParent(crossRt.parent, false);
            rt.anchorMin = crossRt.anchorMin;
            rt.anchorMax = crossRt.anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            _image = _go.AddComponent<Image>();
            _image.preserveAspect = true;
            _image.raycastTarget = false;
            _go.SetActive(false);
            return true;
        }

        private void OnDestroy()
        {
            if (_go) Destroy(_go);
        }
    }
}
