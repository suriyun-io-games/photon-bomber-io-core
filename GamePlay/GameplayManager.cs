using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class GameplayManager : MonoBehaviourPunCallbacks
{
    [System.Serializable]
    public struct RewardCurrency
    {
        public string currencyId;
        public int randomAmountMin;
        public int randomAmountMax;
    }
    public const float REAL_MOVE_SPEED_RATE = 0.1f;
    public static GameplayManager Singleton { get; private set; }
    [Header("Character stats")]
    public int minMoveSpeed = 30;
    public int addMoveSpeedPerPowerUp = 5;
    [Header("Powerups")]
    public int maxBombRangePowerUp = 9;
    public int maxBombAmountPowerUp = 9;
    public int maxHeartPowerUp = 9;
    public int maxMoveSpeedPowerUp = 9;
    [Header("Game rules")]
    public RewardCurrency[] rewardCurrencies;
    public int killScore = 15;
    public int suicideScore = -20;
    public int watchAdsRespawnAvailable = 2;
    public float updateScoreDuration = 1f;
    public float brickRespawnDuration = 30f;
    public float respawnDuration = 5f;
    public float invincibleDuration = 1.5f;
    public Transform[] characterSpawnPositions;
    public Transform[] characterSpawnPositionsForTeamA;
    public Transform[] characterSpawnPositionsForTeamB;
    public PowerUpSpawnData[] powerUps;
    public int noDropPowerUpWeight = 1;
    public readonly Dictionary<PowerUpEntity, int> powerUpDropWeights = new Dictionary<PowerUpEntity, int>();

    private void Awake()
    {
        if (Singleton != null)
        {
            Destroy(gameObject);
            return;
        }
        Singleton = this;

        powerUpDropWeights.Clear();
        foreach (var powerUp in powerUps)
        {
            var powerUpPrefab = powerUp.powerUpPrefab;
            if (powerUpPrefab != null && !powerUpDropWeights.ContainsKey(powerUpPrefab))
                powerUpDropWeights.Add(powerUpPrefab, powerUp.randomWeight);
        }
    }

    public void SpawnPowerUp(Vector3 position)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;
        
        var randomizer = WeightedRandomizer.From(powerUpDropWeights);
        randomizer.noResultWeight = noDropPowerUpWeight;
        var powerUpPrefab = randomizer.TakeOne();
        SpawnPowerUp(powerUpPrefab, position);
    }

    public void SpawnPowerUp(PowerUpEntity powerUpPrefab, Vector3 position)
    {
        if (powerUpPrefab != null)
            PhotonNetwork.InstantiateRoomObject(powerUpPrefab.name, position, Quaternion.identity, 0, new object[0]);
    }

    public Vector3 GetCharacterSpawnPosition(CharacterEntity character)
    {
        // TODO: Improve team codes

        if (character.PlayerTeam == 1 &&
            characterSpawnPositionsForTeamA != null &&
            characterSpawnPositionsForTeamA.Length > 0)
            return characterSpawnPositionsForTeamA[Random.Range(0, characterSpawnPositionsForTeamA.Length)].position;

        if (character.PlayerTeam == 2 &&
            characterSpawnPositionsForTeamB != null &&
            characterSpawnPositionsForTeamB.Length > 0)
            return characterSpawnPositionsForTeamB[Random.Range(0, characterSpawnPositionsForTeamB.Length)].position;

        if (characterSpawnPositions == null || characterSpawnPositions.Length == 0)
            return Vector3.zero;
        return characterSpawnPositions[Random.Range(0, characterSpawnPositions.Length)].position;
    }

    public virtual bool CanRespawn(CharacterEntity character)
    {
        var networkGameplayManager = BaseNetworkGameManager.Singleton;
        if (networkGameplayManager != null && networkGameplayManager.IsMatchEnded)
            return false;
        return true;
    }

    public virtual bool CanReceiveDamage(CharacterEntity damageReceiver, CharacterEntity attacker)
    {
        if (damageReceiver == attacker)
            return true;
        var networkGameplayManager = BaseNetworkGameManager.Singleton;
        if (networkGameplayManager != null)
        {
            if (networkGameplayManager.gameRule != null && networkGameplayManager.gameRule.IsTeamGameplay && attacker)
                return damageReceiver.PlayerTeam != attacker.PlayerTeam;
            if (networkGameplayManager.IsMatchEnded)
                return false;
        }
        return true;
    }

    public virtual bool CanAttack(CharacterEntity character)
    {
        var networkGameplayManager = BaseNetworkGameManager.Singleton;
        if (networkGameplayManager != null)
        {
            if (networkGameplayManager.IsMatchEnded)
                return false;
        }
        return true;
    }
}
