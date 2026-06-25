using System;
using System.Collections.Generic;

namespace PortfolioSamples.LiveOps
{
    public enum RewardType
    {
        Diamond = 0,
        Gold = 1,
        Equipment = 2
    }

    public readonly struct DailyRewardEntry
    {
        public DailyRewardEntry(int index, RewardType type, int value)
        {
            Index = index;
            Type = type;
            Value = value;
        }

        public int Index { get; }
        public RewardType Type { get; }
        public int Value { get; }
    }

    public sealed class RewardAdDailyGrantSample
    {
        private readonly List<DailyRewardEntry> _serverConfiguredRewards = new();

        public IReadOnlyList<DailyRewardEntry> Rewards => _serverConfiguredRewards;
        public int LastClaimDateYmd { get; private set; }

        public void ApplyServerConfig(IEnumerable<DailyRewardEntry> rewards)
        {
            _serverConfiguredRewards.Clear();
            _serverConfiguredRewards.AddRange(rewards);
            _serverConfiguredRewards.Sort((a, b) => a.Index.CompareTo(b.Index));
        }

        public bool CanClaim(int todayYmd)
        {
            return LastClaimDateYmd != todayYmd;
        }

        public bool TryClaim(int todayYmd, Action<DailyRewardEntry> grantReward)
        {
            if (!CanClaim(todayYmd))
                return false;

            foreach (DailyRewardEntry reward in _serverConfiguredRewards)
                grantReward?.Invoke(reward);

            LastClaimDateYmd = todayYmd;
            return true;
        }
    }
}
