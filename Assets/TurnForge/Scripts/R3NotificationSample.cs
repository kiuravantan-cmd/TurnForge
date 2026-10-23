using System;
using R3;
using UnityEngine;

public sealed class R3NotificationSample : MonoBehaviour
{
    private readonly ReactiveProperty<int> _hp = new ReactiveProperty<int>(100);

    private void Start()
    {
        var subscription = _hp.Subscribe(hp => Debug.Log($"実験HP: {hp}"));
        _hp.Value = 80;
        _hp.Value = 80;
        subscription.Dispose();
        _hp.Value = 60;
    }
    
    /// <summary>実験の通知元を終了する。</summary>
    private void OnDestroy()
    {
        _hp.Dispose();
    }
}