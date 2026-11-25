using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;

namespace BattleTactics
{
    internal static class OnlineLink
    {
        private const string MessageKey = "ftk2f";
        private const char FieldSeparator = '|';
        private const string LayoutKind = "1";
        private const string RequestKind = "1?";
        private const int FieldCount = 3;

        private static readonly ConditionalWeakTable<IConnectionAdapter, object> subscribedConnections =
            new ConditionalWeakTable<IConnectionAdapter, object>();

        private static string sessionKey;
        private static bool needsSync;

        internal static LayoutTimeline Timeline { get; } = new LayoutTimeline();

        internal static void Install(Harmony harmony)
        {
            MethodInfo register = AccessTools.Method(typeof(NetworkHelper), nameof(NetworkHelper.RegisterOnMessageReceived));

            if (register == null)
            {
                Plugin.Log.LogError("Network registration method not found, the game may have been updated. Online layouts are inactive.");
                return;
            }

            var postfix = new HarmonyMethod(typeof(OnlineLink), nameof(Subscribe)) { priority = Priority.Last };
            harmony.Patch(register, postfix: postfix);
            Plugin.Note("Online layout patch installed");
        }

        internal static void EnsureAttached()
        {
            IConnectionAdapter connection = RouterHelper.Env?.NetworkData?.Connection;

            if (connection != null)
                Subscribe(connection);
        }

        internal static int CurrentActionIndex()
        {
            return NetworkHelper.GetLatestGameActionIndex(RouterHelper.Env.NetworkData.ActionHistory);
        }

        internal static bool Send(Dictionary<string, int> layout)
        {
            return SendMessage(LayoutKind, LayoutCodec.Format(layout));
        }

        private static bool SendMessage(string kind, string body)
        {
            try
            {
                EnsureAttached();
                NetworkData network = RouterHelper.Env?.NetworkData;

                if (network?.Connection == null || !network.PlayingOnlineMultiplayer)
                    return false;

                string payload = string.Join(FieldSeparator.ToString(), kind, network.CurrentReloadId, body);

                return network.Connection.SendMessage(new Dictionary<string, object> { { MessageKey, payload } }, false);
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError(exception);
                return false;
            }
        }

        // parameter name must match the game's
        private static void Subscribe(IConnectionAdapter pConnection)
        {
            try
            {
                if (pConnection == null || subscribedConnections.TryGetValue(pConnection, out _))
                    return;

                subscribedConnections.Add(pConnection, new object());
                needsSync = true;
                pConnection.OnMessageReceived += OnMessage;
                pConnection.OnRemotePlayerConnectToRoom += OnPlayerJoined;
                Plugin.Note("Online layout channel attached");
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError(exception);
            }
        }

        private static void OnMessage(object sender, NetworkMessageEventArgs args)
        {
            try
            {
                Dictionary<string, object> message = args?.Message;

                if (message == null)
                    return;

                ResetOnNewSession();
                SyncIfNeeded();

                if (message.ContainsKey(NetworkHelper.ORDERED_GAME_ACTION_KEY))
                    NoteOrderedAction(message);
                else if (message.TryGetValue(MessageKey, out object payload))
                    Accept(payload as string);
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError(exception);
            }
        }

        // the game stamps each ordered message with its index before raising it, in the same order on every client
        private static void NoteOrderedAction(Dictionary<string, object> message)
        {
            if (message.TryGetValue(NetworkHelper.ORDERED_GAME_ACTION_INDEX_KEY, out object index) && index is int messageIndex)
                Timeline.NoteOrdered(messageIndex);
        }

        private static void Accept(string payload)
        {
            string[] fields = payload?.Split(new[] { FieldSeparator }, FieldCount);

            if (fields == null || fields.Length != FieldCount || (fields[0] != LayoutKind && fields[0] != RequestKind))
            {
                Plugin.Note("Layout update ignored: unknown format");
                return;
            }

            NetworkData network = RouterHelper.Env?.NetworkData;

            if (network == null || fields[1] != network.CurrentReloadId.ToString())
            {
                Plugin.Note("Layout update ignored: from another game session ("
                    + fields[1] + " here " + network?.CurrentReloadId + ")");
                return;
            }

            if (fields[0] == RequestKind)
            {
                AnswerRequest(network);
                return;
            }

            Dictionary<string, int> received = LayoutCodec.Parse(fields[2]);
            Plugin.Note("Layout update received: " + received.Count + " entries");
            Timeline.Add(received, network.JoinInProgressGameActionIndexOffset);

            LayoutSource.RaiseChanged();

            if (network.IsHost)
                RememberHosted(received);
        }

        // a reconnect keeps the same room, and can briefly report none, so only a new room or a reloaded game starts over
        private static void ResetOnNewSession()
        {
            string current = SessionKey();

            if (string.IsNullOrEmpty(current) || current == sessionKey)
                return;

            sessionKey = current;
            needsSync = true;
            Timeline.Clear();
            LayoutSource.RaiseChanged();
        }

        // a disk error must never get in the way of the live layout
        private static void RememberHosted(Dictionary<string, int> layout)
        {
            try
            {
                FormationStore.SaveHosted(layout);
            }
            catch (Exception exception)
            {
                Plugin.Log.LogWarning("Could not remember the hosted layout: " + exception.Message);
            }
        }

        // a fresh session starts from the host's remembered layout, and everyone else asks the host for the current one
        private static void SyncIfNeeded()
        {
            if (!needsSync)
                return;

            NetworkData network = RouterHelper.Env?.NetworkData;

            if (network == null || !network.PlayingOnlineMultiplayer)
                return;

            needsSync = false;

            if (!network.IsHost)
            {
                Plugin.Note("Asking the host for the current layout");
                SendMessage(RequestKind, string.Empty);
                return;
            }

            Dictionary<string, int> hosted = FormationStore.LoadHosted();

            if (hosted.Count > 0 && Timeline.IsEmpty)
            {
                Plugin.Note("Offering the remembered hosted layout to the room");
                Send(hosted);
            }
        }

        private static void AnswerRequest(NetworkData network)
        {
            if (network.IsHost && !Timeline.IsEmpty)
            {
                Plugin.Note("Answering a layout request");
                Send(Timeline.Latest());
            }
        }

        // a failed lookup must never stop the ordered action count from being kept
        private static string SessionKey()
        {
            try
            {
                NetworkData network = RouterHelper.Env?.NetworkData;
                string room = network?.Connection?.QueryRoomId();
                return string.IsNullOrEmpty(room) ? null : network.CurrentReloadId + "/" + room;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // a player who joins mid-session missed earlier updates, so the host repeats the current layout
        private static Task OnPlayerJoined(object sender, string username, string connectionId)
        {
            try
            {
                NetworkData network = RouterHelper.Env?.NetworkData;

                if (network != null && network.IsHost && !Timeline.IsEmpty)
                    Send(Timeline.Latest());
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError(exception);
            }

            return Task.CompletedTask;
        }
    }
}
