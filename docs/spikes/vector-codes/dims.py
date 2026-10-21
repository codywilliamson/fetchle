# dims experiment: centroid+depth rank of each spike target at different stored widths
import re, sys, time, numpy as np
from model2vec import StaticModel
SP = "../../../../spikes/embeddings/"  # the embeddings spike folder, outside this repo
m = StaticModel.from_pretrained(SP + "models/potion-retrieval-32M")
paths = [l.rstrip("\n") for l in open(SP + "paths-pruned.txt", encoding="utf8")]
queries = [l.rstrip("\n").split("\t") for l in open(SP + "queries.tsv", encoding="utf8")]
WORD = re.compile(r"[A-Z]+(?![a-z])|[A-Z]?[a-z]+|\d+")
def words(seg): return " ".join(WORD.findall(seg))
seg_ids = {}; path_segs = []
for p in paths:
    ids = []
    for s in p.split("\\"):
        ids.append(seg_ids.setdefault(s, len(seg_ids)))
    path_segs.append(ids)
segs = list(seg_ids)
t = time.time(); V = m.encode([words(s) or s for s in segs], batch_size=4096).astype(np.float32); print("encode", len(segs), time.time() - t)
# flat (path, segment, weight) arrays for vectorized centroid scoring
P = np.repeat(np.arange(len(paths)), [len(x) for x in path_segs]); S = np.concatenate([np.array(x) for x in path_segs])
W = np.concatenate([np.array([0.5] * (len(x) - 2) + ([1.0] if len(x) > 1 else []) + [2.0]) for x in path_segs]).astype(np.float32)
depth = np.array([len(x) for x in path_segs], dtype=np.float32)
def quant8(X): sc = np.abs(X).max(1, keepdims=True) / 127 + 1e-12; return np.round(X / sc).clip(-127, 127) * sc
def quant4(X): sc = np.abs(X).max(1, keepdims=True) / 7 + 1e-12; return np.round(X / sc).clip(-7, 7) * sc
def variant(name):
    if name == "512f32": return V, None
    if name == "512i8": return quant8(V), None
    d, q = name.split("-")[0], name.split("-")[1]
    d = int(d.replace("t", "").replace("p", ""))
    if name.startswith("p"):
        mu = V.mean(0); U, s, Vt = np.linalg.svd((V - mu)[np.random.default_rng(0).choice(len(V), 50000, replace=False)], full_matrices=False)
        proj = Vt[:d].T; X = (V - mu) @ proj
    else:
        proj = None; X = V[:, :d]
    X = X / (np.linalg.norm(X, axis=1, keepdims=True) + 1e-12)
    return (quant8(X) if q == "i8" else quant4(X)), (proj, (V.mean(0) if name.startswith("p") else None))
def rank_all(X, proj):
    out = []
    # centroid norms
    C = np.zeros((len(paths), X.shape[1]), np.float32); np.add.at(C, P, X[S] * W[:, None]); norms = np.linalg.norm(C, axis=1) + 1e-12
    for q, rx in queries:
        qv = m.encode([q])[0].astype(np.float32)
        if proj is not None and proj[0] is not None: qv = (qv - proj[1]) @ proj[0]
        elif X.shape[1] < 512: qv = qv[:X.shape[1]]
        qv /= np.linalg.norm(qv) + 1e-12
        sims = X @ qv; dots = np.bincount(P, weights=W * sims[S], minlength=len(paths))
        score = dots / norms - 0.02 * depth
        r = re.compile(rx); order = np.argsort(-score)
        hit = next((i + 1 for i, p in enumerate(order[:20000]) if r.search(paths[p])), None)
        out.append(hit)
    return out
for name in sys.argv[1:]:
    X, proj = variant(name); rs = rank_all(X, proj)
    print(f"{name:8} top3={sum(1 for r in rs if r and r<=3)} top10={sum(1 for r in rs if r and r<=10)} top50={sum(1 for r in rs if r and r<=50)} ranks={rs}", flush=True)
