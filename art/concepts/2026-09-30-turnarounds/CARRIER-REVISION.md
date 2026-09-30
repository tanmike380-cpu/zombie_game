# 投尸巨尸修订 · 2026-09-30

使用内置 image_gen 编辑已有三视图，非 CLI。编辑输入是 Git 提交 `4ee1e74` 中的 `zombies--07-carrier.png`，不是重画其他六种僵尸；当前同名文件已覆盖为新版。

用户要求：姿态比旧版更佝偻、手臂更长、长方形背笼改成正方形。保留原有肤色、衣物、长发小头、麻绳与木材风格。背笼仍为空，不把被投掷的小僵尸融合进底模。

完整实际提示词已更新到 [provenance.json](provenance.json) 的 `zombies/07-carrier` 记录。该记录的 `reference` 是原始角色设计参考；本次工具的直接编辑输入为上面指定的历史三视图。

交付为 [正面](../tripo/zombies/07-carrier/front.png)、[侧面](../tripo/zombies/07-carrier/side.png)、[背面](../tripo/zombies/07-carrier/back.png) 三张独立 PNG。采用不等宽裁切边界 0／510／1075／1536，避免侧面前伸的头部被三等分裁切切掉。原合并图仅作生成底稿。
