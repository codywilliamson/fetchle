import { AbsoluteFill, Easing, interpolate, useCurrentFrame } from "remotion";
import { Backdrop } from "../components/Backdrop";
import { Wordmark } from "../components/Wordmark";
import { alpha, clamp, colors, sans } from "../theme";

export const Reveal: React.FC = () => {
  const frame = useCurrentFrame();
  const streakX = interpolate(frame, [16, 30], [-900, 2600], { ...clamp, easing: Easing.in(Easing.quad) });
  const ring = interpolate(frame, [18, 55], [0, 1], { ...clamp, easing: Easing.out(Easing.cubic) });

  return (
    <AbsoluteFill>
      <Backdrop intensity={interpolate(frame, [0, 25], [0, 0.4], clamp)} />
      <AbsoluteFill style={{ justifyContent: "center", alignItems: "center" }}>
        <div
          style={{
            position: "absolute",
            width: 900,
            height: 900,
            borderRadius: "50%",
            border: `3px solid ${alpha(colors.accent, 1 - ring)}`,
            transform: `scale(${0.2 + ring * 1.8})`,
          }}
        />
        <Wordmark start={4} />
        <div
          style={{
            marginTop: 26,
            fontFamily: sans,
            fontSize: 48,
            fontWeight: 500,
            color: colors.dim,
            opacity: interpolate(frame, [40, 55], [0, 1], clamp),
            transform: `translateY(${interpolate(frame, [40, 55], [20, 0], clamp)}px)`,
          }}
        >
          Describe the file. <span style={{ color: colors.text }}>Get the path.</span>
        </div>
      </AbsoluteFill>
      <div
        style={{
          position: "absolute",
          top: 470,
          left: streakX,
          width: 700,
          height: 8,
          borderRadius: 4,
          background: `linear-gradient(90deg, transparent, ${colors.hot}, ${colors.accent}, transparent)`,
          boxShadow: `0 0 40px ${colors.accent}`,
          transform: "rotate(-4deg)",
        }}
      />
    </AbsoluteFill>
  );
};
