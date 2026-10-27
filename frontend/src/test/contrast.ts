// The WCAG contrast of the painted text of an element against its painted background, for the component tests.

type Rgba = [number, number, number, number]

const canvas = document.createElement('canvas')
canvas.width = canvas.height = 1
const context = canvas.getContext('2d', { willReadFrequently: true })!

// The canvas paints the color in sRGB, so an oklch color reads back as the bytes that the screen shows.
function painted(color: string): Rgba {
  context.clearRect(0, 0, 1, 1)
  context.fillStyle = color
  context.fillRect(0, 0, 1, 1)
  const [r, g, b, a] = context.getImageData(0, 0, 1, 1).data
  return [r, g, b, a / 255]
}

function over(top: Rgba, bottom: Rgba): Rgba {
  const [r, g, b] = [0, 1, 2].map((i) => top[i] * top[3] + bottom[i] * (1 - top[3]))
  return [r, g, b, 1]
}

function backgroundOf(element: Element): Rgba {
  const layers: Rgba[] = []
  for (let node: Element | null = element; node; node = node.parentElement) {
    const layer = painted(getComputedStyle(node).backgroundColor)
    if (layer[3] > 0) layers.unshift(layer)
    if (layer[3] === 1) break
  }
  return layers.reduce<Rgba>((bottom, layer) => over(layer, bottom), [255, 255, 255, 1])
}

function luminance([r, g, b]: Rgba): number {
  const [lr, lg, lb] = [r, g, b].map((v) => {
    const c = v / 255
    return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4
  })
  return 0.2126 * lr + 0.7152 * lg + 0.0722 * lb
}

function ratio(a: Rgba, b: Rgba): number {
  const [high, low] = [luminance(a), luminance(b)].sort((x, y) => y - x)
  return Math.round(((high + 0.05) / (low + 0.05)) * 100) / 100
}

/** The contrast ratio of the text color of `element` on the stack of backgrounds under it, rounded to 2 places. */
export function contrastOf(element: Element): number {
  const background = backgroundOf(element)
  return ratio(over(painted(getComputedStyle(element).color), background), background)
}

// The color of the last box-shadow layer that has a spread, which is the Tailwind ring. A ring offset comes before it.
// The computed value puts the color first in each layer, for example `oklab(0.5 0.1 0.1 / 0.8) 0px 0px 0px 3px`.
function ringColorOf(element: Element): string {
  const layers = getComputedStyle(element).boxShadow.split(/,(?![^(]*\))/)
  const spreads = layers.filter((layer) => /0px 0px 0px [1-9][\d.]*px\s*$/.test(layer))
  const ring = spreads[spreads.length - 1]
  if (!ring) throw new Error('The element shows no focus ring.')
  return ring.trim().replace(/(\s+-?[\d.]+px)+$/, '')
}

/**
 * The lower contrast ratio of the focus ring of `element` against its two neighbors: the background outside the
 * element and the background of the element. WCAG 1.4.11 asks 3:1 against the adjacent colors.
 */
export function focusRingContrastOf(element: Element): number {
  const outside = backgroundOf(element.parentElement ?? document.body)
  const ring = over(painted(ringColorOf(element)), outside)
  return Math.min(ratio(ring, outside), ratio(ring, backgroundOf(element)))
}
