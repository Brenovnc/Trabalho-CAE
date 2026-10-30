export function PereLogo({ size = 36 }: { size?: number }) {
  return <svg width={size} height={size} viewBox="0 0 48 48" role="img" aria-label="PERE">
    <defs><linearGradient id="pere-gradient" x1="0" y1="0" x2="1" y2="1"><stop stopColor="#7887ff"/><stop offset="1" stopColor="#aab2ff"/></linearGradient></defs>
    <rect x="1" y="1" width="46" height="46" rx="14" fill="url(#pere-gradient)"/>
    <path d="M34 18a12 12 0 0 0-21-4l-2 2m0 0v-6m0 6h6M14 30a12 12 0 0 0 21 4l2-2m0 0v6m0-6h-6" fill="none" stroke="white" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"/>
    <path d="M21 17h7a5 5 0 0 1 0 10h-7zm0 0v15" fill="none" stroke="white" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"/>
  </svg>
}
