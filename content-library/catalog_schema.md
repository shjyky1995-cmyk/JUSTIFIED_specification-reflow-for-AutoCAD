# 内容包格式与核定状态

`schemaVersion=1`。内容包含 `chapters`、`fields`、`clauses`、`sourceDigest`。`packageId` 带内容哈希；资料或修正变化会产生新版本 ID。章节 ID 沿用交接包 `CH01–CH25`，条款 ID 沿用 `C0001` 等，字段 ID 统一同义名后保持稳定。条款保留原模板、来源、规范引用和整理备注；旧工程例值不进入应用内容包。

每条 `clause` 至少含 `id`、`chapterId`、`disciplines`、`kind`、`template`、`fieldIds`、`sources`、`refs`、`reviewStatus`、`flags`、`usableAsText`。`disciplines` 中 `process` 代表水处理工艺来源，在给排水专业的候选面板中单列提示；不能据此认定所有给排水工程都适用。跨专业来源保留多个专业码。

`reviewStatus` 当前全部为 `pending`。人工在**当前说明**中确认某条适用，只更新该说明的快照标记，不将该条提升为全局批准正文。全局批准还需核对原件、工程适用条件、规范现行性与使用权限，并记录核定人、日期和内容版本。不能用出现频率替代专业审查。

`flags` 用于定位风险：`reference`（规范引用）、`condition`（条件未结构化）、`table`（当前编辑器不支持）、`source_note`（整理备注）、`possible_project_literal`（疑似残留旧工程名称）、`field_alias_normalized`（同义字段已统一）。这些标记不自动证明内容有错；复核后在审核记录中处理。`usableAsText=false` 的表格只保留在私有候选包，不可直接加入纯文本说明。

工程字段没有旧项目默认值。用户在每份说明填写的 `fieldValues` 独立保存，预览展示未填提示，导出前阻断未填变量。用户手写的章节正文仍可自由编辑，与候选条款快照分开。内容包更新不静默修改已有草稿。
