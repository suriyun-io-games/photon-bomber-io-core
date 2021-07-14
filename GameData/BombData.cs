using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class BombData : ItemData
{
    public BombEntity bombPrefab;

    public BombEntity Plant(CharacterEntity planter, Vector3 position)
    {
        if (planter == null)
            return null;

        var bombEntityGo = PhotonNetwork.InstantiateRoomObject(bombPrefab.name, position, Quaternion.identity, 0, new object[0]);
        var bombEntity = bombEntityGo.GetComponent<BombEntity>();
        bombEntity.transform.position = new Vector3(
            Mathf.RoundToInt(position.x),
            position.y, 
            Mathf.RoundToInt(position.z));
        bombEntity.addBombRange = planter.PowerUpBombRange;
        bombEntity.planterViewId = planter.photonView.ViewID;
        return bombEntity;
    }
}
