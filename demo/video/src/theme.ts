import { loadFont as loadMono } from "@remotion/google-fonts/JetBrainsMono";
import { loadFont as loadSans } from "@remotion/google-fonts/SpaceGrotesk";

export const mono = loadMono("normal", { weights: ["400", "700"], subsets: ["latin"] }).fontFamily;
export const sans = loadSans("normal", { weights: ["500", "700"], subsets: ["latin"] }).fontFamily;

export const colors = {
  bg: "#07070B",
  panel: "#0F0F16",
  border: "#25253A",
  grid: "#16162A",
  text: "#EDEDF2",
  dim: "#7C7C92",
  accent: "#FF6A3D",
  hot: "#FFB23D",
  ok: "#4DE2D0",
  bad: "#FF4D5E",
};

export const alpha = (hex: string, a: number) => {
  const n = parseInt(hex.slice(1), 16);
  return `rgba(${(n >> 16) & 255}, ${(n >> 8) & 255}, ${n & 255}, ${a})`;
};

export const clamp = { extrapolateLeft: "clamp", extrapolateRight: "clamp" } as const;
