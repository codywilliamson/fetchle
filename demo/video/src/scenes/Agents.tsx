import { AbsoluteFill, interpolate, spring, useCurrentFrame, useVideoConfig } from "remotion";
import { Backdrop } from "../components/Backdrop";
import { Headline } from "../components/Headline";
import { Terminal } from "../components/Terminal";
import { Typed } from "../components/Typed";
import { clamp, colors } from "../theme";

const REQUEST = `{
  "tool": "find_files",
  "query": "the api build output",
  "budget_ms": 2000
}`;
const RESPONSE = [
  "src/api/bin/Release/net10.0/Api.dll",
  "src/api/bin/Release/net10.0/Api.pdb",
  "src/api/obj/Release/net10.0/Api.dll",
];
const REPLY = 50;

export const Agents: React.FC = () => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const dash = -frame * 4;
  const arrow = interpolate(frame, [36, 48], [0, 1], clamp);

  return (
    <AbsoluteFill>
      <Backdrop />
      <AbsoluteFill style={{ justifyContent: "center", alignItems: "center", gap: 60 }}>
        <Headline text="Built for agents." sub="one MCP call · ranked results · always inside the budget" size={80} />
        <div style={{ display: "flex", alignItems: "center", gap: 30 }}>
          <Terminal title="agent → fetchle mcp" width={720}>
            <Typed text={REQUEST} start={4} duration={30} caret={frame < 40} />
          </Terminal>
          <svg width={120} height={40} style={{ opacity: arrow }}>
            <line x1={0} y1={20} x2={100} y2={20} stroke={colors.accent} strokeWidth={4} strokeDasharray="10 8" strokeDashoffset={dash} />
            <polygon points="100,8 120,20 100,32" fill={colors.accent} />
          </svg>
          <Terminal title="fetchle → agent" width={820} glow={colors.ok}>
            {RESPONSE.map((p, i) => {
              const s = spring({ frame: frame - REPLY - i * 5, fps, config: { damping: 15 } });
              return (
                <div key={p} style={{ opacity: s, transform: `translateY(${(1 - s) * 20}px)`, whiteSpace: "nowrap" }}>
                  {p}
                </div>
              );
            })}
            <div style={{ marginTop: 10, color: colors.dim, fontSize: 24, opacity: interpolate(frame, [REPLY + 18, REPLY + 26], [0, 1], clamp) }}>
              3 matches · <span style={{ color: colors.ok, fontWeight: 700 }}>4 ms</span> · stopped_early: null
            </div>
          </Terminal>
        </div>
      </AbsoluteFill>
    </AbsoluteFill>
  );
};
