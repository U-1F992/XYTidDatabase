using System.Runtime.Serialization.Formatters.Binary;
using PokemonPRNG;

#pragma warning disable SYSLIB0011

var app = ConsoleApp.Create(args);
app.AddCommand("create", Create);
app.AddCommand("search", Search);
app.Run();

/// <summary>
/// tid-sidペアが出現する初期seedのデータベースを作成する
/// Dictionary<{初期seed}, {消費数}>
/// 
/// <sample>
/// XYTidDatabase.exe create --tid 0 --sid 28552 --timeout 3030 --start $(0xB2B6FFFBU) --end $(0xD000000U)
/// </sample>
/// 
/// </summary>
/// <param name="tid">TID</param>
/// <param name="sid">SID</param>
/// <param name="timeout">データベースに登録する上限の消費数</param>
/// <param name="start">検索範囲</param>
/// <param name="end">検索範囲</param>
void Create
(
    [Option("", "TID")] UInt32 tid,
    [Option("", "SID")] UInt32 sid,
    [Option("", "Timeout")] UInt32 timeout,
    [Option("", "Search from this seed")] UInt32 start,
    [Option("", "Search to this seed")] UInt32 end
)
{
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs e) => { cts.Cancel(); };

    // key: initialSeed
    // value: count of advance
    var db = new Dictionary<UInt32, UInt32>();
    
    // db登録完了数
    UInt32 done = 0;
    Task.Run(() => WriteProgress(cts.Token));

    var po = new ParallelOptions() { CancellationToken = cts.Token };
    try
    {
        Parallel.For(start, end + 1, po, initialSeed =>
        {
            var ct = po.CancellationToken;
            ct.ThrowIfCancellationRequested();

            var rng = new TinyMT((UInt32)initialSeed);
            // 何らかに14消費
            // 6genTidSearchに合わせているだけで、詳細は知らない
            for (int i = 0; i < 14; i++) rng.Advance();

            // timeoutまでIDを生成して、表裏一致が出なければnull
            UInt32 count = 0;
            while (rng.GetId() != (tid, sid) && count != timeout && !ct.IsCancellationRequested) count++;
            if (ct.IsCancellationRequested) return;
            else if (count == timeout)
            {
                done++;
                return;
            }
            lock (db) db.Add((UInt32)initialSeed, count);

            done++;
        });
    } catch (OperationCanceledException) {}

    cts.Cancel();

    var filename = String.Format("{0}-{1}.dat", tid, sid);
    using (var fs = new FileStream(filename, FileMode.Create))
    {
        var bf = new BinaryFormatter();
        bf.Serialize(fs, db);
        Console.WriteLine("{0} has been created.", filename);
    }

    void WriteProgress(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested) Console.Write("\r{0}/{1} : {2:0.00}%", done, (end - start + 1), Math.Floor((double)(done / (double)(end - start + 1) * 10000)) / 100);
        Console.Write("\n");
    }
}

/// <summary>
/// データベースの範囲で、基準seedから近い順に抽出する
/// 
/// <sample>
/// XYTidDatabase.exe search --path 0-28552.dat --base-seed $(0xB281A4EEU) --min-advance 30 --max-advance 2000 --count 3
/// </sample>
/// 
/// </summary>
/// <param name="path">データベース</param>
/// <param name="baseSeed">基準seed</param>
/// <param name="minAdvance">最小消費数 (博士の瞬き等で30程度は消費される)</param>
/// <param name="maxAdvance">最大消費数 (データベースのtimeout以下)</param>
/// <param name="count">表示する結果の数</param>
void Search
(
    [Option("", "*.dat")] string path,
    [Option("", "Base seed")] UInt32 baseSeed,
    [Option("", "Minimum advance")] UInt32 minAdvance,
    [Option("", "Maximum advance")] UInt32 maxAdvance,
    [Option("", "Number of results")] UInt32 count
)
{
    Dictionary<UInt32, UInt32> db;

    using (var fs = new FileStream(path, FileMode.Open))
    {
        BinaryFormatter bf = new BinaryFormatter();
        db = (Dictionary<UInt32, UInt32>)bf.Deserialize(fs);
        Console.WriteLine("{0} has been loaded.", path);
    }

    Console.WriteLine("");
    Console.WriteLine("WaitMilliseconds Seed     State                                ");
    Console.WriteLine("---------------- -------- -------------------------------------");

    var c = 0;
    for (long seed = baseSeed; seed < (baseSeed + 0x100000000); seed++)
    {
        UInt32 tmp_seed = (UInt32)(seed & 0xFFFFFFFF);
        if (!db.Keys.Contains(tmp_seed)) continue;
        if (db[tmp_seed] < minAdvance || maxAdvance < db[tmp_seed]) continue;
        
        var vec = tmp_seed.GetStateVector();
        Console.WriteLine("{0,16} {1:X8} [{2:X8},{3:X8},{4:X8},{5:X8}]", Math.Abs(seed - baseSeed), seed, vec[3], vec[2], vec[1], vec[0]);
        c++;

        if (c == count) break;
    }

    Console.WriteLine("");
}

public static class TinyMTExtension
{
    public static (UInt32 tid, UInt32 sid) GetId(this TinyMT rng)
    {
        UInt32 rand = rng.GetRand();
        return (rand & 0x0000FFFF, (rand & 0xFFFF0000) >> 16);
    }

    public static uint[] GetStateVector(this uint seed)
    {
        // https://github.com/yatsuna827/PokemonPRNG/blob/master/PokemonPRNG/TinyMT.cs
        var stateVector = new uint[4]
        {
            seed,
            0x8f7011ee,
            0xfc78ff1f,
            0x3793fdff
        };
        for (uint j = 1; j < 8; j++) stateVector[j & 3] ^= j + 0x6C078965u * (stateVector[(j - 1) & 3] ^ (stateVector[(j - 1) & 3] >> 30));
        
        return stateVector;
    }
}