using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using Photon.Realtime;

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

    #region Sync Vars
    protected int _watchAdsCount;
    protected bool _isDead;
    protected int _selectCharacter;
    protected int _selectHead;
    protected int _selectBomb;
    protected int[] _selectCustomEquipments;
    protected bool _isInvincible;
    protected CharacterStats _addStats;
    protected string _extra;

    public virtual int watchAdsCount
    {
        get { return _watchAdsCount; }
        set
        {
            if (PhotonNetwork.IsMasterClient && value != watchAdsCount)
            {
                _watchAdsCount = value;
                photonView.RPC("RpcUpdateWatchAdsCount", RpcTarget.Others, value);
            }
        }
    }
    public virtual bool isDead
    {
        get { return _isDead; }
        set
        {
            if (PhotonNetwork.IsMasterClient && value != isDead)
            {
                _isDead = value;
                photonView.RPC("RpcUpdateIsDead", RpcTarget.Others, value);
            }
        }
    }
    public virtual int selectCharacter
    {
        get { return _selectCharacter; }
        set
        {
            if (PhotonNetwork.IsMasterClient && value != selectCharacter)
            {
                _selectCharacter = value;
                photonView.RPC("RpcUpdateSelectCharacter", RpcTarget.All, value);
            }
        }
    }
    public virtual int selectHead
    {
        get { return _selectHead; }
        set
        {
            if (PhotonNetwork.IsMasterClient && value != selectHead)
            {
                _selectHead = value;
                photonView.RPC("RpcUpdateSelectHead", RpcTarget.All, value);
            }
        }
    }
    public virtual int selectBomb
    {
        get { return _selectBomb; }
        set
        {
            if (PhotonNetwork.IsMasterClient && value != selectBomb)
            {
                _selectBomb = value;
                photonView.RPC("RpcUpdateSelectBomb", RpcTarget.All, value);
            }
        }
    }
    public virtual int[] selectCustomEquipments
    {
        get { return _selectCustomEquipments; }
        set
        {
            if (PhotonNetwork.IsMasterClient && value != selectCustomEquipments)
            {
                _selectCustomEquipments = value;
                photonView.RPC("RpcUpdateSelectCustomEquipments", RpcTarget.All, value);
            }
        }
    }
    public virtual bool isInvincible
    {
        get { return _isInvincible; }
        set
        {
            if (PhotonNetwork.IsMasterClient && value != isInvincible)
            {
                _isInvincible = value;
                photonView.RPC("RpcUpdateIsInvincible", RpcTarget.Others, value);
            }
        }
    }
    public virtual CharacterStats addStats
    {
        get { return _addStats; }
        set
        {
            if (PhotonNetwork.IsMasterClient)
            {
                _addStats = value;
                photonView.RPC("RpcUpdateAddStats", RpcTarget.Others, value);
            }
        }
    }
    public virtual string extra
    {
        get { return _extra; }
        set
        {
            if (PhotonNetwork.IsMasterClient && value != extra)
            {
                _extra = value;
                photonView.RPC("RpcUpdateExtra", RpcTarget.Others, value);
            }
        }
    }
    #endregion

    [HideInInspector]
    public int rank = 0;

    public override bool IsDead
    {
        get { return isDead; }
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

    public bool isReady { get; private set; }
    public float deathTime { get; private set; }
    public float invincibleTime { get; private set; }

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
        watchAdsCount = 0;
        isDead = false;
        selectCharacter = 0;
        selectHead = 0;
        selectBomb = 0;
        isInvincible = false;
        extra = "";
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
        deathTime = Time.unscaledTime;
    }

    protected override void OnStartLocalPlayer()
    {
        if (photonView.IsMine)
        {
            var followCam = FindObjectOfType<FollowCamera>();
            followCam.target = CacheTransform;
            targetCamera = followCam.GetComponent<Camera>();
            var uiGameplay = FindObjectOfType<UIGameplay>();
            if (uiGameplay != null)
                uiGameplay.FadeOut();

            foreach (var localPlayerObject in localPlayerObjects)
            {
                localPlayerObject.SetActive(true);
            }
            CmdReady();
        }
    }

    protected override void SyncData()
    {
        base.SyncData();
        if (!PhotonNetwork.IsMasterClient)
            return;
        photonView.RPC("RpcUpdateWatchAdsCount", RpcTarget.Others, watchAdsCount);
        photonView.RPC("RpcUpdateIsDead", RpcTarget.Others, isDead);
        photonView.RPC("RpcUpdateSelectCharacter", RpcTarget.Others, selectCharacter);
        photonView.RPC("RpcUpdateSelectHead", RpcTarget.Others, selectHead);
        photonView.RPC("RpcUpdateSelectBomb", RpcTarget.Others, selectBomb);
        photonView.RPC("RpcUpdateSelectCustomEquipments", RpcTarget.Others, selectCustomEquipments);
        photonView.RPC("RpcUpdateIsInvincible", RpcTarget.Others, isInvincible);
        photonView.RPC("RpcUpdateAddStats", RpcTarget.Others, addStats);
        photonView.RPC("RpcUpdateExtra", RpcTarget.Others, extra);
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;
        base.OnPlayerEnteredRoom(newPlayer);
        photonView.RPC("RpcUpdateWatchAdsCount", newPlayer, watchAdsCount);
        photonView.RPC("RpcUpdateIsDead", newPlayer, isDead);
        photonView.RPC("RpcUpdateSelectCharacter", newPlayer, selectCharacter);
        photonView.RPC("RpcUpdateSelectHead", newPlayer, selectHead);
        photonView.RPC("RpcUpdateSelectBomb", newPlayer, selectBomb);
        photonView.RPC("RpcUpdateSelectCustomEquipments", newPlayer, selectCustomEquipments);
        photonView.RPC("RpcUpdateIsInvincible", newPlayer, isInvincible);
        photonView.RPC("RpcUpdateAddStats", newPlayer, addStats);
        photonView.RPC("RpcUpdateExtra", newPlayer, extra);
    }

    protected override void Update()
    {
        base.Update();
        if (NetworkManager != null && NetworkManager.IsMatchEnded)
            return;
        
        if (IsDead)
        {
            if (!PhotonNetwork.IsMasterClient && photonView.IsMine && Time.unscaledTime - deathTime >= DISCONNECT_WHEN_NOT_RESPAWN_DURATION)
                GameNetworkManager.Singleton.LeaveRoom();
        }

        if (PhotonNetwork.IsMasterClient && isInvincible && Time.unscaledTime - invincibleTime >= GameplayManager.Singleton.invincibleDuration)
            isInvincible = false;
        if (invincibleEffect != null)
            invincibleEffect.SetActive(isInvincible);
        if (nameText != null)
            nameText.text = playerName;
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
                CmdKick(kickingBomb.photonView.ViewID, (sbyte)moveDirNorm.x, (sbyte)moveDirNorm.z);
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
        if (!photonView.IsMine || isDead)
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
        var animator = characterModel.TempAnimator;
        if (animator == null)
            return;
        if (isDead)
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
        if (!photonView.IsMine || isDead)
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
        if (isDead || isInvincible)
            return;

        var gameplayManager = GameplayManager.Singleton;
        if (!gameplayManager.CanReceiveDamage(this, attacker))
            return;

        if (addStats.heart == 0)
        {
            if (attacker != null)
                attacker.KilledTarget(this);
            deathTime = Time.unscaledTime;
            ++dieCount;
            isDead = true;
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
            score += gameplayManager.suicideScore;
        else
        {
            score += gameplayManager.killScore;
            foreach (var rewardCurrency in gameplayManager.rewardCurrencies)
            {
                var currencyId = rewardCurrency.currencyId;
                var amount = Random.Range(rewardCurrency.randomAmountMin, rewardCurrency.randomAmountMax);
                photonView.RPC("RpcTargetRewardCurrency", photonView.Owner, currencyId, amount);
            }
            ++killCount;
        }
        GameNetworkManager.Singleton.SendKillNotify(playerName, target.playerName, bombData == null ? string.Empty : bombData.GetId());
    }

    public virtual void OnSpawn() { }
    
    public void ServerInvincible()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        invincibleTime = Time.unscaledTime;
        isInvincible = true;
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
            photonView.RPC("RpcTargetSpawn", photonView.Owner, position.x, position.y, position.z);
            isDead = false;
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

        isDead = false;
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
        photonView.RPC("RpcServerInit", RpcTarget.MasterClient, selectHead, selectCharacter, selectBomb, selectCustomEquipments, extra);
    }

    [PunRPC]
    protected void RpcServerInit(int selectHead, int selectCharacter, int selectBomb, int[] selectCustomEquipments, string extra)
    {
        isDead = false;
        this.selectHead = selectHead;
        this.selectCharacter = selectCharacter;
        this.selectBomb = selectBomb;
        this.selectCustomEquipments = selectCustomEquipments;
        this.extra = extra;
        var networkManager = BaseNetworkGameManager.Singleton;
        if (networkManager != null)
            networkManager.RegisterCharacter(this);
    }

    public void CmdReady()
    {
        photonView.RPC("RpcServerReady", RpcTarget.MasterClient);
    }

    [PunRPC]
    protected void RpcServerReady()
    {
        if (!isReady)
        {
            ServerSpawn(false);
            isReady = true;
        }
    }

    public void CmdRespawn(bool isWatchedAds)
    {
        photonView.RPC("RpcServerRespawn", RpcTarget.MasterClient, isWatchedAds);
    }

    [PunRPC]
    protected void RpcServerRespawn(bool isWatchedAds)
    {
        ServerRespawn(isWatchedAds);
    }
    
    public void CmdPlantBomb(Vector3 position)
    {
        photonView.RPC("RpcServerPlantBomb", RpcTarget.MasterClient, position);
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

    #region Update RPCs
    [PunRPC]
    protected virtual void RpcUpdateWatchAdsCount(int watchAdsCount)
    {
        _watchAdsCount = watchAdsCount;
    }
    [PunRPC]
    protected virtual void RpcUpdateIsDead(bool isDead)
    {
        if (!_isDead && isDead)
            deathTime = Time.unscaledTime;
        _isDead = isDead;
    }
    [PunRPC]
    protected virtual void RpcUpdateSelectCharacter(int selectCharacter)
    {
        _selectCharacter = selectCharacter;
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
    [PunRPC]
    protected virtual void RpcUpdateSelectHead(int selectHead)
    {
        _selectHead = selectHead;
        headData = GameInstance.GetHead(selectHead);
        if (characterModel != null && headData != null)
            characterModel.SetHeadModel(headData.modelObject);
    }
    [PunRPC]
    protected virtual void RpcUpdateSelectBomb(int selectBomb)
    {
        _selectBomb = selectBomb;
        bombData = GameInstance.GetBomb(selectBomb);
    }
    [PunRPC]
    protected virtual void RpcUpdateSelectCustomEquipments(int[] selectCustomEquipments)
    {
        _selectCustomEquipments = selectCustomEquipments;
        if (characterModel != null)
            characterModel.ClearCustomModels();
        customEquipmentDict.Clear();
        if (_selectCustomEquipments != null)
        {
            for (var i = 0; i < _selectCustomEquipments.Length; ++i)
            {
                var customEquipmentData = GameInstance.GetCustomEquipment(_selectCustomEquipments[i]);
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
    [PunRPC]
    protected virtual void RpcUpdateIsInvincible(bool isInvincible)
    {
        _isInvincible = isInvincible;
    }
    [PunRPC]
    protected virtual void RpcUpdateAddStats(CharacterStats addStats)
    {
        _addStats = addStats;
    }
    [PunRPC]
    protected virtual void RpcUpdateExtra(string extra)
    {
        _extra = extra;
    }
    public void CmdKick(int bombViewId, sbyte dirX, sbyte dirZ)
    {
        photonView.RPC("RpcKick", RpcTarget.MasterClient, bombViewId, dirX, dirZ);
    }
    [PunRPC]
    protected void RpcKick(int bombViewId, sbyte dirX, sbyte dirZ)
    {
        var view = PhotonView.Find(bombViewId);
        if (view == null)
            return;
        view.GetComponent<BombEntity>().Kick(photonView.ViewID, dirX, dirZ);
    }
    #endregion
}
