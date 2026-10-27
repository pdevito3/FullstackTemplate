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

/** The contrast ratio of the text color of `element` on the stack of backgrounds under it, rounded to 2 places. */
export function contrastOf(element: Element): number {
  const background = backgroundOf(element)
  const text = over(painted(getComputedStyle(element).color), background)
  const [high, low] = [luminance(text), luminance(background)].sort((a, b) => b - a)
  return Math.round(((high + 0.05) / (low + 0.05)) * 100) / 100
}
