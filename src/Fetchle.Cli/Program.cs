using Fetchle.Cli.Commands;
using Fetchle.Core.Naive;
using Fetchle.Core.Search;
using Fetchle.Core.Walking;

// swap the search implementation here
IFileSearch search = new NaiveFileSearch(PruneRules.Default);

var app = new FetchleApp(search, CliEnvironment.FromProcess());
return (int)await app.RunAsync(args);
