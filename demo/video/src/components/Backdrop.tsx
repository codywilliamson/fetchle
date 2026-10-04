import { AbsoluteFill, useCurrentFrame } from "remotion";
import { alpha, colors } from "../theme";

export const Backdrop: React.FC<{ tint?: string; intensity?: number }> = ({ tint = colors.accent, intensity = 0.28 }) => {
  const frame = useCurrentFrame();
  const x = 50 + Math.sin(frame / 55) * 20;
  const y = 45 + Math.cos(frame / 70) * 15;
  return (
    <AbsoluteFill style={{ backgroundColor: colors.bg }}>
      <AbsoluteFill
        style={{ background: `radial-gradient(circle at ${x}% ${y}%, ${alpha(tint, intensity)} 0%, transparent 55%)` }}
      />
      <AbsoluteFill
        style={{
          backgroundImage: `linear-gradient(${colors.grid} 1px, transparent 1px), linear-gradient(90deg, ${colors.grid} 1px, transparent 1px)`,
          backgroundSize: "64px 64px",
          backgroundPosition: `0 ${frame * 0.8}px`,
          maskImage: "radial-gradient(ellipse at center, black 25%, transparent 72%)",
          WebkitMaskImage: "radial-gradient(ellipse at center, black 25%, transparent 72%)",
        }}
      />
    </AbsoluteFill>
  );
};
