using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

[RequireComponent(typeof(Rigidbody))]
public class CharacterEntity : BaseNetworkGameCharacter
{
    public const float DISCONNECT_WHEN_NOT_RESPAWN_DURATION = 60;
    public Transform damageLaunchTransform;
    public Transform effectTransform;
    public Transform characterModelTransform;
    public GameObject[] localPlayerObjects;
    [Header("UI")]
    public Text nameText;
    [Header("Effect")]
    public GameObject invincibleEffect;

    protected SyncWatchAdsCountRpcComponent syncWatchAdsCount = null;
    public virtual byte WatchAdsCount
    {
        get { return syncWatchAdsCount.Value; }
        set { syncWatchAdsCount.Value = value; }
    }

    protected SyncIsDeadRpcComponent syncIsDead = null;
    public virtual bool IsDeadMarked
    {
        get { return syncIsDead.Value; }
        set { syncIsDead.Value = value; }
    }

    protected SyncSelectCharacterRpcComponent syncSelectCharacter = null;
    public virtual int SelectCharacter
    {
        get { return syncSelectCharacter.Value; }
        set { syncSelectCharacter.Value = value; }
    }

    protected SyncSelectHeadRpcComponent syncSelectHead = null;
    public virtual int SelectHead
    {
        get { return syncSelectHead.Value; }
        set { syncSelectHead.Value = value; }
    }

    protected SyncSelectBombRpcComponent syncSelectBomb = null;
    public virtual int SelectBomb
    {
        get { return syncSelectBomb.Value; }
        set { syncSelectBomb.Value = value; }
    }

    protected SyncSelectCustomEquipmentsRpcComponent syncSelectCustomEquipments = null;
    public virtual int[] SelectCustomEquipments
    {
        get { return syncSelectCustomEquipments.Value; }
        set { syncSelectCustomEquipments.Value = value; }
    }

    protected SyncIsInvincibleRpcComponent syncIsInvincible = null;
    public virtual bool IsInvincible
    {
        get { return syncIsInvincible.Value; }
        set { syncIsInvincible.Value = value; }
    }

    protected SyncCharacterStatsRpcComponent syncAddStats = null;
    public virtual CharacterStats addStats
    {
        get { return syncAddStats.Value; }
        set { syncAddStats.Value = value; }
    }

    protected SyncExtraRpcComponent syncExtra = null;
    public virtual string Extra
    {
        get { return syncExtra.Value; }
        set { syncExtra.Value = value; }
    }

    public override bool IsDead
    {
        get { return IsDeadMarked; }
    }

    public override bool IsBot
    {
        get { return false; }
    }

    protected readonly List<BombEntity> bombs = new List<BombEntity>();
    protected Camera targetCamera;
    protected CharacterModel characterModel;
    protected CharacterData characterData;
    protected HeadData headData;
    protected BombData bombData;
    protected Dictionary<int, CustomEquipmentData> customEquipmentDict = new Dictionary<int, CustomEquipmentData>();
    protected bool isMobileInput;
    protected Vector2 inputMove;
    protected Vector3? previousPosition;
    protected Vector3 currentVelocity;
    protected Vector3 currentMoveDirection;
    protected BombEntity kickingBomb;

    public bool IsReady { get; private set; }
    public float DeathTime { get; private set; }
    public float InvincibleTime { get; private set; }

    private bool isHidding;
    public bool IsHidding
    {
        get { return isHidding; }
        set
        {
            isHidding = value;
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
                renderer.enabled = !isHidding;
            var canvases = GetComponentsInChildren<Canvas>();
            foreach (var canvas in canvases)
                canvas.enabled = !isHidding;
        }
    }
    
    public Transform CacheTransform { get; private set; }
    public Rigidbody CacheRigidbody { get; private set; }
    public Collider CacheCollider { get; private set; }

    public int PowerUpBombRange
    {
        get
        {
            var max = GameplayManager.Singleton.maxBombRangePowerUp;
            if (addStats.bombRange > max)
                return max;
            return addStats.bombRange;
        }
    }

    public int PowerUpBombAmount
    {
        get
        {
            var max = GameplayManager.Singleton.maxBombAmountPowerUp;
            if (addStats.bombAmount > max)
                return max;
            return addStats.bombAmount;
        }
    }

    public int PowerUpHeart
    {
        get
        {
            var max = GameplayManager.Singleton.maxHeartPowerUp;
            if (addStats.heart > max)
                return max;
            return addStats.heart;
        }
    }

    public int PowerUpMoveSpeed
    {
        get
        {
            var max = GameplayManager.Singleton.maxMoveSpeedPowerUp;
            if (addStats.moveSpeed > max)
                return max;
            return addStats.moveSpeed;
        }
    }

    public bool PowerUpCanKickBomb
    {
        get { return addStats.canKickBomb; }
    }

    public int TotalMoveSpeed
    {
        get
        {
            var gameplayManager = GameplayManager.Singleton;
            var total = gameplayManager.minMoveSpeed + (PowerUpMoveSpeed * gameplayManager.addMoveSpeedPerPowerUp);
            return total;
        }
    }

    protected override void Init()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;
        base.Init();
        WatchAdsCount = 0;
        IsDeadMarked = false;
        SelectCharacter = 0;
        SelectHead = 0;
        SelectBomb = 0;
        IsInvincible = false;
        Extra = string.Empty;
    }

    protected override void Awake()
    {
        base.Awake();
        gameObject.layer = GameInstance.Singleton.characterLayer;
        CacheTransform = transform;
        CacheRigidbody = GetComponent<Rigidbody>();
        CacheCollider = GetComponent<Collider>();
        if (damageLaunchTransform == null)
            damageLaunchTransform = CacheTransform;
        if (effectTransform == null)
            effectTransform = CacheTransform;
        if (characterModelTransform == null)
            characterModelTransform = CacheTransform;
        foreach (var localPlayerObject in localPlayerObjects)
        {
            localPlayerObject.SetActive(false);
        }
        DeathTime = Time.unscaledTime;
    }

    protected override void OnStartLocalPlayer()
    {
        if (photonView.IsMine)
        {
            var followCam = FindObjectOfType<FollowCamera>();
            followCam.target = CacheTransform;
            targetCamera = followCam.GetComponent<Camera>();

            foreach (var localPlayerObject in localPlayerObjects)
            {
                localPlayerObject.SetActive(true);
            }

            StartCoroutine(DelayReady());
        }
    }

    IEnumerator DelayReady()
    {
        yield return new WaitForSeconds(0.5f);
        // Add some delay before ready to make sure that it can receive team and game rule
        // TODO: Should improve this (Or remake team system, one which made by Photon is not work well)
        var uiGameplay = FindObjectOfType<UIGameplay>();
        if (uiGameplay != null)
            uiGameplay.FadeOut();
        CmdReady();
    }

    protected override void Update()
    {
        base.Update();
        if (NetworkManager != null && NetworkManager.IsMatchEnded)
            return;
        
        if (IsDead)
        {
            if (!PhotonNetwork.IsMasterClient && photonView.IsMine && Time.unscaledTime - DeathTime >= DISCONNECT_WHEN_NOT_RESPAWN_DURATION)
                GameNetworkManager.Singleton.LeaveRoom();
        }

        if (PhotonNetwork.IsMasterClient && IsInvincible && Time.unscaledTime - InvincibleTime >= GameplayManager.Singleton.invincibleDuration)
            IsInvincible = false;
        if (invincibleEffect != null)
            invincibleEffect.SetActive(IsInvincible);
        if (nameText != null)
            nameText.text = PlayerName;
        UpdateAnimation();
        UpdateInput();
        CacheCollider.enabled = PhotonNetwork.IsMasterClient || photonView.IsMine;
    }

    private void FixedUpdate()
    {
        if (!previousPosition.HasValue)
            previousPosition = CacheTransform.position;
        var currentMove = CacheTransform.position - previousPosition.Value;
        currentVelocity = currentMove / Time.deltaTime;
        previousPosition = CacheTransform.position;

        if (NetworkManager != null && NetworkManager.IsMatchEnded)
            return;

        UpdateMovements();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!photonView.IsMine)
            return;

        if (PowerUpCanKickBomb)
            kickingBomb = collision.gameObject.GetComponent<BombEntity>();
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!photonView.IsMine)
            return;

        if (!PowerUpCanKickBomb || kickingBomb == null)
            return;

        if (kickingBomb == collision.gameObject.GetComponent<BombEntity>())
        {
            var moveDirNorm = currentMoveDirection.normalized;
            var heading = kickingBomb.CacheTransform.position - CacheTransform.position;
            var distance = heading.magnitude;
            var direction = heading / distance;
            
            if ((moveDirNorm.x > 0.5f  && direction.x > 0.5f) ||
                (moveDirNorm.z > 0.5f && direction.z > 0.5f) ||
                (moveDirNorm.x < -0.5f && direction.x < -0.5f) ||
                (moveDirNorm.z < -0.5f && direction.z < -0.5f))
            {
                // Kick bomb if direction is opposite
                CmdKick(kickingBomb.photonView.ViewID, (int)moveDirNorm.x, (int)moveDirNorm.z);
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (!photonView.IsMine)
            return;

        if (!PowerUpCanKickBomb || kickingBomb == collision.gameObject.GetComponent<BombEntity>())
            kickingBomb = null;
    }

    protected virtual void UpdateInput()
    {
        if (!photonView.IsMine || IsDeadMarked)
            return;

        bool canControl = true;
        var fields = FindObjectsOfType<InputField>();
        foreach (var field in fields)
        {
            if (field.isFocused)
            {
                canControl = false;
                break;
            }
        }

        isMobileInput = Application.isMobilePlatform;
#if UNITY_EDITOR
        isMobileInput = GameInstance.Singleton.showJoystickInEditor;
#endif
        InputManager.useMobileInputOnNonMobile = isMobileInput;

        inputMove = Vector2.zero;
        if (canControl)
        {
            inputMove = new Vector2(InputManager.GetAxis("Horizontal", false), InputManager.GetAxis("Vertical", false));
            if (InputManager.GetButtonDown("Fire1"))
                CmdPlantBomb(RoundXZ(CacheTransform.position));
        }
    }

    protected virtual void UpdateAnimation()
    {
        if (characterModel == null)
            return;
        var animator = characterModel.CacheAnimator;
        if (animator == null)
            return;
        if (IsDeadMarked)
        {
            animator.SetBool("IsDead", true);
            animator.SetFloat("JumpSpeed", 0);
            animator.SetFloat("MoveSpeed", 0);
            animator.SetBool("IsGround", true);
        }
        else
        {
            var velocity = currentVelocity;
            var xzMagnitude = new Vector3(velocity.x, 0, velocity.z).magnitude;
            var ySpeed = velocity.y;
            animator.SetBool("IsDead", false);
            animator.SetFloat("JumpSpeed", ySpeed);
            animator.SetFloat("MoveSpeed", xzMagnitude);
            animator.SetBool("IsGround", Mathf.Abs(ySpeed) < 0.5f);
        }
    }

    protected virtual float GetMoveSpeed()
    {
        return TotalMoveSpeed * GameplayManager.REAL_MOVE_SPEED_RATE;
    }

    protected virtual void Move(Vector3 direction)
    {
        if (direction.sqrMagnitude > 0)
        {
            if (direction.sqrMagnitude > 1)
                direction = direction.normalized;
            direction.y = 0;

            var targetSpeed = GetMoveSpeed();
            var targetVelocity = direction * targetSpeed;
            var rigidbodyVel = CacheRigidbody.velocity;
            rigidbodyVel.y = 0;
            if (rigidbodyVel.sqrMagnitude < 1)
                CacheTransform.position += targetVelocity * Time.deltaTime;

            var rotateHeading = (CacheTransform.position + direction) - CacheTransform.position;
            var targetRotation = Quaternion.LookRotation(rotateHeading);
            CacheTransform.rotation = Quaternion.Lerp(CacheTransform.rotation, targetRotation, Time.deltaTime * 6f);
        }
    }

    protected virtual void UpdateMovements()
    {
        if (!photonView.IsMine || IsDeadMarked)
            return;

        currentMoveDirection = new Vector3(inputMove.x, 0, inputMove.y);
        Move(currentMoveDirection);
    }

    public void RemoveBomb(BombEntity bomb)
    {
        if (!PhotonNetwork.IsMasterClient || bombs == null)
            return;
        bombs.Remove(bomb);
    }
    
    public void ReceiveDamage(CharacterEntity attacker)
    {
        if (IsDeadMarked || IsInvincible)
            return;

        var gameplayManager = GameplayManager.Singleton;
        if (!gameplayManager.CanReceiveDamage(this, attacker))
            return;

        if (addStats.heart == 0)
        {
            if (attacker != null)
                attacker.KilledTarget(this);
            photonView.TargetRPC(RpcTargetDead, photonView.Owner);
            DeathTime = Time.unscaledTime;
            ++syncDieCount.Value;
            IsDeadMarked = true;
            var velocity = CacheRigidbody.velocity;
            CacheRigidbody.velocity = new Vector3(0, velocity.y, 0);
        }

        if (addStats.heart > 0)
        {
            var tempStats = addStats;
            --tempStats.heart;
            addStats = tempStats;
            ServerInvincible();
        }
    }
    
    public void KilledTarget(CharacterEntity target)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        var gameplayManager = GameplayManager.Singleton;
        if (target == this)
        {
            syncScore.Value += gameplayManager.suicideScore;
            GameNetworkManager.Singleton.OnScoreIncrease(this, gameplayManager.suicideScore);
        }
        else
        {
            syncScore.Value += gameplayManager.killScore;
            GameNetworkManager.Singleton.OnScoreIncrease(this, gameplayManager.killScore);
            foreach (var rewardCurrency in gameplayManager.rewardCurrencies)
            {
                var currencyId = rewardCurrency.currencyId;
                var amount = Random.Range(rewardCurrency.randomAmountMin, rewardCurrency.randomAmountMax);
                photonView.TargetRPC(RpcTargetRewardCurrency, photonView.Owner, currencyId, amount);
            }
            var increaseKill = 1;
            syncKillCount.Value += increaseKill;
            GameNetworkManager.Singleton.OnKillIncrease(this, increaseKill);
        }
        GameNetworkManager.Singleton.SendKillNotify(PlayerName, target.PlayerName, bombData == null ? string.Empty : bombData.GetId());
    }

    public virtual void OnSpawn() { }

    public virtual void OnUpdateSelectCharacter(int selectCharacter)
    {
        if (characterModel != null)
            Destroy(characterModel.gameObject);
        characterData = GameInstance.GetCharacter(selectCharacter);
        if (characterData == null || characterData.modelObject == null)
            return;
        characterModel = Instantiate(characterData.modelObject, characterModelTransform);
        characterModel.transform.localPosition = Vector3.zero;
        characterModel.transform.localEulerAngles = Vector3.zero;
        characterModel.transform.localScale = Vector3.one;
        if (headData != null)
            characterModel.SetHeadModel(headData.modelObject);
        if (customEquipmentDict != null)
        {
            characterModel.ClearCustomModels();
            foreach (var value in customEquipmentDict.Values)
            {
                characterModel.SetCustomModel(value.containerIndex, value.modelObject);
            }
        }
        characterModel.gameObject.SetActive(true);
    }

    public virtual void OnUpdateSelectHead(int selectHead)
    {
        headData = GameInstance.GetHead(selectHead);
        if (characterModel != null && headData != null)
            characterModel.SetHeadModel(headData.modelObject);
    }

    public virtual void OnUpdateSelectBomb(int selectBomb)
    {
        bombData = GameInstance.GetBomb(selectBomb);
    }

    public virtual void OnUpdateSelectCustomEquipments(int[] selectCustomEquipments)
    {
        if (characterModel != null)
            characterModel.ClearCustomModels();
        customEquipmentDict.Clear();
        if (selectCustomEquipments != null)
        {
            for (var i = 0; i < selectCustomEquipments.Length; ++i)
            {
                var customEquipmentData = GameInstance.GetCustomEquipment(selectCustomEquipments[i]);
                if (customEquipmentData != null &&
                    !customEquipmentDict.ContainsKey(customEquipmentData.containerIndex))
                {
                    customEquipmentDict[customEquipmentData.containerIndex] = customEquipmentData;
                    if (characterModel != null)
                        characterModel.SetCustomModel(customEquipmentData.containerIndex, customEquipmentData.modelObject);
                }
            }
        }
    }

    public void ServerInvincible()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        InvincibleTime = Time.unscaledTime;
        IsInvincible = true;
    }
    
    public void ServerSpawn(bool isWatchedAds)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (Respawn(isWatchedAds))
        {
            var gameplayManager = GameplayManager.Singleton;
            ServerInvincible();
            OnSpawn();
            var position = gameplayManager.GetCharacterSpawnPosition(this);
            CacheTransform.position = position;
            photonView.TargetRPC(RpcTargetSpawn, photonView.Owner, position.x, position.y, position.z);
            IsDeadMarked = false;
        }
    }
    
    public void ServerRespawn(bool isWatchedAds)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (CanRespawn(isWatchedAds))
            ServerSpawn(isWatchedAds);
    }
    
    public void Reset()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        IsDeadMarked = false;
        var stats = new CharacterStats();
        if (headData != null)
            stats += headData.stats;
        if (characterData != null)
            stats += characterData.stats;
        if (bombData != null)
            stats += bombData.stats;
        if (customEquipmentDict != null)
        {
            foreach (var value in customEquipmentDict.Values)
                stats += value.stats;
        }
        addStats = stats;
        bombs.Clear();
    }

    public void CmdInit(int selectHead, int selectCharacter, int selectBomb, int[] selectCustomEquipments, string extra)
    {
        photonView.MasterRPC(RpcServerInit, selectHead, selectCharacter, selectBomb, selectCustomEquipments, extra);
    }

    [PunRPC]
    protected void RpcServerInit(int selectHead, int selectCharacter, int selectBomb, int[] selectCustomEquipments, string extra)
    {
        IsDeadMarked = false;
        SelectHead = selectHead;
        SelectCharacter = selectCharacter;
        SelectBomb = selectBomb;
        SelectCustomEquipments = selectCustomEquipments;
        Extra = extra;
        var networkManager = BaseNetworkGameManager.Singleton;
        if (networkManager != null)
            networkManager.RegisterCharacter(this);
    }

    public void CmdReady()
    {
        photonView.MasterRPC(RpcServerReady);
    }

    [PunRPC]
    protected void RpcServerReady()
    {
        if (!IsReady)
        {
            ServerSpawn(false);
            IsReady = true;
        }
    }

    public void CmdRespawn(bool isWatchedAds)
    {
        photonView.MasterRPC(RpcServerRespawn, isWatchedAds);
    }

    [PunRPC]
    protected void RpcServerRespawn(bool isWatchedAds)
    {
        ServerRespawn(isWatchedAds);
    }
    
    public void CmdPlantBomb(Vector3 position)
    {
        photonView.MasterRPC(RpcServerPlantBomb, position);
    }

    [PunRPC]
    protected void RpcServerPlantBomb(Vector3 position)
    {
        // Avoid hacks
        if (Vector3.Distance(position, CacheTransform.position) > 3)
            position = CacheTransform.position;
        if (bombs.Count >= 1 + PowerUpBombAmount || !BombEntity.CanPlant(position))
            return;
        if (bombData != null)
            bombs.Add(bombData.Plant(this, position));
    }

    [PunRPC]
    protected void RpcTargetDead()
    {
        DeathTime = Time.unscaledTime;
    }

    [PunRPC]
    protected void RpcTargetSpawn(float x, float y, float z)
    {
        transform.position = new Vector3(x, y, z);
    }

    [PunRPC]
    protected void RpcTargetRewardCurrency(string currencyId, int amount)
    {
        MonetizationManager.Save.AddCurrency(currencyId, amount);
    }

    protected Vector3 RoundXZ(Vector3 vector)
    {
        return new Vector3(
            Mathf.RoundToInt(vector.x),
            vector.y,
            Mathf.RoundToInt(vector.z));
    }

    public void CmdKick(int bombViewId, int dirX, int dirZ)
    {
        photonView.MasterRPC(RpcKick, bombViewId, dirX, dirZ);
    }

    [PunRPC]
    protected void RpcKick(int bombViewId, int dirX, int dirZ)
    {
        var view = PhotonView.Find(bombViewId);
        if (view == null)
            return;
        view.GetComponent<BombEntity>().Kick(photonView.ViewID, (sbyte)dirX, (sbyte)dirZ);
    }
}
