using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon;

[RequireComponent(typeof(Collider))]
public class BrickEntity : PunBehaviour
{
    [Tooltip("Use this delay to play dead animation")]
    public float disableRenderersDelay;
    public Animator animator;
    protected bool _isDead;
    protected bool _isRendererDisabled;
    public bool isDead
    {
        get { return _isDead; }
        set
        {
            if (PhotonNetwork.isMasterClient && value != isDead)
            {
                _isDead = value;
                photonView.RPC("RpcUpdateIsDead", PhotonTargets.Others, value);
            }
        }
    }
    /// <summary>
    /// Use this flag to set brick renderer disabled, so when player's character come closer when is dead player won't see the brick
    /// </summary>
    public bool isRendererDisabled
    {
        get { return _isRendererDisabled; }
        set
        {
            if (PhotonNetwork.isMasterClient && value != isRendererDisabled)
            {
                _isRendererDisabled = value;
                photonView.RPC("RpcUpdateIsRendererDisabled", PhotonTargets.All, value);
            }
        }
    }
    public float deathTime { get; private set; }
    
    private Transform tempTransform;
    public Transform TempTransform
    {
        get
        {
            if (tempTransform == null)
                tempTransform = GetComponent<Transform>();
            return tempTransform;
        }
    }
    private Collider tempCollider;
    public Collider TempCollider
    {
        get
        {
            if (tempCollider == null)
                tempCollider = GetComponent<Collider>();
            return tempCollider;
        }
    }

    private void Awake()
    {
        gameObject.layer = GameInstance.Singleton.brickLayer;
    }

    public override void OnPhotonPlayerConnected(PhotonPlayer newPlayer)
    {
        base.OnPhotonPlayerConnected(newPlayer);
        if (!PhotonNetwork.isMasterClient)
            return;
        photonView.RPC("RpcUpdateIsDead", newPlayer, isDead);
        photonView.RPC("RpcUpdateIsRendererDisabled", newPlayer, isRendererDisabled);
    }

    private void Update()
    {
        TempCollider.enabled = !isDead;

        if (!PhotonNetwork.isMasterClient || !isDead)
            return;

        // Respawning.
        var gameplayManager = GameplayManager.Singleton;
        if (Time.unscaledTime - deathTime >= gameplayManager.brickRespawnDuration && !IsNearPlayerOrBomb())
        {
            KillNearlyPowerup();
            isDead = false;
            if (animator != null)
                animator.SetBool("IsDead", isDead);
            photonView.RPC("RpcIsDeadChanged", PhotonTargets.Others, isDead);
            isRendererDisabled = isDead;
        }
    }

    public void ReceiveDamage()
    {
        if (!PhotonNetwork.isMasterClient || isDead)
            return;
        deathTime = Time.unscaledTime;
        isDead = true;
        if (animator != null)
            animator.SetBool("IsDead", isDead);
        StartCoroutine(PlayDeadAnimation());
        photonView.RPC("RpcIsDeadChanged", PhotonTargets.Others, isDead);
        // Spawn powerup when it dead.
        GameplayManager.Singleton.SpawnPowerUp(TempTransform.position);
    }

    private void SetEnabledAllRenderer(bool isEnable)
    {
        // GetComponentsInChildren will include this transform so it will be fine without GetComponents calls
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
            renderer.enabled = isEnable;
    }

    private IEnumerator PlayDeadAnimation()
    {
        yield return new WaitForSeconds(disableRenderersDelay);
        if (PhotonNetwork.isMasterClient)
        {
            isRendererDisabled = true;
            SetEnabledAllRenderer(!isRendererDisabled);
        }
    }

    private bool IsNearPlayerOrBomb()
    {
        var currentPosition = TempTransform.position;
        var colliders = Physics.OverlapSphere(currentPosition, 5);
        foreach (var collider in colliders)
        {
            if (collider.GetComponent<CharacterEntity>() != null || collider.GetComponent<BombEntity>() != null)
                return true;
        }
        return false;
    }

    private void KillNearlyPowerup()
    {
        var currentPosition = TempTransform.position;
        var colliders = Physics.OverlapSphere(currentPosition, 0.4f);
        foreach (var collider in colliders)
        {
            var powerUp = collider.GetComponent<PowerUpEntity>();
            if (powerUp != null)
                PhotonNetwork.Destroy(powerUp.gameObject);
        }
    }

    [PunRPC]
    private void RpcIsDeadChanged(bool isDead)
    {
        if (PhotonNetwork.isMasterClient)
            return;

        if (!isDead)
        {
            if (animator != null)
                animator.SetBool("IsDead", isDead);
            SetEnabledAllRenderer(true);
        }
        else
        {
            if (animator != null)
                animator.SetBool("IsDead", isDead);
            StartCoroutine(PlayDeadAnimation());
        }
    }

    [PunRPC]
    protected void RpcUpdateIsDead(bool isDead)
    {
        _isDead = isDead;
    }

    [PunRPC]
    protected void RpcUpdateIsRendererDisabled(bool isRendererDisabled)
    {
        _isRendererDisabled = isRendererDisabled;
        SetEnabledAllRenderer(!isRendererDisabled);
    }
}
