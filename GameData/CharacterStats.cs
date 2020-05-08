using ExitGames.Client.Photon;

[System.Serializable]
public struct CharacterStats
{
    public int bombRange;
    public int bombAmount;
    public int heart;
    public int moveSpeed;
    public bool canKickBomb;

    public static CharacterStats operator +(CharacterStats a, CharacterStats b)
    {
        var result = new CharacterStats();
        result.bombRange = a.bombRange + b.bombRange;
        result.bombAmount = a.bombAmount + b.bombAmount;
        result.heart = a.heart + b.heart;
        result.moveSpeed = a.moveSpeed + b.moveSpeed;
        result.canKickBomb = a.canKickBomb || b.canKickBomb;
        return result;
    }

    public static CharacterStats operator -(CharacterStats a, CharacterStats b)
    {
        var result = new CharacterStats();
        result.bombRange = a.bombRange - b.bombRange;
        result.bombAmount = a.bombAmount - b.bombAmount;
        result.heart = a.heart - b.heart;
        result.moveSpeed = a.moveSpeed - b.moveSpeed;
        result.canKickBomb = a.canKickBomb && b.canKickBomb;
        return result;
    }

    public static byte[] SerializeMethod(object customobject)
    {
        CharacterStats data = (CharacterStats)customobject;
        byte[] writeBytes = new byte[5 * 5];
        int index = 0;
        Protocol.Serialize(data.bombRange, writeBytes, ref index);
        Protocol.Serialize(data.bombAmount, writeBytes, ref index);
        Protocol.Serialize(data.heart, writeBytes, ref index);
        Protocol.Serialize(data.moveSpeed, writeBytes, ref index);
        Protocol.Serialize(data.canKickBomb ? 1 : 0, writeBytes, ref index);
        return writeBytes;
    }

    public static object DeserializeMethod(byte[] readBytes)
    {
        CharacterStats data = new CharacterStats();
        int index = 0;
        int tempInt = 0;
        Protocol.Deserialize(out tempInt, readBytes, ref index);
        data.bombRange = tempInt;
        Protocol.Deserialize(out tempInt, readBytes, ref index);
        data.bombAmount = tempInt;
        Protocol.Deserialize(out tempInt, readBytes, ref index);
        data.heart = tempInt;
        Protocol.Deserialize(out tempInt, readBytes, ref index);
        data.moveSpeed = tempInt;
        Protocol.Deserialize(out tempInt, readBytes, ref index);
        data.canKickBomb = tempInt > 0;
        return data;
    }
}
