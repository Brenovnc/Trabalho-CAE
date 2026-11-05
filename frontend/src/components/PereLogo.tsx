export function PereLogo({ size = 36 }: { size?: number }) {
  return <svg width={size} height={size} viewBox="0 0 512 512" role="img" aria-label="PERE">
    <rect x="16" y="16" width="480" height="480" rx="112" fill="var(--logo-bg)"/>
    <g fill="none" stroke="var(--logo-mark)" strokeWidth="16" strokeLinecap="butt" strokeLinejoin="miter">
      <path d="M 111.1 217.2 A 150 150 0 0 1 400.9 217.2"/><path d="M 426.9 172.2 L 400.9 217.2 L 355.9 191.2"/>
      <g transform="rotate(180 256 256)"><path d="M 111.1 217.2 A 150 150 0 0 1 400.9 217.2"/><path d="M 426.9 172.2 L 400.9 217.2 L 355.9 191.2"/></g>
      <path d="M 211 188 V 324 M 211 196 H 263 A 38 38 0 0 1 263 272 H 211"/>
    </g>
  </svg>
}
