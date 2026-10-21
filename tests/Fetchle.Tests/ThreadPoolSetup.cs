namespace Fetchle.Tests;

// the suite runs tests in parallel and the ranked search fans out on the thread pool, so the pool starts short of
// threads and injects more slowly than the picker tests' 10 s settle wait allows. start it wide instead
public static class ThreadPoolSetup
{
    const int MIN_THREADS = 64;

    [Before(HookType.Assembly)]
    public static void WidenThePool()
    {
        ThreadPool.GetMinThreads(out var workers, out var io);
        ThreadPool.SetMinThreads(Math.Max(workers, MIN_THREADS), Math.Max(io, MIN_THREADS));
    }
}
