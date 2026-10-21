using System.IO.Hashing;
using System.Text;

namespace Fetchle.Evals.Queries;

public enum Split { Dev, HeldOut }

// a query's split is a hash of its id, so it never moves and nobody picks it. about a third is held out.
// the spike's queries were seen while designing the rankers, so they are always dev
public static class QuerySplit
{
    const uint HELD_OUT_OF = 3;

    public static Split Of(EvalQuery query) =>
        query.Source != "s" && XxHash32.HashToUInt32(Encoding.UTF8.GetBytes(query.Id)) % HELD_OUT_OF == 0 ? Split.HeldOut : Split.Dev;
}
