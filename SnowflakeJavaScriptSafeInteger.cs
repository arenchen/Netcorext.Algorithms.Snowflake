namespace Netcorext.Algorithms;

public class SnowflakeJavaScriptSafeInteger : ISnowflake
{
    private const long DEFAULT_EPOCH = 1605093071; // 2020-11-11T11:11:11+00

    private const int TICKS_BITS = 31;
    private const int MACHINE_ID_BITS = 5;
    private const int SEQUENCE_BITS = 17;
    private const int MAX_MACHINE_ID = (1 << MACHINE_ID_BITS) - 1;
    private const int MAX_SEQUENCE = (1 << SEQUENCE_BITS) - 1;
    private const long MAX_SAFE_INTEGER = 9007199254740991;
    private const uint DEFAULT_OVERTIME = 7;

    private static readonly object SyncRoot = new object();

    private static SnowflakeJavaScriptSafeInteger _snowflake;
    private static long _lastTimestamp = -1;
    private static uint _sequence;
    private static long _overShift;
    private static long _overtime;

    private readonly uint _machineId;
    private readonly long _epoch;

    public static SnowflakeJavaScriptSafeInteger Instance => _snowflake ??= new SnowflakeJavaScriptSafeInteger();

    public SnowflakeJavaScriptSafeInteger() : this(1) { }

    public SnowflakeJavaScriptSafeInteger(uint machineId, long epoch = DEFAULT_EPOCH, uint overtime = DEFAULT_OVERTIME)
    {
        if (machineId > MAX_MACHINE_ID || machineId < 0)
            throw new ArgumentOutOfRangeException("machineId Maximum value should be between 0 and " + MAX_MACHINE_ID);

        _machineId = machineId;

        _epoch = epoch;

        _overtime = overtime;

        _snowflake = this;
    }

    private static long GetTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private static long GetTimestampShift(long shift = 0)
    {
        if (_overShift < 0) _overShift = 0;

        _overShift += shift;

        if (_overShift > _overtime) _overShift = _overtime;

        return GetTimestamp() + _overShift;
    }

    private static long GetNextTimestamp(long lastTimestamp)
    {
        var timestamp = GetTimestamp();

        while (timestamp <= lastTimestamp)
        {
            var diffTimestamp = DEFAULT_OVERTIME - (lastTimestamp - timestamp);

            if (diffTimestamp > 0) _overShift -= diffTimestamp;

            timestamp = GetTimestampShift(1);
        }

        return timestamp;
    }

    public long Generate()
    {
        lock (SyncRoot)
        {
            var timestamp = GetTimestampShift();

            if (timestamp < _lastTimestamp)
                throw new ArgumentOutOfRangeException(nameof(timestamp));

            if (timestamp == _lastTimestamp)
            {
                _sequence = (_sequence + 1) & MAX_SEQUENCE;

                if (_sequence == 0) timestamp = _overShift < DEFAULT_OVERTIME ? GetTimestampShift(1) : GetNextTimestamp(_lastTimestamp);
            }

            _lastTimestamp = timestamp;

            var ticks = timestamp - _epoch;

            var id = (ticks << (SEQUENCE_BITS + MACHINE_ID_BITS)) |
                     (_machineId << SEQUENCE_BITS) |
                     _sequence;

            if (id > MAX_SAFE_INTEGER) throw new ArgumentOutOfRangeException(nameof(id));

            return id;
        }
    }
}