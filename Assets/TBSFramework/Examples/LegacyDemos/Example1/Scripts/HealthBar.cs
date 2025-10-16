using TurnBasedStrategyFramework.Common.Units;
using TurnBasedStrategyFramework.Unity.Units;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example1
{
    /// <summary>
    /// Handles displaying the health bar.
    /// </summary>
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] private Unit _unitReference;
        [SerializeField] private Transform _healthBar;

        private void Awake()
        {
            _unitReference.HealthChanged += OnHealthChanged;
        }

        private void OnHealthChanged(HealthChangedEventArgs eventArgs)
        {
            _healthBar.localScale = new Vector3(eventArgs.AffectedUnit.Health / eventArgs.AffectedUnit.MaxHealth, 1, 1);
        }
    }
}