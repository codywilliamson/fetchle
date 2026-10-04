import { interpolate, useCurrentFrame } from "remotion";
import { clamp, colors } from "../theme";

export const Typed: React.FC<{ text: string; start: number; duration: number; caret?: boolean; color?: string }> = ({
  text,
  start,
  duration,
  caret = true,
  color = colors.text,
}) => {
  const frame = useCurrentFrame();
  const shown = Math.floor(interpolate(frame, [start, start + duration], [0, text.length], clamp));
  const typing = frame >= start && shown < text.length;
  const blinkOn = typing || Math.floor(frame / 15) % 2 === 0;
  return (
    <span style={{ color, whiteSpace: "pre-wrap" }}>
      {text.slice(0, shown)}
      {caret && frame >= start - 10 && (
        <span style={{ display: "inline-block", width: "0.6em", height: "1.1em", verticalAlign: "-0.2em", background: blinkOn ? colors.accent : "transparent" }} />
      )}
    </span>
  );
};
