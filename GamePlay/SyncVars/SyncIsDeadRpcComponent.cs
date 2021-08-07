using Photon.Pun;

public class SyncIsDeadRpcComponent : BaseSyncVarRpcComponent<bool>
{
    [PunRPC]
    protected virtual void RpcUpdateIsDead(bool value)
    {
        _value = value;
    }
}
