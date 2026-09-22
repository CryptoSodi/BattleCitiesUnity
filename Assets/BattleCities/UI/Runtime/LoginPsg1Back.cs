using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleCities.UI
{
    public sealed class LoginPsg1Back : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private Button firstButton;
        public void Configure(Button first) => firstButton = first;
        public void OnCancel(BaseEventData eventData)
        {
            if (!firstButton || !firstButton.interactable || !EventSystem.current) return;
            EventSystem.current.SetSelectedGameObject(firstButton.gameObject);
            eventData.Use();
        }
    }
}
