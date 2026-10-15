# Versioned development

One Unity project and one source tree are maintained. Git snapshots carry all code
and authored assets; the folders below contain release records, not copied projects.

| Version | Status | Record |
| --- | --- | --- |
| V0.10 baseline | Frozen at `63946b1`, tag `v0.10.0-baseline` | [V0.10](V0.10/README.md) |
| V0.10 integration | In progress on `codex/v0.10` | [Plan](V0.10/PLAN.md) |

Frozen tags are immutable. Future work begins from the last delivery and gets a new
version record. Known failed checks stay visible rather than being hidden by a version label.

## 中文

工程只保留一套源码目录，不每版复制整份项目。Git快照保存全部代码和已跟踪源资产；
版本文件夹集中保存模型／动作清单、修改记录、测试、已知问题和打开入口。
冻结标签不可覆盖；后续在新版本继续，旧地图和旧资产仍可追溯。
