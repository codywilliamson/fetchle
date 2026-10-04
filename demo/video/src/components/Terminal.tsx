import { alpha, colors, mono } from "../theme";

export const Terminal: React.FC<{
  title: string;
  width?: number;
  glow?: string;
  children: React.ReactNode;
}> = ({ title, width = 1500, glow = colors.accent, children }) => (
  <div
    style={{
      width,
      background: alpha(colors.panel, 0.92),
      border: `1px solid ${colors.border}`,
      borderRadius: 18,
      boxShadow: `0 40px 120px ${alpha("#000000", 0.6)}, 0 0 80px ${alpha(glow, 0.18)}`,
      overflow: "hidden",
      fontFamily: mono,
    }}
  >
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 10,
        padding: "16px 22px",
        borderBottom: `1px solid ${colors.border}`,
        color: colors.dim,
        fontSize: 20,
      }}
    >
      {["#FF5F57", "#FEBC2E", "#28C840"].map((c) => (
        <div key={c} style={{ width: 14, height: 14, borderRadius: 7, background: c }} />
      ))}
      <div style={{ marginLeft: 14 }}>{title}</div>
    </div>
    <div style={{ padding: "28px 34px", fontSize: 28, lineHeight: 1.6, color: colors.text }}>{children}</div>
  </div>
);
