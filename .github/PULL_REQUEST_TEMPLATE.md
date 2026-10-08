## 这个 PR 做了什么

<!-- 一两句话。 -->

## 怎么验证的

<!-- 必填。这个项目的规矩是"改了就要有证据"： -->

- [ ] Unity 里 **EditMode 测试 84/84 通过**（新增测试的话说明加了几条）
- [ ] 控制台**无报错**（`refresh_unity` 后 `read_console` 过滤 error 为空）
- [ ] 需要看画面的改动**贴了截图**（前后对比更好）
- [ ] 改了 `PetRoom` 的摆放逻辑 → 跑过 `Tools/DSH Pet/Build Pet Scene` 并保存场景

## 检查清单

- [ ] 改了含中文的 `.cs` → 跑过 `Tools/DSH Pet/Fix Script Encodings`（补 UTF-8 BOM）
- [ ] 没有把本机的绝对路径（`D:\...`）写进要提交的文件
- [ ] 改了行为/界面 → 更新了相关文档（`docs/REQUIREMENTS.md` / `CHANGELOG.md` / `PET_DESIGN.md`）
- [ ] 没有提交 `Library/`、`Temp/`、`logs/` 之类的生成物

## 相关 issue

<!-- 例如 Closes #12 -->
