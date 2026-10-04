import { AbsoluteFill, interpolate, spring, useCurrentFrame, useVideoConfig } from "remotion";
import { Backdrop } from "../components/Backdrop";
import { Headline } from "../components/Headline";
import { alpha, clamp, colors, sans } from "../theme";

const CARDS = [
  { value: "5.6 MB", label: "native executable" },
  { value: "0 bytes", label: "leave your machine" },
  { value: "3 OSes", label: "Windows · Linux · macOS" },
  { value: ".NET 10", label: "NativeAOT · no runtime" },
];
const GAP = 14;
const FIRST = 20;

const Card: React.FC<{ value: string; label: string; start: number }> = ({ value, label, start }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const s = spring({ frame: frame - start, fps, config: { damping: 10, stiffness: 180 } });
  const glow = interpolate(frame, [start, start + 20], [1, 0.25], clamp);
  return (
    <div
      style={{
        width: 380,
        padding: "44px 30px",
        borderRadius: 24,
        background: colors.panel,
        border: `1px solid ${alpha(colors.accent, 0.3 + glow * 0.5)}`,
        boxShadow: `0 0 ${60 * glow}px ${alpha(colors.accent, 0.5 * glow)}`,
        textAlign: "center",
        fontFamily: sans,
        opacity: Math.min(1, s * 2),
        transform: `scale(${1.5 - s * 0.5})`,
      }}
    >
      <div style={{ fontSize: 76, fontWeight: 700, letterSpacing: "-0.04em", color: colors.text }}>{value}</div>
      <div style={{ marginTop: 10, fontSize: 26, color: colors.dim }}>{label}</div>
    </div>
  );
};

export const Stats: React.FC = () => (
  <AbsoluteFill>
    <Backdrop intensity={0.32} />
    <AbsoluteFill style={{ justifyContent: "center", alignItems: "center", gap: 80 }}>
      <Headline text="Small. Private. Everywhere." size={84} />
      <div style={{ display: "flex", gap: 34 }}>
        {CARDS.map((c, i) => (
          <Card key={c.value} {...c} start={FIRST + i * GAP} />
        ))}
      </div>
    </AbsoluteFill>
  </AbsoluteFill>
);
