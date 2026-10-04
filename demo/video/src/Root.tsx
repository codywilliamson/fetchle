import { Composition } from "remotion";
import { Hype, HYPE_FRAMES } from "./Hype";

export const Root: React.FC = () => (
  <Composition id="Hype" component={Hype} durationInFrames={HYPE_FRAMES} fps={30} width={1920} height={1080} />
);
