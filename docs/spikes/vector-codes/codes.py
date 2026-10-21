# 64-byte codes over all 512 dims: sign bits (symmetric hamming / asymmetric), and 8-bit PQ with 64 subspaces
import sys, numpy as np
exec(open("dims.py").read().split("def quant8")[0])  # reuse corpus + encode setup
mu = V.mean(0)
def sign_codes(center): X = V - (mu if center else 0); return np.where(X >= 0, 1.0, -1.0).astype(np.float32)
def pq(Xc, M=64, K=256, iters=12):
    rng = np.random.default_rng(0); d = Xc.shape[1] // M; codes = np.zeros((len(Xc), M), np.uint8); books = []
    sample = Xc[rng.choice(len(Xc), 60000, replace=False)]
    for j in range(M):
        sub = sample[:, j*d:(j+1)*d]; C = sub[rng.choice(len(sub), K, replace=False)].copy()
        for _ in range(iters):
            a = ((sub[:, None, :] - C[None]) ** 2).sum(-1).argmin(1)
            for k in range(K):
                sel = sub[a == k]
                if len(sel): C[k] = sel.mean(0)
        full = Xc[:, j*d:(j+1)*d]
        codes[:, j] = np.concatenate([((full[i:i+50000, None, :] - C[None]) ** 2).sum(-1).argmin(1) for i in range(0, len(full), 50000)])
        books.append(C)
    return codes, books
def ranks(simfn):
    out = []
    for q, rx in queries:
        qv = m.encode([q])[0].astype(np.float32); sims = simfn(qv)
        # centroid with norms from reconstructed vectors is query independent; approximate with weights only
        dots = np.bincount(P, weights=W * sims[S], minlength=len(paths)); score = dots / NORMS - 0.02 * depth
        r = re.compile(rx); order = np.argsort(-score)
        out.append(next((i + 1 for i, p in enumerate(order[:20000]) if r.search(paths[p])), None))
    return out
def norms_of(X):
    C = np.zeros((len(paths), X.shape[1]), np.float32); np.add.at(C, P, X[S] * W[:, None]); return np.linalg.norm(C, axis=1) + 1e-12
def report(name, rs): print(f"{name:14} top3={sum(1 for r in rs if r and r<=3)} top10={sum(1 for r in rs if r and r<=10)} top50={sum(1 for r in rs if r and r<=50)} ranks={rs}", flush=True)
for center in (False, True):
    B = sign_codes(center); Bn = B / np.sqrt(512)
    NORMS = norms_of(Bn)
    report(f"sign-asym c={int(center)}", ranks(lambda q: Bn @ ((q - (mu if center else 0)) / np.linalg.norm(q - (mu if center else 0)))))
    report(f"sign-ham c={int(center)}", ranks(lambda q: Bn @ (np.where((q - (mu if center else 0)) >= 0, 1, -1) / np.sqrt(512))))
codes, books = pq(V)
R = np.concatenate([books[j][codes[:, j]] for j in range(64)], axis=1)
NORMS = norms_of(R)
report("pq64x8 asym", ranks(lambda q: R @ (q / np.linalg.norm(q))))
