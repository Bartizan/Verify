﻿namespace VerifyTests;

public partial class Counter
{
#if NET6_0_OR_GREATER

    ConcurrentDictionary<Time, (int intValue, string stringValue)> timeCache = new();
    static Dictionary<Time, string> namedTimes = new();
    int currentTime;

    public static void AddNamed(Time time, string name) =>
        namedTimes.Add(time, name);

    public int Next(Time input) =>
        NextValue(input).intValue;

    public string NextString(Time input) =>
        NextValue(input).stringValue;

    (int intValue, string stringValue) NextValue(Time input)
    {
        if (namedTimes.TryGetValue(input, out var name))
        {
            return new(0, name);
        }

        return timeCache.GetOrAdd(input, _ =>
        {
            var value = Interlocked.Increment(ref currentTime);
            return (value, $"Time_{value}");
        });
    }

#endif
}