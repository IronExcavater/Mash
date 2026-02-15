using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HELP_URL)]
    [AddComponentMenu("Evo/UI/Effects/Button Group Dimmer")]
    public class ButtonGroupDimmer : MonoBehaviour
    {
        Button currentButton;
        readonly List<Button> elements = new();

        void Start()
        {
            foreach (Transform child in transform)
            {
                if (child.TryGetComponent<Button>(out var btn))
                {
                    elements.Add(btn);
                    btn.onPointerEnter.AddListener(() => SetToDim(btn));
                    btn.onPointerExit.AddListener(() => SetToNormal());
                }
            }
        }

        void SetToDim(Button sourceButton)
        {
            currentButton = sourceButton;

            for (int i = 0; i < elements.Count; ++i)
            {
                if (elements[i].interactionState == InteractionState.Selected)
                    continue;

                elements[i].SetInteractable(elements[i] == sourceButton); 
            }
        }

        void SetToNormal()
        {
            currentButton = null;

            for (int i = 0; i < elements.Count; ++i)
            {
                if (elements[i].interactionState == InteractionState.Selected)
                    continue;

                elements[i].interactable = true;
            }

            StopCoroutine(nameof(SetToNormalHelper));
            StartCoroutine(nameof(SetToNormalHelper));
        }

        IEnumerator SetToNormalHelper()
        {
            yield return new WaitForSecondsRealtime(0.05f);
            if (currentButton != null) { yield break; }
            for (int i = 0; i < elements.Count; ++i) 
            {
                if (elements[i].interactionState == InteractionState.Selected)
                    continue;

                elements[i].SetState(InteractionState.Normal); 
            }
        }
    }
}