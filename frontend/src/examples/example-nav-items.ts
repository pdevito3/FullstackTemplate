export const exampleItems = [
  {
    title: "Weather",
    url: "/weather",
  },
  {
    title: "Components",
    url: "/components",
  },
  {
    title: "Filter Builder",
    url: "/filter-demo",
  },
  {
    title: "Workshops",
    url: "/workshops",
  },
  {
    title: "New venue",
    url: "/venues/new",
  },
] as const

export const exampleNavUrls = exampleItems.map((item) => item.url)
