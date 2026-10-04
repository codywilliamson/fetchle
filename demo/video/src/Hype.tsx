import { Audio, staticFile } from "remotion";
import { linearTiming, TransitionPresentation, TransitionSeries } from "@remotion/transitions";
import { fade } from "@remotion/transitions/fade";
import { slide } from "@remotion/transitions/slide";
import { wipe } from "@remotion/transitions/wipe";
import { ColdOpen } from "./scenes/ColdOpen";
import { Guess } from "./scenes/Guess";
import { Reveal } from "./scenes/Reveal";
import { Search } from "./scenes/Search";
import { Race } from "./scenes/Race";
import { Meaning } from "./scenes/Meaning";
import { Agents } from "./scenes/Agents";
import { Stats } from "./scenes/Stats";
import { Outro } from "./scenes/Outro";

const T = 12;

const scenes = [
  { C: ColdOpen, frames: 150 },
  { C: Guess, frames: 80 },
  { C: Reveal, frames: 95 },
  { C: Search, frames: 155 },
  { C: Race, frames: 110 },
  { C: Meaning, frames: 125 },
  { C: Agents, frames: 125 },
  { C: Stats, frames: 125 },
  { C: Outro, frames: 125 },
];

// each presentation has its own prop type
const presentations: TransitionPresentation<any>[] = [
  fade(),
  wipe({ direction: "from-left" }),
  slide({ direction: "from-bottom" }),
  fade(),
  slide({ direction: "from-right" }),
  wipe({ direction: "from-top-left" }),
  fade(),
  fade(),
];

export const HYPE_FRAMES = scenes.reduce((sum, s) => sum + s.frames, 0) - T * presentations.length;

export const Hype: React.FC = () => (
  <>
    <Audio src={staticFile("soundtrack.wav")} />
    <TransitionSeries>
      {scenes.flatMap(({ C, frames }, i) => {
        const seq = (
          <TransitionSeries.Sequence key={`s${i}`} durationInFrames={frames}>
            <C />
          </TransitionSeries.Sequence>
        );
        if (i === scenes.length - 1) return [seq];
        return [
          seq,
          <TransitionSeries.Transition key={`t${i}`} presentation={presentations[i]} timing={linearTiming({ durationInFrames: T })} />,
        ];
      })}
    </TransitionSeries>
  </>
);
