using Fetchle.Cli.Commands;
using Fetchle.Core.Index;
using Fetchle.Core.Search;
using Fetchle.Core.Walking;

var env = CliEnvironment.FromProcess();

// reads the snapshot for every root it covers and walks the rest live
IFileSearch search = new IndexedFileSearch(PruneRules.Default, new IndexStore(env.Dirs.Index));

var app = new FetchleApp(search, env);
return (int)await app.RunAsync(args);
