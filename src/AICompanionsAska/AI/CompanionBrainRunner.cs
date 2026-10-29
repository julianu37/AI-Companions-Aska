using UnityEngine;

namespace AICompanionsAska.AI;

/// <summary>
/// Ponte entre o ciclo do Unity e o <see cref="CompanionBrain"/> estático.
/// Registrado por <c>ClassInjector</c> no <c>Plugin.Load</c>; nada de lógica
/// pesada aqui, só o pump/drain throttled.
/// </summary>
public sealed class CompanionBrainRunner : MonoBehaviour
{
    private float _nextPump;

    private void Update()
    {
        CompanionBrain.Drain();

        if (Time.unscaledTime < _nextPump)
            return;

        _nextPump = Time.unscaledTime + 0.1f;
        CompanionBrain.Pump();
    }
}
