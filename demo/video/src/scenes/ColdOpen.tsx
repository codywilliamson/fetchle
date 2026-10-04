import { AbsoluteFill, Easing, interpolate, useCurrentFrame } from "remotion";
import { Backdrop } from "../components/Backdrop";
import { Terminal } from "../components/Terminal";
import { Typed } from "../components/Typed";
import { clamp, colors, mono, sans } from "../theme";

const CMD = 'gci "$env:APPDATA\\Claude","$env:LOCALAPPDATA\\AnthropicClaude" -Recurse -Filter claude*.exe';
const SPINNER = "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏";
const TOTAL_FILES = 2485082;
const CRAWL = [40, 128] as const;
const FAIL = 130;

export const ColdOpen: React.FC = () => {
  const frame = useCurrentFrame();
  const files = Math.round(interpolate(frame, CRAWL, [0, TOTAL_FILES], { ...clamp, easing: Easing.in(Easing.cubic) }));
  const secs = interpolate(frame, CRAWL, [0, 120], clamp);
  const failed = frame >= FAIL;
  const shake = failed ? Math.sin(frame * 2.2) * 18 * Math.max(0, 1 - (frame - FAIL) / 12) : 0;
  const zoom = interpolate(frame, [0, 150], [1, 1.12], clamp);
  const heat = interpolate(frame, [CRAWL[0], FAIL], [0.08, 0.5], clamp);

  return (
    <AbsoluteFill>
      <Backdrop tint={colors.bad} intensity={heat} />
      <AbsoluteFill style={{ justifyContent: "center", alignItems: "center", transform: `scale(${zoom}) translateX(${shake}px)` }}>
        <div style={{ fontFamily: sans, fontSize: 30, color: colors.dim, marginBottom: 28, opacity: interpolate(frame, [8, 20], [0, 1], clamp) }}>
          your agent, looking for one file
        </div>
        <Terminal title="pwsh" glow={colors.bad}>
          <div>
            <span style={{ color: colors.dim }}>{"> "}</span>
            <Typed text={CMD} start={4} duration={30} caret={frame < 36} />
          </div>
          {frame >= 40 && (
            <div style={{ marginTop: 18, color: failed ? colors.dim : colors.text }}>
              <span style={{ color: colors.hot }}>{failed ? "·" : SPINNER[Math.floor(frame / 2) % SPINNER.length]}</span>
              {"  crawling  "}
              <span style={{ fontFamily: mono, fontWeight: 700, color: colors.text }}>{files.toLocaleString("en-US")}</span>
              {" files   "}
              <span style={{ color: secs > 90 ? colors.bad : colors.dim }}>{secs.toFixed(1)}s</span>
            </div>
          )}
          {failed && (
            <div style={{ marginTop: 10, color: colors.bad, fontWeight: 700 }}>
              ✗ command timed out after 120s · 0 results
            </div>
          )}
        </Terminal>
      </AbsoluteFill>
    </AbsoluteFill>
  );
};
