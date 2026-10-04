import { AbsoluteFill, interpolate, useCurrentFrame, useVideoConfig } from "remotion";
import { Backdrop } from "../components/Backdrop";
import { Wordmark } from "../components/Wordmark";
import { clamp, colors, mono, sans } from "../theme";

export const Outro: React.FC = () => {
  const frame = useCurrentFrame();
  const { durationInFrames } = useVideoConfig();
  const fadeOut = interpolate(frame, [durationInFrames - 18, durationInFrames], [1, 0], clamp);
  const appear = (at: number) => interpolate(frame, [at, at + 12], [0, 1], clamp);

  return (
    <AbsoluteFill style={{ opacity: fadeOut }}>
      <Backdrop intensity={0.45} />
      <AbsoluteFill style={{ justifyContent: "center", alignItems: "center" }}>
        <Wordmark start={2} size={240} />
        <div style={{ marginTop: 30, fontFamily: mono, fontSize: 52, color: colors.accent, opacity: appear(30) }}>fetchle.dev</div>
        <div style={{ marginTop: 18, fontFamily: sans, fontSize: 32, color: colors.dim, opacity: appear(42) }}>
          open source · coming soon
        </div>
      </AbsoluteFill>
      <div style={{ position: "absolute", bottom: 40, right: 56, fontFamily: sans, fontSize: 20, color: colors.dim, opacity: appear(50) * 0.7 }}>
        concept preview · product numbers are targets
      </div>
    </AbsoluteFill>
  );
};
