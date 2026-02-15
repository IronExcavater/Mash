using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

namespace Evo.UI
{
    [HelpURL(Constants.HELP_URL + "styler")]
    [CreateAssetMenu(fileName = "Styler Preset", menuName = "Evo/UI/Styler Preset")]
    public class StylerPreset : ScriptableObject
    {
        [EvoHeader("Audio", Constants.CUSTOM_EDITOR_ID)]
        public List<Styler.AudioItem> audioItems = new()
        {
            new("Hover SFX", null),
            new("Click SFX", null),
            new("Notification SFX", null)
        };

        [EvoHeader("Color", Constants.CUSTOM_EDITOR_ID)]
        public List<Styler.ColorItem> colorItems = new()
        {
            new Styler.ColorItem("Primary", Color.white),
            new Styler.ColorItem("Secondary", new Color(0.1f, 0.15f, 0.2f, 1f))
        };

        [EvoHeader("Font", Constants.CUSTOM_EDITOR_ID)]
        public List<Styler.FontItem> fontItems = new()
        {
            new Styler.FontItem("Thin", null),
            new Styler.FontItem("Light", null),
            new Styler.FontItem("Regular", null),
            new Styler.FontItem("Semibold", null),
            new Styler.FontItem("Bold", null)
        };

        [EvoHeader("Settings", Constants.CUSTOM_EDITOR_ID)]
        public Styler.UpdateMode updateMode = Styler.UpdateMode.Adaptive;

        /// <summary>
        /// Get audio clip from the preset.
        /// </summary>
        public AudioClip GetAudio(string itemID)
        {
            var item = audioItems.FirstOrDefault(x => x.itemID == itemID);
            return item?.audioAsset;
        }

        /// <summary>
        /// Get color from the preset.
        /// </summary>
        public Color GetColor(string itemID)
        {
            var item = colorItems.FirstOrDefault(x => x.itemID == itemID);
            return item?.colorValue ?? Color.white;
        }

        /// <summary>
        /// Get font from the preset.
        /// </summary>
        public TMP_FontAsset GetFont(string itemID)
        {
            var item = fontItems.FirstOrDefault(x => x.itemID == itemID);
            return item?.fontAsset;
        }

        /// <summary>
        /// Set audio clip for an existing item or add new if it doesn't exist.
        /// </summary>
        public void SetAudio(string itemID, AudioClip audioClip)
        {
            var item = audioItems.FirstOrDefault(x => x.itemID == itemID);
            if (item != null) { item.audioAsset = audioClip; }
            else { audioItems.Add(new Styler.AudioItem(itemID, audioClip)); }
        }

        /// <summary>
        /// Set color for an existing item or add new if it doesn't exist.
        /// </summary>
        public void SetColor(string itemID, Color color)
        {
            var item = colorItems.FirstOrDefault(x => x.itemID == itemID);
            if (item != null) { item.colorValue = color; }
            else { colorItems.Add(new Styler.ColorItem(itemID, color)); }
        }

        /// <summary>
        /// Set font for an existing item or add new if it doesn't exist.
        /// </summary>
        public void SetFont(string itemID, TMP_FontAsset font)
        {
            var item = fontItems.FirstOrDefault(x => x.itemID == itemID);
            if (item != null) { item.fontAsset = font; }
            else { fontItems.Add(new Styler.FontItem(itemID, font)); }
        }

        /// <summary>
        /// Add an audio item to the preset.
        /// </summary>
        public void AddAudio(string itemID, AudioClip audioClip)
        {
            if (audioItems.Any(x => x.itemID == itemID))
            {
                Debug.LogWarning($"Audio item with ID '{itemID}' already exists.", this);
                return;
            }
            audioItems.Add(new Styler.AudioItem(itemID, audioClip));
        }

        /// <summary>
        /// Add a color item to the preset.
        /// </summary>
        public void AddColor(string itemID, Color color)
        {
            if (colorItems.Any(x => x.itemID == itemID))
            {
                Debug.LogWarning($"Color item with ID '{itemID}' already exists.", this);
                return;
            }
            colorItems.Add(new Styler.ColorItem(itemID, color));
        }

        /// <summary>
        /// Add a font item to the preset.
        /// </summary>
        public void AddFont(string itemID, TMP_FontAsset font)
        {
            if (fontItems.Any(x => x.itemID == itemID))
            {
                Debug.LogWarning($"Font item with ID '{itemID}' already exists.", this);
                return;
            }
            fontItems.Add(new Styler.FontItem(itemID, font));
        }

        /// <summary>
        /// Remove an audio item from the preset.
        /// </summary>
        public bool RemoveAudio(string itemID)
        {
            var item = audioItems.FirstOrDefault(x => x.itemID == itemID);
            if (item != null)
            {
                audioItems.Remove(item);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Remove a color item from the preset.
        /// </summary>
        public bool RemoveColor(string itemID)
        {
            var item = colorItems.FirstOrDefault(x => x.itemID == itemID);
            if (item != null)
            {
                colorItems.Remove(item);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Remove a font item from the preset.
        /// </summary>
        public bool RemoveFont(string itemID)
        {
            var item = fontItems.FirstOrDefault(x => x.itemID == itemID);
            if (item != null)
            {
                fontItems.Remove(item);
                return true;
            }
            return false;
        }

#if UNITY_EDITOR
        [HideInInspector] public bool audioFoldout = true;
        [HideInInspector] public bool colorFoldout = true;
        [HideInInspector] public bool fontFoldout = true;
        [HideInInspector] public bool settingsFoldout = true;

        void OnValidate()
        {
            if (updateMode == Styler.UpdateMode.Always || (!Application.isPlaying && updateMode == Styler.UpdateMode.Adaptive))
            {
                NotifyStylerObjects();
            }
        }

        void NotifyStylerObjects()
        {
            // Find all MonoBehaviours, but filter for the ones implementing our interface
            var targets = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).OfType<IStylerHandler>();
            foreach (var target in targets)
            {
                if (target.Preset == this)
                {
                    target.UpdateStyler();
                }
            }
        }
#endif
    }
}