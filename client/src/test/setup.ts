import {vi} from 'vitest'

// Unovis ritar SVG och mäter text via getBBox, vilket jsdom inte stödjer. Testerna bryr sig om
// innehållet runt graferna, så vi ersätter ritkomponenterna med enkla behållare.
vi.mock('@unovis/vue', () => {
  const container = {
    template: '<div data-test="chart-canvas"><slot/></div>',
  }
  const part = {
    template: '<div/>',
  }
  return {
    VisSingleContainer: container,
    VisXYContainer: container,
    VisDonut: part,
    VisGroupedBar: part,
    VisStackedBar: part,
    VisAxis: part,
    VisTooltip: part,
    VisCrosshair: part,
  }
})
