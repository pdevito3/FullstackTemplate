export {}

declare module 'react' {
  /** A style can set a CSS custom property, such as `--sidebar-width`. */
  interface CSSProperties {
    [property: `--${string}`]: string | number | undefined
  }
}
