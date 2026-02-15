using UnityEngine;
using TMPro;

namespace Evo.UI
{
    /// <summary>
    /// Applies a horizontal gradient to TextMeshPro text components.
    /// This component handles per-character gradient coloring for smooth text transitions.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TextMeshProUGUI))]
    [HelpURL(Constants.HELP_URL + "effects/tmp-gradient")]
    [AddComponentMenu("Evo/UI/Effects/Smooth TMP Gradient")]
    public class SmoothTMPGradient : MonoBehaviour
    {
        TextMeshProUGUI tmpComponent;
        bool needsUpdate = true;
        int lastCharacterCount = -1;
        Color lastTopLeft;
        Color lastTopRight;

        void Awake()
        {
            tmpComponent = GetComponent<TextMeshProUGUI>();
        }

        void OnEnable()
        {
            needsUpdate = true;
            ApplyGradient();
        }

        void LateUpdate()
        {
            if (tmpComponent.havePropertiesChanged || NeedsGradientUpdate())
            {
                ApplyGradient();
            }
        }

        bool NeedsGradientUpdate()
        {
            if (tmpComponent == null) 
                return false;

            // Check if text length changed
            int currentCharCount = tmpComponent.textInfo.characterCount;
            if (currentCharCount != lastCharacterCount)
            {
                lastCharacterCount = currentCharCount;
                return true;
            }

            // Check if gradient colors changed
            if (!tmpComponent.enableVertexGradient) 
                return false;

            if (lastTopLeft != tmpComponent.colorGradient.topLeft || lastTopRight != tmpComponent.colorGradient.topRight)
            {
                lastTopLeft = tmpComponent.colorGradient.topLeft;
                lastTopRight = tmpComponent.colorGradient.topRight;
                return true;
            }

            return needsUpdate;
        }

        void ApplyGradient()
        {
            if (tmpComponent == null)
            {
                tmpComponent = GetComponent<TextMeshProUGUI>();
                if (tmpComponent == null) { return; }
            }

            if (!tmpComponent.enableVertexGradient) 
                return;

            tmpComponent.ForceMeshUpdate();
            TMP_TextInfo textInfo = tmpComponent.textInfo;
            int count = textInfo.characterCount;

            // No characters to process
            if (count == 0) 
                return;

            // Calculate gradient steps
            Color[] steps = CalculateGradientSteps(
                tmpComponent.colorGradient.topLeft,
                tmpComponent.colorGradient.topRight,
                count + 1
            );

            // Create vertex gradients for each character
            VertexGradient[] characterGradients = new VertexGradient[steps.Length];
            for (int i = 0; i < steps.Length - 1; i++)
            {
                characterGradients[i] = new VertexGradient(
                    steps[i],     // topLeft
                    steps[i + 1], // topRight
                    steps[i],     // bottomLeft
                    steps[i + 1]  // bottomRight
                );
            }

            // Apply gradients to each character
            ApplyGradientsToCharacters(textInfo, characterGradients, count);

            needsUpdate = false;
        }

        void ApplyGradientsToCharacters(TMP_TextInfo textInfo, VertexGradient[] gradients, int characterCount)
        {
            for (int charIndex = 0; charIndex < characterCount; charIndex++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[charIndex];

                // Skip invisible characters
                if (!charInfo.isVisible) 
                    continue;

                int materialIndex = charInfo.materialReferenceIndex;
                Color32[] colors = textInfo.meshInfo[materialIndex].colors32;
                int vertexIndex = charInfo.vertexIndex;

                // Apply gradient to the four vertices of the character
                colors[vertexIndex + 0] = gradients[charIndex].bottomLeft;
                colors[vertexIndex + 1] = gradients[charIndex].topLeft;
                colors[vertexIndex + 2] = gradients[charIndex].bottomRight;
                colors[vertexIndex + 3] = gradients[charIndex].topRight;
            }

            // Update the mesh with new colors
            tmpComponent.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        static Color[] CalculateGradientSteps(Color start, Color end, int steps)
        {
            if (steps <= 1) return new[] { start };

            Color[] result = new Color[steps];

            // Calculate color component increments
            float rStep = (end.r - start.r) / (steps - 1);
            float gStep = (end.g - start.g) / (steps - 1);
            float bStep = (end.b - start.b) / (steps - 1);
            float aStep = (end.a - start.a) / (steps - 1);

            // Generate interpolated colors
            for (int i = 0; i < steps; i++)
            {
                result[i] = new Color(
                    start.r + (rStep * i),
                    start.g + (gStep * i),
                    start.b + (bStep * i),
                    start.a + (aStep * i)
                );
            }

            return result;
        }

        /// <summary>
        /// Forces a gradient refresh.
        /// </summary>
        public void RefreshGradient()
        {
            needsUpdate = true;
        }
    }
}