import { spring, useCurrentFrame, useVideoConfig } from "remotion";
import { alpha, colors } from "../theme";

export type Result = { dir: string; name: string; match: string; score: number; meta: string };

const highlight = (name: string, match: string) => {
  const i = name.toLowerCase().indexOf(match.toLowerCase());
  if (!match || i < 0) return <>{name}</>;
  return (
    <>
      {name.slice(0, i)}
      <span style={{ color: colors.hot, fontWeight: 700 }}>{name.slice(i, i + match.length)}</span>
      {name.slice(i + match.length)}
    </>
  );
};

export const ResultRow: React.FC<{ r: Result; start: number; rank: number }> = ({ r, start, rank }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const s = spring({ frame: frame - start, fps, config: { damping: 15 } });
  const bar = spring({ frame: frame - start - 4, fps, config: { damping: 20 } });
  const first = rank === 1;
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 24,
        padding: "10px 16px",
        borderRadius: 10,
        background: first ? alpha(colors.accent, 0.1 * s) : "transparent",
        opacity: s,
        transform: `translateX(${(1 - s) * 60}px)`,
      }}
    >
      <span style={{ color: first ? colors.accent : colors.dim, width: 30 }}>{rank}</span>
      <span style={{ flex: 1, whiteSpace: "nowrap", overflow: "hidden" }}>
        <span style={{ color: colors.dim }}>{r.dir}</span>
        {highlight(r.name, r.match)}
      </span>
      <span style={{ width: 160, height: 10, borderRadius: 5, background: colors.border }}>
        <span style={{ display: "block", height: 10, borderRadius: 5, width: 160 * r.score * bar, background: first ? colors.accent : colors.dim }} />
      </span>
      <span style={{ color: colors.dim, fontSize: 22, width: 150, textAlign: "right" }}>{r.meta}</span>
    </div>
  );
};
