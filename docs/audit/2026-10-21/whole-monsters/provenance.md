# 素材来源与处理

正式图片直接提取 Assets/示意图/怪物实例.jpg，原图 SHA256 为 2b874418ff1ea102b46de061c93c9aba284fd9aa4f3b72712173cc985202e92f。原始 JPG 保持不变。

内置 imagegen 曾用于整张去背试稿，使用 background-extraction 提示，要求仅去掉中灰背景、保留全部十二个怪物的位置、比例、原色、眼睛、牙齿、针脚与黑色轮廓，并输出透明背景。试稿出现细节重绘与边缘噪点，已弃用。正式玩家素材没有引用该生成结果。

最终处理通过 Unity CLI 执行 MonsterArtIntegration.Apply。裁剪框、去背、内部空隙、边缘去灰和白色细节保护都写在 Assets/Editor/MonsterArtIntegration.cs。渐变默认值在 MonsterTemplateDefaults.cs。导入输出 12 张透明原稿与 72 张渐变图片，正式目录为 Assets/Arts/Monsters/Whole。

原稿像素、透明轮廓与黑线的独立检查由 scripts/audit-whole-monster-art.py 执行。运行截图、真实拖放和培育证据另见同目录验收 JSON。局部注入资源的验收不代表自然七天通关。
