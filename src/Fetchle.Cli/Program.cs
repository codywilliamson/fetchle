using Fetchle.Cli.Commands;
using Fetchle.Core.Index;
using Fetchle.Core.Search;
using Fetchle.Core.Walking;

var env = CliEnvironment.FromProcess();
using var model = ModelLoader.ForSearch(Console.Error);

// reads the snapshot for every root it covers and walks the rest live
IFileSearch search = new IndexedFileSearch(PruneRules.Default, new IndexStore(env.Dirs.Index), model);

var app = new FetchleApp(search, env, model);
return (int)await app.RunAsync(args);
