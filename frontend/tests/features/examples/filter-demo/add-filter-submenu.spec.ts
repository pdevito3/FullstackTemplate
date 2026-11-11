import { test, expect } from '@tests/support/fixtures'
import { filterDemoPage } from './filter-demo.tasks'

// Each path starts on a date property and crosses other properties on its way to the first date field of the submenu.
// Up: the submenu moves up to fit on the screen, so its date field is above the property.
const straightPaths = [
  { screen: { width: 1280, height: 800 }, property: 'Due Date', scroll: 0, rises: true },
  { screen: { width: 1280, height: 800 }, property: 'Due Date', scroll: 400, rises: false },
  { screen: { width: 1024, height: 700 }, property: 'Created Date', scroll: 0, rises: true },
  { screen: { width: 1920, height: 1080 }, property: 'Starts At in Pago Pago', scroll: 0, rises: true },
]

for (const { screen, property, scroll, rises } of straightPaths) {
  test.describe(`the ${property} submenu on a ${screen.width} x ${screen.height} desktop`, () => {
    test.use({ viewport: screen })

    test(`stays open while the mouse moves straight ${rises ? 'up' : 'down'} to its date field over other properties`, async ({ page }) => {
      const demo = filterDemoPage(page)
      await demo.open()
      await demo.at.addFilterMenu.click()
      if (scroll) await demo.scrollPropertyList(scroll, property)
      await demo.hoverProperty(property)
      const field = demo.at.dateSegment(property, 'Date', 'month')
      const path = await demo.pathFrom(demo.at.property(property), field)
      expect(path.rises, 'the direction of the path').toBe(rises)
      expect(path.crossed.length, 'the properties that the path crosses').toBeGreaterThan(0)

      await demo.moveMouseStraightTo(path.to)
      await demo.clickMouse()

      await expect(field).toBeFocused()
    })
  })
}

test.describe('the submenu of a property on a desktop', () => {
  test.use({ viewport: { width: 1280, height: 800 } })

  test('closes when the mouse moves to another property', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()
    await demo.at.addFilterMenu.click()
    await demo.hoverProperty('Created Date')

    await demo.hoverProperty('Updated Date')

    await expect(demo.at.control('Created Date')).toBeHidden()
  })

  test('opens the property where the mouse stops on its way to the submenu', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()
    await demo.at.addFilterMenu.click()
    await demo.hoverProperty('Due Date')
    const path = await demo.pathFrom(demo.at.property('Due Date'), demo.at.dateSegment('Due Date', 'Date', 'month'))
    const [stop] = path.crossed

    await demo.moveMouseStraightTo(stop.point)

    await expect(demo.at.control(stop.name)).toBeVisible()
    await expect(demo.at.control('Due Date')).toBeHidden()
  })

  // A text property opens its dialog on a click, not on a hover.
  test('opens the property that the mouse clicks on its way to the submenu', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()
    await demo.at.addFilterMenu.click()
    await demo.hoverProperty('Labels')

    await demo.moveMouseStraightTo(await demo.rightEndOf('Email'))
    await demo.clickMouse()

    await expect(demo.at.control('Email')).toBeVisible()
    await expect(demo.at.control('Labels')).toBeHidden()
  })

  test('lets the wheel scroll the properties under the mouse, and the property that comes under it takes the hover', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()
    await demo.at.addFilterMenu.click()
    await demo.hoverProperty('Created Date')

    for (const scrollTop of [100, 200]) {
      await page.mouse.wheel(0, 100)
      await expect.poll(() => demo.scrollTopOfPropertyList()).toBe(scrollTop)
    }

    await expect(demo.at.control('Created Date')).toBeHidden()
  })

  test('closes with Escape', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()
    await demo.at.addFilterMenu.click()
    await demo.hoverProperty('Created Date')

    await page.keyboard.press('Escape')

    await expect(demo.at.control('Created Date')).toBeHidden()
  })

  test('closes after a click outside the menu', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()
    await demo.at.addFilterMenu.click()
    await demo.hoverProperty('Created Date')

    await demo.clickMouseAt(demo.at.heading)

    await expect(demo.at.control('Created Date')).toBeHidden()
    await expect(demo.at.propertyList).toBeHidden()
  })
})
