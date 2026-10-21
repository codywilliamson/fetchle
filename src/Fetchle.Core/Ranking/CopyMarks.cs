using Fetchle.Core.Index;
using Fetchle.Core.Ranking.Lexical;

namespace Fetchle.Core.Ranking;

// segments that name a scratch copy dir: any name holding the word worktree or worktrees (".claude/worktrees",
// "site-worktrees"). agents check a repo out again and again under these, so the same file shows up many times and
// the copies sit shallower than the real checkout. found through the lexicon, so it costs two lookups per query
static class CopyMarks
{
    static readonly byte[][] Words = ["worktree"u8.ToArray(), "worktrees"u8.ToArray()];

    public static HashSet<uint> Build(Snapshot snapshot)
    {
        var lexicon = snapshot.Lexicon;
        var marks = new HashSet<uint>();
        foreach (var word in Words)
        {
            var rank = lexicon.Find(word);
            if (rank < 0)
            {
                continue;
            }

            foreach (var posting in lexicon.Postings(rank))
            {
                marks.Add(Posting.Segment(posting));
            }
        }
        return marks;
    }
}
