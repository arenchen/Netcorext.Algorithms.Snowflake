namespace Netcorext.Algorithms;

public class Snowflake : ISnowflake
{
    /*
     * 1970-01-01T00:00:00+00 ~ 2179-01-19T23:22:46
     * 0 ~ 6597069766655
     * Ticks = 65970409660000000
     * Days = 76354.640810185185
     * Hours = 1832511.3794444446
     * Minutes = 109950682.76666667
     * Seconds = 6597040966
     * Milliseconds = 6597040966000
     */

    private const long DEFAULT_EPOCH = 1605093071000; //2020-11-11T11:11:11+00;
    private const int MACHINE_ID_BITS = 5;            // 機器碼字元數
    private const int DATACENTER_ID_BITS = 5;         // 資料中心字元數
    private const int MAX_MACHINE_ID = -1 ^ (-1 << MACHINE_ID_BITS);
    private const int MAX_DATACENTER_ID = -1 ^ (-1 << DATACENTER_ID_BITS);
    private const int SEQUENCE_BITS = 12;               // 計數器字元數，12個字元用來保存計數碼
    private const int MACHINE_ID_SHIFT = SEQUENCE_BITS; // 機器碼數據左移位數，就是後面計數器佔用的位數
    private const int DATACENTER_ID_SHIFT = SEQUENCE_BITS + MACHINE_ID_BITS;
    private const int TIMESTAMP_LEFT_SHIFT = SEQUENCE_BITS + MACHINE_ID_BITS + DATACENTER_ID_BITS; // 時間戳左移動位數就是機器碼+計數器總字元數+資料中心字元數
    private const int SEQUENCE_MASK = -1 ^ (-1 << SEQUENCE_BITS);                                  // 微秒內可以產生計數，如果達到該值則等到下一微妙在進行生成

    private static readonly object SyncRoot = new object();
    private static ISnowflake _snowflake;
    private static long _lastTimestamp = -1;
    private static uint _sequence;

    public static ISnowflake Instance => _snowflake ??= new Snowflake(0, 0, DEFAULT_EPOCH);

    public Snowflake() : this(0, 0) { }

    public Snowflake(uint machineId, uint datacenterId, long epoch = DEFAULT_EPOCH)
    {
        if (machineId > MAX_MACHINE_ID || machineId < 0)
            throw new ArgumentOutOfRangeException("machineId Maximum value should be between 0 and " + MAX_MACHINE_ID);

        if (datacenterId > MAX_DATACENTER_ID || datacenterId < 0)
            throw new ArgumentOutOfRangeException("datacenterId Maximum value should be between 0 and " + MAX_DATACENTER_ID);

        MachineId = machineId;

        DatacenterId = datacenterId;

        Epoch = epoch;

        _snowflake = this;
    }

    public uint MachineId { get; }

    public uint DatacenterId { get; }

    public long Epoch { get; }

    private static long GetTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private static long GetNextTimestamp(long lastTimestamp)
    {
        var timestamp = GetTimestamp();

        while (timestamp <= lastTimestamp) timestamp = GetTimestamp();

        return timestamp;
    }

    public long Generate()
    {
        lock (SyncRoot)
        {
            var timestamp = GetTimestamp();

            // 時間戳比上一次生成ID時時間戳還小，故異常
            if (timestamp < _lastTimestamp)
                throw new ArgumentOutOfRangeException(nameof(timestamp));

            if (timestamp == _lastTimestamp)
            {
                // 用 & 運算計算該微秒內產生的計數是否已經到達上限
                _sequence = (_sequence + 1) & SEQUENCE_MASK;

                // 一微妙內產生的ID計數已達上限，等待下一微妙
                if (_sequence == 0) timestamp = GetNextTimestamp(_lastTimestamp);
            }

            _lastTimestamp = timestamp;

            return ((timestamp - Epoch) << TIMESTAMP_LEFT_SHIFT) | (DatacenterId << DATACENTER_ID_SHIFT) | (MachineId << MACHINE_ID_SHIFT) | _sequence;
        }
    }
}