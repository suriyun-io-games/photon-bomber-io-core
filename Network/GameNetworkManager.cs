using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameNetworkManager : BaseNetworkGameManager
{
    public static new GameNetworkManager Singleton
    {
        get { return SimplePhotonNetworkManager.Singleton as GameNetworkManager; }
    }
    
    [PunRPC]
    protected override void RpcAddPlayer()
    {
        var position = Vector3.zero;
        var rotation = Quaternion.identity;
        RandomStartPoint(out position, out rotation);

        // Get character prefab
        CharacterEntity characterPrefab = GameInstance.Singleton.characterPrefab;
        var characterGo = PhotonNetwork.Instantiate(characterPrefab.name, position, rotation, 0);
        var character = characterGo.GetComponent<CharacterEntity>();
        character.CmdInit(GameInstance.GetAvailableHead(PlayerSave.GetHead()).GetId(),
            GameInstance.GetAvailableCharacter(PlayerSave.GetCharacter()).GetId(),
            GameInstance.GetAvailableBomb(PlayerSave.GetBomb()).GetId(),
            "");
    }

    protected override void UpdateScores(NetworkGameScore[] scores)
    {
        var uiGameplay = FindObjectOfType<UIGameplay>();
        if (uiGameplay != null)
            uiGameplay.UpdateRankings(scores);
    }

    protected override void KillNotify(string killerName, string victimName, string weaponId)
    {
        var uiGameplay = FindObjectOfType<UIGameplay>();
        if (uiGameplay != null)
            uiGameplay.KillNotify(killerName, victimName, weaponId);
    }
}
