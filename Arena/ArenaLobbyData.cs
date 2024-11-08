﻿using System;
using System.Collections.Generic;

namespace RainMeadow
{
    internal class ArenaLobbyData : OnlineResource.ResourceData
    {
        public ArenaLobbyData() { }

        public override ResourceDataState MakeState(OnlineResource resource)
        {
            return new State(this, resource);
        }

        internal class State : ResourceDataState
        {
            [OnlineField]
            public bool isInGame;
            [OnlineField]
            public bool allPlayersReadyLockLobby;
            [OnlineField]
            public List<string> playList;
            [OnlineField]
            public List<ushort> arenaSittingOnlineOrder;
            [OnlineField]
            public bool returnToLobby;
            [OnlineField]
            public Dictionary<string, int> onlineArenaSettingsInterfaceMultiChoice;
            [OnlineField]
            public Dictionary<string, bool> onlineArenaSettingsInterfaceBool;
            [OnlineField]
            public Dictionary<string, int> playersChoosingSlugs;
            [OnlineField]
            public bool countdownInitiatedHoldFire;
            [OnlineField]
            public bool allPlayersAreNowInGame;
            //public ushort setupTimer;


            public State() { }
            public State(ArenaLobbyData arenaLobbyData, OnlineResource onlineResource)
            {
                ArenaCompetitiveGameMode arena = (onlineResource as Lobby).gameMode as ArenaCompetitiveGameMode;
                isInGame = RWCustom.Custom.rainWorld.processManager.currentMainLoop is RainWorldGame;
                playList = arena.playList;
                arenaSittingOnlineOrder = arena.arenaSittingOnlineOrder;
                allPlayersReadyLockLobby = arena.allPlayersReadyLockLobby;
                returnToLobby = arena.returnToLobby;
                onlineArenaSettingsInterfaceMultiChoice = arena.onlineArenaSettingsInterfaceMultiChoice;
                onlineArenaSettingsInterfaceBool = arena.onlineArenaSettingsInterfaceeBool;
                playersChoosingSlugs = arena.playersInLobbyChoosingSlugs;
                countdownInitiatedHoldFire = arena.countdownInitiatedHoldFire;
                //allPlayersAreNowInGame = arena.allPlayersAreNowInGame;
                //setupTimer = arena.setupTime;

            }

            public override void ReadTo(OnlineResource.ResourceData data, OnlineResource resource)
            {
                var lobby = (resource as Lobby);
                (lobby.gameMode as ArenaCompetitiveGameMode).isInGame = isInGame;
                (lobby.gameMode as ArenaCompetitiveGameMode).playList = playList;
                (lobby.gameMode as ArenaCompetitiveGameMode).arenaSittingOnlineOrder = arenaSittingOnlineOrder;
                (lobby.gameMode as ArenaCompetitiveGameMode).allPlayersReadyLockLobby = allPlayersReadyLockLobby;
                (lobby.gameMode as ArenaCompetitiveGameMode).returnToLobby = returnToLobby;
                (lobby.gameMode as ArenaCompetitiveGameMode).onlineArenaSettingsInterfaceMultiChoice = onlineArenaSettingsInterfaceMultiChoice;
                (lobby.gameMode as ArenaCompetitiveGameMode).onlineArenaSettingsInterfaceeBool = onlineArenaSettingsInterfaceBool;
                (lobby.gameMode as ArenaCompetitiveGameMode).playersInLobbyChoosingSlugs = playersChoosingSlugs;
                (lobby.gameMode as ArenaCompetitiveGameMode).countdownInitiatedHoldFire = countdownInitiatedHoldFire;
                //(lobby.gameMode as ArenaCompetitiveGameMode).allPlayersAreNowInGame = allPlayersAreNowInGame;
                //(lobby.gameMode as ArenaCompetitiveGameMode).setupTime = setupTimer;





            }

            public override Type GetDataType() => typeof(ArenaLobbyData);
        }
    }
}
