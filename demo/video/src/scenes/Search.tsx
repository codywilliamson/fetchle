import { AbsoluteFill, interpolate, spring, useCurrentFrame, useVideoConfig } from "remotion";
import { Backdrop } from "../components/Backdrop";
import { Headline } from "../components/Headline";
import { Result, ResultRow } from "../components/ResultRow";
import { Terminal } from "../components/Terminal";
import { Typed } from "../components/Typed";
import { clamp, colors } from "../theme";

const QUERY = 'fetchle "desktop app bundled claude binary"';
const ENTER = 50;

const RESULTS: Result[] = [
  { dir: "~/AppData/…/claude-code/2.1.286/", name: "claude.exe", match: "claude", score: 0.94, meta: "212 MB · 2d" },
  { dir: "~/AppData/…/claude-code/2.1.284/", name: "claude.exe", match: "claude", score: 0.89, meta: "211 MB · 9d" },
  { dir: "~/AppData/Roaming/Claude/", name: "claude_desktop_config.json", match: "claude", score: 0.41, meta: "2 KB · 1d" },
];

export const Search: React.FC = () => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const pop = spring({ frame: frame - (ENTER + 30), fps, config: { damping: 8 } });
  const flash = interpolate(frame, [ENTER, ENTER + 8], [0.35, 0], clamp);

  return (
    <AbsoluteFill>
      <Backdrop />
      <AbsoluteFill style={{ background: colors.accent, opacity: frame >= ENTER ? flash : 0 }} />
      <AbsoluteFill style={{ justifyContent: "center", alignItems: "center", gap: 50 }}>
        <Headline text="The right file. First try." start={ENTER + 40} size={64} />
        <Terminal title="fetchle">
          <div>
            <span style={{ color: colors.accent }}>{"❯ "}</span>
            <Typed text={QUERY} start={6} duration={36} caret={frame < ENTER} />
          </div>
          <div style={{ marginTop: 18, minHeight: 230 }}>
            {RESULTS.map((r, i) => (
              <ResultRow key={i} r={r} rank={i + 1} start={ENTER + 4 + i * 6} />
            ))}
          </div>
          {frame >= ENTER + 26 && (
            <div style={{ marginTop: 12, color: colors.dim, fontSize: 24 }}>
              3 of 3 matches ·{" "}
              <span style={{ display: "inline-block", color: colors.ok, fontWeight: 700, transform: `scale(${0.6 + pop * 0.4})` }}>6 ms</span>
            </div>
          )}
        </Terminal>
      </AbsoluteFill>
    </AbsoluteFill>
  );
};
