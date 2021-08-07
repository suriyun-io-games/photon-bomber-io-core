using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class PowerUpEntity : MonoBehaviourPunCallbacks
{
    public const float DestroyDelay = 1f;
    [Header("Stats / Currencies")]
    public CharacterStats stats;
    public InGameCurrency[] currencies;
    [Header("Effect")]
    public EffectEntity powerUpEffect;

    private bool isDead;

    private void Awake()
    {
        gameObject.layer = Physics.IgnoreRaycastLayer;
        var collider = GetComponent<Collider>();
        collider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isDead)
            return;

        if (other.gameObject.layer == Physics.IgnoreRaycastLayer)
            return;

        var character = other.GetComponent<CharacterEntity>();
        var gameplayManager = GameplayManager.Singleton;
        if (character != null && !character.IsDeadMarked)
        {
            isDead = true;
            EffectEntity.PlayEffect(powerUpEffect, character.effectTransform);
            if (PhotonNetwork.IsMasterClient)
                character.addStats += stats;
            if (currencies != null && currencies.Length > 0 &&
                character.photonView.IsMine &&
                !(character is BotEntity))
            {
                foreach (var currency in currencies)
                {
                    MonetizationManager.Save.AddCurrency(currency.id, currency.amount);
                }
            }
            StartCoroutine(DestroyRoutine());
        }
    }

    IEnumerator DestroyRoutine()
    {
        var renderers = gameObject.GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            foreach (var renderer in renderers)
            {
                renderer.enabled = false;
            }
        }
        yield return new WaitForSeconds(DestroyDelay);
        // Destroy this on all clients
        if (PhotonNetwork.IsMasterClient)
            PhotonNetwork.Destroy(gameObject);
    }
}
