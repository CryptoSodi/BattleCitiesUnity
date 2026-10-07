using TMPro;
using UnityEngine;

namespace BattleCities.LevelEditor
{
    /// <summary>Saved controls; refreshed by document selection and property changes.</summary>
    public sealed class LevelEditorBuildingPanel : MonoBehaviour
    {
        public LevelEditorDocument Document;
        public GameObject Types, Damage, DamageTab;
        public UnityEngine.UI.Toggle Override, Invulnerable, NormalShots, PowerShots;
        public TMP_InputField Health;

        public void Refresh()
        {
            var selected = Document.Selected;
            bool placed = !Document.BrushProperties && selected && selected.IsBuilding;
            if (!placed) Document.BuildingPage = "TYPE";
            bool damage = placed && Document.BuildingPage == "DAMAGE";
            Types.SetActive(!damage); Damage.SetActive(damage); DamageTab.SetActive(placed);
            Override.interactable = placed; Override.SetIsOnWithoutNotify(placed && selected.OverrideDamage);
            bool edit = placed && selected.OverrideDamage;
            Health.interactable = Invulnerable.interactable = NormalShots.interactable = PowerShots.interactable = edit;
            Health.SetTextWithoutNotify(placed ? selected.Damage.hitPoints.ToString() : "3");
            Invulnerable.SetIsOnWithoutNotify(placed && selected.Damage.invulnerable);
            NormalShots.SetIsOnWithoutNotify(!placed || selected.Damage.normalShots);
            PowerShots.SetIsOnWithoutNotify(!placed || selected.Damage.powerShots);
        }
    }
}
