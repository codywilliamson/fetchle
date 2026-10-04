import { AbsoluteFill, interpolate, spring, useCurrentFrame, useVideoConfig } from "remotion";
import { Backdrop } from "../components/Backdrop";
import { Headline } from "../components/Headline";
import { alpha, clamp, colors, mono, sans } from "../theme";

const TRACK = 1300;
const CRAWL_END = 95;

const Lane: React.FC<{ label: string; progress: number; color: string; time: string; timeColor: string; note: string }> = ({
  label,
  progress,
  color,
  time,
  timeColor,
  note,
}) => (
  <div style={{ width: TRACK + 300 }}>
    <div style={{ display: "flex", justifyContent: "space-between", fontFamily: mono, fontSize: 30, marginBottom: 14 }}>
      <span style={{ color: colors.text }}>{label}</span>
      <span style={{ color: timeColor, fontWeight: 700 }}>{time}</span>
    </div>
    <div style={{ height: 34, borderRadius: 17, background: colors.panel, border: `1px solid ${colors.border}`, overflow: "hidden" }}>
      <div style={{ height: "100%", width: `${progress * 100}%`, borderRadius: 17, background: color, boxShadow: `0 0 40px ${alpha(color, 0.7)}` }} />
    </div>
    <div style={{ marginTop: 10, fontFamily: sans, fontSize: 22, color: colors.dim }}>{note}</div>
  </div>
);

export const Race: React.FC = () => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const slow = interpolate(frame, [14, CRAWL_END], [0, 0.34], clamp);
  const secs = interpolate(frame, [14, CRAWL_END], [0, 120], clamp);
  const fast = spring({ frame: frame - 16, fps, config: { damping: 18, stiffness: 300 } });
  const done = frame >= CRAWL_END;

  return (
    <AbsoluteFill>
      <Backdrop />
      <AbsoluteFill style={{ justifyContent: "center", alignItems: "center", gap: 70 }}>
        <Headline text="Same question." sub="one guessed folder vs one ranked index" size={80} />
        <Lane
          label="Get-ChildItem -Recurse"
          progress={slow}
          color={colors.bad}
          time={done ? "120 s · timed out" : `${secs.toFixed(1)} s`}
          timeColor={done ? colors.bad : colors.dim}
          note="the real command, the real timeout"
        />
        <Lane label="fetchle" progress={fast} color={colors.accent} time={frame >= 18 ? "6 ms" : "…"} timeColor={colors.ok} note="answered from a local index" />
      </AbsoluteFill>
    </AbsoluteFill>
  );
};
