using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Valheim.SettingsGui;

namespace BowArc
{
    // Adds two rows to Settings -> Gameplay:
    //   "Arrow icon flashes"      (toggle, cloned from the Skip Intro Cinematic row)
    //   "Arrow icon flash time"   (slider, cloned from the Auto Backups row, with its value label)
    // The Settings window is instantiated fresh each time it opens, so we inject in Initialize, keep the
    // edits pending, and write them to the BepInEx config only when the player presses OK (Back discards).
    [HarmonyPatch]
    internal static class SettingsUi
    {
        private const string Marker = "BowArc_";
        private const float FlashMin = 0.2f, FlashMax = 5f;

        private static Toggle _flashToggle;
        private static Slider _flashSlider;
        private static bool _loggedLayout;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameplaySettings), nameof(GameplaySettings.Initialize))]
        private static void InitializePostfix(GameplaySettings __instance)
        {
            try
            {
                Inject(__instance);
            }
            catch (Exception e)
            {
                Plugin.Warn($"Could not add Bow Arc rows to Settings > Gameplay (use the .cfg file instead): {e}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameplaySettings), nameof(GameplaySettings.OnOkAsync))]
        private static void OkPostfix()
        {
            if (_flashToggle) Plugin.AmmoIconFlash.Value = _flashToggle.isOn;
            if (_flashSlider) Plugin.AmmoIconFlashSeconds.Value = Round(_flashSlider.value);
        }

        private static void Inject(GameplaySettings settings)
        {
            if (settings.transform.Find(Marker + "done")) return;
            new GameObject(Marker + "done").transform.SetParent(settings.transform, false);
            _flashToggle = null;
            _flashSlider = null;

            Transform toggleRow = FindRow(settings.m_skipIntroCinematic.transform, settings.transform);
            Transform sliderRow = FindRow(settings.m_autoBackups.transform, settings.transform);
            if (!toggleRow || !sliderRow)
            {
                Plugin.Warn("Settings > Gameplay: layout not recognised; Bow Arc options are only in the .cfg file.");
                return;
            }

            // Insert after whichever template row is lower in the list, so we land at the bottom.
            Transform anchor = toggleRow.GetSiblingIndex() > sliderRow.GetSiblingIndex() || toggleRow.parent != sliderRow.parent
                ? toggleRow : sliderRow;

            // --- Toggle row ---
            GameObject t = Clone(toggleRow, "FlashToggle");
            _flashToggle = t.GetComponentInChildren<Toggle>(true);
            _flashToggle.onValueChanged = new Toggle.ToggleEvent();          // drop the cloned persistent listener
            SetLabel(t, null, "Bow Arc: arrow icon flashes");
            _flashToggle.isOn = Plugin.AmmoIconFlash.Value;

            // --- Slider row ---
            GameObject s = Clone(sliderRow, "FlashSeconds");
            _flashSlider = s.GetComponentInChildren<Slider>(true);
            _flashSlider.onValueChanged = new Slider.SliderEvent();
            TMP_Text valueText = FindSameText(sliderRow, s.transform, settings.m_autoBackupsText);
            SetLabel(s, valueText, "Bow Arc: icon flash time");
            _flashSlider.wholeNumbers = false;
            _flashSlider.minValue = FlashMin;
            _flashSlider.maxValue = FlashMax;
            _flashSlider.value = Mathf.Clamp(Plugin.AmmoIconFlashSeconds.Value, FlashMin, FlashMax);
            void Refresh(float v)
            {
                if (valueText) valueText.text = Round(v).ToString("0.0") + " s";
                _flashSlider.interactable = _flashToggle.isOn;
            }
            _flashSlider.onValueChanged.AddListener(Refresh);
            _flashToggle.onValueChanged.AddListener(_ => Refresh(_flashSlider.value));
            Refresh(_flashSlider.value);

            Place(anchor, new List<Transform> { t.transform, s.transform });

            if (!_loggedLayout)
            {
                _loggedLayout = true;
                Transform p = anchor.parent;
                Plugin.Log($"Settings > Gameplay: added Bow Arc rows under '{p.name}' " +
                           $"(layout={(p.GetComponent<LayoutGroup>() ? p.GetComponent<LayoutGroup>().GetType().Name : "none")}).");
            }
        }

        private static float Round(float v) => Mathf.Round(v * 10f) / 10f;

        // The row is the ancestor that sits directly in a layout group (or directly under the tab, if none).
        private static Transform FindRow(Transform control, Transform tabRoot)
        {
            Transform t = control;
            while (t.parent && t.parent != tabRoot)
            {
                if (t.parent.GetComponent<LayoutGroup>()) return t;
                t = t.parent;
            }
            // No layout group: the control's own parent is the most likely "row" (label + control).
            return control.parent && control.parent != tabRoot ? control.parent : control;
        }

        private static GameObject Clone(Transform row, string name)
        {
            GameObject go = UnityEngine.Object.Instantiate(row.gameObject, row.parent, false);
            go.name = Marker + name;
            go.SetActive(true);
            foreach (Localize loc in go.GetComponentsInChildren<Localize>(true))
                UnityEngine.Object.DestroyImmediate(loc);
            // Tooltips would show the vanilla setting's description.
            foreach (SettingsTooltip tip in go.GetComponentsInChildren<SettingsTooltip>(true))
                UnityEngine.Object.DestroyImmediate(tip);
            return go;
        }

        // Finds the clone's copy of a text that lives somewhere under the original row.
        private static TMP_Text FindSameText(Transform originalRow, Transform cloneRow, TMP_Text original)
        {
            if (!original || !original.transform.IsChildOf(originalRow)) return null;
            var path = new List<int>();
            for (Transform t = original.transform; t != originalRow; t = t.parent) path.Add(t.GetSiblingIndex());
            Transform c = cloneRow;
            for (int i = path.Count - 1; i >= 0; i--)
            {
                if (path[i] >= c.childCount) return null;
                c = c.GetChild(path[i]);
            }
            return c.GetComponent<TMP_Text>();
        }

        // The caption is the first text in the row that isn't the value read-out or part of the toggle/slider.
        private static void SetLabel(GameObject row, TMP_Text skip, string label)
        {
            Selectable control = row.GetComponentInChildren<Selectable>(true);
            foreach (TMP_Text text in row.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text == skip) continue;
                if (control && control.transform != row.transform && text.transform.IsChildOf(control.transform)) continue;
                text.text = label;
                return;
            }
            // Toggle rows often keep the caption as a child of the Toggle itself.
            foreach (TMP_Text text in row.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text == skip) continue;
                text.text = label;
                return;
            }
        }

        // With a layout group, sibling order is enough. Without one, stack the rows manually below the lowest row.
        private static void Place(Transform anchor, List<Transform> rows)
        {
            Transform parent = anchor.parent;
            int index = anchor.GetSiblingIndex();
            foreach (Transform r in rows) r.SetSiblingIndex(++index);

            var prt = parent as RectTransform;
            if (parent.GetComponent<LayoutGroup>())
            {
                if (prt) LayoutRebuilder.ForceRebuildLayoutImmediate(prt);
                GrowScrollContent(parent, rows);
                return;
            }

            // Manual: place each new row one row-height below the current lowest child.
            float rowHeight = ((RectTransform)anchor).rect.height + 4f;
            float lowest = float.MaxValue;
            var origin = (RectTransform)anchor;
            foreach (Transform child in parent)
            {
                if (child.name.StartsWith(Marker) || !child.gameObject.activeSelf || !(child is RectTransform crt)) continue;
                lowest = Mathf.Min(lowest, crt.anchoredPosition.y);
                if (crt.anchoredPosition.y <= lowest) origin = crt;
            }
            float y = origin.anchoredPosition.y;
            foreach (Transform r in rows)
            {
                y -= rowHeight;
                var rrt = (RectTransform)r;
                rrt.anchorMin = origin.anchorMin;
                rrt.anchorMax = origin.anchorMax;
                rrt.pivot = origin.pivot;
                rrt.anchoredPosition = new Vector2(((RectTransform)anchor).anchoredPosition.x, y);
            }
            GrowScrollContent(parent, rows);
        }

        // If the tab scrolls, make its content tall enough to reach the new rows.
        private static void GrowScrollContent(Transform under, List<Transform> added)
        {
            ScrollRect scroll = under.GetComponentInParent<ScrollRect>();
            if (!scroll || !scroll.content) return;
            RectTransform content = scroll.content;
            float lowest = float.MaxValue;
            var corners = new Vector3[4];
            foreach (Transform t in added)
            {
                ((RectTransform)t).GetWorldCorners(corners);
                foreach (Vector3 c in corners) lowest = Mathf.Min(lowest, content.InverseTransformPoint(c).y);
            }
            float missing = content.rect.yMin - lowest;
            if (missing <= 0f) return;
            float delta = missing + 8f;
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, content.rect.height + delta);
            content.anchoredPosition -= new Vector2(0f, delta * (1f - content.pivot.y));
        }
    }
}
