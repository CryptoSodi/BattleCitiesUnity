using UnityEngine;
using UnityEngine.InputSystem;

namespace BattleCities
{
    public sealed partial class BattleGame
    {
        private InputAction dayEarlier;
        private InputAction dayLater;

        private void ConfigureDayNightShortcuts()
        {
            dayEarlier = input.AddAction("DayEarlier", InputActionType.Button, "<Keyboard>/minus");
            dayLater = input.AddAction("DayLater", InputActionType.Button, "<Keyboard>/equals");
        }

        private void UpdateDayNightShortcuts(float deltaTime)
        {
            float change = 0;
            if (dayEarlier.WasPressedThisFrame()) change -= 1f / 24f;
            if (dayLater.WasPressedThisFrame()) change += 1f / 24f;
            if (dayEarlier.IsPressed()) change -= deltaTime * .16f;
            if (dayLater.IsPressed()) change += deltaTime * .16f;
            if (Mathf.Approximately(change, 0)) return;

            weather.Cycle = false;
            weather.TimeOfDay = Mathf.Repeat(weather.TimeOfDay + change, 1f);
        }
    }
}
