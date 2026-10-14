// 版本化工程规则数据。数值来自 GB/T 50046-2018 表 4.2.3、4.2.5、4.8.5-1。
// 不适用于微腐蚀、预应力、桩基、非 50 年设计；池体内介质另核定。
export const CORROSION_RULE_VERSION = 'gbt50046-2018-desktop-1'
export const CORROSION_SOURCES = {
  materials: 'https://gf.cabr-fire.com/m/article-62023.htm',
  foundation: 'https://gf.cabr-fire.com/m/article-62054.htm',
  pool: 'https://gf.cabr-fire.com/m/article-62167.htm',
}
export type CorrosionRule = { grade: number; binder: number; ratio: number; chloride: number; alkali: number; flatCover: number; barCover: number; foundationCover: number; coating: string }
export const CORROSION_RULES: Record<string, CorrosionRule> = {
  '弱': { grade: 30, binder: 300, ratio: 0.50, chloride: 0.10, alkali: 3.5, flatCover: 30, barCover: 35, foundationCover: 50, coating: '沥青冷底子油两遍及厚度不小于300μm的沥青胶泥涂层' },
  '中': { grade: 35, binder: 320, ratio: 0.45, chloride: 0.10, alkali: 3.0, flatCover: 30, barCover: 35, foundationCover: 50, coating: '厚度不小于300μm的环氧沥青或聚氨酯沥青涂层' },
  '强': { grade: 40, binder: 340, ratio: 0.40, chloride: 0.08, alkali: 3.0, flatCover: 35, barCover: 40, foundationCover: 50, coating: '厚度不小于500μm的环氧沥青或聚氨酯沥青涂层' },
}
