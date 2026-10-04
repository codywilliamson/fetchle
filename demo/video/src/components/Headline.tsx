import { interpolate, spring, useCurrentFrame, useVideoConfig } from "remotion";
import { clamp, colors, sans } from "../theme";

export const Headline: React.FC<{ text: string; start?: number; size?: number; color?: string; sub?: string }> = ({
  text,
  start = 0,
  size = 72,
  color = colors.text,
  sub,
}) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const words = text.split(" ");
  return (
    <div style={{ fontFamily: sans, textAlign: "center" }}>
      <div style={{ fontSize: size, fontWeight: 700, letterSpacing: "-0.03em", color }}>
        {words.map((w, i) => {
          const s = spring({ frame: frame - start - i * 3, fps, config: { damping: 14, mass: 0.6 } });
          return (
            <span
              key={i}
              style={{
                display: "inline-block",
                marginRight: "0.25em",
                opacity: s,
                transform: `translateY(${(1 - s) * 40}px)`,
                filter: `blur(${(1 - s) * 8}px)`,
              }}
            >
              {w}
            </span>
          );
        })}
      </div>
      {sub && (
        <div
          style={{
            marginTop: 14,
            fontSize: size * 0.38,
            fontWeight: 500,
            color: colors.dim,
            opacity: interpolate(frame, [start + 12, start + 24], [0, 1], clamp),
          }}
        >
          {sub}
        </div>
      )}
    </div>
  );
};
