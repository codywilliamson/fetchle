using Fetchle.Core.Index;
using Fetchle.Core.Ranking.Lexical;

namespace Fetchle.Core.Ranking;

// segments that name a scratch copy dir: any name holding the word worktree, worktrees or wt (".claude/worktrees",
// "site-worktrees", "app-wt"). agents check a repo out again and again under these, so the same file shows up many times and
// the copies sit shallower than the real checkout. found through the lexicon, so it costs three lookups per query
static class CopyMarks
{
    static readonly byte[][] Words = ["worktree"u8.ToArray(), "worktrees"u8.ToArray(), "wt"u8.ToArray()];

    // one bit per segment, so the entry pass tests a bit rather than hashing. null when no name is a copy dir
    public static ulong[]? Build(Snapshot snapshot)
    {
        var lexicon = snapshot.Lexicon;
        ulong[]? marks = null;
        foreach (var word in Words)
        {
            var rank = lexicon.Find(word);
            if (rank < 0)
            {
                continue;
            }

            marks ??= new ulong[(snapshot.SegmentCount + 63) / 64];
            foreach (var posting in lexicon.Postings(rank))
            {
                // a word in the name, not an extension: notes.wt isn't a worktree
                if (Posting.Kind(posting) != Posting.WORD)
                {
                    continue;
                }

                var segment = Posting.Segment(posting);
                marks[segment >> 6] |= 1UL << (int)(segment & 63);
            }
        }
        return marks;
    }

    public static bool IsSet(ulong[] marks, uint segment) => (marks[segment >> 6] & (1UL << (int)(segment & 63))) != 0;
}
