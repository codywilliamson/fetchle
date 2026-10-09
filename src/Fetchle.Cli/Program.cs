using Fetchle.Cli.Commands;
using Fetchle.Core.Search;
using Fetchle.Core.Walking;

// swap the search implementation here
IFileSearch search = new FileSearch(PruneRules.Default);

var app = new FetchleApp(search, CliEnvironment.FromProcess());
return (int)await app.RunAsync(args);
