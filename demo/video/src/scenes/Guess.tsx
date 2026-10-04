import { AbsoluteFill, interpolate, spring, useCurrentFrame, useVideoConfig } from "remotion";
import { clamp, colors, sans } from "../theme";

const Line: React.FC<{ text: string; start: number; color: string; shake?: boolean }> = ({ text, start, color, shake }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const s = spring({ frame: frame - start, fps, config: { damping: 12 } });
  const jitter = shake ? Math.sin((frame - start) * 3) * 14 * Math.max(0, 1 - (frame - start) / 14) : 0;
  return (
    <div
      style={{
        fontSize: 110,
        fontWeight: 700,
        letterSpacing: "-0.04em",
        color,
        opacity: s,
        transform: `translateY(${(1 - s) * 60}px) translateX(${jitter}px) scale(${0.9 + s * 0.1})`,
        filter: `blur(${(1 - s) * 10}px)`,
      }}
    >
      {text}
    </div>
  );
};

export const Guess: React.FC = () => {
  const frame = useCurrentFrame();
  return (
    <AbsoluteFill style={{ background: "#000", justifyContent: "center", alignItems: "center", fontFamily: sans, gap: 6 }}>
      <Line text="It guessed a folder." start={2} color={colors.text} />
      <Line text="It guessed wrong." start={24} color={colors.bad} shake />
      <div style={{ marginTop: 30, fontSize: 34, color: colors.dim, opacity: interpolate(frame, [46, 58], [0, 1], clamp) }}>
        so PowerShell crawled 2.5 million files instead of saying so
      </div>
    </AbsoluteFill>
  );
};
