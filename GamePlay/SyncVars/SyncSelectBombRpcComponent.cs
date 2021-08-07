using Photon.Pun;

public class SyncSelectBombRpcComponent : BaseSyncVarRpcComponent<int>
{
    private CharacterEntity entity;
    protected override void Awake()
    {
        base.Awake();
        entity = GetComponent<CharacterEntity>();
        onValueChange.AddListener(OnValueChange);
    }

    void OnValueChange(int value)
    {
        entity.OnUpdateSelectBomb(value);
    }

    [PunRPC]
    protected virtual void RpcUpdateSelectBomb(int value)
    {
        _value = value;
        entity.OnUpdateSelectBomb(value);
    }
}
