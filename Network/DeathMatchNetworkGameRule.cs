using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeathMatchNetworkGameRule : IONetworkGameRule
{
    public int endMatchCountDown = 10;
    public int EndMatchCountingDown { get; protected set; }

    public override bool HasOptionMatchTime { get { return true; } }
    public override bool HasOptionMatchKill { get { return true; } }

    protected bool endMatchCalled;

    protected override void EndMatch()
    {
        if (!endMatchCalled)
        {
            networkManager.StartCoroutine(EndMatchRoutine());
            endMatchCalled = true;
        }
    }

    IEnumerator EndMatchRoutine()
    {
        EndMatchCountingDown = endMatchCountDown;
        while (EndMatchCountingDown > 0)
        {
            yield return new WaitForSeconds(1);
            --EndMatchCountingDown;
        }
        networkManager.LeaveRoom();
    }

    public override bool RespawnCharacter(BaseNetworkGameCharacter character, params object[] extraParams)
    {
        var targetCharacter = character as CharacterEntity;
        var gameplayManager = GameplayManager.Singleton;
        // In death match mode will not reset score, kill, assist, death
        targetCharacter.watchAdsCount = 0;

        return true;
    }

    public override void InitialClientObjects()
    {
        base.InitialClientObjects();
        var gameplayManager = FindObjectOfType<GameplayManager>();
        if (gameplayManager != null)
        {
            gameplayManager.killScore = 1;
            gameplayManager.suicideScore = 0;
        }
    }
}
