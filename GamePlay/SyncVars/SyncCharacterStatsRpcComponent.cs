using Photon.Pun;

public class SyncCharacterStatsRpcComponent : BaseSyncVarRpcComponent<CharacterStats>
{
    [PunRPC]
    protected virtual void RpcUpdateCharacterStats(CharacterStats value)
    {
        _value = value;
    }
}
