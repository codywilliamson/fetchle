using System.Numerics;

namespace Fetchle.Core.Embedding;

// adds up int8 embedding rows into int32 lanes. exact, and the global scale drops out when the sum is normalized
ref struct RowSum(ReadOnlySpan<sbyte> rows, int dims, Span<int> sums) : ITokenSink
{
    readonly ReadOnlySpan<sbyte> _rows = rows;
    readonly int _dims = dims;
    readonly Span<int> _sums = sums;

    public int Tokens { get; private set; }

    public void Add(int id)
    {
        Tokens++;
        var row = _rows.Slice(id * _dims, _dims);
        var i = 0;
        if (Vector.IsHardwareAccelerated && row.Length >= Vector<sbyte>.Count)
        {
            var lane = Vector<int>.Count;
            for (; i <= row.Length - Vector<sbyte>.Count; i += Vector<sbyte>.Count)
            {
                Vector.Widen(new Vector<sbyte>(row[i..]), out Vector<short> low, out Vector<short> high);
                Vector.Widen(low, out Vector<int> a, out Vector<int> b);
                Vector.Widen(high, out Vector<int> c, out Vector<int> d);
                Accumulate(a, i);
                Accumulate(b, i + lane);
                Accumulate(c, i + 2 * lane);
                Accumulate(d, i + 3 * lane);
            }
        }

        for (; i < row.Length; i++)
        {
            _sums[i] += row[i];
        }
    }

    void Accumulate(Vector<int> add, int at)
    {
        (new Vector<int>(_sums[at..]) + add).CopyTo(_sums[at..]);
    }

    // sums / ||sums|| into vector, false and zeros when nothing was added or the sums cancel out
    public readonly bool Normalize(Span<float> vector)
    {
        if (Tokens == 0)
        {
            vector.Clear();
            return false;
        }

        double squares = 0;
        foreach (var sum in _sums)
        {
            squares += (double)sum * sum;
        }

        if (squares == 0)
        {
            vector.Clear();
            return false;
        }

        var inverse = (float)(1 / Math.Sqrt(squares));
        for (var d = 0; d < _sums.Length; d++)
        {
            vector[d] = _sums[d] * inverse;
        }

        return true;
    }
}
