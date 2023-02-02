﻿using Steamworks;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RainMeadow
{
    // Static/singleton class for online features and callbacks
    // is a mainloopprocess so update bound to game update? worth it? idk
    public class OnlineManager : MainLoopProcess {

        public static string CLIENT_KEY = "client";
        public static string CLIENT_VAL = "Meadow_" + RainMeadow.MeadowVersionStr;
        public static string NAME_KEY = "name";

        public static CSteamID me;
        public static OnlinePlayer mePlayer;
        public static Lobby lobby;

        public static LobbyManager lobbyManager;

        public OnlineManager(ProcessManager manager) : base(manager, RainMeadow.Ext_ProcessID.OnlineManager)
        {
            me = SteamUser.GetSteamID();
            mePlayer = new OnlinePlayer(me);
            lobbyManager = new LobbyManager();

            RainMeadow.Debug("OnlineManager Created");
        }

        public override void Update()
        {
            base.Update();

            lobby?.Update();
        }
    }
}