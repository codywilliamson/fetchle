# Vector codes experiment

What 64 bytes per segment can hold without losing the spike's rankings. Both scripts read the embeddings spike's corpus and queries (`spikes/embeddings/paths-pruned.txt`, `queries.tsv`, 567,584 real AppData paths, kept outside this repo) and score each query's target with the spike's centroid+depth scorer.

    uv run --with model2vec python -I dims.py 512f32 512i8 t64-i8 t128-i8 t128-i4 p64-i8 p128-i4 t256-i8
    uv run --with model2vec python -I codes.py

`dims.py` truncates or PCA-projects to fewer dims; `codes.py` tries 512 sign bits (symmetric hamming and asymmetric, centered or not) and 8-bit product quantization. Results and the choice are in docs/decisions.md, 2026-10-21, "segment vectors are the 512 signs".
