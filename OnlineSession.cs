namespace BattleTactics
{
    internal static class OnlineSession
    {
        internal static bool IsActive
        {
            get
            {
                NetworkData network = RouterHelper.Env?.NetworkData;
                return network != null && network.PlayingOnlineMultiplayer;
            }
        }
    }
}
