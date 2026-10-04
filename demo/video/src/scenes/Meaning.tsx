import { AbsoluteFill, Easing, interpolate, random, spring, useCurrentFrame, useVideoConfig } from "remotion";
import { Backdrop } from "../components/Backdrop";
import { Headline } from "../components/Headline";
import { alpha, clamp, colors, mono } from "../theme";

const FILES = [
  "invoice_2026_03.pdf", "package.json", "settings.json", "vacation.jpg", "README.md", "Dockerfile",
  "notes.md", "schema.sql", "Discord.exe", "obs64.exe", "thesis_v3.docx", "tsconfig.json",
  "places.sqlite", "budget.xlsx", "Code.exe", "id_ed25519.pub", "launch.json", "Steam.exe",
  "logo.svg", "backup.zip", "app.log", "Makefile",
];
const TARGET = { name: "Spotify.exe", x: 1330, y: 600 };
// the music cluster sits next to the target, like neighbors in embedding space
const CLUSTER = [
  { name: "playlist.m3u", x: 1180, y: 480 },
  { name: "SoundCloud.url", x: 1500, y: 470 },
  { name: "Podcasts.lnk", x: 1230, y: 760 },
];
const QUERY = "music streaming app";
const FLY = [30, 70] as const;
const COLS = 6;
const CELL = { w: 290, h: 170 };
const ORIGIN = { x: 120, y: 290 };
// keep the target cluster and the query pill clear of noise
const KEEP_OUT = [
  { x0: 1050, x1: 1800, y0: 400, y1: 840 },
  { x0: 0, x1: 760, y0: 520, y1: 680 },
];

const grid = Array.from({ length: COLS * 4 }, (_, i) => ({
  x: ORIGIN.x + (i % COLS) * CELL.w + random(`x${i}`) * 120,
  y: ORIGIN.y + Math.floor(i / COLS) * CELL.h + random(`y${i}`) * 70,
})).filter((p) => !KEEP_OUT.some((k) => p.x > k.x0 && p.x < k.x1 && p.y > k.y0 && p.y < k.y1));
const nodes = [
  ...grid.slice(0, FILES.length).map((p, i) => ({ ...p, name: FILES[i], near: false })),
  ...CLUSTER.map((c) => ({ ...c, near: true })),
];
const near = CLUSTER;

export const Meaning: React.FC = () => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const t = interpolate(frame, FLY, [0, 1], { ...clamp, easing: Easing.inOut(Easing.cubic) });
  const qx = interpolate(t, [0, 1], [180, TARGET.x - 40]);
  const qy = interpolate(t, [0, 1], [620, TARGET.y]) - Math.sin(t * Math.PI) * 180;
  const lines = interpolate(frame, [FLY[1], FLY[1] + 15], [0, 1], clamp);
  const hit = spring({ frame: frame - FLY[1], fps, config: { damping: 9 } });
  const typed = QUERY.slice(0, Math.floor(interpolate(frame, [4, 26], [0, QUERY.length], clamp)));

  return (
    <AbsoluteFill>
      <Backdrop tint={colors.ok} intensity={0.16} />
      <div style={{ position: "absolute", top: 70, width: "100%" }}>
        <Headline text="It knows what you mean." size={72} start={2} />
      </div>
      <svg width={1920} height={1080} style={{ position: "absolute" }}>
        {near.map((n) => (
          <line key={n.name} x1={qx} y1={qy} x2={n.x} y2={n.y} stroke={colors.ok} strokeOpacity={0.5 * lines} strokeWidth={2} strokeDasharray="6 8" />
        ))}
        <line x1={qx} y1={qy} x2={TARGET.x} y2={TARGET.y} stroke={colors.accent} strokeWidth={4} strokeOpacity={lines} />
        {nodes.map((n, i) => {
          const isNear = n.near;
          const fade = interpolate(frame, [FLY[1], FLY[1] + 15], [1, isNear ? 0.9 : 0.25], clamp);
          return (
            <g key={i} opacity={fade}>
              <circle cx={n.x} cy={n.y} r={6} fill={isNear ? colors.ok : colors.dim} />
              <text x={n.x + 14} y={n.y + 7} fill={colors.dim} fontSize={20} fontFamily={mono}>
                {n.name}
              </text>
            </g>
          );
        })}
        <circle cx={TARGET.x} cy={TARGET.y} r={10 + hit * 10} fill={colors.accent} />
        <circle cx={TARGET.x} cy={TARGET.y} r={10 + hit * 60} fill="none" stroke={colors.accent} strokeOpacity={Math.max(0, 1 - hit) * 0.8} strokeWidth={3} />
        <text x={TARGET.x + 34} y={TARGET.y + 14} fill={colors.text} fontSize={34 + hit * 8} fontWeight={700} fontFamily={mono}>
          {TARGET.name}
        </text>
        <circle cx={qx} cy={qy} r={12} fill={colors.hot} opacity={frame >= FLY[0] ? 1 : 0} />
      </svg>
      <div
        style={{
          position: "absolute",
          left: 120,
          top: 570,
          padding: "14px 26px",
          borderRadius: 40,
          border: `2px solid ${colors.hot}`,
          background: alpha(colors.panel, 0.9),
          fontFamily: mono,
          fontSize: 32,
          color: colors.text,
          opacity: interpolate(frame, [FLY[0], FLY[0] + 10], [1, 0], clamp),
        }}
      >
        "{typed}"
      </div>
      <div
        style={{
          position: "absolute",
          left: TARGET.x - 10,
          top: TARGET.y + 50,
          padding: "8px 18px",
          borderRadius: 8,
          background: colors.accent,
          color: "#000",
          fontFamily: mono,
          fontSize: 24,
          fontWeight: 700,
          opacity: hit,
          transform: `scale(${0.7 + hit * 0.3})`,
          transformOrigin: "left top",
        }}
      >
        #1 · zero shared keywords
      </div>
    </AbsoluteFill>
  );
};
