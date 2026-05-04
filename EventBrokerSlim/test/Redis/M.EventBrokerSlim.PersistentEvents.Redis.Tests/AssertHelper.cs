using System.Text.RegularExpressions;
using M.EventBrokerSlim.Persistent;
using StackExchange.Redis;

namespace M.EventBrokerSlim.PersistentEvents.Redis.Tests;

public static class AssertHelper
{
    private static readonly Regex _keyPrefixPattern = new Regex(@"^\{(?<prefix>.*)\}");
    private static readonly EventStatus[] _eventStatuses = Enum.GetValues<EventStatus>().Where(x => x != EventStatus.Unknown).ToArray();

    extension(Assert)
    {
        public static async Task StatusIsAsync(EventStatus status, string recordId, IDatabase db)
        {
            var keyPrefix = ParseKeyPrefix(recordId);
            var record = await db.HashGetAsync(recordId, ["status", "last_updated_at"]).ConfigureAwait(false);
            var recordStatus = (EventStatus)(int)record[0];
            var lastUpdatedAt = (double)record[1];
            Assert.Equal(status, recordStatus);

            foreach(var eventStatus in _eventStatuses)
            {
                string indexKey = GetIndexKey(eventStatus, keyPrefix);
                var setScore = await db.SortedSetScoreAsync(indexKey, recordId).ConfigureAwait(false);
                if(eventStatus == status)
                {
                    Assert.True(setScore.HasValue, $"Expected recordId {recordId} to be in index {indexKey} for status {status} but it was not");
                    Assert.Equal(lastUpdatedAt, setScore.Value);
                }
                else
                {
                    Assert.False(setScore.HasValue, $"Expected recordId {recordId} to not be in index {indexKey} for status {eventStatus} but it was");
                }
            }
        }

        public static async Task KeyDoesNotExistAsync(string key, IDatabase db)
        {
            var keyPrefix = ParseKeyPrefix(key);
            var exists = await db.KeyExistsAsync(key).ConfigureAwait(false);
            Assert.False(exists, $"Expected key {key} to not exist but it does");
            foreach(var eventStatus in _eventStatuses)
            {
                string indexKey = GetIndexKey(eventStatus, keyPrefix);
                var setScore = await db.SortedSetScoreAsync(indexKey, key).ConfigureAwait(false);
                Assert.False(setScore.HasValue, $"Expected recordId {key} to not be in index {indexKey} for status {eventStatus} but it was");
            }
        }
    }

    private static string ParseKeyPrefix(string recordId)
    {
        var match = _keyPrefixPattern.Match(recordId);
        if(!match.Success)
        {
            Assert.Fail($"recordId {recordId} does not contain a key prefix enclosed in '{{}}'");
            return null!;
        }

        var keyPrefix = match.Groups["prefix"].Value;
        if(keyPrefix is null)
        {
            Assert.Fail($"recordId {recordId} key prefix is null");
            return null!;
        }

        return keyPrefix;
    }

    private static string GetIndexKey(EventStatus status, string keyPrefix) => status switch
    {
        EventStatus.Scheduled => $"{{{keyPrefix}}}:idx:scheduled",
        EventStatus.DeadLettered => $"{{{keyPrefix}}}:idx:dead_lettered",
        EventStatus.Completed => $"{{{keyPrefix}}}:idx:completed",
        EventStatus.InProgress => $"{{{keyPrefix}}}:idx:in_progress",
        _ => throw new ArgumentException($"Unsupported status {status}")
    };
}
