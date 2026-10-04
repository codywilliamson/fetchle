import { spring, useCurrentFrame, useVideoConfig } from "remotion";
import { colors, sans } from "../theme";

export const Wordmark: React.FC<{ start?: number; size?: number }> = ({ start = 0, size = 220 }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const blink = Math.floor((frame - start) / 14) % 2 === 0;
  return (
    <div style={{ display: "flex", alignItems: "flex-end", fontFamily: sans, fontWeight: 700, fontSize: size, letterSpacing: "-0.05em", color: colors.text }}>
      {"fetchle".split("").map((ch, i) => {
        const s = spring({ frame: frame - start - i * 3, fps, config: { damping: 11, mass: 0.7 } });
        return (
          <span key={i} style={{ display: "inline-block", opacity: Math.min(1, s * 1.5), transform: `translateY(${(1 - s) * 120}px) scale(${0.6 + s * 0.4})` }}>
            {ch}
          </span>
        );
      })}
      <span
        style={{
          width: size * 0.32,
          height: size * 0.62,
          marginLeft: size * 0.06,
          marginBottom: size * 0.16,
          background: colors.accent,
          opacity: frame - start > 22 && blink ? 1 : 0.15,
          boxShadow: `0 0 ${size * 0.3}px ${colors.accent}`,
        }}
      />
    </div>
  );
};
