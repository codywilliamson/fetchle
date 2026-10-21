using Fetchle.Core.Search;
using Fetchle.Evals.Corpus;

namespace Fetchle.Evals.Running;

// a ranking under test, run against the in-memory corpus
public interface IEvalSearcher
{
    string Name { get; }

    SearchResult Search(EvalCorpus corpus, string query, int limit);
}
